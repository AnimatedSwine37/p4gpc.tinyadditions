using p4gpc.tinyadditions.Configuration;
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

        private IAsmHook _bgmStartHook;
        private IAsmHook _bgmEndHook;
        private IAsmHook _battleStartingHook;
        private IAsmHook _battleStartedHook;
        private IAsmHook _bgmFadeHook;
        private IAsmHook _battleEndingHook;

        private string _shouldSwitchBgmCall;

        private IntPtr _skipBgmChange;
        private IntPtr _battleEnding;

        public PersistentBGM(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks) : base(utils, baseAddress, configuration, memory, hooks)
        {
            _skipBgmChange = _memory.Allocate(1);
            _memory.Write(_skipBgmChange, false);
            _battleEnding = _memory.Allocate(1);
            _memory.Write(_battleEnding, false);
            _shouldSwitchBgmCall = _hooks.Utilities.GetAbsoluteCallMnemonics(ShouldSwitchBgm, out _shouldSwitchBgmReverseWrapper);
            InitBattleStartingHook();
            InitBgmEndHook();
            InitBgmStartHook();
            InitBgmFadeHook();
            InitBattleStartedHook();
            InitBattleEndingHook();
            InitBattleEndedHook();
        }

        public override void Resume()
        {
            throw new NotImplementedException();
        }

        public override void Suspend()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Initialise the hook that will stop the bgm from ending when starting a battle
        /// </summary>
        private void InitBgmEndHook()
        {
            long address = _utils.SigScan("53 ?? ?? B9 ?? ?? ?? ?? 56 E8 ?? ?? ?? ?? 6B F3 70", "bgm end");
            string[] function =
            {
                "use32",
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_shouldSwitchBgmCall}",
                // If we should switch the bgm jump to the end and let the code run as normal 
                "cmp eax, 1",
                "je endHook",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                // Return the function as we shouldn't switch bgm so we shouldn't start a new one
                "ret",
                "label endHook",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };
            _bgmEndHook = _hooks.CreateAsmHook(function, address, AsmHookBehaviour.ExecuteFirst).Activate();
        }

        /// <summary>
        /// Initialises the hook that will stop new bgm from starting
        /// </summary>
        private void InitBgmStartHook()
        {
            long address = _utils.SigScan("55 8B EC 51 53 56 8B F1 B9 ?? ?? ?? ?? 57 89 75 ?? E8 ?? ?? ?? ?? 69 05 ?? ?? ?? ?? 20 02 00 00", "bgm start");
            string[] function =
            {
                "use32",
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_shouldSwitchBgmCall}",
                // If we should switch the bgm jump to the end and let the code run as normal 
                "cmp eax, 1",
                "je endHook",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                // Return the function as we shouldn't switch bgm so we shouldn't start a new one
                "ret",
                "label endHook",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };
            _bgmStartHook = _hooks.CreateAsmHook(function, address, AsmHookBehaviour.ExecuteFirst).Activate();
        }

        /// <summary>
        /// Initialises the hook that happens just before a battle starts
        /// </summary>
        private void InitBattleStartingHook()
        {
            //long address = _utils.SigScan("33 C9 E8 ?? ?? ?? ?? F3 0F 10 05 ?? ?? ?? ??", "battle starting");
            long address = _utils.SigScan("E8 ?? ?? ?? ?? C7 47 ?? 05 00 00 00 E9 ?? ?? ?? ?? A1 ?? ?? ?? ??", "battle starting");
            string[] function =
            {
                "use32",
                //"cmp ecx, 4",
                //"je battleStarting",
                //"cmp ecx, 5",
                //"je battleStarting",
                //// A battle isn't starting (the player just swung their sword or something)
                //$"mov byte [{_battleStarting}], 0",
                //"jmp endHook",
                //// A battle is starting
                //"label battleStarting",
                $"mov byte [{_skipBgmChange}], 1",
                //"label endHook"
            };
            _battleStartingHook = _hooks.CreateAsmHook(function, address, AsmHookBehaviour.ExecuteAfter).Activate();
        }

        /// <summary>
        /// Initialise the hook that happens just as a battle is starting
        /// </summary>
        private void InitBattleStartedHook()
        {
            long address = _utils.SigScan("E8 ?? ?? ?? ?? 8B 0D ?? ?? ?? ?? 8B 81 ?? ?? ?? ?? 8B B1 ?? ?? ?? ??", "battle started");
            string[] function =
            {
                "use32",
                // The battle has now started
                $"mov byte [{_skipBgmChange}], 0"
            };
            _battleStartedHook = _hooks.CreateAsmHook(function, address, AsmHookBehaviour.ExecuteAfter).Activate();
        }

        /// <summary>
        /// Initialises the hook that stops the bgm from fading in and out
        /// </summary>
        private void InitBgmFadeHook()
        {
            long address = _utils.SigScan("55 8B EC 51 53 56 8B F1 B9 ?? ?? ?? ?? 57 8B FA 89 75 ??", "bgm fade");
            string[] function =
            {
                "use32",
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_shouldSwitchBgmCall}",
                // If we should switch the bgm jump to the end and let the code run as normal 
                "cmp eax, 1",
                "je endHook",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                // Return the function as we shouldn't switch bgm so we shouldn't start a new one
                "ret",
                "label endHook",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };
            _bgmFadeHook = _hooks.CreateAsmHook(function, address, AsmHookBehaviour.ExecuteFirst).Activate();
        }

        /// <summary>
        /// Initialises the hook that stops the music from changing when the battle goes to the end screen stuff
        /// </summary>
        private void InitBattleEndingHook()
        {
            long address = _utils.SigScan("BA 07 00 00 00 ?? C9 E8 ?? ?? ?? ?? 8B 0D ?? ?? ?? ??", "battle ending");
            string[] function =
            {
                "use32",
                $"mov byte [{_battleEnding}], 1",
                // Skip changing the music
                $"{_hooks.Utilities.GetAbsoluteJumpMnemonics((IntPtr)(address + 18), false)}",
            };
            _battleEndingHook = _hooks.CreateAsmHook(function, address, AsmHookBehaviour.ExecuteFirst).Activate();
        }

        /// <summary>
        /// Initialse the hook that will stop the music from switching once the battle has ended
        /// </summary>
        private void InitBattleEndedHook()
        {
            long address = _utils.SigScan("74 ?? 8B 77 ?? E8 ?? ?? ?? ??", "battle ended");
            string[] function =
            {
                "use32",
                // If the bgm wasn't going to be run in the first place skip this whole thing (there's a cmp just before the hook)
                "jne endHook",
                $"cmp byte [{_battleEnding}], 1",
                "jne endHookResetComp",
                $"mov byte [{_battleEnding}], 0",
                // Skip the bgm call
                $"{_hooks.Utilities.GetAbsoluteJumpMnemonics((IntPtr)(address + 30), false)}",
                // End hook and reset the comparison to true
                "label endHookResetComp",
                "cmp eax, eax",
                "label endHook"
            };
            _hooks.CreateAsmHook(function, address, AsmHookBehaviour.ExecuteFirst).Activate();
        }

        /// <summary>
        /// Checks if we should switch the bgm
        /// </summary>
        /// <returns>True if we should switch it, false if the current bgm should persist</returns>
        private bool ShouldSwitchBgm(int eax)
        {
            _memory.Read(_skipBgmChange, out bool battleStarting);
            // If in dungeon don't switch bgm
            //if (_utils.CheckFlag(3075))
            if (battleStarting)
                return false;
            return true;
        }

        // Function delegates
        [Function(Register.eax, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool ShouldSwitchBgmFunction(int eax);
    }
}
