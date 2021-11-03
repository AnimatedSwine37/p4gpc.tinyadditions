using p4gpc.tinyadditions.Additions;
using p4gpc.tinyadditions.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using System;
namespace p4gpc.tinyadditions
{
    class AutoAdvanceToggle: Addition
    {
        private bool enabled;
        public AutoAdvanceToggle(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks): base(utils, baseAddress, configuration, memory, hooks)
        {
            _utils.Log("Initialised auto-advance toggle");
            enabled = true;
        }

        public void ToggleAutoAdvance()
        {
            if (!enabled) return;
            try
            {
                _memory.SafeRead((IntPtr)(_baseAddress + 0x49DD563), out byte autoAdvance);
                // 5th bit is the flag
                autoAdvance ^= 0b00001000;

                _memory.SafeWrite((IntPtr)(_baseAddress + 0x49DD563), ref autoAdvance);
                _utils.Log("Toggled Auto Advance");
            }
            catch (Exception e)
            {
                _utils.LogError("Couldn't Read or write address for auto advance toggle", e);
            }
        }

        public override void Resume()
        {
            enabled = true;
        }

        public override void Suspend()
        {
            enabled = false;
        }
    }
}