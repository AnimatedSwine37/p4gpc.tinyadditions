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
using static p4gpc.tinyadditions.Utils;
using p4gpc.tinyadditions.Additions;
using Reloaded.Memory.Sigscan;
using Reloaded.Memory.Sigscan.Structs;

namespace p4gpc.tinyadditions
{
    class Inputs
    {
        private IReloadedHooks _hooks;
        // For calling C# code from ASM.
        private IReverseWrapper<KeyboardInputFunction> _keyboardReverseWrapper;
        private IReverseWrapper<ControllerInputFunction> _controllerReverseWrapper;
        // For maniplulating input reading hooks
        private IAsmHook _keyboardHook;
        private IAsmHook _controllerHook;
        // Keeps track of the last inputs for rising/falling edge detection
        private int[] lastControllerInputs = { 0, 0 };
        private int lastKeyboardInput = 0;
        // For accessing memory
        private IMemory _memory;
        // Base address (probably won't ever change)
        private int _baseAddress;
        // Functionalities
        private Sprint _sprint;
        private AutoAdvanceToggle _autoAdvanceToggle;
        private EasyBugCatching _easyBugCatching;

        // Current mod configuration
        private Config _config { get; set; }
        private Utils _utils;

        public Inputs(IReloadedHooks hooks, Config configuration, Utils utils, int baseAddress, IMemory memory)
        {
            // Initialise private variables
            _config = configuration;
            _hooks = hooks;
            _memory = memory;
            _utils = utils;
            _baseAddress = baseAddress;

            // Create input hook
            _utils.Log("Hooking into input functions");

            try
            {
                // Define functions (they're the same but use different reverse wrappers)
                string[] keyboardFunction =
                {
                $"use32",
                // Not always necessary but good practice;
                // just in case the parent function doesn't preserve them.
                $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{hooks.Utilities.GetAbsoluteCallMnemonics(KeyboardInputHappened, out _keyboardReverseWrapper)}",
                $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };
                string[] controllerFunction =
                {
                $"use32",
                // Not always necessary but good practice;
                // just in case the parent function doesn't preserve them.
                $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{hooks.Utilities.GetAbsoluteCallMnemonics(ControllerInputHappened, out _controllerReverseWrapper)}",
                $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };

                // Create function hooks
                long keyboardAddress = _utils.SigScan("85 DB 74 05 E8 ?? ?? ?? ?? 8B 7D F8", "keyboard hook");
                long controllerAddress = _utils.SigScan("0F AB D3 89 5D C8", "controller hook");
                _keyboardHook = hooks.CreateAsmHook(keyboardFunction, keyboardAddress, AsmHookBehaviour.ExecuteFirst).Activate(); // call 85 DB 74 05 E8 7F 81 13 DA
                _controllerHook = hooks.CreateAsmHook(controllerFunction, controllerAddress, AsmHookBehaviour.ExecuteFirst).Activate();
            }
            catch (Exception e)
            {
                _utils.LogError($"Error hooking into input functions. Unloading mod", e);
                Suspend();
                return;
            }

            _utils.Log("Successfully hooked into input functions");

            _sprint = new Sprint(_utils, _baseAddress, _config, _memory, _hooks);
            _autoAdvanceToggle = new AutoAdvanceToggle(_utils, _baseAddress, _config, _memory, _hooks);
            _easyBugCatching = new EasyBugCatching(_utils, _baseAddress, _config, _memory, _hooks);
        }

        public void Suspend()
        {
            _keyboardHook?.Disable();
            _controllerHook?.Disable();
            _sprint?.Suspend();
            _autoAdvanceToggle?.Suspend();
            _easyBugCatching?.Suspend();
        }
        public void Resume()
        {
            _keyboardHook?.Enable();
            _controllerHook?.Enable();
            _sprint?.Resume();
            _autoAdvanceToggle?.Resume();
            _easyBugCatching?.Resume();
        }

