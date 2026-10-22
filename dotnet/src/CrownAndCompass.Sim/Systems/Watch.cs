namespace CrownAndCompass.Sim.Systems;

/// <summary>A hero sent out to watch a site from cover, or to hide a mirror there and come home.</summary>
public sealed class Scout
{
    public int Id { get; init; }
    public int HeroId { get; init; }
    public required string SiteId { get; init; }

    /// <summary>Carrying the far half of a mirror pair to hide at the site.</summary>
    public bool Mirror { get; init; }
    public int DepartTick { get; init; }
    public int ArriveTick { get; set; }

    /// <summary>When the scout stops watching and turns for home.</summary>
    public int LeaveTick { get; set; }
    public int ReturnTick { get; set; }
    public bool Home { get; set; }
    public bool Lost { get; set; }

    /// <summary>The arrival has been written up (and any mirror hidden).</summary>
    public bool Arrived { get; set; }

    public bool Out => !Home && !Lost;
}

/// <summary>A mirror hidden at a site: the town sees the site live until someone finds and smashes it.</summary>
public sealed class Mirror
{
    public required string SiteId { get; init; }
    public int PlacedTick { get; init; }
    public int PlacedBy { get; init; }
    public int? SmashedTick { get; set; }
    public bool Intact => SmashedTick == null;
}

/// <summary>Why the town can see a site live right now.</summary>
public enum LiveSource
{
    Scrying,
    Scout,
    Mirror,
    Spire,
    Outpost,
    Tower,
}

/// <summary>
/// Live sight of the wilds, past the town's own lamplight. The fog of war holds unless the town pays
/// for eyes: scouts posted to watch a site from cover, mirrors hidden at a site (the library's Mirror
/// Network), scryings (see <see cref="Knowledge"/>), and two works that house library relics, the
/// Scrying Pool (a free scrying a day) and the Sleepless Spire (everything within reach, always).
/// A relic a party carries home does the same as its building.
/// </summary>
public static class Watch
{
    private static KnowledgeRules Rules(Town town) => town.Content.Knowledge;

    // Scouts --------------------------------------------------------------------------------------

    public static bool CanScout(Town town, Agent a) => a.Hero != null && Rules(town).Scouts.Roles.Contains(a.Hero.Cls.Role);

    private static bool Ready(Agent a) => !a.Departed && !a.IsAway && a.Hero!.Hp >= a.Hero.MaxHp * 0.7;

    /// <summary>Heroes in town who could go and watch a site, keenest eyes first.</summary>
    public static List<Agent> Watchers(Town town)
    {
        return town.Heroes
            .Where(a => Ready(a) && CanScout(town, a))
            .OrderByDescending(a => a.Hero!.Cls.Skills.GetValueOrDefault(SkillKind.Perception, 0) + Rng.Modifier(a.Abilities.Wis))
            .ThenByDescending(a => a.Hero!.Level)
            .ThenBy(a => a.Id)
            .ToList();
    }

    public static Scout? ScoutAt(Town town, string siteId) => town.Scouts.LastOrDefault(s => s.SiteId == siteId && s.Out);

    public static bool Watching(Town town, Scout s) => s.Out && !s.Mirror && town.Tick >= s.ArriveTick && town.Tick < s.LeaveTick;

    /// <summary>Where a scout is between town (0) and its site (1).</summary>
    public static double ScoutLeg(Town town, Scout s, double hours = 0)
    {
        double t = town.Tick + hours;
        if (t < s.ArriveTick) return (t - s.DepartTick) / Math.Max(1, s.ArriveTick - s.DepartTick);
        if (t < s.LeaveTick) return 1;
        double back = Math.Max(1, s.ReturnTick - s.LeaveTick);
        double reached = s.LeaveTick < s.ArriveTick ? (s.LeaveTick - s.DepartTick) / (double)Math.Max(1, s.ArriveTick - s.DepartTick) : 1;
        return Math.Clamp(reached * (1 - (t - s.LeaveTick) / back), 0, 1);
    }

    private static Scout Dispatch(Town town, Agent hero, SiteState site, bool mirror)
    {
        int travel = site.Def.Distance * SimTime.HoursPerDay;
        int arrive = town.Tick + travel;
        int leave = mirror ? arrive : arrive + Rules(town).Scouts.WatchDays * SimTime.HoursPerDay;
        var s = new Scout
        {
            Id = town.Scouts.Count + 1,
            HeroId = hero.Id,
            SiteId = site.Def.Id,
            Mirror = mirror,
            DepartTick = town.Tick,
            ArriveTick = arrive,
            LeaveTick = leave,
            ReturnTick = leave + travel,
        };
        town.Scouts.Add(s);
        hero.Hero!.AwayUntil = s.ReturnTick;
        hero.Activity = Activity.Idle;
        hero.LocationId = null;
        return s;
    }

