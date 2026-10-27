namespace CrownAndCompass.Sim.Systems;

public enum ThreatStage
{
    Quiet,
    Omens,
    Probing,
    Raids,
    Marching,
    Defeated,
    Victorious,
}

public sealed class EnemyState
{
    public double Strength { get; set; }
    public ThreatStage Stage { get; set; } = ThreatStage.Quiet;

    /// <summary>When the marching warband reaches the walls.</summary>
    public int? ArrivalTick { get; set; }

    public int? NextRaidTick { get; set; }
    public bool Outpost { get; set; }
    public int Sieges { get; set; }
    public int Raids { get; set; }

    /// <summary>Nights the raiders have hung back because the heroes were home.</summary>
    public int RaidWaited { get; set; }
    public int LastSignTick { get; set; }

    /// <summary>The wall section the march comes at (<see cref="Garrison.Approach"/>).</summary>
    public int Approach { get; set; }

    /// <summary>Signs shown lately, so the same rumour doesn't repeat.</summary>
    public List<string> RecentSigns { get; } = [];
}

public enum PalisadeState
{
    None,
    Building,
    Built,
}

public enum DefenseWork
{
    Palisade,
    Tower,

    /// <summary>Stone walls round the old town, once it is a town (GDD rule C4).</summary>
    StoneWall,

    /// <summary>The city wall round the old town and the streets about it, once it is a city.</summary>
    CityWall,

    /// <summary>A stone gatehouse with a portcullis on the south road, once the stone walls stand.</summary>
    Gatehouse,

    /// <summary>A moat dug round the stone walls.</summary>
    Moat,

    /// <summary>Barracks built into the stone walls, where the watch lives.</summary>
    Barracks,
}

public sealed class Defenses
{
    public PalisadeState Palisade { get; set; } = PalisadeState.None;
    public int? PalisadeReadyTick { get; set; }
    public int Towers { get; set; }

    /// <summary>Ready ticks of towers under construction.</summary>
    public List<int> TowersBuilding { get; set; } = [];

    public PalisadeState StoneWall { get; set; } = PalisadeState.None;
    public int? StoneWallReadyTick { get; set; }
    public PalisadeState CityWall { get; set; } = PalisadeState.None;
    public int? CityWallReadyTick { get; set; }
    public PalisadeState Gatehouse { get; set; } = PalisadeState.None;
    public int? GatehouseReadyTick { get; set; }
    public PalisadeState Moat { get; set; } = PalisadeState.None;
    public int? MoatReadyTick { get; set; }

    /// <summary>Wall barracks standing, and the ready ticks of those going up.</summary>
    public int Barracks { get; set; }
    public List<int> BarracksBuilding { get; set; } = [];

    /// <summary>The wall sections the player put each tower and barracks on, in the order they were built (<see cref="Threat.BuildOnWall"/>).</summary>
    public List<int> TowerAt { get; set; } = [];
    public List<int> BarracksAt { get; set; } = [];

    /// <summary>The building raiders last struck, and the tick its alarm ends: who is near it and outside the walls runs for their life (<see cref="Passage.Fleeing"/>).</summary>
    public int? AlarmBuilding { get; set; }
    public int AlarmUntil { get; set; }

    /// <summary>The last day the walls going up stood idle for want of timber or stone, or -1.</summary>
    public int WallsShort { get; set; } = -1;
}

/// <summary>
/// The slice's Tier 2 enemy, a warband that grows off-screen on the daily tick. Uncleared sites
/// feed it; heroes clearing them, and its war camp, starve it. Pressure on the town climbs through
/// visible stages (omens, probing, raids, the march) and ends in the Last Stand: a siege at the
/// walls, and the warlord in person. Kill the warlord and the town wins; let the walls break twice
/// and it falls.
/// </summary>
public static class Threat
{
    public static string Name(this ThreatStage stage) => stage.ToString().ToLowerInvariant();

    public static void InitThreat(Town town) => town.Enemy.Strength = town.Content.Enemy.StartStrength;

    /// <summary>Position in the escalation (quiet 0 to marching 4); -1 once the campaign is over.</summary>
    private static int StageIndex(ThreatStage s) => s <= ThreatStage.Marching ? (int)s : -1;

    public static bool IsActive(Town town) => town.Enemy.Stage != ThreatStage.Defeated && town.Enemy.Stage != ThreatStage.Victorious;

    /// <summary>Daily growth before the Director's hand: uncleared sites feed the warband, the war camp most of all.</summary>
    public static double Growth(Town town)
    {
        var e = town.Content.Enemy;
        double g = e.BaseGrowth + e.GrowthPerDay * town.Day + e.GrowthPerResident * town.Population;
        foreach (var s in town.Sites)
        {
            // Only the country near town feeds the warband; the far wilds keep their own troubles.
            if (s.Cleared || s.Generated) continue;
            if (s.Def.Id == e.Outpost.Id) g += e.OutpostGrowth;
            else g += s.Def.Tier >= 3 ? e.GrowthPerTier3Site : e.GrowthPerSite;
        }
        return g;
    }

    /// <summary>The town's strength at the walls: watch, heroes at home, a militia of townsfolk, and fortifications.</summary>
    /// <param name="allies">Count warriors the town's allies have pledged; the warlord does not see them coming.</param>
    public static double Defense(Town town, bool allies = true)
    {
        var d = town.Content.Enemy.Defense;
        int heroLevels = town.Heroes.Where(a => !a.IsAway).Sum(a => a.Hero!.Level);
        double baseDefense = town.Watch * d.Watchman * Planning.WatchFactor(town) * Kit.WatchFactor(town) + heroLevels * d.HeroLevel + town.Workers.Count * d.MilitiaPerWorker + Faith.Defense(town) + Factions.Defense(town) + Callings.Defence(town) + Institutions.Defence(town) + Eggs.Defence(town) + Ruins.Defence(town) + Loyalty.Defense(town) + Notoriety.Defense(town) + Decor.Defense(town) + Mercenaries.Defense(town) + Powers.Defense(town) + Clockwork.Defense(town) + Bastions.Defense(town) + Research.Defense(town) + Garrison.DutyDefense(town) + Army.Defense(town) + Voyages.Defense(town) + (allies ? World.AllyTroops(town) + AllyCalls.Defense(town) : 0);
        return baseDefense * Fortification(town);
    }

