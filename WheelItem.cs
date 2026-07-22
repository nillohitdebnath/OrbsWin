using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OrbsWin;

public class WheelItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string? IconPath { get; set; }

    [JsonPropertyName("type")]
    public string ItemType { get; set; } = "builtin_tool"; // builtin_tool, app_launch, shell_command, snippet, submenu

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("children")]
    public List<WheelItem> Children { get; set; } = new();

    public WheelItem() { }

    public WheelItem(string name, string itemType = "builtin_tool", string? value = null, string? iconPath = null, List<WheelItem>? children = null)
    {
        Name = name;
        ItemType = itemType;
        Value = value;
        IconPath = iconPath;
        if (children != null)
        {
            Children = children;
        }
    }
}
