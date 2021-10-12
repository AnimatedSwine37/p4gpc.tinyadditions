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
using System.Threading;
using System.Globalization;
using static p4gpc.tinyadditions.Utils;

namespace p4gpc.tinyadditions
{
    public class Twitch
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
        private IReverseWrapper<BattleInput> _battleReverseWrapper;
        private IReverseWrapper<BattleMenuSelection> _battleMenuReverseWrapper;
        private IAsmHook _twitchHook;
        private IAsmHook _battleHook;
        private IAsmHook _battleMenuHook;


        private System.Threading.Timer _timer;
        // To read and write memory (let Twitch chat control the game)
        private IMemory _memory = new Memory();
        // Set a base address - this should usually be 0x4000000
        private int _baseAddress;
        // Utilities (input enum is stored there)      
        private Utils _utils;

        // Stuff related to Config files
        public Config _config { get; set; }

        // inputs
        public int botRes = 0;

        // battle inputs
        public int battleRes = 0;
        List<int> battleAction = new List<int>();
        public int highlightedAction = 0;

        // frame
        public int frame = 0;
        public int tickAmount = 50;

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

            tickAmount = configuration.TickSpeed;

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
                string[] battleControl =
                {
                    $"use32",
                    $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                    $"{hooks.Utilities.GetAbsoluteCallMnemonics(AnalysisOrAttackInputHappened, out _battleReverseWrapper)}",
                    $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                };
                string[] battleMenuControl =
                {
                    $"use32",
                    $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                    $"{hooks.Utilities.GetAbsoluteCallMnemonics(MenuHighlighted, out _battleMenuReverseWrapper)}",
                    $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                };
                // _battleHook = hooks.CreateAsmHook(battleControl, _baseAddress + 0x21BC4E30, AsmHookBehaviour.ExecuteAfter).Activate();
                _battleMenuHook = hooks.CreateAsmHook(battleMenuControl, _baseAddress + 0x225F190A, AsmHookBehaviour.ExecuteFirst).Activate();
                // Scanning for instruction to write to cbt
                // Fetch instruction that the jump command refers to

                _memory.SafeRead((IntPtr)(_baseAddress + 0x27076A9C + 2), out uint addressScan);
                _utils.LogDebug($"{addressScan}");
                string addressScanHex = Convert.ToString(addressScan, toBase: 16);
                _utils.LogDebug($"{addressScanHex}");
                _memory.SafeRead((IntPtr)addressScan, out int addressScan1);
                _utils.LogDebug($"{addressScan1}");
                string addressScanHex1 = Convert.ToString(addressScan1, toBase: 16);
                _utils.LogDebug($"{addressScanHex1}");
                _memory.SafeRead((IntPtr)(addressScan1 - 42), out int addressScan2); // This is the instruction that we will control using the power of Twitch

                // Run this script for the entire time P4G is open

