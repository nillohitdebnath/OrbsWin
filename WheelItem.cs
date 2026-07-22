using System.Collections.Generic;

namespace OrbsWin;

public class WheelItem
{
    public string Name { get; set; } = string.Empty;
    public string? IconPath { get; set; }
    public List<WheelItem> Children { get; set; } = new();

    public WheelItem(string name, string? iconPath = null, List<WheelItem>? children = null)
    {
        Name = name;
        IconPath = iconPath;
        if (children != null)
        {
            Children = children;
        }
    }
}
