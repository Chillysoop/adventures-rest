namespace CrownAndCompass.Sim.Systems;

/// <summary>A way to mend a lasting wound (wounds.json <c>mends</c>): a priest's restoration or regrowth, or an artificer's clockwork.</summary>
public sealed record MendDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public double Cost { get; init; }

    /// <summary>What the town must have: "shrine" (a shrine or the temple), "temple", or "workshop" (<see cref="Clockwork.Workshop"/>).</summary>
    public string Needs { get; init; } = "";

    /// <summary>A healer hero of at least this level must be home to cast it; 0 for none.</summary>
    public int Level { get; init; }

    /// <summary>The Ledger: {hero}, {wound}, {healer}, {part}.</summary>
    public string Text { get; init; } = "";
}

/// <summary>A kind of lasting wound and what it costs a hero (wounds.json <c>wounds</c>).</summary>
public sealed record WoundDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";

    /// <summary>Added to the hero's attack, armour and skill checks while the wound stands.</summary>
    public int Attack { get; init; }
    public int Ac { get; init; }
    public int Skill { get; init; }

    /// <summary>Days until it heals on its own; 0 for never.</summary>
    public int Heals { get; init; }

    /// <summary>The mends that put it right (<see cref="MendDef"/> ids).</summary>
    public IReadOnlyList<string> Mends { get; init; } = [];

    /// <summary>What a clockwork mend fits in its place.</summary>
    public string Part { get; init; } = "";

    /// <summary>The Ledger as the hero comes home with it ({hero}, {site}) and as it heals ({hero}).</summary>
    public string Text { get; init; } = "";
    public string Healed { get; init; } = "";
}

/// <summary>Lasting wounds (content/game/wounds.json).</summary>
public sealed record WoundRules
{
    /// <summary>Chance of a lasting wound for each time a hero went down in a fight, and for pulling through on death saves.</summary>
    public double Down { get; init; } = 0.25;
    public double Saved { get; init; } = 0.55;

    /// <summary>A hero carries at most this many at once.</summary>
    public int Most { get; init; } = 3;

    /// <summary>The stand-in mayor pays for a mend once the treasury holds this many times its cost.</summary>
    public double AutoReserve { get; init; } = 4;
    public List<MendDef> Mends { get; init; } = [];
    public List<WoundDef> Wounds { get; init; } = [];
}

/// <summary>A wound a hero carries: what, where and when they took it, and the clockwork fitted in its place, if any.</summary>
public sealed class Wound
{
    public string Id { get; set; } = "";
    public string Site { get; set; } = "";
    public int Day { get; set; }

    /// <summary>Fitted with clockwork: the wound stands, but costs the hero nothing.</summary>
    public bool Clockwork { get; set; }
}

/// <summary>
/// Lasting wounds, after Kenshi and RimWorld: a hero who went down in a fight, or crawled back from death's door, may
/// come home with a lost eye, a lame leg or burn scars that stay with them, a point or two off their blows, their
/// armour or their checks. Some heal in time. The rest want a priest's restoration, the temple's great prayer of
/// regrowth from a high enough healer, or an artificer's clockwork part. Heroes pay for their own mends when they can;
/// the mayor can pay for the rest. Wounds roll on their own dice, seeded by the hero and the hour, so the town's
/// stream stays as it was.
/// </summary>
public static class Wounds
{
    public static WoundRules Rules(Content c) => c.Wounds;

    public static WoundDef? Def(Content c, string id) => c.Wounds.Wounds.FirstOrDefault(w => w.Id == id);

    public static MendDef? Mend(Content c, string id) => c.Wounds.Mends.FirstOrDefault(m => m.Id == id);

    /// <summary>The wounds that still cost the hero something.</summary>
    private static IEnumerable<WoundDef> Open(Content c, HeroSheet s)
    {
        foreach (var w in s.Wounds)
            if (!w.Clockwork && Def(c, w.Id) is { } d) yield return d;
    }

    public static int Attack(Content c, HeroSheet s) => s.Wounds.Count == 0 ? 0 : Open(c, s).Sum(d => d.Attack);

    public static int Ac(Content c, HeroSheet s) => s.Wounds.Count == 0 ? 0 : Open(c, s).Sum(d => d.Ac);

    public static int Skill(Content c, HeroSheet s) => s.Wounds.Count == 0 ? 0 : Open(c, s).Sum(d => d.Skill);

    private static Rng Dice(Town town, Agent a, int salt) => new(unchecked((int)Voices.Hash("wound", town.Seed, town.Tick, a.Id, salt)));

    /// <summary>A company home from a fight: each survivor who went down, or came back from death's door, may carry a wound home.</summary>
    public static void Home(Town town, ExpeditionResult result, IEnumerable<Agent> survivors, string site)
    {
        var r = Rules(town.Content);
        if (r.Wounds.Count == 0) return;
        foreach (var a in survivors)
        {
            if (a.Dead || a.Hero is not { } s) continue;
            int downs = result.Beats.Count(b => b.HeroId == a.Id && b.Kind == "down");
            int saved = result.Beats.Count(b => b.HeroId == a.Id && b.Kind == "saved");
            if (downs + saved == 0) continue;
            var dice = Dice(town, a, downs * 7 + saved);
            double chance = 1 - Math.Pow(1 - r.Down, Math.Max(0, downs - saved)) * Math.Pow(1 - r.Saved, saved);
            if (s.Wounds.Count >= r.Most || !dice.Chance(chance)) continue;
            var free = r.Wounds.Where(w => s.Wounds.All(o => o.Id != w.Id)).ToList();
            if (free.Count == 0) continue;
            var def = free[dice.Range(0, free.Count)];
            s.Wounds.Add(new Wound { Id = def.Id, Site = site, Day = town.Day });
            town.Log(LedgerCategory.Quest, Importance.Notable, Fill(def.Text, a, site), [a.Id]);
        }
    }

