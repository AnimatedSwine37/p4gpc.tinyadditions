using p4gpc.tinyadditions.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using System;
namespace p4gpc.tinyadditions
{
    class AutoAdvanceToggle
    {
        private ILogger _logger;
        private IReloadedHooks _hooks;
        private IMemory _memory;
        private int _baseAddress;
        public Config Configuration {get; set;}

        public AutoAdvanceToggle(ILogger logger, int baseAddress, Config configuration, IMemory memory,IReloadedHooks hooks)
        {
            Configuration = configuration;
            _logger = logger;
            _hooks = hooks;
            _memory = memory;
            _baseAddress = baseAddress;
            _logger.WriteLine("[TinyAdditons] Initizalizing auto-advance toggle");
        }
        public void ToggleAutoAdvance() {
            try
            {
                _memory.SafeRead((IntPtr)(_baseAddress + 0x49DD563), out byte autoAdvance);
                // 5th bit is the flag
                autoAdvance ^= 0b00001000;

                _memory.SafeWrite((IntPtr)(_baseAddress + 0x49DD563), ref autoAdvance);
                _logger.WriteLine("[TinyAdditions] Toggled Auto Advance");
            } catch (Exception e)
            {
                _logger.WriteLine($"[TinyAdditions] Couldn't Read or write address for auto advance toggle {e.Message}");
            }
        }
    }
}