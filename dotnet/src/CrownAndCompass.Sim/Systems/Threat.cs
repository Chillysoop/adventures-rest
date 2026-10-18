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

    /// <summary>The city wall round the four inner wards, once it is a city.</summary>
    CityWall,
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

    /// <summary>The innermost ring the walls leave outside: 1 behind stone walls, 2 behind the city wall, 99 with none.</summary>
    public int OutsideRing => CityWall == PalisadeState.Built ? 2 : StoneWall == PalisadeState.Built ? 1 : 99;
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
        double baseDefense = town.Watch * d.Watchman * Planning.WatchFactor(town) + heroLevels * d.HeroLevel + town.Workers.Count * d.MilitiaPerWorker + Faith.Defense(town) + Factions.Defense(town) + Callings.Defence(town) + Loyalty.Defense(town) + Notoriety.Defense(town) + Decor.Defense(town) + Mercenaries.Defense(town) + Bastions.Defense(town) + (allies ? World.AllyTroops(town) : 0);
        return baseDefense * Fortification(town);
    }

    public static double Fortification(Town town)
    {
        var d = town.Content.Enemy.Defense;
        var w = town.Defenses;
        double walls = w.CityWall == PalisadeState.Built ? d.CityWall
            : w.StoneWall == PalisadeState.Built ? d.StoneWall
            : w.Palisade == PalisadeState.Built ? d.Palisade : 1;
        return walls * (1 + d.PerTower * w.Towers + Planning.Defense(town));
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
            town.Log(LedgerCategory.War, Importance.Major, e.Omens[0]);
            en.RecentSigns.Add(e.Omens[0]);
        }
        else if (en.Stage == ThreatStage.Omens && en.Strength >= e.Stages.Probing)
        {
            Next(ThreatStage.Probing);
            SpawnOutpost(town);
        }
        else if (en.Stage == ThreatStage.Probing && en.Strength >= e.Stages.Raids)
        {
            Next(ThreatStage.Raids);
            en.NextRaidTick = town.Tick + town.Rng.Range(2, 4) * SimTime.HoursPerDay;
            town.Log(LedgerCategory.War, Importance.Major, $"{Cap(e.Name)} has grown bold. Its raiders will come for the outlying farms next.");
        }
        else if (en.Stage == ThreatStage.Raids && !Saga.Restrained(town) && en.Strength >= e.Stages.March && en.Strength >= Defense(town, allies: false) * e.MarchConfidence)
        {
            Next(ThreatStage.Marching);
            int march = e.MarchDays + Seasons.MarchDays(town);
            en.ArrivalTick = town.Tick + march * SimTime.HoursPerDay;
            town.Log(
                LedgerCategory.War,
                Importance.Major,
                $"{Cap(e.Name)} is on the march! {e.Leader} leads some {Js.Str(Js.Round(en.Strength / 10) * 10)} warriors toward {town.Name}. " +
                $"They will reach the walls in {march} days.");
        }
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
        en.NextRaidTick = town.Tick + (int)Math.Round(rng.Range(e.RaidEveryDays[0], e.RaidEveryDays[1] + 1) / Seasons.Warband(town)) * SimTime.HoursPerDay;
        double raiders = Math.Max(4, Js.Round(en.Strength * e.RaidShare * (0.7 + rng.Next() * 0.6)));
        var home = town.Heroes.Where(a => !a.IsAway).ToList();
        double local = LocalDefense(town);
        var party = Lairs.AwayParty(town);
        string timing = party != null && Lairs.AwayShare(town) >= town.Content.Lairs.SoftShare ? $", while the {party.Name} are away" : "";

        // They strike outside the walls: the farm or camp farthest from the town hall, or failing that whatever stands beyond the stone.
        int outside = town.Defenses.OutsideRing;
        var target = town.Buildings
            .Where(b => b.Complete && (b.Def.Zone == Zone.Farmland || b.Def.Id == "lumber_camp" || City.Ring(town.Lot(b.LotId)) >= outside))
            .OrderByDescending(b => b.Def.Zone == Zone.Farmland || b.Def.Id == "lumber_camp" ? 1 : 0)
            .ThenByDescending(b => Distance(town, b.LotId))
            .ThenBy(b => b.Id)
            .FirstOrDefault();
        string where = target != null ? target.Name : "the outlying fields";

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
        if (target != null)
        {
            double loot = Math.Floor(target.Cash * 0.6);
            target.Cash -= loot;
            foreach (var (g, q) in target.Stock.ToList()) target.Stock[g] = Math.Floor(q * 0.3);
            text += loot > 0 ? $", carrying off the stores and {Js.Str(loot)} gold" : ", carrying off the stores";
            var workers = target.Workers.Select(town.Agent).Where(a => !a.Departed).ToList();
            if (workers.Count > 0 && rng.Chance(0.45))
            {
                var v = workers[rng.Range(0, workers.Count)];
                Kill(town, v);
                victims.Add(v.Id);
                text += $". {v.Name} dies defending it";
            }
        }
        town.Log(LedgerCategory.War, Importance.Major, $"{text}. The watch arrives too late.", victims);
        town.Tension += 25 + victims.Count * 10;
        town.Mandate = Math.Max(0, town.Mandate - 4);
    }

    /// <summary>The watch and the heroes at home against raiders tonight, behind whatever walls there are.</summary>
    public static double LocalDefense(Town town)
    {
        var d = town.Content.Enemy.Defense;
        var home = town.Heroes.Where(a => !a.IsAway);
        return (town.Watch * d.Watchman * Planning.WatchFactor(town) + home.Sum(a => a.Hero!.Level) * d.HeroLevel) * Fortification(town) * (0.7 + town.Rng.Next() * 0.6);
    }

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
            if (away || en.RaidWaited >= l.MaxWaitDays) return true;
            en.RaidWaited++;
            if (en.RaidWaited == 1) town.Log(LedgerCategory.War, Importance.Notable, $"Riders of {town.Content.Enemy.Name} watch the gate from the treeline, counting the heroes at home. They do not come tonight.");
            return false;
        }
        return away && town.Tick >= en.NextRaidTick - 2 * SimTime.HoursPerDay;
    }

    private static string Wall(int w) => w == 1 ? "gate" : w == 2 ? "east wall" : "river side";

    private static void Siege(Town town)
    {
        var e = town.Content.Enemy;
        var en = town.Enemy;
        var rng = town.Rng;
        en.Sieges++;
        var lines = new List<string>();
        double def = Defense(town);
        double startDef = def;
        int breaches = 0;
        int towers = town.Defenses.Towers;
        lines.Add(
            $"{Cap(e.Name)}, some {Js.Str(Js.Round(en.Strength))} strong, surrounds {town.Name}. " +
            $"The defenders muster {Js.Str(Js.Round(startDef))} strong" +
            (town.Defenses.Palisade == PalisadeState.Built ? " behind the palisade" : " with no wall to hide behind") +
            (towers != 0 ? $" and {towers} {(towers == 1 ? "tower" : "towers")}." : "."));
        def = Math.Max(0, def - Loyalty.Betray(town, e.Name, null, lines));
        for (int w = 1; w <= e.SiegeWaves && breaches < e.BreachesToFall; w++)
        {
            double wave = (en.Strength / (e.SiegeWaves - w + 1)) * (0.8 + rng.Next() * 0.4);
            double hold = (def / (e.SiegeWaves - w + 1)) * (0.8 + rng.Next() * 0.4);
            if (hold >= wave)
            {
                def -= wave * 0.15;
                en.Strength -= wave;
                lines.Add($"Wave {w}: {Js.Str(Js.Round(wave))} orcs hit the {Wall(w)} and break against it.");
            }
            else
            {
                def -= wave * 0.35;
                en.Strength -= wave * 0.5;
                breaches++;
                lines.Add($"Wave {w}: {Js.Str(Js.Round(wave))} orcs breach the {Wall(w)}. Fighting spills into the streets.");
            }
        }

        // Townsfolk who stood in the militia pay for it.
        int fallen = (int)Math.Min(town.Workers.Count, Js.Round(((startDef - def) / Math.Max(1, Fortification(town))) / 12));
        var dead = new List<Agent>();
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
        town.Watch = Math.Max(0, town.Watch - breaches * 2);

        if (breaches >= e.BreachesToFall)
        {
            if (Saga.Sack(town, Menaces.Warband(town), lines))
            {
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
            .OrderByDescending(x => x.Hero!.Level)
            .ThenBy(x => x.Id)
            .Take(4)
            .ToList();
        bool slain = false;
        if (champions.Count > 0)
        {
            lines.Add($"The walls hold. {e.Leader} comes to the breach himself, and {Expedition.ListNames(champions.Select(a => a.GivenName).ToList())} go out to meet him.");
            var battle = new SiteDef { Id = "last_stand", Name = "the walls", Tier = 0, Distance = 0, Encounters = [], Boss = e.WarlordBattle, Loot = [0, 0] };
            var result = Expedition.Run(rng, town.Content, battle, champions);
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
            case DefenseWork.Tower when d.Towers + d.TowersBuilding.Count >= spec.Max:
                return $"the town has {spec.Max}";
            case DefenseWork.StoneWall when d.StoneWall != PalisadeState.None:
                return d.StoneWall == PalisadeState.Built ? "built" : "under way";
            case DefenseWork.CityWall when d.CityWall != PalisadeState.None:
                return d.CityWall == PalisadeState.Built ? "built" : "under way";
            case DefenseWork.CityWall when d.StoneWall != PalisadeState.Built:
                return "needs the stone walls";
        }
        if (spec.Tier != "" && !City.Reached(town, spec.Tier)) return $"needs the town to be a {City.TierName(town, spec.Tier).ToLowerInvariant()}";
        if (town.Treasury < BuildCost(town, what)) return "not enough gold";
        return null;
    }

    public static bool Build(Town town, DefenseWork what)
    {
        var d = town.Defenses;
        var spec = Spec(town, what);
        if (BuildBlocked(town, what) != null) return false;
        double cost = BuildCost(town, what);
        town.Book(TreasuryAccount.Defenses, -cost);
        town.Market.Buy(Good.Timber, spec.Timber);
        town.Market.Buy(Good.Stone, spec.Stone);
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
                town.Log(LedgerCategory.War, Importance.Notable, "Carpenters start on a watchtower on the palisade.");
                break;
            case DefenseWork.StoneWall:
                d.StoneWall = PalisadeState.Building;
                d.StoneWallReadyTick = ready;
                town.Log(LedgerCategory.War, Importance.Notable, $"Masons begin stone walls round the old town. It will take {spec.Days} days.");
                break;
            default:
                d.CityWall = PalisadeState.Building;
                d.CityWallReadyTick = ready;
                town.Log(LedgerCategory.War, Importance.Major, $"Work begins on the city wall, a ring of stone round the four inner wards. It will take {spec.Days} days.");
                break;
        }
        return true;
    }

    public static double BuildCost(Town town, DefenseWork what)
    {
        var spec = Spec(town, what);
        return Js.Round(spec.Cost + town.Market.Quote(Good.Timber, spec.Timber) + town.Market.Quote(Good.Stone, spec.Stone));
    }

    private static void FinishDefenses(Town town)
    {
        var d = town.Defenses;
        if (d.Palisade == PalisadeState.Building && d.PalisadeReadyTick != null && town.Tick >= d.PalisadeReadyTick)
        {
            d.Palisade = PalisadeState.Built;
            town.Log(LedgerCategory.War, Importance.Notable, $"The palisade is finished. {town.Name} sleeps a little easier.");
        }
        if (d.StoneWall == PalisadeState.Building && d.StoneWallReadyTick != null && town.Tick >= d.StoneWallReadyTick)
        {
            d.StoneWall = PalisadeState.Built;
            town.Log(LedgerCategory.War, Importance.Major, $"The stone walls round the old town are finished. Whatever stands outside them is the raiders' first mark.");
        }
        if (d.CityWall == PalisadeState.Building && d.CityWallReadyTick != null && town.Tick >= d.CityWallReadyTick)
        {
            d.CityWall = PalisadeState.Built;
            town.Log(LedgerCategory.War, Importance.Major, $"The city wall is closed. {town.Name} is a walled city.");
        }
        var done = d.TowersBuilding.Where(t => town.Tick >= t).ToList();
        if (done.Count > 0)
        {
            d.Towers += done.Count;
            d.TowersBuilding = d.TowersBuilding.Where(t => town.Tick < t).ToList();
            town.Log(LedgerCategory.War, Importance.Routine, "A new watchtower stands on the palisade.");
        }
    }

    /// <summary>The stand-in mayor fortifies once raids begin.</summary>
    private static void Fortify(Town town)
    {
        int stage = StageIndex(town.Enemy.Stage);
        if (stage < StageIndex(ThreatStage.Probing)) return;
        if (town.Defenses.Palisade == PalisadeState.None && town.Treasury > BuildCost(town, DefenseWork.Palisade) + 150) Build(town, DefenseWork.Palisade);
        else if (stage >= StageIndex(ThreatStage.Raids) && town.Treasury > BuildCost(town, DefenseWork.Tower) + 200) Build(town, DefenseWork.Tower);
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
