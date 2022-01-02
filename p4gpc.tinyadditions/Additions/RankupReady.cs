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
using static p4gpc.tinyadditions.P4GEnums;
using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;

namespace p4gpc.tinyadditions.Additions
{
    class RankupReady : Addition
    {
        private IAsmHook _displayRankHook;
        private IAsmHook _beforeDisplayRankHook;
        private IReverseWrapper<AlreadyCheckedFunction> _alreadyCheckedReverseWrapper;
        private IReverseWrapper<GetRankupSymbolFunction> _getRankupSymbolReverseWrapper;
        private CheckIfSlLvlUp _checkIfSlLvlUp;
        private bool _slChecked = false;

        public RankupReady(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks) : base(utils, baseAddress, configuration, memory, hooks)
        {
            _utils.Log("Initialising Visible Rankup Ready");
            long displayRankStartAddress = _utils.SigScan("F3 0F 10 44 24 ?? 8D 84 24 ?? ?? ?? ?? 83 C4 1C 0F B7 56 ??", "display rank start");
            long displayRankAddress = _utils.SigScan("50 E8 ?? ?? ?? ?? F3 0F 10 44 24 ?? 8D 84 24 ?? ?? ?? ?? 83 C4 30", "display rank");
            long checkLvlUpAddress = _utils.SigScan("53 ?? ?? B9 ?? ?? ?? ?? 56 E8 ?? ?? ?? ?? 66 85 DB", "check if sl level up");

            if(displayRankStartAddress == -1 || displayRankAddress == -1 || checkLvlUpAddress == -1)
            {
                _utils.LogError("Failed to find all addresses required for Visible Rankup Ready. It will not be initialised.");
                return;
            }

            // Create a wrapper for the native CHECK_IF_SL_LVLUP function that can be called
            _checkIfSlLvlUp = _hooks.CreateWrapper<CheckIfSlLvlUp>(checkLvlUpAddress, out IntPtr checkIfLvlUpAddress);

            string[] beforeRenderFunction =
                {
                $"use32",
                // Save xmm0
                $"sub esp, 16", // allocate space on stack
                $"movdqu dqword [esp], xmm0",
                // Save xmm3
                $"sub esp, 16", // allocate space on stack
                $"movdqu dqword [esp], xmm3",
                // Get the rankup symbol
                $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{hooks.Utilities.GetAbsoluteCallMnemonics(GetRankupSymbol, out _getRankupSymbolReverseWrapper)}",
                $"cmp eax, 0",
                // Jump back to the original code if we shouldn't display the rankup symbol
                $"je noRankup",
                // Move the rankup symbol into edx
                $"mov edx, eax",
                // Restore remaning registers
                $"pop ecx",
                $"pop ecx",
                $"pop eax",
                // Restore xmm3
                $"movdqu xmm3, dqword [esp]",
                $"add esp, 16", // re-align the stack
                // Alter xmm3, moving the rankup symbol to the right
                $"addss xmm3, xmm7",
                $"jmp endCode",
                // Pop all call registers if there was no rankup symbol to render
                $"label noRankup",
                $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                // Restore xmm3
                $"movdqu xmm3, dqword [esp]",
                $"add esp, 16", // re-align the stack
                $"label endCode",
                // Restore xmm0
                $"movdqu xmm0, dqword [esp]",
                $"add esp, 16", // re-align the stack
            };

            // Display Rank Hook
            string[] renderFunction =
            {
                $"use32",
                // See if the current slink has been done already
                $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{hooks.Utilities.GetAbsoluteCallMnemonics(AlreadyChecked, out _alreadyCheckedReverseWrapper)}",
                $"cmp eax, 1",
                $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                // If already checked leave this code
                $"je endCode",
                // Jump back to the before render hook since we need to check it
                $"{hooks.Utilities.GetAbsoluteJumpMnemonics((IntPtr)displayRankStartAddress, false)}",
                $"label endCode",
            };
            _beforeDisplayRankHook = hooks.CreateAsmHook(beforeRenderFunction, displayRankAddress - 7, AsmHookBehaviour.ExecuteAfter).Activate();
            _displayRankHook = hooks.CreateAsmHook(renderFunction, displayRankAddress, AsmHookBehaviour.ExecuteAfter).Activate();

            _utils.Log("Successfully Initialised Visible Rankup Ready");
        }

        public override void Resume()
        {
            _beforeDisplayRankHook?.Enable();
            _displayRankHook?.Enable();
        }

        public override void Suspend()
        {
            _beforeDisplayRankHook?.Disable();
            _displayRankHook?.Disable();
        }

        // Checks if the current s link has already been checked so an infinite loop doesn't happen
        private bool AlreadyChecked(int eax)
        {
            _slChecked = !_slChecked; // Flip checked (if it was checked we're going to have a new one which won't be)
            return !_slChecked;
        }

        // Returns the symbol number to indicate that a social link is ready to rank up if they are, otherwise returns 0
        private int GetRankupSymbol(IntPtr slInfoAddress)
        {
            //_utils.LogDebug($"The sl info address is 0x{slInfoAddress:X}");
            // Get the id of the current social link
            _memory.Read(slInfoAddress + 2, out SocialLink socialLink);
            // Check if it's ready to level up
            bool rankupReady;
            if (!_checkIfSlLvlUp(socialLink))
            {
                //_utils.LogDebug($"{socialLink} is not ready to level up");
                rankupReady = false;
            }
            else
            {
                //_utils.LogDebug($"{socialLink} is ready to level up");
                rankupReady = true;
            }
            if (!_slChecked || !rankupReady)
                return 0;
            return 164;
        }

        // Delegates for functions
        [Function(Register.ecx, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool CheckIfSlLvlUp(SocialLink socialLink);

        [Function(Register.eax, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool AlreadyCheckedFunction(int eax);

        [Function(Register.esi, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetRankupSymbolFunction(IntPtr slInfoAddress);
    }
}
