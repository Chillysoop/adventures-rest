namespace CrownAndCompass.Sim.Systems;

public sealed record ShipDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public double Cost { get; init; }
    public double Timber { get; init; }
    public int Crew { get; init; }
    public double Hold { get; init; }

    /// <summary>Days at sea for each day out the port's region lies.</summary>
    public double Speed { get; init; } = 1;
    public double Hull { get; init; }
    public double Wear { get; init; }
    public double Repair { get; init; } = 1;
    public double Fish { get; init; }
    public double Fight { get; init; }

    /// <summary>The town tier it needs, if any.</summary>
    public string Tier { get; init; } = "";

    /// <summary>"sea" for the harbour's ships, "sky" for the Sky Dock's airships (<see cref="Sky"/>).</summary>
    public string Realm { get; init; } = "sea";

    /// <summary>An airship's days of lift-crystal at a full charge, and gold a day to refill it.</summary>
    public double Lift { get; init; }
    public double Recharge { get; init; }

    /// <summary>An airship's ballistae, its edge in the approach, the heroes it carries into a boarding, and the share of storms it climbs above.</summary>
    public double Guns { get; init; }
    public double Agility { get; init; }
    public int Marines { get; init; }
    public double Ceiling { get; init; }

    /// <summary>Shallow enough for the river: a river harbour builds only these.</summary>
    public bool River { get; init; }
}

public sealed record CorsairRules
{
    public string Name { get; init; } = "the corsairs";
    public double Chance { get; init; } = 0.12;
    public double PerRing { get; init; } = 0.05;
    public double Max { get; init; } = 0.45;
    public double Strength { get; init; } = 3;
    public double StrengthPerRing { get; init; } = 2;
    public double Loot { get; init; } = 0.6;
    public double Hurt { get; init; } = 25;
    public double Prize { get; init; } = 30;
    public int RaidEvery { get; init; } = 20;
    public double RaidChance { get; init; } = 0.35;
    public double RaidStrength { get; init; } = 5;
    public double RaidGold { get; init; } = 60;
    public double RaidFood { get; init; } = 30;

    /// <summary>What each watchman adds to the harbour's fight.</summary>
    public double WatchFight { get; init; } = 0.5;

    /// <summary>
    /// The corsair fleet is the group in groups.json (`group`): its holds (sites of the group not cleared) are where the raids
    /// come from. With none left the harbour is not raided and only `stray` of the ambushes at sea remain. Each hold standing
    /// adds `perHold` to a raid and `grow` a day to the corsairs' wrath (at most `most`), which a raid that lands halves; a hold
    /// burned scatters `scatter` of the wrath, and while others stand the rest come back for revenge within `revengeDays`
    /// with `revenge` more of it. `lords` names the captain who sails over them all.
    /// </summary>
    public string Group { get; init; } = "corsair-fleet";
    public double Stray { get; init; } = 0.3;
    public double PerHold { get; init; } = 1.5;
    public double Grow { get; init; } = 0.01;
    public double Most { get; init; } = 10;
    public double Scatter { get; init; } = 0.5;
    public double Revenge { get; init; } = 3;
    public int RevengeDays { get; init; } = 6;
    public IReadOnlyList<string> Lords { get; init; } = [];
    public IReadOnlyList<string> Burned { get; init; } = [];
    public IReadOnlyList<string> Swept { get; init; } = [];
    public IReadOnlyList<string> Back { get; init; } = [];
    public IReadOnlyList<string> Win { get; init; } = [];
    public IReadOnlyList<string> Lose { get; init; } = [];
    public IReadOnlyList<string> Sunk { get; init; } = [];
    public IReadOnlyList<string> Repelled { get; init; } = [];
    public IReadOnlyList<string> Raided { get; init; } = [];
}

/// <summary>content/game/sea.json.</summary>
public sealed record FleetRules
{
    public double Ration { get; init; } = 0.25;
    public double MinStanding { get; init; } = -10;
    /// <summary>Degrees past the ocean's edge a land region's own arc may end and still have a shore on it.</summary>
    public double Shore { get; init; } = 0;
    public IReadOnlyList<string> CoastThemes { get; init; } = [];

    /// <summary>Countries that never have a harbour however near the sea they lie (mountain holds, the deep roads).</summary>
    public IReadOnlyList<string> InlandThemes { get; init; } = [];
    public int Turnaround { get; init; } = 1;
    public double Learn { get; init; } = 0.06;
    public double ProwessFish { get; init; } = 0.1;
    public double ProwessFight { get; init; } = 1;
    public double WinterFish { get; init; } = 0.5;

    /// <summary>The chance a captain goes down with her ship, and the prowess a new captain loses against the old.</summary>
    public double Drown { get; init; } = 0.5;
    public double MateLoss { get; init; } = 1;

    /// <summary>Below this share of her hands at the harbour (townsfolk who work there, <see cref="Fleet.Manned"/>) a ship won't sail.</summary>
    public double ManMost { get; init; } = 0.5;
    public double PatchBelow { get; init; } = 0.5;
    public int StandInEvery { get; init; } = 10;
    public double StandInProfit { get; init; } = 40;
    public IReadOnlyList<ShipDef> Ships { get; init; } = [];
    public IReadOnlyList<string> Names { get; init; } = [];
    public IReadOnlyList<string> Captains { get; init; } = [];
    public IReadOnlyList<string> Prowess { get; init; } = [];
    public CorsairRules Corsairs { get; init; } = new();

    /// <summary>What a ship meets at sea, by ring (fought by <see cref="ShipFight"/>), and how much stronger a ring out.</summary>
    public IReadOnlyList<SkyFoeDef> Foes { get; init; } = [];
    public double PowerPerRing { get; init; } = 3;
    public GuardRules Guards { get; init; } = new();
    public VoyageRules Voyages { get; init; } = new();
    public HarbourParts Harbour { get; init; } = new();
    public RiverHarbourRules River { get; init; } = new();

    public ShipDef? Ship(string id) => Ships.FirstOrDefault(s => s.Id == id);
}

