namespace CrownAndCompass.Sim.Systems;

/// <summary>A hero sent to rally a shadowed people: how long they stay, the check, and what it does.</summary>
public sealed record RallyRules
{
    /// <summary>Days they spend there, on top of the road both ways.</summary>
    public int Days { get; init; } = 3;

    /// <summary>The Persuasion DC: this, plus the shadow's depth times PerDepth.</summary>
    public int Dc { get; init; } = 10;
    public double PerDepth { get; init; } = 0.1;

    /// <summary>Depth lifted on a success (Base plus PerLevel a hero level), and on a failure.</summary>
    public double Base { get; init; } = 25;
    public double PerLevel { get; init; } = 3;
    public double Failed { get; init; } = 8;
    public int Renown { get; init; } = 3;

    /// <summary>The chance a hero who failed is caught on the road home and comes back hurt.</summary>
    public double Hurt { get; init; } = 0.25;
}

/// <summary>An enemy's shadow over the settlements near it (neighbours.json "shadow").</summary>
public sealed record ShadowRules
{
    /// <summary>Depth a day from a risen menace, and more for each stage past rising.</summary>
    public double PerDay { get; init; } = 0.5;
    public double PerStage { get; init; } = 0.35;

    /// <summary>How much a settlement's own security slows it (or a poor one speeds it).</summary>
    public double Security { get; init; } = 0.6;
    public double Fade { get; init; } = 1.2;

    /// <summary>The depths of its forms: dread, tribute, and the last (a cult, or a puppet ruler).</summary>
    public double Dread { get; init; } = 25;
    public double Tribute { get; init; } = 50;
    public double Last { get; init; } = 80;
    public double DreadSecurity { get; init; } = 8;
    public double TributeProsperity { get; init; } = 10;

    /// <summary>The share of a caravan's load left once they pay tribute, and the power a day the menace takes from it.</summary>
    public double TributeLoad { get; init; } = 0.5;
    public double TributePower { get; init; } = 0.1;
    public int[] Flee { get; init; } = [1, 3];
    public int[] FleeEveryDays { get; init; } = [30, 50];

    /// <summary>Depth lifted when the menace's site nearest them is cleared, by an envoy, and by aid.</summary>
    public double Cleared { get; init; } = 45;
    public double Envoy { get; init; } = 15;
    public double Aid { get; init; } = 10;

    /// <summary>The least depth a mastermind's domain leaves on a settlement it sways.</summary>
    public double Swayed { get; init; } = 55;

    /// <summary>A form the town already heard of is news again only after this many days.</summary>
    public int RetellDays { get; init; } = 150;

    /// <summary>The stand-in mayor sends an envoy against a tribute shadow this often, keeping this much in the treasury.</summary>
    public int AutoEveryDays { get; init; } = 45;
    public double AutoReserve { get; init; } = 400;
    public RallyRules Rally { get; init; } = new();
    public Dictionary<string, string> Forms { get; init; } = [];
    public string Lifting { get; init; } = "";
    public string Lifted { get; init; } = "";
    public string Fled { get; init; } = "";
    public string EnvoyText { get; init; } = "";
    public string AidText { get; init; } = "";
    public string RallyOut { get; init; } = "";
    public string Rallied { get; init; } = "";
    public string RallyFailed { get; init; } = "";
    public string RallyHurt { get; init; } = "";
    public string RallyHome { get; init; } = "";
}

/// <summary>A menace's shadow over a settlement, and the hero the town sent to rally it.</summary>
public sealed class SettlementShadow
{
    public int MenaceId { get; set; }

    /// <summary>0 to 100: dread, then tribute, then a cult or a puppet ruler.</summary>
    public double Depth { get; set; }

    /// <summary>"", "dread", "tribute", "cult" or "puppet".</summary>
    public string Form { get; set; } = "";
    public int SinceDay { get; set; }

