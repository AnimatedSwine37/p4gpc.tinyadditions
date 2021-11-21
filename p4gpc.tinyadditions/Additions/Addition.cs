using p4gpc.tinyadditions.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.Sources;
using System;
using System.Collections.Generic;
using System.Text;

namespace p4gpc.tinyadditions.Additions
{
    abstract class Addition
    {
        // Current mod configuration
        protected Config _configuration;
        protected Utils _utils;

        // Variables for memory editing/reading
        protected IReloadedHooks _hooks;
        protected int _baseAddress;
        protected IMemory _memory;

        public Addition(Utils utils, int baseAddress, Config configuration, IMemory memory, IReloadedHooks hooks)
        {
            _configuration = configuration;
            _utils = utils;
            _hooks = hooks;
            _memory = memory;
            _baseAddress = baseAddress;
        }

        public abstract void Suspend();
        public abstract void Resume();
        public virtual void UpdateConfiguration(Config configuration)
        {
            _configuration = configuration;
        }
    }
}
