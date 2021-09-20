using p4gpc.tinyadditions.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;

namespace p4gpc.tinyadditions
{
    class Sprint
    {
        private ILogger _logger;
        private IReloadedHooks _hooks;

        // Current mod configuration
        public Config Configuration { get; set; }

        public Sprint(ILogger logger, IReloadedHooks hooks, Config configuration)
        {
            Configuration = configuration;
            _logger = logger;
            _hooks = hooks;

            _logger.WriteLine("[TinyAdditions] Initialising sprint");
        }

    }
}
