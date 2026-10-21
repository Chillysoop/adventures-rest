namespace CrownAndCompass.Sim.Systems;

/// <summary>A setting an intro's picture is staged in: its sky, ground and light, and what stands about.</summary>
public sealed record IntroScene
{
    public string Id { get; init; } = "";

    /// <summary>The sky at the top of the picture and at the horizon, and the haze between.</summary>
    public string Sky { get; init; } = "2a3448";
    public string Horizon { get; init; } = "8a7a68";
    public string Ground { get; init; } = "4a5a32";
    public string Ground2 { get; init; } = "3a4428";

    /// <summary>The sun's colour and the light from behind that rims the figures.</summary>
    public string Light { get; init; } = "ffe0b8";
    public string Rim { get; init; } = "ff9a48";

    /// <summary>What drifts in the air: "embers", "snow", "spores", "mist", "ash" or "".</summary>
    public string Air { get; init; } = "";

    /// <summary>Baked models (Models.Get names) set about the figures, by lot from the faction's id.</summary>
    public IReadOnlyList<IntroProp> Props { get; init; } = [];
}

/// <summary>Some of one model scattered in a band round the figures: n of them, from near to far (in figure heights).</summary>
public sealed record IntroProp(string Model, int Count, double Near, double Far, double Scale = 1, string Tint = "");

/// <summary>
/// One faction's introduction (intros.json): the picture shown the first time the town meets anyone of its lineage,
/// and its crest. Keyed by the lineage in history.json, so every menace, people, power and enemy group that descends
/// from it shares it.
/// </summary>
public sealed record IntroDef
{
    /// <summary>The lineage (history.json lineages) this introduces.</summary>
    public string Lineage { get; init; } = "";

    /// <summary>The game-icons.net glyph on its crest (scripts/icons.ts makes it as crest-&lt;lineage&gt;).</summary>
    public string Crest { get; init; } = "";

    /// <summary>Its colours: the field and the charge, as on a banner.</summary>
    public string Field { get; init; } = "5a2a20";
    public string Charge { get; init; } = "d9a93a";

    public string Scene { get; init; } = "";

    /// <summary>Who stands in the picture: a host look ("tribe", "undead", "cult", "rivals", "schemers", "beasts", "dragon", "folk"), and how many.</summary>
    public string Host { get; init; } = "tribe";
    public int Count { get; init; } = 4;

    /// <summary>How big the chief stands against the rest: a giant is 1.8, a queen of spiders 1.6.</summary>
    public double Chief { get; init; } = 1.25;

    /// <summary>Skin and cloth colours for the host.</summary>
    public string Skin { get; init; } = "";
    public string Cloth { get; init; } = "";

    /// <summary>The line under its name: what they are, in a breath.</summary>
    public string Line { get; init; } = "";
}

public sealed record IntroRules
{
    public IReadOnlyList<IntroScene> Scenes { get; init; } = [];
    public IReadOnlyList<IntroDef> Intros { get; init; } = [];
}

/// <summary>
/// First meetings told as a picture: when the town first meets someone (<see cref="Discovery.Meet"/>), the client
/// shows the intro of the lineage they descend from, once a campaign, with a little of its history. This only reads:
/// it rolls nothing and keeps nothing, so which intros are shown is worked out from <see cref="Town.Discovered"/>.
/// </summary>
public static class Intros
{
    public static IntroDef? Def(Town town, string lineage) => town.Content.Intros.Intros.FirstOrDefault(i => i.Lineage == lineage);

    public static IntroScene? Scene(Town town, IntroDef d) => town.Content.Intros.Scenes.FirstOrDefault(s => s.Id == d.Scene);

    /// <summary>The lineage behind a meeting's key: a menace's own, a power's or a people's from history.json.</summary>
    public static string? LineageOf(Town town, string key)
    {
        var h = town.Content.History;
        if (key == Discovery.Warband)
            return town.Saga.Menaces.FirstOrDefault(m => m.Origin == Menaces.FromWarband) is { Lineage: not "" } w ? w.Lineage
                : Annals.LineageOf(h, "menace:tribe")?.Id;
        int colon = key.IndexOf(':');
        if (colon < 0) return null;
        string kind = key[..colon], rest = key[(colon + 1)..];
        switch (kind)
        {
            case "menace":
                if (!int.TryParse(rest, out int id) || town.Saga.Menaces.FirstOrDefault(m => m.Id == id) is not { } m) return null;
                return m.Lineage != "" ? m.Lineage : Annals.LineageOf(h, "menace:" + m.Kind)?.Id;
            case "power":
                var p = town.Content.Powers.Powers.FirstOrDefault(x => x.Id == rest);
                return p == null ? null : Annals.LineageOf(h, "faction:" + p.Ref)?.Id;
            case "people":
                if (!int.TryParse(rest, out int sid) || town.Settlements.FirstOrDefault(s => s.Id == sid) is not { } s) return null;
                return Annals.LineageOf(h, "people:" + s.Kin.Id)?.Id;
        }
        return null;
    }

    /// <summary>
    /// The intros due for the town's meetings from the index-th on, in the order met: one per lineage, and only for a
    /// lineage none of the meetings before had.
    /// </summary>
    public static List<(Meeting Meeting, IntroDef Intro)> Due(Town town, int from)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var due = new List<(Meeting, IntroDef)>();
        for (int i = 0; i < town.Discovered.Count; i++)
        {
            var c = town.Discovered[i];
            if (LineageOf(town, c.Key) is not { } l || !seen.Add(l)) continue;
            if (i >= from && Def(town, l) is { } d) due.Add((c, d));
        }
        return due;
    }

    /// <summary>What the history says of the power a lineage descends from: its name, its age and its tale.</summary>
    public static HistoryPower? Ancestor(Town town, string lineage) =>
        Annals.Lineage(town.Content.History, lineage) is { Power: not "" } l ? town.Content.History.Powers.FirstOrDefault(p => p.Id == l.Power) : null;
}
