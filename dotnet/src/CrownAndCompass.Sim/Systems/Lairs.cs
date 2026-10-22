namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Sites are lairs that live off-screen (GDD rules E4, E8, E9). Each grows on the daily tick at its
/// kind's pace, swells the fights there as it grows, and once bold enough acts on the world in its
/// kind's way: beasts hunt the herds, a tribe raids or demands tribute, the dead walk into the fields,
/// schemers take a resident in thrall, rivals toll the roads. Raiders pick a night when the heroes are
/// away. A cleared site does not stay empty: something new moves in after a while, perhaps worse,
/// unless the town holds it with an outpost. The town learns of it only from rumour or sight.
/// </summary>
public static class Lairs
{
    private static LairRules Rules(Town town) => town.Content.Lairs;

    private static bool IsWarCamp(Town town, SiteState s) => s.Def.Id == town.Content.Enemy.Outpost.Id;

    /// <summary>Extra creatures in every fight at the site, from its strength.</summary>
    public static int Swell(Town town, SiteState s)
    {
        var r = Rules(town);
        if (IsWarCamp(town, s) || s.Cleared) return 0;
        return Math.Min(r.MaxSwell, (int)Math.Floor(s.Strength / r.StrengthPerSwell));
    }

    /// <summary>Share of the town's hero levels out on the road: raiders watch for it.</summary>
    public static double AwayShare(Town town)
    {
        var heroes = town.Heroes;
        int total = heroes.Sum(h => h.Hero!.Level);
        return total == 0 ? 1 : (double)heroes.Where(h => h.IsAway).Sum(h => h.Hero!.Level) / total;
    }

    /// <summary>A party that has just marched out, for the raiders' story.</summary>
    public static Party? AwayParty(Town town) =>
        town.Parties.Where(p => !p.Returned).OrderByDescending(p => p.DepartTick).ThenBy(p => p.Id).FirstOrDefault();

    public static void Daily(Town town, bool autoMayor, bool autoDefense = true)
    {
        var r = Rules(town);
        var rng = town.Rng;
        foreach (var s in town.Sites.ToList())
        {
            if (IsWarCamp(town, s) || Undercity.IsUnder(town, s)) continue;
            if (s.NextActTick == null)
            {
                s.Strength = r.StartStrength;
                Next(town, s);
            }
            if (s.Cleared)
            {
                if (s.Restored) continue;
                // A knight runs their own land: their retainers keep any tower at no cost to the treasury.
                var lord = Loyalty.Lord(town, s.Def.Id);
                if (s.TowerDue is int due)
                {
                    if (town.Tick >= due) Raised(town, s, lord);
                }
                else if (s.Held)
                {
                    if (lord == null) town.Book(TreasuryAccount.Outposts, -r.Outpost.Upkeep);
                    Tested(town, s, lord, autoMayor && autoDefense);
                }
                else if (lord == null && s.Patrolled && !PayPatrol(town, s)) { }
                else if (s.ResettleTick != null && town.Tick >= s.ResettleTick)
                {
                    if (s.Patrolled || lord?.Hero!.FiefWay == "ride") Meet(town, s, lord);
                    else Resettle(town, s);
                }
                continue;
            }
            if (s.QuietUntil != null && town.Tick < s.QuietUntil) continue;
            var kind = r.Kinds.GetValueOrDefault(s.Def.Kind);
            s.Strength = Math.Min(r.MaxStrength, s.Strength + (kind?.Growth ?? 0.5));
            // The far wilds trouble their own country (World's pleas), not the town.
            if (s.Generated && s.Def.Distance > (town.Content.Foes.Of(s.Def.Foe)?.Reach ?? 1)) continue;
            if (s.Strength >= r.ActAt && town.Tick >= s.NextActTick) Act(town, s, autoMayor);
        }
        Thralls(town);
        if (autoMayor && autoDefense) AutoHold(town);
    }

    /// <summary>Far lairs reach the town less often: half again the wait for every day of road past the first.</summary>
    private static void Next(Town town, SiteState s)
    {
        var r = Rules(town);
        int days = town.Rng.Range(r.ActEveryDays[0], r.ActEveryDays[1] + 1);
        s.NextActTick = town.Tick + (int)Math.Round(days * (1 + 0.5 * Math.Max(0, s.Def.Distance - 1))) * SimTime.HoursPerDay;
    }

    private static void Act(Town town, SiteState s, bool autoMayor)
    {
        bool acted = s.Def.Kind switch
        {
            "beasts" or "dragon" => Hunt(town, s),
            "tribe" => TribeActs(town, s, autoMayor),
            "undead" => Walk(town, s),
            "schemers" => Whisper(town, s),
            "rivals" => Toll(town, s),
            _ => false,
        };
        if (!acted) return;
        Next(town, s);
        // Whatever comes out of a place the town had never heard of puts it on the map, half believed.
        if (s.Known == SiteKnowledge.Unheard) Rumors.Hear(town, s, Rumors.Occupants(town.Content, s.Def), s.Def.Tier, 40);
    }

    /// <summary>One of the three farms or camps farthest from the town hall that no flag's guards cover: raiders who were turned back go round.</summary>
    private static Building? Outlying(Town town)
    {
        var far = town.Buildings
            .Where(b => b.Complete && (b.Def.Zone == Zone.Farmland || b.Def.Id == "lumber_camp"))
            .OrderByDescending(b => Threat.Distance(town, b.LotId))
            .ThenBy(b => b.Id)
            .Take(6)
            .ToList();
        var edge = Garrison.Unguarded(town, far).Take(3).ToList();
        return edge.Count == 0 ? null : edge[town.Rng.Range(0, edge.Count)];
    }

