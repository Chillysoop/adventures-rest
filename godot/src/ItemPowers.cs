using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Client;

/// <summary>
/// Magic items' powers as the client shows them: a small icon a power after the name, and the powers one a line in
/// the hover. Who can use it (a class lock, casters only) goes in the hover too.
/// </summary>
public partial class Main
{
    /// <summary>The power icons of an item, inline in rich text ("" for a plain piece).</summary>
    private static string PowerIcons(Content c, LibItem it, int size = 13) =>
        string.Concat(Enchant.Of(c, it).Select(p => Enchant.Rules(c).Kind(p.Kind)?.Icon).Where(i => i is { Length: > 0 }).Distinct().Select(i => Ui.Img(i!, size)));

    /// <summary>The item's powers one a line, and who can use it, for a hover.</summary>
    private static string PowerTip(Content c, LibItem it)
    {
        var lines = Enchant.Of(c, it).Select(p => $"• {Enchant.Rules(c).Kind(p.Kind)?.Name ?? p.Kind}: {Enchant.Describe(c, p)}").Distinct().ToList();
        if (Enchant.Classes(c, it) is { Count: > 0 } cls) lines.Add($"Only {string.Join(", ", cls.Select(x => c.Classes.FirstOrDefault(k => k.Id == x)?.Name ?? x))}");
        else if (Enchant.Of(c, it).Any(p => p.Caster)) lines.Add("Only spellcasters");
        if (Enchant.Basic(c, it)) lines.Add("Plain arms: the watch and soldiers can carry it");
        return string.Join("\n", lines);
    }

    /// <summary>A piece's name with its power icons and its powers on hover.</summary>
    private static string Powered(Content c, LibItem it, string name) =>
        PowerTip(c, it) is var tip && tip != "" ? Ui.Tip($"{name} {PowerIcons(c, it)}", tip) : name;
}
