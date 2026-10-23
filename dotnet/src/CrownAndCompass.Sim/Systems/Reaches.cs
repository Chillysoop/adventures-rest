namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// The sky's heights (Conner, 2026-10-22: "there should be areas only accessible by airship, including high level encounters
/// like ancient dragons for late game adventuring parties"). Floating sites sit in bands (world.json isles `bands`): low
/// isles any airship reaches, high isles that want a frigate or better, the cloud sea and the dragon roosts that only the
/// great hulls climb to. Heroes who fly reach the low and high isles on their own. A company bound for a band its wings
/// can't reach boards one of the town's airships, whose ballistae open the fight and which waits at the isle to bring it
/// home. A ship lost on the way leaves the company stranded: the town only knows it is overdue, and sends a ship to look
/// where it went. Every roll here uses the expedition's own dice, never the town's.
/// </summary>
public static class Reaches
{
    private static SkyFleetRules Rules(Town town) => town.Content.Sky;

    private static ExpeditionRules X(Town town) => town.Content.Sky.Expedition;

    private static Rng Dice(Town town, int salt) => new(town.Seed * 7393 + town.Day * 53 + salt * 97 + 3);

    /// <summary>The height of the sky a site floats at, if it floats in one.</summary>
    public static SkyBandDef? Band(Town town, SiteState site) => site.Def.Aloft ? town.Content.World.Isles.Band(site.Def.Band) : null;

    /// <summary>How high a ship must climb to reach the site.</summary>
    public static double Ceiling(Town town, SiteState site) => Band(town, site)?.Ceiling ?? 0;

    /// <summary>Whether heroes who fly get there on their own wings.</summary>
    public static bool FliersGo(Town town, SiteState site) => Band(town, site)?.Fliers ?? true;

    /// <summary>Whether the Sky Dock's lent airship climbs high enough.</summary>
    public static bool DockReaches(Town town, SiteState site) => Ceiling(town, site) <= Rules(town).DockCeiling + 1e-9;

    private static Region? RegionOf(Town town, SiteState site) => site.RegionId is int rid ? town.Regions[rid] : null;

    /// <summary>Days of lift a trip there and back takes: the flight both ways and the wait at the isle.</summary>
    public static int Need(Town town, Ship s, SiteState site) =>
        RegionOf(town, site) is { } r ? Sky.Days(town, s, r) + X(town).Wait : 2;

    /// <summary>The town's airships that could carry a company to the site now, the least ship that climbs high enough first.</summary>
    public static IEnumerable<Ship> Free(Town town, SiteState site)
    {
        if (Sky.Dock(town) == null) return [];
        double ceiling = Ceiling(town, site);
        return Sky.Ships(town)
            .Where(s => Sky.Moored(town, s) && Fleet.Def(town, s).Ceiling >= ceiling - 1e-9 && s.Lift >= Need(town, s, site) && s.Hull >= Fleet.Def(town, s).Hull * 0.25)
            .OrderBy(s => Fleet.Def(town, s).Ceiling)
            .ThenByDescending(s => Fleet.Def(town, s).Marines)
            .ThenBy(s => s.Id)
            .ToList();
    }

    /// <summary>Whether the town has a way as high as this: an airship that climbs to it, or the Lofted City itself.</summary>
    public static bool Climbs(Town town, double ceiling) =>
        ceiling <= 0 || Lofted.Aloft(town) || (Sky.Dock(town) != null && Sky.Ships(town).Any(s => Fleet.Def(town, s).Ceiling >= ceiling - 1e-9));

    /// <summary>Why envoys, caravans, aid and armies can't get to a people of the sky (<see cref="PeopleDef.Band"/>), or null when they can.</summary>
    public static string? Unreached(Town town, Settlement s)
    {
        if (town.Content.World.Isles.Band(s.Kin.Band) is not { } band) return null;
        return Climbs(town, band.Ceiling) ? null : $"only an airship that climbs to {band.Name} reaches them";
    }

    /// <summary>Whether a company can get to a site in the sky at all: on its own wings where the band allows, or aboard.</summary>
    public static bool CanReach(Town town, IReadOnlyList<Agent> members, SiteState site)
    {
        if (!site.Def.Aloft) return true;
        if (FliersGo(town, site) && (Passage.Abilities(town, members) & Ways.Air) != 0) return true;
        if (Free(town, site).Any()) return true;
        return DockReaches(town, site) && (Passage.Parse(Passage.Rules(town).Airship.Ways) & Ways.Air) != 0 && Passage.AirshipsFree(town) > 0;
    }

