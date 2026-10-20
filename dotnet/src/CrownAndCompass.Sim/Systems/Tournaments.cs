namespace CrownAndCompass.Sim.Systems;

/// <summary>Numbers for the yearly tourney (content/game/tournaments.json).</summary>
public sealed record TourneyRules
{
    /// <summary>The season it is held in, the day of it the council proposes it, and the day of it the lists open.</summary>
    public string Season { get; init; } = "summer";
    public int ProposeDay { get; init; } = 6;
    public int OpenDay { get; init; } = 16;

    /// <summary>Fewest days between the proposal and the opening: a year that starts too late for it has none.</summary>
    public int LeadDays { get; init; } = 3;

    /// <summary>The charter the town must hold first.</summary>
    public string Tier { get; init; } = "village";

    /// <summary>The hours the heats of an event's day are spread over.</summary>
    public int StartHour { get; init; } = 9;
    public int EndHour { get; init; } = 18;

    /// <summary>The cost grows with the town: gold × (1 + population / this).</summary>
    public double PerPeople { get; init; } = 400;
    public List<TourneyLevel> Levels { get; init; } = [];
    public TourneySkip Skip { get; init; } = new();

    /// <summary>Gold the council keeps back when it holds a modest tourney the mayor never answered.</summary>
    public double DefaultReserve { get; init; } = 300;
    public TourneyArena Arena { get; init; } = new();

    /// <summary>What the home crowd's cheering adds to the town's own heroes' rolls.</summary>
    public int Home { get; init; } = 1;

    /// <summary>Gold each visitor pays at the lists on a tourney day.</summary>
    public double Gate { get; init; } = 0.5;
    public TourneyGrounds Grounds { get; init; } = new();
    public List<TourneyEventDef> Events { get; init; } = [];
    public TourneyChampions Champions { get; init; } = new();
    public TourneyBetting Betting { get; init; } = new();
    public TourneyStandIn StandIn { get; init; } = new();

    public TourneyEventDef? Event(string id) => Events.FirstOrDefault(e => e.Id == id);
}

/// <summary>How grandly the tourney is held: its cost, its field, its crowd, the town's mood and the winners' renown.</summary>
public sealed record TourneyLevel
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public double Gold { get; init; }

    /// <summary>Share of the treasury it costs on top: a rich town's tourney is a rich town's.</summary>
    public double Wealth { get; init; }

    /// <summary>Riders in each event (a power of two).</summary>
    public int Entrants { get; init; } = 4;

    /// <summary>Visitors on tourney days, times the season's own.</summary>
    public double Tourists { get; init; } = 2;
    public double Mood { get; init; }

    /// <summary>Renown for the winner and for the runner-up of each event.</summary>
    public List<int> Renown { get; init; } = [5, 2];

    /// <summary>The winner's purse (part of the cost).</summary>
    public double Purse { get; init; }
    public string Text { get; init; } = "";
}

public sealed record TourneySkip
{
    public double Mandate { get; init; } = 3;
    public double Mood { get; init; } = -2;
    public int Days { get; init; } = 6;
}

public sealed record TourneyArena
{
    /// <summary>Crowd, mood and renown, times this, when the town has an arena.</summary>
    public double Scale { get; init; } = 1.35;
}

/// <summary>The open ground the lists are put up on: a footprint in plan cells, and how far from the centre to look for it, in tiles.</summary>
public sealed record TourneyGrounds
{
    public int W { get; init; } = 20;
    public int D { get; init; } = 10;
    public double Reach { get; init; } = 16;
}

/// <summary>One event: who it suits, its rounds (each an ability), and its words.</summary>
public sealed record TourneyEventDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>The notoriety.json deed whose epithets the bards give its winners.</summary>
    public string Deed { get; init; } = "";
    public List<string> Classes { get; init; } = [];
    public List<TourneyRound> Rounds { get; init; } = [];

    /// <summary>"{winner} unhorses {loser}".</summary>
    public string Win { get; init; } = "";
    public string Blurb { get; init; } = "";
}

public sealed record TourneyRound
{
    /// <summary>str, dex, con, int, wis, cha, or "cast" for the class's casting ability.</summary>
    public string Ability { get; init; } = "";
    public string Text { get; init; } = "";
}

public sealed record TourneyChampions
{
    /// <summary>Champions' levels range this far below and above the town's heroes' middle level.</summary>
    public int LevelBelow { get; init; } = 2;
    public int LevelAbove { get; init; } = 1;

    /// <summary>Standing with a settlement for inviting its champion, and for its champion winning.</summary>
    public double Invited { get; init; } = 1;
    public double Win { get; init; } = 4;

    /// <summary>Where a champion with no settlement to ride for comes from.</summary>
    public string Wanderer { get; init; } = "the open road";
}

public sealed record TourneyBetting
{
    /// <summary>The bookmakers' share off fair odds.</summary>
    public double Edge { get; init; } = 0.05;
    public double Min { get; init; } = 10;

    /// <summary>Largest stake, as a share of the treasury.</summary>
    public double MaxShare { get; init; } = 0.25;

    /// <summary>The stakes the card offers.</summary>
    public List<double> Stakes { get; init; } = [];
}

public sealed record TourneyStandIn
{
    /// <summary>Gold kept back before a modest tourney (on top of its cost); treasuries for a grand and a legendary one.</summary>
    public double Reserve { get; init; } = 400;
    public double Grand { get; init; } = 3500;
    public double Legendary { get; init; } = 12000;

