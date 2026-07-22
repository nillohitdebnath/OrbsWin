namespace OrbsWin;

public class WheelItem
{
    public string Name { get; set; } = string.Empty;
    public string? IconPath { get; set; }

    public WheelItem(string name, string? iconPath = null)
    {
        Name = name;
        IconPath = iconPath;
    }
}
