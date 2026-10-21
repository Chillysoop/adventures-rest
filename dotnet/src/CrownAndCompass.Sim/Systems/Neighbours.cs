namespace CrownAndCompass.Sim.Systems;

/// <summary>Who leads a people's settlements: what they are called and the names they go by.</summary>
public sealed record LeaderDef
{
    public string Title { get; init; } = "elder";
    public IReadOnlyList<string> Names { get; init; } = [];
}

/// <summary>Where a settlement's prosperity settles: a base, and what lifts or sinks it.</summary>
public sealed record ProsperityRules
{
    public double Base { get; init; } = 45;
    public double Route { get; init; } = 10;
    public double Allied { get; init; } = 6;
    public double Insecure { get; init; } = 15;
    public double InsecureBelow { get; init; } = 30;

    /// <summary>Prosperity lost per point of a trouble's severity.</summary>
    public double Trouble { get; init; } = 0.4;

    /// <summary>Lost while a menace holds the road to it.</summary>
    public double Tolled { get; init; } = 12;
}

/// <summary>Where a settlement's security settles: its own band holds it up, menaces near it bring it down.</summary>
public sealed record SecurityRules
{
    public double Base { get; init; } = 45;
    public double PerMember { get; init; } = 3;
    public double MenaceNear { get; init; } = 12;

    /// <summary>More again for each stage past rising of a menace near it.</summary>
    public double MenacePower { get; init; } = 8;
}

/// <summary>What a town can send to see a settlement through a trouble.</summary>
public sealed record AidDef
{
    public double Gold { get; init; }
    public Good Good { get; init; } = Good.Food;
    public double Qty { get; init; }
}

/// <summary>
/// A trouble a settlement faces: a site that menaces it (cleared by anyone, it ends), or a need that coin
/// and goods can meet. Left alone, it worsens until it breaks the settlement's luck.
/// </summary>
public sealed record TroubleDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>"site" (a site near them to clear) or "need" (aid can meet it).</summary>
    public string Kind { get; init; } = "site";

    /// <summary>Peoples it befalls; empty for any.</summary>
    public IReadOnlyList<string> Peoples { get; init; } = [];

    /// <summary>A season it comes in only, if any.</summary>
    public string Season { get; init; } = "";

    /// <summary>The role of the person the town meets through it (a hunter, a miller), who may remember it.</summary>
    public string Contact { get; init; } = "";
    public double PerDay { get; init; } = 1.5;
    public AidDef? Aid { get; init; }
    public string Text { get; init; } = "";
    public string Solved { get; init; } = "";
    public string Failed { get; init; } = "";
}

/// <summary>A settlement's own adventurers: how often they go out, how they fare, and what is said of them.</summary>
public sealed record BandRules
{
    public IReadOnlyList<string> Names { get; init; } = [];
    public IReadOnlyList<string> Captains { get; init; } = [];
    public int Members { get; init; } = 4;
    public double SetOutChance { get; init; } = 0.08;
    public double Base { get; init; } = 0.45;
    public double PerLevel { get; init; } = 0.15;
    public double PerMember { get; init; } = 0.04;
    public double Min { get; init; } = 0.1;
    public double Max { get; init; } = 0.85;

    /// <summary>Wins between levels, and how far past its ring's first level a band can grow.</summary>
    public int LevelEvery { get; init; } = 2;
    public int LevelAbove { get; init; } = 1;
    public int RegrowDays { get; init; } = 30;
    public double CaptiveChance { get; init; } = 0.5;

    /// <summary>A plea after the band failed pays this much more.</summary>
    public double PleaBonus { get; init; } = 1.5;

    /// <summary>Defence each member at home adds at the walls, times level, while the settlement is allied.</summary>
    public double Troops { get; init; } = 2;
    public string Out { get; init; } = "";
    public string Won { get; init; } = "";
    public string Lost { get; init; } = "";
    public string Taken { get; init; } = "";
    public string Broken { get; init; } = "";
    public string Raised { get; init; } = "";
    public string Rescued { get; init; } = "";
}

/// <summary>A shared improvement the town can pay for at a settlement: it changes something there for good.</summary>
public sealed record ProjectDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public double Cost { get; init; }
    public int Days { get; init; }
    public double Upkeep { get; init; }
    public double Security { get; init; }
    public double Prosperity { get; init; }

    /// <summary>Days off a caravan's trip there, and the share of the raid chance left.</summary>
    public int TripDays { get; init; }
    public double Raid { get; init; } = 1;

    /// <summary>Parties coming home from its region rest there and arrive whole.</summary>
    public bool Rest { get; init; }

    /// <summary>The allied people's gifts to market, times this.</summary>
    public double Boon { get; init; } = 1;

    /// <summary>Peoples who would have it; empty for any.</summary>
    public IReadOnlyList<string> Peoples { get; init; } = [];
    public string Text { get; init; } = "";
    public string Done { get; init; } = "";
}

/// <summary>A favour a contact can do the town, by their role.</summary>
public sealed record FavourDef
{
    /// <summary>goods, gold, reveal, favour (the gods' favour) or hero (they come to fight for the town).</summary>
    public string Kind { get; init; } = "goods";
    public double Qty { get; init; }

    /// <summary>The good; none means the first their people sells.</summary>
    public Good? Good { get; init; }
    public string Text { get; init; } = "";
}

public sealed record CastRules
{
    public int[] FavourEveryDays { get; init; } = [45, 75];