    // Habits -------------------------------------------------------------------------------------

    /// <summary>How the lair picks its night (lairs.json habits): rolled the first time it is asked, the dead always moon-bound.</summary>
    public static string Habit(Town town, SiteState s)
    {
        var r = Rules(town);
        if (s.Def.Kind == "undead") return r.UndeadHabit;
        if (s.Def.Kind != "tribe") return "";
        if (s.Habit != "" && r.Habits.ContainsKey(s.Habit)) return s.Habit;
        var habits = r.Habits.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        if (habits.Count == 0) return "";
        double roll = town.Rng.Next() * habits.Sum(kv => kv.Value.Weight);
        s.Habit = habits[^1].Key;
        foreach (var (k, h) in habits)
        {
            roll -= h.Weight;
            if (roll < 0)
            {
                s.Habit = k;
                break;
            }
        }
        return s.Habit;
    }

    /// <summary>What the town knows of the lair's habit, for its card; empty until the town has heard of the place.</summary>
    public static string HabitText(Town town, SiteState s)
    {
        if (s.Known == SiteKnowledge.Unheard || s.Cleared) return "";
        string id = s.Def.Kind == "undead" ? Rules(town).UndeadHabit : s.Habit;
        return Rules(town).Habits.GetValueOrDefault(id)?.Text ?? "";
    }

    /// <summary>The dark of the moon, when the moon-bound come.</summary>
    public static bool Dark(Town town) => town.Day % Rules(town).MoonDays >= Rules(town).MoonDays - Rules(town).DarkDays;

    /// <summary>Put the lair's next act off to the next dark of the moon.</summary>
    private static void UntilDark(Town town, SiteState s)
    {
        var r = Rules(town);
        int wait = r.MoonDays - r.DarkDays - town.Day % r.MoonDays;
        if (wait <= 0) wait += r.MoonDays;
        s.NextActTick = town.Tick + wait * SimTime.HoursPerDay;
    }

    /// <summary>Another lair raided within the last few days: this one keeps its own calendar and waits.</summary>
    private static bool Staggered(Town town, SiteState s) =>
        town.Sites.Any(o => o != s && o.LastRaidDay is int d && town.Day - d < Rules(town).StaggerDays);

    private static string From(SiteState s) => s.Known == SiteKnowledge.Unheard ? $"from somewhere past the hills ({s.Def.Name}, the survivors say)" : $"from {s.Def.Name}";

    // Beasts: they hunt the herds and the fields' edge.
    private static bool Hunt(Town town, SiteState s)
    {
        var farm = town.Buildings
            .Where(b => b.Complete && b.Def.Zone == Zone.Farmland && b.Stock.Get(Good.Food, 0) > 0)
            .OrderByDescending(b => Threat.Distance(town, b.LotId))
            .ThenBy(b => b.Id)
            .FirstOrDefault();
        if (farm == null) return false;
        double taken = Math.Floor(farm.Stock.Get(Good.Food, 0) * Math.Min(0.6, s.Strength / 150));
        farm.Stock[Good.Food] = farm.Stock.Get(Good.Food, 0) - taken;
        string who = Cap(Rumors.Occupants(town.Content, s.Def));
        // Fed often enough, they lose their fear of people.
        if (++s.Stirs >= Rules(town).EscalateAt && farm.Workers.Select(town.Agent).Where(a => !a.Departed).OrderBy(a => a.Id).FirstOrDefault() is { } herder && !Garrison.Covered(town, farm))
        {
            s.Stirs = 0;
            Threat.Kill(town, herder);
            town.Log(LedgerCategory.War, Importance.Major, $"{who} {From(s)} have grown bold on {town.Name}'s herds. This time they take {herder.Name} at the edge of {farm.Name} as well, and {Js.Str(taken)} food.", [herder.Id]);
            town.Tension += 12;
            return true;
        }
        town.Log(LedgerCategory.War, Importance.Notable, $"{who} {From(s)} hunt the edges of {farm.Name} by night and carry off {Js.Str(taken)} food.{(s.Stirs == Rules(town).EscalateAt - 1 ? " They come closer each time." : "")}");
        return true;
    }