    /// <summary>
    /// A company bound for a site in the sky takes the town's least airship that climbs high enough, out and home again,
    /// unless it can fly there itself. Whatever finds the ship on the way up is fought with the heroes at the rail.
    /// </summary>
    public static RouteMind Embark(Town town, Party party, IReadOnlyList<Agent> members, RouteMind mind, SiteState site)
    {
        if (!site.Def.Aloft || party.Airship) return mind;
        if (FliersGo(town, site) && (mind.Ways & Ways.Air) != 0) return mind;
        var ship = Free(town, site).FirstOrDefault();
        if (ship == null) return mind;
        party.ShipId = ship.Id;
        party.Edge += X(town).Edge;
        var rng = Dice(town, ship.Id * 5 + party.Id);
        var region = RegionOf(town, site);
        party.Said.Add(Fill(town, rng, "carry", ship, party, site.Def.Name));
        if (rng.Chance(Sky.MeetChance(town, region)) && Sky.MeetFoe(town, rng, region) is { } foe)
        {
            var heroes = members.Where(a => a.Hero != null).Select(a => a.Id).Take(Fleet.Def(town, ship).Marines).ToList();
            string outcome = ShipFight.Resolve(town, ship, foe, region, heroes, 0, false, rng, out int volleys);
            Sky.Record(town, ship, foe.Name, region, outcome, volleys);
            if (outcome != "evaded")
                party.Said.Add(Sky.Fill(town, rng, outcome is "downed" or "wrecked" ? "lost" : outcome, ship, region?.Name ?? "the clouds", foe.Name)
                    .Replace("{marines}", Expedition.ListNames(heroes.Select(id => town.Agent(id).GivenName).DefaultIfEmpty("the crew").ToList()), StringComparison.Ordinal));
            // A ship that loses the fight limps on to the isle, but will not get off it again.
            if (outcome is "downed" or "wrecked" or "lost") party.ShipLost = true;
        }
        party.Said.Add(Fill(town, rng, "opened", ship, party, site.Def.Name));
        return mind with { Ways = mind.Ways | Passage.Parse(Passage.Rules(town).Airship.Ways), Carrier = ship.Name };
    }

    /// <summary>
    /// Called when the company comes out of the site: the ship that waited spends its lift, and a ship lost on the way up
    /// (or found by a foe while it waited) leaves the company stranded. True when it is.
    /// </summary>
    public static bool Wait(Town town, Party party, SiteState site)
    {
        if (party.ShipId is not int id || town.Ships.FirstOrDefault(s => s.Id == id) is not { } ship || !Fleet.Aloft(town, ship)) return false;
        var rng = Dice(town, ship.Id * 11 + party.Id);
        var region = RegionOf(town, site);
        bool lost = party.ShipLost;
        if (!lost && rng.Chance(Sky.MeetChance(town, region)) && Sky.MeetFoe(town, rng, region) is { } foe)
        {
            // The crew alone hold the ship while the company is below.
            string outcome = ShipFight.Resolve(town, ship, foe, region, [], 0, false, rng, out int volleys);
            Sky.Record(town, ship, foe.Name, region, outcome, volleys);
            lost = outcome is "downed" or "wrecked";
        }
        ship.Lift = Math.Max(0, ship.Lift - Need(town, ship, site));
        if (!lost) return false;
        party.ShipId = null;
        if (ship.Hull <= 0 || region == null) town.Ships.Remove(ship);
        else
        {
            ship.DownedRegion = region.Id;
            ship.DownedDay = town.Day;
            ship.Lift = 0;
        }
        return true;
    }

    /// <summary>Strand a company at its site until it is rescued or makes its own way down.</summary>
    public static void Strand(Town town, Party party, SiteState site)
    {
        party.StrandedTick = town.Tick;
        party.ReturnTick = int.MaxValue / 2;
        foreach (int m in party.Members) town.Agent(m).Hero!.AwayUntil = party.ReturnTick;
        var ship = new Ship { Name = "their airship", Captain = "" };
        town.Log(LedgerCategory.Quest, Importance.Notable, Fill(town, Dice(town, party.Id), "stranded", ship, party, site.Def.Name), party.Members);
    }