    public static double Fortification(Town town)
    {
        var d = town.Content.Enemy.Defense;
        var w = town.Defenses;
        double walls = w.CityWall == PalisadeState.Built ? d.CityWall
            : w.StoneWall == PalisadeState.Built ? d.StoneWall
            : w.Palisade == PalisadeState.Built ? d.Palisade : 1;
        // A wall is only as strong as the watch on it.
        walls = 1 + (walls - 1) * Frontier.WallShare(town);
        return walls * Blows.WallFactor(town) * Garrison.MoatFactor(town) * (1 + d.PerTower * w.Towers + Garrison.FortBonus(town) + Planning.Defense(town) + Founding.Defence(town));
    }

    /// <summary>What the town's scouts believe the warband numbers, rounded the way rumours are.</summary>
    public static double? EstimatedStrength(Town town)
    {
        if (StageIndex(town.Enemy.Stage) < StageIndex(ThreatStage.Probing)) return null;
        return Math.Max(10, Js.Round(town.Enemy.Strength / 10) * 10);
    }

    public static void Daily(Town town, double director, bool autoMayor, bool enemy = true)
    {
        FinishDefenses(town);
        var en = town.Enemy;
        if (!enemy || !IsActive(town)) return;

        // Living off a sacked town's tribute, the warband grows slowly.
        if (en.Stage != ThreatStage.Marching) en.Strength += Growth(town) * director * (Saga.Restrained(town) ? 0.25 : 1) * Seasons.Warband(town);
        OutpostFalls(town);
        Advance(town);
        if (en.Stage == ThreatStage.Raids && RaidTonight(town)) Raid(town);
        if (en.Stage == ThreatStage.Marching && en.ArrivalTick != null && town.Tick >= en.ArrivalTick) Siege(town);
        else if (en.Stage != ThreatStage.Marching && town.Tick - en.LastSignTick >= 4 * SimTime.HoursPerDay && town.Rng.Chance(0.35)) Sign(town);
        if (autoMayor) Fortify(town);
    }

    private static void Advance(Town town)
    {
        var e = town.Content.Enemy;
        var en = town.Enemy;
        void Next(ThreatStage s)
        {
            en.Stage = s;
            en.LastSignTick = town.Tick;
        }
        // Every stage is announced before it bites, so nothing arrives without a tell.
        if (en.Stage == ThreatStage.Quiet && en.Strength >= e.Stages.Omens)
        {
            Next(ThreatStage.Omens);
            // The first omen is news, not a call to act, so it goes to the feed without stopping the clock.
            town.Log(LedgerCategory.War, Importance.Notable, e.Omens[0]);
            en.RecentSigns.Add(e.Omens[0]);
        }
        else if (en.Stage == ThreatStage.Omens && en.Strength >= e.Stages.Probing)
        {
            Next(ThreatStage.Probing);
            Discovery.Meet(town, Discovery.Warband, Discovery.Reported);
            SpawnOutpost(town);
        }
        else if (en.Stage == ThreatStage.Probing && en.Strength >= e.Stages.Raids)
        {
            Next(ThreatStage.Raids);
            en.NextRaidTick = town.Tick + town.Rng.Range(2, 4) * SimTime.HoursPerDay;
            town.Log(LedgerCategory.War, Importance.Major, $"{Cap(e.Name)} has grown bold. Its raiders will come for the outlying farms next.");
        }
        else if (en.Stage == ThreatStage.Raids && !Saga.Restrained(town) && en.Strength >= e.Stages.March && en.Strength >= Defense(town, allies: false) * MarchConfidence(town))
        {
            Next(ThreatStage.Marching);
            int march = e.MarchDays + Seasons.MarchDays(town);
            en.ArrivalTick = town.Tick + march * SimTime.HoursPerDay;
            en.Approach = Garrison.Approach(town, en.Sieges);
            town.Log(
                LedgerCategory.War,
                Importance.Major,
                $"{Cap(e.Name)} is on the march! {e.Leader} leads some {Js.Str(Js.Round(en.Strength / 10) * 10)} warriors toward {town.Name}. " +
                $"Scouts say they come at the {Garrison.Place(town, en.Approach)}, and will reach it in {march} days.");
        }
    }

    /// <summary>The share of the town's defence the warband must match to march. A warband kept waiting too long marches anyway, so every campaign reaches its Last Stand.</summary>
    public static double MarchConfidence(Town town)
    {
        var e = town.Content.Enemy;
        double wane = 1 - e.MarchWanePerDay * Math.Max(0, town.Day - e.MarchWaneAfter);
        return e.MarchConfidence * Math.Max(Math.Min(1, e.MarchWaneFloor), wane);
    }

    private static void Sign(Town town)
    {
        var e = town.Content.Enemy;
        var en = town.Enemy;
        IReadOnlyList<string> pool = en.Stage == ThreatStage.Omens ? e.Omens : en.Stage is ThreatStage.Probing or ThreatStage.Raids ? e.Probing : [];
        var fresh = pool.Where(p => !en.RecentSigns.Contains(p)).ToList();
        if (fresh.Count == 0) return;
        en.LastSignTick = town.Tick;
        string text = fresh[town.Rng.Range(0, fresh.Count)];
        en.RecentSigns.Add(text);
        if (en.RecentSigns.Count > 3) en.RecentSigns.RemoveAt(0);
        town.Log(LedgerCategory.War, Importance.Notable, text);
        // A caravan that never arrives leaves the market short.
        if (text.Contains("caravan", StringComparison.Ordinal)) town.Market.Seed(Good.Food, -Math.Floor(town.Market.Stock(Good.Food) * 0.3));
    }