    // Tribe: each picks its night its own way (a habit), is seen on the road the day before, and falls on its mark the next night.
    private static bool TribeActs(Town town, SiteState s, bool autoMayor)
    {
        var r = Rules(town);
        var rng = town.Rng;
        if (s.BandTarget is int bid) return Arrive(town, s, bid);
        string habit = Habit(town, s);
        double demand = Js.Round(s.Strength * r.Tribute.PerStrength);
        bool asks = s.Known != SiteKnowledge.Unheard && (habit == "tribute" ? !s.Refused : s.Strength >= 50 && rng.Chance(0.4));
        if (asks)
        {
            if (autoMayor && town.Treasury >= demand * r.Tribute.PayIfTreasuryOver)
            {
                town.Book(TreasuryAccount.Tribute, -demand);
                s.QuietUntil = town.Tick + r.Tribute.QuietDays * SimTime.HoursPerDay;
                town.Log(LedgerCategory.War, Importance.Major, $"Envoys from {s.Def.Name} demand {Js.Str(demand)} gold to leave {town.Name} be. The council pays, and buys {r.Tribute.QuietDays} quiet days.");
                return true;
            }
            town.Log(LedgerCategory.War, Importance.Major, $"Envoys from {s.Def.Name} demand {Js.Str(demand)} gold to leave {town.Name} be. The council sends them away empty-handed.");
            s.Refused = true;
            s.NextActTick = town.Tick + 2 * SimTime.HoursPerDay;
            return false;
        }
        switch (habit)
        {
            case "newmoon" when !Dark(town):
                UntilDark(town, s);
                return false;
            case "watchers" or "" when AwayShare(town) < r.SoftShare && s.Waited < r.MaxWaitDays:
                // Raiders wait for a night when the heroes are out on the road.
                s.Waited++;
                s.NextActTick = town.Tick + SimTime.HoursPerDay;
                if (s.Waited == 1) town.Log(LedgerCategory.War, Importance.Notable, $"Woodcutters see watchers on the ridge toward {(s.Known == SiteKnowledge.Unheard ? "the hills" : s.Def.Name)}, counting the heroes at the gate.");
                return false;
        }
        if (Staggered(town, s))
        {
            s.NextActTick = town.Tick + rng.Range(1, r.StaggerDays + 1) * SimTime.HoursPerDay;
            return false;
        }
        s.Waited = 0;
        var target = Outlying(town);
        double raiders = Math.Max(3, Js.Round(s.Strength * 0.3 * (0.7 + rng.Next() * 0.6) * (habit == "bold" ? r.BoldBand : 1) * (1 + r.RebuffBand * Math.Min(r.MaxRebuffs, s.Rebuffs))));
        // A band that can't win stays home and waits for a better night.
        if (target == null || !Threat.WorthRaiding(town, target, raiders)) return true;
        // Someone sees them coming: the town has a night to plant a flag over the mark.
        s.BandTarget = target.Id;
        s.BandSize = raiders;
        s.LastRaidDay = town.Day;
        // A sky watch sees them gathering sooner: a day more to plant the flags.
        // So do riders patrolling the ground it sets out from or crosses.
        int sky = Institutions.SkyWatch(town);
        int patrolled = Wilds.Warns(town, World.Spot(town, s));
        sky += patrolled;
        s.NextActTick = town.Tick + r.BandHours + sky;
        string who = Rumors.Occupants(town.Content, s.Def);
        string bigger = s.Rebuffs > 0 ? " More of them than last time." : "";
        string when = sky >= 12 ? "They will be there tomorrow night." : "They will be there by nightfall.";
        Sightings.Report(town, Key(s), Sightings.Band, who, (int)raiders, s.Def.Id, target.Id, Garrison.SectionAt(town, town.Lot(target.LotId).X, town.Lot(target.LotId).Y), 0.5, r.BandHours + sky + 6,
            sky > patrolled ? $"flies in with news: about {Js.Str(raiders)} {who} gathering {From(s)}, bound for {target.Name}. {when}{bigger}"
                : patrolled > 0 ? $"brings news: about {Js.Str(raiders)} {who} setting out {From(s)}, bound for {target.Name}. {when}{bigger}"
                : $"runs in with news: about {Js.Str(raiders)} {who} coming down {From(s)} toward {target.Name}. They will be there by nightfall.{bigger}",
            reporter: sky > patrolled ? Sightings.SkyRider(town) : patrolled > 0 ? Wilds.Spotter(town, World.Spot(town, s)) : null);
        return false;
    }

    /// <summary>The key of a lair's sightings.</summary>
    public static string Key(SiteState s) => $"site:{s.Def.Id}";

    /// <summary>The band seen on the road falls on its mark, unless the town made it not worth the try.</summary>
    private static bool Arrive(Town town, SiteState s, int targetId)
    {
        var r = Rules(town);
        var rng = town.Rng;
        s.BandTarget = null;
        s.LastRaidDay = town.Day;
        Sightings.Clear(town, Key(s));
        double raiders = s.BandSize;
        var target = town.Buildings.FirstOrDefault(b => b.Id == targetId && b.Complete) ?? Outlying(town);
        string where = target?.Name ?? "the outlying fields";
        string who = Rumors.Occupants(town.Content, s.Def);
        // The flags went up in time: they see the guards and melt away, and come back with more.
        if (target == null || !Threat.WorthRaiding(town, target, raiders))
        {
            s.Rebuffs++;
            town.Log(LedgerCategory.War, Importance.Notable, $"The {who} {From(s)} see the watch waiting at {where} and melt back into the dark without a fight. They will be back, and more of them.");
            town.Tension += 2;
            return true;
        }
        double local = Threat.LocalDefense(town) * Garrison.FlagFactor(town, target);
        var party = AwayParty(town);
        string timing = party != null && AwayShare(town) >= r.SoftShare ? $", while the {party.Name} are away" : "";
        if (local >= raiders)
        {
            s.Strength = Math.Max(0, s.Strength - raiders);
            s.Rebuffs++;
            town.FallenRaiders += raiders;
            town.Log(LedgerCategory.War, Importance.Notable, $"Some {Js.Str(raiders)} raiders {From(s)} fall on {where}{timing}, and the watch drives them off.");
            town.Tension += 5;
            return true;
        }
        s.Rebuffs = 0;
        s.Refused = false;
        string text = $"Some {Js.Str(raiders)} raiders {From(s)} fall on {where} in the night{timing}";
        double loot = Math.Floor(target.Cash * 0.4);
        target.Cash -= loot;
        target.Stock[Good.Food] = Math.Floor(target.Stock.Get(Good.Food, 0) * 0.5);
        text += loot > 0 ? $", carrying off food and {Js.Str(loot)} gold" : ", carrying off food";
        string taken = Captives.Take(town, s, target);
        text += taken;
        town.Log(LedgerCategory.War, Importance.Major, $"{text}.");
        // Seen going home, with whatever they took.
        Sightings.Report(town, Key(s), Sightings.Flight, who, (int)raiders, s.Def.Id, target.Id, null, 0.25, SimTime.HoursPerDay, "");
        town.Tension += 15;
        town.Mandate = Math.Max(0, town.Mandate - 2);
        return true;
    }

