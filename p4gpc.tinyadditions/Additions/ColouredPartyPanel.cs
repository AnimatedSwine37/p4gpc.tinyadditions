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
        private IAsmHook _inBtlHpBarHook;

        private IReverseWrapper<SetFgColourFunction> _setFgColourReverseWrapper;
        private IReverseWrapper<SetBgColourFunction> _setBgColourReverseWrapper;
        private IReverseWrapper<SetHpBgColourFunction> _setHpBgColourReverseWrapper;

        private PartyPanelConfig _partyPanelConfig;

        private Colour[] _fgColours;
        private Colour[] _bgColours;
        private Colour[] _ogFgColours;
        private Colour[] _ogBgColours;
        private PartyMember _currentMember;

        public ColouredPartyPanel(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks, PartyPanelConfig partyPanelConfig) : base(utils, baseAddress, configuration, memory, hooks)
        {
            _partyPanelConfig = partyPanelConfig;
            InitColourArrays();
            InitInBtlHook();
            if (!_configuration.ColourfulPartyPanelEnabled)
                Suspend();
        }

        // Initialises the arrays that store the party panel colours (so we don't do reflection 100s of times a second which presumably would be badish)
        private void InitColourArrays()
        {
            List<Colour> fgColours = new List<Colour>();
            List<Colour> bgColours = new List<Colour>();
            foreach (PartyMember member in (PartyMember[])Enum.GetValues(typeof(PartyMember)))
            {
                if (member == PartyMember.Rise)
                {
                    // Rise doesn't have a party panel so no need for a colour
                    fgColours.Add(null);
                    bgColours.Add(null);
                }
                else
                {
                    fgColours.Add((Colour)_partyPanelConfig.GetType().GetProperty($"{member}FgColour").GetValue(_partyPanelConfig));
                    bgColours.Add((Colour)_partyPanelConfig.GetType().GetProperty($"{member}BgColour").GetValue(_partyPanelConfig));
                }
            }

            // The og colours are used for rgb garbage since we need to keep a reference of the original value so it's nice and smooth
            _fgColours = fgColours.ToArray();
            _bgColours = bgColours.ToArray();

            // Create 2 new arrays of colours (has to be done like this otherwise the new array would still reference the old colour objects)
            fgColours.Clear();
            bgColours.Clear();
            for (int i = 0; i < _fgColours.Length; i++)
            {
                Colour fgColour = _fgColours[i];
                if (fgColour == null)
                {
                    // Rise's "colour" is null
                    fgColours.Add(null);
                    bgColours.Add(null);
                    continue;
                }
                fgColours.Add(new Colour(fgColour.R, fgColour.G, fgColour.B));
                Colour bgColour = _bgColours[i];
                bgColours.Add(new Colour(bgColour.R, bgColour.G, bgColour.B));
            }
            _ogBgColours = bgColours.ToArray();
            _ogFgColours = fgColours.ToArray();
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
            };
            _inBtlBgHook = _hooks.CreateAsmHook(bgFunction, address - 0x96, AsmHookBehaviour.ExecuteAfter).Activate();

            string[] hpBgFunction =
            {
                "use32",
                // Save xmm0 (will be unintentionally altered)
                $"sub esp, 16", // allocate space on stack
                $"movdqu dqword [esp], xmm0",
                // Save xmm3
                $"sub esp, 16", // allocate space on stack
                $"movdqu dqword [esp], xmm3",
                $"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{_hooks.Utilities.GetAbsoluteCallMnemonics(SetHpBgColour, out _setHpBgColourReverseWrapper)}",
                $"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                //Pop back the value from stack to xmm3
                $"movdqu xmm3, dqword [esp]",
                $"add esp, 16", // re-align the stack
                //Pop back the value from stack to xmm0
                $"movdqu xmm0, dqword [esp]",
                $"add esp, 16", // re-align the stack
            };
            _inBtlHpBarHook = _hooks.CreateAsmHook(hpBgFunction, address + 0x324, AsmHookBehaviour.ExecuteAfter).Activate();
        }

        private void SetFgColour(PartyMember member, IntPtr colourAddress)
        {
            Colour colour = _fgColours[(int)member];
            DoRgbTransition(colour, _ogFgColours[(int)member]);
            byte[] colourBytes = { colour.R, colour.G, colour.B };
            _memory.SafeWrite(colourAddress + 0x84, colourBytes);
        }

        private void SetBgColour(PartyMember member, IntPtr colourAddress)
        {
            _currentMember = member;
            Colour colour = _bgColours[(int)member];
            DoRgbTransition(colour, _ogBgColours[(int)member]);
            byte[] colourBytes = { colour.R, colour.G, colour.B };
            _memory.SafeWrite(colourAddress + 0x84, colourBytes);
        }

        private void SetHpBgColour(IntPtr colourAddress)
        {
            Colour colour = _fgColours[(int)_currentMember];
            byte[] colourBytes = { colour.R, colour.G, colour.B };
            _memory.SafeWrite(colourAddress + 0x84, colourBytes);
        }

        // Makes the epic rgb transition rainbow thing happen
        private void DoRgbTransition(Colour colour, Colour transitionColour)
        {
            if (!_partyPanelConfig.RgbMode)
                return;
            if (colour.Equals(transitionColour))
            {
                byte r = transitionColour.R;
                transitionColour.R = transitionColour.G;
                transitionColour.G = r;
                transitionColour.B = r;
            }

            if (colour.G < transitionColour.G) colour.G++;
            else if (colour.R > transitionColour.R) colour.R--;
            else if (colour.B < transitionColour.B) colour.B++;
            else if (colour.G > transitionColour.G) colour.G--;
            else if (colour.R < transitionColour.R) colour.R++;
            else if (colour.B > transitionColour.B) colour.B--;
        }

        public override void Resume()
        {
            _inBtlBgHook?.Enable();
            _inBtlFgHook?.Enable();
        }

        public override void Suspend()
        {
            _inBtlBgHook?.Disable();
            _inBtlFgHook?.Disable();
        }

        public override void UpdateConfiguration(Config configuration)
        {
            if (_configuration.ColourfulPartyPanelEnabled && !configuration.ColourfulPartyPanelEnabled)
                Suspend();
            else if (!_configuration.ColourfulPartyPanelEnabled && configuration.ColourfulPartyPanelEnabled)
                Resume();
            base.UpdateConfiguration(configuration);
        }

        public void UpdateConfiguration(PartyPanelConfig configuration)
        {
            _partyPanelConfig = configuration;
            InitColourArrays(); // Reload the colour arrays in case they were changed
        }

        // Function delegates
        [Function(new Register[] { Register.edx, Register.edi }, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SetFgColourFunction(PartyMember member, IntPtr colourAddress);

        [Function(new Register[] { Register.edx, Register.edi }, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SetBgColourFunction(PartyMember member, IntPtr colourAddress);

        [Function(Register.edi, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SetHpBgColourFunction(IntPtr colourAddress);
    }
}