    /// <summary>Now and then, on a town hero with at least this chance, with a deep treasury, a small share of it.</summary>
    public double BetChance { get; init; } = 0.15;
    public double BetFloor { get; init; } = 0.4;
    public double BetReserve { get; init; } = 3000;
    public double BetShare { get; init; } = 0.01;
    public double BetMax { get; init; } = 150;
}

public enum TourneyStage
{
    None,
    Proposed,
    Funded,
    Running,
    Over,
    Skipped,
}

/// <summary>Someone riding in the tourney: a hero of the town, or a settlement's champion with a sheet of their own.</summary>
public sealed class Entrant
{
    public int Id { get; init; }
    public string Name { get; set; } = "";

    /// <summary>The town's hero, or null for a champion.</summary>
    public int? AgentId { get; init; }

    /// <summary>The settlement a champion rides for, if any.</summary>
    public int? SettlementId { get; init; }

    /// <summary>"Ironhold", or the town's name for its own.</summary>
    public string From { get; set; } = "";
    public string Class { get; set; } = "";
    public int Level { get; set; } = 1;

    /// <summary>A champion's ability scores (a hero's are their own).</summary>
    public Abilities Abilities { get; set; } = new();

    /// <summary>Their class suits the event: full proficiency.</summary>
    public bool Fit { get; set; }
}

/// <summary>One round of a heat: each side's d20 and bonus.</summary>
public sealed class Pass
{
    public string Ability { get; init; } = "";
    public int DieA { get; init; }
    public int BonusA { get; init; }
    public int DieB { get; init; }
    public int BonusB { get; init; }
    public int TotalA => DieA + BonusA;
    public int TotalB => DieB + BonusB;
}

/// <summary>Two riders meet: their passes, the winner, and a walkover when one never came.</summary>
public sealed class Heat
{
    /// <summary>0 for the first round of the draw, then on to the final.</summary>
    public int Round { get; init; }
    public int A { get; init; }
    public int B { get; init; }

    /// <summary>The hour it is ridden (tick).</summary>
    public int Tick { get; set; }
    public List<Pass> Passes { get; } = [];
    public int? Winner { get; set; }
    public bool Walkover { get; set; }
    public int ScoreA { get; set; }
    public int ScoreB { get; set; }
}

/// <summary>The mayor's stake on an event.</summary>
public sealed class TourneyBet
{
    public int Entrant { get; init; }
    public double Stake { get; init; }

    /// <summary>What a winning stake pays back, times the stake.</summary>
    public double Odds { get; init; }
    public bool Settled { get; set; }
    public double Paid { get; set; }
}

/// <summary>An event of this year's tourney: its draw, its heats, and the mayor's bet.</summary>
public sealed class TourneyEvent
{
    public string Def { get; init; } = "";
    public int Day { get; set; }

    /// <summary>Entrant ids in the order of the draw: first against second, third against fourth.</summary>
    public List<int> Draw { get; } = [];
    public List<Heat> Heats { get; } = [];
    public int? Winner { get; set; }
    public int? RunnerUp { get; set; }
    public TourneyBet? Bet { get; set; }
    public bool Started => Heats.Count > 0;
    public bool Done => Winner != null;
}

/// <summary>A past event's winner, for the card's roll of champions.</summary>
public sealed class TourneyHonour
{
    public int Year { get; init; }
    public string Event { get; init; } = "";
    public string Name { get; init; } = "";
    public string From { get; init; } = "";
}

/// <summary>This year's tourney, where its lists stand, and what the town remembers of the others.</summary>
public sealed class TourneyState
{
    public TourneyStage Stage { get; set; }

    /// <summary>The year it was last proposed.</summary>
    public int Year { get; set; }
    public int ProposedDay { get; set; }
    public int OpenDay { get; set; }

    /// <summary>The level funded (index into the levels), and what it cost.</summary>
    public int Level { get; set; } = -1;
    public double Cost { get; set; }
    public bool Arena { get; set; }

    /// <summary>The lists, in tiles from the town's corner, and their heading.</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public double Yaw { get; set; }
    public List<Entrant> Entrants { get; } = [];
    public List<TourneyEvent> Events { get; } = [];

    /// <summary>The town skipped this year's: it sulks until this day.</summary>
    public int SulkUntil { get; set; } = -1;

    /// <summary>This year's crowd and gate, and all the mayor's bets have won or lost.</summary>
    public int Visitors { get; set; }
    public double GateGold { get; set; }
    public double Winnings { get; set; }
    public int Held { get; set; }
    public List<TourneyHonour> Honours { get; } = [];
}

/// <summary>
/// The yearly tourney (after Quadriga): a summer highlight that is a real gold sink for a rich town. Once the
/// town holds its village charter the council proposes it; the mayor funds it modest, grand or legendary (the
/// cost grows with the treasury) or skips it and disappoints the town. Four events, one a day: the joust, the
/// archery, the wizards' duel and the race, each a knockout draw of heats, every heat a short series of opposed
/// d20 rolls on the riders' own ability modifiers and proficiency. The town's heroes ride where their class
/// fits, and named champions of the neighbouring settlements fill the field. The mayor may stake gold on one
/// rider an event at fair odds; winners earn renown and epithets, a champion's win warms their settlement, and
/// the crowds pay at the gate, spend at the shops and cheer the town up. An arena makes it all bigger. Nothing
/// here stops the clock: the heats are ridden hour by hour whether anyone is watching.
/// </summary>
public static class Tournaments
{
    public static TourneyRules Rules(Town town) => town.Content.Tourney;

