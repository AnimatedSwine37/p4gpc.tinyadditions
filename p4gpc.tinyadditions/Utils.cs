using p4gpc.tinyadditions.Configuration;
using Reloaded.Memory.Sigscan;
using Reloaded.Mod.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public void LogDebug(string message)
        {
            if(Configuration.DebugEnabled) 
                _logger.WriteLine($"[TinyAdditions] {message}");
        }

        public void Log(string message)
        {
            _logger.WriteLine($"[TinyAdditions] {message}");
        }

        public void LogError(string message, Exception e)
        {
            _logger.WriteLine($"[TinyAdditions] {message}: {e.Message}", System.Drawing.Color.Red);
        }

        // Signature Scans for a location in memory, returning -1 if the scan fails otherwise the address
        public long SigScan(string pattern, int baseAddress, string functionName)
        {
            try
            {
                using var thisProcess = Process.GetCurrentProcess();
                using var scanner = new Scanner(thisProcess, thisProcess.MainModule);
                long functionAddress = scanner.CompiledFindPattern(pattern).Offset + baseAddress;
                LogDebug($"Found the {functionName} address at 0x{functionAddress:X}");
                return functionAddress;
            }
            catch (Exception exception)
            {
                LogError($"An error occured trying to find the {functionName} function address. Not initializing. Please report this with information on the version of P4G you are running.", exception);
                return -1;
            }
        }
    }
}
