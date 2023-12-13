﻿using GenericModConfigMenu;
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
using StardewValley.TerrainFeatures;
using System.Linq;
using System.Reflection;

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
        public Dictionary<string, List<string>> MapWarpLocations = new();

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

            // add debug to config
            configMenu.AddBoolOption(
                mod: this.ModManifest,
                name: () => "DrawBorders",
                tooltip: () => "Disable to remove border around arrow labels",
                getValue: () => Config.DrawBorders,
                setValue: value => HandleFieldChange("DrawBorders", value),
                fieldId: "DrawBorders"
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
               allowedValues: new string[] { "Pastel", "Dark", "Black", "All" },
               fieldId: "ColourPalette"
           );
        }


        // Handle what to do on change of each config field
        public void HandleFieldChange(string fieldId, object newValue)
        {
            switch (fieldId) 
            {
                // Handle config option "Enabled"
                case "Enabled":
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
                    break;
                    }
                // Handle config option "Debug"
                case "Debug":
                {
                    // If the value didnt change, skip
                    if (Config.Debug == (bool)newValue)
                    {
                        return;
                    }
                    this.Monitor.Log($"{Game1.player.Name}: {fieldId} : Changed from {Config.Debug} To {newValue}", ProgramLogLevel);
                    Config.Debug = (bool)newValue;
                    ProgramLogLevel = Config.Debug ? LogLevel.Debug : LogLevel.Trace;
                    break;
                }
                // Handle config NamesOnArrows
                case "NamesOnArrows":
                {
                // If the value didnt change, skip
                    if (Config.NamesOnArrows == (bool)newValue)
                    {
                         return;
                    }
                    this.Monitor.Log($"{Game1.player.Name}: {fieldId} : Changed from {Config.Debug} To {newValue}", ProgramLogLevel);
                    Config.NamesOnArrows = (bool)newValue;
                    break;
                }
                // Handle config option "Draw borders"
                case "DrawBorders":
                {
                        // If the value didnt change, skip
                        if (Config.DrawBorders == (bool)newValue)
                        {
                            return;
                        }
                        this.Monitor.Log($"{Game1.player.Name}: {fieldId} : Changed from {Config.Debug} To {newValue}", ProgramLogLevel);
                        Config.DrawBorders = (bool)newValue;
                        break;
                 }
                // Handle config option "RenderFPS"
                case "RenderFPS":
                {
                    this.Monitor.Log($"{Game1.player.Name}: {fieldId} : Changed from {Config.PlayerLocationUpdateFPS} To {newValue}", ProgramLogLevel);
                    Config.PlayerLocationUpdateFPS = (int)newValue;
                    break;
                }
                // Handle config option "Opacity"
                case "ArrowOpacity":
                {
                    this.Monitor.Log($"{Game1.player.Name}: {fieldId} : Changed from {Config.ArrowOpacity} To {newValue}", ProgramLogLevel);
                    Config.ArrowOpacity = (int)newValue;
                    break;
                }
                // Handle config option "ColourPalette"
                case "ColourPalette":
                {
                    this.Monitor.Log($"{Game1.player.Name}: {fieldId} : Changed from {Config.ColourPalette} To {newValue}", ProgramLogLevel);
                    Config.ColourPalette = (string)newValue;
                    break;
                }
                // Should never occur, but in case i forgot one option
                default:
                {
                    this.Monitor.Log($"{Game1.player.Name}: Unhandled config option", ProgramLogLevel);
                    break;
                }
            }
            // Write the changes to config
            this.Helper.WriteConfig(Config);
            this.Monitor.Log($"{Game1.player.Name}: Changes saved to Mod config file", ProgramLogLevel);
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
            this.Helper.Events.Player.Warped += OnPlayerWarp;

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
                MapWarpLocations = GenerateMapConnections();

                this.Monitor.Log($"{Game1.player.Name}: computed warp locations", ProgramLogLevel);
                AttachEventHandlers();