    private static void SpawnOutpost(Town town)
    {
        var e = town.Content.Enemy;
        town.Enemy.Outpost = true;
        var camp = new SiteState(e.Outpost);
        town.Sites.Add(camp);
        Rumors.Hear(town, camp, Rumors.Occupants(town.Content, e.Outpost), e.Outpost.Tier);
        town.Log(
            LedgerCategory.War,
            Importance.Major,
            $"Scouts bring word: {e.Name} has raised {e.Outpost.Name} two days from town. While it stands, the warband grows faster. Heroes could burn it.");
    }

    private static void OutpostFalls(Town town)
    {
        var e = town.Content.Enemy;
        var camp = town.Sites.FirstOrDefault(s => s.Def.Id == e.Outpost.Id);
        if (camp == null || !camp.Cleared || !town.Enemy.Outpost) return;
        town.Enemy.Outpost = false;
        double lost = Js.Round(town.Enemy.Strength * e.OutpostClearedLoss);
        town.Enemy.Strength -= lost;
        town.Log(LedgerCategory.War, Importance.Major, $"With {e.Outpost.Name} in ashes, {e.Name} loses some {Js.Str(lost)} warriors to desertion and the sword.");
        // The warband falls back a stage if it has shrunk below it.
        if (town.Enemy.Stage == ThreatStage.Raids && town.Enemy.Strength < e.Stages.Raids) town.Enemy.Stage = ThreatStage.Probing;
    }

    private static void Raid(Town town)
    {
        var e = town.Content.Enemy;
        var en = town.Enemy;
        var rng = town.Rng;
        en.Raids++;
        en.RaidWaited = 0;
        en.NextRaidTick = town.Tick + (int)Math.Round(rng.Range(e.RaidEveryDays[0], e.RaidEveryDays[1] + 1) * Storyteller.RaidEvery(town) / Seasons.Warband(town)) * SimTime.HoursPerDay;
        double raiders = Math.Max(4, Js.Round(en.Strength * e.RaidShare * (0.7 + rng.Next() * 0.6) * Storyteller.RaidStrength(town) * Frontier.Lure(town)));
        var home = town.Heroes.Where(a => !a.IsAway).ToList();
        double local = LocalDefense(town);
        var party = Lairs.AwayParty(town);
        string timing = party != null && Lairs.AwayShare(town) >= town.Content.Lairs.SoftShare ? $", while the {party.Name} are away" : "";

        // They strike outside the walls: the farm or camp farthest from the town hall, or failing that whatever stands beyond the stone.
        var target = Garrison.Unguarded(town, town.Buildings.Where(b => b.Complete && Exposed(town, b)).ToList())
            .OrderByDescending(b => b.Def.Zone == Zone.Farmland || b.Def.Id == "lumber_camp" ? 1 : 0)
            .ThenByDescending(b => Distance(town, b.LotId))
            .ThenBy(b => b.Id)
            .FirstOrDefault();
        if (!WorthRaiding(town, target, raiders))
        {
            // Nothing out there worth the blood: they watch the walls and ride off.
            if (rng.Chance(0.3)) town.Log(LedgerCategory.War, Importance.Routine, $"Riders of {e.Name} look the town over from the treeline at dusk, and turn back.");
            return;
        }
        string where = target!.Name;
        Passage.Alarm(town, target);
        local *= Garrison.FlagFactor(town, target) * Elements.Watch(town, out string fog);
        if (Founding.Flooded(town, town.Tick))
        {
            local *= 1 + Founding.Of(town).Tide!.Raid;
            timing += ", wading the causeway against the tide";
        }
        if (fog != "") timing += fog;
        if (Garrison.Covered(town, target)) timing += ", where the watch had planted its flag";

        if (local >= raiders)
        {
            en.Strength = Math.Max(0, en.Strength - raiders * 0.5);
            var champion = home.OrderByDescending(x => x.Hero!.Level).ThenBy(x => x.Id).FirstOrDefault();
            string by = champion != null ? $"{champion.Name} and the watch" : "the watch";
            town.Log(LedgerCategory.War, Importance.Notable, $"Some {Js.Str(raiders)} raiders fall on {where} in the night{timing}, and {by} drive them off.", champion != null ? [champion.Id] : []);
            town.Tension += 8;
            return;
        }
        string text = $"Some {Js.Str(raiders)} raiders fall on {where} in the night{timing}";
        var victims = new List<int>();
        {
            double loot = Math.Floor(target.Cash * 0.6);
            target.Cash -= loot;
            foreach (var (g, q) in target.Stock.ToList()) target.Stock[g] = Math.Floor(q * 0.3);
            text += loot > 0 ? $", carrying off the stores and {Js.Str(loot)} gold" : ", carrying off the stores";
            var workers = target.Workers.Select(town.Agent).Where(a => !a.Departed).ToList();
            if (workers.Count > 0 && rng.Chance(town.Content.Enemy.Defense.RaidKill))
            {
                var v = workers[rng.Range(0, workers.Count)];
                Kill(town, v);
                victims.Add(v.Id);
                text += $". {v.Name} dies defending it";
            }
        }
        // They may drag the hands off to their war camp, to be rescued in time or lost.
        var camp = town.Sites.FirstOrDefault(x => x.Def.Id == e.Outpost.Id);
        text += Captives.Take(town, camp, target);
        bool burned = false;
        if (rng.Chance(town.Content.Enemy.Defense.RaidBurn) && Blows.Burns(target))
        {
            Blows.Wreck(town, target, 0.4 + rng.Next() * 0.3);
            text += $". They fire {target.Name} as they go";
            burned = true;
        }
        town.Log(LedgerCategory.War, Importance.Major, $"{text}. The watch arrives too late.", victims);
        if (burned) Fires.Spread(town, target);
        town.Tension += 25 + victims.Count * 10;
        town.Mandate = Math.Max(0, town.Mandate - 4);
    }

