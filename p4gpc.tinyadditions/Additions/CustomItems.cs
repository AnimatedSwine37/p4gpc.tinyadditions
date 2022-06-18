using p4gpc.tinyadditions.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;
using Reloaded.Memory.Pointers;
using Reloaded.Memory.Sources;
using Reloaded.Memory.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;

namespace p4gpc.tinyadditions.Additions
{
    class CustomItems : Addition
    {
        private IReverseWrapper<SetFunctionIdFunction>? _functionIdReverseWrapper;
        private IAsmHook? _flowFunctionHook;
        private IReverseWrapper<CheckSkillFunction>? _checkSkillReverseWrapper;
        private IAsmHook? _checkSkillHook;
        private IReverseWrapper<FreezeControlsFunction>? _freezeControlsReverseWrapper;
        private IAsmHook? _freezeControlsHook;
        private int _currentSkill;
        private GetDungeonFlowLocation? getDungeonFlowLocation;
        private List<CustomItemInfo> _customItems;
        private CustomItemInfo? _currentCustomItem;
        private short numInitialised = 0;
        private int _freezeControlsLocation;
        private int _flowFunctionLocation;

        public unsafe CustomItems(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks) : base(utils, baseAddress, configuration, memory, hooks)
        {
            _utils.Log("Initialising custom items");

            _utils.SigScan("E8 ?? ?? ?? ?? A1 ?? ?? ?? ?? 6A 05", "custom items flow function", InitFlowFunctionHook);
            _utils.SigScan("C7 46 ?? 03 00 00 00 B8 01 00 00 00 5E 5D C3 A1 ?? ?? ?? ??", "custom items freeze controls", InitFreezeControlsHook);
            _utils.SigScan("B8 F6 00 00 00 66 ?? ?? 75 17", "custom skill id", InitItemIdHook);

            _customItems = LoadCustomItems();
        }

        private void InitFlowFunctionHook(int address)
        {
            _flowFunctionLocation = address;
            string[] flowIdFunction =
            {
                $"use32",
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_hooks.Utilities.GetAbsoluteCallMnemonics(SetFunctionId, out _functionIdReverseWrapper)}",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };
            _flowFunctionHook = _hooks.CreateAsmHook(flowIdFunction, address, AsmHookBehaviour.ExecuteFirst).Activate();

            // Get the dungeon flow location
            _memory.SafeRead((IntPtr)address + 0x1A, out int getDungeonFlowFuncOffset);
            //_utils.LogDebug($"Looking at 0x{_flowFunctionLocation + 0x1A:X}");
            _utils.LogDebug($"getDungeonFlowFuncOffset is 0x{getDungeonFlowFuncOffset:X}");
            long getDungeonFlowFuncAddress = address + 0x19 + 5 + getDungeonFlowFuncOffset;
            _utils.LogDebug($"The get dungeon flow address function is at 0x{getDungeonFlowFuncAddress:X}");
            // Create a wrapper for the function that can be called
            getDungeonFlowLocation = _hooks.CreateWrapper<GetDungeonFlowLocation>(getDungeonFlowFuncAddress, out IntPtr getDungeonFlowPointerAddress);
            numInitialised++;
            if(numInitialised == 3)
                _utils.Log("Custom items initialised");
        }

        private void InitItemIdHook(int address)
        {
            string[] itemIdFunction =
{
                $"use32",
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_hooks.Utilities.GetAbsoluteCallMnemonics(CheckSkill, out _checkSkillReverseWrapper)}",
                $"cmp eax, 1",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };
            _checkSkillHook = _hooks.CreateAsmHook(itemIdFunction, address, AsmHookBehaviour.DoNotExecuteOriginal).Activate();
            numInitialised++;
            if (numInitialised == 3)
                _utils.Log("Custom items initialised");
        }

        private void InitFreezeControlsHook(int address)
        {
            _freezeControlsLocation = address;
            string[] freezeControlsFunction =
{
                $"use32",
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_hooks.Utilities.GetAbsoluteCallMnemonics(FreezeControls, out _freezeControlsReverseWrapper)}",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };
            _freezeControlsHook = _hooks.CreateAsmHook(freezeControlsFunction, address, AsmHookBehaviour.DoNotExecuteOriginal).Activate();
            numInitialised++;
            if (numInitialised == 3)
                _utils.Log("Custom items initialised");
        }

        public override void Resume()
        {
            _flowFunctionHook?.Enable();
            _checkSkillHook?.Enable();
            _freezeControlsHook?.Enable();
        }

        public override void Suspend()
        {
            _flowFunctionHook?.Disable();
            _checkSkillHook?.Disable();
            _freezeControlsHook?.Disable();
        }