    public static TourneyLevel? LevelOf(Town town) =>
        town.Tourney.Level >= 0 && town.Tourney.Level < Rules(town).Levels.Count ? Rules(town).Levels[town.Tourney.Level] : null;

    public static TourneyEventDef DefOf(Town town, TourneyEvent e) => Rules(town).Event(e.Def) ?? throw new KeyNotFoundException($"Unknown tourney event {e.Def}");

    public static Entrant Rider(Town town, int id) => town.Tourney.Entrants[id - 1];

    /// <summary>The tourney is being ridden today (or the lists stand ready for the day's heats).</summary>
    public static bool On(Town town) =>
        town.Tourney.Stage == TourneyStage.Running && town.Tourney.Events.Any(e => e.Day == town.Day);

    /// <summary>The town has an arena (or the Grand Arena institution) to hold it in.</summary>
    public static bool HasArena(Town town) =>
        town.Buildings.Any(b => b.Def.Id == "arena" && b.Complete && !b.Closed) || Institutions.Find(town, "grand-arena") is { Shut: false };

    private static double Scale(Town town) => town.Tourney.Arena ? Rules(town).Arena.Scale : 1;

    /// <summary>What a level would cost now.</summary>
    public static double Cost(Town town, TourneyLevel l) =>
        Js.Round(l.Gold * (1 + town.Population / Math.Max(1, Rules(town).PerPeople)) + l.Wealth * Math.Max(0, town.Treasury));

    // Effects, read by the other systems ----------------------------------------------------------

    /// <summary>Mood on tourney days, or the sulk after a skipped one.</summary>
    public static double Mood(Town town)
    {
        if (On(town) && LevelOf(town) is { } l) return l.Mood * Scale(town);
        return town.Day <= town.Tourney.SulkUntil ? Rules(town).Skip.Mood : 0;
    }

    /// <summary>Visitors on tourney days, times the season's own.</summary>
    public static double Tourists(Town town) =>
        On(town) && LevelOf(town) is { } l ? 1 + (l.Tourists - 1) * Scale(town) : 1;

    // The year --------------------------------------------------------------------------------------

    /// <summary>Propose the tourney in its season, hold the mayor to an answer, open the lists and count the crowd.</summary>
    public static void Daily(Town town, bool autoMayor)
    {
        var r = Rules(town);
        var st = town.Tourney;
        if (r.Levels.Count == 0 || r.Events.Count == 0) return;
        int year = Seasons.Year(town);
        if (st.Year != year && st.Stage is not (TourneyStage.Proposed or TourneyStage.Funded or TourneyStage.Running)
            && Seasons.Current(town).Id == r.Season && Seasons.DayOf(town) >= r.ProposeDay)
        {
            st.Year = year;
            if (City.Reached(town, r.Tier) && Seasons.DayOf(town) <= r.OpenDay - r.LeadDays) Propose(town);
        }
        if (st.Stage == TourneyStage.Proposed)
        {
            if (autoMayor) StandIn(town);
            else if (town.Day >= st.OpenDay) Lapse(town);
        }
        if (st.Stage == TourneyStage.Funded && town.Day >= st.OpenDay) Open(town);
        if (st.Stage == TourneyStage.Running && st.Events.FirstOrDefault(e => e.Day == town.Day && !e.Started) is { } today) Stand(town, today);
    }

    /// <summary>
    /// The morning of an event: a hero of the town who is away (or gone) gives up their place to another at home
    /// whose class fits, or to a champion of the settlements; a stake on them is handed back.
    /// </summary>
    private static void Stand(Town town, TourneyEvent e)
    {
        var st = town.Tourney;
        var d = DefOf(town, e);
        var riding = st.Events.SelectMany(x => x.Draw).Select(id => Rider(town, id).AgentId).OfType<int>().ToHashSet();
        int middle = Middle(town);
        for (int i = 0; i < e.Draw.Count; i++)
        {
            var en = Rider(town, e.Draw[i]);
            if (!Absent(town, en)) continue;
            var sub = town.Heroes.Where(a => !a.IsAway && !a.Dead && !riding.Contains(a.Id) && d.Classes.Contains(a.Hero!.Cls.Id))
                .OrderByDescending(a => Strength(town, d, HeroEntrant(town, a, d, 0))).ThenBy(a => a.Id).FirstOrDefault();
            var settlements = town.Settlements.Where(s => !s.Razed).OrderByDescending(s => s.Met).ThenByDescending(s => s.Standing).ThenBy(s => s.Id).ToList();
            var next = sub != null ? HeroEntrant(town, sub, d, st.Entrants.Count + 1)
                : Champion(town, d, settlements.Count == 0 ? null : settlements[town.Rng.Range(0, settlements.Count)], middle, st.Entrants.Count + 1);
            if (sub != null) riding.Add(sub.Id);
            st.Entrants.Add(next);
            if (e.Bet is { Settled: false } bet && bet.Entrant == en.Id)
            {
                town.Book(TreasuryAccount.Tourney, bet.Stake);
                e.Bet = null;
            }
            e.Draw[i] = next.Id;
        }
    }

    /// <summary>The middle level of the heroes at home, which the champions are matched to.</summary>
    private static int Middle(Town town)
    {
        var home = town.Heroes.Where(a => !a.IsAway && !a.Dead).Select(a => a.Hero!.Level).Order().ToList();
        return home.Count == 0 ? 3 : home[home.Count / 2];
    }