/// <summary>Who fills a ship's marine places the heroes leave (sea.json "guards").</summary>
/// <summary>
/// What the harbour's parts do (sea.json `harbour`, the parts in buildings.json with `partOf: harbour`): the harbour berths
/// `berths` ships and each quay `perQuay` more; past that, up to `anchorage` more lie at anchor outside, rowing their cargo
/// in (`anchorLoad` of a trade run's sale) and patched only at a berth. The shipyard builds for less, the trading house sells
/// for more, the lighthouse steadies the navigator and keeps corsairs off, the naval barracks fights raiders, and the dry dock
/// patches for less and mends moored hulls a little each day.
/// </summary>
public sealed record HarbourParts
{
    public int Berths { get; init; } = 2;
    public int PerQuay { get; init; } = 2;
    public int Anchorage { get; init; } = 2;
    public double AnchorLoad { get; init; } = 0.8;
    public IReadOnlyList<string> Quays { get; init; } = [];
    public string Shipyard { get; init; } = "";
    public double YardCost { get; init; } = 0.8;
    public string TradingHouse { get; init; } = "";
    public double Trading { get; init; } = 1.2;
    public string Lighthouse { get; init; } = "";
    public int Light { get; init; } = 2;
    public double LightCorsairs { get; init; } = 0.7;
    public string Barracks { get; init; } = "";
    public double BarracksFight { get; init; } = 6;
    public string DryDock { get; init; } = "";
    public double DockRepair { get; init; } = 0.6;
    public double DockMend { get; init; } = 2;

    /// <summary>The harbourmaster's choice of what the quay is for first: the fishing fleet's catch, the merchants' sale, the guard boat's fight against corsair raids.</summary>
    public double Fishers { get; init; } = 1.4;

    /// <summary>Foreign hulls at the quay: dues a day for each allied port on the coast, at most `foreignBerth` a berth (doubled by the merchants' quay).</summary>
    public double Foreign { get; init; } = 1.5;
    public double ForeignBerth { get; init; } = 4;
    public double Merchants { get; init; } = 1.15;
    public double GuardBoat { get; init; } = 6;
}

/// <summary>sea.json `river`: the river harbour's building, how far (days) from the water a port may lie, and its catch.</summary>
public sealed record RiverHarbourRules
{
    public string Building { get; init; } = "river_harbour";
    public double Reach { get; init; } = 1.2;
    public double Fish { get; init; } = 0.6;
}

public sealed record GuardRules
{
    public GuardDef Watch { get; init; } = new();
    public GuardDef Hired { get; init; } = new();
}

public sealed record GuardDef
{
    public double Fight { get; init; }
    public double Wage { get; init; }
}

/// <summary>How the sea's jobs and passages go (sea.json "voyages", <see cref="Voyages"/>).</summary>
public sealed record VoyageRules
{
    /// <summary>The navigator's roll: d20 + the captain's prowess and the best Survival aboard against this.</summary>
    public int NavigateDc { get; init; } = 12;

    /// <summary>Days at sea times this on a crit, a pass, a miss and a fumble.</summary>
    public double[] Navigate { get; init; } = [0.7, 0.85, 1.15, 1.4];

    /// <summary>How much a season's storms (sky.json "storm") slow a ship: days times 1 + this times the season's storm chance.</summary>
    public double Weather { get; init; } = 1.5;
    public double Meet { get; init; } = 0.08;
    public double MeetPerRing { get; init; } = 0.03;
    public double MeetMax { get; init; } = 0.4;

    /// <summary>Hours sooner corsairs are seen and the share of their raids turned away for each ship on patrol.</summary>
    public double PatrolGuard { get; init; } = 0.35;
    public int PatrolDays { get; init; } = 6;

    /// <summary>A shore party's share of a sea site's strength taken by each gun's day in reach, at most StrikeMost.</summary>
    public double StrikeShare { get; init; } = 0.05;
    public double StrikeMost { get; init; } = 0.4;

    /// <summary>Gold a company that sailed in the town's own ship brings home in its hold, per ring of the site, when it won.</summary>
    public double Hold { get; init; } = 40;
    public Dictionary<string, List<string>> Texts { get; init; } = [];
}

/// <summary>A trade run at sea: where, with what, and when it is due home.</summary>
public sealed class Voyage
{
    public int SettlementId { get; init; }
    public Good Cargo { get; init; }
    public double Qty { get; init; }
    public int Days { get; init; }
    public int BackDay { get; init; }
}

/// <summary>One of the town's ships: its kind, its captain and how good they are, and its hull.</summary>
public sealed class Ship
{
    public int Id { get; init; }
    public string Kind { get; init; } = "";
    public string Name { get; init; } = "";
    public string Captain { get; set; } = "";

    /// <summary>The townsperson who captains her, if one does (<see cref="Fleet.Captains"/>); null for a captain from away.</summary>
    public int? CaptainId { get; set; }

    /// <summary>1 green to 5 legendary.</summary>
    public double Prowess { get; set; } = 1;
    public double Hull { get; set; }
    public Voyage? Out { get; set; }
    public int Voyages { get; set; }
    public double Earned { get; set; }
    public double Fished { get; set; }
    public string LastLine { get; set; } = "";

    /// <summary>An airship's lift-crystal left, in days aloft (<see cref="Sky"/>).</summary>
    public double Lift { get; set; }

    /// <summary>An airship out on a job.</summary>
    public SkyFlight? Flight { get; set; }

    /// <summary>Where an airship came down with its lift spent, and the day it did: it waits there for salvage.</summary>
    public int? DownedRegion { get; set; }
    public int DownedDay { get; set; }

    /// <summary>Riggers out to a downed airship are due back with it on this day.</summary>
    public int? SalvageDay { get; set; }
    public int Fights { get; set; }
    public int Won { get; set; }

    /// <summary>A standing run: the port this ship sails to again each time it is home, with the cargo that pays best then; null for none.</summary>
    public int? Route { get; set; }

    /// <summary>Who stands at the rail with the crew in a sea ship's free marine places: "" nobody, "watch" lent watchmen, "hired" sellsword marines (<see cref="Voyages"/>).</summary>
    public string Guard { get; set; } = "";
}

public sealed class FleetState
{
    public int NextId { get; set; }
    public int NextRaidDay { get; set; }
    public int NextStandInDay { get; set; }
    public bool Granted { get; set; }

    /// <summary>The corsair holds standing when last counted, and whether they have been counted yet (<see cref="Fleet.Corsairs"/>).</summary>
    public int Holds { get; set; }
    public bool Counted { get; set; }