    /// <summary>Send the best scout in town to watch a site. Null when nobody can go or someone already is.</summary>
    public static Scout? PostScout(Town town, string siteId)
    {
        var site = Quests.Site(town, siteId);
        if (site == null || site.Known == SiteKnowledge.Unheard || site.ThoughtCleared || ScoutAt(town, siteId) != null) return null;
        if (Stewards.Intercept(town, "bounties", new StewardAsk { Area = "bounties", Kind = "scout", Title = $"Scout {site.Def.Name}", Why = "• A party is heading there\n• Eyes on it before they arrive", S1 = siteId })) return null;
        var hero = Watchers(town).FirstOrDefault();
        if (hero == null) return null;
        var s = Dispatch(town, hero, site, false);
        town.Log(
            LedgerCategory.Quest,
            Importance.Notable,
            $"{hero.Name} the {hero.Hero!.Cls.Name} slips out of town to watch {site.Def.Name} from cover, for {Js.Str(Rules(town).Scouts.WagePerDay)} gold a day.",
            [hero.Id]);
        return s;
    }

    /// <summary>Call a scout home early.</summary>
    public static bool RecallScout(Town town, int scoutId)
    {
        var s = town.Scouts.FirstOrDefault(x => x.Id == scoutId);
        if (s == null || !s.Out || town.Tick >= s.LeaveTick) return false;
        int back = town.Tick < s.ArriveTick ? town.Tick - s.DepartTick : s.ArriveTick - s.DepartTick;
        s.LeaveTick = town.Tick;
        s.ReturnTick = town.Tick + back;
        town.Agent(s.HeroId).Hero!.AwayUntil = s.ReturnTick;
        return true;
    }

    // Mirrors -------------------------------------------------------------------------------------

    /// <summary>Casters in town who can work the Mirror Network, strongest first.</summary>
    public static List<Agent> MirrorCasters(Town town)
    {
        var m = Rules(town).Mirror;
        return town.Heroes
            .Where(a => Ready(a) && a.Hero!.Level >= m.MinLevel && Knowledge.Casts(town, a, m.Spell))
            .OrderByDescending(a => a.Hero!.Level)
            .ThenBy(a => a.Id)
            .ToList();
    }

    public static Mirror? MirrorAt(Town town, string siteId) => town.Mirrors.LastOrDefault(m => m.SiteId == siteId && m.Intact);

    public static bool MirrorUnderway(Town town, string siteId) => town.Scouts.Any(s => s.Out && s.Mirror && !s.Arrived && s.SiteId == siteId);

    /// <summary>
    /// Pay for a mirror pair: a caster in town binds them, and a scout (or the caster, if no scout is
    /// free) carries the far mirror out and hides it at the site.
    /// </summary>
    public static Scout? PlantMirror(Town town, string siteId)
    {
        var rules = Rules(town).Mirror;
        var site = Quests.Site(town, siteId);
        if (site == null || site.Known == SiteKnowledge.Unheard || MirrorAt(town, siteId) != null || MirrorUnderway(town, siteId) || town.Treasury < rules.Cost) return null;
        var caster = MirrorCasters(town).FirstOrDefault();
        if (caster == null) return null;
        var carrier = Watchers(town).FirstOrDefault() ?? caster;
        town.Book(TreasuryAccount.Sight, -rules.Cost);
        caster.Gold += rules.Cost / 4;
        var s = Dispatch(town, carrier, site, true);
        string spell = town.Content.Library.Spell(rules.Spell)?.Name ?? "the Mirror Network";
        town.Log(
            LedgerCategory.Quest,
            Importance.Notable,
            carrier == caster
                ? $"{caster.Name} the {caster.Hero!.Cls.Name} binds a pair of silvered mirrors with {spell} and sets out to hide one at {site.Def.Name}."
                : $"{caster.Name} the {caster.Hero!.Cls.Name} binds a pair of silvered mirrors with {spell}; {carrier.Name} carries one out to hide at {site.Def.Name}.",
            carrier == caster ? [caster.Id] : [caster.Id, carrier.Id]);
        return s;
    }