    private static void Propose(Town town)
    {
        var r = Rules(town);
        var st = town.Tourney;
        st.Stage = TourneyStage.Proposed;
        st.ProposedDay = town.Day;
        st.OpenDay = town.Day + (r.OpenDay - Seasons.DayOf(town));
        st.Level = -1;
        st.Cost = 0;
        st.Entrants.Clear();
        st.Events.Clear();
        st.Visitors = 0;
        st.GateGold = 0;
        string season = Seasons.Current(town).Name.ToLowerInvariant();
        town.Log(LedgerCategory.Council, Importance.Notable,
            $"The council proposes a {season} tourney: jousting, archery, a wizards' duel and a race, with champions invited from the holds and villages round {town.Name}. The lists would open in {Days(st.OpenDay - town.Day)}, if the mayor pays for them.");
    }

    /// <summary>The mayor never answered: the council holds a modest one if the treasury can bear it, and lets it drop if not.</summary>
    private static void Lapse(Town town)
    {
        var r = Rules(town);
        if (town.Treasury >= Cost(town, r.Levels[0]) + r.DefaultReserve) Fund(town, 0);
        else Skip(town);
    }

    /// <summary>Pay for the tourney at a level: the heralds post the draw at once.</summary>
    public static bool Fund(Town town, int level)
    {
        var r = Rules(town);
        var st = town.Tourney;
        if (st.Stage != TourneyStage.Proposed || level < 0 || level >= r.Levels.Count) return false;
        var l = r.Levels[level];
        double cost = Cost(town, l);
        if (town.Treasury < cost) return false;
        town.Book(TreasuryAccount.Tourney, -cost);
        st.Stage = TourneyStage.Funded;
        st.Level = level;
        st.Cost = cost;
        st.Arena = HasArena(town);
        Draw(town, l);
        int heroes = st.Entrants.Count(e => e.AgentId != null);
        var from = st.Entrants.Where(e => e.SettlementId != null).Select(e => e.From).Distinct().ToList();
        town.Log(LedgerCategory.Town, Importance.Notable,
            $"Mayor {town.Stories.Mayor.Name} pays {Js.Str(cost)} gold for a {l.Name.ToLowerInvariant()} tourney{(st.Arena ? " in the arena" : "")}. {l.Text} " +
            $"The heralds post the lists: {(heroes == 0 ? "none of the town's heroes" : heroes == 1 ? "one of the town's heroes" : $"{heroes} of the town's heroes")}" +
            (from.Count > 0 ? $" against champions from {Expedition.ListNames(from.Take(4).ToList())}{(from.Count > 4 ? " and beyond" : "")}." : " against wandering champions."));
        return true;
    }

    /// <summary>Let the tourney go: the townsfolk are let down and say so.</summary>
    public static bool Skip(Town town)
    {
        var r = Rules(town);
        var st = town.Tourney;
        if (st.Stage != TourneyStage.Proposed) return false;
        st.Stage = TourneyStage.Skipped;
        st.SulkUntil = town.Day + r.Skip.Days;
        town.Mandate = Math.Clamp(town.Mandate - r.Skip.Mandate, 0, 100);
        town.Log(LedgerCategory.Town, Importance.Notable, $"There will be no tourney in {town.Name} this year. The taverns grumble that the council has gold for everything but a bit of fun.");
        return true;
    }

    /// <summary>The lists go up and the crowds come in.</summary>
    private static void Open(Town town)
    {
        var st = town.Tourney;
        st.Stage = TourneyStage.Running;
        Grounds(town);
        foreach (var s in st.Entrants.Where(e => e.SettlementId != null).Select(e => e.SettlementId!.Value).Distinct().Order())
            if (town.Settlements.FirstOrDefault(x => x.Id == s) is { } settlement)
                settlement.Standing = Math.Clamp(settlement.Standing + Rules(town).Champions.Invited, -100, 100);
        var first = DefOf(town, st.Events[0]);
        town.Log(LedgerCategory.Social, Importance.Notable,
            $"The lists open {(st.Arena ? "in the arena" : "outside " + town.Name)} under a hundred banners. Visitors crowd the inns, and {first.Name} is ridden today.");
    }

    /// <summary>Visitors pay at the lists, as the first heat of the day is called.</summary>
    private static void Gate(Town town)
    {
        var st = town.Tourney;
        int crowd = town.Seasons.Tourists;
        double gold = Js.Round(crowd * Rules(town).Gate);
        st.Visitors += crowd;
        if (gold <= 0) return;
        st.GateGold += gold;
        town.Book(TreasuryAccount.Tourney, gold);
    }

    /// <summary>Where the lists go: the arena's floor, or the open ground nearest the hall a field fits on.</summary>
    private static void Grounds(Town town)
    {
        var st = town.Tourney;
        var g = Rules(town).Grounds;
        if (town.Buildings.Where(b => b.Def.Id == "arena" && b.Complete && !b.Closed).OrderBy(b => b.Id).FirstOrDefault() is { } arena)
        {
            var lot = town.Lot(arena.LotId);
            (st.X, st.Y, st.Yaw) = (lot.X, lot.Y, lot.Yaw);
            return;
        }
        for (double r = 3; r <= g.Reach; r += 0.5)
        {
            int n = Math.Max(8, (int)Math.Round(r * 4));
            for (int i = 0; i < n; i++)
            {
                double a = Math.PI * 2 * i / n;
                double x = Town.Center + Math.Cos(a) * r, y = Town.Center + Math.Sin(a) * r;
                foreach (double yaw in (double[])[0, Math.PI / 2])
                    if (Land.Fits(town, x, y, g.W, g.D, yaw, null))
                    {
                        (st.X, st.Y, st.Yaw) = (x, y, yaw);
                        return;
                    }
            }
        }
        // No field free near the hall: the square takes it.
        (st.X, st.Y, st.Yaw) = (Town.Center, Town.Center + 2.5, 0);
    }

