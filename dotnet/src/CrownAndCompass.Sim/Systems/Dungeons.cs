namespace CrownAndCompass.Sim.Systems;

/// <summary>A kind of room that tests the party (content/game/dungeons.json "features"): dark halls, chasms, flooding.</summary>
public sealed record FeatureDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public double Weight { get; init; } = 1;

    /// <summary>Habitats it turns up in; empty: anywhere.</summary>
    public List<string> Habitats { get; init; } = [];
    public string Text { get; init; } = "";

    /// <summary>The line when someone in the party has the answer ({name}).</summary>
    public string Answer { get; init; } = "";

    /// <summary>Who has the answer: a hero of one of these races, or of one of these classes at minLevel or more.</summary>
    public List<string> Races { get; init; } = [];
    public List<string> Classes { get; init; } = [];
    public int MinLevel { get; init; } = 1;

    /// <summary>Unanswered: to hit and armour for the room's fight, the foes moving first, hours lost.</summary>
    public int Attack { get; init; }
    public int Ac { get; init; }
    public bool MonstersFirst { get; init; }
    public double Hours { get; init; }

    /// <summary>Unanswered: every hero makes this check or takes the die (times the site's scale) in damage.</summary>
    public FeatureCheck? Check { get; init; }
}

public sealed record FeatureCheck
{
    public string Ability { get; init; } = "dex";
    public int Dc { get; init; } = 10;
    public int PerTier { get; init; }
    public int Die { get; init; } = 6;
}

public sealed record ReactionRules
{
    public int EveryDays { get; init; } = 6;
    public int Max { get; init; } = 3;
    public int TrapDc { get; init; } = 11;
    public int TrapDie { get; init; } = 8;
    public double HiddenShare { get; init; } = 0.5;
    public Dictionary<string, Dictionary<string, double>> Kinds { get; init; } = [];
    public Dictionary<string, string> Texts { get; init; } = [];
}

public sealed record DoorDef
{
    public double Weight { get; init; }
    public double Bold { get; init; }
    public int Min { get; init; }
    public double Mapped { get; init; }
    public double Underdog { get; init; }
    public int MaxParty { get; init; } = 99;
    public int MinParty { get; init; }
    public int MinInt { get; init; }
    public int Days { get; init; } = 2;
    public int Swell { get; init; }
    public double Sally { get; init; }
    public List<string> Kinds { get; init; } = [];
}

public sealed record ApproachRules
{
    public double Noise { get; init; } = 0.3;
    public DoorDef Storm { get; init; } = new() { Weight = 1 };
    public DoorDef Sneak { get; init; } = new();
    public DoorDef Parley { get; init; } = new();
    public DoorDef Siege { get; init; } = new();
    public Dictionary<string, string> Texts { get; init; } = [];
}

public sealed record DeepsRules
{
    public int MinTier { get; init; } = 2;
    public double Chance { get; init; }
    public int MaxDepth { get; init; } = 2;
    public double Loot { get; init; } = 1.5;
    public List<string> Habitats { get; init; } = [];
    public List<string> Names { get; init; } = [];
    public string Found { get; init; } = "";
}

public sealed record DungeonRules
{
    /// <summary>Side rooms off the main way, by tier (the last for any higher).</summary>
    public List<int> SideRooms { get; init; } = [];

    /// <summary>Share of hit points the party needs to look into a side room.</summary>
    public double SideHealthy { get; init; } = 0.6;

    /// <summary>A side room on an unmapped place is found with a Wisdom (Perception) check at this DC, plus perTier a tier.</summary>
    public int FindDc { get; init; } = 11;
    public int FindPerTier { get; init; } = 1;

    /// <summary>A cache room holds this share of the hoard's gold, and a find this often.</summary>
    public double[] CacheGold { get; init; } = [0.15, 0.3];
    public double CacheItem { get; init; }

    /// <summary>Hours to walk through a room already cleared.</summary>
    public double PassHours { get; init; } = 0.25;
    public double FeatureChance { get; init; }

    /// <summary>How much a company is drawn back to a place it has already got partway through (times the share cleared).</summary>
    public double Pull { get; init; }
    public List<FeatureDef> Features { get; init; } = [];
    public ReactionRules Reactions { get; init; } = new();
    public ApproachRules Approaches { get; init; } = new();
    public DeepsRules Deeps { get; init; } = new();

    public FeatureDef? Feature(string id) => id == "" ? null : Features.FirstOrDefault(f => f.Id == id);
}

/// <summary>One room of a site's map.</summary>
public sealed class DungeonRoom
{
    public int Id { get; init; }

    /// <summary>Its encounter: an index into the site's deck, <see cref="Dungeons.Boss"/> for the heart, <see cref="Dungeons.Cache"/> for a cache of the hoard.</summary>
    public int Enc { get; init; }

