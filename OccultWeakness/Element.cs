using System.Collections.Generic;

namespace OccultWeakness;

public enum Element
{
    Fire,
    Ice,
    Wind,
    // Conservés uniquement pour pouvoir relire les anciens fichiers de configuration.
    Earth,
    Lightning,
    Water
}

public static class ElementExtensions
{
    public static IReadOnlyList<Element> SupportedElements { get; } = new[]
    {
        Element.Fire,
        Element.Ice,
        Element.Wind,
        Element.Lightning
    };

    public static bool IsSupported(this Element element) =>
        element is Element.Fire or Element.Ice or Element.Wind or Element.Lightning;

    public static string DisplayName(this Element element) => element switch
    {
        Element.Fire => "Feu",
        Element.Ice => "Glace",
        Element.Wind => "Vent",
        Element.Lightning => "Foudre",
        _ => element.ToString()
    };

    public static string ShortName(this Element element) => element switch
    {
        Element.Fire => "F",
        Element.Ice => "G",
        Element.Wind => "V",
        Element.Lightning => "É",
        _ => "?"
    };
}
