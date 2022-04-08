using p4gpc.tinyadditions.Configuration;
using p4gpc.tinyadditions.Utilities;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;
using Reloaded.Memory.Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;

namespace p4gpc.tinyadditions.Additions
{
    class PersistentBGM : Addition
    {
        private IReverseWrapper<ShouldSwitchBgmFunction> _shouldSwitchBgmReverseWrapper;

        private IAsmHook _btlBgmStartHook;
        private IAsmHook _btlEnterStopBgmHook;
        private IAsmHook _resultsStartBgmHook;
        private IAsmHook _afterResultsStopBgmHook;
        private List<IAsmHook> _hooksList;
        private IntPtr _shouldSwitchBgm;

        private GetFloorId _getFloorId;

        public PersistentBGM(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks) : base(utils, baseAddress, configuration, memory, hooks)
        {
            _shouldSwitchBgm = _memory.Allocate(1);
            InitialiseGetFloorId();
            InitBtlBgmStartHook();
            InitBtlEnterStopBgmHook();
            InitResultsStartBgmHook();
            InitAfterResultsStopBgmHook();
        }

        public override void Resume()
        {
            foreach (var hook in _hooksList)
                hook.Enable();
        }

        public override void Suspend()
        {
            foreach (var hook in _hooksList)
                hook.Disable();
        }

        /// <summary>
        /// Initialises the wrapper for the native get floor id function
        /// </summary>
        private void InitialiseGetFloorId()
        {
            long address = _utils.SigScan("B9 E0 BB ?? ?? E8 ?? ?? ?? ?? A1 ?? ?? ?? ?? 85 C0 75 ?? 33 C9 EB 06", "get floor id");
            _getFloorId = _hooks.CreateWrapper<GetFloorId>(address, out IntPtr getFloorIdAddress);
        }

        /// <summary>
        /// Initialises the hook that controls battle bgm starting
        /// </summary>
        private void InitBtlBgmStartHook()
        {
            long address = _utils.SigScan("E8 ?? ?? ?? ?? 8B 0D ?? ?? ?? ?? 8B 81 ?? ?? ?? ?? 8B B1 ?? ?? ?? ??", "battle bgm start");
            string[] function =
            {
                "use32",
                $"cmp byte [{_shouldSwitchBgm}], 1",
                // If we should switch bgm end the hook, running the original code
                $"je endHook",
                // Jump to the instruction after this start bgm call so it's skipped
                $"{_hooks.Utilities.GetAbsoluteJumpMnemonics((IntPtr)(address + 5), false)}",
                "label endHook",
            };
            _btlBgmStartHook = _hooks.CreateAsmHook(function, address - 2, AsmHookBehaviour.ExecuteFirst).Activate();
        }

        /// <summary>
        /// Initialises the hook that controls regular bgm stopping when entering a battle
        /// </summary>
        private void InitBtlEnterStopBgmHook()
        {
            long address = _utils.SigScan("E8 ?? ?? ?? ?? 8B 15 ?? ?? ?? ?? A1 ?? ?? ?? ?? 0F 57 C0", "battle entering stop bgm");
            string[] function =
            {
                "use32",
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_hooks.Utilities.GetAbsoluteCallMnemonics(ShouldSwitchBgm, out _shouldSwitchBgmReverseWrapper)}",
                "cmp eax, 1",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                // If we should switch bgm end the hook, running the original code
                $"je endHook",
                // Jump to the instruction after this start bgm call so it's skipped
                $"{_hooks.Utilities.GetAbsoluteJumpMnemonics((IntPtr)(address + 5), false)}",
                "label endHook",
            };
            _btlEnterStopBgmHook = _hooks.CreateAsmHook(function, address - 2, AsmHookBehaviour.ExecuteFirst).Activate();
        }

        /// <summary>
        /// Initialises the hook that controls the results screen bgm starting
        /// </summary>
        private void InitResultsStartBgmHook()
        {
            long address = _utils.SigScan("33 C9 E8 ?? ?? ?? ?? 8B 0D ?? ?? ?? ?? 8B 81 ?? ?? ?? ?? 8B 40 ??", "results start bgm");
            string[] function =
            {
                "use32",
                $"cmp byte [{_shouldSwitchBgm}], 1",
                // If we should switch bgm end the hook, running the original code
                $"je endHook",
                // Jump to the instruction after this start bgm call so it's skipped
                $"{_hooks.Utilities.GetAbsoluteJumpMnemonics((IntPtr)(address + 7), false)}",
                "label endHook",
            };
            _resultsStartBgmHook = _hooks.CreateAsmHook(function, address, AsmHookBehaviour.ExecuteFirst).Activate();
        }

        /// <summary>
        /// Initialises the hook that controls bgm stoppping after the results screen is closed
        /// </summary>
        private void InitAfterResultsStopBgmHook()
        {
            long address = _utils.SigScan("E8 ?? ?? ?? ?? A1 ?? ?? ?? ?? 83 60 ?? FE", "after results stop bgm");
            string[] function =
            {
                "use32",
                $"cmp byte [{_shouldSwitchBgm}], 1",
                // If we should switch bgm end the hook, running the original code
                $"je endHook",
                // Jump to the instruction after this start bgm call so it's skipped
                $"{_hooks.Utilities.GetAbsoluteJumpMnemonics((IntPtr)(address + 5), false)}",
                "label endHook",
            };
            _afterResultsStopBgmHook = _hooks.CreateAsmHook(function, address - 2, AsmHookBehaviour.ExecuteFirst).Activate();
        }

        private List<Dungeon> _dungeons = new List<Dungeon>
        {
            new Dungeon("Castle", 5, 14),
            new Dungeon("Bathhouse", 20, 32),
            new Dungeon("Striptease", 40, 52),
            new Dungeon("VoidQuest", 60, 71),
            new Dungeon("Lab", 80, 90),
            new Dungeon("Heaven", 100, 111),
            new Dungeon("MagatsuInaba", 120, 130),
            new Dungeon("YomotsuHirasaka", 140, 149),
            new Dungeon("HollowForest", 160, 170)
        };

        /// <summary>
        /// Checks if we should switch the bgm
        /// </summary>
        /// <returns>True if we should switch it, false if the current bgm should persist</returns>
        private bool ShouldSwitchBgm(int eax)
        {
            _memory.Write(_shouldSwitchBgm, false);
            return false;
            int floorId = _getFloorId();
            Dungeon? dungeon = _dungeons.FirstOrDefault(x => floorId > x.StartFloor && floorId <= x.EndFloor);
            
            // We're not in a known dungeon so just let the music through
            if (dungeon == null)
                return true;

            _utils.LogDebug($"The floor id is {floorId} in the dungeon {dungeon.Name}");

            Random random = new Random();
            double randomNum = random.NextDouble();
            float normalChance = (float)_configuration.GetType().GetProperty($"{dungeon.Name}NormalChance")!.GetValue(_configuration)!;
            // We should let the bgm persist
            if(randomNum > normalChance)
                return false;
            // Let the bgm change
            return true;
        }


        public override void UpdateConfiguration(Config configuration)
        {
            if (_configuration.PersistentBgmEnabled && !configuration.PersistentBgmEnabled)
                Suspend();
            else if (!_configuration.PersistentBgmEnabled && configuration.PersistentBgmEnabled)
                Resume();
            base.UpdateConfiguration(configuration);
        }

        // Function delegates
        [Function(Register.eax, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool ShouldSwitchBgmFunction(int eax);

        [Function(Register.eax, Register.ecx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetFloorId();

    }
}