    /// <summary>The menace's site nearest them: clearing it loosens the grip.</summary>
    public string? SourceId { get; set; }
    public int NextFleeDay { get; set; }

    /// <summary>The deepest form the town has been told of, and when.</summary>
    public string Told { get; set; } = "";
    public int ToldDay { get; set; }

    /// <summary>A hero of the town's on the way, there, or on the way home.</summary>
    public int? RallyHeroId { get; set; }
    public int RallyArriveTick { get; set; }
    public int RallyReturnTick { get; set; }
    public bool Rallied { get; set; }
}

/// <summary>
/// An enemy's shadow over the villages near it. A risen menace's fear spreads over the settlements in
/// reach, faster the stronger it is and the weaker and more timid they are: first dread (shutters closed,
/// a weaker watch), then tribute (their carts go to the enemy, not the town's market; caravans carry half;
/// families slip away to the town), and for a mind that can rule, a cult or a puppet on their seat. It
/// fades once the menace is gone or out of reach. The town wins a people back by clearing the menace's
/// site nearest them (a bounty; they plead for it), by envoys and aid that remind them they have friends,
/// or by sending a hero to stand in their square and rally them.
/// </summary>
public static class Shadows
{
    private static ShadowRules Rules(Town town) => town.Content.Neighbours.Shadow;

    /// <summary>Whether a menace's seat is in reach of a settlement: its ring or the next, and its arc.</summary>
    public static bool Near(Town town, Menace m, Settlement s)
    {
        if (Quests.Site(town, m.SeatId) is not { RegionId: int rid } || rid < 0 || rid >= town.Regions.Count) return false;
        var mr = town.Regions[rid];
        var home = town.Regions[s.RegionId];
        return Math.Abs(mr.Ring - home.Ring) <= 1 && World.AngleBetween(mr.Bearing, home.Bearing) <= (mr.Arc + home.Arc) / 2 + 1;
    }

    /// <summary>The menace whose shadow lies on a settlement (or would): the one that holds it while it can, else the strongest in reach.</summary>
    public static Menace? Caster(Town town, Settlement s)
    {
        if (s.Shadow is { } sh && town.Saga.Menaces.FirstOrDefault(m => m.Id == sh.MenaceId) is { Alive: true } held && Casts(town, held) && Near(town, held, s)) return held;
        return town.Saga.Menaces
            .Where(m => m.Alive && Casts(town, m) && Near(town, m, s))
            .OrderByDescending(m => (int)m.Stage).ThenByDescending(m => m.Power).ThenBy(m => m.Id)
            .FirstOrDefault();
    }

    private static bool Casts(Town town, Menace m) => m.Origin != Menaces.FromWarband && m.Stage >= MenaceStage.Rising && Quests.Site(town, m.SeatId) is { Cleared: false };

    /// <summary>The menace holding the settlement under its shadow now, if it has reached dread.</summary>
    public static Menace? Over(Town town, Settlement s) =>
        s.Shadow is { Form: not "" } sh ? town.Saga.Menaces.FirstOrDefault(m => m.Id == sh.MenaceId) : null;

    public static bool Shadowed(Settlement s) => !s.Razed && s.Shadow is { Form: not "" };

    /// <summary>They pay the enemy tribute (or worse): no new trade, half loads, no gifts or relief.</summary>
    public static bool Paying(Settlement s) => !s.Razed && s.Shadow is { Form: "tribute" or "cult" or "puppet" };

    public static double TradeShare(Town town, Settlement s) => Paying(s) ? Rules(town).TributeLoad : 1;

    public static double SecurityLoss(Town town, Settlement s) => Shadowed(s) ? Rules(town).DreadSecurity : 0;

    public static double ProsperityLoss(Town town, Settlement s) => Paying(s) ? Rules(town).TributeProsperity : 0;