    /// <summary>Companies stranded in the sky (the town doesn't know it: it only sees them overdue).</summary>
    public static IEnumerable<Party> Stranded(Town town) => town.Parties.Where(p => !p.Returned && p.StrandedTick != null).OrderBy(p => p.Id);

    /// <summary>Why the town can't send a ship to look for a company now, or null when it can.</summary>
    public static string? RescueRefused(Town town, Party party)
    {
        var site = Quests.Site(town, party.SiteId);
        if (party.Returned || site == null || !site.Def.Aloft) return "not lost in the sky";
        if (party.StrandedTick == null) return party.ShipId != null ? "their ship is still with them" : "not lost in the sky";
        if (Sky.Dock(town) == null) return "needs the Sky Dock";
        if (!Free(town, site).Any()) return "no airship free that climbs so high";
        return null;
    }

    /// <summary>
    /// Send the least free airship that climbs high enough to look for a stranded company where it went. Finding them takes
    /// days of searching, none if the site is being scried; then it brings them home. Null when sent, or why not.
    /// </summary>
    public static string? Rescue(Town town, int partyId)
    {
        var party = town.Parties.FirstOrDefault(p => p.Id == partyId);
        if (party == null) return "no such company";
        if (RescueRefused(town, party) is { } why) return why;
        var site = Quests.Site(town, party.SiteId)!;
        var ship = Free(town, site).First();
        var rng = Dice(town, ship.Id * 13 + party.Id);
        var x = X(town);
        int search = Knowledge.ActiveScrying(town, site.Def.Id) != null ? 0 : rng.Range(x.Search[0], x.Search[^1] + 1);
        int days = Need(town, ship, site) + search;
        ship.Lift = Math.Max(0, ship.Lift - days);
        party.ShipId = ship.Id;
        party.StrandedTick = null;
        party.ReturnTick = town.Tick + days * SimTime.HoursPerDay;
        party.LeaveTick = party.ReturnTick - Math.Max(1, Need(town, ship, site) / 2) * SimTime.HoursPerDay;
        party.Expected = party.ReturnTick;
        foreach (int m in party.Members) town.Agent(m).Hero!.AwayUntil = party.ReturnTick;
        town.Log(LedgerCategory.Quest, Importance.Notable, Fill(town, rng, "searching", ship, party, site.Def.Name));
        party.Said.Add(Fill(town, rng, "rescued", ship, party, site.Def.Name).Replace("{search}", search == 0 ? "no time at all" : search == 1 ? "a day" : $"{search} days", StringComparison.Ordinal));
        return null;
    }

    /// <summary>Daily: a company stranded long enough climbs down on its own, hurt for it.</summary>
    public static void Daily(Town town)
    {
        var x = X(town);
        foreach (var party in Stranded(town).ToList())
        {
            if (town.Tick - party.StrandedTick!.Value < x.StrandMost * SimTime.HoursPerDay) continue;
            var site = Quests.Site(town, party.SiteId)!;
            int walk = (2 + (RegionOf(town, site)?.Ring ?? 0)) * SimTime.HoursPerDay;
            party.StrandedTick = null;
            party.LeaveTick = town.Tick;
            party.ReturnTick = town.Tick + walk;
            foreach (var a in party.Members.Select(town.Agent).Where(a => !a.Dead && a.Hero != null))
            {
                a.Hero!.AwayUntil = party.ReturnTick;
                a.Hero.Hp = Math.Max(1, a.Hero.Hp - (int)Math.Round(a.Hero.MaxHp * x.WalkHurt));
            }
            party.Said.Add(Fill(town, Dice(town, party.Id), "ownWay", new Ship { Name = "", Captain = "" }, party, site.Def.Name));
        }
    }

    private static string Fill(Town town, Rng rng, string key, Ship s, Party party, string where)
    {
        var lines = Rules(town).Texts.TryGetValue(key, out var t) && t.Count > 0 ? t : [""];
        string line = lines[rng.Range(0, lines.Count)]
            .Replace("{party}", party.Name, StringComparison.Ordinal)
            .Replace("{ship}", s.Name, StringComparison.Ordinal)
            .Replace("{captain}", s.Captain, StringComparison.Ordinal)
            .Replace("{where}", where, StringComparison.Ordinal);
        return line.Length == 0 ? line : char.ToUpperInvariant(line[0]) + line[1..];
    }
}
