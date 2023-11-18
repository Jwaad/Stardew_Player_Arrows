using GenericModConfigMenu;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PlayerArrows.Entry
{

    internal class ModEntry : Mod
    {
        public ModConfig Config { get; private set; }
        IModHelper myHelper;
        LogLevel ProgramLogLevel = LogLevel.Trace; // By default trace logs. but in debug mode, do debug logs

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            // Read config
            Config = this.Helper.ReadConfig<ModConfig>();

            // Attach event handlers to SMAPI's
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        }


        /// <summary>On title screen load: connect this mod to GMCM.: </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // get Generic Mod Config Menu's API (if it's installed) 
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => Config = new ModConfig(),
                save: () =>
                {
                    this.Helper.WriteConfig(Config);
                }
            );

            // add enable / disable to config
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Enabled",
                tooltip: () => "Disables mod entirely.",
                getValue: () => Config.Enabled,
                setValue: value => HandleFieldChange("Enabled", value),
                fieldId: "Enabled"
            );

            // add debug to config
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Debug",
                tooltip: () => "Prints some information in the smapi console, such as where players are",
                getValue: () => Config.Debug,
                setValue: value => HandleFieldChange("Debug", value),
                fieldId: "Debug"
            );
        }

        // Handle what to do on change of each config field
        public void HandleFieldChange(string fieldId, object newValue)
        {
            if (fieldId == "Enabled")
            {
                // If the value didnt change, skip
                if (Config.Enabled == (bool)newValue)
                {
                    return;
                }

                // Detach the mod code from Smapi
                if (Config.Enabled)
                {
                    this.Monitor.Log($"{Game1.player.Name}: Disabling Player Arrows", ProgramLogLevel);
                    this.DetachEventHandlers();
                }
                // reattach the mod code to Smapi
                else
                {
                    this.Monitor.Log($"{Game1.player.Name}: Enabling Player Arrows", ProgramLogLevel);
                    this.AttachEventHandlers();
                }

                // Save the new setting to config
                Config.Enabled = (bool)newValue;
            }
            else if (fieldId == "Debug")
            {
                // If the value didnt change, skip
                if (Config.Debug == (bool)newValue)
                {
                    return;
                }

                this.Monitor.Log($"{Game1.player.Name}: {fieldId} : Changed from {Config.Debug} To {newValue}", ProgramLogLevel);
                Config.Debug = (bool)newValue;
                ProgramLogLevel = Config.Debug ? LogLevel.Debug : LogLevel.Trace;
            }
            else
            {
                this.Monitor.Log($"{Game1.player.Name}: Unhandled config option", ProgramLogLevel);
            }
        }

        // Attach all of the removeable event handlers to SMAPI's
        private void AttachEventHandlers()
        {
            this.Helper.Events.GameLoop.UpdateTicked += OnUpdateTick;
        }

        // Attach all of the removeable event handlers to SMAPI's
        private void DetachEventHandlers()
        {
            this.Helper.Events.GameLoop.UpdateTicked -= OnUpdateTick;
        }

        private void OnQuitGame(object sender, ReturnedToTitleEventArgs e)
        {
            DetachEventHandlers();
        }

        private void OnUpdateTick(object sender, UpdateTickedEventArgs e)
        {
            if (e.IsMultipleOf(30))
            {
                //this.Monitor.Log("TICK", ProgramLogLevel);
            }
        }
    }
}
