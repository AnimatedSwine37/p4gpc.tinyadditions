using p4gpc.tinyadditions.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Memory.Sources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.tinyadditions.Additions
{
    class BetterSlMenu : Addition
    {
        private IAsmHook _maxRankHook;
        private IAsmHook _rankColourHook;
        private IAsmHook _grabStatusHook;
        private IntPtr _currentStatus;

        public BetterSlMenu(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks) : base(utils, baseAddress, configuration, memory, hooks)
        {
            _currentStatus = _memory.Allocate(1);
            long displayMaxAddress = _utils.SigScan("83 7E ?? 01 C7 44 24 ?? 00 00 00 00", "display max");
            _memory.SafeWrite((IntPtr)(displayMaxAddress + 2), (short)0x0A04); // Switch checking status = 1 to checking if rank = 10
            long detailsMaxAddress = _utils.SigScan("83 7C ?? ?? 01 A1 ?? ?? ?? ??", "details display max");
            _memory.SafeWrite((IntPtr)(detailsMaxAddress + 3), (short)0x0A40); // Switch checking status = 1 to checking if rank = 10
            InitFixMaxDisplayHook();
            InitGrabStatusHook();
            InitRankColourHook();
        }

        // Initialise the hook that fixes the display so it shows Reverse or Broken stuff instead of max if the sl is maxed
        void InitFixMaxDisplayHook()
        {
            long address = _utils.SigScan("75 ?? 8D 04 ?? C7 44 ?? ?? 01 00 00 00", "fixed reverse/broken display");
            _memory.SafeWrite((IntPtr)address, (byte)0xEB); // Switch the jne at this address to a jmp so the code is skipped
            string[] function =
            {
                "use32",
                "push eax",
                // If the status is 2 (reverse) skip the check for it being max
                "cmp ecx, 2",
                "je endHook",
                // Load the rank into eax
                "mov eax, [ebx+eax*4+0x40]",
                // Check if rank is 10
                "cmp eax, 10",
                "jne endHook",
                // If rank is 10 change ecx to 1 to indicate max
                "mov ecx, 1",
                "label endHook",
                "pop eax"
            };
            _maxRankHook = _hooks.CreateAsmHook(function, address + 0x54, AsmHookBehaviour.ExecuteAfter).Activate();
        }

        // Initialises a hook that grabs the current slink's status so it can be use later in the rank colour hook
        private void InitGrabStatusHook()
        {
            long address = _utils.SigScan("83 F8 01 0F 84 ?? ?? ?? ?? 83 C0 FE", "grab status");
            string[] function =
            {
                "use32",
                $"mov [{_currentStatus}], eax"
            };
            _grabStatusHook = _hooks.CreateAsmHook(function, address, AsmHookBehaviour.ExecuteFirst).Activate();
        }

        // Initialises a hook that corrects the colour of the rank text if it's a max and broken/reverse link
        private void InitRankColourHook()
        {
            long rankColourAddress = _utils.SigScan("56 8B 80 ?? ?? ?? ?? ?? ?? C1 E9", "details rank colour");
            string[] function =
            {
                "use32",
                "push esi",
                // If the status is less than 2 (1 or 0) do the original code
                $"cmp byte [{_currentStatus}], 2",
                "jl originalCode",
                "mov eax, dword [eax + 0x18]", // Set to the non max colour
                "jmp endHook",
                "label originalCode",
                "mov eax, dword [eax + 0x80]", // Set to the normal max colour
                "label endHook",

            };
            _rankColourHook = _hooks.CreateAsmHook(function, rankColourAddress, AsmHookBehaviour.DoNotExecuteOriginal).Activate();
        }

        public override void Resume()
        {
            _maxRankHook?.Enable();
            _grabStatusHook?.Enable();
            _rankColourHook?.Enable();
        }

        public override void Suspend()
        {
            _maxRankHook?.Disable();
            _grabStatusHook?.Disable();
            _rankColourHook?.Disable();
        }
    }
}
