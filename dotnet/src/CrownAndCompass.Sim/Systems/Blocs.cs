namespace CrownAndCompass.Sim.Systems;

/// <summary>One bloc of the town: who speaks for it, who belongs and how much, and the choices it likes and dislikes (content/game/blocs.json).</summary>
public sealed record BlocDef
{
    public string Id { get; init; } = "";

    /// <summary>"the stallholders": who speaks for the bloc.</summary>
    public string Voice { get; init; } = "";
    public string Icon { get; init; } = "";
    public List<string> Likes { get; init; } = [];
    public List<string> Dislikes { get; init; } = [];

    /// <summary>
    /// Who belongs, and how much (0 to 1): "work:&lt;zone&gt;", "work:holy" or "work:&lt;building id&gt;" for where they work,
    /// "home:&lt;house id&gt;" for the house they live in, and "devout", "hedge" or "refugee" for who they are.
    /// </summary>
    public Dictionary<string, double> Members { get; init; } = [];
}

public sealed record BlocRules
{
    /// <summary>Feeling a choice moves a bloc by, per tag it likes less each it dislikes; and the share kept each day.</summary>
    public double Step { get; init; } = 6;
    public double Decay { get; init; } = 0.98;

    /// <summary>At full feeling (±100), the contentment its households gain or lose, and the mood its members carry.</summary>
    public double Comfort { get; init; } = 0.1;
    public double Mood { get; init; } = 3;

    /// <summary>Members (summed by degree) a bloc needs before it speaks up.</summary>
    public double Least { get; init; } = 3;
    public List<BlocDef> Blocs { get; init; } = [];

    public BlocDef? Of(string id) => Blocs.FirstOrDefault(b => b.Id == id);
}

/// <summary>
/// Blocs: the town's interests, wherever their people live. Every stallholder in town is one bloc, every temple warden
/// another; a townsperson belongs to several at once, each to a degree, by where they work, the house they live in and who
/// they are, and the blocs grow and shrink as the town does. When the mayor decides, the blocs judge it the way heroes do
/// (<see cref="Regard"/>): one that likes the choice warms to the mayor, its members cheerier and the houses it lives in
/// quicker to grow; one that doesn't sours. It fades.
/// </summary>
public static class Blocs
{
    private static BlocRules Rules(Town town) => town.Content.Blocs;

    public static BlocDef? Def(Town town, string id) => Rules(town).Of(id);

    public static double Feeling(Town town, string blocId) => town.City.Blocs.Get(blocId, 0);

    private static readonly Dictionary<Zone, string> ZoneNames = Enum.GetValues<Zone>().Distinct().ToDictionary(z => z, z => z.ToString());

    private static string ZoneName(Zone z) => ZoneNames.TryGetValue(z, out var n) ? n : z.ToString();

    /// <summary>How much a townsperson belongs to a bloc, 0 to 1.</summary>
    public static double Degree(Town town, BlocDef bloc, Agent a)
    {
        if (a.Departed || a.IsHero) return 0;
        double d = 0;
        var work = a.JobId is int j ? town.Building(j) : null;
        var home = a.HomeId is int h ? town.Building(h) : null;
        foreach (var (key, v) in bloc.Members)
        {
            bool match = key switch
            {
                "devout" => a.Devout,
                "hedge" => a.HedgeSpell != "",
                "refugee" => a.Refugee,
                "work:holy" => work?.Def.Holy == true,
                _ when key.StartsWith("work:", StringComparison.Ordinal) => work != null && (key.AsSpan(5).SequenceEqual(work.Def.Id) || key.AsSpan(5).SequenceEqual(ZoneName(work.Def.Zone))),
                _ when key.StartsWith("home:", StringComparison.Ordinal) => home != null && key.AsSpan(5).SequenceEqual(home.Def.Id),
                _ => false,
            };
            if (match) d += v;
        }
        return Math.Min(1, d);
    }

    /// <summary>A bloc's size: its members, each by how much they belong.</summary>
    public static double Size(Town town, BlocDef bloc) => Js.Sum(town.Agents, a => Degree(town, bloc, a));

