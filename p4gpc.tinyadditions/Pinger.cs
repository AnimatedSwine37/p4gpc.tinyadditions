using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;

namespace p4gpc.tinyadditions
{
    public class Pinger
    {
        public TwitchChat myClient;
        public Thread pingSender;
        public Pinger(TwitchChat myClient)
        {
            this.myClient = myClient;
            pingSender = new Thread(new ThreadStart(this.Run));
        }
        public void Start()
        {
            pingSender.IsBackground = true;
            pingSender.Start();
        }
        public void Run()
        {
            while (true)
            {
                myClient.SendPing();
                Thread.Sleep(300000); // 5 minutes
            }
        }
    }
}
