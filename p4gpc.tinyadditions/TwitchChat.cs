using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;

namespace p4gpc.tinyadditions
{
    public class TwitchChat
    {
        // <summary>
        // Based heavily on TwitchChatBot.cs on C# chatbot script example - https://github.com/twitchdev/chatbot-csharp-sample
        // </summary>
        public TcpClient Client;
        public StreamReader Reader;
        public StreamWriter Writer;
        private string Login { get; set; }
        private string Token { get; set; }
        private string Channel { get; set; }

        public TwitchChat(string login, string token, string channel)
        {
            this.Login = login;
            this.Token = token;
            this.Channel = channel;
        }
        // Make an IRC connection to Twitch
        public void Connect()
        {
            Client = new TcpClient("irc.twitch.tv", 6667);
            Reader = new StreamReader(Client.GetStream());
            Writer = new StreamWriter(Client.GetStream());

            Writer.WriteLine("PASS " + Token);
            Writer.WriteLine("NICK " + Login);
            Writer.WriteLine("USER " + Login + " 8 * :" + Login);
            Writer.WriteLine("JOIN #" + Channel);
            Writer.Flush();
        }
        // Read a message from the chat of the channel the program is connected to
        public string ReadMessage()
        {
            string chat_message = Reader.ReadLine();
            return chat_message;
        }
        // Send a message in the chat of the channel the program is connected to
        public void SendMessage(string message)
        {
            string toSend = (":" + Login + "!" + Login + "@" + Login +
            ".tmi.twitch.tv PRIVMSG #" + Channel + " :" + message);
            Writer.WriteLine(toSend);
            Writer.Flush();
        }
        // Send a ping to keep connection
        public void SendPing()
        {
            Console.WriteLine("Sending PING....");
            Writer.WriteLine("PING :irc.twitch.tv");
            Writer.Flush();
        }
        // Send a pong to respond to the ping
        public void SendPong()
        {
            Console.WriteLine("Sending PONG....");
            Writer.WriteLine("PONG :irc.twitch.tv");
            Writer.Flush();
        }
        // Handles text to control P4G
        public int PlayP4GPoggies(string text)
        {
            int res;
            switch (text)
            {
                case string inputText when (string.Equals(inputText, "Select", StringComparison.CurrentCultureIgnoreCase)):
                    res = 1;
                    break;
                case string inputText when (string.Equals(inputText, "Start", StringComparison.CurrentCultureIgnoreCase)):
                    res = 8;
                    break;
                case string inputText when (string.Equals(inputText, "Up", StringComparison.CurrentCultureIgnoreCase)):
                    res = 16;
                    break;
                case string inputText when (string.Equals(inputText, "Right", StringComparison.CurrentCultureIgnoreCase)):
                    res = 32;
                    break;
                case string inputText when (string.Equals(inputText, "Down", StringComparison.CurrentCultureIgnoreCase)):
                    res = 64;
                    break;
                case string inputText when (string.Equals(inputText, "Left", StringComparison.CurrentCultureIgnoreCase)):
                    res = 128;
                    break;
                case string inputText when (string.Equals(inputText, "LB", StringComparison.CurrentCultureIgnoreCase)):
                    res = 1024;
                    break;
                case string inputText when (string.Equals(inputText, "RB", StringComparison.CurrentCultureIgnoreCase)):
                    res = 2048;
                    break;
                case string inputText when (string.Equals(inputText, "Triangle", StringComparison.CurrentCultureIgnoreCase)):
                    res = 4096;
                    break;
                case string inputText when (string.Equals(inputText, "Circle", StringComparison.CurrentCultureIgnoreCase) || string.Equals(inputText, "O", StringComparison.CurrentCultureIgnoreCase)):
                    res = 8192;
                    break;
                case string inputText when (string.Equals(inputText, "Cross", StringComparison.CurrentCultureIgnoreCase) || string.Equals(inputText, "X", StringComparison.CurrentCultureIgnoreCase)):
                    res = 16384;
                    break;
                case string inputText when (string.Equals(inputText, "Square", StringComparison.CurrentCultureIgnoreCase)):
                    res = 32768;
                    break;
                default:
                    res = 0;
                    break;
            }
            return res;
        }
        public int BattleMenu(string text)
        {
            int res;
            switch (text)
            {
                case string inputText when (string.Equals(inputText, "Analysis", StringComparison.CurrentCultureIgnoreCase)):
                    res = 1;
                    break;
                case string inputText when (string.Equals(inputText, "Tactics", StringComparison.CurrentCultureIgnoreCase)):
                    res = 2;
                    break;
                case string inputText when (string.Equals(inputText, "Guard", StringComparison.CurrentCultureIgnoreCase)):
                    res = 3;
                    break;
                case string inputText when (string.Equals(inputText, "Attack", StringComparison.CurrentCultureIgnoreCase)):
                    res = 4;
                    break;
                case string inputText when (string.Equals(inputText, "Skill", StringComparison.CurrentCultureIgnoreCase)):
                    res = 5;
                    break;
                case string inputText when (string.Equals(inputText, "Persona", StringComparison.CurrentCultureIgnoreCase)):
                    res = 6;
                    break;
                case string inputText when (string.Equals(inputText, "Item", StringComparison.CurrentCultureIgnoreCase)):
                    res = 7;
                    break;
                case string inputText when (string.Equals(inputText, "Escape", StringComparison.CurrentCultureIgnoreCase)):
                    res = 8;
                    break;
                default:
                    res = 0;
                    break;
            }
            return res;
        }
    }
}