    /// <summary>The watch and the heroes at home against raiders tonight, behind whatever walls there are.</summary>
    public static double LocalDefense(Town town, bool roll = true)
    {
        var d = town.Content.Enemy.Defense;
        var home = town.Heroes.Where(a => !a.IsAway);
        return (Garrison.NightWatch(town) * d.Watchman * Planning.WatchFactor(town) + home.Sum(a => a.Hero!.Level) * d.HeroLevel + Garrison.DutyDefense(town) + Army.RaidDefense(town)) * Fortification(town) * (roll ? 0.7 + town.Rng.Next() * 0.6 : 1);
    }

    /// <summary>Outside the walls (or there are none yet), or out in the fields: what raiders can reach.</summary>
    public static bool Exposed(Town town, Building b) =>
        b.Def.Zone == Zone.Farmland || b.Def.Id == "lumber_camp" || WallLines.Outside(town, town.Lot(b.LotId).X, town.Lot(b.LotId).Y);

    /// <summary>
    /// Raiders don't throw themselves away: they go only for a building beyond the walls, and only when they
    /// expect to beat the watch and the heroes they would meet there.
    /// </summary>
    public static bool WorthRaiding(Town town, Building? target, double raiders) =>
        target != null && Exposed(town, target) && raiders >= LocalDefense(town, roll: false) * Garrison.FlagFactor(town, target) * town.Content.Enemy.Defense.RaidNerve;

    /// <summary>
    /// Raiders pick their night (GDD rule E8): with the heroes home they hang back a day or three; once
    /// most of the heroes' strength has marched out, they come early.
    /// </summary>
    public static bool RaidTonight(Town town)
    {
        var en = town.Enemy;
        var l = town.Content.Lairs;
        // A town paying the warlord's tribute is left alone.
        if (en.NextRaidTick == null || Saga.Restrained(town)) return false;
        bool away = Lairs.AwayShare(town) >= l.SoftShare;
        if (town.Tick >= en.NextRaidTick)
        {
            // An island's raiders wait for a night the causeway lies bare, as long as they wait for anything.
            if (Founding.Flooded(town, town.Tick) && en.RaidWaited < l.MaxWaitDays)
            {
                en.RaidWaited++;
                if (en.RaidWaited == 1) town.Log(LedgerCategory.War, Importance.Notable, $"Riders of {town.Content.Enemy.Name} reach the shore at dusk to find the causeway under the tide. They wait for the ebb.");
                return false;
            }
            if (away || en.RaidWaited >= l.MaxWaitDays) return true;
            en.RaidWaited++;
            if (en.RaidWaited == 1) town.Log(LedgerCategory.War, Importance.Notable, $"Riders of {town.Content.Enemy.Name} watch the gate from the treeline, counting the heroes at home. They do not come tonight.");
            return false;
        }
        return away && town.Tick >= en.NextRaidTick - 2 * SimTime.HoursPerDay;
    }

