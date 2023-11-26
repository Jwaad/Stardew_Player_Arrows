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
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Monsters;
using System.Reflection.Metadata;
using PlayerArrows.Objects;
using xTile.Dimensions;
using Vector4 = Microsoft.Xna.Framework.Vector4;
using StardewValley.Network;
using System.Drawing;
using System.IO;
using xTile.Format;
using Color = Microsoft.Xna.Framework.Color;
using System.Drawing.Imaging;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;

namespace PlayerArrows.Entry
{

    internal class ModEntry : Mod
    {
        public ModConfig Config;
        private LogLevel ProgramLogLevel = LogLevel.Trace; // By default trace logs. but in debug mode: debug logs
        private bool EventHandlersAttached = false;        
        public Dictionary<long, Dictionary<long, PlayerArrow>> PlayersArrowsDict = new(); // This players list of arrows per player
        private Texture2D ArrowBody;
        private Texture2D ArrowBorder;

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            // Load textures once
            ArrowBody = helper.ModContent.Load<Texture2D>("assets/PlayerArrowColour.png");
            ArrowBorder = helper.ModContent.Load<Texture2D>("assets/PlayerArrowBorder.png");

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
                tooltip: () => "Enables verbose logging.",
                getValue: () => Config.Debug,
                setValue: value => HandleFieldChange("Debug", value),
                fieldId: "Debug"
            );

