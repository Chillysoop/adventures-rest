namespace CrownAndCompass.Sim.Systems;

/// <summary>One level of the undercity (content/game/undercity.json): opened by a charter tier, with its site under the town.</summary>
public sealed record UnderLevelDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>The charter tier (city.json) that opens it.</summary>
    public string Tier { get; init; } = "";

    /// <summary>What it is, for the City window and the Ledger when it opens.</summary>
    public string Text { get; init; } = "";

    /// <summary>What happens when it rises, for the Ledger.</summary>
    public string Rise { get; init; } = "";

    /// <summary>Stir it gathers each day while uncleared.</summary>
    public double Stir { get; init; }

    public required SiteDef Site { get; init; }
}

/// <summary>Numbers for the undercity.</summary>
public sealed record UndercityRules
{
    public double StirPerFaction { get; init; } = 0.25;
    public double StirPerHundred { get; init; } = 0.04;
    public double WarnAt { get; init; } = 60;
    public double RiseAt { get; init; } = 100;
    public double RiseAfter { get; init; } = 30;
    public double PatrolCost { get; init; } = 6;
    public double PatrolCut { get; init; } = 0.75;
    public int[] RiseDead { get; init; } = [1, 3];
    public double RiseMandate { get; init; } = -5;
    public double RiseTension { get; init; } = 8;
    public int RefillDays { get; init; } = 60;
    public double ClearedMandate { get; init; } = 3;
    public double ClearedHeat { get; init; } = 15;
    public double StandInAt { get; init; } = 50;
    public double StandInGold { get; init; } = 600;
    public List<UnderLevelDef> Levels { get; init; } = [];

    public UnderLevelDef? Level(string id) => Levels.FirstOrDefault(l => l.Id == id);
}

/// <summary>An open level of the undercity.</summary>
public sealed class UnderLevel
{
    public string Id { get; set; } = "";
    public int OpenedDay { get; set; }

    /// <summary>How restless it is below, 0 up to the rise.</summary>
    public double Stir { get; set; }

    /// <summary>The watch goes down at night, at a daily cost.</summary>
    public bool Patrol { get; set; }

    /// <summary>Times heroes have cleared it, and times it has risen.</summary>
    public int Cleared { get; set; }
    public int Rose { get; set; }

    /// <summary>Said that it is stirring (once, until it rises or is cleared).</summary>
    public bool Warned { get; set; }

    /// <summary>When the dark below fills again after a clearing.</summary>
    public int? RefillDay { get; set; }
}

/// <summary>
/// The undercity: cellars, sewers, catacombs, vaults and the deep ways under the town, a level opening with
/// each charter from the village up. Each level is a site under the town that heroes can delve with no
/// road to walk. Left alone it stirs, faster with secret factions at work and more people above, and when
/// it rises something comes up into the streets. The council can send the watch down on patrol; clearing a
/// level quiets it for a season and turns up the secret factions' hiding places, then the dark fills again.
/// </summary>
public static class Undercity
{
    private static UndercityRules Rules(Town town) => town.Content.Undercity;

    public static UnderLevelDef Def(Town town, UnderLevel l) => Rules(town).Level(l.Id)!;

    public static UnderLevel? Of(Town town, string id) => town.Undercity.FirstOrDefault(l => l.Id == id);

    /// <summary>Whether a site is a level of the undercity.</summary>
    public static bool IsUnder(Town town, SiteState s) => Rules(town).Levels.Any(l => l.Site.Id == s.Def.Id);

    /// <summary>The open level a site belongs to.</summary>
    public static UnderLevel? LevelOf(Town town, string siteId) =>
        town.Undercity.FirstOrDefault(l => Def(town, l).Site.Id == siteId);

    /// <summary>Whether an undercity site's level stirs enough (the town has heard it) for companies and the stand-in mayor to go down.</summary>
    public static bool Stirring(Town town, SiteState s) => LevelOf(town, s.Def.Id) is { } l && l.Stir >= Rules(town).WarnAt;

    public static SiteState Site(Town town, UnderLevel l) => Quests.Site(town, Def(town, l).Site.Id)!;

    /// <summary>The stir a level gathers today.</summary>
    public static double Rate(Town town, UnderLevel l)
    {
        var r = Rules(town);
        double rate = Def(town, l).Stir
            + r.StirPerFaction * town.Factions.Count(f => f.Active && f.Secret)
            + r.StirPerHundred * town.Residents.Count / 100.0;
        rate *= 1 - Research.Ward(town);
        rate *= 1 - Institutions.Hush(town);
        return l.Patrol ? rate * (1 - r.PatrolCut) : rate;
    }

    public static string Mood(Town town, UnderLevel l)
    {
        var r = Rules(town);
        if (Site(town, l).Cleared) return "quiet";
        return l.Stir >= r.WarnAt ? "stirring" : l.Stir >= r.WarnAt / 2 ? "restless" : "quiet";
    }