    /// <summary>The room it opens off (the one before it on the main way, or the main room a side room hangs from); -1 for the first.</summary>
    public int From { get; init; } = -1;
    public bool Side { get; init; }
    public string Feature { get; init; } = "";
    public bool Cleared { get; set; }

    /// <summary>The occupants took it back after a company cleared it.</summary>
    public bool Retaken { get; set; }

    /// <summary>A trap the occupants set in it since it was cleared.</summary>
    public bool Trapped { get; set; }
}

/// <summary>A site's map as the world has it: which rooms are cleared, and what the occupants did since the last company came.</summary>
public sealed class DungeonState
{
    public List<DungeonRoom> Rooms { get; set; } = [];

    /// <summary>The boss it was drawn for: a new master (resettled) draws the place afresh.</summary>
    public string Master { get; set; } = "";

    /// <summary>A company came home from it, or tracked their way in: the town knows its ways.</summary>
    public bool Mapped { get; set; }

    /// <summary>When a company was last inside.</summary>
    public int? VisitTick { get; set; }
    public int Trips { get; set; }

    /// <summary>The occupants hid the hoard: finding it takes a check at the heart.</summary>
    public bool Hidden { get; set; }
}

/// <summary>A room as a company met it on one delve, for the delve card: the room, the first and last lines of the tale about it, and how it went.</summary>
public sealed record RoomVisit(int Room, int From, int To, string Outcome);

/// <summary>A room of the map as it stood after a delve, for the delve card.</summary>
public sealed record RoomPlan(int Id, int From, bool Side, string Kind, string Feature, bool Cleared);

/// <summary>
/// Dungeon maps (content/game/dungeons.json): every site is a map of rooms drawn once on its own dice, a main way
/// to the heart with side rooms off it, some with features that test the party. Cleared rooms stay cleared between
/// trips and the occupants react to the time between; companies pick an approach at the door; a cleared site may
/// open its deeps, a harder site below it. The fights themselves stay in <see cref="Expedition"/>.
/// </summary>
public static class Dungeons
{
    public const int Boss = -1;
    public const int Cache = -2;

    public static DungeonRules Rules(Content c) => c.Dungeons;

    private static Rng Dice(Town town, SiteState s, string salt, int n = 0) =>
        new(unchecked((int)Voices.Hash("dungeon:" + salt + ":" + s.Def.Id, town.Seed, n)));

    /// <summary>The site's map, drawn the first time anyone needs it, and afresh when a new master moves in.</summary>
    public static DungeonState Map(Town town, SiteState s)
    {
        if (s.Dungeon is { } have && have.Master == s.Def.Boss.Monster) return have;
        var d = Draw(town.Content, s.Def, Dice(town, s, "map", s.Dungeon?.Trips ?? 0));
        s.Dungeon = d;
        return d;
    }

    /// <summary>Draw a map: the main way from the deck (as many rooms as a delve always had), side rooms from what is left or caches, then the heart.</summary>
    public static DungeonState Draw(Content c, SiteDef site, Rng dice)
    {
        var r = Rules(c);
        var d = new DungeonState { Master = site.Boss.Monster };
        var deck = Enumerable.Range(0, site.Encounters.Count).ToList();
        int tier = Math.Max(1, site.Tier);
        int main = Math.Min(deck.Count, 2 + tier);
        var mainIds = new List<int>();
        for (int i = 0; i < main; i++)
        {
            int k = dice.Range(0, deck.Count);
            var room = new DungeonRoom { Id = d.Rooms.Count, Enc = deck[k], From = mainIds.Count == 0 ? -1 : mainIds[^1], Feature = PickFeature(r, site, dice) };
            deck.RemoveAt(k);
            d.Rooms.Add(room);
            mainIds.Add(room.Id);
        }
        d.Rooms.Add(new DungeonRoom { Id = d.Rooms.Count, Enc = Boss, From = mainIds.Count == 0 ? -1 : mainIds[^1], Feature = PickFeature(r, site, dice) });
        int sides = r.SideRooms.Count == 0 ? 0 : r.SideRooms[Math.Min(tier, r.SideRooms.Count - 1)];
        for (int i = 0; i < sides && mainIds.Count > 0; i++)
        {
            int enc = Cache;
            // Leftover fights and puzzles first; when the deck is spent, a cache of the hoard.
            if (deck.Count > 0)
            {
                int k = dice.Range(0, deck.Count);
                enc = deck[k];
                deck.RemoveAt(k);
            }
            d.Rooms.Add(new DungeonRoom { Id = d.Rooms.Count, Enc = enc, From = mainIds[dice.Range(0, mainIds.Count)], Side = true, Feature = PickFeature(r, site, dice) });
        }
        return d;
    }

