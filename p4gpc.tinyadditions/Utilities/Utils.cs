﻿using p4gpc.tinyadditions.Configuration;
using Reloaded.Memory.Sigscan;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using p4gpc.tinyadditions.Utilities;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;

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
        private IntPtr _inMenuLocation;
        private IntPtr _itemLocation;
        private string _exeHash;
        private InternalConfig _internalConfig;
        private IStartupScanner? _scanner;

        public Utils(Config configuration, ILogger logger, int baseAddress, IMemory memory, InternalConfig internalConfig, IStartupScanner? startupScanner)
        {
            // Initialise fields
            _internalConfig = internalConfig;
            Configuration = configuration;
            _logger = logger;
            _baseAddress = baseAddress;
            _memory = memory;
            _scanner = startupScanner;

            _exeHash = GetExeHash();
            LogDebug($"The P4G.exe hash is {_exeHash}");

            // Initialise locations
            InitLocation("flag", "68 ?? ?? ?? ?? 56 E8 ?? ?? ?? ?? 83 C4 0C 81 C6 40 03 00 00", 1);
            InitLocation("event", "A3 ?? ?? ?? ?? 8B 85 ?? ?? ?? ?? 66 89 0D ?? ?? ?? ??", 1);
            InitLocation("in menu", "89 3D ?? ?? ?? ?? 89 1D ?? ?? ?? ?? A3 ?? ?? ?? ??", 2);
        }

        // Initialise the location to something in memory by sig scanning for a pointer
        public void InitLocation(string name, string signature, int pointerOffset)
        {
            SigScan(signature, $"{name} pointer",
                (result) =>
                {
                    try
                    {
                        _memory.SafeRead((IntPtr)result + pointerOffset, out IntPtr temp);
                        LogDebug($"The {name} is at 0x{temp:X}");
                        switch(name)
                        {
                            case "flag":
                                _flagLocation = temp;
                                break;
                            case "event":
                                _eventLocation = temp;
                                break;
                            case "in menu":
                                _inMenuLocation = temp;
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        LogError($"Unable to read {name} location", e);
                    }
                });
        }

        // Initialises the item location as right at startup the pointer does not exist
        // (called once an input is read)
        public void InitialiseItemLocation()
        {
            // Get item location
            long itemLocationPointer = SigScan("A1 ?? ?? ?? ?? 0F BF D6 ?? ?? C1 E1 04", "item pointer");
            if (itemLocationPointer != -1)
            {
                try
                {
                    _memory.SafeRead((IntPtr)itemLocationPointer + 1, out IntPtr pointer);
                    LogDebug($"The item pointer location is 0x{pointer:X}");
                    _memory.SafeRead(pointer, out _itemLocation);
                    LogDebug($"Items begin at 0x{_itemLocation:X}");
                }
                catch (Exception e)
                {
                    LogError("Unable to read item location", e);
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

        public void LogError(string message)
        {
            _logger.WriteLine($"[TinyAdditions] {message}", System.Drawing.Color.Red);
        }

        /// <summary>
        /// Sets up a hash of the exe
        /// </summary>
        private string GetExeHash()
        {
            if (!File.Exists("P4G.exe"))
            {
                return "file not found :(";
            }
            byte[] exeBytes = File.ReadAllBytes("P4G.exe");
            byte[] hashBytes = new MD5CryptoServiceProvider().ComputeHash(exeBytes);
            return ByteArrayToString(hashBytes);
        }

        /// <summary>
        /// Converts an array of bytes to a string 
        /// ("borrowed" from visual studio docs https://docs.microsoft.com/en-us/troubleshoot/developer/visualstudio/csharp/general/compute-hash-values)
        /// </summary>
        /// <param name="arrInput">The array of bytes to convert</param>
        /// <returns>A string representing the bytes in hexadecimal form</returns>
        private string ByteArrayToString(byte[] arrInput)
        {
            int i;
            StringBuilder sOutput = new StringBuilder(arrInput.Length);
            for (i = 0; i < arrInput.Length; i++)
            {
                sOutput.Append(arrInput[i].ToString("X2"));
            }
            return sOutput.ToString();
        }


        /// <summary>
        /// Queues a signature scan executing the <paramref name="successAction"/> if the signature is found
        /// </summary>
        /// <param name="pattern">The pattern to scan for</param>
        /// <param name="functionName">The name of the thing being scanned for (for logging)</param>
        /// <param name="successAction">The <see cref="Action{int}"/> to run if the address is found with the only paramater being that address</param>
        public void SigScan(string pattern, string functionName, Action<int> successAction)
        {
            var previousResult = _internalConfig.SigScanResults.Find(x => x.Function == functionName);
            if (previousResult != null)
            {
                // Use the previous result if all of the conditions were the same as now
                if (previousResult.WasSuccessful && previousResult.Pattern == pattern && previousResult.ExeHash == _exeHash)
                {
                    LogDebug($"Using previous address (0x{previousResult.Address:X}) for {functionName}");
                    successAction((int)previousResult.Address);
                    return;
                }
            }
            if (_scanner == null)
            {
                LogError("Unable to scan for " + functionName + " because the scanner is null");
                return;
            }
            _scanner.AddMainModuleScan(pattern, (result) =>
            {
                if (!result.Found)
                {
                    LogError($"Unable to find address for {functionName} signature scan");
                    return;
                }
                var functionAddress = result.Offset + _baseAddress;
                LogDebug($"Found the {functionName} address at 0x{functionAddress:X}");
                // Document the result
                if (previousResult != null)
                {
                    previousResult.Address = functionAddress;
                    previousResult.Pattern = pattern;
                    previousResult.WasSuccessful = true;
                    previousResult.ExeHash = _exeHash;
                }
                else
                {
                    _internalConfig.SigScanResults.Add(new SigScanResults(functionName, pattern, _exeHash, true, functionAddress));
                }
                _internalConfig.Save();
                successAction(functionAddress);
            });
        }

        // Signature Scans for a location in memory, returning -1 if the scan fails otherwise the address
        public long SigScan(string pattern, string functionName)
        {
            var previousResult = _internalConfig.SigScanResults.Find(x => x.Function == functionName);
            if (previousResult != null)
            {
                // Use the previous result if all of the conditions were the same as now
                if (previousResult.WasSuccessful && previousResult.Pattern == pattern && previousResult.ExeHash == _exeHash)
                {
                    LogDebug($"Using previous address (0x{previousResult.Address:X}) for {functionName}");
                    return previousResult.Address;
                }                    
            }
            try
            {
                using var thisProcess = Process.GetCurrentProcess();
                using var scanner = new Scanner(thisProcess, thisProcess.MainModule);
                long functionAddress = scanner.FindPattern(pattern).Offset;
                if (functionAddress < 0) throw new Exception($"Unable to find bytes with pattern {pattern}");
                functionAddress += _baseAddress;
                LogDebug($"Found the {functionName} address at 0x{functionAddress:X}");
                // Document the result
                if (previousResult != null)
                {
                    previousResult.Address = functionAddress;
                    previousResult.Pattern = pattern;
                    previousResult.WasSuccessful = true;
                    previousResult.ExeHash = _exeHash;
                }
                else
                {
                    _internalConfig.SigScanResults.Add(new SigScanResults(functionName, pattern, _exeHash, true, functionAddress));
                }
                _internalConfig.Save();
                return functionAddress;
            }
            catch (Exception exception)
            {
                // Document the result
                if (previousResult != null)
                {
                    previousResult.Address = -1;
                    previousResult.Pattern = pattern;
                    previousResult.WasSuccessful = false;
                    previousResult.ExeHash = _exeHash;
                }
                else
                {
                    _internalConfig.SigScanResults.Add(new SigScanResults(functionName, pattern, _exeHash, false, -1));
                }
                _internalConfig.Save();
                LogError($"An error occured trying to find the {functionName} function address. Not initializing. Please report this with information on the version of P4G you are running", exception);
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
            try
            {
                _memory.SafeRead(_eventLocation, out short[] currentEvent, 3);
                // If either the event major or minor isn't 0 we are in an event otherwise we're not
                return currentEvent[0] != 0 || currentEvent[2] != 0;
            }
            catch (Exception e)
            {
                LogError("Error getting current event", e);
                return false;
            }
        }

        // Checks if the player is currently in a menu
        public bool InMenu()
        {
            if (_inMenuLocation == IntPtr.Zero) return false;
            try
            {
                _memory.SafeRead(_inMenuLocation, out int currentMenu);
                LogDebug($"The current menu is {currentMenu}");
                return !(currentMenu == 256 || currentMenu == 128);
            }
            catch (Exception e)
            {
                LogError("Error getting current menu", e);
            }
            return false;
        }

        // Checks how many of an item the player hasz
        public int GetItem(int itemId)
        {
            try
            {
                _memory.SafeRead(_itemLocation + itemId, out byte amount);
                return amount;
            }
            catch (Exception e)
            {
                LogError($"Error checking the amount of item {itemId}", e);
                return -1;
            }
        }

        // Pushes an item to the beginning of the array, pushing everything else forward and removing the last element
        public void ArrayPush<T>(T[] array, T newItem)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                array[i] = array[i - 1];
            }
            array[0] = newItem;
        }
    }
}
