public sealed class ModConfig
{
    public bool Enabled { get; set; } = true;
    public bool Debug { get; set; } = false;
    public bool NamesOnArrows { get; set; } = true;
    public int PlayerLocationUpdateFPS { get; set; } = 40;
    public int ArrowOpacity { get; set; } = 70;
    public string ColourPalette { get; set; } = "All"; // Do comments show up?
}