    // The draw ---------------------------------------------------------------------------------------

    /// <summary>The field for each event: the town's heroes whose class fits (half the places at most), champions of the settlements for the rest.</summary>
    private static void Draw(Town town, TourneyLevel l)
    {
        var r = Rules(town);
        var st = town.Tourney;
        st.Entrants.Clear();
        st.Events.Clear();
        var taken = new HashSet<int>();
        // Heroes out on the road are entered too: the heralds expect them back, and the morning of their event finds a stand-in if not.
        var home = town.Heroes.Where(a => !a.Dead && !a.Departed).OrderBy(a => a.Id).ToList();
        int middle = Middle(town);
        var settlements = town.Settlements.Where(s => !s.Razed).OrderByDescending(s => s.Met).ThenByDescending(s => s.Standing).ThenBy(s => s.Id).ToList();
        int next = 0;
        for (int i = 0; i < r.Events.Count; i++)
        {
            var d = r.Events[i];
            var e = new TourneyEvent { Def = d.Id, Day = st.OpenDay + i };
            // The town's best at it, one event each.
            var riders = home.Where(a => !taken.Contains(a.Id) && d.Classes.Contains(a.Hero!.Cls.Id))
                .OrderByDescending(a => Strength(town, d, HeroEntrant(town, a, d, 0))).ThenBy(a => a.Id)
                .Take(l.Entrants / 2).ToList();
            foreach (var a in riders)
            {
                taken.Add(a.Id);
                var en = HeroEntrant(town, a, d, st.Entrants.Count + 1);
                st.Entrants.Add(en);
                e.Draw.Add(en.Id);
            }
            while (e.Draw.Count < l.Entrants)
            {
                var s = settlements.Count == 0 ? null : settlements[next++ % settlements.Count];
                var en = Champion(town, d, s, middle, st.Entrants.Count + 1);
                st.Entrants.Add(en);
                e.Draw.Add(en.Id);
            }
            // The heralds draw lots for who meets whom.
            for (int k = e.Draw.Count - 1; k > 0; k--)
            {
                int j = town.Rng.Range(0, k + 1);
                (e.Draw[k], e.Draw[j]) = (e.Draw[j], e.Draw[k]);
            }
            st.Events.Add(e);
        }
    }

    private static Entrant HeroEntrant(Town town, Agent a, TourneyEventDef d, int id) => new()
    {
        Id = id,
        Name = Notoriety.Known(a),
        AgentId = a.Id,
        From = town.Name,
        Class = a.Hero!.Cls.Id,
        Level = a.Hero.Level,
        Fit = d.Classes.Contains(a.Hero.Cls.Id),
    };

    /// <summary>A settlement's champion: a name of its people, a class the event suits, a level near the town's heroes', and the scores to match.</summary>
    private static Entrant Champion(Town town, TourneyEventDef d, Settlement? s, int middle, int id)
    {
        var r = Rules(town).Champions;
        var rng = town.Rng;
        var classes = d.Classes.Where(c => town.Content.Classes.Any(x => x.Id == c)).ToList();
        string cls = classes.Count == 0 ? town.Content.Classes[0].Id : classes[rng.Range(0, classes.Count)];
        var race = s != null && town.Content.Races.FirstOrDefault(x => x.Id == s.Kin.Race) is { } kin ? kin : town.Content.Races[0];
        string name = race.Given.Count == 0 ? "a masked champion" : race.Family.Count == 0 ? rng.Pick(race.Given) : $"{rng.Pick(race.Given)} {rng.Pick(race.Family)}";
        int level = Math.Max(1, middle + rng.Range(-r.LevelBelow, r.LevelAbove + 1));
        var ab = new Abilities();
        foreach (var k in (string[])[Ability.Str, Ability.Dex, Ability.Con, Ability.Int, Ability.Wis, Ability.Cha]) ab[k] = 9 + rng.Range(0, 5);
        var def = town.Content.Classes.First(x => x.Id == cls);
        // Their best at the event's first round, good at the rest.
        for (int i = 0; i < d.Rounds.Count; i++)
        {
            string k = d.Rounds[i].Ability == "cast" ? (def.Casting != "" ? def.Casting : Ability.Cha) : d.Rounds[i].Ability;
            ab[k] = Math.Max(ab[k], Math.Min(20, (i == 0 ? 13 : 11) + rng.Range(0, 3) + level / 4));
        }
        return new Entrant
        {
            Id = id,
            Name = name,
            SettlementId = s?.Id,
            From = s?.Name ?? r.Wanderer,
            Class = cls,
            Level = level,
            Abilities = ab,
            Fit = true,
        };
    }

    // The heats ------------------------------------------------------------------------------------

    /// <summary>The ability a round calls on, for this rider: "cast" is their class's casting ability, or their best mind if they have none.</summary>
    public static string AbilityFor(Town town, Entrant e, string ability)
    {
        if (ability != "cast") return ability;
        var cls = town.Content.Classes.FirstOrDefault(c => c.Id == e.Class);
        if (cls is { Casting: not "" }) return cls.Casting;
        var ab = Scores(town, e);
        return new[] { Ability.Int, Ability.Wis, Ability.Cha }.OrderByDescending(k => ab[k]).First();
    }

    private static Abilities Scores(Town town, Entrant e) => e.AgentId is int id ? town.Agent(id).Abilities : e.Abilities;