    private static void Siege(Town town)
    {
        var e = town.Content.Enemy;
        var en = town.Enemy;
        var rng = town.Rng;
        en.Sieges++;
        var lines = new List<string>();
        Garrison.Sync(town);
        double def = Defense(town);
        double startDef = def;
        int breaches = 0;
        int towers = town.Defenses.Towers;
        lines.Add(
            $"{Cap(e.Name)}, some {Js.Str(Js.Round(en.Strength))} strong, surrounds {town.Name}. " +
            $"The defenders muster {Js.Str(Js.Round(startDef))} strong" +
            (town.Defenses.Palisade == PalisadeState.Built ? " behind the palisade" : " with no wall to hide behind") +
            (towers != 0 ? $" and {towers} {(towers == 1 ? "tower" : "towers")}." : "."));
        var record = Battles.Begin(town, "warband", "warband", e.Name, "orcs", e.Leader, en.Approach, en.Strength, startDef);
        def = Math.Max(0, def - Loyalty.Betray(town, e.Name, null, lines));
        double went = AllyCalls.Betray(town, e.Name, lines);
        def = Math.Max(0, def - went);
        en.Strength += went;
        double gate = Cannons.Wagons(town, "warband", en.Sieges - 1, Garrison.WaveSection(town, en.Approach, 1), lines, record);
        for (int w = 1; w <= e.SiegeWaves && breaches < e.BreachesToFall; w++)
        {
            double wave = (en.Strength / (e.SiegeWaves - w + 1)) * (0.8 + rng.Next() * 0.4);
            int at = Garrison.WaveSection(town, en.Approach, w);
            double hold = (def / (e.SiegeWaves - w + 1)) * (0.8 + rng.Next() * 0.4) * Garrison.SectionFactor(town, at) * gate;
            double shot = Cannons.Volley(town, "warband", "", wave, w, lines);
            shot = Traps.Spring(town, "warband", shot, at, lines, record);
            en.Strength -= wave - shot;
            wave = shot;
            double wading = Garrison.Wade(town, wave);
            if (hold >= wading)
            {
                def -= wading * 0.15;
                en.Strength -= wave;
                Battles.Wave(record, at, wave, false);
                lines.Add($"Wave {w}: {Js.Str(Js.Round(wave))} orcs hit the {Garrison.Place(town, at)} and break against it.");
            }
            else
            {
                def -= wading * 0.35;
                en.Strength -= wave * 0.5;
                breaches++;
                Battles.Wave(record, at, wave, true);
                lines.Add($"Wave {w}: {Js.Str(Js.Round(wave))} orcs breach the {Garrison.Place(town, at)}. Fighting spills into the streets.");
            }
        }

        // The army's soldiers stand in the front rank and fall first; the militia pays for the rest.
        var dead = Army.Bloodied(town, startDef - def, startDef);
        AllyCalls.Fought(town, breaches < e.BreachesToFall, (startDef - def) / Math.Max(1, startDef));
        int fallen = (int)Math.Min(town.Workers.Count, Math.Max(0, Js.Round(((startDef - def) / Math.Max(1, Fortification(town))) / 12) - dead.Count));
        var workers = town.Workers;
        for (int i = 0; i < fallen; i++)
        {
            int k = rng.Range(0, workers.Count);
            var v = workers[k];
            workers.RemoveAt(k);
            Kill(town, v);
            dead.Add(v);
        }
        if (dead.Count > 0) lines.Add($"{Expedition.ListNames(dead.Select(a => a.Name).ToList())} {(dead.Count == 1 ? "falls" : "fall")} on the walls.");
        record.Fallen.AddRange(dead.Select(a => a.Id));
        town.Watch = Math.Max(0, town.Watch - breaches * 2);
        Mayoralty.Breached(town, breaches, e.Name);

        if (breaches >= e.BreachesToFall)
        {
            record.End = BattleEnd.Fell;
            // Without the saga there is no yoke, and no warband to raise as a menace.
            if (town.Saga.Enabled && Saga.Sack(town, Menaces.Warband(town), lines))
            {
                record.End = BattleEnd.Sacked;
                WarlordInCamp(town);
                Chronicle(town, lines, ExpeditionOutcome.Wiped, []);
                town.Log(LedgerCategory.War, Importance.Major, $"{town.Name} is sacked. {e.Leader}'s warband carries off the treasury and leaves the town under its yoke. (Chronicle {town.Chronicles.Count})", dead.Select(a => a.Id).ToList());
                town.Tension += 80;
                return;
            }
            lines.Add($"The defence collapses. {e.Leader} raises the red banner over the town hall.");
            en.Stage = ThreatStage.Victorious;
            Saga.Fall(town, $"{e.Leader}'s banner flies over the town hall on day {town.Day}.");
            town.Mandate = 0;
            town.Book(TreasuryAccount.Discrepancies, -town.Treasury);
            Chronicle(town, lines, ExpeditionOutcome.Wiped, []);
            town.Log(LedgerCategory.War, Importance.Major, $"{town.Name} falls. {e.Leader}'s warband sacks the town and empties the treasury. (Chronicle {town.Chronicles.Count})", dead.Select(a => a.Id).ToList());
            town.Tension += 100;
            return;
        }

        // The walls held; now the warlord comes in person, and only heroes can meet him.
        var champions = town.Heroes
            .Where(a => !a.IsAway && a.Hero!.Hp > 0)
            .OrderByDescending(x => x.Hero!.Duty == HeroDuty.Walls)
            .ThenByDescending(x => x.Hero!.Level)
            .ThenBy(x => x.Id)
            .Take(4)
            .ToList();
        bool slain = false;
        if (champions.Count > 0)
        {
            lines.Add($"The walls hold. {e.Leader} comes to the breach himself, and {Expedition.ListNames(champions.Select(a => a.GivenName).ToList())} go out to meet him.");
            var battle = new SiteDef { Id = "last_stand", Name = "the walls", Tier = 0, Distance = 0, Encounters = [], Boss = e.WarlordBattle, Loot = [0, 0] };
            var result = Expedition.Run(rng, town.Content, battle, champions);
            record.Champions.AddRange(champions.Select(c => c.Id));
            record.ChampionsFell.AddRange(result.Dead.Order());
            lines.AddRange(result.Lines.Skip(1).Where(l =>
                !l.StartsWith("At last", StringComparison.Ordinal) &&
                !l.Contains("is cleared. The hoard", StringComparison.Ordinal) &&
                !l.StartsWith("No one comes back", StringComparison.Ordinal)));
            foreach (int id in result.Dead)
            {
                var h = town.Agent(id);
                h.Dead = true;
                Population.Depart(town, h);
                dead.Add(h);
            }
            foreach (var c in champions)
            {
                if (result.Dead.Contains(c.Id)) continue;
                Notoriety.Deed(town, c, "slay", 10);
                c.Hero!.Kills += result.Kills.Get(c.Id, 0);
            }
            slain = result.Outcome == ExpeditionOutcome.Cleared;
        }
        else
        {
            lines.Add($"The walls hold, but no hero is in town to face {e.Leader}.");
        }

        if (slain)
        {
            lines.Add($"{e.Leader} is dead. The warband breaks and scatters into the hills.");
            record.End = BattleEnd.Slain;
            en.Stage = ThreatStage.Defeated;
            en.Strength = 0;
            town.Mandate = Math.Min(100, town.Mandate + 25);
            Chronicle(town, lines, ExpeditionOutcome.Cleared, champions);
            town.Log(
                LedgerCategory.War,
                Importance.Major,
                $"The Last Stand is won! {e.Leader} falls before the walls of {town.Name}, and the warband is broken. (Chronicle {town.Chronicles.Count})",
                champions.Select(a => a.Id).ToList());
            Saga.WarlordSlain(town);
        }
        else
        {
            lines.Add($"{e.Leader} sounds the retreat. The warband limps back to the hills to lick its wounds.");
            record.End = champions.Count > 0 ? BattleEnd.Repelled : BattleEnd.Held;
            en.Stage = ThreatStage.Raids;
            en.ArrivalTick = null;
            en.Strength = Math.Max(town.Content.Enemy.Stages.Probing, en.Strength * 0.6);
            en.NextRaidTick = town.Tick + 6 * SimTime.HoursPerDay;
            Chronicle(town, lines, ExpeditionOutcome.Retreated, champions);
            town.Log(
                LedgerCategory.War,
                Importance.Major,
                $"{town.Name} survives the siege, but {e.Leader} escapes. He will be back. (Chronicle {town.Chronicles.Count})",
                champions.Select(a => a.Id).ToList());
        }
        town.Tension += 60 + dead.Count * 5;
    }

