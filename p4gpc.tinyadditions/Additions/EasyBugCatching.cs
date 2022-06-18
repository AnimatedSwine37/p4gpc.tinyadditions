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
        private IAsmHook? _easyBugsHook;

        public EasyBugCatching(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks) : base(utils, baseAddress, configuration, memory, hooks)
        {
            _utils.SigScan("F3 0F 2C C8 83 F9 F6", "easy bug catching", Initialise);
        }

        private void Initialise(int address)
        {
            // Create the function hook
            string[] function =
            {
                $"use32",
                // Set ecx to 3, making the catch always perfect
                $"mov ecx, 3",
                // Always false, this would normally the check for a failed attempt
                $"cmp ecx, -1",
            };
            _easyBugsHook = _hooks.CreateAsmHook(function, address, AsmHookBehaviour.DoNotExecuteOriginal).Activate();
            _utils.Log("Easy Bug Catching initialised");

            if (!_configuration.EasyBugCatchingEnabled)
                _easyBugsHook.Disable();
        }

        public override void Resume() => _easyBugsHook?.Enable();

        public override void Suspend() => _easyBugsHook?.Disable();

        public override void UpdateConfiguration(Config configuration)
        {
            if (_configuration.EasyBugCatchingEnabled && !configuration.EasyBugCatchingEnabled)
                Suspend();
            if (!_configuration.EasyBugCatchingEnabled && configuration.EasyBugCatchingEnabled)
                Resume();
            _configuration = configuration;
        }
    }
}