    /// <summary>Regard a contact needs before they do the town favours.</summary>
    public double Regard { get; init; } = 50;
    public double Start { get; init; } = 40;
    public double Solved { get; init; } = 25;
    public double Aided { get; init; } = 20;
    public double Rescued { get; init; } = 45;
    public double Project { get; init; } = 10;
    public double Lapsed { get; init; } = -10;
    public int Memories { get; init; } = 3;
    public Dictionary<string, FavourDef> Favours { get; init; } = [];
}

/// <summary>Allies sending food to a town a host is marching on.</summary>
public sealed record ReliefRules
{
    public double Food { get; init; } = 12;
    public double MinProsperity { get; init; } = 40;
    public int EveryDays { get; init; } = 15;
    public string Text { get; init; } = "";
    public string Led { get; init; } = "";
}

/// <summary>The life of the settlements of the wilds (content/game/neighbours.json).</summary>
public sealed record NeighbourRules
{
    public Dictionary<string, LeaderDef> Leaders { get; init; } = [];
    public double Drift { get; init; } = 0.5;
    public ProsperityRules Prosperity { get; init; } = new();
    public SecurityRules Security { get; init; } = new();
    public int TroubleFirstDay { get; init; } = 14;
    public int[] TroubleEveryDays { get; init; } = [35, 65];

    /// <summary>Days a trouble can run before it does its worst, whatever its severity.</summary>
    public int TroubleMaxDays { get; init; } = 70;
    public IReadOnlyList<TroubleDef> Troubles { get; init; } = [];
    public double FailedProsperity { get; init; } = 25;
    public double FailedStanding { get; init; } = 6;
    public double SolvedProsperity { get; init; } = 12;
    public double AidedStanding { get; init; } = 12;
    public int[] Refugees { get; init; } = [2, 5];
    public double NewLeaderChance { get; init; } = 0.5;

    /// <summary>Rulers' temperaments and how reigns end (<see cref="Reigns"/>), and an enemy's shadow over the settlements (<see cref="Shadows"/>).</summary>
    public IReadOnlyList<TemperamentDef> Temperaments { get; init; } = [];
    public ReignRules Reigns { get; init; } = new();
    public ShadowRules Shadow { get; init; } = new();

    /// <summary>The stand-in mayor aids when the treasury holds this many times the aid's cost.</summary>
    public double AidAuto { get; init; } = 3;
    public BandRules Band { get; init; } = new();
    public IReadOnlyList<ProjectDef> Projects { get; init; } = [];
    public double ProjectMinStanding { get; init; } = 10;
    public double ProjectStanding { get; init; } = 10;
    public double ProjectAuto { get; init; } = 3;
    public double ProjectAutoReserve { get; init; } = 700;
    public CastRules Cast { get; init; } = new();
    public ReliefRules Relief { get; init; } = new();

    public TroubleDef? Trouble(string id) => Troubles.FirstOrDefault(t => t.Id == id);
    public ProjectDef? Project(string id) => Projects.FirstOrDefault(p => p.Id == id);
}

/// <summary>A trouble a settlement is in now.</summary>
public sealed class SettlementTrouble
{
    public string Def { get; set; } = "";
    public int SinceDay { get; set; }

    /// <summary>0 to 100: at 100 (or after the longest a trouble runs) it does its worst.</summary>
    public double Severity { get; set; }

    /// <summary>The site behind it, for a site trouble.</summary>
    public string? SiteId { get; set; }
}

/// <summary>A settlement's own small band of adventurers.</summary>
public sealed class LocalBand
{
    public string Name { get; set; } = "";
    public string Captain { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Members { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }

    /// <summary>Out on a job: the day they come back and where they went.</summary>
    public int? BackDay { get; set; }
    public string? SiteId { get; set; }

    /// <summary>Where their captain is held, if they were taken.</summary>
    public string? CaptiveAt { get; set; }
    public string? CaptiveName { get; set; }
    public int RegrowDay { get; set; }
    public bool Home => BackDay == null;
}

/// <summary>Someone out in the wilds the town has dealt with, who remembers it: a reeve, a rescued miller, a guide.</summary>
public sealed class Contact
{
    public int Id { get; init; }
    public string Name { get; set; } = "";

    /// <summary>"leader", or the role a trouble named (hunter, miller, smith...), or "veteran".</summary>
    public string Role { get; set; } = "";
    public int SettlementId { get; init; }
    public double Regard { get; set; }
    public int MetDay { get; init; }
    public int NextFavourDay { get; set; }

    /// <summary>What they remember of the town, newest last ("the day the Red Lanterns came to the mill").</summary>
    public List<string> Memories { get; } = [];
    public int Favours { get; set; }

    /// <summary>Gone from the wilds: dead, moved away, or come to the town.</summary>
    public bool Gone { get; set; }
}

public sealed class NeighbourState
{
    public List<Contact> Cast { get; } = [];
}

/// <summary>
/// The settlements of the wilds as places with lives of their own. Each has a leader, prosperity and
/// security, and now and then a trouble (missing hunters, a broken mill, a fever) that worsens if no
/// one deals with it. Its own small band of adventurers goes after the troubles it can, wins some and
/// loses some, and asks the town for help when it fails. The town can meet a need with aid, pay for
/// projects there that change it for good (a watchtower, a bridge, a waystation), and the people it
/// helps remember: they come back later with favours. Allies send relief when a host marches on the
/// town, and their bands stand on its walls. Every trouble ends in a lasting change: a person met, a
/// leader replaced, prosperity won or lost.
/// </summary>
public static class Neighbours
{
    private static NeighbourRules Rules(Town town) => town.Content.Neighbours;

