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
using StardewValley.BellsAndWhistles;

namespace PlayerArrows.Objects
{

    public class PlayerArrow
    {

        // Vars for drawing
        public Vector2 Position { get; set; } = new Vector2(0, 0);
        public Vector2 Origin { get; private set; }
        public Vector2 TextOrigin { get; private set; }
        private readonly Texture2D ArrowBorder;
        private readonly Texture2D ArrowBody;
        private float TextOffset = 0; // Distance text floats above arrow 
        public Texture2D DisplayTextTexture { get; private set; }
        public Color BorderColor { get; set; } = Color.Black;
        public Color BodyColor { get; set; } = Color.DarkRed;
        public float ArrowAngle { get; set; } = 0;
        public float Scale { get; set; } = 1f;
        public float LayerDepth { get; set; } = 0f;
        public bool SameMap = false;        // If this arrow points to someone on same map as player
        public bool TargetOnScreen = false; // Store here so we can track if player is visible
        public float Opacity;
        bool TextInitialised = false;
        private readonly long PlayerID;
        private int RandomSeed;
        Random Randomiser;
        ModConfig Config;

        // Constructor, set initial Position and Angle. Also load textures here
        public PlayerArrow(Vector2 position, double angle, Texture2D arrowBody, Texture2D arrowBorder, long playerID, ModConfig config)
        {
            // Load arrow textures
            ArrowBody = arrowBody;
            ArrowBorder = arrowBorder;

            // Derive seed from player ID, so everyone sees them as one colour
            PlayerID = playerID;
            RandomSeed = int.Parse((playerID.ToString()).Substring(0, 5));
            Randomiser = new Random(RandomSeed);
            BodyColor = GenerateRandomColor(config.ColourPalette);

            // Set the default
            Position = position;
            ArrowAngle = (float)(angle); // Rotate 90 deg;
            Origin = new Vector2(ArrowBody.Width * Scale / 2, ArrowBody.Height * Scale); // Bottom center
            
        }

        // Draw arrow
        public void DrawArrow(RenderedWorldEventArgs e, bool nameOnArrow)
        {

            // Draw arrow
            e.SpriteBatch.Draw(ArrowBody, Position, null, BodyColor * (float)Opacity, ArrowAngle - (float)(Math.PI / 2), Origin, Scale, SpriteEffects.None, LayerDepth);
            e.SpriteBatch.Draw(ArrowBorder, Position, null, BorderColor * (float)Opacity, ArrowAngle - (float)(Math.PI / 2), Origin, Scale, SpriteEffects.None, LayerDepth);

            if (nameOnArrow && TextInitialised)
            {
                // Move pos along the angle, by offset amount + arrow height. Also rotate 90
                float textX = Position.X - ((TextOffset + ArrowBody.Height) * (float)Math.Cos(ArrowAngle));
                float textY = Position.Y - ((TextOffset + ArrowBody.Height) * (float)Math.Sin(ArrowAngle));
                Vector2 textPosition = new Vector2(textX, textY);
                e.SpriteBatch.Draw(DisplayTextTexture, textPosition, null, BodyColor * (float)Opacity, ArrowAngle - (float)(Math.PI / 2), TextOrigin, Scale, SpriteEffects.None, LayerDepth);
            }
        }

        // Generate Random Colour
        private Color GenerateRandomColor(string colourPalette)
        {
            switch (colourPalette)
            {
                case "Pastel":
                {
                    return new Color(
                    Randomiser.Next(120, 256),
                    Randomiser.Next(120, 256), 
                    Randomiser.Next(120, 256), 
                    255 );
                }
                case "Dark":
                {
                    return new Color(
                    Randomiser.Next(0, 150), 
                    Randomiser.Next(0, 150),
                    Randomiser.Next(0, 150),
                    255);
                }
                case "All":
                {
                    return new Color(
                    Randomiser.Next(0, 256), 
                    Randomiser.Next(0, 256),
                    Randomiser.Next(0, 256), 
                    255);
                }
                case "Black":
                    {
                        return Color.Black;
                    }
                default:
                {
                    return new Color(
                    Randomiser.Next(0, 256), // R (0 to 255)
                    Randomiser.Next(0, 256), // G (0 to 255)
                    Randomiser.Next(0, 256), // B (0 to 255) 
                    255);               // A (alpha, fully opaque)
                }
            }
        }

        // Draw some text and save as PNG
        public Texture2D CreateTextPNG(GraphicsDevice graphicsDevice, SpriteFont font, string displayText)
        {
            // Setup render target
            int stringWidth = (int)font.MeasureString(displayText).X;
            int stringHeight = (int)font.MeasureString(displayText).Y;
            RenderTarget2D renderTarget = new RenderTarget2D(graphicsDevice, stringWidth, stringHeight);
            graphicsDevice.SetRenderTarget(renderTarget);
            graphicsDevice.Clear(Color.Transparent); //Color.Transparent

            // Start sprite batch and draw target
            SpriteBatch spriteBatch = new SpriteBatch(graphicsDevice);

            spriteBatch.Begin();
            spriteBatch.DrawString(font, displayText, new Vector2(0, 0), Color.White);
            spriteBatch.End();

            //using (FileStream stream = new FileStream("mods/Stardew_Player_Arrows/assets/test.png", FileMode.Create, FileAccess.Write))
            using (MemoryStream stream = new MemoryStream())
            {

                renderTarget.SaveAsPng(stream, stringWidth, stringHeight);

                stream.Seek(0, SeekOrigin.Begin);
                DisplayTextTexture = Texture2D.FromStream(graphicsDevice, stream);
            }
            graphicsDevice.SetRenderTarget(null);

            TextOrigin = new Vector2(DisplayTextTexture.Width * Scale / 2, DisplayTextTexture.Height * Scale); // bottom middle

            TextInitialised = true;

            return DisplayTextTexture;
        }
    }
}