    // Sight works and relics ----------------------------------------------------------------------

    public static Building? WorkBuilding(Town town, SightWorkDef w) => town.Buildings.FirstOrDefault(b => b.Def.Id == w.Building);

    /// <summary>The work stands finished, or its relic came home with a party.</summary>
    public static bool HasWork(Town town, SightWorkDef w) => WorkBuilding(town, w)?.Complete == true || town.Relics.Contains(w.Relic);

    public static double WorkCost(Town town, SightWorkDef w)
    {
        var def = town.Content.Building(w.Building);
        return Js.Round(def.Cost + town.Market.Quote(Good.Timber, def.Timber));
    }

    /// <summary>Why the town can't start this work yet, or null when it can.</summary>
    public static string? WorkBlocked(Town town, SightWorkDef w)
    {
        if (HasWork(town, w)) return "built";
        if (WorkBuilding(town, w) != null) return "under way";
        if (town.Population < w.Population) return $"needs {w.Population} residents";
        if (town.Treasury < WorkCost(town, w)) return "not enough gold";
        return null;
    }

    /// <summary>Commission a sight work on a civic lot near the hall; builders put it up like any other site.</summary>
    /// <param name="at">The lot the player picked, else one near the hall.</param>
    public static Building? BuildWork(Town town, string buildingId, Lot? at = null)
    {
        var w = Rules(town).Works.FirstOrDefault(x => x.Building == buildingId);
        if (w == null || WorkBlocked(town, w) != null) return null;
        var def = town.Content.Building(buildingId);
        var lot = at ?? Planning.CivicLot(town, def);
        if (lot == null) return null;
        lot.Zone = Zone.Civic;
        town.Book(TreasuryAccount.Sight, -WorkCost(town, w));
        town.Market.Buy(Good.Timber, def.Timber);
        var b = town.AddBuilding(def, lot, null, $"the {def.Name}", false);
        b.LaborBudget = Construction.Hours(town, def) * Brain.ConstructionWage;
        var relic = town.Content.Library.Item(w.Relic);
        town.Log(
            LedgerCategory.Town,
            Importance.Notable,
            $"The council commissions {b.Name}{(relic != null ? $" to house {Library.A(relic.Name)}" : "")}.");
        return b;
    }

    /// <summary>How many days out the town can always see, from the Spire or its relic.</summary>
    public static int Reach(Town town) => Rules(town).Works.Where(w => HasWork(town, w)).Select(w => w.Reach).DefaultIfEmpty(0).Max();

    /// <summary>Free scryings a day from the Scrying Pool or its relic.</summary>
    public static int FreeScryings(Town town) => Rules(town).Works.Where(w => HasWork(town, w)).Sum(w => w.Scryings);

    public static int FreeScryingsLeft(Town town) => Math.Max(0, FreeScryings(town) - town.FreeScryingsUsed);

    /// <summary>A party hauls home a town-sized item; the hall keeps it.</summary>
    public static void ReceiveRelic(Town town, LibItem item, string partyName)
    {
        if (Regalia.Is(town, item))
        {
            // Regalia goes to the armory, for the mayor to wear.
            Armory.Add(town, item, $"hauled home by the {partyName}");
            town.Log(LedgerCategory.Town, Importance.Notable, $"The {partyName} lay the {item.Name} before the mayor. It waits in the armory to be worn.");
            return;
        }
        town.Relics.Add(item.Id);
        var work = Rules(town).Works.FirstOrDefault(w => w.Relic == item.Id);
        string use = work == null ? "" : work.Reach > 0
            ? " From the hall's roof it shows the near country live, day and night."
            : " Once a day it will show one site under fog, with no scryer to pay.";
        town.Log(LedgerCategory.Town, Importance.Major, $"The {partyName} set the {item.Name} down in the town hall.{use}");
    }

    // Watchtowers -------------------------------------------------------------------------------

    private static WatchtowerRules Towers(Town town) => Rules(town).Watchtowers;

    /// <summary>Tiles each watchtower sees around itself: wider as the town grows.</summary>
    public static double TowerSight(Town town)
    {
        var r = Towers(town);
        return Math.Min(r.MostSight, r.Sight + r.SightPerHundred * town.Population / 100.0);
    }

    /// <summary>The finished, open watchtowers, in id order.</summary>
    public static List<Building> Watchtowers(Town town) =>
        town.Buildings.Where(b => b.Def.Id == Towers(town).Building && b.Complete && !b.Closed).OrderBy(b => b.Id).ToList();