    /// <summary>How hard the corsairs want the town: it grows while their holds stand and is spent by a raid that lands.</summary>
    public double Wrath { get; set; }
}

/// <summary>
/// The town's ships, for a town founded on the sea (Q8, Q53, Q79): once the Harbour stands, boats fish while moored,
/// sail trade runs to the ports along the coast (selling like the airship, the same prices and gluts), wear at sea and are
/// patched at the harbour. Captains grow from green to legendary. Corsairs meet runs on the way home and now and then
/// raid the harbour. Towns away from the sea have no fleet and nothing here touches their dice.
/// </summary>
public static class Fleet
{
    public static FleetRules Rules(Town town) => town.Content.Sea;

    public static ShipDef Def(Town town, Ship s) => Kind(town, s.Kind) ?? throw new KeyNotFoundException($"Unknown ship {s.Kind}");

    /// <summary>A ship kind of either realm: the harbour's (sea.json) or the Sky Dock's (sky.json).</summary>
    public static ShipDef? Kind(Town town, string kind) => Rules(town).Ship(kind) ?? town.Content.Sky.Ships.FirstOrDefault(d => d.Id == kind);

    public static bool Aloft(Town town, Ship s) => Def(town, s).Realm == "sky";

    /// <summary>The town's sea ships, in id order.</summary>
    public static IEnumerable<Ship> AtSea(Town town) => town.Ships.Where(s => !Aloft(town, s)).OrderBy(s => s.Id);

    /// <summary>The town's harbour, standing: needed for any ship.</summary>
    public static Building? Harbour(Town town) => town.Buildings.FirstOrDefault(b => (b.Def.Id == "harbour" || b.Def.Id == Rules(town).River.Building) && b.Complete && !b.Closed);

    /// <summary>A town off the sea keeps its boats at a river harbour: river hulls only, ports along the water, the open sea only where the river reaches it.</summary>
    public static bool OnRiver(Town town) => !Coastal(town) && Harbour(town) is { } h && h.Def.Id == Rules(town).River.Building;

    /// <summary>Whether the harbour's ships can reach the open sea: from the coast, or down a river that runs to it.</summary>
    public static bool ToSea(Town town) => Coastal(town) || OnRiver(town) && Passage.Mouth(town);

    /// <summary>Whether a part of the harbour stands (buildings.json `partOf: harbour`).</summary>
    public static bool Has(Town town, string partId) =>
        partId != "" && Harbour(town) is { } h && town.Buildings.Any(b => b.ParentId == h.Id && b.Def.Id == partId && b.Complete && !b.Closed);

    private static HarbourParts Parts(Town town) => Rules(town).Harbour;

    /// <summary>What the harbourmaster gives the quay to first ("fishers", "merchants", "guard"), or "" before the choice.</summary>
    public static string Quay(Town town) =>
        Institutions.Find(town, "harbour") is { } h && Institutions.Chosen(town, h) is { } o ? o.Id : "";

    /// <summary>Ships the harbour can berth: its own and each quay's.</summary>
    public static int Berths(Town town) => Parts(town).Berths + Parts(town).PerQuay * Parts(town).Quays.Count(q => Has(town, q));

    /// <summary>A ship past the harbour's berths lies at anchor outside: the last built go there first.</summary>
    public static bool AtAnchor(Town town, Ship s) =>
        !Aloft(town, s) && AtSea(town).OrderBy(x => x.Id).TakeWhile(x => x != s).Count() >= Berths(town);

    public static bool Coastal(Town town) => town.Founding != null && Founding.Of(town).Sea != null;

    // ---- The corsairs ----

    /// <summary>The corsair fleet's holds still standing (groups.json, sea.json `corsairs.group`), nearest first.</summary>
    public static List<SiteState> Holds(Town town) =>
        town.Sites.Where(x => x.Def.Group == Rules(town).Corsairs.Group && !x.Cleared)
            .OrderBy(x => x.RegionId is int r ? town.Regions[r].Ring : 99).ThenBy(x => x.Def.Id, StringComparer.Ordinal).ToList();

    /// <summary>The captain the corsairs sail under, a pure function of the seed.</summary>
    public static string Lord(Town town)
    {
        var l = Rules(town).Corsairs.Lords;
        return l.Count == 0 ? "the corsair king" : l[(int)(World.Hash(town.Seed * 151 + 9) * l.Count)];
    }

    /// <summary>How strong the corsairs come at the harbour: the raid's own strength, each hold standing and their wrath.</summary>
    public static double CorsairPower(Town town) =>
        Rules(town).Corsairs.RaidStrength + Rules(town).Corsairs.PerHold * Holds(town).Count + town.FleetState.Wrath;

    /// <summary>Daily: count the holds; one burned scatters them (and the rest want revenge), one retaken puts black sails back on the sea.</summary>
    private static void Corsairs(Town town)
    {
        var c = Rules(town).Corsairs;
        var st = town.FleetState;
        var holds = Holds(town);
        int live = holds.Count;
        if (st.Counted && live < st.Holds)
        {
            st.Wrath *= 1 - c.Scatter;
            if (live == 0)
                town.Log(LedgerCategory.War, Importance.Major, Say(town, c.Swept, ""));
            else
            {
                st.Wrath = Math.Min(c.Most, st.Wrath + c.Revenge);
                st.NextRaidDay = Math.Min(st.NextRaidDay, town.Day + c.RevengeDays);
                town.Log(LedgerCategory.War, Importance.Notable, Say(town, c.Burned, holds[0].Def.Name));
            }
        }
        else if (st.Counted && live > st.Holds && holds.Count > 0)
            town.Log(LedgerCategory.War, Importance.Notable, Say(town, c.Back, holds[0].Def.Name));
        st.Holds = live;
        st.Counted = true;
        st.Wrath = Math.Min(c.Most, st.Wrath + c.Grow * live);
    }

    private static string Say(Town town, IReadOnlyList<string> texts, string hold) => Cap(
        (texts.Count == 0 ? "" : texts[(int)(World.Hash(town.Seed * 31 + town.Day * 7 + 1) * texts.Count)])
            .Replace("{lord}", Lord(town), StringComparison.Ordinal).Replace("{hold}", hold, StringComparison.Ordinal));

    public static string ProwessName(Town town, Ship s)
    {
        var p = Rules(town).Prowess;
        return p.Count == 0 ? "" : p[Math.Clamp((int)Math.Floor(s.Prowess) - 1, 0, p.Count - 1)];
    }