    /// <summary>The menace's standing site nearest the settlement: its seat or a holding.</summary>
    public static SiteState? SourceOf(Town town, Menace m, Settlement s)
    {
        var home = town.Regions[s.RegionId];
        return new[] { m.SeatId }.Concat(m.Holdings)
            .Select(id => Quests.Site(town, id))
            .Where(x => x is { Cleared: false, RegionId: int rid } && rid >= 0 && rid < town.Regions.Count)
            .OrderBy(x => x!.RegionId == home.Id ? 0 : 1)
            .ThenBy(x => Math.Abs(town.Regions[x!.RegionId!.Value].Ring - home.Ring))
            .ThenBy(x => World.AngleBetween(town.Regions[x!.RegionId!.Value].Bearing, home.Bearing))
            .ThenBy(x => x!.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>The form a depth gives under this menace: a dull one gets no further than its wits allow.</summary>
    private static string FormAt(Town town, Menace? m, double depth, string now)
    {
        var r = Rules(town);
        // Forms climb at their depth and fall back only well below it, so a people on the edge isn't news every day.
        double slack = 10;
        string last = m == null ? "puppet" : Last(town, m);
        if (depth >= r.Last || (depth >= r.Last - slack && now is "cult" or "puppet")) return last;
        if (depth >= r.Tribute || (depth >= r.Tribute - slack && now != "dread" && now != "")) return "tribute";
        if (depth > 0 && (depth >= r.Dread || now != "")) return "dread";
        return "";
    }

    /// <summary>A cult for the hymn-singers and whisperers, a puppet on the seat for the rest.</summary>
    private static string Last(Town town, Menace m) =>
        m.Kind is "cult" or "undead" || Menaces.AmbitionOf(town, m).Id is "plague" or "intrigue" ? "cult" : "puppet";

    /// <summary>How deep a menace's wits let its shadow go: a beast spreads only dread, a low cunning gets tribute.</summary>
    private static double Cap(Town town, Menace m)
    {
        var r = Rules(town);
        int rank = Menaces.CunningRank(town, m.Cunning);
        int crafty = Menaces.CunningRank(town, "crafty");
        if (rank < 0 || crafty < 0) return 100;
        return rank < crafty ? r.Tribute - 1 : rank == crafty ? r.Last - 1 : 100;
    }

    // ---- The daily tick (from Neighbours) ----

    internal static void Daily(Town town, bool autoMayor)
    {
        var freed = new List<(SiteState Source, Menace Menace, Settlement Settlement)>();
        foreach (var s in town.Settlements)
        {
            Rally(town, s);
            if (s.Razed)
            {
                if (s.Shadow is { RallyHeroId: null }) s.Shadow = null;
                continue;
            }
            Spread(town, s, freed);
            Flee(town, s);
        }
        // One site cleared can loosen the grip on several peoples at once: one line for all of them.
        foreach (var g in freed.GroupBy(x => x.Source.Def.Id).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var (source, m, _) = g.First();
            string names = Expedition.ListNames(g.Select(x => x.Settlement.Name).ToList());
            town.Log(LedgerCategory.Town, Importance.Notable,
                Saga.Sentences(Rules(town).Lifting.Replace("{source}", source.Def.Name).Replace("{settlements}", names).Replace("{menace}", m.Name).Replace("{town}", town.Name)));
        }
        if (autoMayor) StandIn(town);
    }

    private static void Spread(Town town, Settlement s, List<(SiteState, Menace, Settlement)> freed)
    {
        var r = Rules(town);
        var m = Caster(town, s);
        var sh = s.Shadow;
        bool quiet = false;
        if (m != null)
        {
            if (sh == null) s.Shadow = sh = new SettlementShadow { MenaceId = m.Id, SinceDay = town.Day, NextFleeDay = town.Day + r.FleeEveryDays[1] };
            else if (sh.MenaceId != m.Id)
            {
                // Another takes over the fear the last one left.
                sh.MenaceId = m.Id;
                sh.SourceId = null;
                if (sh.Form == "") sh.SinceDay = town.Day;
            }
            double rate = (r.PerDay + r.PerStage * Math.Max(0, (int)m.Stage - (int)MenaceStage.Rising)) * (1 + r.Security * (0.5 - s.Security / 100))
                          / Math.Max(0.3, Reigns.Resolve(town, s));
            sh.Depth = Math.Min(Math.Max(sh.Depth, Math.Min(Cap(town, m), sh.Depth + rate)), 100);
            if (Paying(s)) m.Power += r.TributePower;
        }
        else if (sh != null) sh.Depth = Math.Max(0, sh.Depth - r.Fade);
        if (sh == null) return;
        var holder = m ?? town.Saga.Menaces.FirstOrDefault(x => x.Id == sh.MenaceId);
        // Clearing the menace's site nearest them shows them it can bleed.
        if (sh.SourceId != null && Quests.Site(town, sh.SourceId) is { Cleared: true } gone)
        {
            bool had = sh.Form != "";
            sh.Depth = Math.Max(0, sh.Depth - r.Cleared);
            if (had && holder != null && town.Regions[s.RegionId].Known) freed.Add((gone, holder, s));
            quiet = had;
        }
        sh.SourceId = holder is { Alive: true } ? SourceOf(town, holder, s)?.Def.Id : null;
        Settle(town, s, sh, holder, quiet);
    }

    /// <summary>The shadow's form follows its depth: a deeper form is news, and a lifted one too.</summary>
    /// <param name="quiet">The town has just been told the grip loosened: a lift the same day needs no line of its own.</param>
    private static void Settle(Town town, Settlement s, SettlementShadow sh, Menace? m, bool quiet = false)
    {
        var r = Rules(town);
        string was = sh.Form;
        string now = FormAt(town, m, sh.Depth, was);
        bool known = town.Regions[s.RegionId].Known;
        if (now == was)
        {
            if (now == "" && sh.Depth <= 0 && sh.RallyHeroId == null) s.Shadow = null;
            return;
        }
        sh.Form = now;
        if (Rank(now) > Rank(was))
        {
            if (was == "") sh.SinceDay = town.Day;
            if (now == "puppet" && m != null && !Reigns.Puppet(s)) Reigns.Fall(town, s, "puppet", m);
            // Only a deeper grip than the town last heard of is news, or the same one come back after a long while.
            bool news = Rank(now) > Rank(sh.Told) || town.Day - sh.ToldDay >= r.RetellDays;
            if (!news) return;
            sh.Told = now;
            sh.ToldDay = town.Day;
            if (known && m != null && r.Forms.TryGetValue(now, out var text)) town.Log(LedgerCategory.Rumor, Importance.Notable, Text(town, s, m, text));
            return;
        }
        // Fallen below tribute: the creature on their seat is thrown down.
        if (Reigns.Puppet(s) && !Paying(s)) Reigns.Fall(town, s, "freed", m);
        if (now == "")
        {
            if (known && m != null && !quiet) town.Log(LedgerCategory.Town, Importance.Notable, Text(town, s, m, r.Lifted));
            if (sh.RallyHeroId == null) s.Shadow = null;
        }
    }

    private static int Rank(string form) => form switch { "dread" => 1, "tribute" => 2, "cult" or "puppet" => 3, _ => 0 };

    /// <summary>Families slip out from under a tribute shadow and come to the town.</summary>
    private static void Flee(Town town, Settlement s)
    {
        var r = Rules(town);
        if (!Paying(s) || s.Shadow is not { } sh || town.Day < sh.NextFleeDay) return;
        sh.NextFleeDay = town.Day + town.Rng.Range(r.FleeEveryDays[0], r.FleeEveryDays[1] + 1);
        if (!town.Regions[s.RegionId].Known || Over(town, s) is not { } m) return;
        int n = Population.Settle(town, town.Rng.Range(r.Flee[0], r.Flee[1] + 1));
        if (n > 0) town.Log(LedgerCategory.Arrival, Importance.Routine, Text(town, s, m, r.Fled).Replace("{n}", n.ToString()).Replace("{families}", n == 1 ? "family" : "families"));
    }

    // ---- Winning them back ----

    /// <summary>A mastermind's domain swallows a settlement: its shadow falls on it at once.</summary>
    internal static void Sway(Town town, Menace m, Settlement s)
    {
        s.Shadow ??= new SettlementShadow { MenaceId = m.Id, SinceDay = town.Day, NextFleeDay = town.Day + Rules(town).FleeEveryDays[1] };
        s.Shadow.MenaceId = m.Id;
        s.Shadow.Depth = Math.Max(s.Shadow.Depth, Rules(town).Swayed);
    }

    /// <summary>An envoy reached a shadowed settlement: the shadow thins.</summary>
    internal static void Envoy(Town town, Settlement s) => Loosen(town, s, Rules(town).Envoy, Rules(town).EnvoyText);

    /// <summary>The town's aid reached a shadowed settlement: the shadow thins.</summary>
    internal static void Aided(Town town, Settlement s) => Loosen(town, s, Rules(town).Aid, Rules(town).AidText);

    private static void Loosen(Town town, Settlement s, double by, string text)
    {
        if (!Shadowed(s) || Over(town, s) is not { } m) return;
        s.Shadow!.Depth = Math.Max(0, s.Shadow.Depth - by);
        if (text != "") town.Log(LedgerCategory.Town, Importance.Routine, Text(town, s, m, text));
        Settle(town, s, s.Shadow, m);
    }

    /// <summary>Why the town can't send a hero to rally this settlement now, or null if it can.</summary>
    public static string? RallyRefused(Town town, Settlement s)
    {
        if (s.Razed) return "they are gone";
        if (!town.Regions[s.RegionId].Known) return "the town hasn't heard of them";
        if (!Shadowed(s)) return "no enemy's shadow lies on them";
        if (s.Shadow!.RallyHeroId is int id) return $"{town.Agent(id).GivenName} is already on the way";
        if (Rallier(town) == null) return "no hero in town is free to go";
        return null;
    }

    /// <summary>The hero in town best at stirring a crowd, who is free to go.</summary>
    public static Agent? Rallier(Town town) => town.Heroes
        .Where(a => Quests.Available(a))
        .OrderByDescending(a => Skills.Bonus(town, a, "persuasion"))
        .ThenByDescending(a => a.Hero!.Level)
        .ThenBy(a => a.Id)
        .FirstOrDefault();

    /// <summary>The player's lever: send a hero to stand in a shadowed people's square and speak against the enemy.</summary>
    public static bool Rally(Town town, int settlementId, int? heroId = null)
    {
        if (settlementId < 0 || settlementId >= town.Settlements.Count) return false;
        var s = town.Settlements[settlementId];
        if (RallyRefused(town, s) != null) return false;
        var hero = heroId is int hid ? town.Heroes.FirstOrDefault(a => a.Id == hid && Quests.Available(a)) : Rallier(town);
        if (hero == null) return false;
        var r = Rules(town);
        int road = Math.Max(1, town.Regions[s.RegionId].Days) * SimTime.HoursPerDay;
        var sh = s.Shadow!;
        sh.RallyHeroId = hero.Id;
        sh.Rallied = false;
        sh.RallyArriveTick = town.Tick + road;
        sh.RallyReturnTick = sh.RallyArriveTick + r.Rally.Days * SimTime.HoursPerDay + road;
        hero.Hero!.AwayUntil = sh.RallyReturnTick;
        hero.Activity = Activity.Idle;
        hero.LocationId = null;
        town.Log(LedgerCategory.Quest, Importance.Notable, Text(town, s, Over(town, s)!, r.RallyOut).Replace("{hero}", hero.Name), [hero.Id]);
        return true;
    }

    /// <summary>A hero sent to rally a people: they speak there, then come home.</summary>
    private static void Rally(Town town, Settlement s)
    {
        if (s.Shadow is not { RallyHeroId: int id } sh) return;
        var r = Rules(town);
        var rr = r.Rally;
        var hero = town.Agent(id);
        if (hero.Departed || hero.Hero == null)
        {
            sh.RallyHeroId = null;
            return;
        }
        var m = town.Saga.Menaces.FirstOrDefault(x => x.Id == sh.MenaceId);
        string menace = m?.Name ?? "the enemy";
        if (!sh.Rallied && town.Tick >= sh.RallyArriveTick)
        {
            sh.Rallied = true;
            if (s.Razed || sh.Form == "")
            {
                // The shadow was gone before they got there: they are welcomed, and that is all.
            }
            else
            {
                int dc = rr.Dc + (int)Math.Round(sh.Depth * rr.PerDepth);
                var check = Skills.Roll(town, hero, "persuasion", dc, "rally");
                string said = check.Success ? r.Rallied : r.RallyFailed;
                sh.Depth = Math.Max(0, sh.Depth - (check.Success ? rr.Base + rr.PerLevel * hero.Hero.Level : rr.Failed));
                if (check.Success) Notoriety.Deed(town, hero, "plea", rr.Renown);
                town.Log(LedgerCategory.Quest, Importance.Notable,
                    Saga.Sentences(Neighbours.Fill(town, s, said).Replace("{hero}", hero.GivenName).Replace("{menace}", menace).Replace("{total}", check.Total.ToString()).Replace("{dc}", dc.ToString())),
                    [hero.Id]);
                if (check.Success) Remember(town, s, hero);
                else if (town.Rng.Chance(rr.Hurt))
                {
                    hero.Hero.Hp = Math.Max(1, hero.Hero.Hp / 2);
                    town.Log(LedgerCategory.Quest, Importance.Notable, Saga.Sentences(Neighbours.Fill(town, s, r.RallyHurt).Replace("{hero}", hero.GivenName).Replace("{menace}", menace)), [hero.Id]);
                }
                Settle(town, s, sh, m);
            }
        }
        if (town.Tick < sh.RallyReturnTick) return;
        sh.RallyHeroId = null;
        hero.Hero.AwayUntil = null;
        town.Log(LedgerCategory.Quest, Importance.Routine, Saga.Sentences(Neighbours.Fill(town, s, r.RallyHome).Replace("{hero}", hero.GivenName)), [hero.Id]);
        if (sh.Form == "" && sh.Depth <= 0) s.Shadow = null;
    }

    /// <summary>The people there remember who stood up for them.</summary>
    private static void Remember(Town town, Settlement s, Agent hero)
    {
        string memory = $"the day {hero.GivenName} of {town.Name} spoke in their square";
        var c = Neighbours.CastOf(town, s).FirstOrDefault(x => x.Role == "leader");
        if (c != null) Neighbours.Meet(town, s, "leader", memory, town.Content.Neighbours.Cast.Aided, c.Name);
        World.Shift(town, s, town.Content.Neighbours.AidedStanding / 2);
    }

    /// <summary>The stand-in mayor sends an envoy now and then to a people paying an enemy tribute.</summary>
    private static void StandIn(Town town)
    {
        var r = Rules(town);
        var w = town.Content.World.Standing;
        if (Threat.IsActive(town) || town.Day - town.LastEnvoyDay < r.AutoEveryDays || town.Treasury < w.EnvoyCost + r.AutoReserve) return;
        var s = town.Settlements
            .Where(x => Paying(x) && !Reigns.Puppet(x) && town.Regions[x.RegionId].Known)
            .OrderByDescending(x => x.Standing).ThenBy(x => x.Id)
            .FirstOrDefault();
        if (s != null) World.SendEnvoy(town, s.Id);
    }

    private static string Text(Town town, Settlement s, Menace m, string text) =>
        Saga.Sentences(Neighbours.Fill(town, s, text).Replace("{menace}", m.Name));
}