    private static string PickFeature(DungeonRules r, SiteDef site, Rng dice)
    {
        if (r.Features.Count == 0 || !dice.Chance(r.FeatureChance)) return "";
        var fits = r.Features.Where(f => f.Habitats.Count == 0 || f.Habitats.Any(site.Habitats.Contains)).ToList();
        if (fits.Count == 0) return "";
        double total = fits.Sum(f => f.Weight);
        double roll = dice.Next() * total;
        foreach (var f in fits)
        {
            if (roll < f.Weight) return f.Id;
            roll -= f.Weight;
        }
        return fits[^1].Id;
    }

    /// <summary>The rooms of the main way in order, ending at the heart.</summary>
    public static List<DungeonRoom> MainWay(DungeonState d) => d.Rooms.Where(x => !x.Side).ToList();

    public static List<DungeonRoom> SidesOf(DungeonState d, DungeonRoom room) => d.Rooms.Where(x => x.Side && x.From == room.Id).ToList();

    /// <summary>Share of the main way's rooms cleared, the heart aside.</summary>
    public static double Progress(DungeonState? d)
    {
        if (d == null) return 0;
        var way = d.Rooms.Where(x => !x.Side && x.Enc != Boss).ToList();
        return way.Count == 0 ? 0 : way.Count(x => x.Cleared) / (double)way.Count;
    }

    /// <summary>What draws a company back to a place it has got partway into: only what the town has heard of it.</summary>
    public static double Pull(Town town, SiteState s) => s.Dungeon is { Mapped: true } d ? Rules(town.Content).Pull * Progress(d) : 0;

    /// <summary>The kind of encounter a room holds, for the card.</summary>
    public static string KindOf(SiteDef site, DungeonRoom room) =>
        room.Enc == Boss ? "boss" : room.Enc == Cache ? "treasure" : room.Enc < site.Encounters.Count ? site.Encounters[room.Enc].Kind : "combat";

    /// <summary>A cache room's treasure: a share of the hoard's gold.</summary>
    public static EncounterDef CacheOf(Content c, SiteDef site)
    {
        var g = Rules(c).CacheGold;
        return new EncounterDef
        {
            Kind = "treasure",
            Gold = [(int)Math.Round(site.Loot[0] * g[0]), (int)Math.Round(site.Loot[^1] * g[^1])],
            MimicChance = 0.05,
            Text = "a side chamber where the occupants keep a cache",
        };
    }

    /// <summary>The map as it stands, for the delve card.</summary>
    public static List<RoomPlan> Plan(SiteDef site, DungeonState d) =>
        d.Rooms.Select(x => new RoomPlan(x.Id, x.From, x.Side, KindOf(site, x), x.Feature, x.Cleared)).ToList();

    // Between trips -------------------------------------------------------------------------------

    /// <summary>
    /// A company arrives: what the occupants did since the last one left (retaken rooms, new traps, friends called in,
    /// the hoard hidden), on the site's own dice. Returns the lines the party finds and the extra foes for every fight.
    /// </summary>
    public static (List<string> Lines, int Swell) React(Town town, SiteState s, int now)
    {
        var lines = new List<string>();
        var d = Map(town, s);
        var r = Rules(town.Content).Reactions;
        int swell = 0;
        int? last = d.VisitTick;
        d.VisitTick = now;
        d.Trips++;
        if (last is not int was || s.Cleared || !r.Kinds.TryGetValue(s.Def.Kind, out var kinds) || kinds.Count == 0) return (lines, swell);
        var weights = kinds.OrderBy(k => k.Key, StringComparer.Ordinal).ToList();
        int days = (now - was) / SimTime.HoursPerDay;
        int n = Math.Min(r.Max, days / Math.Max(1, r.EveryDays));
        var dice = Dice(town, s, "react", d.Trips);
        string they = Cap(Rumors.Occupants(town.Content, s.Def));
        for (int i = 0; i < n; i++)
        {
            double total = weights.Sum(w => w.Value);
            double roll = dice.Next() * total;
            string kind = weights[^1].Key;
            foreach (var (k, w) in weights)
            {
                if (roll < w)
                {
                    kind = k;
                    break;
                }
                roll -= w;
            }
            var cleared = MainWay(d).Where(x => x.Cleared && x.Enc != Boss).ToList();
            string? room = null;
            switch (kind)
            {
                case "regroup":
                    if (cleared.Count == 0) continue;
                    var back = cleared[^1];
                    back.Cleared = false;
                    back.Retaken = true;
                    room = Name(s.Def, back);
                    break;
                case "traps":
                    var open = cleared.Where(x => !x.Trapped).ToList();
                    if (open.Count == 0) continue;
                    var set = open[dice.Range(0, open.Count)];
                    set.Trapped = true;
                    room = Name(s.Def, set);
                    break;
                case "allies":
                    if (swell > 0) continue;
                    swell = 1;
                    break;
                case "hoard":
                    if (d.Hidden) continue;
                    d.Hidden = true;
                    break;
                default:
                    continue;
            }
            if (r.Texts.TryGetValue(kind, out var text))
                lines.Add(text.Replace("{they}", they, StringComparison.Ordinal).Replace("{room}", room ?? "", StringComparison.Ordinal));
        }
        return (lines, swell);
    }