    // Undead: the dead walk into the fields in the dark of the moon; strong enough, they kill, unless the god's light holds them.
    // Walks that come to nothing build: the third time, the barrow empties.
    private static bool Walk(Town town, SiteState s)
    {
        var r = Rules(town);
        var rng = town.Rng;
        if (Habit(town, s) == "newmoon" && !Dark(town))
        {
            UntilDark(town, s);
            return false;
        }
        Raise(town, s);
        var target = Outlying(town);
        string where = target?.Name ?? "the fields";
        bool surge = s.Stirs + 1 >= r.EscalateAt && target != null;
        string whence = s.Known == SiteKnowledge.Unheard ? "coming from somewhere past the hills" : $"from the direction of {s.Def.Name}";
        if (!surge && (s.Strength < r.UndeadKillAt || !Threat.WorthRaiding(town, target, s.Strength * 0.3)))
        {
            s.Stirs++;
            string worse = s.Stirs == r.EscalateAt - 1 ? " Each time there are more of them." : "";
            Sightings.Report(town, Key(s), Sightings.Dead, "the dead", 0, s.Def.Id, target?.Id, null, 0.75, SimTime.HoursPerDay,
                $"swears to it: shapes that should be in the ground are walking at the edge of {where}, {whence}. No one sleeps well.{worse}");
            town.Tension += 6;
            return true;
        }
        if (surge)
        {
            s.Strength = Math.Min(r.MaxStrength, s.Strength + r.EscalateStrength);
            town.Log(LedgerCategory.War, Importance.Major, $"This time it is no warning. {(s.Known == SiteKnowledge.Unheard ? "Something past the hills" : s.Def.Name)} empties, and the dead come for {where} in force.");
        }
        s.Stirs = 0;
        double ward = Shrines.Temple(town) != null ? r.WardTemple : Shrines.Shrine(town) != null ? r.WardShrine : 0;
        var holy = Shrines.HolyPlace(town);
        if (holy != null && rng.Chance(surge ? ward * 0.5 : ward))
        {
            town.Log(LedgerCategory.War, Importance.Notable, $"The dead walk {From(s)} as far as {where}, and turn back at the lamps of {holy.Name}.");
            town.Tension += 3;
            return true;
        }
        var workers = target!.Workers.Select(town.Agent).Where(a => !a.Departed).OrderBy(a => a.Id).ToList();
        if (workers.Count == 0) return false;
        int n = surge ? Math.Min(workers.Count, 2) : 1;
        var dead = new List<Agent>();
        for (int i = 0; i < n; i++)
        {
            int k = rng.Range(0, workers.Count);
            dead.Add(workers[k]);
            workers.RemoveAt(k);
        }
        foreach (var v in dead) Threat.Kill(town, v);
        town.Log(LedgerCategory.War, Importance.Major, $"The dead walk {From(s)} into {where}. {Expedition.ListNames(dead.Select(v => v.Name).ToList())} {(dead.Count == 1 ? "is" : "are")} found at dawn, cold and grey.", dead.Select(v => v.Id).ToList());
        town.Tension += 15 * dead.Count;
        return true;
    }

    /// <summary>A spirit-caller's walk brings back the raiders who fell at the town since its last.</summary>
    private static void Raise(Town town, SiteState s)
    {
        var rules = town.Content.Foes;
        if (town.Content.Foes.Of(s.Def.Foe) is not { Raises: true } || town.FallenRaiders < 1) return;
        double gain = Math.Min(rules.RaiseMax, Js.Round(town.FallenRaiders * rules.Raise));
        int risen = (int)town.FallenRaiders;
        town.FallenRaiders = 0;
        s.Strength = Math.Min(Rules(town).MaxStrength, s.Strength + gain);
        town.Log(LedgerCategory.War, Importance.Notable, rules.RaiseText.Replace("{site}", s.Def.Name).Replace("{n}", Js.Str(risen)).Replace("{town}", town.Name));
    }

    // Schemers: a whisper at the well, and a resident steals for them.
    private static bool Whisper(Town town, SiteState s)
    {
        var rng = town.Rng;
        if (town.Residents.Count(a => a.ThrallOf == s.Def.Id) >= 1 + (int)(s.Strength / 40)) return false;
        var pool = town.Workers.Where(a => a.ThrallOf == null && !a.Devout && town.Seats.All(x => x.HolderId != a.Id)).OrderBy(a => a.Id).ToList();
        if (pool.Count == 0) return false;
        var v = pool[rng.Range(0, pool.Count)];
        v.ThrallOf = s.Def.Id;
        town.Log(LedgerCategory.Hardship, Importance.Routine, $"{v.Name} has taken to walking out of town alone at dusk, toward {(s.Known == SiteKnowledge.Unheard ? "the marsh road" : s.Def.Name)}.", [v.Id]);
        return true;
    }