    // ---- Building ships ----

    public static string? BuildBlocked(Town town, string kind)
    {
        if (Kind(town, kind) is { Realm: "sky" }) return Sky.BuildBlocked(town, kind);
        if (Harbour(town) == null) return Coastal(town) ? "needs a harbour" : "needs a river harbour";
        if (OnRiver(town) && Rules(town).Ship(kind) is { River: false }) return "too big for the river";
        if (AtSea(town).Count() >= Berths(town) + Parts(town).Anchorage) return "the harbour and the anchorage are full: add a quay";
        if (Rules(town).Ship(kind) is not { } d) return "no such ship";
        if (d.Tier != "" && !City.Reached(town, d.Tier)) return $"needs the town to be a {City.TierName(town, d.Tier).ToLowerInvariant()}";
        if (town.Treasury < Cost(town, d)) return "not enough gold";
        return null;
    }

    /// <summary>Gold for a new ship and its timber at today's market.</summary>
    public static double Cost(Town town, ShipDef d) => Js.Round(d.Cost * Yard(town, d) + town.Market.Quote(Good.Timber, d.Timber));

    /// <summary>The shipyard builds sea ships for less.</summary>
    private static double Yard(Town town, ShipDef d) => d.Realm != "sky" && Has(town, Parts(town).Shipyard) ? Parts(town).YardCost : 1;

    public static Ship? Build(Town town, string kind, bool free = false)
    {
        if (!free && BuildBlocked(town, kind) != null) return null;
        var r = Rules(town);
        var d = Kind(town, kind)!;
        bool sky = d.Realm == "sky";
        if (!free && Stewards.Intercept(town, "fleet", new StewardAsk { Area = "fleet", Kind = "ship", Title = $"Build a {d.Name} · {Js.Str(Cost(town, d))}g", Why = sky ? "• Another airship for the Sky Dock" : "• Another hull for the harbour", S1 = d.Id, Cost = Cost(town, d) }))
            return null;
        if (!free)
        {
            double cost = d.Cost * Yard(town, d) + town.Market.Buy(Good.Timber, d.Timber);
            town.Book(TreasuryAccount.Works, -cost);
        }
        var st = town.FleetState;
        int id = st.NextId++;
        var used = town.Ships.Select(s => s.Name).ToHashSet();
        var pool = sky ? town.Content.Sky.Names : r.Names;
        var captains = sky ? town.Content.Sky.Captains : r.Captains;
        var names = pool.Where(n => !used.Contains(n)).ToList();
        string name = names.Count > 0 ? names[(int)(World.Hash(town.Seed * 131 + id * 17 + 3) * names.Count)] : $"the {d.Name} {id + 1}";
        string captain = captains.Count > 0 ? captains[(int)(World.Hash(town.Seed * 137 + id * 19 + 5) * captains.Count)] : "the mate";
        var ship = new Ship { Id = id, Kind = d.Id, Name = name, Captain = captain, Hull = d.Hull, Lift = d.Lift };
        if (!sky && Skipper(town) is { } who)
        {
            ship.CaptainId = who.Id;
            ship.Captain = who.Name;
        }
        town.Ships.Add(ship);
        if (!sky) Hands(town);
        town.Log(LedgerCategory.Town, Importance.Notable,
            sky ? free ? $"The Sky Dock's first airship, {name}, rises on her mast. {captain} will fly her."
                       : $"A new {d.Name}, {name}, is warped out of the gasbag shed, with {captain} for her captain."
            : free ? $"The harbour's first boat, {name}, is pushed down the slip. {ship.Captain} will skipper her."
                 : $"A new {d.Name}, {name}, slides down the harbour slip, with {ship.Captain} for her captain.", ship.CaptainId is int cid ? [cid] : null);
        return ship;
    }

    // ---- Hands and captains ----

    /// <summary>
    /// The townsperson best fit to captain a new ship: a hand already at the harbour first, then the wisest and nimblest
    /// grown townsfolk who are not heroes, travellers, councillors or captains already. No dice.
    /// </summary>
    public static Agent? Skipper(Town town)
    {
        var taken = town.Ships.Where(x => x.CaptainId != null).Select(x => x.CaptainId!.Value).ToHashSet();
        var seated = town.Seats.Where(x => x.HolderId != null).Select(x => x.HolderId!.Value).ToHashSet();
        int? harbour = Harbour(town)?.Id;
        return town.Agents.Where(a => !a.Dead && !a.Departed && !a.Traveller && a.Hero == null && a.Age >= 20 && !taken.Contains(a.Id) && !seated.Contains(a.Id))
            .OrderByDescending(a => harbour != null && a.JobId == harbour ? 1 : 0)
            .ThenByDescending(a => a.Abilities.Wis + a.Abilities.Dex).ThenBy(a => a.Id).FirstOrDefault();
    }

    /// <summary>The hands the town's ships need: their crews, taken on at the harbour as townsfolk (<see cref="Building.Crew"/>).</summary>
    public static int CrewNeeded(Town town) => AtSea(town).Sum(s => Def(town, s).Crew);

    /// <summary>The share of the ships' hands the harbour has: below one, the boats fish less, and below `manMost` none sails.</summary>
    public static double Manned(Town town)
    {
        int need = CrewNeeded(town);
        return need == 0 || Harbour(town) is not { } h ? 1 : Math.Min(1, h.Workers.Count / (double)need);
    }

    /// <summary>
    /// Daily, and when a ship is laid down: the harbour takes on as many hands as the ships need (the idle first, in id
    /// order) and lets the rest go; a captain dead or gone is followed by the mate.
    /// </summary>
    public static void Hands(Town town)
    {
        if (Harbour(town) is not { } h) return;
        int need = CrewNeeded(town);
        h.Crew = need;
        foreach (var a in town.Workers.Where(a => a.JobId == null).OrderBy(a => a.Id))
        {
            if (h.Workers.Count >= need) break;
            a.JobId = h.Id;
            h.Workers.Add(a.Id);
        }
        while (h.Workers.Count > need)
        {
            int id = h.Workers[^1];
            h.Workers.RemoveAt(h.Workers.Count - 1);
            town.Agent(id).JobId = null;
        }
        foreach (var s in AtSea(town).Where(s => s.CaptainId is int c && (town.Agent(c).Dead || town.Agent(c).Departed)).ToList())
        {
            string was = s.Captain;
            s.CaptainId = null;
            s.Prowess = Math.Max(1, s.Prowess - Rules(town).MateLoss);
            if (Skipper(town) is { } who)
            {
                s.CaptainId = who.Id;
                s.Captain = who.Name;
            }
            else s.Captain = "the mate";
            town.Log(LedgerCategory.Town, Importance.Notable, $"{Cap(s.Name)} has lost {was}. {Cap(s.Captain)} takes her helm, greener than the old hand.", s.CaptainId is int cid ? [cid] : null);
        }
    }