    /// <summary>
    /// The warband has sacked the town and holds it under the yoke. It goes home with its plunder; the
    /// warlord waits in his war camp, raised again if it was burned, where heroes can go for him.
    /// </summary>
    public static void WarlordInCamp(Town town)
    {
        var e = town.Content.Enemy;
        var en = town.Enemy;
        en.Stage = ThreatStage.Raids;
        en.ArrivalTick = null;
        en.NextRaidTick = town.Tick + 10 * SimTime.HoursPerDay;
        en.Strength *= 0.6;
        var camp = town.Sites.FirstOrDefault(s => s.Def.Id == e.Outpost.Id);
        if (camp == null)
        {
            camp = new SiteState(e.Outpost);
            town.Sites.Add(camp);
        }
        camp.Cleared = false;
        camp.ThoughtCleared = false;
        camp.Def = camp.Def with { Boss = e.WarlordBattle with { Text = $"{e.Leader} in his great tent, his honour guard around him" }, Tier = Math.Max(camp.Def.Tier, 3) };
        en.Outpost = true;
        Rumors.Hear(town, camp, Rumors.Occupants(town.Content, camp.Def), camp.Def.Tier);
    }

    /// <summary>Heroes burned the war camp with the warlord in it: the warband is broken.</summary>
    public static void WarlordFalls(Town town, SiteState camp)
    {
        var e = town.Content.Enemy;
        var en = town.Enemy;
        if (!IsActive(town) || Menaces.WarlordEscapes(town, camp)) return;
        en.Stage = ThreatStage.Defeated;
        en.Strength = 0;
        en.Outpost = false;
        camp.Def = camp.Def with { Boss = e.Outpost.Boss };
        town.Mandate = Math.Min(100, town.Mandate + 25);
        town.Log(LedgerCategory.War, Importance.Major, $"{e.Leader} is dead, cut down in his own war camp. The warband breaks and scatters into the hills.");
        Saga.WarlordSlain(town);
    }

    private static void Chronicle(Town town, List<string> lines, ExpeditionOutcome outcome, List<Agent> champions)
    {
        town.Chronicles.Add(new Chronicle(
            town.Chronicles.Count + 1,
            $"Defenders of {town.Name}",
            $"the walls of {town.Name}",
            champions.Select(a => a.Describe()).ToList(),
            SimTime.DayOf(town.Tick),
            SimTime.DayOf(town.Tick),
            outcome,
            lines));
    }

    /// <summary>The player's lever: order a palisade or a watchtower, paid from the treasury.</summary>
    public static WorkSpec Spec(Town town, DefenseWork what)
    {
        var e = town.Content.Enemy;
        return what switch
        {
            DefenseWork.Palisade => e.Palisade,
            DefenseWork.Tower => e.Tower,
            DefenseWork.StoneWall => e.StoneWall,
            DefenseWork.Gatehouse => town.Content.Garrison.GatehouseWork,
            DefenseWork.Moat => town.Content.Garrison.MoatWork,
            DefenseWork.Barracks => town.Content.Garrison.BarracksWork,
            _ => e.CityWall,
        };
    }

    /// <summary>Why the town can't order this work now, or null when it can.</summary>
    public static string? BuildBlocked(Town town, DefenseWork what)
    {
        var d = town.Defenses;
        var spec = Spec(town, what);
        switch (what)
        {
            case DefenseWork.Palisade when d.Palisade != PalisadeState.None:
                return d.Palisade == PalisadeState.Built ? "built" : "under way";
            case DefenseWork.Tower when d.Palisade != PalisadeState.Built:
                return "needs the palisade";
            case DefenseWork.Tower when d.Towers + d.TowersBuilding.Count >= Garrison.MaxTowers(town):
                return $"the wall carries {Garrison.MaxTowers(town)}";
            case DefenseWork.StoneWall when d.StoneWall != PalisadeState.None:
                return d.StoneWall == PalisadeState.Built ? "built" : "under way";
            case DefenseWork.CityWall when d.CityWall != PalisadeState.None:
                return d.CityWall == PalisadeState.Built ? "built" : "under way";
            case DefenseWork.CityWall when d.StoneWall != PalisadeState.Built:
                return "needs the stone walls";
            case DefenseWork.Gatehouse when d.Gatehouse != PalisadeState.None:
                return d.Gatehouse == PalisadeState.Built ? "built" : "under way";
            case DefenseWork.Moat when d.Moat != PalisadeState.None:
                return d.Moat == PalisadeState.Built ? "dug" : "under way";
            case DefenseWork.Barracks when d.Barracks + d.BarracksBuilding.Count >= spec.Max:
                return $"the walls hold {spec.Max}";
            case DefenseWork.Gatehouse or DefenseWork.Moat or DefenseWork.Barracks when d.StoneWall != PalisadeState.Built:
                return "needs the stone walls";
        }
        if (spec.Tier != "" && !City.Reached(town, spec.Tier)) return $"needs the town to be a {City.TierName(town, spec.Tier).ToLowerInvariant()}";
        // Every wall is drawn by hand, and needs a gate put on its line, before the masons start.
        if (IsWall(what) && WallLines.Plan(town, WallLines.RingOf(what)) is not { Radii.Length: > 0 }) return "draw its line first";
        if (IsWall(what) && WallLines.Gates(town, WallLines.RingOf(what)).Count == 0) return "needs a gate: place one on the wall's line";
        if (town.Treasury < BuildCost(town, what)) return "not enough gold";
        return null;
    }

    /// <summary>Why a tower or barracks can't go on the wall at a point, or null (<see cref="BuildOnWall"/>).</summary>
    public static string? OnWallBlocked(Town town, DefenseWork what, double x, double y)
    {
        if (what is not (DefenseWork.Tower or DefenseWork.Barracks)) return "that is not built on the wall";
        if (BuildBlocked(town, what) is { } why) return why;
        int ring = town.Defenses.CityWall == PalisadeState.Built ? 1 : 0;
        double a = Math.Atan2(y - Town.Center, x - Town.Center);
        var (px, py) = WallLines.Point(town, ring, a);
        if (Js.Hypot(px - x, py - y) > WallLines.Rules(town).Snap) return "it goes on the wall";
        int section = Garrison.SectionAt(town, x, y);
        var d = town.Defenses;
        if (what == DefenseWork.Tower && Enumerable.Range(0, d.Towers + d.TowersBuilding.Count).Any(i => Garrison.TowerSection(town, i) == section)) return "a tower stands on that stretch";
        if (what == DefenseWork.Barracks && Enumerable.Range(0, d.Barracks + d.BarracksBuilding.Count).Any(i => Garrison.BarracksSection(town, i) == section)) return "barracks stand on that stretch";
        return null;
    }