    /// <summary>The settlement's own stream for names: it never moves the town's dice.</summary>
    private static Rng Names(Town town, Settlement s, int salt) => new(town.Seed * 31 + s.Id * 977 + salt * 7919 + 17);

    public static LeaderDef LeaderOf(Town town, Settlement s) => Rules(town).Leaders.GetValueOrDefault(s.Kin.Id) ?? new LeaderDef();

    public static TroubleDef? TroubleOf(Town town, Settlement s) => s.Trouble is { } t ? Rules(town).Trouble(t.Def) : null;

    public static IEnumerable<Contact> CastOf(Town town, Settlement s) => town.Neighbours.Cast.Where(c => c.SettlementId == s.Id && !c.Gone);

    public static Contact? ContactById(Town town, int id) => id >= 1 && id <= town.Neighbours.Cast.Count ? town.Neighbours.Cast[id - 1] : null;

    // ---- The daily tick ----

    public static void Daily(Town town, bool autoMayor)
    {
        var r = Rules(town);
        if (town.Settlements.Count == 0) return;
        foreach (var s in town.Settlements)
        {
            if (s.Leader == "") Found(town, s);
            if (s.Razed) continue;
            Reigns.Daily(town, s);
            Drift(town, s);
            Building(town, s);
            Troubles(town, s);
            Band(town, s);
        }
        Shadows.Daily(town, autoMayor);
        Upkeep(town);
        Favours(town);
        Relief(town);
        if (autoMayor) StandIn(town);
    }

    /// <summary>A settlement's leader, band and first trouble day, the first time it is looked at.</summary>
    private static void Found(Town town, Settlement s)
    {
        var r = Rules(town);
        var names = Names(town, s, 1);
        var lead = LeaderOf(town, s);
        s.Leader = lead.Names.Count > 0 ? lead.Names[names.Range(0, lead.Names.Count)] : "the elders";
        s.NextTroubleDay = r.TroubleFirstDay + names.Range(0, r.TroubleEveryDays[1]);
        s.Band = NewBand(town, s, names);
        town.Neighbours.Cast.Add(new Contact
        {
            Id = town.Neighbours.Cast.Count + 1,
            Name = s.Leader,
            Role = "leader",
            SettlementId = s.Id,
            Regard = Math.Max(0, s.Standing + 20),
            MetDay = town.Day,
            NextFavourDay = town.Day + r.Cast.FavourEveryDays[0] + names.Range(0, r.Cast.FavourEveryDays[1] - r.Cast.FavourEveryDays[0] + 1),
        });
    }

    private static LocalBand NewBand(Town town, Settlement s, Rng names)
    {
        var b = Rules(town).Band;
        string name = b.Names.Count > 0 ? b.Names[names.Range(0, b.Names.Count)].Replace("{settlement}", s.Name) : $"the {s.Name} band";
        // "the {settlement} Watch" for "the Court of Thaliel" would read "the the Court of Thaliel Watch".
        if (name.StartsWith("the the ", StringComparison.Ordinal)) name = name[4..];
        string captain = b.Captains.Count > 0 ? b.Captains[names.Range(0, b.Captains.Count)] : "their captain";
        return new LocalBand { Name = name, Captain = captain, Level = Math.Max(1, town.Regions[s.RegionId].Levels[0]), Members = b.Members };
    }

    /// <summary>Prosperity and security move a little each day toward where the settlement's luck puts them.</summary>
    private static void Drift(Town town, Settlement s)
    {
        var r = Rules(town);
        double step = r.Drift;
        s.Security = Toward(s.Security, Math.Clamp(SecurityTarget(town, s), 0, 100), step);
        s.Prosperity = Toward(s.Prosperity, Math.Clamp(ProsperityTarget(town, s), 0, 100), step);
    }

    private static double Toward(double v, double target, double step) => v + Math.Sign(target - v) * Math.Min(Math.Abs(target - v), step);

    public static double SecurityTarget(Town town, Settlement s)
    {
        var r = Rules(town).Security;
        double t = r.Base + (s.Band is { Home: true } b ? b.Members * r.PerMember : 0);
        foreach (var p in s.Projects) t += Rules(town).Project(p)?.Security ?? 0;
        t -= Shadows.SecurityLoss(town, s);
        foreach (var m in Menaces.Alive(town))
        {
            if (Quests.Site(town, m.SeatId) is not { RegionId: int rid }) continue;
            var mr = town.Regions[rid];
            var home = town.Regions[s.RegionId];
            if (Math.Abs(mr.Ring - home.Ring) > 1 || World.AngleBetween(mr.Bearing, home.Bearing) > (mr.Arc + home.Arc) / 2 + 1) continue;
            t -= r.MenaceNear + Math.Max(0, (int)m.Stage - (int)MenaceStage.Rising) * r.MenacePower;
        }
        return t;
    }

    public static double ProsperityTarget(Town town, Settlement s)
    {
        var r = Rules(town).Prosperity;
        double t = r.Base;
        if (Trade.Open(town).Any(x => x.SettlementId == s.Id)) t += r.Route;
        if (s.Allied) t += r.Allied;
        if (s.Security < r.InsecureBelow) t -= r.Insecure;
        if (s.Trouble != null) t -= s.Trouble.Severity * r.Trouble;
        if (Plots.Tolled(town, s) != null) t -= r.Tolled;
        t -= Shadows.ProsperityLoss(town, s);
        foreach (var p in s.Projects) t += Rules(town).Project(p)?.Prosperity ?? 0;
        return t;
    }

    // ---- Troubles ----

