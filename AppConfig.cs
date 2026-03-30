using System.Collections.Generic;

namespace Photobooth;

public class PhotoSlotConfig
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class TemplateConfig
{
    public string Path { get; set; } = string.Empty;
    public List<PhotoSlotConfig> Slots { get; set; } = new();
}

public class TemplatesConfig
{
    public TemplateConfig Simple { get; set; } = new();
    public TemplateConfig Grid { get; set; } = new();
}

public class AppConfigRoot
{
    public TemplatesConfig Templates { get; set; } = new();
}