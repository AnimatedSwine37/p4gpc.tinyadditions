using p4gpc.tinyadditions.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X86;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Mod.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;

namespace p4gpc.tinyadditions
{
    class KeyPress
    {
        private ILogger _logger;
        private IReloadedHooks _hooks;
        // For calling C# code from ASM.
        private IReverseWrapper<InputHappenedFunction> _reverseWrapper;
        // For maniplulating input reading hook
        private IAsmHook _asmHook;

        // Current mod configuration
        public Config Configuration { get; set; }

        public KeyPress(ILogger logger, IReloadedHooks hooks, Config configuration)
        {
            // Initialise private variables
            Configuration = configuration;
            _logger = logger;
            _hooks = hooks;

            // Create input hook
            _logger.WriteLine("[TinyAdditions] Hooking into key press function");

            using var thisProcess = Process.GetCurrentProcess();
            int baseAddress = thisProcess.MainModule.BaseAddress.ToInt32();
         
            string[] function =
            {
                $"use32",
                // Not always necessary but good practice;
                // just in case the parent function doesn't preserve them.
                $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{hooks.Utilities.GetAbsoluteCallMnemonics(InputHappened, out _reverseWrapper)}",
                $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };

            _asmHook = hooks.CreateAsmHook(function, 0x266AF738, AsmHookBehaviour.ExecuteFirst).Activate();


        }

        public void Suspend() => _asmHook?.Disable();
        public void Resume() => _asmHook?.Enable();

        // Function that reads all inputs
        private unsafe void InputHappened(int input)
        {
            if(input != 0)
            {
                _logger.WriteLine($"Input was 0x{input:X}");
            }
        }

        [Function(Register.ebx, Register.edi, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void InputHappenedFunction(int ebx);
    }
}