    private static void Troubles(Town town, Settlement s)
    {
        var r = Rules(town);
        if (s.Trouble is { } t)
        {
            var def = r.Trouble(t.Def);
            if (def == null)
            {
                s.Trouble = null;
                return;
            }
            if (t.SiteId != null && Quests.Site(town, t.SiteId) is { Cleared: true })
            {
                Solve(town, s, def, null);
                return;
            }
            // Insecure country makes every trouble worse; a strong band holds it off a little.
            t.Severity = Math.Min(100, t.Severity + def.PerDay * (s.Security < r.Prosperity.InsecureBelow ? 1.3 : 1) * Character.Trouble(town, s, def.Id));
            if (t.Severity >= 100 || town.Day - t.SinceDay >= r.TroubleMaxDays) Fail(town, s, def);
            return;
        }
        if (town.Day < s.NextTroubleDay) return;
        s.NextTroubleDay = town.Day + town.Rng.Range(r.TroubleEveryDays[0], r.TroubleEveryDays[1] + 1);
        Begin(town, s);
    }

    /// <summary>Something goes wrong for a settlement: a trouble that fits its people and the season, and a site near it for a site trouble.</summary>
    private static void Begin(Town town, Settlement s)
    {
        var r = Rules(town);
        string season = Seasons.Current(town).Id;
        var region = town.Regions[s.RegionId];
        var site = TroubleSite(town, s);
        var fits = r.Troubles
            .Where(d => d.Peoples.Count == 0 || d.Peoples.Contains(s.Kin.Id))
            .Where(d => d.Season == "" || d.Season == season)
            .Where(d => d.Kind != "site" || site != null)
            .ToList();
        if (fits.Count == 0) return;
        var def = fits[town.Rng.Range(0, fits.Count)];
        s.Trouble = new SettlementTrouble { Def = def.Id, SinceDay = town.Day, SiteId = def.Kind == "site" ? site!.Def.Id : null };
        if (region.Known) town.Log(LedgerCategory.Quest, Importance.Routine, Saga.Cap(Fill(town, s, def.Text)));
    }