    /// <summary>Wounds that heal in time heal; heroes home with the gold mend themselves; the stand-in mayor pays for the rest.</summary>
    public static void Daily(Town town, bool autoMayor)
    {
        var c = town.Content;
        var r = Rules(c);
        foreach (var a in town.Heroes)
        {
            var s = a.Hero!;
            if (s.Wounds.Count == 0) continue;
            for (int i = s.Wounds.Count - 1; i >= 0; i--)
            {
                var w = s.Wounds[i];
                if (Def(c, w.Id) is not { Heals: > 0 } d || town.Day - w.Day < d.Heals) continue;
                s.Wounds.RemoveAt(i);
                if (d.Healed != "") town.Log(LedgerCategory.Quest, Importance.Routine, Fill(d.Healed, a, w.Site), [a.Id]);
            }
            if (a.IsAway) continue;
            for (int i = 0; i < s.Wounds.Count; i++)
            {
                if (s.Wounds[i].Clockwork || Best(town, a, i) is not { } m) continue;
                if (a.Gold >= m.Cost) Apply(town, a, i, m, fromTreasury: false);
                else if (autoMayor && town.Treasury >= m.Cost * r.AutoReserve) Apply(town, a, i, m, fromTreasury: true);
                else continue;
                break;
            }
        }
    }

    /// <summary>Why a mend can't be had for this wound now, or null when it can.</summary>
    public static string? Blocked(Town town, Agent a, int index, MendDef m)
    {
        if (a.Hero is not { } s || index < 0 || index >= s.Wounds.Count) return "no such wound";
        var w = s.Wounds[index];
        if (Def(town.Content, w.Id) is not { } d || !d.Mends.Contains(m.Id)) return "won't mend that";
        if (w.Clockwork) return "already fitted";
        if (a.IsAway) return "away";
        switch (m.Needs)
        {
            case "shrine" when Shrines.HolyPlace(town) == null: return "needs a shrine";
            case "temple" when Shrines.Temple(town) == null: return "needs the temple";
            case "workshop" when !Clockwork.Workshop(town): return "needs an artificers' workshop";
        }
        if (m.Level > 0 && Healer(town, m.Level) == null) return $"needs a level {m.Level} healer home";
        return null;
    }

    /// <summary>The cheapest mend open for a wound now, or null.</summary>
    public static MendDef? Best(Town town, Agent a, int index)
    {
        if (a.Hero is not { } s || index < 0 || index >= s.Wounds.Count || Def(town.Content, s.Wounds[index].Id) is not { } d) return null;
        return d.Mends.Select(id => Mend(town.Content, id)).OfType<MendDef>()
            .Where(m => Blocked(town, a, index, m) == null)
            .OrderBy(m => m.Cost).ThenBy(m => m.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    /// <summary>The highest-level healer hero home, at least this level.</summary>
    private static Agent? Healer(Town town, int level) =>
        town.Heroes.Where(h => !h.IsAway && h.Hero!.Cls.Healer && h.Hero.Level >= level)
            .OrderByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).FirstOrDefault();

    /// <summary>The mayor pays for a mend from the treasury. Returns why not, or null when it is done.</summary>
    public static string? Pay(Town town, int heroId, int index, string mendId)
    {
        if (heroId < 1 || heroId > town.Agents.Count) return "no such hero";
        var a = town.Agent(heroId);
        if (Mend(town.Content, mendId) is not { } m) return "no such mend";
        if (Blocked(town, a, index, m) is { } why) return why;
        if (town.Treasury < m.Cost) return "not enough gold";
        Apply(town, a, index, m, fromTreasury: true);
        return null;
    }

    private static void Apply(Town town, Agent a, int index, MendDef m, bool fromTreasury)
    {
        var s = a.Hero!;
        var w = s.Wounds[index];
        var d = Def(town.Content, w.Id)!;
        if (fromTreasury)
        {
            town.Book(TreasuryAccount.Temple, -m.Cost);
            Loyalty.Sway(a, town.Content.Loyalty.Raised * 0.5);
        }
        else a.Gold -= m.Cost;
        if (m.Id == "clockwork") w.Clockwork = true;
        else s.Wounds.RemoveAt(index);
        string healer = m.Level > 0 ? Healer(town, m.Level)?.GivenName ?? "The high priest" : "";
        town.Log(LedgerCategory.Faith, Importance.Notable, Fill(m.Text, a, w.Site).Replace("{wound}", d.Name).Replace("{healer}", healer).Replace("{part}", d.Part), [a.Id]);
    }

    private static string Fill(string text, Agent a, string site) =>
        text.Replace("{hero}", a.GivenName).Replace("{site}", site == "" ? "the wilds" : site);
}
