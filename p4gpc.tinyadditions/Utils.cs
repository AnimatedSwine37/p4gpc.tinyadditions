using p4gpc.tinyadditions.Configuration;
using Reloaded.Memory.Sigscan;
using Reloaded.Memory.Sources;
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
        private int _baseAddress;
        private IMemory _memory;
        private IntPtr _flagLocation;
        private IntPtr _eventLocation;
        public Utils(Config configuration, ILogger logger, int baseAddress, IMemory memory)
        {
            // Initialise fields
            Configuration = configuration;
            _logger = logger;
            _baseAddress = baseAddress;
            _memory = memory;

            // Get flag base location
            long flagPointer = SigScan("68 ?? ?? ?? ?? 56 E8 ?? ?? ?? ?? 83 C4 0C 81 C6 40 03 00 00", "flag pointer");
            if (flagPointer != -1)
            {
                try
                {
                    _memory.SafeRead((IntPtr)flagPointer + 1, out _flagLocation);
                    LogDebug($"The flags start at 0x{_flagLocation:X}");
                }
                catch (Exception e)
                {
                    LogError("Unable to read flag start location", e);
                }
            }
            // Get event major and minor location
            long eventPointer = SigScan("A3 ?? ?? ?? ?? 8B 85 ?? ?? ?? ?? 66 89 0D ?? ?? ?? ??", "event pointer");
            if (eventPointer != -1)
            {
                try
                {
                    _memory.SafeRead((IntPtr)eventPointer + 1, out _eventLocation);
                    LogDebug($"The current event is at 0x{_eventLocation:X}");
                }
                catch (Exception e)
                {
                    LogError("Unable to read event location", e);
                }
            }
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
            if (Configuration.DebugEnabled)
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
        public long SigScan(string pattern, string functionName)
        {
            try
            {
                using var thisProcess = Process.GetCurrentProcess();
                using var scanner = new Scanner(thisProcess, thisProcess.MainModule);
                long functionAddress = scanner.CompiledFindPattern(pattern).Offset + _baseAddress;
                LogDebug($"Found the {functionName} address at 0x{functionAddress:X}");
                return functionAddress;
            }
            catch (Exception exception)
            {
                LogError($"An error occured trying to find the {functionName} function address. Not initializing. Please report this with information on the version of P4G you are running.", exception);
                return -1;
            }
        }

        // Gets the value of a specified flag
        public bool CheckFlag(int flag)
        {
            if (_flagLocation == IntPtr.Zero) return false;
            try
            {
                _memory.SafeRead(_flagLocation + flag / 8, out byte flagByte);
                int flagMask = 1 << (flag % 8);
                return (flagByte & (flagMask)) == flagMask;
            }
            catch (Exception e)
            {
                LogError($"Unable to check flag {flag}", e);
                return false;
            }
        }

        // Turns a specified flag on or off
        public void ChangeFlag(int flag, bool state)
        {
            if (_flagLocation == IntPtr.Zero) return;
            try
            {
                _memory.SafeRead(_flagLocation + flag / 8, out byte flagByte);
                if (state == true)
                {
                    flagByte |= (byte)(1 << (flag % 8));
                }
                else
                {
                    flagByte &= (byte)~(1 << (flag % 8));
                }
                _memory.SafeWrite(_flagLocation + flag / 8, ref flagByte);
            }
            catch (Exception e)
            {
                LogError($"Unable to change flag {flag}", e);
            }
        }

        // Checks if the player is currently in an event
        public bool InEvent()
        {
            if (_eventLocation == IntPtr.Zero) return false;
            // Get the current event
            _memory.SafeRead(_eventLocation, out short[] currentEvent, 3);
            // If either the event major or minor isn't 0 we are in an event otherwise we're not
            return currentEvent[0] != 0 || currentEvent[2] != 0;
        }

    }
}