        public void UpdateConfiguration(Config configuration)
        {
            _config = configuration;
            _sprint.Configuration = configuration;
            _autoAdvanceToggle.Configuration = configuration;
            _easyBugCatching.UpdateConfiguration(configuration);
        }

        // Function that reads all inputs
        private void InputHappened(int input, bool keyboard)
        {
            _utils.LogDebug($"Input was {(Input)input}");
            // Check if sprint was pressed
            if (_config.SprintEnabled && (input == (int)_config.SprintButton || (keyboard && InputInCombo(input, _config.SprintButton))))
                _sprint.ToggleSprint();
            if (_config.AdvanceEnabled && InEvent() && (input == (int)_config.AdvanceButton || (keyboard && InputInCombo(input, _config.AdvanceButton))))
                _autoAdvanceToggle.ToggleAutoAdvance();

        }

        // Switches keyboard inputs to match controller ones
        private void KeyboardInputHappened(int input)
        {
            // Switch cross and circle as it is opposite compared to controller
            if (input == (int)Input.Circle) input = (int)Input.Cross;
            else if (input == (int)Input.Cross) input = (int)Input.Circle;
            // Decide whether the input needs to be processed (only rising edge for now)
            if (RisingEdge(input, lastKeyboardInput))
                InputHappened(input, true);
            // Update the last input
            lastKeyboardInput = input;
            lastControllerInputs[1] = lastControllerInputs[0];
            lastControllerInputs[0] = 0;
        }

        // Gets controller inputs
        private void ControllerInputHappened(int input)
        {
            //_utils.LogDebug($"Debug input was {input}, lastInput: {lastControllerInputs[0]}, {lastControllerInputs[1]}, {lastControllerInputs[2]} ");
            // Decide whether the input needs to be processed (only rising edge for now)
            if (RisingEdge(input, lastControllerInputs[0]) && RisingEdge(input, lastControllerInputs[1]))
                InputHappened(input, false);
            // Update the last input
            lastControllerInputs[1] = lastControllerInputs[0];
            lastControllerInputs[0] = input;
        }

        // Checks if an input was rising edge (the button was just pressed)
        private bool RisingEdge(int currentInput, int lastInput)
        {
            if (currentInput == 0) return false;
            return currentInput != lastInput;
        }

        // Works out what inputs were pressed if a combination of keys were pressed (only applicable to keyboard)
        private List<Input> GetInputsFromCombo(int inputCombo)
        {
            // List of the inputs found in the combo
            List<Input> foundInputs = new List<Input>();
            // Check if the input isn't actually a combo, if so we can directly return it
            if (Enum.IsDefined(typeof(Input), inputCombo))
            {

                foundInputs.Add((Input)inputCombo);
                return foundInputs;
            }

            // Get all possible inputs as an array
            var possibleInputs = Enum.GetValues(typeof(Input));
            // Reverse the array so it goes from highest input value to smallest
            Array.Reverse(possibleInputs);
            // Go through each possible input to find out which are a part of the key combo
            foreach (int possibleInput in possibleInputs)
            {
                // If input - possibleInput is greater than 0 that input must be a part of the combination
                // This is the same idea as converting bits to decimal
                if (inputCombo - possibleInput >= 0)
                {
                    inputCombo -= possibleInput;
                    // Switch cross and circle if it is one of them as it is opposite compared to controller
                    if (possibleInput == (int)Input.Circle)
                        foundInputs.Add(Input.Cross);
                    else if (possibleInput == (int)Input.Cross)
                        foundInputs.Add(Input.Circle);
                    else
                        foundInputs.Add((Input)possibleInput);
                }
            }
            _utils.LogDebug($"Input combo was {string.Join(", ", foundInputs)}");
            return foundInputs;
        }

        // Checks if the desired input is a part of the combo
        // (so individual keyboard inputs aren't missed if they were pressed with other keys like pressing esc whilst running)
        private bool InputInCombo(int inputCombo, Input desiredInput)
        {
            return GetInputsFromCombo(inputCombo).Contains(desiredInput);
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