    private static void Thralls(Town town)
    {
        var r = Rules(town);
        foreach (var a in town.Residents)
        {
            if (a.ThrallOf == null || (town.Day + a.Id) % r.ThrallStealDays != 0) continue;
            // Past the watch first: caught, and the spell on them is broken in the cells.
            if (!Skills.SlipPast(town, a))
            {
                var site = town.Sites.FirstOrDefault(x => x.Def.Id == a.ThrallOf);
                a.ThrallOf = null;
                town.Log(LedgerCategory.Hardship, Importance.Notable,
                    $"The watch catches {a.Name} with a hand in the town strongbox ({Skills.Said(town, town.Checks[^1])}). In the cells they weep and swear they were made to{(site != null && site.Known != SiteKnowledge.Unheard ? $" by whatever waits at {site.Def.Name}" : "")}.", [a.Id]);
                continue;
            }
            // The coin goes missing quietly: the accounts show it, the Ledger does not.
            double take = Math.Min(Math.Max(0, town.Treasury), town.Rng.Range(r.ThrallSteal[0], r.ThrallSteal[1] + 1));
            if (take > 0) town.Book(TreasuryAccount.Discrepancies, -take);
        }
    }

    // Rivals: toll-men on the road take a cut from a business.
    private static bool Toll(Town town, SiteState s)
    {
        var mark = town.Buildings
            .Where(b => b.Complete && b.Def.Zone is Zone.Commercial or Zone.Industry && b.OwnerId != null && b.Cash > 20)
            .OrderByDescending(b => b.Cash)
            .ThenBy(b => b.Id)
            .FirstOrDefault();
        if (mark == null) return false;
        double take = Math.Floor(mark.Cash * Math.Min(0.4, s.Strength / 200));
        mark.Cash -= take;
        town.Log(LedgerCategory.War, Importance.Notable, $"Toll-men {From(s)} stop {mark.Name}'s carts on the road and take {Js.Str(take)} gold for passage.");
        town.Tension += 4;
        return true;
    }

    // After a delve ------------------------------------------------------------------------------

