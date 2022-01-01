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
        private IReverseWrapper<IsRankupReadyFunction> _isRankupReadyReverseWrapper;
        private IReverseWrapper<AlreadyCheckedFunction> _alreadyCheckedReverseWrapper;
        private IReverseWrapper<GetRankupSymbolFunction> _getRankupSymbolReverseWrapper;
        private CheckIfSlLvlUp _checkIfSlLvlUp;
        private bool _slChecked = false;
        private bool _rankupReady = false;

        public RankupReady(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks) : base(utils, baseAddress, configuration, memory, hooks)
        {
            _utils.Log("Initialising Visible Rankup Ready");
            long displayRankAddress = _utils.SigScan("50 E8 ?? ?? ?? ?? F3 0F 10 44 24 ?? 8D 84 24 ?? ?? ?? ?? 83 C4 30", "display rank");
            long checkLvlUpAddress = _utils.SigScan("53 8B D9 B9 ?? ?? ?? ?? 56 E8 ?? ?? ?? ?? 66 85 DB", "check if sl level up");

            // Create a wrapper for the native CHECK_IF_SL_LVLUP function that can be called
            _checkIfSlLvlUp = _hooks.CreateWrapper<CheckIfSlLvlUp>(checkLvlUpAddress, out IntPtr checkIfLvlUpAddress);

            // SLink id is stored at esi + 2
            string getRankupSymbolMnemonics = "";
            try
            {
                getRankupSymbolMnemonics = hooks.Utilities.GetAbsoluteCallMnemonics(GetRankupSymbol, out _getRankupSymbolReverseWrapper);
            } catch (Exception ex)
            {
                _utils.LogError("Stuff broke", ex);
            }

            string[] beforeRenderFunction =
                {
                $"use32",
                $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{getRankupSymbolMnemonics}",
                $"cmp eax, 0",
                $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                // Jump back to the original code if we shouldn't display the rankup symbol
                $"{hooks.Utilities.GetAbsoluteJumpMnemonics((IntPtr)(checkLvlUpAddress - 1), false)}",
                // Get the rankup symbol back (I realise this is a roundabout way of doing things)
                $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{getRankupSymbolMnemonics}",
                // Move the rankup symbol into edx
                $"mov edx, eax",
                // Restore remaning registers
                $"pop eax",
                $"pop ecx",
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
                // Jump back to the original code if already checked
                $"{hooks.Utilities.GetAbsoluteJumpMnemonics((IntPtr)(checkLvlUpAddress + 12), false)}",
                // Save xmm0
                $"sub esp, 16", // allocate space on stack
                $"movdqu dqword [esp], xmm0",
                // Save xmm3
                $"sub esp, 16", // allocate space on stack
                $"movdqu dqword [esp], xmm3",
                // Check if the sl is ready to rank up
                $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{hooks.Utilities.GetAbsoluteCallMnemonics(IsRankupReady, out _isRankupReadyReverseWrapper)}",
                // Check if a symbol number was returned (the link is ready to level up)
                $"cmp eax, 0",
                $"mov edx, eax",
                //$"{hooks.Utilities.GetRelativeJumpMnemonics((IntPtr)25, false).Replace("jmp", "jne")}",
                // Skip popping edx if they were ready to rank up
                //$"jne 25",
                $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                // Restore xmm3
                $"movdqu xmm3, dqword [esp]",
                $"add esp, 16", // re-align the stack
                // Restore xmm0
                $"movdqu xmm0, dqword [esp]",
                $"add esp, 16", // re-align the stack
                // Jump back to the original code if not ready to rank up
                //$"{hooks.Utilities.GetAbsoluteJumpMnemonics((IntPtr)(checkLvlUpAddress + 6), false).Replace("jmp", "jne")}"
            };
            foreach (string line in renderFunction)
                _utils.Log(line);
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

        // Returns the symbol number to indicate that a social link is ready to rank up if they are, otherwise returns 0
        private bool IsRankupReady(IntPtr slInfoAddress)
        {
            _utils.LogDebug($"The sl info address is 0x{slInfoAddress:X}");
            // Get the id of the current social link
            _memory.SafeRead(slInfoAddress + 2, out SocialLink socialLink);
            // Check if it's ready to level up
            if (!_checkIfSlLvlUp(socialLink))
            {
                _utils.LogDebug($"{socialLink} is not ready to level up");
                _rankupReady = false;
                return false;
            }
            _utils.LogDebug($"{socialLink} is ready to level up");
            _rankupReady = true;
            return true;
        }

        // Checks if the current s link has already been checked so an infinite loop doesn't happen
        private bool AlreadyChecked(int eax)
        {
            _slChecked = !_slChecked; // Flip checked (if it was checked we're going to have a new one which won't be)
            return !_slChecked;
        }

        private int GetRankupSymbol(int eax)
        {
            if (!_slChecked || !_rankupReady)
                return 0;
            return 164;
        }

        // Delegates for functions
        [Function(Register.ecx, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool CheckIfSlLvlUp(SocialLink socialLink);

        [Function(Register.esi, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool IsRankupReadyFunction(IntPtr slInfoAddress);

        [Function(Register.eax, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool AlreadyCheckedFunction(int eax);

        [Function(Register.eax, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetRankupSymbolFunction(int eax);
    }
}