    /// <summary>The player's tower or wall barracks, put on the stretch of wall they clicked. Returns why not, or null.</summary>
    public static string? BuildOnWall(Town town, DefenseWork what, double x, double y)
    {
        if (OnWallBlocked(town, what, x, y) is { } why) return why;
        int section = Garrison.SectionAt(town, x, y);
        var d = town.Defenses;
        var list = what == DefenseWork.Tower ? d.TowerAt : d.BarracksAt;
        int built = what == DefenseWork.Tower ? d.Towers + d.TowersBuilding.Count : d.Barracks + d.BarracksBuilding.Count;
        // Sections already standing by the old order keep their place; this one comes next.
        while (list.Count < built) list.Add(what == DefenseWork.Tower ? Garrison.TowerSection(town, list.Count) : Garrison.BarracksSection(town, list.Count));
        list.Add(section);
        if (Build(town, what)) return null;
        list.RemoveAt(list.Count - 1);
        return "the steward holds it for your word";
    }

    public static bool Build(Town town, DefenseWork what)
    {
        var d = town.Defenses;
        var spec = Spec(town, what);
        if (BuildBlocked(town, what) != null) return false;
        if (Stewards.Intercept(town, "defence", new StewardAsk { Area = "defence", Kind = "defense", Title = $"Build {Stewards.Lower(what.ToString())} · {Js.Str(BuildCost(town, what))}g", Why = $"• Threat: {town.Enemy.Stage.ToString().ToLowerInvariant()}\n• The walls are the town's first answer", I1 = (int)what, Cost = BuildCost(town, what) })) return false;
        if (IsWall(what))
        {
            // A wall's timber and stone come off the market as it rises (Haul), so it waits when the market runs dry.
            town.Book(TreasuryAccount.Defenses, -spec.Cost * Land(town, what));
        }
        else
        {
            town.Book(TreasuryAccount.Defenses, -BuildCost(town, what));
            town.Market.Buy(Good.Timber, spec.Timber);
            town.Market.Buy(Good.Stone, spec.Stone);
        }
        int ready = town.Tick + spec.Days * SimTime.HoursPerDay;
        switch (what)
        {
            case DefenseWork.Palisade:
                d.Palisade = PalisadeState.Building;
                d.PalisadeReadyTick = ready;
                town.Log(LedgerCategory.War, Importance.Notable, $"Work begins on a palisade around {town.Name}. It will take {spec.Days} days.");
                break;
            case DefenseWork.Tower:
                d.TowersBuilding.Add(ready);
                town.Log(LedgerCategory.War, Importance.Notable, d.StoneWall == PalisadeState.Built
                    ? $"Masons start on a stone tower on the {town.Content.Garrison.Sections[Garrison.TowerSection(town, d.Towers + d.TowersBuilding.Count - 1)]} wall."
                    : "Carpenters start on a watchtower on the palisade.");
                break;
            case DefenseWork.Gatehouse:
                d.Gatehouse = PalisadeState.Building;
                d.GatehouseReadyTick = ready;
                town.Log(LedgerCategory.War, Importance.Notable, $"Masons begin a gatehouse over the south road, with a portcullis and murder holes. It will take {spec.Days} days.");
                break;
            case DefenseWork.Moat:
                d.Moat = PalisadeState.Building;
                d.MoatReadyTick = ready;
                town.Log(LedgerCategory.War, Importance.Notable, $"Diggers start a moat round the stone walls, to be filled from the river. It will take {spec.Days} days.");
                break;
            case DefenseWork.Barracks:
                d.BarracksBuilding.Add(ready);
                town.Log(LedgerCategory.War, Importance.Notable, $"Masons hollow out barracks in the thickness of the {town.Content.Garrison.Sections[Garrison.BarracksSection(town, d.Barracks + d.BarracksBuilding.Count - 1)]} wall, bunks for {town.Content.Garrison.BarracksHouses} of the watch.");
                break;
            case DefenseWork.StoneWall:
                d.StoneWall = PalisadeState.Building;
                d.StoneWallReadyTick = ready;
                town.Log(LedgerCategory.War, Importance.Notable, $"Masons begin stone walls round the old town. It will take {spec.Days} days.");
                break;
            default:
                d.CityWall = PalisadeState.Building;
                d.CityWallReadyTick = ready;
                town.Log(LedgerCategory.War, Importance.Notable, $"Work begins on the city wall, a ring of stone round the old town and the streets about it. It will take {spec.Days} days.");
                break;
        }
        return true;
    }

    public static double BuildCost(Town town, DefenseWork what)
    {
        var spec = Spec(town, what);
        double land = Land(town, what);
        return Js.Round(spec.Cost * land + town.Market.Quote(Good.Timber, spec.Timber * land) + town.Market.Quote(Good.Stone, spec.Stone * land));
    }

    /// <summary>What the lie of the land does to a wall's cost (<see cref="WallLines.Terrain(Town, int)"/>); 1 for any other work.</summary>
    public static double Land(Town town, DefenseWork what) => IsWall(what) ? WallLines.Terrain(town, WallLines.RingOf(what)) : 1;

    /// <summary>The walls themselves, raised along the line the player drew, as the market has their timber and stone.</summary>
    public static bool IsWall(DefenseWork w) => w is DefenseWork.Palisade or DefenseWork.StoneWall or DefenseWork.CityWall;