    /// <summary>A party came back: what they killed thins the lair; a clear frees thralls, scatters a tribe, and starts the clock on resettlement.</summary>
    public static void AfterDelve(Town town, SiteState s, ExpeditionResult result)
    {
        var r = Rules(town);
        if (IsWarCamp(town, s)) return;
        int kills = result.Kills.Values.Sum();
        s.Strength = Math.Max(0, s.Strength - kills * r.RetreatLossPerKill);
        if (result.Outcome != ExpeditionOutcome.Cleared) return;
        s.BandTarget = null;
        Sightings.Clear(town, Key(s));
        int wait = town.Rng.Range(r.ResettleDays[0], r.ResettleDays[1] + 1);
        // Folk driven out rather than slain come back sooner.
        if (result.Driven) wait = Math.Max(1, (int)Math.Round(wait * town.Content.Bounties.Term("drive").Resettle));
        s.ResettleTick = town.Tick + wait * SimTime.HoursPerDay;
        foreach (var a in town.Residents.Where(a => a.ThrallOf == s.Def.Id))
        {
            a.ThrallOf = null;
            town.Log(LedgerCategory.Hardship, Importance.Notable, $"With {s.Def.Name} cleared, {a.Name} wakes as if from a long fever and confesses to stealing from the town.", [a.Id]);
        }
        if (s.Def.Kind != "tribe") return;
        var refuge = town.Sites
            .Where(o => o != s && !o.Cleared && o.Def.Kind == "tribe" && !IsWarCamp(town, o))
            .OrderBy(o => Math.Abs(o.Def.Distance - s.Def.Distance))
            .ThenBy(o => o.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (refuge == null) return;
        refuge.Strength = Math.Min(r.MaxStrength, refuge.Strength + r.ScatterJoin);
        if (refuge.Known != SiteKnowledge.Unheard)
            town.Log(LedgerCategory.War, Importance.Routine, $"Survivors of {s.Def.Name} were seen fleeing toward {refuge.Def.Name}.");
    }

    // Resettlement -------------------------------------------------------------------------------

    internal static string KindOf(string type, string was) => type switch
    {
        "undead" => "undead",
        "dragon" => "dragon",
        "humanoid" => was == "rivals" ? "rivals" : "tribe",
        "giant" => "tribe",
        "fiend" or "fey" or "aberration" or "celestial" => "schemers",
        _ => "beasts",
    };

    /// <summary>Something moves into a cleared site: a library creature of the site's country, perhaps worse than before.</summary>
    public static void Resettle(Town town, SiteState s)
    {
        var r = Rules(town);
        var rng = town.Rng;
        var c = town.Content;
        int tier = s.Def.Tier + (rng.Chance(r.WorseChance) ? 1 : 0);
        // Hand-written sites stay within the slice's three tiers; the generated wilds keep their level band.
        if (!s.Generated) tier = Math.Min(3, tier);
        double[] cr = r.BossCr.Length > tier ? r.BossCr[tier] : [Math.Max(0.5, tier * 0.75), Math.Max(1, tier)];
        var leaders = c.Library.Monsters
            .Where(lm => lm.Damage != null && lm.Cr >= cr[0] && lm.Cr <= cr[^1] && lm.Habitats.Any(s.Def.Habitats.Contains))
            .OrderBy(lm => lm.Id, StringComparer.Ordinal)
            .ToList();
        // A group's site is retaken by its own kind when one of its bosses fits.
        if (c.Group(s.Def.Group) is GroupDef g)
        {
            var own = leaders.Where(lm => g.Bosses.Contains(lm.Id)).ToList();
            if (own.Count == 0)
                own = c.Library.Monsters
                    .Where(lm => g.Bosses.Contains(lm.Id) && lm.Damage != null && lm.Cr >= cr[0] && lm.Cr <= cr[^1])
                    .OrderBy(lm => lm.Id, StringComparer.Ordinal)
                    .ToList();
            if (own.Count > 0) leaders = own;
        }
        s.Cleared = false;
        s.ResettleTick = null;
        s.Strength = r.StartStrength;
        s.Waited = 0;
        s.Habit = "";
        s.Rebuffs = 0;
        s.Stirs = 0;
        s.Refused = false;
        Next(town, s);
        // Nothing fits: the old occupants' kin come back.
        if (leaders.Count == 0) return;
        var lead = leaders[rng.Range(0, leaders.Count)];
        var escorts = c.Wanderers(s.Def, [0.125, Math.Max(0.25, lead.Cr / 2)]).Where(m => m.Id != lead.Id).ToList();
        var escort = escorts.Count > 0 ? escorts[rng.Range(0, escorts.Count)] : null;
        s.Def = s.Def with
        {
            Tier = tier,
            Kind = KindOf(lead.Type, s.Def.Kind),
            Boss = s.Def.Boss with
            {
                Monster = c.BossOf(lead.Id).Id,
                Count = [1, 1],
                Escort = escort?.Id,
                EscortCount = escort != null ? [2, 3] : null,
            },
            Loot = [s.Def.Loot[0] + 30 * (tier - s.Def.Tier), s.Def.Loot[1] + 60 * (tier - s.Def.Tier)],
        };
    }

    // Towers -------------------------------------------------------------------------------------

    public static string? HoldBlocked(Town town, SiteState s)
    {
        var o = Rules(town).Outpost;
        if (!s.Cleared || !s.ThoughtCleared) return "only a cleared site can be held";
        if (s.TowerDue != null) return "a tower is rising";
        if (s.Held) return "already held";
        if (Loyalty.Lord(town, s.Def.Id) != null) return "a knight's land";
        if (town.Sites.Count(x => (x.Held || x.TowerDue != null) && Loyalty.Lord(town, x.Def.Id) == null) >= o.Max) return $"the town can hold {o.Max} towers";
        if (town.Treasury < o.Cost) return $"needs {Js.Str(o.Cost)} gold";
        return null;
    }

    /// <summary>Raise a guard tower on a cleared site: it takes gold and days to build, and once up nothing moves back in and the town sees it live.</summary>
    public static bool Hold(Town town, string siteId)
    {
        var s = town.Sites.FirstOrDefault(x => x.Def.Id == siteId);
        if (s == null || HoldBlocked(town, s) != null) return false;
        var o = Rules(town).Outpost;
        if (Stewards.Intercept(town, "defence", new StewardAsk { Area = "defence", Kind = "hold", Title = $"Build a tower at {s.Def.Name} · {Js.Str(o.Cost)}g", Why = "• Cleared, but something may move back in\n• A tower keeps it clear", S1 = siteId })) return false;
        town.Book(TreasuryAccount.Outposts, -o.Cost);
        Build(town, s);
        town.Log(LedgerCategory.War, Importance.Notable, $"Masons and a guard of the watch march out to raise a tower at {s.Def.Name}. It will stand in {o.BuildDays} days, and cost {Js.Str(o.Upkeep)} gold a day to keep.");
        return true;
    }

    /// <summary>Builders go out to a site (the town's or a knight's): nothing moves in while they work.</summary>
    internal static void Build(Town town, SiteState s)
    {
        s.TowerDue = town.Tick + Rules(town).Outpost.BuildDays * SimTime.HoursPerDay;
        s.Patrolled = false;
        s.ResettleTick = null;
    }

    private static void Raised(Town town, SiteState s, Agent? lord)
    {
        s.TowerDue = null;
        s.Held = true;
        town.Log(LedgerCategory.War, Importance.Notable, lord != null
            ? $"Ser {lord.Name}'s tower stands over {s.Def.Name}, manned by their own retainers."
            : $"The tower at {s.Def.Name} is finished and manned. Nothing moves back in while it stands.", lord != null ? [lord.Id] : []);
    }

    /// <summary>Who comes to test a tower: the strongest menace abroad, or whatever the wilds send.</summary>
    private static string Tester(Town town)
    {
        var m = Menaces.Alive(town).OrderByDescending(x => x.Power).ThenBy(x => x.Id).FirstOrDefault();
        return m != null ? $"raiders of {m.Name}" : "raiders out of the wilds";
    }

    /// <summary>Its own dice, so a tower's tests never shift the town's main stream.</summary>
    private static Rng Dice(Town town, SiteState s) => new(unchecked(town.Seed * 7919 + town.Tick * 104729 + Stable(s.Def.Id) + 29));

    private static int Stable(string id)
    {
        int h = 17;
        foreach (char c in id) h = unchecked(h * 31 + c);
        return h;
    }

    /// <summary>The garrison's odds of holding a test: less at deeper sites, more for a knight's levels and for reinforcements.</summary>
    public static double HoldOdds(Town town, SiteState s)
    {
        var o = Rules(town).Outpost;
        int level = Loyalty.Lord(town, s.Def.Id)?.Hero!.Level ?? 0;
        return Math.Clamp(o.HoldChance - o.PerTier * s.Def.Tier + o.PerLevel * level + (s.Reinforced ? o.ReinforceBonus : 0), 0.1, 0.95);
    }

    /// <summary>
    /// Now and then something comes to test a standing tower. The town hears of it <c>WarnDays</c> ahead and can
    /// reinforce it (<see cref="Reinforce"/>); a strong garrison holds, a weak one falls and the site wants clearing again.
    /// </summary>
    internal static void Tested(Town town, SiteState s, Agent? lord, bool auto)
    {
        var o = Rules(town).Outpost;
        var dice = Dice(town, s);
        if (s.ThreatTick == null)
        {
            if (!dice.Chance(1 / o.TestEveryDays)) return;
            s.ThreatTick = town.Tick + o.WarnDays * SimTime.HoursPerDay;
            s.ThreatBy = Tester(town);
            s.Reinforced = false;
            town.Log(LedgerCategory.War, Importance.Major, $"Word comes that {s.ThreatBy} gather to test the tower at {s.Def.Name}. They will come in {o.WarnDays} days.", lord != null ? [lord.Id] : []);
            // A knight sees to their own walls when they can afford it; the stand-in mayor sees to the town's.
            if (lord != null && !lord.Has(Trait.Lazy) && lord.Gold >= o.Reinforce)
            {
                lord.Gold -= o.Reinforce;
                s.Reinforced = true;
            }
            else if (lord == null && auto && town.Treasury >= o.Reinforce + 300) Reinforce(town, s.Def.Id);
            return;
        }
        if (town.Tick < s.ThreatTick) return;
        bool held = dice.Chance(HoldOdds(town, s));
        string by = s.ThreatBy;
        s.ThreatTick = null;
        s.ThreatBy = "";
        s.Reinforced = false;
        if (held)
        {
            if (lord != null) Notoriety.Deed(town, lord, "patrol", 1);
            town.Log(LedgerCategory.War, Importance.Notable, $"The tower at {s.Def.Name} holds: {by} break on its walls and go back the way they came.", lord != null ? [lord.Id] : []);
            return;
        }
        s.Held = false;
        Resettle(town, s);
        town.Log(LedgerCategory.War, Importance.Major, $"The tower at {s.Def.Name} falls to {by}. They hold the site now; it wants clearing again.", lord != null ? [lord.Id] : []);
    }

    public static string? ReinforceBlocked(Town town, SiteState s)
    {
        var o = Rules(town).Outpost;
        if (!s.Held || s.ThreatTick == null) return "nothing comes against it";
        if (Loyalty.Lord(town, s.Def.Id) != null) return "a knight's land";
        if (s.Reinforced) return "already reinforced";
        if (town.Treasury < o.Reinforce) return $"needs {Js.Str(o.Reinforce)} gold";
        return null;
    }

    /// <summary>Send men and stores to a tower before its test: better odds that it holds.</summary>
    public static bool Reinforce(Town town, string siteId)
    {
        var s = town.Sites.FirstOrDefault(x => x.Def.Id == siteId);
        if (s == null || ReinforceBlocked(town, s) != null) return false;
        var o = Rules(town).Outpost;
        if (Stewards.Intercept(town, "defence", new StewardAsk { Area = "defence", Kind = "reinforce", Title = $"Reinforce {s.Def.Name} · {Js.Str(o.Reinforce)}g", Why = $"• {Cap(s.ThreatBy)} are coming\n• Holds {Math.Round(HoldOdds(town, s) * 100)}% now", S1 = siteId })) return false;
        town.Book(TreasuryAccount.Outposts, -o.Reinforce);
        s.Reinforced = true;
        town.Log(LedgerCategory.War, Importance.Notable, $"Spears and stores go out to the tower at {s.Def.Name} before {s.ThreatBy} come.");
        return true;
    }

    /// <summary>
    /// The cleared site the town would garrison next, when it can spare the gold: the stand-in mayor holds it, and a
    /// player is asked on the alert strip instead (the watch never marches out without the mayor's word).
    /// </summary>
    public static SiteState? WorthHolding(Town town)
    {
        var o = Rules(town).Outpost;
        if (town.Treasury < o.Cost + 600) return null;
        return town.Sites
            .Where(x => !IsWarCamp(town, x) && HoldBlocked(town, x) == null)
            .OrderBy(x => x.Def.Distance)
            .ThenBy(x => x.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    // Patrols ------------------------------------------------------------------------------------

    public static double PatrolWage(Town town, SiteState s) => Rules(town).Patrol.Wage + Rules(town).Patrol.PerTier * s.Def.Tier;

    public static string? PatrolBlocked(Town town, SiteState s)
    {
        var p = Rules(town).Patrol;
        if (IsWarCamp(town, s) || Undercity.IsUnder(town, s)) return "no patrol rides there";
        if (!s.Cleared || !s.ThoughtCleared) return "only a cleared site can be patrolled";
        if (s.Held || s.TowerDue != null) return "a tower holds it";
        if (Loyalty.Lord(town, s.Def.Id) != null) return "a knight's land";
        if (s.Restored) return "the town restored it";
        if (s.Patrolled) return "already patrolled";
        if (town.Sites.Count(x => x.Patrolled) >= p.Max) return $"the town can keep {p.Max} patrols";
        if (town.Treasury < PatrolWage(town, s) * 5) return "not enough gold";
        return null;
    }

    /// <summary>
    /// Keep a cleared site clear with a standing patrol (or call it off): the step between clearing it with a bounty
    /// and holding it with an outpost. It costs a little gold a day; when something tries to move back in, the best hero
    /// at home rides out with the patrol and usually drives it off (<see cref="Meet"/>).
    /// </summary>
    public static bool Patrol(Town town, string siteId, bool on)
    {
        var s = town.Sites.FirstOrDefault(x => x.Def.Id == siteId);
        if (s == null) return false;
        if (!on)
        {
            if (!s.Patrolled) return false;
            s.Patrolled = false;
            town.Log(LedgerCategory.War, Importance.Routine, $"The council calls off the patrol over {s.Def.Name}.");
            return true;
        }
        if (PatrolBlocked(town, s) != null) return false;
        s.Patrolled = true;
        // A site cleared long ago with no clock running gets one now, so the patrol has something to watch for.
        s.ResettleTick ??= town.Tick + Rules(town).ResettleDays[1] * SimTime.HoursPerDay;
        town.Log(LedgerCategory.War, Importance.Notable, $"The council posts a standing patrol over {s.Def.Name}, at {Js.Str(PatrolWage(town, s))} gold a day: riders keep it clear of whatever would move back in.");
        return true;
    }

    /// <summary>The patrol's day wage; an empty treasury sends the riders home.</summary>
    private static bool PayPatrol(Town town, SiteState s)
    {
        double wage = PatrolWage(town, s);
        if (town.Treasury < wage)
        {
            s.Patrolled = false;
            town.Log(LedgerCategory.War, Importance.Notable, $"The treasury can't pay the patrol over {s.Def.Name}, and the riders go home.");
            return false;
        }
        town.Book(TreasuryAccount.Outposts, -wage);
        return true;
    }

    /// <summary>
    /// Something tries to move into a patrolled site: the best hero at home rides with the patrol to meet it (on a
    /// knight's land, the knight, or their retainers alone). A lost fight wounds the hero who led it.
    /// </summary>
    private static void Meet(Town town, SiteState s, Agent? lord = null)
    {
        var p = Rules(town).Patrol;
        var hero = lord != null
            ? (lord.IsAway || lord.Dead ? null : lord)
            : town.Heroes.Where(a => !a.IsAway && !a.Dead).OrderByDescending(a => a.Hero!.Level).ThenBy(a => a.Id).FirstOrDefault();
        double chance = hero == null ? p.Chance * 0.5 : Math.Clamp(p.Chance + p.PerLevel * (hero.Hero!.Level - 2 * s.Def.Tier), 0.15, 0.95);
        string riders = lord != null ? $"Ser {lord.GivenName}'s riders" : "The patrol";
        if (town.Rng.Chance(chance))
        {
            var r = Rules(town);
            s.ResettleTick = town.Tick + town.Rng.Range(r.ResettleDays[0], r.ResettleDays[1] + 1) * SimTime.HoursPerDay;
            if (hero != null)
            {
                Quests.GainXp(town, hero, p.Xp);
                Notoriety.Deed(town, hero, "patrol", 1);
            }
            town.Log(LedgerCategory.War, Importance.Notable, hero != null
                ? $"On patrol, {hero.Name} meets something moving into {s.Def.Name} and drives it off. The site stays clear."
                : $"{riders} meet something moving into {s.Def.Name} and drive it off. The site stays clear.", hero != null ? [hero.Id] : []);
            return;
        }
        s.Patrolled = false;
        Resettle(town, s);
        string wound = "";
        if (hero != null)
        {
            var h = hero.Hero!;
            int hurt = Dice(town, s).Roll(p.WoundDice * (s.Def.Tier + 1), 6);
            h.Hp = Math.Max(1, h.Hp - hurt);
            wound = $" {hero.Name} is carried home wounded ({hurt} hit points).";
        }
        town.Log(LedgerCategory.War, Importance.Major, $"{riders} could not hold {s.Def.Name}: something has moved back in. It wants clearing again.{wound}", hero != null ? [hero.Id] : []);
    }

    /// <summary>The cleared site the player might keep clear next: nearest first, one the treasury could pay a patrol for.</summary>
    public static SiteState? WorthPatrolling(Town town)
    {
        // Read every frame by the alert strip: the cheap checks first, and none at all once the town keeps all it can.
        var p = Rules(town).Patrol;
        int kept = 0;
        foreach (var x in town.Sites)
            if (x.Patrolled) kept++;
        if (kept >= p.Max) return null;
        SiteState? best = null;
        foreach (var x in town.Sites)
        {
            if (!x.Cleared || !x.ThoughtCleared || x.Held || x.TowerDue != null || x.Patrolled || x.Restored || x.ResettleTick == null) continue;
            if (best != null && (x.Def.Distance > best.Def.Distance || (x.Def.Distance == best.Def.Distance && string.CompareOrdinal(x.Def.Id, best.Def.Id) > 0))) continue;
            if (PatrolBlocked(town, x) != null) continue;
            best = x;
        }
        return best;
    }

    private static void AutoHold(Town town)
    {
        if (WorthHolding(town) is { } s) Hold(town, s.Def.Id);
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
