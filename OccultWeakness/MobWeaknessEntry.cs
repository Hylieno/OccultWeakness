using System;
using System.Collections.Generic;

namespace OccultWeakness;

[Serializable]
public sealed class MobWeaknessEntry
{
    public string Name { get; set; } = string.Empty;
    public List<Element> Weaknesses { get; set; } = new();
    public bool Enabled { get; set; } = true;
}