    /// <summary>A ship gone down: her captain may go with her (on the ship's own dice); the rest of the crew come home in the boats.</summary>
    public static void Sink(Town town, Ship s, Rng rng)
    {
        town.Ships.Remove(s);
        if (s.CaptainId is int c && !town.Agent(c).Dead && !town.Agent(c).Departed && rng.Chance(Rules(town).Drown))
        {
            var a = town.Agent(c);
            Threat.Kill(town, a);
            town.Log(LedgerCategory.Hardship, Importance.Notable, $"{a.Name} goes down with {s.Name}, the last aboard.", [a.Id]);
        }
    }

    // ---- Repairs ----

    public static double RepairCost(Town town, Ship s) =>
        Js.Round((Def(town, s).Hull - s.Hull) * Def(town, s).Repair * (Aloft(town, s) ? Sky.RiggerShare(town) : Has(town, Parts(town).DryDock) ? Parts(town).DockRepair : 1));

    public static bool Repair(Town town, int shipId)
    {
        var s = town.Ships.FirstOrDefault(x => x.Id == shipId);
        if (s == null || s.Out != null || s.Flight != null || s.DownedRegion != null || s.Hull >= Def(town, s).Hull || (Aloft(town, s) ? Sky.Dock(town) : Harbour(town)) == null || AtAnchor(town, s)) return false;
        double cost = RepairCost(town, s);
        if (town.Treasury < cost) return false;
        if (Stewards.Intercept(town, "fleet", new StewardAsk { Area = "fleet", Kind = "patch", Title = $"Patch {s.Name} · {Js.Str(cost)}g", Why = $"• Hull {Js.Str(Js.Round(s.Hull))} of {Js.Str(Def(town, s).Hull)}", I1 = s.Id, Cost = cost })) return false;
        town.Book(TreasuryAccount.Works, -cost);
        s.Hull = Def(town, s).Hull;
        town.Log(LedgerCategory.Town, Importance.Routine, $"{(Aloft(town, s) ? "Riggers" : "Shipwrights")} patch {s.Name}'s hull ({Js.Str(cost)} gold).");
        return true;
    }

    // ---- Trade runs ----

    /// <summary>Ports a ship can sail to: known, not hostile, and along the coast.</summary>
    public static IEnumerable<Settlement> Ports(Town town)
    {
        var r = Rules(town);
        if (OnRiver(town))
        {
            var line = Passage.River(town);
            return town.Settlements.Where(s => !s.Razed && town.Regions[s.RegionId].Known && s.Standing >= r.MinStanding
                && (Passage.Mouth(town) && OnCoast(town, town.Regions[s.RegionId])
                    || line.Min(p => Math.Abs(p.X - Spot(town, s).X) + Math.Abs(p.Y - Spot(town, s).Y)) <= r.River.Reach)).OrderBy(s => s.Id);
        }
        if (!Coastal(town)) return [];
        return town.Settlements.Where(s => !s.Razed && town.Regions[s.RegionId].Known && s.Standing >= r.MinStanding && OnCoast(town, town.Regions[s.RegionId])).OrderBy(s => s.Id);
    }

    /// <summary>Why the town can't trade over the sea with a people out on it (no harbour ship or airship to carry the goods), or null.</summary>
    public static string? Overseas(Town town, Settlement s) =>
        !World.Sea(town.Regions[s.RegionId]) || Harbour(town) != null && ToSea(town) || Sky.Dock(town) != null ? null : "over the sea: needs a harbour or a Sky Dock";

    /// <summary>
    /// Whether a land has a shore a ship can tie up at: the sea's own lands, coast country, or a land whose arc reaches the
    /// ocean's sector (not mountain holds or the deep roads, however near).
    /// </summary>
    public static bool OnCoast(Town town, Region g)
    {
        var r = Rules(town);
        if (g.Theme.Sea || r.CoastThemes.Contains(g.Theme.Id) || g.Theme.Habitats.Contains("coast")) return true;
        if (r.InlandThemes.Contains(g.Theme.Id) || town.OceanArc <= 0) return false;
        return Math.Abs(World.AngleBetween(g.Bearing, town.OceanBearing)) - town.OceanArc / 2 <= g.Arc / 2 + r.Shore;
    }

    private static (double X, double Y) Spot(Town town, Settlement s)
    {
        var g = town.Regions[s.RegionId];
        double b = g.Bearing * Math.PI / 180;
        return (g.Days * Math.Sin(b), -g.Days * Math.Cos(b));
    }

    /// <summary>Days there and back at the ship's own speed in fair weather: what the captain plans for.</summary>
    public static int Days(Town town, Ship s, Settlement port) => Days(town, port, Def(town, s).Speed);

    private static int Days(Town town, Settlement port, double pace) =>
        Math.Max(2, 2 * (int)Math.Ceiling(town.Regions[port.RegionId].Days * pace - 1e-9) + Rules(town).Turnaround);

    /// <summary>Days the run really takes: the ship's speed, the navigator's roll, the weather over the port's waters and the wind on the heading (<see cref="Voyages.SailPace"/>).</summary>
    public static int Sailed(Town town, Ship s, Settlement port, Rng rng)
    {
        var g = town.Regions[port.RegionId];
        return Days(town, port, Voyages.SailPace(town, s, 0, rng, g, g.Bearing));
    }

    /// <summary>Food the crew eats on a run there and back.</summary>
    public static double Rations(Town town, Ship s, Settlement port) => Js.Round(Def(town, s).Crew * Days(town, s, port) * Rules(town).Ration * 10) / 10;