    /// <summary>
    /// A day's timber and stone for each wall going up, off the market's stock. What it can't get holds the work back: the
    /// day's share of the work waits, so the wall finishes later, and the alert strip says so.
    /// </summary>
    private static void Haul(Town town)
    {
        var d = town.Defenses;
        bool shortToday = false;
        foreach (var (work, state, ready) in new[] { (DefenseWork.Palisade, d.Palisade, d.PalisadeReadyTick), (DefenseWork.StoneWall, d.StoneWall, d.StoneWallReadyTick), (DefenseWork.CityWall, d.CityWall, d.CityWallReadyTick) })
        {
            // A work held back indefinitely (ready at int.MaxValue) draws nothing and can't slip further.
            if (state != PalisadeState.Building || ready is not int tick || tick == int.MaxValue) continue;
            var spec = Spec(town, work);
            double share = 1, paid = 0, land = Land(town, work);
            foreach (var (g, total) in new[] { (Good.Timber, spec.Timber * land), (Good.Stone, spec.Stone * land) })
            {
                if (total <= 0) continue;
                double need = total / Math.Max(1, spec.Days);
                var (qty, cost) = town.Market.Take(g, need);
                paid += cost;
                share = Math.Min(share, qty / need);
            }
            if (paid > 0) town.Book(TreasuryAccount.Defenses, -paid);
            if (share >= 1 - 1e-9) continue;
            shortToday = true;
            int late = (int)Math.Min(Math.Round((1 - share) * SimTime.HoursPerDay), int.MaxValue - (long)tick);
            switch (work)
            {
                case DefenseWork.Palisade: d.PalisadeReadyTick = tick + late; break;
                case DefenseWork.StoneWall: d.StoneWallReadyTick = tick + late; break;
                default: d.CityWallReadyTick = tick + late; break;
            }
        }
        if (shortToday && d.WallsShort < town.Day - 1)
            town.Log(LedgerCategory.War, Importance.Notable, "Work on the walls slows: the market is short of the timber and stone they need.");
        if (shortToday) d.WallsShort = town.Day;
    }

    private static void FinishDefenses(Town town)
    {
        var d = town.Defenses;
        // Walls raised before gates were placed by hand keep the south gate they always had.
        if (WallLines.Begun(town, 0)) WallLines.EnsureGate(town, 0);
        if (WallLines.Begun(town, 1)) WallLines.EnsureGate(town, 1);
        Haul(town);
        if (d.Palisade == PalisadeState.Building && d.PalisadeReadyTick != null && town.Tick >= d.PalisadeReadyTick)
        {
            d.Palisade = PalisadeState.Built;
            town.Log(LedgerCategory.War, Importance.Notable, $"The palisade is finished. {town.Name} sleeps a little easier.");
        }
        if (d.StoneWall == PalisadeState.Building && d.StoneWallReadyTick != null && town.Tick >= d.StoneWallReadyTick)
        {
            d.StoneWall = PalisadeState.Built;
            town.Log(LedgerCategory.War, Importance.Notable, $"The stone walls round the old town are finished. Whatever stands outside them is the raiders' first mark.");
        }
        if (d.CityWall == PalisadeState.Building && d.CityWallReadyTick != null && town.Tick >= d.CityWallReadyTick)
        {
            d.CityWall = PalisadeState.Built;
            town.Log(LedgerCategory.War, Importance.Notable, $"The city wall is closed. {town.Name} is a walled city.");
        }
        var done = d.TowersBuilding.Where(t => town.Tick >= t).ToList();
        if (done.Count > 0)
        {
            d.Towers += done.Count;
            d.TowersBuilding = d.TowersBuilding.Where(t => town.Tick < t).ToList();
            town.Log(LedgerCategory.War, Importance.Routine, d.StoneWall == PalisadeState.Built ? "A new stone tower stands on the walls." : "A new watchtower stands on the palisade.");
        }
        if (d.Gatehouse == PalisadeState.Building && d.GatehouseReadyTick != null && town.Tick >= d.GatehouseReadyTick)
        {
            d.Gatehouse = PalisadeState.Built;
            town.Log(LedgerCategory.War, Importance.Notable, "The gatehouse is finished. Its portcullis drops at dusk.");
        }
        if (d.Moat == PalisadeState.Building && d.MoatReadyTick != null && town.Tick >= d.MoatReadyTick)
        {
            d.Moat = PalisadeState.Built;
            town.Log(LedgerCategory.War, Importance.Notable, "The river is let into the new moat. The walls stand in water now.");
        }
        var bunks = d.BarracksBuilding.Where(t => town.Tick >= t).ToList();
        if (bunks.Count > 0)
        {
            d.Barracks += bunks.Count;
            d.BarracksBuilding = d.BarracksBuilding.Where(t => town.Tick < t).ToList();
            town.Log(LedgerCategory.War, Importance.Routine, "The wall barracks are finished, and the watch moves into the walls.");
        }
    }

    /// <summary>The stand-in mayor fortifies once raids begin.</summary>
    private static void Fortify(Town town)
    {
        int stage = StageIndex(town.Enemy.Stage);
        if (stage < StageIndex(ThreatStage.Probing)) return;
        if (town.Defenses.Palisade == PalisadeState.None && town.Treasury > BuildCost(town, DefenseWork.Palisade) + 150)
        {
            WallLines.StandIn(town, 0);
            Build(town, DefenseWork.Palisade);
        }
        // The palisade's towers; more on the stone come later, among the wall works (Garrison.Daily).
        else if (stage >= StageIndex(ThreatStage.Raids) && town.Defenses.Towers + town.Defenses.TowersBuilding.Count < town.Content.Enemy.Tower.Max && town.Treasury > BuildCost(town, DefenseWork.Tower) + 200) Build(town, DefenseWork.Tower);
    }

    public static void Kill(Town town, Agent a)
    {
        a.Dead = true;
        Population.Depart(town, a);
        foreach (var other in town.Residents) if (other.OpinionOf(a.Id) >= 40) other.Loneliness = Math.Min(100, other.Loneliness + 30);
        var s = town.Seats.FirstOrDefault(x => x.HolderId == a.Id);
        if (s != null) s.HolderId = null;
    }

    public static double Distance(Town town, int lotId)
    {
        var l = town.Lots[lotId];
        return Js.Hypot(l.X - Town.Center, l.Y - Town.Center);
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
