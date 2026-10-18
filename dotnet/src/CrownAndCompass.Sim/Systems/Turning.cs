namespace CrownAndCompass.Sim.Systems;

/// <summary>How a menace's leader comes over to the town: its race and the classes it fights as.</summary>
public sealed record TurnKindDef
{
    public string Race { get; init; } = "human";
    public IReadOnlyList<string> Classes { get; init; } = [];

    /// <summary>Added to the story when it can't come as it is (a beast or a dragon takes a borrowed shape).</summary>
    public string Shape { get; init; } = "";
}

/// <summary>Turncoats against their own history (content/game/saga.json "turning").</summary>
public sealed record TurningRules
{
    /// <summary>Chance a menace with wits begs quarter when heroes clear its seat, and is spared.</summary>
    public double Spared { get; init; } = 0.1;

    /// <summary>Daily chance a risen menace's leader comes over to the town unbeaten (once a campaign).</summary>
    public double Waver { get; init; } = 0.0004;

    /// <summary>A leader needs at least these wits to choose a side.</summary>
    public string MinCunning { get; init; } = "crafty";

    /// <summary>The highest level a turncoat leader or legend joins the town at.</summary>
    public int MaxLevel { get; init; } = 9;

    /// <summary>The loyalty a turncoat starts with: the town is slow to trust them.</summary>
    public double Loyalty { get; init; } = 40;
    public IReadOnlyDictionary<string, TurnKindDef> Kinds { get; init; } = new Dictionary<string, TurnKindDef>();

    /// <summary>{leader}, {seat}, {name}, {town}, {heirs}/{Heirs} (its lineage), {shape}.</summary>
    public IReadOnlyList<string> SpareText { get; init; } = [];
    public IReadOnlyList<string> WaverText { get; init; } = [];

    /// <summary>Daily chance a hero in town falls and rises as a menace, against everything their past taught them.</summary>
    public double Fall { get; init; } = 0.0001;
    public int FallDay { get; init; } = 90;
    public int FallLevel { get; init; } = 3;

    /// <summary>The menace kind a fallen hero of each class leads.</summary>
    public IReadOnlyDictionary<string, string> FallKinds { get; init; } = new Dictionary<string, string>();

    /// <summary>{hero}, {town}, {past}.</summary>
    public IReadOnlyList<string> FallText { get; init; } = [];

    /// <summary>A foe from another run may come over: daily chance from this day, once a campaign.</summary>
    public int LegendDay { get; init; } = 60;
    public double LegendChance { get; init; } = 0.003;

    /// <summary>{name}, {deed}, {town}.</summary>
    public IReadOnlyList<string> LegendFoe { get; init; } = [];

    /// <summary>Chance a new act's menace is led by a hero from another run.</summary>
    public double LegendHeroChance { get; init; } = 0.25;
    public IReadOnlyList<string> LegendHero { get; init; } = [];
}

/// <summary>
/// Now and then someone turns against everything their history says they are: a menace's leader comes
/// over to the town (spared when beaten, or unbeaten), or a hero falls and rises as a menace. Foes and
/// heroes from earlier campaigns (<see cref="Legends"/>) can come back the same way.
/// </summary>
public static class Turning
{
    public const string FromFallen = "fallen";
    public const string FromLegend = "legend";

    private static TurningRules Rules(Town town) => town.Content.Saga.Turning;

    public static void Daily(Town town)
    {
        if (!town.Saga.Enabled || town.Outcome != null) return;
        var r = Rules(town);
        foreach (var m in town.Saga.Wavered ? [] : Menaces.Alive(town))
        {
            if (m.Stage < MenaceStage.Rising || !CanTurn(town, m) || !town.Rng.Chance(r.Waver)) continue;
            town.Saga.Wavered = true;
            Redeem(town, m);
            return;
        }
        Fall(town);
        Recall(town);
    }

    /// <summary>A named menace whose leader has the wits to choose a side, and isn't the big bad or the warband.</summary>
    private static bool CanTurn(Town town, Menace m) =>
        m.Alive && m.Named && !m.BigBad && m.Origin != Menaces.FromWarband && Rules(town).Kinds.ContainsKey(m.Kind)
        && Menaces.CunningRank(town, m.Cunning) >= Menaces.CunningRank(town, Rules(town).MinCunning);

    /// <summary>Heroes cleared its seat: it may beg quarter and be spared. True when it turned.</summary>
    public static bool Spared(Town town, Menace m)
    {
        if (!town.Saga.Enabled || !CanTurn(town, m) || !town.Rng.Chance(Rules(town).Spared)) return false;
        Redeem(town, m, spared: true);
        return true;
    }

    /// <summary>The menace is over: its leader joins the town as a hero (spared when beaten, or of their own will).</summary>
    public static Agent? Redeem(Town town, Menace m, bool spared = false)
    {
        var r = Rules(town);
        var texts = spared ? r.SpareText : r.WaverText;
        var kind = r.Kinds[m.Kind];
        var lineage = Annals.Lineage(town.Content.History, m.Lineage);
        string heirs = lineage?.Title ?? m.Def.People;
        string text = texts.Count == 0 ? "{leader} comes over to {town}." : texts[town.Rng.Range(0, texts.Count)];
        text = Saga.Fill(town, text
            .Replace("{heirs}", heirs)
            .Replace("{Heirs}", Saga.Cap(heirs))
            .Replace("{shape}", kind.Shape), m);
        var hero = Join(town, m.Leader, kind, m.Level, town.Rng.Pick(kind.Classes), $"Once {m.Name}, until they turned against {heirs}.");
        m.Redeemed = true;
        m.TurnedHeroId = hero?.Id;
        Menaces.End(town, m);
        town.Log(LedgerCategory.War, Importance.Major, text, hero != null ? [hero.Id] : []);
        return hero;
    }