    /// <summary>A rider's bonus at a round: the ability's modifier and their proficiency (half where their class doesn't fit), and the home crowd for the town's own.</summary>
    public static int Bonus(Town town, Entrant e, string ability)
    {
        int prof = Skills.HeroProficiency(e.Level);
        return Rng.Modifier(Scores(town, e)[AbilityFor(town, e, ability)]) + (e.Fit ? prof : prof / 2) + (e.AgentId != null ? Rules(town).Home : 0);
    }

    /// <summary>A rough measure of a rider at an event, for choosing the town's best: their bonuses added up.</summary>
    private static int Strength(Town town, TourneyEventDef d, Entrant e) => d.Rounds.Sum(x => Bonus(town, e, x.Ability));

    /// <summary>The town's hero can't ride: away, dead or gone.</summary>
    public static bool Absent(Town town, Entrant e) => e.AgentId is int id && town.Agent(id) is var a && (a.Departed || a.Dead || a.IsAway);

    /// <summary>The hours of an event's day its heats are ridden at, one per heat of the draw.</summary>
    public static List<int> Hours(Town town, TourneyEvent e)
    {
        var r = Rules(town);
        int heats = Math.Max(1, e.Draw.Count - 1);
        int gap = Math.Max(1, (r.EndHour - r.StartHour) / heats);
        return Enumerable.Range(0, heats).Select(i => Math.Min(23, r.StartHour + i * gap)).ToList();
    }

    /// <summary>The next pair to meet in an event's draw, or null when it is decided.</summary>
    public static (int A, int B, int Round)? NextPair(TourneyEvent e)
    {
        var alive = e.Draw.ToList();
        int round = 0;
        int done = 0;
        while (alive.Count > 1)
        {
            var winners = new List<int>();
            for (int i = 0; i + 1 < alive.Count; i += 2)
            {
                if (done < e.Heats.Count)
                {
                    winners.Add(e.Heats[done++].Winner!.Value);
                    continue;
                }
                return (alive[i], alive[i + 1], round);
            }
            alive = winners;
            round++;
        }
        return null;
    }

    /// <summary>Each hour of a tourney day, the heat due then is ridden.</summary>
    public static void Hourly(Town town)
    {
        var st = town.Tourney;
        if (st.Stage != TourneyStage.Running) return;
        if (town.Hour == Rules(town).StartHour && On(town)) Gate(town);
        var e = st.Events.FirstOrDefault(x => x.Day == town.Day && !x.Done);
        if (e == null) return;
        if (!Hours(town, e).Contains(town.Hour)) return;
        if (NextPair(e) is not var (a, b, round)) return;
        Ride(town, e, a, b, round);
        if (NextPair(e) == null) Crown(town, e);
    }

    private static void Ride(Town town, TourneyEvent e, int a, int b, int round)
    {
        var d = DefOf(town, e);
        var ea = Rider(town, a);
        var eb = Rider(town, b);
        var h = new Heat { Round = round, A = a, B = b, Tick = town.Tick };
        bool goneA = Absent(town, ea), goneB = Absent(town, eb);
        if (goneA || goneB)
        {
            h.Walkover = true;
            h.Winner = goneA && !goneB ? b : a;
            e.Heats.Add(h);
            return;
        }
        var rng = town.Rng;
        void Clash(string ability)
        {
            var p = new Pass { Ability = ability, DieA = rng.D20(), BonusA = Bonus(town, ea, ability), DieB = rng.D20(), BonusB = Bonus(town, eb, ability) };
            h.Passes.Add(p);
            if (p.TotalA > p.TotalB) h.ScoreA++;
            else if (p.TotalB > p.TotalA) h.ScoreB++;
        }
        foreach (var r in d.Rounds) Clash(r.Ability);
        // Level after the rounds: they ride again until one has it.
        for (int i = 0; i < 5 && h.ScoreA == h.ScoreB; i++) Clash(d.Rounds[0].Ability);
        if (h.ScoreA == h.ScoreB)
        {
            int ba = Bonus(town, ea, d.Rounds[0].Ability), bb = Bonus(town, eb, d.Rounds[0].Ability);
            if (ba >= bb) h.ScoreA++;
            else h.ScoreB++;
        }
        h.Winner = h.ScoreA > h.ScoreB ? a : b;
        e.Heats.Add(h);
    }

