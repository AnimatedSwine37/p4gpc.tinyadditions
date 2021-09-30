using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using Reloaded.Mod.Interfaces;
using p4gpc.tinyadditions.Configuration;
using Reloaded.Memory.Sources;
using System.Diagnostics;
using Reloaded.Hooks.Definitions;
using System.Runtime.InteropServices;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;
using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;

namespace p4gpc.tinyadditions
{
    class Twitch
    {
        // <summary>
        // login_name is the username that the bot will use to read messages
        // token is generated from https://twitchapps.com/tmi/ based on the user
        // channels_to_join is the list of channels that messages will be read from
        // The majority of this code is sourced from https://github.com/twitchdev/chatbot-csharp-sample/blob/main/Program.cs
        // </summary>

        private string _modDirectory;
        private ILogger _logger;

        private IReloadedHooks _hooks;
        private IReverseWrapper<ChatInput> _chatReverseWrapper;
        private IAsmHook _twitchHook;


        private System.Threading.Timer _timer;
        // To read and write memory (let Twitch chat control the game)
        private IMemory _memory = new Memory();
        // Set a base address - this should usually be 0x4000000
        private int _baseAddress;
        // Mod configuration        
        private Utils _utils;

        public Config _config { get; set; }
        public Twitch (IReloadedHooks hooks, Utils utils, Config configuration, string modDirectory)
        {
            // init private variables
            _config = configuration;
            _utils = utils;
            _hooks = hooks;
            _utils.Log("Initialising connection to Twitch");
            _memory = new Memory();
            using var thisProcess = Process.GetCurrentProcess();
            _baseAddress = thisProcess.MainModule.BaseAddress.ToInt32();
            _modDirectory = modDirectory;

            int botRes = 0;

            try
            {
                // Start up twitch bot
                // Create a list of TwitchChat objects for each channel
                List<TwitchChat> chatBots = new List<TwitchChat>();
                // add channels to the list
                chatBots.Add(new TwitchChat(configuration.TwitchUsername, configuration.OAuthToken, configuration.ChannelConnection));
                // Connect chatbot to the channel and setup a pinger to maintain connection
                TwitchChat chatBot = chatBots[0];
                chatBot.Connect();
                Pinger pinger = new Pinger(chatBot);
                pinger.Start();
                // Assembly code that injects into the routine to control the game
                string[] twitchControl =
                    {
                        $"use32",
                        $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                        $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                        $"mov edi, {botRes}",
                    };
                _twitchHook = hooks.CreateAsmHook(twitchControl, _baseAddress + 0x27076A9C, AsmHookBehaviour.ExecuteFirst).Activate();
                // Scanning for instruction to write to cbt
                // Fetch instruction that the jump command refers to
                _memory.SafeRead((IntPtr)(_baseAddress + 0x27076A9C + 2), out uint addressScan);
                _utils.Log($"{addressScan}");
                string addressScanHex = Convert.ToString(addressScan, toBase: 16);
                _utils.Log($"{addressScanHex}");
                _memory.SafeRead((IntPtr)addressScan, out int addressScan1);
                _utils.Log($"{addressScan1}");
                string addressScanHex1 = Convert.ToString(addressScan1, toBase: 16);
                _utils.Log($"{addressScanHex1}");
                _memory.SafeRead((IntPtr)(addressScan1 - 42), out int addressScan2); // This is the instruction that we will control using the power of Twitch

                // Run this script for the entire time P4G is open
                _timer = new System.Threading.Timer(OnTick, null, 0, 20);
                void OnTick(object state)
                {
                    TwitchChat chatBot = chatBots[0];
                    if (!chatBot.Client.Connected)
                    {
                        // disconnected, try to reconnect
                        chatBot.Connect();
                    }
                    else
                    {
                        // bot is connected, read message
                        string message = chatBot.ReadMessage();
                        string botResAsString = "";
                        string finalResultHex = "";
                        int actuallyControlTheGameChat = 0;
                        if (message != "" && message != null)
                        {
                            // send message to console
                            _utils.Log(message);
                            // trim message to just the chat
                            string messageTrimmed = trimMessage(message);
                            _utils.Log(messageTrimmed);
                            botRes = chatBot.PlayP4GPoggies(messageTrimmed);
                            _utils.Log($"{botRes}");
                            if (botRes != 0)
                            {
                                string[] twitchControl =
                                {
                                    $"use32",
                                    $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                                    $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                                    $"mov edi, {botRes}",
                                };
                                for (int j = 0; j < twitchControl.Length; j++)
                                {
                                    _utils.LogDebug(twitchControl[j]);

                                }
                                _twitchHook.Enable();
                                _memory.SafeRead((IntPtr)(addressScan1 - 42), out int addresses);
                                _utils.Log($"{Convert.ToString(addresses, toBase: 16)}");
                                botResAsString = Convert.ToString(botRes, toBase: 16);
                                finalResultHex = $"{botResAsString}BF";
                                actuallyControlTheGameChat = int.Parse(finalResultHex, System.Globalization.NumberStyles.HexNumber);
                                _memory.SafeWrite((IntPtr)(addressScan1 - 42), actuallyControlTheGameChat);
                            } else
                            {
                                _twitchHook.Disable(); // turn off your keyboard lmao
                                _memory.SafeRead((IntPtr)(addressScan1 - 42), out int addresses);
                                _utils.Log($"{Convert.ToString(addresses, toBase: 16)}");
                            }
                        }
                    }
                }
            } catch (Exception e)
            {
                _utils.LogError($"Error connecting to Twitch", e);
            }
        }
        public static string trimMessage(string message)
        {
            int indexOfSecondColon = getNthIndex(message, ':', 2);
            var result = message.Substring(indexOfSecondColon + 1);
            return result;
        }
        public static int getNthIndex(string s, char t, int n)
        {
            int count = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == t)
                {
                    count++;
                    if (count == n)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }
        private void ChatdidanInput (int edi)
        {
            _memory.SafeRead((IntPtr)edi, out byte addressStuff);
            _utils.Log($"{addressStuff}");
            // _memory.SafeWrite(edi, (IntPtr)10);
        }
        [Function(Register.edi, Register.edi, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ChatInput(int input);
    }
}