    /// <summary>The nearest standing site to a settlement: in its own region first, then its ring.</summary>
    private static SiteState? TroubleSite(Town town, Settlement s)
    {
        var region = town.Regions[s.RegionId];
        return town.Sites
            .Where(x => !x.Cleared && x.RegionId is int rid && town.Regions[rid].Ring == region.Ring && Menaces.At(town, x.Def.Id) is not { BigBad: true })
            .Where(x => !town.Settlements.Any(o => o.Trouble?.SiteId == x.Def.Id))
            .OrderBy(x => x.RegionId == region.Id ? 0 : 1)
            .ThenBy(x => World.AngleBetween(town.Regions[x.RegionId!.Value].Bearing, region.Bearing))
            .ThenBy(x => x.Def.Tier)
            .ThenBy(x => x.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>
    /// The trouble ends well: the settlement prospers again, and whoever helped is remembered. Who helped:
    /// a party of the town's (by name), the town's aid, or their own band (null party, aided false).
    /// </summary>
    private static void Solve(Town town, Settlement s, TroubleDef def, string? party, bool aided = false)
    {
        var r = Rules(town);
        string said = Saga.Cap(Fill(town, s, def.Solved));
        s.Trouble = null;
        s.Prosperity = Math.Min(100, s.Prosperity + r.SolvedProsperity);
        if (town.Regions[s.RegionId].Known) town.Log(LedgerCategory.Quest, Importance.Notable, said);
        string? memory = party != null ? $"the day the {party} came for {Lower(def.Name)}" : aided ? $"what {town.Name} sent when {s.Name} had {Lower(def.Name)}" : null;
        if (memory == null) return;
        Remember(town, s, "leader", memory, aided ? r.Cast.Aided : r.Cast.Solved);
        if (def.Contact != "") Meet(town, s, def.Contact, memory, aided ? r.Cast.Aided : r.Cast.Solved);
    }

    /// <summary>The trouble does its worst: the settlement is poorer and angrier, some of its people come looking for a new home, and its leader may be replaced.</summary>
    private static void Fail(Town town, Settlement s, TroubleDef def)
    {
        var r = Rules(town);
        string said = Saga.Cap(Fill(town, s, def.Failed));
        s.Trouble = null;
        s.Prosperity = Math.Max(0, s.Prosperity - r.FailedProsperity);
        bool known = town.Regions[s.RegionId].Known;
        World.Shift(town, s, -r.FailedStanding);
        if (known) town.Log(LedgerCategory.Quest, Importance.Notable, said);
        foreach (var c in CastOf(town, s).ToList()) c.Regard = Math.Max(0, c.Regard + r.Cast.Lapsed);
        // Those who give up on the settlement come to the town, if it is known to them.
        if (known && town.Rng.Chance(0.5))
        {
            int n = Population.Settle(town, town.Rng.Range(r.Refugees[0], r.Refugees[1] + 1));
            if (n > 0) town.Log(LedgerCategory.Arrival, Importance.Routine, $"{n} {(n == 1 ? "family" : "families")} from {s.Name} come up the road to {town.Name}, having given up on home.");
        }
        if (!town.Rng.Chance(r.NewLeaderChance) || Reigns.Puppet(s) || Reigns.YearsOf(town, s) < 1) return;
        Reigns.Fall(town, s, "stepdown", null, Lower(def.Name));
    }

    /// <summary>A party of the town's came home from a site: it may have ended a settlement's trouble or freed a band's captain.</summary>
    public static void AfterDelve(Town town, Party party, SiteState site, ExpeditionOutcome outcome)
    {
        if (outcome != ExpeditionOutcome.Cleared && outcome != ExpeditionOutcome.Retreated) return;
        foreach (var s in town.Settlements)
        {
            if (s.Razed) continue;
            if (outcome == ExpeditionOutcome.Cleared && s.Trouble is { } t && t.SiteId == site.Def.Id && TroubleOf(town, s) is { } def)
                Solve(town, s, def, party.Name);
            if (outcome == ExpeditionOutcome.Cleared && s.Band is { CaptiveAt: string at } b && at == site.Def.Id) Rescue(town, s, b, party.Name, site);
        }
        // A waystation in the country they come back through: they rest there and come home whole.
        if (World.RegionOf(town, site) is { SettlementId: int sid } && town.Settlements[sid] is { Razed: false } ws && ws.Projects.Any(p => Rules(town).Project(p)?.Rest == true))
            foreach (int id in party.Members)
                if (town.Agent(id) is { Departed: false, Hero: { } h }) h.Hp = h.MaxHp;
    }

    private static void Rescue(Town town, Settlement s, LocalBand b, string party, SiteState site)
    {
        string name = b.CaptiveName ?? b.Captain;
        b.CaptiveAt = null;
        b.CaptiveName = null;
        string memory = $"the day the {party} found them in {site.Def.Name}";
        town.Log(LedgerCategory.Quest, Importance.Notable, Saga.Cap(Fill(town, s, Rules(town).Band.Rescued)).Replace("{captain}", name).Replace("{party}", party).Replace("{site}", site.Def.Name));
        var c = Meet(town, s, "veteran", memory, Rules(town).Cast.Rescued, name);
        World.Shift(town, s, Rules(town).Cast.Rescued / 5);
        c.NextFavourDay = Math.Min(c.NextFavourDay, town.Day + 20);
    }

    // ---- The band ----

    private static void Band(Town town, Settlement s)
    {
        var br = Rules(town).Band;
        var b = s.Band;
        if (b == null) return;
        if (b.Members <= 0)
        {
            if (town.Day < b.RegrowDay || s.Prosperity < 35) return;
            var nb = NewBand(town, s, Names(town, s, 2 + b.Losses));
            nb.CaptiveAt = b.CaptiveAt;
            nb.CaptiveName = b.CaptiveName;
            nb.Losses = b.Losses;
            s.Band = nb;
            if (town.Regions[s.RegionId].Known) town.Log(LedgerCategory.Quest, Importance.Routine, Saga.Cap(Fill(town, s, br.Raised)));
            return;
        }
        if (b.BackDay is int back)
        {
            if (town.Day >= back) Return(town, s, b);
            return;
        }
        if (s.Trouble is not { SiteId: string sid } || Quests.Site(town, sid) is not { Cleared: false } site) return;
        // The town's own party on the job: the band waits to see.
        if (town.Pleas.Any(p => p.SiteId == sid && p.Status == PleaStatus.Taken) || town.Parties.Any(p => !p.Returned && p.SiteId == sid)) return;
        if (World.LevelOf(site) > b.Level + 1 || b.Members < 2 || !town.Rng.Chance(br.SetOutChance)) return;
        b.BackDay = town.Day + 2 * town.Regions[s.RegionId].Days + 2;
        b.SiteId = sid;
        if (town.Regions[s.RegionId].Known) town.Log(LedgerCategory.Quest, Importance.Routine, Saga.Cap(Fill(town, s, br.Out)).Replace("{site}", site.Def.Name));
    }

    /// <summary>The odds a settlement's band clears a site.</summary>
    public static double BandOdds(Town town, Settlement s, LocalBand b, SiteState site)
    {
        var br = Rules(town).Band;
        return Math.Clamp(br.Base + Character.Band(town, s) + br.PerLevel * (b.Level - World.LevelOf(site)) + br.PerMember * (b.Members - br.Members) - (site.Strength >= town.Content.Lairs.MaxStrength * 0.9 ? 0.05 : 0), br.Min, br.Max);
    }

    private static void Return(Town town, Settlement s, LocalBand b)
    {
        var br = Rules(town).Band;
        var site = b.SiteId != null ? Quests.Site(town, b.SiteId) : null;
        b.BackDay = null;
        b.SiteId = null;
        if (site == null) return;
        bool known = town.Regions[s.RegionId].Known;
        string text;
        if (site.Cleared)
            return;
        if (town.Rng.Chance(BandOdds(town, s, b, site)))
        {
            Clear(town, site);
            b.Wins++;
            int cap = town.Regions[s.RegionId].Levels[^1] + br.LevelAbove;
            if (b.Wins % Math.Max(1, br.LevelEvery) == 0 && b.Level < cap) b.Level++;
            s.Security = Math.Min(100, s.Security + 5);
            if (known) town.Log(LedgerCategory.Quest, Importance.Notable, Saga.Cap(Fill(town, s, br.Won)).Replace("{site}", site.Def.Name));
            if (TroubleOf(town, s) is { } def && s.Trouble!.SiteId == site.Def.Id) Solve(town, s, def, null);
            return;
        }
        int lost = Math.Min(b.Members, town.Rng.Range(1, 3));
        b.Members -= lost;
        b.Losses++;
        site.Attempts++;
        bool taken = b.CaptiveAt == null && town.Rng.Chance(br.CaptiveChance);
        if (taken)
        {
            b.CaptiveAt = site.Def.Id;
            b.CaptiveName = b.Captain;
            text = br.Taken;
        }
        else text = br.Lost;
        if (known) town.Log(LedgerCategory.Quest, Importance.Notable, Saga.Cap(Fill(town, s, text)).Replace("{site}", site.Def.Name).Replace("{lost}", lost.ToString()).Replace("{captain}", b.CaptiveName ?? b.Captain));
        // Their own heroes failed: they turn to the town's, and pay more for it.
        World.Plead(town, s, site, br.PleaBonus);
        if (b.Members <= 1)
        {
            b.Members = 0;
            b.RegrowDay = town.Day + br.RegrowDays;
            if (known) town.Log(LedgerCategory.Quest, Importance.Routine, Saga.Cap(Fill(town, s, br.Broken)).Replace("{site}", site.Def.Name));
            Reigns.Broken(town, s);
        }
    }

    /// <summary>A site someone other than the town's heroes cleared: any bounty on it handed back.</summary>
    internal static void Clear(Town town, SiteState s)
    {
        s.Cleared = true;
        s.ClearedTick = town.Tick;
        if (s.Known != SiteKnowledge.Unheard)
        {
            // Whoever cleared it says what they fought, so word of it also corrects who the town thought lived there.
            s.ThoughtCleared = true;
            s.Believed = Rumors.Occupants(town.Content, s.Def);
        }
        foreach (var b in town.Bounties.Where(b => b.SiteId == s.Def.Id && b.Status == BountyStatus.Open))
        {
            b.Status = BountyStatus.Failed;
            Quests.Refund(town, b);
        }
        foreach (var p in town.Pleas.Where(p => p.SiteId == s.Def.Id && p.Status == PleaStatus.Open)) p.Status = PleaStatus.Answered;
    }

    /// <summary>Defence the bands of allied settlements bring to the walls.</summary>
    public static double Troops(Town town)
    {
        double t = 0;
        foreach (var s in town.Settlements)
            if (s.Allied && !s.Razed && !Shadows.Paying(s) && s.Band is { Home: true, Members: > 0 } b) t += b.Members * b.Level * Rules(town).Band.Troops;
        return t;
    }

    // ---- Aid ----

    /// <summary>Why the town can't send aid to this settlement now, or null if it can.</summary>
    public static string? AidRefused(Town town, Settlement s)
    {
        if (s.Razed) return "they are gone";
        if (!town.Regions[s.RegionId].Known) return "the town hasn't heard of them";
        if (TroubleOf(town, s) is not { Aid: { } aid } def || def.Kind != "need") return "they need nothing aid can buy";
        if (town.Treasury < aid.Gold) return $"needs {Js.Str(aid.Gold)} gold";
        if (town.Market.Stock(aid.Good) < aid.Qty) return $"needs {Js.Str(aid.Qty)} {Lower(aid.Good.ToString())}";
        return null;
    }

    /// <summary>The player's lever: send coin and goods to see a settlement through its trouble.</summary>
    public static bool Aid(Town town, int settlementId)
    {
        if (settlementId < 0 || settlementId >= town.Settlements.Count) return false;
        var s = town.Settlements[settlementId];
        if (AidRefused(town, s) != null) return false;
        var def = TroubleOf(town, s)!;
        var aid = def.Aid!;
        town.Book(TreasuryAccount.Region, -aid.Gold);
        town.Market.Seed(aid.Good, -aid.Qty);
        town.Log(LedgerCategory.Town, Importance.Routine, $"The council sends {Js.Str(aid.Gold)} gold and {Js.Str(aid.Qty)} {Lower(aid.Good.ToString())} to {s.Name} against {Lower(def.Name)}.");
        World.Shift(town, s, Rules(town).AidedStanding);
        Solve(town, s, def, null, aided: true);
        Shadows.Aided(town, s);
        return true;
    }

    // ---- Projects ----

    public static IEnumerable<ProjectDef> ProjectsFor(Town town, Settlement s) =>
        Rules(town).Projects.Where(p => p.Peoples.Count == 0 || p.Peoples.Contains(s.Kin.Id));

    /// <summary>Why the town can't fund this project at this settlement now, or null if it can.</summary>
    public static string? ProjectRefused(Town town, Settlement s, ProjectDef p)
    {
        var r = Rules(town);
        if (s.Razed) return "they are gone";
        if (!town.Regions[s.RegionId].Known) return "the town hasn't heard of them";
        if (p.Peoples.Count > 0 && !p.Peoples.Contains(s.Kin.Id)) return "they have no use for it";
        if (s.Projects.Contains(p.Id)) return "already built";
        if (s.Works != null) return "they are already building";
        if (s.Standing < r.ProjectMinStanding) return $"they won't let the town build below {Js.Str(r.ProjectMinStanding)} standing";
        if (town.Treasury < p.Cost) return $"needs {Js.Str(p.Cost)} gold";
        return null;
    }

    /// <summary>The player's lever: pay for a shared improvement at a settlement.</summary>
    public static bool Fund(Town town, int settlementId, string projectId)
    {
        if (settlementId < 0 || settlementId >= town.Settlements.Count || Rules(town).Project(projectId) is not { } p) return false;
        var s = town.Settlements[settlementId];
        if (ProjectRefused(town, s, p) != null) return false;
        town.Book(TreasuryAccount.Region, -p.Cost);
        s.Works = p.Id;
        s.WorksDoneDay = town.Day + p.Days;
        town.Log(LedgerCategory.Town, Importance.Notable, $"The council pays {Js.Str(p.Cost)} gold for a {Lower(p.Name)} at {s.Name}. {Fill(town, s, p.Text)}");
        return true;
    }

    /// <summary>A project put up at no cost to the town (a story's reward).</summary>
    internal static string Give(Town town, Settlement s, string projectId)
    {
        if (Rules(town).Project(projectId) is not { } p || s.Projects.Contains(p.Id) || s.Razed) return "";
        Finish(town, s, p);
        return $"a {Lower(p.Name)} at {s.Name}";
    }

    private static void Building(Town town, Settlement s)
    {
        if (s.Works is not string id || town.Day < s.WorksDoneDay) return;
        s.Works = null;
        if (Rules(town).Project(id) is { } p) Finish(town, s, p);
    }

    private static void Finish(Town town, Settlement s, ProjectDef p)
    {
        var r = Rules(town);
        s.Projects.Add(p.Id);
        World.Shift(town, s, r.ProjectStanding);
        foreach (var c in CastOf(town, s)) c.Regard = Math.Min(100, c.Regard + r.Cast.Project);
        town.Log(LedgerCategory.Town, Importance.Notable, Saga.Cap(Fill(town, s, p.Done)));
    }

    private static void Upkeep(Town town)
    {
        double total = 0;
        foreach (var s in town.Settlements)
            if (!s.Razed)
                foreach (var id in s.Projects)
                    total += Rules(town).Project(id)?.Upkeep ?? 0;
        if (total > 0) town.Book(TreasuryAccount.Region, -total);
    }

    /// <summary>A caravan's trip there is this many days shorter.</summary>
    public static int TripDays(Town town, Settlement s) => s.Projects.Sum(id => Rules(town).Project(id)?.TripDays ?? 0);

    /// <summary>The share of the raid chance left on the road there.</summary>
    public static double RaidShare(Town town, Settlement s)
    {
        double k = 1;
        foreach (var id in s.Projects) k *= Rules(town).Project(id)?.Raid ?? 1;
        return k;
    }

    /// <summary>An allied people's gifts to market, times this.</summary>
    public static double BoonShare(Town town, Settlement s)
    {
        double k = 1;
        foreach (var id in s.Projects) k *= Rules(town).Project(id)?.Boon ?? 1;
        return k * (0.5 + s.Prosperity / 100);
    }

    // ---- The cast ----

    /// <summary>Someone of this settlement remembers what the town did: an existing contact of the role, or a new one met now.</summary>
    internal static Contact Meet(Town town, Settlement s, string role, string memory, double regard, string? name = null)
    {
        var r = Rules(town).Cast;
        var c = CastOf(town, s).FirstOrDefault(x => x.Role == role && (name == null || x.Name == name));
        if (c == null)
        {
            c = new Contact
            {
                Id = town.Neighbours.Cast.Count + 1,
                Name = name ?? NewName(town, s),
                Role = role,
                SettlementId = s.Id,
                Regard = r.Start,
                MetDay = town.Day,
                NextFavourDay = town.Day + town.Rng.Range(r.FavourEveryDays[0], r.FavourEveryDays[1] + 1),
            };
            town.Neighbours.Cast.Add(c);
        }
        c.Regard = Math.Clamp(c.Regard + regard, 0, 100);
        c.Memories.Add(memory);
        while (c.Memories.Count > r.Memories) c.Memories.RemoveAt(0);
        return c;
    }

    private static void Remember(Town town, Settlement s, string role, string memory, double regard)
    {
        var c = CastOf(town, s).FirstOrDefault(x => x.Role == role);
        if (c != null) Meet(town, s, role, memory, regard, c.Name);
    }

    /// <summary>A name for someone of the settlement's people: from the names of the town's races, by its people's race.</summary>
    internal static string NewName(Town town, Settlement s)
    {
        var race = town.Content.Races.FirstOrDefault(x => x.Id == s.Kin.Race) ?? town.Content.Races.First(x => x.Id == "human");
        var taken = town.Neighbours.Cast.Select(c => c.Name).ToHashSet();
        for (int i = 0; i < 20; i++)
        {
            string n = race.Given[town.Rng.Range(0, race.Given.Count)] + (race.Family.Count > 0 ? " " + race.Family[town.Rng.Range(0, race.Family.Count)] : "");
            if (taken.Add(n)) return n;
        }
        return race.Given[0] + " of " + s.Name;
    }

    /// <summary>Contacts who think well enough of the town do it a favour now and then, by their role.</summary>
    private static void Favours(Town town)
    {
        var r = Rules(town).Cast;
        foreach (var c in town.Neighbours.Cast)
        {
            if (c.Gone || town.Day < c.NextFavourDay) continue;
            c.NextFavourDay = town.Day + town.Rng.Range(r.FavourEveryDays[0], r.FavourEveryDays[1] + 1);
            var s = town.Settlements[c.SettlementId];
            if (s.Razed)
            {
                c.Gone = true;
                continue;
            }
            if (c.Regard < r.Regard || c.Memories.Count == 0 || !town.Regions[s.RegionId].Known) continue;
            if (!r.Favours.TryGetValue(c.Role, out var f)) continue;
            Favour(town, c, s, f);
        }
    }

    private static void Favour(Town town, Contact c, Settlement s, FavourDef f)
    {
        var good = f.Good ?? (s.Kin.Trade.Offers.Count > 0 ? s.Kin.Trade.Offers[0] : Good.Food);
        string text = f.Text.Replace("{name}", c.Name).Replace("{memory}", c.Memories[^1]).Replace("{good}", Lower(good.ToString())).Replace("{qty}", Js.Str(f.Qty))
            .Replace("{band}", s.Band?.Name ?? "their band").Replace("{town}", town.Name);
        c.Favours++;
        switch (f.Kind)
        {
            case "goods":
                town.Market.Seed(good, f.Qty);
                break;
            case "gold":
                town.Book(TreasuryAccount.Region, f.Qty);
                break;
            case "favour":
            {
                var god = Faith.TempleGod(town) ?? Faith.CharterGod(town);
                town.Faiths.Favor[god.Id] = Math.Max(0, town.Faiths.Favor.Get(god.Id, 0) + f.Qty);
                break;
            }
            case "reveal":
            {
                var region = town.Regions[s.RegionId];
                int told = 0;
                foreach (var x in town.Sites.Where(x => x.RegionId is int rid && (rid == region.Id || (town.Regions[rid].Ring == region.Ring + 1 && World.AngleBetween(town.Regions[rid].Bearing, region.Bearing) <= region.Arc))).OrderBy(x => x.Def.Id, StringComparer.Ordinal))
                {
                    if (x.Cleared || x.Known == SiteKnowledge.Confirmed || (x.Known == SiteKnowledge.Rumoured && x.Trust >= 70)) continue;
                    town.Regions[x.RegionId!.Value].Known = true;
                    Rumors.Hear(town, x, Rumors.Occupants(town.Content, x.Def), x.Def.Tier, 70);
                    if (++told >= 3) break;
                }
                if (told == 0) return;
                break;
            }
            case "hero":
            {
                if (town.Taverns.Count == 0 || town.Heroes.Count >= City.HeroCap(town)) return;
                var classes = s.Kin.Boon.Classes.Count > 0 ? s.Kin.Boon.Classes : ["fighter"];
                var cls = town.Content.Classes.FirstOrDefault(x => x.Id == classes[town.Rng.Range(0, classes.Count)]) ?? town.Content.Classes[0];
                int level = Math.Max(1, (s.Band?.Level ?? 2));
                var a = Quests.CreateHero(town, cls, level, s.Kin.Race == "" ? null : s.Kin.Race, s.Kin.Id);
                // The one who walks in is the contact the text names, not a stranger.
                if (c.Name != "") a.GivenName = c.Name.Split(' ')[0];
                c.Gone = true;
                town.Log(LedgerCategory.Arrival, Importance.Notable, $"{Saga.Cap(text)} {a.Describe()}.", [a.Id]);
                return;
            }
        }
        town.Log(LedgerCategory.Town, Importance.Notable, Saga.Cap(Fill(town, s, text)));
    }

    // ---- Relief ----

    /// <summary>Allies send food when a host marches on the town; a contact who remembers the town leads it, if there is one.</summary>
    private static void Relief(Town town)
    {
        var r = Rules(town).Relief;
        bool marching = town.Enemy.Stage == ThreatStage.Marching || Menaces.Alive(town).Any(m => m.ArrivalTick != null);
        if (!marching) return;
        foreach (var s in town.Settlements)
        {
            if (!s.Allied || s.Razed || s.Prosperity < r.MinProsperity || town.Day - s.ReliefDay < r.EveryDays) continue;
            if (Plots.Tolled(town, s) != null || Shadows.Paying(s) || Reigns.Relief(town, s) <= 0) continue;
            s.ReliefDay = town.Day;
            double food = Js.Round(r.Food * s.Prosperity / 50 * Reigns.Relief(town, s));
            town.Market.Seed(Good.Food, food);
            var c = CastOf(town, s).Where(x => x.Role != "leader" && x.Memories.Count > 0).OrderByDescending(x => x.Regard).ThenBy(x => x.Id).FirstOrDefault();
            string text = c != null ? r.Led.Replace("{contact}", c.Name).Replace("{memory}", c.Memories[^1]) : r.Text;
            town.Log(LedgerCategory.War, Importance.Notable, Saga.Cap(Fill(town, s, text).Replace("{food}", Js.Str(food))));
        }
    }

    // ---- The stand-in mayor ----

    private static void StandIn(Town town)
    {
        var r = Rules(town);
        // A careful mayor spends nothing abroad while the warband still threatens the town itself.
        if (Threat.IsActive(town)) return;
        foreach (var s in town.Settlements.OrderByDescending(x => x.Standing).ThenBy(x => x.Id))
        {
            if (s.Standing < 10 || AidRefused(town, s) != null) continue;
            var aid = TroubleOf(town, s)!.Aid!;
            if (s.Trouble!.Severity < 40 || town.Treasury < aid.Gold * r.AidAuto + 300) continue;
            Aid(town, s.Id);
            return;
        }
        // A rich town puts something lasting up at its best friend in the wilds, one at a time.
        if (town.Settlements.Any(s => s.Works != null)) return;
        foreach (var s in town.Settlements.Where(x => x.Allied || x.Standing >= 30).OrderByDescending(x => x.Standing).ThenBy(x => x.Id))
        {
            var p = ProjectsFor(town, s).Where(p => ProjectRefused(town, s, p) == null && town.Treasury >= p.Cost * r.ProjectAuto + r.ProjectAutoReserve)
                .OrderBy(p => p.Cost).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
            if (p == null) continue;
            Fund(town, s.Id, p.Id);
            return;
        }
    }

    // ---- Words ----

    public static string Fill(Town town, Settlement s, string text) =>
        text.Replace("{settlement}", s.Name).Replace("{people}", s.People).Replace("{leader}", s.Leader).Replace("{title}", LeaderOf(town, s).Title)
            .Replace("{town}", town.Name).Replace("{band}", s.Band?.Name ?? "their band").Replace("{captain}", s.Band?.Captain ?? "their captain")
            .Replace("{trouble}", TroubleOf(town, s) is { } d ? Lower(d.Name) : "their trouble")
            .Replace("{site}", s.Trouble?.SiteId is string sid && Quests.Site(town, sid) is { } site ? site.Def.Name : "the wilds");

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
