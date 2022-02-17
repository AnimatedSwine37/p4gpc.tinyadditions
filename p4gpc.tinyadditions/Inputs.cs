﻿using p4gpc.tinyadditions.Configuration;
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
using System.Threading.Tasks;

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
        private int[] controllerInputHistory = new int[10];
        private int lastControllerInput = 0;
        private int lastKeyboardInput = 0;
        // For accessing memory
        private IMemory _memory;
        // Base address (probably won't ever change)
        private int _baseAddress;
        // Functionalities
        private AutoAdvanceToggle _autoAdvanceToggle;
        private Sprint _sprint;
        private ColouredPartyPanel _colouredPartyPanel;
        private List<Addition> _additions = new List<Addition>();

        private bool utilsInitialised = false;

        // Current mod configuration
        private Config _config { get; set; }
        private PartyPanelConfig _partyPanelConfig { get; set; }
        private Utils _utils;

        public Inputs(IReloadedHooks hooks, Config configuration, PartyPanelConfig partyPanelConfig, Utils utils, int baseAddress, IMemory memory)
        {
            // Initialise private variables
            _config = configuration;
            _hooks = hooks;
            _memory = memory;
            _utils = utils;
            _baseAddress = baseAddress;
            _partyPanelConfig = partyPanelConfig;

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
                long keyboardAddress = -1, controllerAddress = -1;
                List<Task> hookSigScans = new List<Task>();
                hookSigScans.Add(Task.Run(() => keyboardAddress = _utils.SigScan("85 DB 74 05 E8 ?? ?? ?? ?? 8B 7D F8", "keyboard hook")));
                hookSigScans.Add(Task.Run(() => controllerAddress = _utils.SigScan("0F AB D3 89 5D C8", "controller hook")));
                Task.WaitAll(hookSigScans.ToArray());

                if (keyboardAddress != -1 && controllerAddress != -1)
                {
                    _keyboardHook = hooks.CreateAsmHook(keyboardFunction, keyboardAddress, AsmHookBehaviour.ExecuteFirst).Activate();
                    _controllerHook = hooks.CreateAsmHook(controllerFunction, controllerAddress, AsmHookBehaviour.ExecuteFirst).Activate();
                    _utils.Log("Successfully hooked into input functions");
                }
                else
                {
                    _utils.LogError($"Unable to find input functions to hook into. Additions that rely on inputs will not work");
                }
            }
            catch (Exception e)
            {
                _utils.LogError($"Error hooking into input functions. Additions that rely on inputs will not work", e);
            }


            // Initialise additions
            List<Task> additionInits = new List<Task>();
            additionInits.Add(Task.Run(() =>
            {
                _sprint = new Sprint(_utils, _baseAddress, _config, _memory, _hooks);
                _additions.Add(_sprint);
            }));
            additionInits.Add(Task.Run(() =>
            {
                _autoAdvanceToggle = new AutoAdvanceToggle(_utils, _baseAddress, _config, _memory, _hooks);
                _additions.Add(_autoAdvanceToggle);
            }));
            additionInits.Add(Task.Run(() =>
            {
                _additions.Add(new EasyBugCatching(_utils, _baseAddress, _config, _memory, _hooks));
            }));
            additionInits.Add(Task.Run(() =>
            {
                _additions.Add(new ArcanaAffinityBoost(_utils, _baseAddress, _config, _memory, _hooks));
            }));
            additionInits.Add(Task.Run(() =>
            {
                _additions.Add(new CustomItems(_utils, _baseAddress, _config, _memory, _hooks));
            }));
            additionInits.Add(Task.Run(() =>
            {
                _additions.Add(new RankupReady(_utils, _baseAddress, _config, _memory, _hooks));
            }));
            additionInits.Add(Task.Run(() =>
            {
                _additions.Add(new BetterSlMenu(_utils, _baseAddress, _config, _memory, _hooks));
            })); 
            additionInits.Add(Task.Run(() =>
            {
                _colouredPartyPanel = new ColouredPartyPanel(_utils, _baseAddress, _config, _memory, _hooks, _partyPanelConfig);
                _additions.Add(_colouredPartyPanel);
            }));
            Task.WaitAll(additionInits.ToArray());
        }

        public void Suspend()
        {
            _keyboardHook?.Disable();
            _controllerHook?.Disable();
            foreach (Addition addition in _additions)
                addition.Suspend();
        }
        public void Resume()
        {
            _keyboardHook?.Enable();
            _controllerHook?.Enable();
            foreach (Addition addition in _additions)
                addition.Resume();
        }

        public void UpdateConfiguration(Config configuration, PartyPanelConfig partyPanelConfig)
        {
            _config = configuration;
            _partyPanelConfig = partyPanelConfig;
            foreach (Addition addition in _additions)
                addition.UpdateConfiguration(configuration);
            _colouredPartyPanel.UpdateConfiguration(partyPanelConfig);
        }

        // Do stuff with the inputs
        private bool[] sprintPressed = { false, false };
        private void InputHappened(int input, bool risingEdge, bool keyboard)
        {
            _utils.LogDebug($"Input was {(Input)input} and was {(risingEdge ? "rising" : "falling")} edge");

            // Sprint code
            if (_config.SprintEnabled)
            {
                // Check if sprint was pressed
                sprintPressed[1] = sprintPressed[0];
                if (InputInCombo(input, _config.SprintButton, keyboard))
                    sprintPressed[0] = true;
                else
                    sprintPressed[0] = false;

                // Sprint was let go of
                if (sprintPressed[1] && !sprintPressed[0] && !_config.SprintToggle)
                    _sprint.DisableSprint();
                // Check if sprint should be toggled/enabled
                else if (sprintPressed[0] && !_utils.InMenu() && !(_config.SprintDungeonsOnly && !_utils.CheckFlag(3075)))
                {
                    // Toggle sprint
                    if (_config.SprintToggle)
                        _sprint.ToggleSprint();
                    // Hold to sprint
                    else
                        _sprint.EnableSprint();
                }
            }

            // Check if auto advance should be toggled
            if (_config.AdvanceEnabled && _utils.InEvent() && (input == (int)_config.AdvanceButton || InputInCombo(input, _config.AdvanceButton, keyboard)) && risingEdge)
                _autoAdvanceToggle.ToggleAutoAdvance();
        }

        // Get keyboard inputs
        private void KeyboardInputHappened(int input)
        {
            // Initialise item location once inputs start being received 
            if (!utilsInitialised && _utils.InitialiseItemLocation()) utilsInitialised = true;

            // Switch cross and circle as it is opposite compared to controller
            if (input == (int)Input.Circle) input = (int)Input.Cross;
            else if (input == (int)Input.Cross) input = (int)Input.Circle;
            // Decide whether the input needs to be processed (only rising edge for now)
            if (RisingEdge(input, lastKeyboardInput))
                InputHappened(input, true, true);
            else if (FallingEdge(input, lastKeyboardInput))
                InputHappened(input, false, true);
            // Update the last inputs
            lastKeyboardInput = input;
            if (controllerInputHistory[0] == 0)
            {
                if (lastControllerInput != 0)
                    InputHappened(input, false, false);
                lastControllerInput = 0;
            }
            _utils.ArrayPush(controllerInputHistory, 0);
        }

        // Gets controller inputs
        private void ControllerInputHappened(int input)
        {
            // Get the input
            _utils.ArrayPush(controllerInputHistory, input);
            input = GetControllerInput();
            // Decide whether the input needs to be processed
            if (RisingEdge(input, lastControllerInput))
                InputHappened(input, true, false);
            // Update last input
            lastControllerInput = input;
        }

        // Checks if an input was rising edge (the button was just pressed)
        private bool RisingEdge(int currentInput, int lastInput)
        {
            if (currentInput == 0) return false;
            return currentInput != lastInput;
        }

        // Checks if an input was falling edge (the button was let go of)
        private bool FallingEdge(int currentInput, int lastInput)
        {
            return lastInput != 0 && currentInput != lastInput;
        }

        // Gets controller input returning an input combo int if a combo was done (like what keyboard produces)
        private int GetControllerInput()
        {
            int inputCombo = 0;
            int lastInput = 0;
            // Work out the pressed buttons
            for (int i = 0; i < controllerInputHistory.Length; i++)
            {
                int input = controllerInputHistory[i];
                // Start of a combo
                if (lastInput == 0 && input != 0)
                    inputCombo = input;
                // Middle of a combo
                else if (lastInput != 0 && input != 0)
                    inputCombo += input;
                // End of a combo
                else if (input == 0 && lastInput != 0 && i != 1)
                    break;
                // Two 0's in a row means the combo must be over
                else if (i != 0 && input == 0 && lastInput == 0)
                    break;
                lastInput = input;
            }
            return inputCombo;
        }

        // Works out what inputs were pressed if a combination of keys were pressed (only applicable to keyboard)
        private List<Input> GetInputsFromCombo(int inputCombo, bool keyboard)
        {
            // List of the inputs found in the combo
            List<Input> foundInputs = new List<Input>();
            // Check if the input isn't actually a combo, if so we can directly return it
            if (Enum.IsDefined(typeof(Input), inputCombo))
            {
                // Switch cross and circle if it is one of them as it is opposite compared to controller
                if (keyboard && inputCombo == (int)Input.Circle)
                    foundInputs.Add(Input.Cross);
                else if (keyboard && inputCombo == (int)Input.Cross)
                    foundInputs.Add(Input.Circle);
                else
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
                    if (keyboard && possibleInput == (int)Input.Circle)
                        foundInputs.Add(Input.Cross);
                    else if (keyboard && possibleInput == (int)Input.Cross)
                        foundInputs.Add(Input.Circle);
                    else
                        foundInputs.Add((Input)possibleInput);
                }
            }
            if (foundInputs.Count > 0)
                _utils.LogDebug($"Input combo was {string.Join(", ", foundInputs)}");
            return foundInputs;
        }

        // Checks if the desired input is a part of the combo
        // (so individual keyboard inputs aren't missed if they were pressed with other keys like pressing esc whilst running)
        private bool InputInCombo(int inputCombo, Input desiredInput, bool keyboard)
        {
            return GetInputsFromCombo(inputCombo, keyboard).Contains(desiredInput);
        }

        [Function(Register.ebx, Register.edi, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void KeyboardInputFunction(int input);

        [Function(Register.eax, Register.edi, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ControllerInputFunction(int input);
    }
}