    /// <summary>An event is won: renown and the purse for a hero of the town, standing for a champion's settlement, and the mayor's stake settled.</summary>
    private static void Crown(Town town, TourneyEvent e)
    {
        var st = town.Tourney;
        var r = Rules(town);
        var d = DefOf(town, e);
        var l = LevelOf(town)!;
        var final = e.Heats[^1];
        e.Winner = final.Winner;
        e.RunnerUp = final.Winner == final.A ? final.B : final.A;
        var w = Rider(town, e.Winner!.Value);
        var ru = Rider(town, e.RunnerUp!.Value);
        double scale = Scale(town);
        var said = new List<string>();
        string Who(Entrant x) => x.AgentId != null ? x.Name : $"{x.Name} of {x.From}";
        string score = final.Walkover ? "when the other never came to the lists" : $"{Math.Max(final.ScoreA, final.ScoreB)} passes to {Math.Min(final.ScoreA, final.ScoreB)}";
        string win = d.Win.Replace("{winner}", Who(w)).Replace("{loser}", Who(ru));
        said.Add($"{Cap(win)} in the final of {d.Name}, {score}.");
        if (w.AgentId is int wid)
        {
            var hero = town.Agent(wid);
            int fame = (int)Math.Round(l.Renown[0] * scale);
            Notoriety.Deed(town, hero, d.Deed, fame);
            if (hero.Hero!.PartyName is string party) Notoriety.CompanyDeed(town, party, (fame + 1) / 2);
            hero.Gold += l.Purse;
            hero.Hero.Pay += l.Purse;
            w.Name = Notoriety.Known(hero);
            said.Add($"The town cheers its own, and {hero.GivenName} takes the purse of {Js.Str(l.Purse)} gold.");
        }
        else if (w.SettlementId is int sid && town.Settlements.FirstOrDefault(x => x.Id == sid) is { } s)
        {
            s.Standing = Math.Clamp(s.Standing + r.Champions.Win, -100, 100);
            string[] home = [$"They carry the purse home to {s.Name}, which will remember the welcome.", $"{s.Name} will drink to that for a month.", $"The purse goes home to {s.Name}, and the town's welcome with it."];
            said.Add(home[st.Events.IndexOf(e) % home.Length]);
        }
        if (ru.AgentId is int rid && l.Renown.Count > 1) Notoriety.Deed(town, town.Agent(rid), "lists", (int)Math.Round(l.Renown[1] * scale));
        if (e.Bet is { Settled: false } bet)
        {
            bet.Settled = true;
            var on = Rider(town, bet.Entrant);
            if (bet.Entrant == e.Winner)
            {
                bet.Paid = Js.Round(bet.Stake * bet.Odds);
                town.Book(TreasuryAccount.Tourney, bet.Paid);
                st.Winnings += bet.Paid - bet.Stake;
                said.Add($"The mayor's {Js.Str(bet.Stake)} gold on {on.Name} pays {Js.Str(bet.Paid)}.");
            }
            else
            {
                st.Winnings -= bet.Stake;
                said.Add($"The mayor's {Js.Str(bet.Stake)} gold on {on.Name} is lost.");
            }
        }
        st.Honours.Add(new TourneyHonour { Year = st.Year, Event = d.Name, Name = w.Name, From = w.From });
        if (st.Honours.Count > 24) st.Honours.RemoveAt(0);
        var agents = new List<int>();
        if (w.AgentId is int a1) agents.Add(a1);
        town.Log(LedgerCategory.Social, Importance.Notable, string.Join(" ", said), agents);
        if (st.Events.All(x => x.Done)) Close(town);
    }

    private static void Close(Town town)
    {
        var st = town.Tourney;
        st.Stage = TourneyStage.Over;
        st.Held++;
        int ours = st.Events.Count(e => e.Winner is int w && Rider(town, w).AgentId != null);
        double bets = Js.Sum(st.Events.Where(e => e.Bet != null), e => e.Bet!.Paid - e.Bet.Stake);
        town.Log(LedgerCategory.Town, Importance.Notable,
            $"The tourney ends and the banners come down. {Js.Str(st.Visitors)} visitors came and paid {Js.Str(st.GateGold)} gold to watch; " +
            $"the town's own heroes won {(ours == 0 ? "nothing" : ours == 1 ? "one event" : $"{ours} events")} of {st.Events.Count}." +
            (st.Events.Any(e => e.Bet != null) ? $" The mayor's bets {(bets >= 0 ? $"came out {Js.Str(bets)} gold ahead" : $"cost {Js.Str(-bets)} gold")}." : ""));
    }

    // Odds and bets ----------------------------------------------------------------------------------

    /// <summary>The chance the first wins a heat against the second: each round's opposed d20s counted exactly, a level score ridden off on the first round.</summary>
    public static double HeatChance(Town town, TourneyEventDef d, Entrant a, Entrant b)
    {
        // Points ahead after each round: the first minus the second.
        var lead = new Dictionary<int, double> { [0] = 1 };
        foreach (var r in d.Rounds)
        {
            var (win, lose) = PassChance(Bonus(town, a, r.Ability), Bonus(town, b, r.Ability));
            var next = new Dictionary<int, double>();
            foreach (var (k, p) in lead.OrderBy(x => x.Key))
            {
                next[k + 1] = next.GetValueOrDefault(k + 1) + p * win;
                next[k - 1] = next.GetValueOrDefault(k - 1) + p * lose;
                next[k] = next.GetValueOrDefault(k) + p * (1 - win - lose);
            }
            lead = next;
        }
        var (w0, l0) = PassChance(Bonus(town, a, d.Rounds[0].Ability), Bonus(town, b, d.Rounds[0].Ability));
        double tie = w0 + l0 > 0 ? w0 / (w0 + l0) : 0.5;
        return Js.Sum(lead.Where(x => x.Key > 0), x => x.Value) + lead.GetValueOrDefault(0) * tie;
    }

    /// <summary>The chance one d20 + bonus beats another, and loses to it.</summary>
    private static (double Win, double Lose) PassChance(int a, int b)
    {
        int win = 0, lose = 0;
        for (int x = 1; x <= 20; x++)
            for (int y = 1; y <= 20; y++)
            {
                if (x + a > y + b) win++;
                else if (x + a < y + b) lose++;
            }
        return (win / 400.0, lose / 400.0);
    }

