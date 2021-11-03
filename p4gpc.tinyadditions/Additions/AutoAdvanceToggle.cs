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
            bool autoAdvanceFlag = _utils.CheckFlag(59);
            _utils.ChangeFlag(59, !autoAdvanceFlag);
            _utils.Log("Toggled Auto Advance");
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