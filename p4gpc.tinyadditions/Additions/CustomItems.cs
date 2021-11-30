using p4gpc.tinyadditions.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;
using Reloaded.Memory.Pointers;
using Reloaded.Memory.Sources;
using Reloaded.Memory.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;

namespace p4gpc.tinyadditions.Additions
{
    class CustomItems : Addition
    {
        private IReverseWrapper<SetFunctionIdFunction> _functionIdReverseWrapper;
        private IAsmHook _flowFunctionHook;
        private long _flowFunctionLocation;
        private IReverseWrapper<CheckSkillFunction> _checkSkillReverseWrapper;
        private IAsmHook _checkSkillHook;
        private IReverseWrapper<FreezeControlsFunction> _freezeControlsReverseWrapper;
        private IAsmHook _freezeControlsHook;
        private long _freezeControlsLocation;
        private int _currentSkill;
        private GetDungeonFlowLocation getDungeonFlowLocation;

        public unsafe CustomItems(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks) : base(utils, baseAddress, configuration, memory, hooks)
        {
            _utils.Log("Initialising custom items");

            _flowFunctionLocation = _utils.SigScan("E8 ?? ?? ?? ?? A1 ?? ?? ?? ?? 6A 05", "custom items flow function");
            _freezeControlsLocation = _utils.SigScan("C7 46 ?? 03 00 00 00 B8 01 00 00 00 5E 5D C3 A1 ?? ?? ?? ??", "custom items freeze controls");
            long skillIdLocation = _utils.SigScan("B8 F6 00 00 00 66 ?? ?? 75 17", "custom skill id");

            // Get the dungeon flow location
            _memory.SafeRead((IntPtr)_flowFunctionLocation + 0x1A, out int getDungeonFlowFuncOffset);
            //_utils.LogDebug($"Looking at 0x{_flowFunctionLocation + 0x1A:X}");
            _utils.LogDebug($"getDungeonFlowFuncOffset is 0x{getDungeonFlowFuncOffset:X}");
            long getDungeonFlowFuncAddress = _flowFunctionLocation + 0x19 + 5 + getDungeonFlowFuncOffset;
            _utils.LogDebug($"The get dungeon flow address function is at 0x{getDungeonFlowFuncAddress:X}");
            // Create a wrapper for the function that can be called
            getDungeonFlowLocation = _hooks.CreateWrapper<GetDungeonFlowLocation>(getDungeonFlowFuncAddress, out IntPtr getDungeonFlowPointerAddress);
            IntPtr dungeonFlowLoaction = getDungeonFlowLocation();

            // Create the function hook
            string[] flowIdFunction =
            {
                $"use32",
                $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{hooks.Utilities.GetAbsoluteCallMnemonics(SetFunctionId, out _functionIdReverseWrapper)}",
                $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };
            _flowFunctionHook = hooks.CreateAsmHook(flowIdFunction, _flowFunctionLocation, AsmHookBehaviour.ExecuteFirst).Activate();

            string[] itemIdFunction =
{
                $"use32",
                $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{hooks.Utilities.GetAbsoluteCallMnemonics(CheckSkill, out _checkSkillReverseWrapper)}",
                $"cmp eax, 1",
                $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };
            _checkSkillHook = hooks.CreateAsmHook(itemIdFunction, skillIdLocation, AsmHookBehaviour.DoNotExecuteOriginal).Activate();

            string[] freezeControlsFunction =
            {
                $"use32",
                $"{hooks.Utilities.GetAbsoluteCallMnemonics(FreezeControls, out _freezeControlsReverseWrapper)}",
            };
            _freezeControlsHook = hooks.CreateAsmHook(freezeControlsFunction, _freezeControlsLocation, AsmHookBehaviour.DoNotExecuteOriginal).Activate();

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

        private void SetFunctionId(int eax)
        {
            IntPtr dungeonFlowLocation = getDungeonFlowLocation();
            string flowFunctionName = "dng_escape";
            if (_currentSkill == 311)
                flowFunctionName = "eve_dressroom_yukiko";
            int functionId = (byte)FindFlowFunctionId(dungeonFlowLocation, flowFunctionName);
            _utils.LogDebug($"The dungeon flow is at 0x{dungeonFlowLocation:X}");

            // Check for function ids greater than one byte (this will likely break stuff currently)
            if(functionId > 255)
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
            if(functionIndex != -1)
                return functionIndex;  
            _utils.LogError($"Couldn't find index of function {functionName}. Please ensure the mod this function is from is correctly enabled and that the names match exactly (case sensitive).")
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
            for(int i = 0; i < rawNames.Length; i++)
            {
                // Check if we're up to the next string
                if(i >= 32 && i % 32 == 0)
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
            _currentSkill = skill;
            return skill == 246 || skill == 311;
        }

        // Returns true if the game's controls should be frozen (used when switching floors through the flowscript)
        private void FreezeControls(int eax)
        {
            bool freeze = false;
            if (_currentSkill == 246)
                freeze = true;
            
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
