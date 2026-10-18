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

    public static void Daily(Town town, bool autoMayor)
    {
        var r = Rules(town);
        var rng = town.Rng;
        foreach (var s in town.Sites.ToList())
        {
            if (IsWarCamp(town, s)) continue;
            if (s.NextActTick == null)
            {
                s.Strength = r.StartStrength;
                Next(town, s);
            }
            if (s.Cleared)
            {
                // A knight's own retainers hold their lands at no cost to the treasury.
                if (s.Held)
                {
                    if (Loyalty.Lord(town, s.Def.Id) == null) town.Book(TreasuryAccount.Outposts, -r.Outpost.Upkeep);
                }
                else if (s.ResettleTick != null && town.Tick >= s.ResettleTick) Resettle(town, s);
                continue;
            }
            if (s.QuietUntil != null && town.Tick < s.QuietUntil) continue;
            var kind = r.Kinds.GetValueOrDefault(s.Def.Kind);
            s.Strength = Math.Min(r.MaxStrength, s.Strength + (kind?.Growth ?? 0.5));
            // The far wilds trouble their own country (World's pleas), not the town.
            if (s.Generated && s.Def.Distance > 1) continue;
            if (s.Strength >= r.ActAt && town.Tick >= s.NextActTick) Act(town, s, autoMayor);
        }
        Thralls(town);
        if (autoMayor) AutoHold(town);
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
            "beasts" => Hunt(town, s),
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

    /// <summary>One of the three farms or camps farthest from the town hall.</summary>
    private static Building? Outlying(Town town)
    {
        var edge = town.Buildings
            .Where(b => b.Complete && (b.Def.Zone == Zone.Farmland || b.Def.Id == "lumber_camp"))
            .OrderByDescending(b => Threat.Distance(town, b.LotId))
            .ThenBy(b => b.Id)
            .Take(3)
            .ToList();
        return edge.Count == 0 ? null : edge[town.Rng.Range(0, edge.Count)];
    }

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
        town.Log(LedgerCategory.War, Importance.Notable, $"{Cap(Rumors.Occupants(town.Content, s.Def))} {From(s)} hunt the edges of {farm.Name} by night and carry off {Js.Str(taken)} food.");
        return true;
    }

    // Tribe: raids timed for when the heroes are away, or tribute to stay home.
    private static bool TribeActs(Town town, SiteState s, bool autoMayor)
    {
        var r = Rules(town);
        var rng = town.Rng;
        double demand = Js.Round(s.Strength * r.Tribute.PerStrength);
        if (s.Strength >= 50 && s.Known != SiteKnowledge.Unheard && rng.Chance(0.4))
        {
            if (autoMayor && town.Treasury >= demand * r.Tribute.PayIfTreasuryOver)
            {
                town.Book(TreasuryAccount.Tribute, -demand);
                s.QuietUntil = town.Tick + r.Tribute.QuietDays * SimTime.HoursPerDay;
                town.Log(LedgerCategory.War, Importance.Major, $"Envoys from {s.Def.Name} demand {Js.Str(demand)} gold to leave {town.Name} be. The council pays, and buys {r.Tribute.QuietDays} quiet days.");
                return true;
            }
            town.Log(LedgerCategory.War, Importance.Major, $"Envoys from {s.Def.Name} demand {Js.Str(demand)} gold to leave {town.Name} be. The council sends them away empty-handed.");
            s.NextActTick = town.Tick + 2 * SimTime.HoursPerDay;
            return false;
        }
        // Raiders wait for a night when the heroes are out on the road.
        if (AwayShare(town) < r.SoftShare && s.Waited < r.MaxWaitDays)
        {
            s.Waited++;
            s.NextActTick = town.Tick + SimTime.HoursPerDay;
            if (s.Waited == 1) town.Log(LedgerCategory.War, Importance.Notable, $"Woodcutters see watchers on the ridge toward {(s.Known == SiteKnowledge.Unheard ? "the hills" : s.Def.Name)}, counting the heroes at the gate.");
            return false;
        }
        s.Waited = 0;
        var target = Outlying(town);
        string where = target?.Name ?? "the outlying fields";
        double raiders = Math.Max(3, Js.Round(s.Strength * 0.3 * (0.7 + rng.Next() * 0.6)));
        double local = Threat.LocalDefense(town);
        var party = AwayParty(town);
        string timing = party != null && AwayShare(town) >= r.SoftShare ? $", while the {party.Name} are away" : "";
        if (local >= raiders)
        {
            s.Strength = Math.Max(0, s.Strength - raiders);
            town.Log(LedgerCategory.War, Importance.Notable, $"Some {Js.Str(raiders)} raiders {From(s)} fall on {where}{timing}, and the watch drives them off.");
            town.Tension += 5;
            return true;
        }
        string text = $"Some {Js.Str(raiders)} raiders {From(s)} fall on {where} in the night{timing}";
        if (target != null)
        {
            double loot = Math.Floor(target.Cash * 0.4);
            target.Cash -= loot;
            target.Stock[Good.Food] = Math.Floor(target.Stock.Get(Good.Food, 0) * 0.5);
            text += loot > 0 ? $", carrying off food and {Js.Str(loot)} gold" : ", carrying off food";
        }
        text += Captives.Take(town, s, target);
        town.Log(LedgerCategory.War, Importance.Major, $"{text}.");
        town.Tension += 15;
        town.Mandate = Math.Max(0, town.Mandate - 2);
        return true;
    }

    // Undead: the dead walk into the fields; strong enough, they kill, unless the god's light holds them.
    private static bool Walk(Town town, SiteState s)
    {
        var r = Rules(town);
        var rng = town.Rng;
        var target = Outlying(town);
        string where = target?.Name ?? "the fields";
        if (s.Strength < r.UndeadKillAt || target == null)
        {
            town.Log(LedgerCategory.War, Importance.Notable, $"Shapes that should be in the ground are seen walking at the edge of {where}, {(s.Known == SiteKnowledge.Unheard ? "coming from somewhere past the hills" : $"from the direction of {s.Def.Name}")}. No one sleeps well.");
            town.Tension += 6;
            return true;
        }
        double ward = Shrines.Temple(town) != null ? r.WardTemple : Shrines.Shrine(town) != null ? r.WardShrine : 0;
        var holy = Shrines.HolyPlace(town);
        if (holy != null && rng.Chance(ward))
        {
            town.Log(LedgerCategory.War, Importance.Notable, $"The dead walk {From(s)} as far as {where}, and turn back at the lamps of {holy.Name}.");
            town.Tension += 3;
            return true;
        }
        var workers = target.Workers.Select(town.Agent).Where(a => !a.Departed).OrderBy(a => a.Id).ToList();
        if (workers.Count == 0) return false;
        var v = workers[rng.Range(0, workers.Count)];
        Threat.Kill(town, v);
        town.Log(LedgerCategory.War, Importance.Major, $"The dead walk {From(s)} into {where}. {v.Name} is found at dawn, cold and grey.", [v.Id]);
        town.Tension += 15;
        return true;
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
        s.Cleared = false;
        s.ResettleTick = null;
        s.Strength = r.StartStrength;
        s.Waited = 0;
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

    // Outposts -----------------------------------------------------------------------------------

    public static string? HoldBlocked(Town town, SiteState s)
    {
        var o = Rules(town).Outpost;
        if (!s.Cleared || !s.ThoughtCleared) return "only a cleared site can be held";
        if (s.Held) return "already held";
        if (town.Sites.Count(x => x.Held) >= o.Max) return $"the town can hold {o.Max} outposts";
        if (town.Treasury < o.Cost) return $"needs {Js.Str(o.Cost)} gold";
        return null;
    }

    /// <summary>Garrison a cleared site: nothing moves back in, and the town sees it as it is.</summary>
    public static bool Hold(Town town, string siteId)
    {
        var s = town.Sites.FirstOrDefault(x => x.Def.Id == siteId);
        if (s == null || HoldBlocked(town, s) != null) return false;
        var o = Rules(town).Outpost;
        town.Book(TreasuryAccount.Outposts, -o.Cost);
        s.Held = true;
        s.ResettleTick = null;
        town.Log(LedgerCategory.War, Importance.Notable, $"A garrison of the watch marches out to hold {s.Def.Name} for {town.Name}, at {Js.Str(o.Upkeep)} gold a day.");
        return true;
    }

    private static void AutoHold(Town town)
    {
        var o = Rules(town).Outpost;
        if (town.Treasury < o.Cost + 600) return;
        var s = town.Sites
            .Where(x => !IsWarCamp(town, x) && HoldBlocked(town, x) == null)
            .OrderBy(x => x.Def.Distance)
            .ThenBy(x => x.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (s != null) Hold(town, s.Def.Id);
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
