using GenericModConfigMenu;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using System;
using System.Diagnostics;

namespace PlayerArrows.Entry
{

    internal class ModEntry : Mod
    {
        public ModConfig Config;
        private LogLevel ProgramLogLevel = LogLevel.Trace; // By default trace logs. but in debug mode: debug logs
        private bool EventHandlersAttached = false;        // This stops split screen player reattaching event handlers whilly nilly
        private static Stopwatch ProgramWatch = new Stopwatch();
        public Dictionary<long, long> PreviousArrowRender = new Dictionary<long, long>();


        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            // Read config
            Config = this.Helper.ReadConfig<ModConfig>();
            ProgramLogLevel = Config.Debug ? LogLevel.Debug : LogLevel.Trace;
            ProgramWatch.Start(); // couldn't find smapis on easily (for the render loop), so use my own

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

            // add debug to config
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "RenderFPS",
                tooltip: () => "How many times per second the player arrows should be calculated and drawn. Default = 5",
                getValue: () => Config.RenderFPS,
                setValue: value => HandleFieldChange("RenderFPS", value),
                interval: 1,
                min: 1,
                max: 30,
                fieldId: "RenderFPS"
            );

        }


        // Handle what to do on change of each config field
        public void HandleFieldChange(string fieldId, object newValue)
        {
            // Handle config option "Enabled"
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
            // Handle config option "Debug"
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
            // Handle config option "RenderFPS"
            else if (fieldId == "RenderFPS")
            {
                this.Monitor.Log($"{Game1.player.Name}: {fieldId} : Changed from {Config.RenderFPS} To {newValue}", ProgramLogLevel);
                Config.RenderFPS = (int)newValue;
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


        ///After world render, we will draw our arrows
        private void OnWorldRender(object sender, RenderedWorldEventArgs e)
        {
            // Limit this calculation to save resources
            if ( PreviousArrowRender.ContainsKey(Game1.player.UniqueMultiplayerID) && 
                (ProgramWatch.ElapsedMilliseconds < (PreviousArrowRender[Game1.player.UniqueMultiplayerID] + (1000 / Config.RenderFPS))) )
            {
                return;
            }

            // TEMP
            this.Monitor.Log($"{ProgramWatch.ElapsedMilliseconds}", LogLevel.Debug);
            Vector2 position = new Vector2(1000, 100);
            Color color = Color.White;

            // vars
            List<Farmer> sameMapFarmers = new List<Farmer>();
            List<Farmer> diffMapFarmers = new List<Farmer>();

            // Check which maps each players are in
            foreach (Farmer farmer in Game1.getOnlineFarmers())
            {
                // Sort based on same or diff maps
                if (farmer.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID)
                {
                    if (farmer.currentLocation == Game1.player.currentLocation)
                    {
                        sameMapFarmers.Add(farmer);
                    }
                    else
                    {
                        diffMapFarmers.Add(farmer);
                    }
                }
            }

            // handle arrows for farmers in same place
            string message = "";
            foreach (Farmer farmer in sameMapFarmers)
            {
                message += $"{farmer.Name} : {farmer.getTileLocation()} "; // TEMP TODO
                color = Color.Black;
            }
            Game1.drawWithBorder(message, color, color, position);

            // handle arrows for farmers in diff places
            message = "";
            foreach (Farmer farmer in diffMapFarmers)
            {
                message += $"{farmer.Name} : {farmer.currentLocation} "; // TEMP TODO
                color = Color.Red;
            }
            Game1.drawWithBorder(message, color, color, position);

            // Update timer for next render
            if (PreviousArrowRender.ContainsKey(Game1.player.UniqueMultiplayerID))
            {
                PreviousArrowRender.Remove(Game1.player.UniqueMultiplayerID);
            }
            PreviousArrowRender.Add(Game1.player.UniqueMultiplayerID, ProgramWatch.ElapsedMilliseconds);
            
        }
    }
}