    /// <summary>The blocs big enough to speak, in the order blocs.json lists them.</summary>
    public static List<BlocDef> Speaking(Town town) => Rules(town).Blocs.Where(b => Size(town, b) >= Rules(town).Least).ToList();

    /// <summary>How a choice with these tags moves a bloc.</summary>
    public static double Moves(Town town, BlocDef b, IReadOnlyCollection<string> tags) =>
        Rules(town).Step * (tags.Count(b.Likes.Contains) - tags.Count(b.Dislikes.Contains));

    /// <summary>The speaking blocs a choice pleases and angers.</summary>
    public static (List<BlocDef> Pleased, List<BlocDef> Angered) Reactions(Town town, IReadOnlyCollection<string> tags)
    {
        var pleased = new List<BlocDef>();
        var angered = new List<BlocDef>();
        if (tags.Count == 0) return (pleased, angered);
        foreach (var b in Speaking(town))
        {
            double v = Moves(town, b, tags);
            if (v > 0) pleased.Add(b);
            else if (v < 0) angered.Add(b);
        }
        return (pleased, angered);
    }

    /// <summary>The blocs judge a choice; the Ledger says which spoke up.</summary>
    public static void Judge(Town town, IReadOnlyCollection<string> tags, string about)
    {
        var (pleased, angered) = Reactions(town, tags);
        if (pleased.Count + angered.Count == 0) return;
        foreach (var b in pleased.Concat(angered))
            town.City.Blocs[b.Id] = Math.Clamp(Feeling(town, b.Id) + Moves(town, b, tags), -100, 100);
        var parts = new List<string>();
        if (pleased.Count > 0) parts.Add($"{Voices(pleased)} are pleased");
        if (angered.Count > 0) parts.Add($"{Voices(angered)} grumble");
        town.Log(LedgerCategory.Town, Importance.Routine, $"{about}: {string.Join("; ", parts)}.");
    }

    /// <summary>Move one bloc's feeling about the mayor by a delta (clamped to ±100).</summary>
    public static void Nudge(Town town, string blocId, double delta) =>
        town.City.Blocs[blocId] = Math.Clamp(Feeling(town, blocId) + delta, -100, 100);

    /// <summary>"the stallholders and the wharf crews".</summary>
    public static string Voices(IReadOnlyList<BlocDef> blocs)
    {
        var names = blocs.Select(b => b.Voice).ToList();
        return names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
    }

    /// <summary>Contentment a household draws from how the blocs of its kind of house feel about the mayor.</summary>
    public static double Comfort(Town town, Building house)
    {
        double sum = 0, weight = 0;
        string key = "home:" + house.Def.Id;
        foreach (var b in Rules(town).Blocs)
            if (b.Members.TryGetValue(key, out double v))
            {
                sum += Feeling(town, b.Id) * v;
                weight += v;
            }
        return weight == 0 ? 0 : sum / weight / 100.0 * Rules(town).Comfort;
    }

    /// <summary>Mood a townsperson carries from how their blocs feel, each by how much they belong.</summary>
    public static double Mood(Town town, Agent a)
    {
        double sum = 0, weight = 0;
        foreach (var b in Rules(town).Blocs)
        {
            // Choices remembered plus the laws standing now (Politics.Standing).
            double f = Politics.Feeling(town, b.Id);
            if (f == 0) continue;
            double d = Degree(town, b, a);
            sum += f * d;
            weight += d;
        }
        return weight == 0 ? 0 : sum / Math.Max(1, weight) / 100.0 * Rules(town).Mood;
    }

    /// <summary>Old choices fade.</summary>
    public static void Daily(Town town)
    {
        foreach (var id in town.City.Blocs.Keys.ToList())
        {
            double v = Feeling(town, id) * Rules(town).Decay;
            town.City.Blocs[id] = Math.Abs(v) < 0.5 ? 0 : v;
        }
    }

    /// <summary>"warm", "cool"... for the City window.</summary>
    public static string Describe(double feeling) =>
        feeling >= 40 ? "devoted to you" : feeling >= 15 ? "warm toward you" : feeling <= -40 ? "angry with you" : feeling <= -15 ? "cool toward you" : "making up their minds";
}