                var _nohold = new Thread(NoHoldKey);
                var _tick = new Thread(RunOnTick);
                _tick.Start();
                _nohold.Start();
                void RunOnTick()
                {
                    var stopwatch = Stopwatch.StartNew();
                    while (true)
                    {
                        _utils.LogDebug($"Thread {Thread.CurrentThread.ManagedThreadId}: {stopwatch.ElapsedMilliseconds / 1000.0} seconds | {stopwatch.ElapsedTicks} ticks");
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
                            int actuallyControlTheGameChat = 0;
                            if (message != "" && message != null)
                            {
                                // send message to console
                                _utils.LogDebug(message);
                                // trim message to just the chat
                                string messageTrimmed = trimMessage(message);
                                _utils.LogDebug(messageTrimmed);
                                // get username of the user who posted
                                string usernamePoster = getUsername(message);
                                _utils.Log($"{DateTime.Now}: {usernamePoster} posted {messageTrimmed}");

                                // Read address of memory that states if you are in a battle
                                _memory.SafeRead((IntPtr)(_baseAddress + 0x21A967B0), out int inbattle);
                                if (inbattle != 0)
                                {
                                    // In battle, can accept other responses
                                    battleRes = chatBot.BattleMenu(messageTrimmed);
                                    
                                }
                                // interpret user message
                                botRes = chatBot.PlayP4GPoggies(messageTrimmed);

                                _utils.LogDebug($"Main control - {botRes}");
                                _utils.LogDebug($"Battle control - {battleRes}");
                                // Write the user's input into memory
                                if (botRes != 0 || battleRes != 0)
                                {
                                    frame = 0;
                                    if (botRes != 0) _utils.Log($"Input {messageTrimmed} read as {(Input)botRes}");
                                    if (battleRes != 0) _utils.Log($"Input {messageTrimmed} read as {(BattleMenu)battleRes}");
                                    string[] twitchControl =
                                    {
                                    $"use32",
                                    $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                                    $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                                    $"mov edi, {botRes}",
                                    };
                                    _twitchHook.Enable(); // This prevents the host from controlling the game directly
                                    _memory.SafeRead((IntPtr)(addressScan1 - 42), out int addresses);
                                    _utils.LogDebug($"{Convert.ToString(addresses, toBase: 16)}");
                                    if (botRes != 0)
                                    {
                                        botResAsString = Convert.ToString(botRes, toBase: 16);
                                        actuallyControlTheGameChat = int.Parse($"{botResAsString}BF", NumberStyles.HexNumber);
                                        _memory.SafeWrite((IntPtr)(addressScan1 - 42), actuallyControlTheGameChat);
                                    }

                                    // Read address of memory that detects what part of the in game menu you are in
                                    _memory.SafeRead((IntPtr)(_baseAddress + 0x9C65C0), out int inmenusection);
                                    // Read address of memory to find your ingame location
                                    _memory.SafeRead((IntPtr)(_baseAddress + 0x6FA558), out int location);
                                    if (inmenusection == 29 || location == 0)
                                    {
                                        // no.
                                        chatBot.SendMessage($"Nice try {usernamePoster} lol");
                                        _twitchHook.Disable();
                                    }
                                }
                                else
                                {
                                    _utils.Log($"Input {messageTrimmed} has no input");
                                    _utils.Log($"Input {messageTrimmed} read as {(BattleMenu)battleRes}");
                                    _twitchHook.Disable(); // If the input is invalid, reenable the host's keyboard (I promise there will be a better way to deal with this)
                                    _memory.SafeRead((IntPtr)(addressScan1 - 42), out int addresses);
                                    _utils.LogDebug($"{Convert.ToString(addresses, toBase: 16)}");
                                }
                            }
                        }
                        _utils.LogDebug($"Thread {Thread.CurrentThread.ManagedThreadId}: {stopwatch.ElapsedMilliseconds / 1000.0} seconds | {stopwatch.ElapsedTicks} ticks");
                        Thread.Sleep(tickAmount);
                    }
                }
                void NoHoldKey()
                {
                    var stopwatch = Stopwatch.StartNew();
                    while (true)
                    {
                        if (botRes != 0 || battleRes != 0)
                        {
                            _utils.LogDebug($"{frame}");
                            _utils.LogDebug($"Holding key {(Input)botRes} for {tickAmount} milliseconds");
                            _utils.LogDebug($"Thread {Thread.CurrentThread.ManagedThreadId}: {stopwatch.ElapsedMilliseconds / 1000.0} seconds | {stopwatch.ElapsedTicks} ticks");

                            Thread.Sleep(tickAmount);

                            // Read address of memory that contains the in game menu
                            _memory.SafeRead((IntPtr)(_baseAddress + 0x9C65D8), out int inmenu);
                            // Read address of memory to detect if you are interacting with something/using that side menu
                            _memory.SafeRead((IntPtr)(_baseAddress + 0x4A1CE08), out int menutalk);
                            // Read address of memory to find your ingame location
                            _memory.SafeRead((IntPtr)(_baseAddress + 0x6FA558), out int location);
                            // Read address of memory that states if you are in a battle
                            _memory.SafeRead((IntPtr)(_baseAddress + 0x21A967B0), out int inbattle);
                            if (battleRes == 0)
                            {
                                if (inmenu == 1 || menutalk == 1 || location == 0 || location == 1 || inbattle != 0)
                                {
                                    int hex = int.Parse("BF", NumberStyles.HexNumber);
                                    _memory.SafeWrite((IntPtr)(addressScan1 - 42), hex);
                                    _utils.LogDebug($"Thread {Thread.CurrentThread.ManagedThreadId}: {stopwatch.ElapsedMilliseconds / 1000.0} seconds | {stopwatch.ElapsedTicks} ticks");
                                    botRes = 0;
                                }
                            }
                            if (inbattle != 0 && battleRes != 0)
                            {
                                if (frame == 0)
                                {
                                    // Initialise list to reference for keyboard combination
                                    _utils.LogDebug($"Highlighted action - {highlightedAction}");
                                    int filteredHighlightedAction = highlightedAction;
                                    if (filteredHighlightedAction > 8) filteredHighlightedAction = 6; // Persona is read by the address as some really high value
                                    _utils.LogDebug($"Function from - {filteredHighlightedAction}");
                                    _utils.LogDebug($"Function to - {battleRes}");
                                    int battleSelectionDifference = filteredHighlightedAction - battleRes;
                                    _utils.LogDebug($"Selection Difference - {battleSelectionDifference}");
                                    bool bsdPositive = battleSelectionDifference > 0;
                                    if (battleRes == 1)
                                    {
                                        battleAction.Add(1024);
                                    } else if (battleRes == 6)
                                    {
                                        battleAction.Add(32768);
                                    } else
                                    {
                                        if (battleSelectionDifference != 0)
                                        {
                                            if (bsdPositive)
                                            {
                                                for (int i = 0; i < Math.Abs(battleSelectionDifference); i++)
                                                {
                                                    battleAction.Add(16);
                                                    battleAction.Add(0);
                                                }
                                            }
                                            else
                                            {
                                                for (int i = 0; i < Math.Abs(battleSelectionDifference); i++)
                                                {
                                                    battleAction.Add(64);
                                                    battleAction.Add(0);
                                                }
                                            }
                                        }
                                        battleAction.Add(8192);
                                    }
                                }
                                if (frame == battleAction.Count)
                                {
                                    int hex = int.Parse("BF", NumberStyles.HexNumber);
                                    _memory.SafeWrite((IntPtr)(addressScan1 - 42), hex);
                                    _utils.LogDebug($"Thread {Thread.CurrentThread.ManagedThreadId}: {stopwatch.ElapsedMilliseconds / 1000.0} seconds | {stopwatch.ElapsedTicks} ticks");
                                    botRes = 0;
                                    battleRes = 0;
                                    frame = 0;
                                    battleAction.Clear();
                                }
                                else
                                {
                                    string battleResAsString = Convert.ToString(battleAction[frame], toBase: 16);
                                    int hex = int.Parse($"{battleResAsString}BF", NumberStyles.HexNumber);
                                    _memory.SafeWrite((IntPtr)(addressScan1 - 42), hex);
                                    _utils.LogDebug($"Thread {Thread.CurrentThread.ManagedThreadId}: {stopwatch.ElapsedMilliseconds / 1000.0} seconds | {stopwatch.ElapsedTicks} ticks");
                                    botRes = battleAction[frame];
                                }

                            }

                            frame += 1;
                        }
                    }
                }
            }
            catch (Exception e)
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
        public string getUsername(string message)
        {
            try
            {
                int indexofHash = getNthIndex(message, '#', 1);
                int indexOfSecondColon = getNthIndex(message, ':', 2);
                var result = message.Substring((indexofHash + 1), (indexOfSecondColon - indexofHash - 2));
                return result;
            } catch (Exception e)
            {
                _utils.LogError($"Unable to get username from message", e);
                return message;
            }
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

        private void AnalysisOrAttackInputHappened (int input)
        {
            _utils.Log($"Input was {input}");
        }

        private void MenuHighlighted(int input)
        {
            // _utils.Log($"Menu selection is {input}");
            highlightedAction = input + 1;
        }

        [Function(Register.edi, Register.edi, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ChatInput(int input);

        [Function(Register.ebx, Register.ebx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void BattleInput(int input);

        [Function(Register.eax, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void BattleMenuSelection(int input);
    }
}
