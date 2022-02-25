using p4gpc.inputlibrary.interfaces;
using p4gpc.tinyadditions.Configuration;
using p4gpc.tinyadditions.Configuration.Implementation;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using System;
using System.Diagnostics;

namespace p4gpc.tinyadditions
{
    public class Program : IMod
    {
        /// <summary>
        /// Your mod if from ModConfig.json, used during initialization.
        /// </summary>
        private const string MyModId = "p4gpc.tinyadditions";

        /// <summary>
        /// Used for writing text to the console window.
        /// </summary>
        private ILogger _logger;

        /// <summary>
        /// Provides access to the mod loader API.
        /// </summary>
        private IModLoader _modLoader;

        /// <summary>
        /// Stores the contents of your mod's configuration. Automatically updated by template.
        /// </summary>
        private Config _configuration;

        /// <summary>
        /// Stores a reference to the controller hook in other mod.
        /// </summary>
        private WeakReference<IInputHook> _inputHook;

        /// <summary>
        /// An interface to Reloaded's the function hooks/detours library.
        /// See: https://github.com/Reloaded-Project/Reloaded.Hooks
        ///      for documentation and samples. 
        /// </summary>
        private IReloadedHooks _hooks;

        /// <summary>
        /// Configuration of the current mod.
        /// </summary>
        private IModConfig _modConfig = null!;

        private Inputs _inputs;
        private Utils _utils;

        /// <summary>
        /// Entry point for your mod.
        /// </summary>
        public void StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig)
        {
#if DEBUG
        // Attaches debugger in debug mode; ignored in release.
        Debugger.Launch();
#endif

            _modLoader = (IModLoader)loaderApi;
            _modConfig = (IModConfig)modConfig;
            _logger = (ILogger)_modLoader.GetLogger();
            _modLoader.GetController<IReloadedHooks>().TryGetTarget(out _hooks!);

            // Your config file is in Config.json.
            // Need a different name, format or more configurations? Modify the `Configurator`.
            // If you do not want a config, remove Configuration folder and Config class.
            var configurator = new Configurator(_modLoader.GetModConfigDirectory(_modConfig.ModId));
            configurator.Migrate(_modLoader.GetDirectoryForModId(_modConfig.ModId), configurator.ConfigFolder);
            _configuration = configurator.GetConfiguration<Config>(0);
            _configuration.ConfigurationUpdated += OnConfigurationUpdated;

            /*
                Your mod code starts below.
                Visit https://github.com/Reloaded-Project for additional optional libraries.
            */
            using var thisProcess = Process.GetCurrentProcess();
            int baseAddress = thisProcess.MainModule.BaseAddress.ToInt32();
            IMemory memory = new Memory();
            _utils = new Utils(_configuration, _logger, baseAddress, memory);
            _inputs = new Inputs(_hooks, _configuration, _utils, baseAddress, memory);
            _modLoader.ModLoaded += ModLoaded;
        }

        private void ModLoaded(IModV1 modInstance, IModConfigV1 modConfig)
        {
            if (modConfig.ModId == "p4gpc.inputlibrary") SetupInput();
        }

        private void SetupInput()
        {
            _inputHook = _modLoader.GetController<IInputHook>();
            if (_inputHook.TryGetTarget(out var target)) target.OnInput += TargetOnInputEvent;
        }

        private void TargetOnInputEvent(int input, bool risingEdge, bool controlType)
        {
            _inputs.SetInputEvent(input, risingEdge, controlType);
        }

        private void OnConfigurationUpdated(IConfigurable obj)
        {
            /*
                This is executed when the configuration file gets updated by the user
                at runtime.
            */

            // Replace configuration with new.
            _configuration = (Config)obj;

            _utils.Log("Config Updated: Applying");

            // Apply settings from configuration.
            _inputs.UpdateConfiguration(_configuration);
            _utils.Configuration = _configuration;
        }

        /* Mod loader actions. */
        public void Suspend()
        {
            _inputs.Suspend();
        }

        public void Resume()
        {
            _inputs.Resume();
        }

        public void Unload()
        {
            Suspend();
        }

        /*  If CanSuspend == false, suspend and resume button are disabled in Launcher and Suspend()/Resume() will never be called.
            If CanUnload == false, unload button is disabled in Launcher and Unload() will never be called.
        */
        public bool CanUnload() => true;
        public bool CanSuspend() => true;

        /* Automatically called by the mod loader when the mod is about to be unloaded. */
        public Action Disposing { get; }
    }
}