    /// <summary>How many watchtowers the town may raise: one for every so many residents.</summary>
    public static int MostTowers(Town town) => Math.Max(1, town.Population / Math.Max(1, Towers(town).ResidentsEach));

    /// <summary>Why another watchtower can't go up yet, or null (not one: it isn't a tower).</summary>
    public static string? TowerBlocked(Town town, BuildingDef d)
    {
        if (d.Id != Towers(town).Building) return null;
        int have = town.Buildings.Count(b => b.Def.Id == d.Id);
        int most = MostTowers(town);
        return have >= most ? $"the town has {most}: one more at {(have + 1) * Towers(town).ResidentsEach} residents" : null;
    }

    /// <summary>Where a site lies in the town's tiles (from its corner).</summary>
    public static (double X, double Y) SiteTiles(Town town, SiteState site)
    {
        var (x, y) = World.Spot(town, site);
        return Passage.FromDays(x, y);
    }

    /// <summary>The watchtower that has a site in sight, if any.</summary>
    public static Building? TowerOver(Town town, SiteState site)
    {
        var towers = Watchtowers(town);
        if (towers.Count == 0 || site.Def.Distance == 0) return null;
        double reach = TowerSight(town);
        var (sx, sy) = SiteTiles(town, site);
        foreach (var b in towers)
        {
            var lot = town.Lot(b.LotId);
            if (Js.Hypot(sx - lot.X, sy - lot.Y) <= reach) return b;
        }
        return null;
    }

    // What the town sees -------------------------------------------------------------------------

    /// <summary>Why a site is in live sight right now, or null under fog.</summary>
    public static LiveSource? LiveOf(Town town, SiteState site)
    {
        if (site.Held || site.TowerDue != null) return LiveSource.Outpost;
        if (Knowledge.ActiveScrying(town, site.Def.Id) != null) return LiveSource.Scrying;
        if (MirrorAt(town, site.Def.Id) != null) return LiveSource.Mirror;
        var s = ScoutAt(town, site.Def.Id);
        if (s != null && Watching(town, s)) return LiveSource.Scout;
        int reach = Reach(town);
        if (reach > 0 && site.Def.Distance <= reach) return LiveSource.Spire;
        if (TowerOver(town, site) != null) return LiveSource.Tower;
        return null;
    }

    /// <summary>Every site in live sight, in site order.</summary>
    public static List<(SiteState Site, LiveSource Source)> LiveSites(Town town) =>
        town.Sites.Select(s => (s, LiveOf(town, s))).Where(x => x.Item2 != null).Select(x => (x.s, x.Item2!.Value)).ToList();

    // The day -------------------------------------------------------------------------------------

    public static void Daily(Town town, bool auto)
    {
        town.FreeScryingsUsed = 0;
        Scouting(town);
        Mirrors(town);
        OpenWorks(town);
        if (auto) AutoSight(town);
    }

