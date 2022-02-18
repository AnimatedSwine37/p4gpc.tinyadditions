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
using System.Threading.Tasks;
using p4gpc.inputlibrary.interfaces;

namespace p4gpc.tinyadditions
{
    class Inputs
    {
        private IReloadedHooks _hooks;
        // For accessing memory
        private IMemory _memory;
        // Base address (probably won't ever change)
        private int _baseAddress;
        // Functionalities
        private AutoAdvanceToggle _autoAdvanceToggle;
        private Sprint _sprint;
        private ColouredPartyPanel _colouredPartyPanel;
        private List<Addition> _additions = new List<Addition>();

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

            _utils.Log("Initialise Additions");

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
        public void SetInputEvent(int input, bool risingEdge, bool keyboard)
        {
            // sprint code
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
                // Check if auto advance should be toggled
                if (_config.AdvanceEnabled && _utils.InEvent() && (input == (int)_config.AdvanceButton || InputInCombo(input, _config.AdvanceButton, keyboard)) && risingEdge)
                    _autoAdvanceToggle.ToggleAutoAdvance();
            }
        }

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

        private bool InputInCombo(int inputCombo, Input desiredInput, bool keyboard)
        {
            return GetInputsFromCombo(inputCombo, keyboard).Contains(desiredInput);
        }

        public void Suspend()
        {
            foreach (Addition addition in _additions)
                addition.Suspend();
        }
        public void Resume()
        {
            foreach (Addition addition in _additions)
                addition.Resume();
        }

        public void UpdateConfiguration(Config configuration)
        {
            _config = configuration;
            foreach (Addition addition in _additions)
                addition.UpdateConfiguration(configuration);
        }
    }
}
