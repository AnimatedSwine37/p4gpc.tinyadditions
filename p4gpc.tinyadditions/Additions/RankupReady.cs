using p4gpc.tinyadditions.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;
using Reloaded.Memory.Sources;
using System;
using System.Collections.Generic;
using System.IO;
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
        private IAsmHook? _displayRankHook;
        private IAsmHook? _beforeDisplayRankHook;
        private IReverseWrapper<AlreadyCheckedFunction>? _alreadyCheckedReverseWrapper;
        private IReverseWrapper<GetRankupSymbolFunction>? _getRankupSymbolReverseWrapper;
        private CheckIfSlLvlUp? _checkIfSlLvlUp;
        private bool _slChecked = false;
        private IntPtr _rankupSymbolOffsetAddress;

        private int _displayRankStartAddress = -1;
        private int _displayRankAddress = -1;

        public RankupReady(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks) : base(utils, baseAddress, configuration, memory, hooks)
        {
            _utils.Log("Initialising Visible Rankup Ready");

            // Initialise a place to store the offset of the rankup symbol
            _rankupSymbolOffsetAddress = _memory.Allocate(4);
            _memory.Write(_rankupSymbolOffsetAddress, _configuration.RankupReadySymbolOffset);

            // Sig scan stuff
            _utils.SigScan("F3 0F 10 44 24 ?? 8D 84 24 ?? ?? ?? ?? 83 C4 1C 0F B7 56 ??", "display rank start",
                (result) =>
                {
                    _displayRankStartAddress = result;
                    if (_displayRankAddress != -1)
                        InitRenderHooks();
                });
            _utils.SigScan("50 E8 ?? ?? ?? ?? F3 0F 10 44 24 ?? 8D 84 24 ?? ?? ?? ?? 83 C4 30", "display rank",
                (result) =>
                {
                    _displayRankAddress = result;
                    if (_displayRankStartAddress != -1)
                        InitRenderHooks();
                });
            _utils.SigScan("53 ?? ?? B9 ?? ?? ?? ?? 56 E8 ?? ?? ?? ?? 66 85 DB", "check if sl level up", InitLevelUpCheck);
        }

        private void InitLevelUpCheck(int address)
        {
            // Create a wrapper for the native CHECK_IF_SL_LVLUP function that can be called
            _checkIfSlLvlUp = _hooks.CreateWrapper<CheckIfSlLvlUp>(address, out IntPtr checkIfLvlUpAddress);
            if (_displayRankAddress != -1 && _displayRankStartAddress != -1)
                _utils.Log("Successfully Initialised Visible Rankup Ready");
        }

        private void InitRenderHooks()
        {
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
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetRankupSymbol, out _getRankupSymbolReverseWrapper)}",
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
                // Increase xmm3
                $"addss xmm3, [{_rankupSymbolOffsetAddress}]",
                $"jmp endCode",
                // Pop all call registers if there was no rankup symbol to render
                $"label noRankup",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
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
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_hooks.Utilities.GetAbsoluteCallMnemonics(AlreadyChecked, out _alreadyCheckedReverseWrapper)}",
                $"cmp eax, 1",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                // If already checked leave this code
                $"je endCode",
                // Jump back to the before render hook since we need to check it
                $"{_hooks.Utilities.GetAbsoluteJumpMnemonics((IntPtr)_displayRankStartAddress, false)}",
                $"label endCode",
            };
            _beforeDisplayRankHook = _hooks.CreateAsmHook(beforeRenderFunction, _displayRankAddress - 7, AsmHookBehaviour.ExecuteAfter).Activate();
            _displayRankHook = _hooks.CreateAsmHook(renderFunction, _displayRankAddress + 1, AsmHookBehaviour.ExecuteAfter).Activate();
            if (_checkIfSlLvlUp != null)
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

        public override void UpdateConfiguration(Config configuration)
        {
            if (_configuration.RankupReadyEnabled && !configuration.RankupReadyEnabled)
                Suspend();
            if (!_configuration.RankupReadyEnabled && configuration.RankupReadyEnabled)
                Resume();

            _configuration = configuration;
            _memory.Write(_rankupSymbolOffsetAddress, _configuration.RankupReadySymbolOffset);
        }

        // Checks if the current s link has already been checked so an infinite loop doesn't happen
        private bool AlreadyChecked(IntPtr slInfoAddress)
        {
            _memory.Read(slInfoAddress + 4, out short rank);
            // Max rank s links shouldn't check for rankup
            if (rank >= 10)
            {
                _slChecked = false;
                return true;
            }
            _slChecked = !_slChecked; // Flip checked (if it was checked we're going to have a new one which won't be)
            return !_slChecked;
        }

        // Returns the symbol number to indicate that a social link is ready to rank up if they are, otherwise returns 0
        private int GetRankupSymbol(IntPtr slInfoAddress)
        {
            if (!_configuration.RankupReadyEnabled || _checkIfSlLvlUp == null) return 0;
            //_utils.LogDebug($"The sl info address is 0x{slInfoAddress:X}");
            // Get the id of the current social link
            _memory.Read(slInfoAddress + 2, out SocialLink socialLink);
            _memory.Read(slInfoAddress + 4, out short rank);
            // Check if it's ready to level up
            bool rankupReady;
            if (!_checkIfSlLvlUp(socialLink) || IsStorySl(socialLink, rank))
            {
                //_utils.LogDebug($"{socialLink} is not ready to level up");
                rankupReady = false;
            }
            else if (socialLink == SocialLink.Fox)
            {
                rankupReady = IsFoxReady(rank);
            }
            else
            {
                //_utils.LogDebug($"{socialLink} is ready to level up");
                rankupReady = true;
            }
            if (!_slChecked || !rankupReady)
                return 0;
            return (int)_configuration.RankupReadySymbol;
        }

        // Checks if a social link is one that can only be ranked up by story stuff
        private bool IsStorySl(SocialLink sl, short rank)
        {
            if (sl == SocialLink.InvestigationTeam || sl == SocialLink.SeekersOfTruth || sl == SocialLink.Teddie || sl == SocialLink.Margaret)
                return true;
            else if ((sl == SocialLink.AdachiHunger || sl == SocialLink.AdachiJester) && rank >= 6)
                return true;
            return false;
        }

        // Checks if the fox is ready to rank up based on quests
        private bool IsFoxReady(short rank)
        {
            return (_utils.CheckFlag(0 + 325) && !_utils.CheckFlag(0 + 0x0400 + 1696)) || (_utils.CheckFlag(0 + 332) && !_utils.CheckFlag(0 + 0x0400 + 1697))
                    || (_utils.CheckFlag(0 + 339) && !_utils.CheckFlag(0 + 0x0400 + 1698)) || (_utils.CheckFlag(0 + 345) && !_utils.CheckFlag(0 + 0x0400 + 1699))
                    || (_utils.CheckFlag(0 + 349) && !_utils.CheckFlag(0 + 0x0400 + 1700)) || (_utils.CheckFlag(0 + 355) && !_utils.CheckFlag(0 + 0x0400 + 1701))
                    || (_utils.CheckFlag(0 + 360) && !_utils.CheckFlag(0 + 0x0400 + 1702)) || (_utils.CheckFlag(0 + 361) && !_utils.CheckFlag(0 + 0x0400 + 1703))
                    || (_utils.CheckFlag(0 + 367) && !_utils.CheckFlag(0 + 0x0400 + 1704));
        }

        // Delegates for functions
        [Function(Register.ecx, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool CheckIfSlLvlUp(SocialLink socialLink);

        [Function(Register.esi, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool AlreadyCheckedFunction(IntPtr slInfoAddress);

        [Function(Register.esi, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetRankupSymbolFunction(IntPtr slInfoAddress);
    }
}