    /// <summary>Each rider's chance of winning an event from the draw as it stands (a walkover where a hero is away).</summary>
    public static Dictionary<int, double> Chances(Town town, TourneyEvent e)
    {
        var d = DefOf(town, e);
        // Each slot of the draw holds a spread of who might be standing in it.
        var slots = e.Draw.Select(id => new Dictionary<int, double> { [id] = 1 }).ToList();
        int done = 0;
        while (slots.Count > 1)
        {
            var next = new List<Dictionary<int, double>>();
            for (int i = 0; i + 1 < slots.Count; i += 2)
            {
                if (done < e.Heats.Count)
                {
                    next.Add(new Dictionary<int, double> { [e.Heats[done++].Winner!.Value] = 1 });
                    continue;
                }
                var into = new Dictionary<int, double>();
                foreach (var (x, px) in slots[i])
                    foreach (var (y, py) in slots[i + 1])
                    {
                        double p = Beats(town, d, Rider(town, x), Rider(town, y));
                        into[x] = into.GetValueOrDefault(x) + px * py * p;
                        into[y] = into.GetValueOrDefault(y) + px * py * (1 - p);
                    }
                next.Add(into);
            }
            slots = next;
        }
        var all = e.Draw.ToDictionary(id => id, _ => 0.0);
        foreach (var (id, p) in slots[0]) all[id] = p;
        return all;
    }

    private static double Beats(Town town, TourneyEventDef d, Entrant x, Entrant y)
    {
        bool gx = Absent(town, x), gy = Absent(town, y);
        if (gx || gy) return gx && !gy ? 0 : 1;
        return HeatChance(town, d, x, y);
    }

    /// <summary>What a winning stake on a rider pays back, times the stake: fair odds less the bookmakers' edge.</summary>
    public static double Odds(Town town, double chance) =>
        chance <= 0 ? 0 : Math.Max(1.05, Js.Round((1 - Rules(town).Betting.Edge) / chance * 10) / 10);

    /// <summary>The largest stake the treasury allows.</summary>
    public static double MaxStake(Town town) => Math.Floor(Math.Max(0, town.Treasury) * Rules(town).Betting.MaxShare);

    /// <summary>Why the mayor can't bet on this event now, or null.</summary>
    public static string? BetBlocked(Town town, TourneyEvent e)
    {
        if (town.Tourney.Stage is not (TourneyStage.Funded or TourneyStage.Running)) return "there is no tourney";
        if (e.Bet != null) return "the mayor has a stake on it already";
        if (e.Started) return "the heats have begun";
        if (MaxStake(town) < Rules(town).Betting.Min) return "the treasury can't spare it";
        return null;
    }

    /// <summary>Stake treasury gold on a rider in an event, at the odds of the draw as it stands.</summary>
    public static bool Bet(Town town, int eventIndex, int entrantId, double stake)
    {
        var st = town.Tourney;
        if (eventIndex < 0 || eventIndex >= st.Events.Count) return false;
        var e = st.Events[eventIndex];
        if (BetBlocked(town, e) != null || !e.Draw.Contains(entrantId)) return false;
        stake = Math.Floor(stake);
        if (stake < Rules(town).Betting.Min || stake > MaxStake(town)) return false;
        double chance = Chances(town, e)[entrantId];
        if (chance <= 0) return false;
        e.Bet = new TourneyBet { Entrant = entrantId, Stake = stake, Odds = Odds(town, chance) };
        town.Book(TreasuryAccount.Tourney, -stake);
        var en = Rider(town, entrantId);
        town.Log(LedgerCategory.Town, Importance.Routine, $"The mayor puts {Js.Str(stake)} gold on {en.Name} in {DefOf(town, e).Name}, at {Js.ToFixed(e.Bet.Odds, 1)} to one.");
        return true;
    }

    // The stand-in mayor -------------------------------------------------------------------------------

    /// <summary>The stand-in funds by what the treasury holds, and bets now and then, a little, on a strong hero of the town.</summary>
    private static void StandIn(Town town)
    {
        var r = Rules(town);
        var s = r.StandIn;
        int level = -1;
        for (int i = r.Levels.Count - 1; i >= 0; i--)
        {
            double need = i switch { 0 => Cost(town, r.Levels[0]) + s.Reserve, 1 => Math.Max(s.Grand, Cost(town, r.Levels[1]) * 3), _ => Math.Max(s.Legendary, Cost(town, r.Levels[i]) * 4) };
            if (town.Treasury >= need)
            {
                level = i;
                break;
            }
        }
        if (level < 0 || !Fund(town, level))
        {
            Skip(town);
            return;
        }
        if (town.Treasury < s.BetReserve) return;
        for (int i = 0; i < town.Tourney.Events.Count; i++)
        {
            var e = town.Tourney.Events[i];
            var chances = Chances(town, e);
            var best = e.Draw.Where(id => Rider(town, id).AgentId != null).OrderByDescending(id => chances[id]).ThenBy(id => id).FirstOrDefault();
            if (best == 0 || chances[best] < s.BetFloor || !town.Rng.Chance(s.BetChance)) continue;
            double stake = Math.Floor(Math.Min(s.BetMax, town.Treasury * s.BetShare));
            Bet(town, i, best, stake);
        }
    }

    /// <summary>Put a tourney on now, for tests and scripted shots: proposed, funded at a level and open today.</summary>
    public static bool Stage(Town town, int level)
    {
        var st = town.Tourney;
        st.Stage = TourneyStage.None;
        st.Year = Seasons.Year(town);
        Propose(town);
        st.OpenDay = town.Day;
        town.Treasury = Math.Max(town.Treasury, Cost(town, Rules(town).Levels[Math.Clamp(level, 0, Rules(town).Levels.Count - 1)]) + 500);
        if (!Fund(town, level)) return false;
        foreach (var (e, i) in st.Events.Select((e, i) => (e, i))) e.Day = town.Day + i;
        Open(town);
        return true;
    }

    private static string Days(int n) => n == 1 ? "a day" : $"{n} days";

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