    private static void Scouting(Town town)
    {
        var rules = Rules(town).Scouts;
        foreach (var s in town.Scouts)
        {
            if (!s.Out) continue;
            var hero = town.Agent(s.HeroId);
            var site = Quests.Site(town, s.SiteId)!;
            town.Book(TreasuryAccount.Scouts, -rules.WagePerDay);
            hero.Gold += rules.WagePerDay;

            if (!s.Arrived && town.Tick >= s.ArriveTick && s.LeaveTick >= s.ArriveTick)
            {
                s.Arrived = true;
                Discovery.AtSite(town, s.SiteId, Discovery.Scouted);
                if (s.Mirror)
                {
                    town.Mirrors.Add(new Mirror { SiteId = s.SiteId, PlacedTick = s.ArriveTick, PlacedBy = hero.Id });
                    town.Log(LedgerCategory.Quest, Importance.Notable, $"In the town hall a silvered mirror clouds, then clears: {site.Def.Name}, as it is right now. {hero.GivenName} has hidden its twin there.", [hero.Id]);
                }
                else
                {
                    town.Log(LedgerCategory.Quest, Importance.Routine, $"{hero.GivenName} reaches a hide above {site.Def.Name}. The town can watch it while {hero.GivenName} stays.", [hero.Id]);
                }
            }

            if (Watching(town, s) && site.Cleared)
            {
                RecallScout(town, s.Id);
                town.Log(LedgerCategory.Quest, Importance.Routine, $"With {site.Def.Name} cleared there is nothing left to watch. {hero.GivenName} heads home.", [hero.Id]);
            }
            else if (Watching(town, s) && !site.Cleared && town.Rng.Chance(rules.SpottedPerTier * site.Def.Tier))
            {
                int dc = rules.EscapeDc + rules.EscapeDcPerTier * site.Def.Tier;
                int roll = town.Rng.D20();
                int total = roll + Rng.Modifier(hero.Abilities.Dex) + Expedition.Proficiency(hero.Hero!.Level);
                if (total >= dc)
                {
                    RecallScout(town, s.Id);
                    town.Log(LedgerCategory.Quest, Importance.Notable, $"Something at {site.Def.Name} catches {hero.GivenName}'s scent. {hero.GivenName} breaks cover and runs for home (d20 {roll}, {total} vs DC {dc}).", [hero.Id]);
                }
                else
                {
                    s.Lost = true;
                    hero.Dead = true;
                    Population.Depart(town, hero);
                    foreach (var other in town.Residents) if (other.OpinionOf(hero.Id) >= 40) other.Loneliness = Math.Min(100, other.Loneliness + 30);
                    town.Log(LedgerCategory.Quest, Importance.Major, $"{hero.Name} is found out watching {site.Def.Name} and never comes home (d20 {roll}, {total} vs DC {dc}).", [hero.Id]);
                    continue;
                }
            }

            if (town.Tick >= s.ReturnTick)
            {
                s.Home = true;
                hero.Hero!.AwayUntil = null;
                town.Log(LedgerCategory.Quest, Importance.Routine, $"{hero.GivenName} is back from {site.Def.Name}{(s.Mirror ? ", the mirror hidden" : ", hungry and stiff")}.", [hero.Id]);
            }
        }
    }

    private static void Mirrors(Town town)
    {
        double per = Rules(town).Mirror.SmashPerTier;
        foreach (var m in town.Mirrors)
        {
            if (!m.Intact) continue;
            var site = Quests.Site(town, m.SiteId)!;
            if (site.Cleared || !town.Rng.Chance(per * site.Def.Tier)) continue;
            m.SmashedTick = town.Tick;
            town.Log(LedgerCategory.Quest, Importance.Notable, $"The mirror watching {site.Def.Name} cracks across. Someone there found its twin.");
        }
    }

    private static void OpenWorks(Town town)
    {
        foreach (var w in Rules(town).Works)
        {
            var b = WorkBuilding(town, w);
            if (b == null || !b.Complete || town.SightWorksOpened.Contains(b.Id)) continue;
            town.SightWorksOpened.Add(b.Id);
            string what = w.Reach > 0
                ? $"Everything within {w.Reach} days of town now shows live, day and night."
                : "Once a day it will show one site under fog, with no scryer to pay.";
            town.Log(LedgerCategory.Town, Importance.Major, $"{Cap(b.Name)} opens. {what}");
        }
    }

    /// <summary>The stand-in mayor buys eyes on what matters: the war camp, then wherever the heroes are headed.</summary>
    private static void AutoSight(Town town)
    {
        foreach (var w in Rules(town).Works)
            if (WorkBlocked(town, w) == null && town.Treasury > WorkCost(town, w) + 400) BuildWork(town, w.Building);

        var camp = Quests.Site(town, town.Content.Enemy.Outpost.Id);
        var taken = town.Parties.Where(p => !p.Returned && p.RecalledTick == null).ToList();
        var heading = taken.Select(p => Quests.Site(town, p.SiteId)!).FirstOrDefault(s => LiveOf(town, s) == null);
        // The pool is best spent the day a party goes in.
        var goingIn = taken.Where(p => Knowledge.LegOf(town, p) == Leg.AtSite).Select(p => Quests.Site(town, p.SiteId)!).FirstOrDefault(s => LiveOf(town, s) == null);

        if (camp != null && !camp.Cleared && MirrorAt(town, camp.Def.Id) == null && town.Treasury > Rules(town).Mirror.Cost + 300)
            PlantMirror(town, camp.Def.Id);

        if (goingIn != null && FreeScryingsLeft(town) > 0) Knowledge.CommissionScrying(town, goingIn.Def.Id);
        else if (heading != null && FreeScryings(town) == 0 && !town.Scouts.Any(s => s.Out && !s.Mirror) && town.Treasury > 150) PostScout(town, heading.Def.Id);
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
