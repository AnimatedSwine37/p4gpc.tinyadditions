using p4gpc.tinyadditions.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.Sources;
using Reloaded.Memory.Sigscan;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Reloaded.Hooks.Definitions.Enums;

namespace p4gpc.tinyadditions.Additions
{
    class EasyBugCatching : Addition
    {
        // For calling C# code from ASM.
        //private IReverseWrapper<SpeedChangedFunction> _speedChangeReverseWrapper;
        private IAsmHook _easyBugsHook;

        public EasyBugCatching(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks) : base(utils, baseAddress, configuration, memory, hooks)
        {
            _utils.Log("Initialising Easy Bug Catching");
            // Get the function address
            long functionAddress = _utils.SigScan("F3 0F 2C C8 83 F9 F6", baseAddress, "easy bug catching");
            if (functionAddress == -1) return;

            // Create the function hook
            string[] function =
            {
                $"use32",
                // Set ecx to 3, making the catch always perfect
                $"mov ecx, 3",
                // Always false, this would normally the check for a failed attempt
                $"cmp ecx, -1",
            };
            _easyBugsHook = hooks.CreateAsmHook(function, functionAddress, AsmHookBehaviour.DoNotExecuteOriginal).Activate();
            _utils.Log("Easy Bug Catching initialised");
        }

        public override void Resume() => _easyBugsHook?.Enable();

        public override void Suspend() => _easyBugsHook?.Disable();

        public void UpdateConfiguration(Config configuration)
        {
            if (Configuration.EasyBugCatchingEnabled && !configuration.EasyBugCatchingEnabled)
                Suspend();
            if (!Configuration.EasyBugCatchingEnabled && configuration.EasyBugCatchingEnabled)
                Resume();
            Configuration = configuration;
        }
    }
}
