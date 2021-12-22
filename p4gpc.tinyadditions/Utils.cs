using p4gpc.tinyadditions.Configuration;
using Reloaded.Memory.Sigscan;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

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

        public Utils(Config configuration, ILogger logger, int baseAddress, IMemory memory)
        {
            // Initialise fields
            Configuration = configuration;
            _logger = logger;
            _baseAddress = baseAddress;
            _memory = memory;

            // Initialise locations
            List<Task> locationInits = new List<Task>();
            locationInits.Add(Task.Run(() =>
            {
                InitLocation("flag", "68 ?? ?? ?? ?? 56 E8 ?? ?? ?? ?? 83 C4 0C 81 C6 40 03 00 00", 1, out _flagLocation);
            }));
            locationInits.Add(Task.Run(() =>
            {
                InitLocation("event", "A3 ?? ?? ?? ?? 8B 85 ?? ?? ?? ?? 66 89 0D ?? ?? ?? ??", 1, out _eventLocation);
            }));

            locationInits.Add(Task.Run(() =>
            {
                InitLocation("in menu", "89 3D ?? ?? ?? ?? 89 1D ?? ?? ?? ?? A3 ?? ?? ?? ??", 2, out _inMenuLocation);
            }));
            Task.WaitAll(locationInits.ToArray());
        }

        // Initialise the location to something in memory by sig scanning for a pointer
        public void InitLocation(string name, string signature, int pointerOffset, out IntPtr outVar)
        {
            outVar = IntPtr.Zero;
            long pointer = SigScan(signature, $"{name} pointer");
            if (pointer != -1)
            {
                try
                {
                    _memory.SafeRead((IntPtr)pointer + pointerOffset, out outVar);
                    LogDebug($"The {name} is at 0x{outVar:X}");
                }
                catch (Exception e)
                {
                    LogError($"Unable to read {name} location", e);
                }
            }

        }

        // Initialises the item location as right at startup the pointer does not exist
        // (called once an input is read)
        public bool InitialiseItemLocation()
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
                    if ((int)_itemLocation == 0) return false;
                }
                catch (Exception e)
                {
                    LogError("Unable to read item location", e);
                    return false;
                }
            }
            return true;
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

        // Checks how many of an item the player has
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
