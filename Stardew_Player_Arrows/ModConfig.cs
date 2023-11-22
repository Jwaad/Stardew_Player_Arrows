public sealed class ModConfig
{
    public bool Enabled { get; set; } = true;
    public bool Debug { get; set; } = false;
    public int PlayerLocationUpdateFPS { get; set; } = 40;
    public int ArrowOpacity { get; set; } = 70;
}