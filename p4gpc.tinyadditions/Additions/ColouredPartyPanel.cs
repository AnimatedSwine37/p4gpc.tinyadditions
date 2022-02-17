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
    class ColouredPartyPanel : Addition
    {
        private IAsmHook _inBtlFgHook;
        private IAsmHook _inBtlBgHook;

        private IReverseWrapper<SetFgColourFunction> _setFgColourReverseWrapper;
        private IReverseWrapper<SetBgColourFunction> _setBgColourReverseWrapper;

        public ColouredPartyPanel(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks) : base(utils, baseAddress, configuration, memory, hooks)
        {
            InitInBtlHook();
        }

        // Initialises the hook for changing colours while in battle
        private void InitInBtlHook()
        {
            long address = _utils.SigScan("E8 ?? ?? ?? ?? F3 0F 10 94 24 ?? ?? ?? ?? 83 C4 04 F3 0F 10 8C 24 ?? ?? ?? ??", "in battle colour");
            string[] fgFunction =
            {
                "use32",
                // Save xmm0 (will be unintentionally altered)
                $"sub esp, 16", // allocate space on stack
                $"movdqu dqword [esp], xmm0",
                // Save xmm3
                $"sub esp, 16", // allocate space on stack
                $"movdqu dqword [esp], xmm3",
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_hooks.Utilities.GetAbsoluteCallMnemonics(SetFgColour, out _setFgColourReverseWrapper)}",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                //Pop back the value from stack to xmm3
                $"movdqu xmm3, dqword [esp]",
                $"add esp, 16", // re-align the stack
                //Pop back the value from stack to xmm0
                $"movdqu xmm0, dqword [esp]",
                $"add esp, 16", // re-align the stack
                //"mov byte [edi + 0x84], 210",
                //"mov byte [edi + 0x85], 56",
                //"mov byte [edi + 0x86], 49",
            };
            _inBtlFgHook = _hooks.CreateAsmHook(fgFunction, address - 10, AsmHookBehaviour.ExecuteAfter).Activate();

            string[] bgFunction =
            {
                "use32",
                // Save xmm0 (will be unintentionally altered)
                $"sub esp, 16", // allocate space on stack
                $"movdqu dqword [esp], xmm0",
                // Save xmm3
                $"sub esp, 16", // allocate space on stack
                $"movdqu dqword [esp], xmm3",
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_hooks.Utilities.GetAbsoluteCallMnemonics(SetBgColour, out _setBgColourReverseWrapper)}",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                //Pop back the value from stack to xmm3
                $"movdqu xmm3, dqword [esp]",
                $"add esp, 16", // re-align the stack
                //Pop back the value from stack to xmm0
                $"movdqu xmm0, dqword [esp]",
                $"add esp, 16", // re-align the stack
                //"mov byte [edi + 0x84], 168",
                //"mov byte [edi + 0x85], 32",
                //"mov byte [edi + 0x86], 25",
            };
            _inBtlBgHook = _hooks.CreateAsmHook(bgFunction, address - 0x96, AsmHookBehaviour.ExecuteAfter).Activate();
        }

        private void SetFgColour(PartyMember member, IntPtr colourAddress)
        {
            member++; // The member ids start at 0 here but start at 1 in the enum
            byte[] colour = null;
            if (member == PartyMember.Yukiko)
            {
                colour = new byte[] { 210, 56, 49 };
            }
            if (colour != null)
                _memory.SafeWrite(colourAddress + 0x84, colour);
        }

        private void SetBgColour(PartyMember member, IntPtr colourAddress)
        {
            member++; // The member ids start at 0 here but start at 1 in the enum
            byte[] colour = null;
            if (member == PartyMember.Yukiko)
            {
                colour = new byte[] { 168, 32, 25 };
            }
            if (colour != null)
                _memory.SafeWrite(colourAddress + 0x84, colour);
        }

        public override void Resume()
        {
            throw new NotImplementedException();
        }

        public override void Suspend()
        {
            throw new NotImplementedException();
        }

        // Function delegates
        [Function(new Register[] { Register.edx, Register.edi }, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SetFgColourFunction(PartyMember member, IntPtr colourAddress);

        [Function(new Register[] { Register.edx, Register.edi }, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SetBgColourFunction(PartyMember member, IntPtr colourAddress);
    }
}
