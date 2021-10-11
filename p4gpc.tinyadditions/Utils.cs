using p4gpc.tinyadditions.Configuration;
using Reloaded.Mod.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace p4gpc.tinyadditions
{
    public class Utils
    {
        public Config Configuration;
        private ILogger _logger;
        public Utils(Config configuration, ILogger logger)
        {
            Configuration = configuration;
            _logger = logger;
        }

        public enum Input
        {
            Select = 0x1,
            Start = 0x8,
            Up = 0x10,
            Right = 0x20,
            Down = 0x40,
            Left = 0x80,
            LB = 0x400,
            RB = 0x800,
            Triangle = 0x1000,
            Circle = 0x2000,
            Cross = 0x4000,
            Square = 0x8000
        };
        public enum BattleMenu
        {
            Analysis = 1,
            Tactics = 2,
            Guard = 3,
            Attack = 4,
            Skill = 5,
            Persona = 6,
            Item = 7,
            Escape = 8
        };

        public void LogDebug(string message)
        {
            if(Configuration.DebugEnabled) 
                _logger.WriteLine($"[TwitchPlays] {message}");
        }

        public void Log(string message)
        {
            _logger.WriteLine($"[TwitchPlays] {message}");
        }

        public void LogError(string message, Exception e)
        {
            _logger.WriteLine($"[TwitchPlays] {message}: {e.Message}", System.Drawing.Color.Red);
        }
    }
}
