using GenericModConfigMenu;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using System;

namespace PlayerArrows.Entry
{

    internal class ModEntry : Mod
    {
        public ModConfig Config;
        private LogLevel ProgramLogLevel = LogLevel.Trace; // By default trace logs. but in debug mode: debug logs
        private bool EventHandlersAttached = false;        // This stops split screen player reattaching event handlers whilly nilly


        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            // Read config
            Config = this.Helper.ReadConfig<ModConfig>();
            ProgramLogLevel = Config.Debug ? LogLevel.Debug : LogLevel.Trace;

            // Attach event handlers to SMAPI's
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;     // Connect to GMCM
            this.Helper.Events.GameLoop.SaveLoaded += OnLoadGame;           // Attach Handlers on save load

            this.Monitor.Log($"Loading mod config, and establishing connection to GMCM", ProgramLogLevel);
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

            // Write the changes to config
            this.Helper.WriteConfig(Config);
        }

        // Attach all of the removeable event handlers to SMAPI's
        private void AttachEventHandlers()
        {
            // Only let host attach and detach event handlers in split screen. This wont matter for lan / internet
            if (Context.IsSplitScreen && !Context.IsMainPlayer)
            {
                return;
            }
            if (EventHandlersAttached)
            {
                this.Monitor.Log($"{Game1.player.Name}: Tried to attach event handlers, but they're already attached", LogLevel.Warn);
                return;
            }
            // Attach handlers
            this.Helper.Events.GameLoop.ReturnedToTitle += OnQuitGame;
            this.Helper.Events.Display.RenderedWorld += OnWorldRender;

            this.Monitor.Log($"{Game1.player.Name}: Attached Event handlers", ProgramLogLevel);
            
            EventHandlersAttached = true;
        }

        // Detach all of the removeable event handlers from SMAPI's
        private void DetachEventHandlers()
        {
            // Only let splitscreen host attach and detach event handlers in split screen. This wont effect lan / internet
            if (Context.IsSplitScreen && !Context.IsMainPlayer)
            {
                return;
            }
            if (!EventHandlersAttached)
            {
                this.Monitor.Log($"{Game1.player.Name}: Tried to detach event handlers, but they're already detached", LogLevel.Warn);
                return;
            }
            // Detach Handlers
            this.Helper.Events.GameLoop.ReturnedToTitle -= OnQuitGame;

            EventHandlersAttached = false;

            this.Monitor.Log($"{Game1.player.Name}: Detached Event handlers", ProgramLogLevel);
        }

        // Detach all handlers when quitting the game
        private void OnQuitGame(object sender, ReturnedToTitleEventArgs e)
        {
            this.Monitor.Log($"{Game1.player.Name}: Has quit", ProgramLogLevel);
            DetachEventHandlers();
        }

        // Attach all handlers when loading into a save game
        private void OnLoadGame(object sender, SaveLoadedEventArgs e)
        {
            if (Config.Enabled)
            {
                
                this.Monitor.Log($"{Game1.player.Name}: Has loaded into world", ProgramLogLevel);
                AttachEventHandlers();

            }
        }

        ///After world rendewr, we will draw our arrows
        private void OnWorldRender(object sender, RenderedWorldEventArgs e)
        {
            string message = "";

            foreach (Farmer farmer in Game1.getOnlineFarmers())
            {
                if (farmer.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID)
                {
                    message += $"{farmer.Name} ";
                }
            }

            Vector2 position = new Vector2(1000, 100);
            Color color1 = Color.White;
            Color color2 = Color.White;

            Game1.drawWithBorder(message, color1, color2, position);
        }
    }
}