        private List<CustomItemInfo> LoadCustomItems()
        {
            var items = new List<CustomItemInfo>();
            if (!Directory.Exists(@"mods/customItems"))
                return items;

            var itemFiles = Directory.GetFiles(@"mods/customItems", "*.json", SearchOption.AllDirectories);
            foreach (var itemFile in itemFiles)
            {
                try
                {
                    string json = File.ReadAllText(itemFile, Encoding.UTF8);
                    CustomItemInfo? item = JsonSerializer.Deserialize<CustomItemInfo>(json);
                    if (item == null || (item.SkillId == 0 && item.FunctionName == null && item.FreezeControls == false))
                    {
                        _utils.LogError($"Invalid custom item info file {Path.GetFileName(itemFile)}");
                        continue;
                    }
                    items.Add(item);
                    _utils.Log($"Loaded {Path.GetFileNameWithoutExtension(itemFile)} custom item");
                }
                catch (Exception e)
                {
                    _utils.LogError($"Unable to load custom item info file {Path.GetFileName(itemFile)}", e);
                }
            }

            return items;
        }

        private void SetFunctionId(int eax)
        {
            if (getDungeonFlowLocation == null)
            {
                _utils.LogError("Unable to set function id as getDungeonFlowLocation is null");
                return;
            }
                
            IntPtr dungeonFlowLocation = getDungeonFlowLocation();
            _utils.LogDebug($"The dungeon flow is at 0x{dungeonFlowLocation:X}");

            // Get the function id
            string flowFunctionName = "dng_escape";
            if (_currentCustomItem != null)
                flowFunctionName = _currentCustomItem.FunctionName;

            int functionId = FindFlowFunctionId(dungeonFlowLocation, flowFunctionName);

            // Check for function ids greater than one byte (this will likely break stuff currently)
            if (functionId > 255)
            {
                functionId = 10; // Run a function that won't do anything instead of the actual requested function
                _utils.LogError("Tried to use a function with id greater than 255. Please report this with details on the mods you are running as it is not currently handled properly.");
            }

            // Change the function id that will be pushed
            _memory.SafeWrite((IntPtr)_flowFunctionLocation + 11, (byte)functionId);
        }

        // Finds the id of a flowscript function by the function's name for a bf file loaded in memory
        private int FindFlowFunctionId(IntPtr flowLocation, string functionName)
        {
            var functions = GetFlowFunctions(flowLocation);
            int functionIndex = functions.IndexOf(functionName);
            if (functionIndex != -1)
            {
                _utils.LogDebug($"The flow function id for {functionName} is {functionIndex}");
                return functionIndex;
            }
            _utils.LogError($"Couldn't find index of function {functionName}. Please ensure the mod this function is from is correctly enabled and that the names match exactly (case sensitive).");
            return 10;
        }

        // Gets a list of the names of all defined functions in a flow file
        private List<string> GetFlowFunctions(IntPtr flowLocation)
        {
            // Read flow data
            _memory.SafeRead(flowLocation + 40, out int numFuncs);
            _memory.SafeReadRaw(flowLocation + 112, out byte[] rawNames, numFuncs * 32);

            // Go through the raw data, constructing a list of the names
            List<string> functions = new List<string>();
            int stringStart = 0;
            int currentFunction = 0;
            for (int i = 0; i < rawNames.Length; i++)
            {
                // Check if we're up to the next string
                if (i >= 32 && i % 32 == 0)
                {
                    currentFunction++;
                    stringStart = i;
                }

                // Find the end of the current name string
                if (rawNames[i] == 0 || rawNames[i] == '\n')
                {
                    // Add the current subsection of the raw array to the function names as a string
                    functions.Add(Encoding.UTF8.GetString(rawNames.Skip(stringStart).Take(i - stringStart).ToArray()));
                    // Set counters for the start of the next string
                    i = (currentFunction + 1) * 32 - 1;
                }
            }
            return functions;
        }

        // Checks whether the skill is one that should call a flow function
        private bool CheckSkill(int skill)
        {
            _utils.LogDebug($"Skill {skill} used");
            _currentSkill = skill;
            _currentCustomItem = _customItems.FirstOrDefault(item => item.SkillId == skill);
            if (_currentCustomItem == null)
            {
                _utils.LogDebug("The skill is not a custom item skill");
                return skill == 246;
            }
            _utils.LogDebug("The skill is a custom item skill");
            return true;
        }

        // Returns true if the game's controls should be frozen (used when switching floors through the flowscript)
        private void FreezeControls(int eax)
        {
            bool freeze = false;
            if (_currentSkill == 246)
                freeze = true;
            else if (_currentCustomItem != null)
                freeze = _currentCustomItem.FreezeControls;

            _memory.SafeWrite((IntPtr)_freezeControlsLocation + 8, freeze);
        }

        // Hooked function delegate
        [Function(Register.eax, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SetFunctionIdFunction(int eax);

        [Function(Register.ecx, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool CheckSkillFunction(int skill);

        [Function(Register.eax, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FreezeControlsFunction(int eax);

        [Function(CallingConventions.Cdecl)]
        public delegate IntPtr GetDungeonFlowLocation();
    }
}