/*
                // TEMPORARY FOR TESTING PURPOSES
                PlayerArrow testArrow = new(new(500, 500), 1f, ArrowBody, ArrowBorder, Game1.player.UniqueMultiplayerID, Config);
                testArrow.SameMap = true;
                testArrow.CreateTextPNG(Game1.graphics.GraphicsDevice, Game1.smallFont, "THIS IS A TEST ARROW"); // Init display text

                PlayerArrow testArrow2 = new(new(1000, 100), 2f, ArrowBody, ArrowBorder, Game1.player.UniqueMultiplayerID, Config);
                testArrow2.SameMap = true;
                testArrow2.CreateTextPNG(Game1.graphics.GraphicsDevice, Game1.smallFont, "TestArrow2"); // Init display text

                PlayersArrowsDict[Game1.player.UniqueMultiplayerID][Game1.player.UniqueMultiplayerID] = testArrow;
                PlayersArrowsDict[Game1.player.UniqueMultiplayerID][Game1.player.UniqueMultiplayerID * 2] = testArrow2;
*/
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

                // Skip self
                if (farmer.UniqueMultiplayerID != Game1.player.UniqueMultiplayerID)
                {
                    // Fixes player trying to add players before the game has fully loaded. 
                    if (!PlayersArrowsDict.ContainsKey(Game1.player.UniqueMultiplayerID))
                    {
                        continue;
                    }

                    // Fixes program trying to add players who havn't loaded and given a location themselves yet.
                    if (farmer.currentLocation == null)
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
                        arrowTarget = farmer.position.Get() ;
                        PlayersArrowsDict[Game1.player.UniqueMultiplayerID][farmer.UniqueMultiplayerID].TargetPos = arrowTarget;
                        PlayersArrowsDict[Game1.player.UniqueMultiplayerID][farmer.UniqueMultiplayerID].PlayerCurrentMap = farmer.currentLocation.NameOrUniqueName;
                    }
                    else
                    {
                        // If farmer just left the screen this player is on, or just loaded in, then calculate a path to them
                        // or When a farmer just spawns in, and is in a different map to our player
                        if (PlayersArrowsDict[Game1.player.UniqueMultiplayerID][farmer.UniqueMultiplayerID].SameMap ||
                            PlayersArrowsDict[Game1.player.UniqueMultiplayerID][farmer.UniqueMultiplayerID].Position == new Vector2(0, 0))
                        {
                            // Update farmer path tracking once farmer leaves the map player is on
                            PlayersArrowsDict[Game1.player.UniqueMultiplayerID][farmer.UniqueMultiplayerID].TargetPos = FindTeleporterTile(farmer.currentLocation.NameOrUniqueName);
                        }
                        // Dont calculate this every loop. Instead it should have already been calculated in warping event
                        arrowTarget = PlayersArrowsDict[Game1.player.UniqueMultiplayerID][farmer.UniqueMultiplayerID].TargetPos;

                        PlayersArrowsDict[Game1.player.UniqueMultiplayerID][farmer.UniqueMultiplayerID].SameMap = false;
                        PlayersArrowsDict[Game1.player.UniqueMultiplayerID][farmer.UniqueMultiplayerID].PlayerCurrentMap = farmer.currentLocation.NameOrUniqueName;
                    }

                    // Compute angle difference
                    double angle = Math.Atan2((arrowTarget.Y - (Game1.player.position.Get().Y)), (arrowTarget.X - Game1.player.position.Get().X));

                    // Set pos of arrow
                    int arrowX = (int)((Game1.viewport.Width / 2 * Math.Cos(angle) * 0.9) + (Game1.viewport.Width / 2));
                    int arrowY = (int)((Game1.viewport.Height / 2 * Math.Sin(angle) * 0.9) + (Game1.viewport.Height / 2));
                    Vector2 arrowPosition = new((int)(arrowX), (int)(arrowY));

                    // Check if player is visible on screen // TODO ADAPT THIS FOR TILE TARGETS
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


        // Using the name of target map and current player map, figure out which tile to point to from current map
        private Vector2 FindTeleporterTile(string targetLocation)
        {
            List<string> currentPath = new List<string> { Game1.player.currentLocation.NameOrUniqueName };
            List<List<string>> completedPaths = new List<List<string>>();

            // Recurse through the map directions and find target map
            completedPaths = FindTargetMap(targetLocation, currentPath, completedPaths);
            Monitor.Log($"{Game1.player.Name}: Found {completedPaths.Count} possible routes from {Game1.player.currentLocation.NameOrUniqueName} to {targetLocation}", ProgramLogLevel);

            if (completedPaths.Count > 0)
            {
                // Get path that crosses through least locations
                List<string> shortestPath = completedPaths.OrderBy(item => item.Count).First();
                string currentLocationName = shortestPath[0];
                string mapTargetName = shortestPath[1];

                // Find a warp in the next location in our path, to point to
                IList<GameLocation> allLocations = Game1.locations;
                GameLocation mapTarget = allLocations.FirstOrDefault(item => item.NameOrUniqueName == currentLocationName);
                
                // Check warps and see if tile is a warp
                foreach (Warp warp in mapTarget.warps)
                {
                    // Get tile based on where to warp to next
                    if (warp.TargetName == mapTargetName)
                    {
                        // Use the first warp that connects to our target map, that we find.
                        Vector2 tileTarget = new Vector2(warp.X + 0.5f, warp.Y + 0.5f) * Game1.tileSize;
                        return tileTarget;
                    }
                }

                // If tile isnt a warp, check doors
                foreach (KeyValuePair<Microsoft.Xna.Framework.Point, string> door in mapTarget.doors.Pairs)
                {
                    Warp warpPoint = new();

                    try
                    {
                        warpPoint = mapTarget.getWarpFromDoor(door.Key);
                    }
                    catch
                    {
                        continue;
                    }

                    // Get tile based on where to warp to next
                    if (warpPoint.TargetName == mapTargetName)
                    {
                        // Use the first warp that connects to our target map, that we find.
                        Vector2 tileTarget = new Vector2(warpPoint.X + 0.5f, warpPoint.Y + 0.5f) * Game1.tileSize;
                        return tileTarget;
                    }
                }
            }

            // If our algorithm coundn't find a route somehow (likley due to mods) dont update tracking pos
            return new Vector2();
        }

        // Find all possible routes to target location, by recusively running through all options
        private List<List<string>> FindTargetMap(string targetLocation, List<string> journey, List<List<string>> completedPaths)
        {
            List<string> currentPath = new List<string>(journey);
            string currentMap = currentPath[^1];

            // If we encounter a map we dont know, try updating our map database, 
            if (!MapWarpLocations.ContainsKey(currentMap))
            {
                Monitor.Log("Map not founded in database. Attempting to rebuild database...", ProgramLogLevel);
                MapWarpLocations = GenerateMapConnections();

                // if it still doesnt work.just skip
                if (!MapWarpLocations.ContainsKey(currentMap))
                {
                    Monitor.Log("Map was no found in rebuild. Skipping...", ProgramLogLevel);
                    return completedPaths;
                }
            }

            List<string> possibleMaps = MapWarpLocations[currentMap];
            foreach (string nextLocation in possibleMaps)
            {
                // We have found the location, but continue through all possiblities
                if (nextLocation == targetLocation)
                {
                    // Add copy so next loops arent effected
                    List<string> currentPathCopy = new List<string>(currentPath) { nextLocation };
                    completedPaths.Add(currentPathCopy);
                }
                // Dont recurse into any of the maps we just came through, (unless it's the target location)
                else if (currentPath.Contains(nextLocation))
                { 
                    continue; 
                }
                // location was not the target, so go into it's possible routes
                else
                {
                    // Add copy so next loops arent effected
                    List<string> currentPathCopy = new List<string>(currentPath){nextLocation};
                    completedPaths = FindTargetMap(targetLocation, currentPathCopy, completedPaths);
                }
            }

            // Hitting here means we've hit a dead end. So end.
            return completedPaths;
        }


        // After player warps somewhere, update their tracking arrows with new positions of all parties.
        private void OnPlayerWarp(object sender, WarpedEventArgs e)
        {
            // Check which maps each players are in
            foreach (PlayerArrow arrow in PlayersArrowsDict[Game1.player.UniqueMultiplayerID].Values)
            {
                // Skip farmers on same map
                Farmer farmer = Game1.getFarmer(arrow.PlayerID);
                if (farmer.currentLocation.NameOrUniqueName == Game1.player.currentLocation.NameOrUniqueName)
                {
                    continue;
                }

                // Update pos of target tile, per existing tracking arrow
                Vector2 trackTarget = FindTeleporterTile(farmer.currentLocation.NameOrUniqueName);

                // Only overwrite target pos if track target didnt also default
                arrow.TargetPos = trackTarget != new Vector2() ? trackTarget: arrow.TargetPos;
            }
        }

        // On peer discocnect, remove them from arrow list
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

        // After world render, we will draw our arrows
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

                        arrow.Opacity = (float)(this.Config.ArrowOpacity) / 100; // update arrow opacity, incase it changed
                        arrow.DrawArrow(e, this.Config.NamesOnArrows);
                    }
                }
            });
        }

        // Recursively loop through all map locations and create database for each map and its teleporters / neighbours
        private Dictionary<string, List<string>> GenerateMapConnections()
        {
            Dictionary<string, List<string>> MapNeighbours = new();

            // Get all game locations
            IList<GameLocation> allLocations = Game1.locations;

            Monitor.Log($"Updating our list of locations and warps", ProgramLogLevel);

            // Iterate through all maps
            foreach (GameLocation location in allLocations)
            {
                string locationName = location.NameOrUniqueName;
                List<string> warpTargets = new();
                string allWarps = "";

                // Loop through all warp points in the map
                foreach (Warp warpPoint in location.warps)
                {
                    // Get unique names amongst warp locations as a shortcut
                    string warpTargetName = warpPoint.TargetName;
                    if (!warpTargets.Contains(warpTargetName))
                    {
                        warpTargets.Add(warpTargetName);
                        allWarps += warpTargetName + " ";
                    }
                }

                // Loop through all buildings in the map
                foreach (KeyValuePair<Microsoft.Xna.Framework.Point, string> door in location.doors.Pairs)
                {
                    Warp warpPoint = new();

                    try
                    {
                        warpPoint = location.getWarpFromDoor(door.Key);
                    }
                    catch
                    {
                        Monitor.Log($"Door name: {door.Value}, in location: {location.NameOrUniqueName} failed", ProgramLogLevel);
                        continue;
                    }

                    // Get unique names amongst warp locations as a shortcut
                    string warpTargetName = warpPoint.TargetName;
                    if (!warpTargets.Contains(warpTargetName))
                    {
                        warpTargets.Add(warpTargetName);
                        allWarps += warpTargetName + " ";
                    }

                }

                // update our dict with location name and warp targets
                Monitor.Log($"Location: {locationName} - Warps: {allWarps}", ProgramLogLevel);
                MapNeighbours.Add(locationName, warpTargets);
            }
            
            return MapNeighbours;
        }
    }
}