    public static double Spare(Town town, Ship s, Good g)
    {
        var m = town.Market;
        double keep = Math.Max(m.Target(g) * town.Content.Trade.Sky.KeepShare, m.Held(g)) + (g == Good.Food ? 0 : 0);
        return Math.Max(0, Math.Min(Def(town, s).Hold, m.Stock(g) - keep));
    }

    public static string? Refuse(Town town, Ship s, Settlement port, Good cargo)
    {
        var r = Rules(town);
        if (Aloft(town, s)) return "an airship trades from the Sky Dock";
        if (Harbour(town) == null) return "needs a harbour";
        if (s.Out != null || s.Flight != null || Voyages.Carrying(town, s) != null) return "at sea";
        if (Def(town, s).Hold <= 0) return "no hold";
        if (s.Hull < Def(town, s).Hull * 0.25) return "needs patching first";
        if (Manned(town) < r.ManMost) return "not enough hands at the harbour";
        if (!Ports(town).Contains(port)) return "not a port the ship can reach";
        if (town.Market.Stock(Good.Food) < Rations(town, s, port) + (cargo == Good.Food ? Spare(town, s, cargo) : 0)) return "not enough food for the crew";
        if (Spare(town, s, cargo) < town.Content.Trade.Sky.MinCargo) return $"no {Lower(cargo)} to spare";
        return null;
    }

    /// <summary>Gold the run should clear, before corsairs.</summary>
    public static double Profit(Town town, Ship s, Settlement port, Good cargo) =>
        Skyship.Quote(town, port, cargo, Spare(town, s, cargo)) - Rations(town, s, port) * town.Market.BasePrice(Good.Food);

    public static bool Send(Town town, int shipId, int settlementId, Good cargo)
    {
        var s = town.Ships.FirstOrDefault(x => x.Id == shipId);
        if (s == null || settlementId < 0 || settlementId >= town.Settlements.Count) return false;
        var port = town.Settlements[settlementId];
        if (Refuse(town, s, port, cargo) != null) return false;
        if (Stewards.Intercept(town, "fleet", new StewardAsk { Area = "fleet", Kind = "run", Title = $"Sail {s.Name} to {port.Name} with {Lower(cargo)}", Why = $"• About {Js.Str(Js.Round(Profit(town, s, port, cargo)))} gold", I1 = s.Id, I2 = port.Id, S1 = cargo.ToString() })) return false;
        double qty = Spare(town, s, cargo), food = Rations(town, s, port);
        town.Market.Seed(cargo, -qty);
        town.Market.Seed(Good.Food, -food);
        int plan = Days(town, s, port), days = Sailed(town, s, port, Dice(town, 31 + s.Id));
        s.Out = new Voyage { SettlementId = port.Id, Cargo = cargo, Qty = qty, Days = days, BackDay = town.Day + days };
        town.Log(LedgerCategory.Town, Importance.Notable,
            $"{Cap(s.Name)} sails for {port.Name} with {Js.Str(Js.Round(qty))} {Lower(cargo)} in the hold and {Js.Str(Js.Round(food))} food for the crew. Home in {days} days{(days > plan ? ", the weather against her" : days < plan ? ", a fair wind behind her" : "")}.");
        return true;
    }

    /// <summary>Daily, from <see cref="Trade.Daily"/>: the harbour's first boat, fishing, ships home, corsairs, and the stand-in.</summary>
    public static void Daily(Town town, bool autoMayor)
    {
        if (!Coastal(town) && !OnRiver(town)) return;
        if (Harbour(town) == null)
        {
            // The stand-in mayor raises the harbour once the treasury can spare it.
            var d = town.Content.Building("harbour");
            if (autoMayor && !town.Buildings.Any(b => b.Def.Id == d.Id) && Planning.Blocked(town, d) == null && town.Treasury > Planning.Cost(town, d) + 300
                && Land.Find(town, d, null) is { } lot)
            {
                lot.Zone = Zone.Civic;
                Planning.Place(town, d.Id, lot);
            }
            return;
        }
        var r = Rules(town);
        var st = town.FleetState;
        if (!st.Granted)
        {
            st.Granted = true;
            st.NextRaidDay = town.Day + r.Corsairs.RaidEvery;
            if (r.Ships.Count > 0) Build(town, r.Ships[0].Id, free: true);
        }
        bool winter = Seasons.Current(town).Id == "winter";
        Hands(town);
        Corsairs(town);
        Voyages.Daily(town);
        foreach (var s in AtSea(town).ToList())
        {
            if (s.Out is { } v)
            {
                if (town.Day >= v.BackDay) Home(town, s, v);
                continue;
            }
            // Out on a job or carrying a company: no fishing today.
            if (s.Hull <= 0 || s.Flight != null || Voyages.Carrying(town, s) != null) continue;
            var d = Def(town, s);
            if (Has(town, r.Harbour.DryDock) && !AtAnchor(town, s)) s.Hull = Math.Min(d.Hull, s.Hull + r.Harbour.DockMend);
            double catchToday = d.Fish * (1 + r.ProwessFish * (s.Prowess - 1)) * (winter ? r.WinterFish : 1) * (OnRiver(town) ? r.River.Fish : 1) * (Quay(town) == "fishers" ? r.Harbour.Fishers : 1) * Manned(town);
            town.Market.Seed(Good.Food, catchToday);
            s.Fished += catchToday;
        }
        if (autoMayor) StandIn(town);
        Routes(town);
        if (Dues(town) is var dues and > 0) town.Book(TreasuryAccount.Trade, dues);
        if (town.Day >= st.NextRaidDay) Raid(town);
    }

    private static Rng Dice(Town town, int salt) => new(town.Seed * 7349 + town.Day * 31 + salt);

