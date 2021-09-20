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
using Reloaded.Memory.Sources;

namespace p4gpc.tinyadditions
{
    class Inputs
    {
        private ILogger _logger;
        private IReloadedHooks _hooks;
        // For calling C# code from ASM.
        private IReverseWrapper<KeyboardInputFunction> _keyboardReverseWrapper;
        private IReverseWrapper<ControllerInputFunction> _controllerReverseWrapper;
        // For maniplulating input reading hooks
        private IAsmHook _keyboardHook;
        private IAsmHook _controllerHook;
        // Keeps track of the last inputs for rising/falling edge detection
        private int[] lastInput = {0, 0};
        // For accessing memory
        private IMemory _memory;
        // Base address (probably won't ever change)
        private int _baseAddress;

        // Current mod configuration
        public Config Configuration { get; set; }

        enum Input
        {
            Select = 0x1,
            Start = 0x8,
            Up = 0x10,
            Right = 0x20,
            Down = 0x40,
            Left = 0x80,
            LB = 0x400,
            RB = 0x800,
            Triangle = 0x1000,
            Circle = 0x2000,
            Cross = 0x4000,
            Square = 0x8000
        };

        public Inputs(ILogger logger, IReloadedHooks hooks, Config configuration)
        {
            // Initialise private variables
            Configuration = configuration;
            _logger = logger;
            _hooks = hooks;
            _memory = new Memory();

            // Create input hook
            _logger.WriteLine("[TinyAdditions] Hooking into input functions");

            try
            {
                using var thisProcess = Process.GetCurrentProcess();
                _baseAddress = thisProcess.MainModule.BaseAddress.ToInt32();

                // Define functions (they're the same but use different reverse wrappers)
                string[] keyboardFunction =
                {
                $"use32",
                // Not always necessary but good practice;
                // just in case the parent function doesn't preserve them.
                $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{hooks.Utilities.GetAbsoluteCallMnemonics(InputHappened, out _keyboardReverseWrapper)}",
                $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };
                string[] controllerFunction =
                {
                $"use32",
                // Not always necessary but good practice;
                // just in case the parent function doesn't preserve them.
                $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{hooks.Utilities.GetAbsoluteCallMnemonics(InputHappened, out _controllerReverseWrapper)}",
                $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };

                // Create function hooks
                _keyboardHook = hooks.CreateAsmHook(keyboardFunction, _baseAddress + 0x262AF738, AsmHookBehaviour.ExecuteFirst).Activate();
                _controllerHook = hooks.CreateAsmHook(controllerFunction, _baseAddress + 0x262AF780, AsmHookBehaviour.ExecuteFirst).Activate();
            } catch(Exception e)
            {
                _logger.WriteLine($"[TinyAdditions] Error hooking into input functions. Unloading mod: {e.Message}", System.Drawing.Color.Red);
                Suspend();
                return;
            }

            _logger.WriteLine($"[TinyAdditions] Successfully hooked into input functions");

        }

        public void Suspend()
        {
            _keyboardHook?.Disable();
            _controllerHook?.Disable();
        }
        public void Resume()
        {
            _keyboardHook?.Enable();
            _controllerHook?.Disable();
        }

        // Function that reads all inputs
        private unsafe void InputHappened(int input)
        {
            // Check for button presses on rising edge (need to keep track of last two as controller input gives a 0 between every input)
            if (lastInput[0] == 0 && lastInput[1] == 0 && input != 0)
            {
                _logger.WriteLine($"Input was {(Input)input}");
                if (InEvent() && input == (int)Input.Down)
                    _logger.WriteLine("Auto advance toggled (not implemented yet)");
            }
            lastInput[1] = lastInput[0];
            lastInput[0] = input;
        }

        private bool InEvent()
        {
            // Get the current event
            _memory.SafeRead((IntPtr)_baseAddress + 0x9CAB94, out short[] currentEvent, 3);
            // If either the event major or minor isn't 0 we are in an event otherwise we're not
            return currentEvent[0] != 0 || currentEvent[2] != 0;
        }

        [Function(Register.ebx, Register.edi, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void KeyboardInputFunction(int input);

        [Function(Register.eax, Register.edi, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ControllerInputFunction(int input);
    }
}
