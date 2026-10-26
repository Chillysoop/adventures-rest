namespace CrownAndCompass.Sim.Systems;

public sealed record GridSize
{
    public int W { get; init; } = 12;
    public int H { get; init; } = 8;
}

/// <summary>What a room feature makes of the showdown's floor: a kind of ground and how many cells of it.</summary>
public sealed record GroundDef
{
    public string Kind { get; init; } = "";
    public int Cells { get; init; }
}

public sealed record TalkWayDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Ability { get; init; } = "cha";
    public string Against { get; init; } = "wis";
    public string Text { get; init; } = "";
}

public sealed record TalkRules
{
    public int Wins { get; init; } = 3;
    public int Losses { get; init; } = 2;
    public int MinInt { get; init; } = 6;

    /// <summary>The share of the hoard a master talked into leaving lets the party take.</summary>
    public double Loot { get; init; } = 0.5;
    public List<TalkWayDef> Ways { get; init; } = [];
    public Dictionary<string, string> Lines { get; init; } = [];
}

/// <summary>content/game/showdown.json.</summary>
public sealed record ShowdownRules
{
    public double Chance { get; init; } = 0.5;
    public int MinTier { get; init; } = 4;
    public int WaitHours { get; init; } = 24;
    public GridSize Grid { get; init; } = new();
    public int Speed { get; init; } = 6;
    public Dictionary<string, int> FastClasses { get; init; } = [];
    public Dictionary<string, int> SmallRaces { get; init; } = [];
    public int Fly { get; init; } = 8;
    public int Ranged { get; init; } = 12;
    public int Thrown { get; init; } = 4;
    public int SpellRange { get; init; } = 24;
    public int BlastRadius { get; init; } = 2;
    public int DodgeAc { get; init; } = 2;
    public List<int> Pillars { get; init; } = [2, 5];
    public Dictionary<string, GroundDef> Ground { get; init; } = [];
    public TalkRules Talk { get; init; } = new();
}

/// <summary>
/// Showdowns: now and then a company reaching the master of a meaningful place stops at its door, and the mayor
/// watches (or takes command of) the meeting on a grid. The delve runs up to the boss as always; the party then
/// waits (<see cref="Party.AtTheDoor"/>) while the fight is played, and the rest of the trip runs once it is over.
/// Only with a mayor at the table: the stand-in's town never stops for one, so sweeps don't move.
/// </summary>
public static class Showdowns
{
    /// <summary>The showdown being played, with what the rest of the trip needs. Not saved: a load mid-fight goes back in as an ordinary delve.</summary>
    public sealed class Live
    {
        public required Party Party { get; init; }
        public required Expedition.Venture Venture { get; init; }
        public required Expedition.Venture.Showdown Fight { get; init; }
        public required SiteState Site { get; init; }
        internal Roads.Arrival Arrival { get; init; } = null!;
        public int Since { get; init; }

        /// <summary>Set up for a screenshot or the smoke run: no real company waits on it.</summary>
        public bool Staged { get; init; }
    }

    /// <summary>
    /// A showdown for a screenshot or the smoke run: the town's best heroes before the master of the hardest place
    /// still held (or <paramref name="siteId"/>), on dice of its own so nothing in the town's stream moves.
    /// </summary>
    public static Live? Stage(Town town, string? siteId = null, string phase = "talk")
    {
        var heroes = town.Agents.Where(a => a.Hero != null && !a.Dead).OrderByDescending(a => a.Hero!.Level).ThenBy(a => a.Id).Take(4).ToList();
        int level = heroes.Count == 0 ? 1 : (int)Math.Round(heroes.Average(a => a.Hero!.Level));
        var site = (siteId != null ? Quests.Site(town, siteId) : null)
            ?? town.Sites.Where(x => !x.Cleared && x.Def.Boss is { Kind: "combat" } && x.Def.Boss.Kind == "combat" && !string.IsNullOrEmpty(x.Def.Boss.Monster)).OrderBy(x => phase == "talk" && (Prowess.Lib(town.Content, town.Content.Monster(x.Def.Boss.Monster))?.Int ?? 10) < Rules(town).Talk.MinInt ? 1 : 0).ThenBy(x => Math.Abs(x.Def.Tier - level)).ThenByDescending(x => x.Def.Tier).ThenBy(x => x.Def.Id, StringComparer.Ordinal).FirstOrDefault();
        if (heroes.Count == 0 || site == null) return null;
        var rng = new Rng(unchecked((int)Voices.Hash("showdown-stage", town.Seed, town.Tick)));
        var v = new Expedition.Venture(rng, town.Content, heroes, site.Def);
        var party = new Party { Id = 0, Name = "staged company", Members = heroes.Select(a => a.Id).ToList(), SiteId = site.Def.Id };
        var fight = new Expedition.Venture.Showdown(v, site.Def, site.Def.Boss, Rules(town), site.Dungeon?.Rooms.LastOrDefault()?.Feature ?? "", unchecked((int)Voices.Hash("showdown-grid", town.Seed, town.Tick)));
        town.Duel = new Live { Party = party, Venture = v, Fight = fight, Site = site, Since = town.Tick, Staged = true };
        if (phase != "talk") fight.Attack();
        if (phase == "over") fight.Auto();
        return town.Duel;
    }