    private static void Home(Town town, Ship s, Voyage v)
    {
        var r = Rules(town);
        var c = r.Corsairs;
        var port = town.Settlements[v.SettlementId];
        var d = Def(town, s);
        s.Out = null;
        s.Voyages++;
        s.Hull = Math.Max(0, s.Hull - d.Wear * v.Days);
        double qty = v.Qty;
        var rng = Dice(town, s.Id);
        string fight = "";
        int ring = town.Regions[port.RegionId].Ring;
        // Corsairs lie in wait on the way home: fought like any ship fight (ShipFight), the guard at the rail with the crew.
        bool anchored = AtAnchor(town, s);
        double lit = Has(town, r.Harbour.Lighthouse) ? r.Harbour.LightCorsairs : 1;
        double stray = Holds(town).Count == 0 ? c.Stray : 1;
        if (rng.Chance(Math.Min(c.Max, c.Chance + c.PerRing * ring) * lit * stray) && Corsair(town, rng, ring) is { } foe)
        {
            string outcome = ShipFight.Resolve(town, s, foe, town.Regions[port.RegionId], [], Voyages.Guards(town, s, 0), false, rng, out _);
            if (outcome is "won" or "boarded")
                fight = " " + Fill(rng, c.Win, s, port).Replace("{prize}", Js.Str(Js.Round(town.Content.Sky.Prize * (1 + ring))), StringComparison.Ordinal);
            else if (outcome is "lost" or "repelled" or "wrecked")
            {
                double lost = Js.Round(qty * c.Loot);
                qty -= lost;
                fight = " " + Fill(rng, c.Lose, s, port).Replace("{lost}", Js.Str(lost), StringComparison.Ordinal).Replace("{good}", Lower(v.Cargo), StringComparison.Ordinal);
                if (outcome == "wrecked" || s.Hull <= 0)
                {
                    Sink(town, s, rng);
                    town.Log(LedgerCategory.War, Importance.Major, Fill(rng, c.Sunk, s, port));
                    return;
                }
            }
        }
        s.Prowess = Math.Min(5, s.Prowess + r.Learn);
        if (port.Razed)
        {
            town.Market.Seed(v.Cargo, qty);
            s.LastLine = $"{fight.Trim()} {Cap(s.Name)} comes home with her hold still full: there is nothing left at {port.Name}.".Trim();
            town.Log(LedgerCategory.Town, Importance.Notable, s.LastLine);
            return;
        }
        // At anchor the cargo comes in by rowing boat, and some is left in the hold; the trading house sells dearer.
        // The trading house, the merchants' quay and a rich port pay over the market's price; the extra is booked here.
        double sold = Skyship.Sell(town, port, v.Cargo, qty * (anchored ? r.Harbour.AnchorLoad : 1));
        double earned = sold * SaleBonus(town, port);
        if (earned > sold) town.Book(TreasuryAccount.Trade, earned - sold);
        s.Earned += earned;
        string brought = "";
        if (Skyship.Bring(town, port, v.Cargo) is { } bring && bring.Price > 0)
        {
            var (good, price) = bring;
            var mk = town.Market;
            double want = Math.Max(0, mk.Target(good) * town.Content.Trade.Sky.BringBelow - mk.Stock(good));
            double bought = Math.Min(Math.Min(d.Hold, want), Math.Max(0, town.Treasury) / price);
            if (bought >= 5)
            {
                mk.Seed(good, bought);
                town.Book(TreasuryAccount.Trade, -bought * price);
                brought = $", and {Js.Str(Js.Round(bought))} of their {Lower(good)} bought cheap for {Js.Str(Js.Round(bought * price))}";
            }
        }
        World.Shift(town, port, town.Content.Trade.Sky.StandingPerRun);
        s.LastLine = $"{fight.Trim()} {Cap(s.Name)} ties up home from {port.Name}: {Js.Str(Js.Round(qty))} {Lower(v.Cargo)} sold for {Js.Str(Js.Round(earned))} gold{brought}.".Trim();
        town.Log(LedgerCategory.Town, Importance.Notable, s.LastLine);
    }

    /// <summary>What a ship's sale fetches over the market: the trading house, the merchants' quay, and a port richer than most (its prosperity).</summary>
    public static double SaleBonus(Town town, Settlement port)
    {
        var h = Rules(town).Harbour;
        return (Has(town, h.TradingHouse) ? h.Trading : 1) * (Quay(town) == "merchants" ? h.Merchants : 1) * Math.Max(1, 1 + (port.Prosperity - 50) / 100);
    }

    /// <summary>Gold a day foreign hulls pay at the quay: each allied port on the coast sends ships, as many as the berths take.</summary>
    public static double Dues(Town town)
    {
        var h = Rules(town).Harbour;
        if (Harbour(town) == null) return 0;
        int allied = Ports(town).Count(p => p.Allied);
        double d = h.Foreign * allied * (Has(town, h.TradingHouse) ? h.Trading : 1) * (Quay(town) == "merchants" ? 2 : 1);
        return Js.Round(Math.Min(d, h.ForeignBerth * Berths(town)) * 10) / 10;
    }

    // ---- Standing runs ----

    /// <summary>Why a ship can't take a standing run to this port, or null. -1 clears it.</summary>
    public static string? RouteRefused(Town town, Ship s, int settlementId)
    {
        if (Aloft(town, s)) return "an airship trades from the Sky Dock";
        if (settlementId < 0) return null;
        if (settlementId >= town.Settlements.Count || !Ports(town).Contains(town.Settlements[settlementId])) return "not a port the ship can reach";
        if (Def(town, s).Hold <= 0) return "no hold";
        return null;
    }

    public static bool SetRoute(Town town, int shipId, int settlementId)
    {
        var s = town.Ships.FirstOrDefault(x => x.Id == shipId);
        if (s == null || RouteRefused(town, s, settlementId) != null) return false;
        s.Route = settlementId < 0 ? null : settlementId;
        if (s.Route is int id) town.Log(LedgerCategory.Town, Importance.Routine, $"{Cap(s.Name)} will sail for {town.Settlements[id].Name} again each time she is home.");
        return true;
    }

    /// <summary>Ships on standing runs that are home sail again: patched first when worn, with the cargo that pays best there now.</summary>
    private static void Routes(Town town)
    {
        var r = Rules(town);
        foreach (var s in AtSea(town).Where(s => s.Route != null && s.Out == null && s.Flight == null && Voyages.Carrying(town, s) == null).ToList())
        {
            var port = town.Settlements[s.Route!.Value];
            if (!Ports(town).Contains(port))
            {
                s.Route = null;
                town.Log(LedgerCategory.Town, Importance.Notable, $"{Cap(s.Name)} gives up her run to {port.Name}: it is no port of the town's any more.");
                continue;
            }
            if (s.Hull < Def(town, s).Hull * r.PatchBelow && town.Treasury >= RepairCost(town, s)) Repair(town, s.Id);
            var best = Goods.All.Where(g => Refuse(town, s, port, g) == null).Select(g => (G: g, Gain: Profit(town, s, port, g)))
                .Where(x => x.Gain > 0).OrderByDescending(x => x.Gain).ThenBy(x => x.G).FirstOrDefault();
            if (best.Gain > 0) Send(town, s.Id, port.Id, best.G);
        }
    }

