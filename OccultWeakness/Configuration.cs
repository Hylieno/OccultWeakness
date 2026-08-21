using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace OccultWeakness;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 7;
    public bool Enabled { get; set; } = true;
    public bool CaseSensitiveNames { get; set; }

    public float OverlayIconSize { get; set; } = 26f;
    public float OverlayIconSpacing { get; set; } = 2f;
    public float OverlayOffsetX { get; set; } = -330f;
    public float OverlayOffsetY { get; set; } = 90f;
    public bool DrawIconBackground { get; set; } = true;

    public Dictionary<Element, uint> IconIds { get; set; } = new();
    public List<MobWeaknessEntry> Mobs { get; set; } = new();

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
