namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// A calling: a lasting part a hero takes up in the town through a story of their class. A bard plays
/// the tavern every night, a paladin swears an oath to the town, a rogue keeps the thieves' guild's
/// knives off it. Like a decree it moves the town every day (the fields it shares with <see cref="DecreeDef"/>),
/// but it lasts as long as its hero stays and stays loyal, not for a span of days.
/// </summary>
public sealed record CallingDef : DecreeDef
{
    /// <summary>The class whose stories offer it.</summary>
    public string Class { get; init; } = "";

    /// <summary>Added each day to the favour of the town's god.</summary>
    public double Favour { get; init; }

    /// <summary>A faction it works with or against, and what it does each day to that faction's standing and heat.</summary>
    public string Faction { get; init; } = "";
    public double Standing { get; init; }
    public double Heat { get; init; }

    /// <summary>Added to dread each day.</summary>
    public double Dread { get; init; }

    /// <summary>Added to the town's defence while the hero is in town.</summary>
    public double Defence { get; init; }

    /// <summary>What having a place in the town is worth to the hero's loyalty.</summary>
    public double Loyalty { get; init; } = 4;
}

/// <summary>
/// Callings held, by calling, each by one hero (<see cref="StoryState.Callings"/>). A hero holds one at a
/// time; a calling ends when its hero leaves, dies or turns, renounces it when their loyalty runs out, or
/// a story ends it.
/// </summary>
public static class Callings
{
    /// <summary>The callings held now, in the order they were taken up.</summary>
    public static IEnumerable<CallingDef> Held(Town town) =>
        town.Stories.Callings.Keys.Select(id => town.Content.Stories.Calling(id)).OfType<CallingDef>();

    public static Agent? Holder(Town town, string id) =>
        town.Stories.Callings.TryGetValue(id, out int h) && town.Agent(h) is { Departed: false, Dead: false, Hero: not null } a ? a : null;

    /// <summary>The calling a hero holds, if any.</summary>
    public static CallingDef? Of(Town town, Agent a)
    {
        foreach (var (id, h) in town.Stories.Callings)
            if (h == a.Id) return town.Content.Stories.Calling(id);
        return null;
    }

    /// <summary>The hero takes up the calling, giving up any other they held. Returns a note for the Ledger.</summary>
    public static string Take(Town town, Agent hero, string id)
    {
        var d = town.Content.Stories.Calling(id) ?? throw new InvalidDataException($"Unknown calling {id}");
        if (Of(town, hero) is { } old) town.Stories.Callings.Remove(old.Id);
        town.Stories.Callings[id] = hero.Id;
        return $"{hero.GivenName}: {d.Name}";
    }

    public static void End(Town town, string id, string why)
    {
        var d = town.Content.Stories.Calling(id);
        if (d == null || !town.Stories.Callings.ContainsKey(id)) return;
        var hero = Holder(town, id);
        town.Stories.Callings.Remove(id);
        if (hero != null && why != "")
            town.Log(LedgerCategory.Story, Importance.Notable, $"{Loyalty.Styled(town, hero)} {why} ({d.Name.ToLowerInvariant()}).", [hero.Id]);
    }

    /// <summary>Defence from callings whose heroes are in town.</summary>
    public static double Defence(Town town)
    {
        double sum = 0;
        foreach (var (id, h) in town.Stories.Callings)
            if (town.Content.Stories.Calling(id) is { Defence: > 0 } d && !town.Agent(h).IsAway) sum += d.Defence;
        return sum;
    }

    /// <summary>Daily: callings whose heroes are gone end, those whose heroes have had enough are renounced, and the rest move the town.</summary>
    public static void Daily(Town town)
    {
        var st = town.Stories;
        if (st.Callings.Count == 0) return;
        double desert = town.Content.Loyalty.Desert;
        foreach (var id in st.Callings.Keys.ToList())
        {
            var d = town.Content.Stories.Calling(id);
            var hero = Holder(town, id);
            if (d == null || hero == null || hero.Hero!.TurnedTo != null || hero.Hero.DefectedTo != null)
            {
                var gone = town.Agent(st.Callings[id]);
                st.Callings.Remove(id);
                if (d != null) town.Log(LedgerCategory.Story, Importance.Routine, $"{d.Name} ends with {gone.Name} {(gone.Dead ? "dead" : "gone from " + town.Name)}.");
                continue;
            }
            if (hero.Hero.Loyalty < desert)
            {
                End(town, id, "has had enough of the town and gives it up");
                continue;
            }
            if (d.Gold != 0) town.Book(TreasuryAccount.Callings, d.Gold);
            if (d.Tension != 0) town.Tension = Math.Max(0, town.Tension + d.Tension);
            if (d.Dread != 0) town.Saga.Dread = Math.Clamp(town.Saga.Dread + d.Dread, 0, 100);
            if (d.Favour != 0)
            {
                var god = Faith.TempleGod(town) ?? Faith.CharterGod(town);
                town.Faiths.Favor[god.Id] = Math.Max(0, town.Faiths.Favor.Get(god.Id, 0) + d.Favour);
            }
            if (d.Faction != "" && Factions.Find(town, d.Faction) is { Active: true } f)
            {
                f.Standing = Math.Clamp(f.Standing + d.Standing, -100, 100);
                f.Heat = Math.Clamp(f.Heat + d.Heat, 0, 100);
            }
        }
    }
}