    /// <summary>A former foe takes up arms for the town, named as it was, at a level the town can trust.</summary>
    private static Agent? Join(Town town, string name, TurnKindDef kind, int level, string classId, string past)
    {
        var cls = town.Content.Classes.FirstOrDefault(c => c.Id == classId);
        if (cls == null) return null;
        string? race = town.Content.Races.Any(x => x.Id == kind.Race) ? kind.Race : null;
        var a = Quests.CreateHero(town, cls, Math.Clamp(level, 1, Rules(town).MaxLevel), race);
        a.GivenName = name;
        a.FamilyName = "";
        a.Hero!.Background = "";
        a.Hero.Past = past;
        a.Hero.Loyalty = Rules(town).Loyalty;
        return a;
    }

    /// <summary>Rarely, a seasoned hero in town falls and rises as a menace's leader.</summary>
    private static void Fall(Town town)
    {
        var r = Rules(town);
        if (town.Day < r.FallDay || Menaces.Alive(town).Any(m => m.Origin == FromFallen)) return;
        foreach (var a in town.Heroes.Where(h => !h.IsAway && h.Hero!.Level >= r.FallLevel).OrderBy(h => h.Id).ToList())
        {
            var s = a.Hero!;
            if (!r.FallKinds.ContainsKey(s.Cls.Id)) continue;
            // Loyal heroes are harder to turn, but nobody is beyond it.
            if (!town.Rng.Chance(r.Fall * Math.Max(0.25, 1.5 - s.Loyalty / 100) * Notoriety.FallFactor(town, s))) continue;
            FallFrom(town, a);
            return;
        }
    }

    /// <summary>The hero leaves the town and rises as the leader of a menace of their class's kind.</summary>
    public static Menace? FallFrom(Town town, Agent a)
    {
        var r = Rules(town);
        var s = a.Hero!;
        if (!r.FallKinds.TryGetValue(s.Cls.Id, out var kind)) return null;
        var m = Menaces.Rise(town, kind, a.Name, s.Level, FromFallen);
        if (m == null) return null;
        string past = Annals.Past(town, a);
        string text = r.FallText.Count == 0 ? "{hero} turns against {town}." : r.FallText[town.Rng.Range(0, r.FallText.Count)];
        s.TurnedTo = m.Id;
        m.FallenHeroId = a.Id;
        town.Log(LedgerCategory.War, Importance.Major, text
            .Replace("{hero}", Loyalty.Styled(town, a))
            .Replace("{town}", town.Name)
            .Replace("{past}", past == "" ? "" : " " + past), [a.Id]);
        Notoriety.Betrayed(town, a);
        Loyalty.Strip(a);
        Population.Depart(town, a);
        Menaces.Announce(town, m, Quests.Site(town, m.SeatId)!);
        return m;
    }

    /// <summary>A foe from another campaign comes to fight for the town, once a campaign.</summary>
    private static void Recall(Town town)
    {
        var r = Rules(town);
        var s = town.Saga;
        if (s.LegendJoined || town.Day < r.LegendDay) return;
        var foes = s.Legends.Where(l => l.Kind == Legend.Foe && !s.LegendsUsed.Contains(l.Name)).ToList();
        if (foes.Count == 0 || !town.Rng.Chance(r.LegendChance)) return;
        Return(town, foes[town.Rng.Range(0, foes.Count)]);
    }

    /// <summary>The legend joins the town as a hero, named as it was.</summary>
    public static Agent? Return(Town town, Legend l)
    {
        var r = Rules(town);
        var s = town.Saga;
        var kind = r.Kinds.GetValueOrDefault(l.MenaceKind) ?? r.Kinds.Values.FirstOrDefault() ?? new TurnKindDef();
        if (kind.Classes.Count == 0) return null;
        // As strong as the town's best, no stronger: a legend joins as a peer, not a god.
        int best = town.Heroes.Select(h => h.Hero!.Level).DefaultIfEmpty(1).Max();
        var hero = Join(town, l.Name, kind, Math.Min(l.Level, best + 1), town.Rng.Pick(kind.Classes), $"In another age, {l.Name} {l.Deed}.");
        if (hero == null) return null;
        s.LegendJoined = true;
        s.LegendsUsed.Add(l.Name);
        string text = r.LegendFoe.Count == 0 ? "{name} comes to fight for {town}." : r.LegendFoe[town.Rng.Range(0, r.LegendFoe.Count)];
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Fill(town, text, l) + kind.Shape), [hero.Id]);
        return hero;
    }

    /// <summary>A new act's menace may be led by a hero of another campaign, fallen since. Call before it is announced.</summary>
    public static void Haunt(Town town, Menace m)
    {
        var s = town.Saga;
        // Once a campaign, and only at the head of a host or a coven: a beast's den needs no general.
        if (s.LegendHaunted || m.Kind == "beasts") return;
        var heroes = s.Legends.Where(l => l.Kind == Legend.Hero && !s.LegendsUsed.Contains(l.Name)).ToList();
        if (heroes.Count == 0 || !town.Rng.Chance(Rules(town).LegendHeroChance)) return;
        s.LegendHaunted = true;
        var l = heroes[town.Rng.Range(0, heroes.Count)];
        s.LegendsUsed.Add(l.Name);
        m.Leader = l.Name;
        var texts = Rules(town).LegendHero;
        if (texts.Count > 0) m.Legend = Fill(town, texts[town.Rng.Range(0, texts.Count)], l);
    }

    private static string Fill(Town town, string text, Legend l) =>
        text.Replace("{name}", l.Name).Replace("{deed}", l.Deed).Replace("{town}", town.Name);
}