    /// <summary>The corsairs a trade run meets: one of sea.json's corsair kinds for the ring.</summary>
    private static ShipFoe? Corsair(Town town, Rng rng, int ring)
    {
        var r = Rules(town);
        var kinds = r.Foes.Where(k => k.Id.StartsWith("corsair", StringComparison.Ordinal) && ring >= k.Rings[0] && ring <= k.Rings[^1]).ToList();
        if (kinds.Count == 0) return null;
        var k = rng.Pick(kinds);
        return new ShipFoe(k.Name, k.Power + r.PowerPerRing * ring + town.FleetState.Wrath, k.Speed, k.Boards);
    }

    /// <summary>Corsairs come for the harbour: the moored ships and the watch fight them off, or they rob it and hole a ship.</summary>
    private static void Raid(Town town)
    {
        var r = Rules(town);
        var c = r.Corsairs;
        town.FleetState.NextRaidDay = town.Day + c.RaidEvery;
        // No hold left on the sea: nobody to come.
        if (Holds(town).Count == 0) return;
        var rng = Dice(town, 977);
        if (!rng.Chance(c.RaidChance)) return;
        // Ships patrolling the home waters turn some raids away before they reach the harbour.
        if (rng.Chance(Voyages.RaidGuard(town)))
        {
            town.Log(LedgerCategory.War, Importance.Routine, "Corsair sails are seen off the coast, but the town's patrol shadows them until they think better of it.");
            return;
        }
        var moored = AtSea(town).Where(s => Voyages.Moored(town, s) && s.Hull > 0).ToList();
        double guard = town.Watch * c.WatchFight + Js.Sum(moored.Where(s => !AtAnchor(town, s)).ToList(), s => Def(town, s).Fight + r.ProwessFight * (s.Prowess - 1))
            + (Has(town, r.Harbour.Barracks) ? r.Harbour.BarracksFight : 0) + (Quay(town) == "guard" ? r.Harbour.GuardBoat : 0);
        int die = rng.D20();
        if (die + guard >= 10 + CorsairPower(town))
        {
            town.Log(LedgerCategory.War, Importance.Notable, Cap(rng.Pick(c.Repelled).Replace("{lord}", Lord(town), StringComparison.Ordinal)));
            return;
        }
        double gold = Math.Min(Math.Max(0, town.Treasury), c.RaidGold), food = Math.Min(town.Market.Stock(Good.Food), c.RaidFood);
        town.Book(TreasuryAccount.Works, -gold);
        town.Market.Seed(Good.Food, -food);
        var holed = moored.Count > 0 ? moored[rng.Range(0, moored.Count)] : null;
        if (holed != null) holed.Hull = Math.Max(1, holed.Hull - c.Hurt);
        town.FleetState.Wrath /= 2;
        town.Log(LedgerCategory.War, Importance.Notable, Cap(rng.Pick(c.Raided)
            .Replace("{lord}", Lord(town), StringComparison.Ordinal)
            .Replace("{gold}", Js.Str(Js.Round(gold)), StringComparison.Ordinal)
            .Replace("{food}", Js.Str(Js.Round(food)), StringComparison.Ordinal)
            .Replace("{ship}", holed?.Name ?? "a fishing boat", StringComparison.Ordinal)));
    }

    /// <summary>
    /// The stand-in (or the harbourmaster): patches worn hulls, keeps as many ships as the berths hold (a carrack once the
    /// charter allows one), gives each idle ship a standing run to the port that clears most a day, and sends the best
    /// fighter on patrol while corsair holds stand near and their wrath is up.
    /// </summary>
    private static void StandIn(Town town)
    {
        var r = Rules(town);
        var st = town.FleetState;
        foreach (var s in AtSea(town).Where(s => s.Out == null && s.Hull < Def(town, s).Hull * r.PatchBelow).ToList())
            if (town.Treasury > RepairCost(town, s) + 200) Repair(town, s.Id);
        int want = Math.Min(Berths(town), City.Reached(town, "town") ? 3 : 2);
        if (AtSea(town).Count() < want && r.Ships.Skip(1).Reverse().FirstOrDefault(k => BuildBlocked(town, k.Id) == null && town.Treasury > Cost(town, k) + 400) is { } kind)
            Build(town, kind.Id);
        if (town.Day < st.NextStandInDay) return;
        st.NextStandInDay = town.Day + r.StandInEvery;
        foreach (var s in AtSea(town).Where(s => s.Route == null && s.Out == null && Voyages.Moored(town, s)).ToList())
        {
            var best = Ports(town).SelectMany(p => Goods.All.Where(g => Refuse(town, s, p, g) == null).Select(g => (P: p, Gain: Profit(town, s, p, g))))
                .Where(x => x.Gain >= r.StandInProfit)
                .OrderByDescending(x => x.Gain / Days(town, s, x.P)).ThenBy(x => x.P.Id).FirstOrDefault();
            if (best.P != null) SetRoute(town, s.Id, best.P.Id);
        }
        // Corsairs near and angry: the best fighter at its mooring goes out on patrol (off its run till it is home).
        if (Holds(town).Any(h => h.RegionId is int g && town.Regions[g].Ring <= 2) && st.Wrath >= 2 && Voyages.Patrolling(town) == 0
            && AtSea(town).Where(s => Voyages.Moored(town, s) && s.Out == null && Def(town, s).Fight > 0).OrderByDescending(s => Def(town, s).Fight).ThenBy(s => s.Id).FirstOrDefault() is { } guard)
            Voyages.Send(town, guard.Id, "patrol");
    }

    private static string Fill(Rng rng, IReadOnlyList<string> texts, Ship s, Settlement port) =>
        (texts.Count == 0 ? "" : rng.Pick(texts))
            .Replace("{captain}", s.Captain, StringComparison.Ordinal)
            .Replace("{ship}", s.Name, StringComparison.Ordinal)
            .Replace("{port}", port.Name, StringComparison.Ordinal);

    private static string Lower(Good g) => g.ToString().ToLowerInvariant();

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