    /// <summary>Send the watch down at night, or call them back. The stand-in mayor does the same.</summary>
    public static bool SetPatrol(Town town, string id, bool on)
    {
        var l = Of(town, id);
        if (l == null || l.Patrol == on) return false;
        if (Stewards.Intercept(town, "order", new StewardAsk { Area = "order", Kind = "undercity", Title = on ? $"Patrol {Def(town, l).Name} · {Js.Str(Rules(town).PatrolCost)}g a day" : $"Stop patrolling {Def(town, l).Name}", Why = on ? "• Something stirs below\n• Lanterns keep it down" : "• It is quiet below\n• Saves the watch's wages", S1 = id, I1 = on ? 1 : 0 })) return false;
        l.Patrol = on;
        town.Log(LedgerCategory.Town, Importance.Routine,
            on ? $"The watch takes lanterns down into {Def(town, l).Name} each night, for {Js.Str(Rules(town).PatrolCost)} gold a day."
               : $"The watch stops going down into {Def(town, l).Name}.");
        return true;
    }

    public static void Daily(Town town, bool auto)
    {
        var r = Rules(town);
        Open(town);
        foreach (var l in town.Undercity)
        {
            var site = Site(town, l);
            if (l.Patrol) town.Book(TreasuryAccount.Undercity, -r.PatrolCost);
            if (site.Cleared)
            {
                if (l.RefillDay == null)
                {
                    // Heroes have just cleared it.
                    l.Cleared++;
                    l.Stir = 0;
                    l.Warned = false;
                    l.RefillDay = town.Day + r.RefillDays;
                    town.Mandate = Math.Clamp(town.Mandate + r.ClearedMandate, 0, 100);
                    string found = "";
                    foreach (var f in town.Factions.Where(f => f.Active && f.Secret).OrderBy(f => f.Id, StringComparer.Ordinal))
                    {
                        f.Heat = Math.Min(100, f.Heat + r.ClearedHeat);
                        if (!f.Known) found = $" Down there they find chalk signs and a hidden room someone in town has been using: whoever {f.Verb("meets", "meet")} there, the watch will be asking.";
                    }
                    town.Log(LedgerCategory.Town, l.Cleared == 1 ? Importance.Notable : Importance.Routine, $"{Cap(Def(town, l).Name)} {(l.Cleared == 1 ? "are" : "are again")} quiet. The town sleeps easier.{found}");
                }
                else if (town.Day >= l.RefillDay)
                {
                    site.Cleared = false;
                    site.ThoughtCleared = false;
                    site.Known = SiteKnowledge.Rumoured;
                    l.RefillDay = null;
                    l.Stir = 0;
                    town.Log(LedgerCategory.Town, Importance.Routine, $"Night-soil men hear things moving in {Def(town, l).Name} again. The dark below has filled.");
                }
                continue;
            }
            l.Stir += Rate(town, l);
            if (!l.Warned && l.Stir >= r.WarnAt)
            {
                l.Warned = true;
                town.Log(LedgerCategory.War, Importance.Notable, $"Scratching under the floors and lights where no one should be: something is stirring in {Def(town, l).Name}. Heroes could go down, or the watch.");
            }
            if (l.Stir >= r.RiseAt) Rise(town, l);
        }
        if (auto) StandIn(town);
    }

    /// <summary>Levels whose charter the town now holds open, adding their site to the board.</summary>
    private static void Open(Town town)
    {
        foreach (var d in Rules(town).Levels)
        {
            if (Of(town, d.Id) != null || !City.Reached(town, d.Tier)) continue;
            town.Undercity.Add(new UnderLevel { Id = d.Id, OpenedDay = town.Day });
            var site = Quests.Site(town, d.Site.Id);
            if (site == null)
            {
                site = new SiteState(d.Site);
                town.Sites.Add(site);
            }
            site.Known = SiteKnowledge.Rumoured;
            site.Believed = Rumors.Occupants(town.Content, d.Site);
            site.BelievedTier = d.Site.Tier;
            town.Log(LedgerCategory.Town, Importance.Notable, $"Digging under {town.Name} breaks into {d.Name}. {d.Text}");
        }
    }

    private static void Rise(Town town, UnderLevel l)
    {
        var r = Rules(town);
        var d = Def(town, l);
        l.Rose++;
        l.Stir = r.RiseAfter;
        l.Warned = false;
        int dead = Math.Max(0, town.Rng.Range(r.RiseDead[0], r.RiseDead[1] + 1) - (l.Patrol ? 1 : 0));
        var folk = town.Residents.Where(a => a.Hero == null).OrderBy(a => a.Id).ToList();
        var lost = new List<string>();
        for (int i = 0; i < dead && folk.Count > 0; i++)
        {
            var a = folk[town.Rng.Range(0, folk.Count)];
            folk.Remove(a);
            a.Dead = true;
            Population.Depart(town, a);
            lost.Add(a.Name);
        }
        town.Mandate = Math.Clamp(town.Mandate + r.RiseMandate, 0, 100);
        town.Tension += r.RiseTension;
        town.Log(LedgerCategory.War, Importance.Major,
            $"{d.Rise}{(lost.Count > 0 ? $" {Expedition.ListNames(lost)} {(lost.Count == 1 ? "is" : "are")} gone by morning." : "")} Until heroes clear {d.Name}, it will come again.");
    }

    private static void StandIn(Town town)
    {
        var r = Rules(town);
        foreach (var l in town.Undercity)
        {
            bool want = !Site(town, l).Cleared && l.Stir >= r.StandInAt && town.Treasury >= r.StandInGold;
            if (want != l.Patrol) SetPatrol(town, l.Id, want);
        }
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