    public static ShowdownRules Rules(Town town) => town.Content.Showdown;

    /// <summary>Turn showdowns on for a campaign saved before they existed.</summary>
    public static void Enable(Town town) => town.ShowdownsOn = true;

    /// <summary>The showdown waiting on the mayor, if any.</summary>
    public static Live? Current(Town town) => town.Duel;

    /// <summary>Whether this company's meeting with the master is a showdown: a meaningful place, the dice, a mayor to see it.</summary>
    internal static bool Due(Town town, Party party, SiteState site, Expedition.Venture v)
    {
        var r = Rules(town);
        if (!town.Duels || town.Duel != null || party.Dueled || v.Terms != null) return false;
        var boss = site.Def.Boss;
        if (boss.Kind != "combat" || string.IsNullOrEmpty(boss.Monster)) return false;
        bool meaningful = Menaces.At(town, site.Def.Id) != null || site.Def.Group != "" || site.Def.Under != "" || site.Def.Tier >= r.MinTier;
        if (!meaningful) return false;
        return new Rng(unchecked((int)Voices.Hash("showdown", town.Seed, party.Id))).Chance(r.Chance);
    }

    /// <summary>The party has reached the master: set the showdown up and hold the trip until it is played.</summary>
    internal static void Begin(Town town, Party party, SiteState site, Expedition.Venture v, Roads.Arrival arrival)
    {
        string feature = v.DoorRoom?.Feature ?? "";
        var fight = new Expedition.Venture.Showdown(v, site.Def, site.Def.Boss, Rules(town), feature, unchecked((int)Voices.Hash("showdown-grid", town.Seed, party.Id)));
        town.Duel = new Live { Party = party, Venture = v, Fight = fight, Site = site, Arrival = arrival, Since = town.Tick };
        party.AtTheDoor = true;
        party.Dueled = true;
        party.LeaveTick = int.MaxValue;
        party.ReturnTick = int.MaxValue;
        foreach (var id in party.Members) town.Agent(id).Hero!.AwayUntil = int.MaxValue;
        town.Log(LedgerCategory.Quest, Importance.Notable, $"The {party.Name} stand before {Expedition.The(town.Content.Monster(site.Def.Boss.Monster))} in the heart of {site.Def.Name}.", [.. party.Members]);
    }

    /// <summary>Hourly: a showdown nobody plays for a day plays itself; one lost to a load goes in again off the grid; a finished one sends the party home.</summary>
    public static void Hourly(Town town)
    {
        if (town.Duel is { } live)
        {
            if (live.Fight.Phase != "over" && town.Tick - live.Since >= Rules(town).WaitHours) live.Fight.Auto();
            if (live.Fight.Phase == "over") Close(town);
            return;
        }
        foreach (var p in town.Parties)
        {
            if (!p.AtTheDoor || p.Returned) continue;
            // Loaded mid-showdown: the fight goes on off the grid, from the door.
            p.AtTheDoor = false;
            p.Resolved = false;
            Roads.Arrive(town, p);
        }
    }

    /// <summary>Play it all out at once with nobody commanded.</summary>
    public static void Auto(Town town)
    {
        if (town.Duel is not { } live) return;
        live.Fight.Auto();
        Close(town);
    }

    /// <summary>The fight is over: the boss room's outcome, the hoard, and the road home.</summary>
    internal static void Close(Town town)
    {
        if (town.Duel is not { } live || live.Fight.Phase != "over") return;
        town.Duel = null;
        if (live.Staged) return;
        var v = live.Venture;
        var f = live.Fight;
        var site = live.Site;
        live.Party.AtTheDoor = false;
        var outcome = v.AfterDoor(site.Def, f);
        Roads.Leave(town, live.Party, v, outcome, live.Arrival);
    }
}
