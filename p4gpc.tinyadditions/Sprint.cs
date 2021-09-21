using p4gpc.tinyadditions.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using System;
using System.Runtime.InteropServices;
using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;

namespace p4gpc.tinyadditions
{
    class Sprint
    {
        // Current mod configuration
        public Config Configuration { get; set; }
        private Utils _utils;

        // Variables for memory editing/reading
        private IReloadedHooks _hooks;
        private int _baseAddress;
        private IMemory _memory;
        private IntPtr _speedLocation;

        // For calling C# code from ASM.
        private IReverseWrapper<SpeedChangedFunction> _speedChangeReverseWrapper;
        private IAsmHook _speedChangeHook;

        // Keep track of the normal speed
        private float normalSpeed = 0;

        public Sprint(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks)
        {
            Configuration = configuration;
            _utils = utils;
            _memory = memory;
            _baseAddress = baseAddress;
            _speedLocation = (IntPtr)0x21AB56F4 + _baseAddress;
            _hooks = hooks;

            try
            {

                _utils.Log("Initialising sprint");
                //hooks.Utilities.GetAbsoluteCallMnemonics(SpeedChanged, out _speedChangeReverseWrapper)

                // Initialise speed factor change hook (when switching fields the normal speed changes sometimes)
                string[] function =
                {
                    $"use32",
                    // Not always necessary but good practice;
                    // just in case the parent function doesn't preserve them.
                    $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                    $"{hooks.Utilities.GetAbsoluteCallMnemonics(SpeedChanged, out _speedChangeReverseWrapper)}",
                    $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
                };

                // Create function hook
                _speedChangeHook = _hooks.CreateAsmHook(function, _baseAddress + 0x23F4AD1F, AsmHookBehaviour.ExecuteAfter).Activate();
            }
            catch (Exception e)
            {
                _utils.LogError($"Error hooking into input functions. Unloading mod", e);
                Suspend();
                return;
            }
            _utils.Log("Successfully initialised sprint");
        }

        public void Suspend() => _speedChangeHook?.Disable();
        public void Resume() => _speedChangeHook?.Activate();


        public void ToggleSprint()
        {
            try
            {
                _utils.Log($"Toggling sprint.");
                // Get current speed
                _memory.SafeRead(_speedLocation, out float currentSpeed);
                // Alter speed
                if (currentSpeed > normalSpeed)
                {
                    // Turn sprint off
                    _utils.LogDebug($"Sprint off. New speed is {normalSpeed}");
                    _memory.SafeWrite(_speedLocation, normalSpeed);
                }
                else
                {
                    // Turn sprint on
                    _utils.LogDebug($"Sprint on. New speed is {currentSpeed * Configuration.SprintSpeed}");
                    _memory.SafeWrite(_speedLocation, (currentSpeed * Configuration.SprintSpeed));
                }
            }
            catch (Exception e)
            {
                _utils.LogError($"Error changing speed", e);
            }
        }

        private void SpeedChanged(int eax)
        {
            try
            {
                // Update normal speed with the new one
                _memory.SafeRead(_speedLocation, out normalSpeed);
                _utils.LogDebug($"Normal speed changed to {normalSpeed}");
            }
            catch (Exception e)
            {
                _utils.LogError($"Error reading normal speed", e);
            }
        }

        // Hooked function delegate
        [Function(Register.eax, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SpeedChangedFunction(int eax);
    }
}