            // add debug to config
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "NamesOnArrows",
                tooltip: () => "Enable if you want name of target that's being tracked to show above arrow",
                getValue: () => Config.NamesOnArrows,
                setValue: value => HandleFieldChange("NamesOnArrows", value),
                fieldId: "NamesOnArrows"
            );

            // Add option to change how smoothness - performance ratio
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "RenderFPS",
                tooltip: () => "Default -> 40. How many times per second the player arrows" +
                " should be updated.\n" +
                "Lowering this number can increase performance, but will cause arrows to move less smoothly",
                getValue: () => Config.PlayerLocationUpdateFPS,
                setValue: value => HandleFieldChange("RenderFPS", value),
                interval: 1,
                min: 1,
                max: 60,
                fieldId: "RenderFPS"
            );

            // Add option to edit opacity
            configMenu.AddNumberOption(
                mod: this.ModManifest,
                name: () => "ArrowOpacity",
                tooltip: () => "How much opacity arrows should have. 100 -> 100% opaque",
                getValue: () => Config.ArrowOpacity,
                setValue: value => HandleFieldChange("ArrowOpacity", value),
                interval: 1,
                min: 1,
                max: 100,
                fieldId: "ArrowOpacity"
            );

            // Add option to select between some limited colour palettes
            configMenu.AddTextOption(
               mod: this.ModManifest,
               name: () => "ColourPalette",
               tooltip: () => "Select between some limited randomised colour palettes. \n" +
               "Pastel -> RBG values of 120 - 255 \n" +
               "Dark -> RBG values of 0 - 120 \n" +
               "Black -> All arrows will be 0,0,0 \n" +
               "All (Default) -> 0 - 255 \n " +
               "(NOTE: Changes will take effect one restart / world load)",
               getValue: () => Config.ColourPalette,
               setValue: value => HandleFieldChange("ColourPalette", value),
               allowedValues: new string[] { "Pastel", "Dark", "All" },
               fieldId: "ColourPalette"
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
            // Handle config option "Debug"
            else if (fieldId == "NamesOnArrows")
            {
                // If the value didnt change, skip
                if (Config.NamesOnArrows == (bool)newValue)
                {
                    return;
                }
                this.Monitor.Log($"{Game1.player.Name}: {fieldId} : Changed from {Config.Debug} To {newValue}", ProgramLogLevel);
                Config.NamesOnArrows = (bool)newValue;
            }
            // Handle config option "RenderFPS"
            else if (fieldId == "RenderFPS")
            {
                this.Monitor.Log($"{Game1.player.Name}: {fieldId} : Changed from {Config.PlayerLocationUpdateFPS} To {newValue}", ProgramLogLevel);
                Config.PlayerLocationUpdateFPS = (int)newValue;
            }
            // Handle config option "Opacity"
            else if (fieldId == "ArrowOpacity")
            {
                this.Monitor.Log($"{Game1.player.Name}: {fieldId} : Changed from {Config.ArrowOpacity} To {newValue}", ProgramLogLevel);
                Config.ArrowOpacity = (int)newValue;
            }
            else if (fieldId == "ColourPalette")
            {
                this.Monitor.Log($"{Game1.player.Name}: {fieldId} : Changed from {Config.ColourPalette} To {newValue}", ProgramLogLevel);
                Config.ColourPalette = (string)newValue;
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
            // Populate dictionaries for storage of farmer locations for this player
            PlayersArrowsDict.Add(Game1.player.UniqueMultiplayerID, new Dictionary<long, PlayerArrow>());

            // Only let host attach and detach event handlers in split screen. This wont matter for lan / internet
            if (Context.IsSplitScreen && !Context.IsMainPlayer)
            {
                return; // This stops split screen player reattaching event handlers whilly nilly
            }
            if (EventHandlersAttached)
            {
                this.Monitor.Log($"{Game1.player.Name}: Tried to attach event handlers, but they're already attached", LogLevel.Warn);
                return;
            }
            // Attach handlers
            this.Helper.Events.GameLoop.ReturnedToTitle += OnQuitGame;
            this.Helper.Events.Display.RenderedWorld += OnWorldRender;
            this.Helper.Events.GameLoop.UpdateTicked += OnUpdateLoop;
            this.Helper.Events.Multiplayer.PeerDisconnected += OnPeerDisconnect;

            EventHandlersAttached = true;

            this.Monitor.Log($"{Game1.player.Name}: Attached Event handlers", ProgramLogLevel);
        }

        // Detach all of the removeable event handlers from SMAPI's
        private void DetachEventHandlers()
        {
            // Remove player from dictionaries
            PlayersArrowsDict.Remove(Game1.player.UniqueMultiplayerID);

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
            this.Helper.Events.Display.RenderedWorld -= OnWorldRender;
            this.Helper.Events.GameLoop.UpdateTicked -= OnUpdateLoop;
            this.Helper.Events.Multiplayer.PeerDisconnected -= OnPeerDisconnect;

            EventHandlersAttached = false;

            this.Monitor.Log($"{Game1.player.Name}: Detached Event handlers", ProgramLogLevel);
        }


        // Detach all handlers when quitting the game
        private void OnQuitGame(object sender, ReturnedToTitleEventArgs e)
        {
            this.Monitor.Log($"{Game1.player.Name}: Has quit", ProgramLogLevel);
            //PlayersSameMap.Add(Game1.player.UniqueMultiplayerID, new List<PlayerArrow>());
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

        private void OnUpdateLoop(object sender, UpdateTickedEventArgs e)
        {
            // Limit updates to FPS specified in config
            if (!e.IsMultipleOf((uint)(60 / Config.PlayerLocationUpdateFPS)))
            {
                return;
            }

            // Check which maps each players are in
            foreach (Farmer farmer in Game1.getOnlineFarmers())
            {
                Vector2 arrowTarget;

                // Sort based on same or diff maps
                if (farmer.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID)
                {
                    //Fixes player trying to add the newly joined player before they're fully connected. 
                    if (!PlayersArrowsDict.ContainsKey(Game1.player.UniqueMultiplayerID))
                    {
                        continue;
                    }

                    // If player isn't already in our dict, add them
                    if (!PlayersArrowsDict[Game1.player.UniqueMultiplayerID].ContainsKey(farmer.UniqueMultiplayerID))
                    {
                        // Create an instance for them, we will set the position this loop, so just use defaults
                        PlayerArrow playerArrow = new(new(0, 0), 0f, ArrowBody, ArrowBorder, farmer.UniqueMultiplayerID, Config);
                        playerArrow.CreateTextPNG(Game1.graphics.GraphicsDevice, Game1.smallFont, farmer.Name); // Init display text
                        this.Monitor.Log($"{Game1.player.Name}: Created new text object for : {farmer.Name}", ProgramLogLevel);

                        PlayersArrowsDict[Game1.player.UniqueMultiplayerID].Add(farmer.UniqueMultiplayerID, playerArrow);

                        this.Monitor.Log($"{Game1.player.Name}: Instanced new player arrow, target: {farmer.Name}", ProgramLogLevel);
                    }

                    // Sort players between same or different map
                    if (farmer.currentLocation == Game1.player.currentLocation)
                    {
                        PlayersArrowsDict[Game1.player.UniqueMultiplayerID][farmer.UniqueMultiplayerID].SameMap = true;
                        arrowTarget = farmer.position.Get() ;// TODO Location to point to in same map
                    }
                    else
                    {
                        PlayersArrowsDict[Game1.player.UniqueMultiplayerID][farmer.UniqueMultiplayerID].SameMap = false;
                        arrowTarget = new Vector2(0,0); // TODO Location to point to when different map
                    }

                    // Compute angle difference
                    double angle = Math.Atan2((arrowTarget.Y - (Game1.player.position.Get().Y + 25)), (arrowTarget.X - Game1.player.position.Get().X));

                    // Set pos of arrow
                    int arrowX = (int)((Game1.viewport.Width / 2 * Math.Cos(angle) * 0.9) + (Game1.viewport.Width / 2));
                    int arrowY = (int)((Game1.viewport.Height / 2 * Math.Sin(angle) * 0.9) + (Game1.viewport.Height / 2));
                    Vector2 arrowPosition = new((int)(arrowX), (int)(arrowY));

                    // Check if player is visible in screen
                    Microsoft.Xna.Framework.Rectangle player_box = farmer.GetBoundingBox();
                    xTile.Dimensions.Rectangle playerRect = new(player_box.X, player_box.Y, player_box.Width, player_box.Height);
                    bool targetOnScreen = Game1.viewport.Intersects(playerRect);

                    // Update player arrows
                    PlayersArrowsDict[Game1.player.UniqueMultiplayerID][farmer.UniqueMultiplayerID].TargetOnScreen = targetOnScreen;
                    PlayersArrowsDict[Game1.player.UniqueMultiplayerID][farmer.UniqueMultiplayerID].Position = arrowPosition;
                    PlayersArrowsDict[Game1.player.UniqueMultiplayerID][farmer.UniqueMultiplayerID].ArrowAngle = (float)angle;
                }
            }
        }

        // On peer discconect, remove them from arrow list
        private void OnPeerDisconnect(object sender, PeerDisconnectedEventArgs e)
        {
            // Sometimes p1 world render happens before other players have loaded.
            if (!PlayersArrowsDict.ContainsKey(Game1.player.UniqueMultiplayerID))
            {
                return;
            }

            if (PlayersArrowsDict[Game1.player.UniqueMultiplayerID].ContainsKey(e.Peer.PlayerID))
            {
                PlayersArrowsDict[Game1.player.UniqueMultiplayerID].Remove(e.Peer.PlayerID);
                this.Monitor.Log($"{Game1.player.Name}: Removed player arrow, target: {e.Peer.PlayerID}", ProgramLogLevel);
            }
        }

        //After world render, we will draw our arrows
        private void OnWorldRender(object sender, RenderedWorldEventArgs e)
        {
            // Draw to UI not to world
            Game1.InUIMode(() =>
            {
                // Sometimes p1 world render happens before other players have loaded.
                if (!PlayersArrowsDict.ContainsKey(Game1.player.UniqueMultiplayerID))
                {
                    return;
                }

                if (PlayersArrowsDict[Game1.player.UniqueMultiplayerID].Count > 0)
                {
                    // Draw the stored arrows
                    foreach (PlayerArrow arrow in PlayersArrowsDict[Game1.player.UniqueMultiplayerID].Values)
                    {
                        // Dont draw arrow if you can see the target
                        if (arrow.TargetOnScreen)
                        {
                            continue;
                        }
                        // TEMPORARILY DONT DRAW DIFF MAP ARROWS. UNTIL WE MAKE THE METHODS TO TRACK MAP LOCATIONS
                        if (!arrow.SameMap) // TEMP
                        {
                            continue; // TEMP
                        }
                        arrow.Opacity = (float)(this.Config.ArrowOpacity) / 100; // update arrow opacity, incase it changed
                        arrow.DrawArrow(e, this.Config.NamesOnArrows);
                    }
                }
            });
        }
    }
}