    /// <summary>How the tale names a room: by what was in it.</summary>
    public static string Name(SiteDef site, DungeonRoom room)
    {
        var kind = KindOf(site, room);
        return kind switch
        {
            "boss" => "the heart of the place",
            "treasure" => "the treasure room",
            "trap" => "the trapped passage",
            "hazard" => "the bad stretch of passage",
            "puzzle" => "the sealed chamber",
            "parley" => "the guard post",
            _ => room.Id == 0 ? "the entrance hall" : "the guard room",
        };
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    // After a delve ------------------------------------------------------------------------------

    /// <summary>A party came home: the town knows the place's ways now, and a cleared site may open its deeps.</summary>
    public static void Home(Town town, Party party, SiteState s, ExpeditionResult result)
    {
        if (result.Outcome != ExpeditionOutcome.Wiped && s.Dungeon is { } d && !party.Withheld) d.Mapped = true;
        if (result.Outcome == ExpeditionOutcome.Cleared && !result.Talked && !result.Driven) Deeps(town, party, s);
    }

    /// <summary>Depth below the surface: 0 for an ordinary site, 1 for its deeps, and so on.</summary>
    public static int Depth(Town town, SiteState s)
    {
        int depth = 0;
        for (var up = s; up.Def.Under != "" && Quests.Site(town, up.Def.Under) is { } next && depth < 10; up = next) depth++;
        return depth;
    }

    /// <summary>The deeps below a site, if they have been found.</summary>
    public static SiteState? Below(Town town, SiteState s) => town.Sites.FirstOrDefault(x => x.Def.Under == s.Def.Id);

    /// <summary>Clearing a deep enough place may break into its deeps: a new site a tier harder below it, where the hoard always holds a find.</summary>
    private static void Deeps(Town town, Party party, SiteState s)
    {
        var r = Rules(town.Content).Deeps;
        if (!s.Generated || s.Def.Tier < r.MinTier || World.RegionOf(town, s) is not { } region || s.Def.Aloft) return;
        if (Undercity.IsUnder(town, s) || s.Def.Id == town.Content.Enemy.Outpost.Id || Menaces.At(town, s.Def.Id) != null) return;
        if (Depth(town, s) >= r.MaxDepth || Below(town, s) != null || r.Names.Count == 0) return;
        var dice = Dice(town, s, "deeps");
        if (!dice.Chance(r.Chance)) return;
        int tier = s.Def.Tier + 1;
        var kind = new SiteKindDef
        {
            Habitats = s.Def.Habitats.Concat(r.Habitats).Distinct().ToList(),
            Deck = s.Def.Encounters.Select(e => e.Kind).ToList(),
        };
        string name = dice.Pick(r.Names).Replace("{site}", Unthe(s.Def.Name), StringComparison.Ordinal);
        var def = World.BuildSite(town.Content, dice, region, kind, tier, s.Def.Id + "-deeps", name);
        def = def with
        {
            Under = s.Def.Id,
            Distance = s.Def.Distance,
            Loot = [(int)Math.Round(def.Loot[0] * r.Loot), (int)Math.Round(def.Loot[^1] * r.Loot)],
        };
        var deep = new SiteState(def)
        {
            RegionId = s.RegionId,
            Generated = true,
            Out = s.Out,
            Known = party.Withheld ? SiteKnowledge.Rumoured : SiteKnowledge.Confirmed,
            Found = true,
            FoundTick = town.Tick,
            HeardFrom = $"the {party.Name}",
            Believed = Rumors.Occupants(town.Content, def),
            BelievedTier = tier,
            Trust = party.Withheld ? 50 : 100,
        };
        town.Sites.Add(deep);
        town.Log(LedgerCategory.Quest, Importance.Notable,
            r.Found.Replace("{site}", s.Def.Name, StringComparison.Ordinal).Replace("{party}", party.Name, StringComparison.Ordinal),
            party.Members.ToList());
    }

    private static string Unthe(string name) => name.StartsWith("the ", StringComparison.OrdinalIgnoreCase) ? name[4..] : name;
}
