using p4gpc.tinyadditions.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;
using Reloaded.Memory.Sources;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static p4gpc.tinyadditions.P4GEnums;
using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;

namespace p4gpc.tinyadditions.Additions
{
    class ArcanaAffinityBoost : Addition
    {
        private IAsmHook? _bonusAffinityHook;
        private IAsmHook? _affinityStartHook;
        private IReverseWrapper<BonusAffinityFunction>? _bonusAffinityReverseWrapper;
        private IReverseWrapper<NormalAffinityFunction>? _normalAffinityReverseWrapper;
        private IReverseWrapper<AffinityStartFunction>? _affinityStartReverseWrapper;
        private IntPtr _affinityScaleAddress;
        private SocialLink currentSocialLink;

        private int _checkBonusAddress = -1;
        private int _functionStartAddress = -1;

        public ArcanaAffinityBoost(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks) : base(utils, baseAddress, configuration, memory, hooks)
        {
            _utils.Log("Initialising Bonus Social Link Affinity");

            // Get bonus addresses
            _utils.SigScan("F3 0F 10 0D ?? ?? ?? ?? 8B 15 ?? ?? ?? ?? 8B 7A ??", "check bonus affinity", 
                (address) =>
                {
                    _checkBonusAddress = address;
                    if (_functionStartAddress != -1)
                        Initialise();
                });
            _utils.SigScan("55 ?? ?? 83 EC 08 56 57 66 ?? ?? 89 55 ??", "social link affinity start",
                (address) =>
                {
                    _functionStartAddress = address;
                    if (_checkBonusAddress != -1)
                        Initialise();
                });
        }

        /// <summary>
        /// Initialise the addition
        /// </summary>
        private void Initialise()
        {
            _memory.SafeRead((IntPtr)(_checkBonusAddress + 4), out _affinityScaleAddress);

            // Get Social Link Id Hook
            string[] initialFunction =
            {
                $"use32",
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_hooks.Utilities.GetAbsoluteCallMnemonics(AffinityStart, out _affinityStartReverseWrapper)}",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };
            _affinityStartHook = _hooks.CreateAsmHook(initialFunction, _functionStartAddress, AsmHookBehaviour.ExecuteFirst).Activate();

            // Change Affinity Scaling Hook
            string[] function =
            {
                $"use32",
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_hooks.Utilities.GetAbsoluteCallMnemonics(BonusAffinity, out _bonusAffinityReverseWrapper)}",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                $"movss xmm1, [{_affinityScaleAddress}]",
                // Save xmm1 (will be unintentionally reset when resetting the 1.0 scale)
                $"sub esp, 16", // allocate space on stack
                $"movdqu dqword [esp], xmm1",
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_hooks.Utilities.GetAbsoluteCallMnemonics(NormalAffinity, out _normalAffinityReverseWrapper)}",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                //Pop back the value from stack to xmm1
                $"movdqu xmm1, dqword [esp]",
                $"add esp, 16", // re-align the stack
            };
            _bonusAffinityHook = _hooks.CreateAsmHook(function, _checkBonusAddress, AsmHookBehaviour.ExecuteAfter).Activate();
            _utils.Log("Bonus Social Link Affinity initialised");

        }

        public override void Resume()
        {
            _bonusAffinityHook?.Enable();
            _affinityStartHook?.Enable();
        }

        public override void Suspend()
        {
            _bonusAffinityHook?.Disable();
            _affinityStartHook?.Disable();
        }

        // If the player has the max s link item sets the scale for normal affinity
        // to the one when you have a Persona of a matching Arcana (1.51)
        private void BonusAffinity(int eax)
        {
            if (!_configuration.AlwaysBoostedAffinity && !_configuration.AffinityBoostEnabled) return;
            // Always boosted
            if (_configuration.AlwaysBoostedAffinity)
            {
                _memory.SafeWrite(_affinityScaleAddress, 1.51f);
                _utils.LogDebug($"Giving bonus affinity for {currentSocialLink}");
                return;
            }
            // Normal max item boost check
            if (!SocialLinkItems.TryGetValue(currentSocialLink, out Item sLinkItem)) return;
            if (_configuration.AlwaysBoostedAffinity || _utils.GetItem((int)sLinkItem) > 0)
            {
                _memory.SafeWrite(_affinityScaleAddress, 1.51f);
                _utils.LogDebug($"Giving bonus affinity for {currentSocialLink}");
            }
            else
                _utils.LogDebug($"Leaving normal affinity for {currentSocialLink}");
        }

        // Returns the scale for affinity back to 1.0
        private void NormalAffinity(int eax)
        {
            _memory.SafeWrite(_affinityScaleAddress, 1.0f);
        }

        // Gets the social link id from edi to be used later when deciding on bonus affinity
        private void AffinityStart(SocialLink socialLink)
        {
            currentSocialLink = socialLink;
        }

        // Hooked function delegate
        [Function(Register.eax, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void BonusAffinityFunction(int eax);

        [Function(Register.edi, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void AffinityStartFunction(SocialLink sLink);

        [Function(Register.eax, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void NormalAffinityFunction(int eax);

    }
}
