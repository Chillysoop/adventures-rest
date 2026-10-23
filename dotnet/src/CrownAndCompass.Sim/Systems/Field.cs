namespace CrownAndCompass.Sim.Systems;

/// <summary>How a host marched out meets the foe in the open field (army.json "field").</summary>
public sealed record FieldRules
{
    /// <summary>The fewest hours before the foe reaches the walls that a host can still march out to meet it.</summary>
    public int LeastHours { get; init; } = 12;

    /// <summary>Share of the time left before the foe arrives that the host marches before they meet (the rest is the foe's).</summary>
    public double MeetShare { get; init; } = 0.5;

    /// <summary>Rounds a field battle is fought in, and the share of each side's blows that kill.</summary>
    public int Rounds { get; init; } = 4;
    public double Bite { get; init; } = 0.5;

    /// <summary>Share of its strength at the start below which a side breaks and runs.</summary>
    public double Break { get; init; } = 0.45;

    /// <summary>Share of a regiment's lost strength that is soldiers killed (the rest come home hurt).</summary>
    public double Lethal { get; init; } = 0.5;

    /// <summary>Each round's dice: blows land between Low and High of their worth.</summary>
    public double Low { get; init; } = 0.75;
    public double High { get; init; } = 1.25;

    /// <summary>The order check: d20 + drill × Drilled + leadership × Led against the order's DC; a failure fights at Muddled, and a 1 routs a line whose host is outmatched (a winning host's line only stumbles).</summary>
    public double Drilled { get; init; } = 4;
    public double Led { get; init; } = 20;
    public int AllyBonus { get; init; } = 2;
    public double Muddled { get; init; } = 0.6;

    /// <summary>A unit whose role its kind beats hits harder; its bane harder still; a flier hits all but archers, casters and fliers harder.</summary>
    public double Beats { get; init; } = 1.25;
    public double Bane { get; init; } = 1.4;
    public double Unreached { get; init; } = 0.6;

    /// <summary>A hero's worth in the line for each level.</summary>
    public double HeroLevel { get; init; } = 6;

    /// <summary>What a host of each kind fights as (a unit role), for matchups.</summary>
    public IReadOnlyDictionary<string, string> FoeRoles { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<FieldOrderDef> Orders { get; init; } = [];
    public IReadOnlyList<FieldGoalDef> Goals { get; init; } = [];

    /// <summary>Field battles kept for the map and the window, newest last.</summary>
    public int Keep { get; init; } = 3;

    /// <summary>In-game hours the battle takes to play back on the map.</summary>
    public double Hours { get; init; } = 5;

    /// <summary>Mandate a won field brings, and a lost one costs.</summary>
    public double WonMandate { get; init; } = 8;
    public double LostMandate { get; init; } = -6;
}

/// <summary>An order the host fights under: its DC, what it does to blows given and taken, and who it favours.</summary>
public sealed record FieldOrderDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    public int Dc { get; init; } = 10;
    public double Give { get; init; } = 1;
    public double Take { get; init; } = 1;

    /// <summary>Roles that hit harder under this order, by how much, and only in the first round when First.</summary>
    public IReadOnlyList<string> Favours { get; init; } = [];
    public double Favour { get; init; } = 1;
    public bool First { get; init; }
    public string Blurb { get; init; } = "";
}

/// <summary>A goal a hero can take in a field battle: its DC (Leader fights the foe's leader instead), and what success does.</summary>
public sealed record FieldGoalDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    public int Dc { get; init; } = 13;

    /// <summary>On success: the line hits harder, takes less, and the foe breaks this much sooner.</summary>
    public double Give { get; init; } = 1;
    public double Take { get; init; } = 1;
    public double Rattle { get; init; }
    public string Blurb { get; init; } = "";
}

/// <summary>A hero sent out with the host and the goal they take on the field.</summary>
public sealed class HeroGoal
{
    public int HeroId { get; init; }
    public string Goal { get; set; } = "";
}

/// <summary>A host marching out to meet a foe on the road.</summary>
public sealed class Sortie
{
    /// <summary>"warband" or "menace:id".</summary>
    public string Foe { get; init; } = "";
    public string Order { get; set; } = "advance";
    public int LeftTick { get; init; }
    public int MeetTick { get; set; }

    /// <summary>Set once the battle is fought: when the host is home again.</summary>
    public int? ReturnTick { get; set; }
    public List<int> Regiments { get; } = [];
    public List<int> Allies { get; } = [];
    public List<HeroGoal> Heroes { get; } = [];
}

/// <summary>One side's unit in a field battle as it was fought, for the map and the report.</summary>
public sealed class FieldUnit
{
    /// <summary>"regiment", "ally", "heroes" or "foe".</summary>
    public string Kind { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>The unit id (regiment) or people id (ally) it was drawn from, for its colours.</summary>
    public string Look { get; init; } = "";
    public string Role { get; init; } = "foot";
    public int Men { get; init; }
    public double Start { get; init; }

    /// <summary>Strength left after each round.</summary>
    public List<double> Left { get; } = [];

    /// <summary>The round it ran (0 if it never did) and whether it failed its order each round.</summary>
    public int Routed { get; set; }
    public List<bool> Muddled { get; } = [];
    public bool Turned { get; set; }
}

public enum FieldEnd
{
    /// <summary>The foe broke and ran.</summary>
    Won,

    /// <summary>The foe broke, and its leader fell to the heroes.</summary>
    Slain,

    /// <summary>The host broke and ran home.</summary>
    Lost,

    /// <summary>Neither broke: both drew off, bloodied.</summary>
    Drawn,
}

/// <summary>A field battle as it was fought, kept for the map to play back and the Army window to report.</summary>
public sealed class FieldBattle
{
    public int Id { get; init; }
    public int Tick { get; init; }
    public string Foe { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Name { get; init; } = "";
    public string Leader { get; init; } = "";
    public int Approach { get; init; }
    public string Order { get; init; } = "";
    public List<FieldUnit> Ours { get; } = [];
    public FieldUnit Theirs { get; init; } = new();
    public List<string> Lines { get; } = [];
    public List<int> Fallen { get; } = [];
    public FieldEnd End { get; set; }

    /// <summary>The goals heroes took and whether each came off.</summary>
    public List<(int HeroId, string Goal, bool Done)> Goals { get; } = [];
}

/// <summary>
/// Marching out. When a foe is on the road, the mayor picks who goes (regiments, allied hosts camped at the gate,
/// heroes with goals) and who stays on the walls, gives the host an order, and sends it out to meet the foe halfway.
/// The battle is fought in rounds without the player's hand: each unit's order is a check (its drill and commander
/// against the order's DC; a failure fights muddled, a 1 runs), blows land by dice, matchups and the order, and the
/// heroes try their goals (the leader, the banner, the flank, the shield wall). A side that loses too much breaks.
/// Won, the foe turns back weakened (or ends, its leader slain); lost, the survivors come home and the foe marches on.
/// </summary>
public static class Field
{
    public static FieldRules Rules(Town town) => town.Content.Army.Field;

    public static FieldOrderDef Order(Town town, string id) => Rules(town).Orders.FirstOrDefault(o => o.Id == id) ?? Rules(town).Orders[0];

    public static FieldGoalDef? Goal(Town town, string id) => Rules(town).Goals.FirstOrDefault(g => g.Id == id);

    /// <summary>A foe on the road: who, how strong (as the town knows it), when they reach the walls.</summary>
    public sealed record Foe(string Key, string Name, string Kind, string Leader, double Strength, int ArrivalTick, int Approach);

    public static List<Foe> Foes(Town town)
    {
        var list = new List<Foe>();
        var en = town.Enemy;
        if (en.Stage == ThreatStage.Marching && en.ArrivalTick is int at)
        {
            var e = town.Content.Enemy;
            list.Add(new Foe("warband", e.Name, "warband", e.Leader, en.Strength, at, en.Approach));
        }
        foreach (var m in town.Saga.Menaces.Where(m => m.Alive && m.ArrivalTick != null).OrderBy(m => m.Id))
            list.Add(new Foe($"menace:{m.Id}", m.Name, m.Kind, m.Leader, m.Power, m.ArrivalTick!.Value, m.Approach));
        return list;
    }

    private static Menace? MenaceOf(Town town, string key) =>
        key.StartsWith("menace:", StringComparison.Ordinal) && int.TryParse(key[7..], out int id) ? town.Saga.Menaces.FirstOrDefault(m => m.Id == id) : null;

    public static Foe? FoeOf(Town town, string key) =>
        Conquest.Target(town, key) is { } s ? Conquest.FoeOf(town, s, town.Army.Sortie?.Foe == key ? town.Army.Sortie.MeetTick : town.Tick + MarchHours(town, s)) : Foes(town).FirstOrDefault(f => f.Key == key);

    /// <summary>Hours the host takes to reach a settlement.</summary>
    public static int MarchHours(Town town, Settlement s) => AllyCalls.MarchDays(town, s) * SimTime.HoursPerDay;

    /// <summary>Whether the regiment is out with the host, or holding a conquered settlement.</summary>
    public static bool Away(Town town, Regiment r) => town.Army.Sortie?.Regiments.Contains(r.Id) == true || Conquest.Garrisoning(town, r) || r.WildsId != null;

    /// <summary>Whether a townsperson is out with the host.</summary>
    public static bool Afield(Town town, Agent a) => a.RegimentId is int id && (town.Army.Sortie?.Regiments.Contains(id) == true || town.Army.Held.Any(o => o.GarrisonId == id));

    /// <summary>The worth of a regiment on the open field.</summary>
    public static double Worth(Town town, Regiment r) => Army.Strength(town, r) * Army.Unit(town, r).Field;

    /// <summary>The heroes free to take a goal: home, not on a quest, not in command (commanders go with their regiment).</summary>
    public static IEnumerable<Agent> FreeHeroes(Town town) =>
        town.Heroes.Where(h => !h.IsAway && !h.Dead && h.Hero!.Hp > 0 && h.Hero.Duty is HeroDuty.None or HeroDuty.Walls or HeroDuty.Streets).OrderByDescending(h => h.Hero!.Level).ThenBy(h => h.Id);

    /// <summary>The host as it would march now: what the player picked (regiments marching, allies, heroes).</summary>
    public static double HostWorth(Town town, IEnumerable<Regiment> regiments, IEnumerable<AllyCall> allies, IEnumerable<Agent> heroes) =>
        regiments.Sum(r => Worth(town, r)) + allies.Sum(c => c.Strength) + heroes.Sum(h => h.Hero!.Level * Rules(town).HeroLevel);

    /// <summary>The regiments picked to march.</summary>
    public static List<Regiment> Marching(Town town) => town.Army.Regiments.Where(r => r.Marches && r.Soldiers.Count > 0 && !Conquest.Garrisoning(town, r)).OrderBy(r => r.Id).ToList();

    public static List<AllyCall> MarchingAllies(Town town) => town.Army.Calls.Where(c => c.State == CallState.Arrived && c.Marches).OrderBy(c => c.Id).ToList();

    public static List<Agent> GoalHeroes(Town town) =>
        town.Army.Goals.Where(g => g.Goal != "").Select(g => town.Agent(g.HeroId)).Where(h => !h.Dead && !h.IsAway && h.Hero!.Hp > 0).ToList();

    public static string? MarchBlocked(Town town, string foeKey)
    {
        if (town.Army.Sortie != null) return "the host is out";
        if (Conquest.Target(town, foeKey) is { } target)
        {
            if (Conquest.AttackBlocked(town, target) is { } no) return no;
        }
        else if (FoeOf(town, foeKey) is not { } f) return "no foe on the road";
        else if (f.ArrivalTick - town.Tick < Rules(town).LeastHours) return "too close to the walls";
        if (Marching(town).Count == 0 && MarchingAllies(town).Count == 0) return "pick who marches";
        return null;
    }

    /// <summary>Send the host out to meet the foe.</summary>
    public static Sortie? MarchOut(Town town, string foeKey, string order)
    {
        if (MarchBlocked(town, foeKey) != null) return null;
        var f = FoeOf(town, foeKey)!;
        var r = Rules(town);
        var target = Conquest.Target(town, foeKey);
        int meet = target != null ? town.Tick + MarchHours(town, target) : town.Tick + Math.Max(1, (int)Math.Round((f.ArrivalTick - town.Tick) * r.MeetShare));
        var s = new Sortie { Foe = foeKey, Order = Order(town, order).Id, LeftTick = town.Tick, MeetTick = meet };
        s.Regiments.AddRange(Marching(town).Select(x => x.Id));
        s.Allies.AddRange(MarchingAllies(town).Select(c => c.Id));
        foreach (var h in GoalHeroes(town))
        {
            s.Heroes.Add(new HeroGoal { HeroId = h.Id, Goal = town.Army.Goals.First(g => g.HeroId == h.Id).Goal });
            h.Hero!.Duty = HeroDuty.Field;
        }
        town.Army.Sortie = s;
        int hours = s.MeetTick - town.Tick;
        int men = s.Regiments.Sum(id => Army.Find(town, id)!.Soldiers.Count);
        if (target != null) Conquest.Declare(town, target);
        town.Log(LedgerCategory.War, Importance.Notable,
            $"The host marches out of {town.Name} {(target != null ? $"on {target.Name}" : $"to meet {f.Name} on the road")}: {men} soldiers{(s.Allies.Count > 0 ? $", {s.Allies.Count} allied {(s.Allies.Count == 1 ? "host" : "hosts")}" : "")}{(s.Heroes.Count > 0 ? $" and {s.Heroes.Count} {(s.Heroes.Count == 1 ? "hero" : "heroes")}" : "")}. They will meet in about {hours} hours.");
        return s;
    }

    public static bool SetOrder(Town town, string order)
    {
        if (town.Army.Sortie is not { } s || Rules(town).Orders.All(o => o.Id != order)) return false;
        s.Order = order;
        return true;
    }

    /// <summary>Call the host home before it meets the foe.</summary>
    public static bool Recall(Town town)
    {
        if (town.Army.Sortie is not { } s || s.ReturnTick != null) return false;
        Home(town, s);
        town.Log(LedgerCategory.War, Importance.Notable, "The host is called back before it meets the foe, and marches home to the walls.");
        return true;
    }

    private static void Home(Town town, Sortie s)
    {
        foreach (var hg in s.Heroes)
        {
            var h = town.Agent(hg.HeroId);
            if (h.Hero is { Duty: HeroDuty.Field }) h.Hero.Duty = HeroDuty.None;
        }
        town.Army.Sortie = null;
    }

    public static bool SetMarches(Town town, int regimentId, bool marches)
    {
        if (Army.Find(town, regimentId) is not { } r || town.Army.Sortie != null) return false;
        r.Marches = marches;
        return true;
    }

    public static bool SetAllyMarches(Town town, int callId, bool marches)
    {
        if (town.Army.Calls.FirstOrDefault(c => c.Id == callId) is not { State: CallState.Arrived } c || town.Army.Sortie != null) return false;
        c.Marches = marches;
        return true;
    }

    /// <summary>Give a hero a goal on the field ("" to keep them home).</summary>
    public static bool SetGoal(Town town, int heroId, string goal)
    {
        if (town.Army.Sortie != null || heroId < 1 || heroId > town.Agents.Count) return false;
        var h = town.Agent(heroId);
        if (h.Hero == null || (goal != "" && Goal(town, goal) == null)) return false;
        town.Army.Goals.RemoveAll(g => g.HeroId == heroId);
        if (goal != "") town.Army.Goals.Add(new HeroGoal { HeroId = heroId, Goal = goal });
        return true;
    }

    /// <summary>The host meets the foe when it reaches the place.</summary>
    public static void Hourly(Town town)
    {
        if (town.Army.Sortie is not { } s || town.Tick < s.MeetTick) return;
        // Fought: the host marches the road home.
        if (s.ReturnTick is int back)
        {
            if (town.Tick < back) return;
            Home(town, s);
            town.Log(LedgerCategory.War, Importance.Notable, $"The host is home in {town.Name}.");
            return;
        }
        var f = FoeOf(town, s.Foe);
        if (f == null)
        {
            // The foe turned back or is gone: nothing to meet.
            Home(town, s);
            town.Log(LedgerCategory.War, Importance.Notable, "The host finds the road empty: the foe is gone. It marches home.");
            return;
        }
        Fight(town, s, f);
        s.ReturnTick = town.Tick + Math.Max(1, s.MeetTick - s.LeftTick);
    }

    private sealed class Line
    {
        public required FieldUnit Unit;
        public Regiment? Regiment;
        public AllyCall? Ally;
        public double Hp;
        public int Bonus;
        public double Lead;
        public UnitDef? Def;
    }

    private static void Fight(Town town, Sortie s, Foe f)
    {
        var r = Rules(town);
        var rng = town.Rng;
        var order = Order(town, s.Order);
        string foeRole = r.FoeRoles.GetValueOrDefault(f.Kind, "foot");
        var battle = new FieldBattle { Id = town.Army.NextField++, Tick = town.Tick, Foe = f.Key, Kind = f.Kind, Name = f.Name, Leader = f.Leader, Approach = f.Approach, Order = order.Id, Theirs = new FieldUnit { Kind = "foe", Name = f.Name, Look = f.Kind, Role = foeRole, Men = (int)Math.Round(f.Strength), Start = f.Strength } };
        var lines = new List<Line>();
        foreach (int id in s.Regiments)
        {
            if (Army.Find(town, id) is not { } reg || reg.Soldiers.Count == 0) continue;
            var u = Army.Unit(town, reg);
            double w = Worth(town, reg);
            var fu = new FieldUnit { Kind = "regiment", Name = reg.Name, Look = u.Id, Role = u.Role, Men = reg.Soldiers.Count, Start = w };
            lines.Add(new Line { Unit = fu, Regiment = reg, Hp = w, Def = u, Lead = Army.Command(town, reg), Bonus = (int)Math.Round(reg.Drill * r.Drilled) });
        }
        double foe = f.Strength;
        var log = battle.Lines;
        foreach (int id in s.Allies)
        {
            if (town.Army.Calls.FirstOrDefault(c => c.Id == id) is not { State: CallState.Arrived } c) continue;
            var st = AllyCalls.Of(town, c);
            var fu = new FieldUnit { Kind = "ally", Name = Saga.Cap(st.People), Look = st.Kin.Id, Role = "foot", Men = (int)Math.Round(c.Strength / 3), Start = c.Strength };
            if (c.Turncoat)
            {
                // A host in the foe's pocket goes over as the lines close.
                fu.Turned = true;
                fu.Left.Add(0);
                foe += c.Strength;
                c.State = CallState.Turned;
                st.Allied = false;
                st.Standing = Math.Min(st.Standing, -60);
                log.Add($"As the lines close, {st.People} turn their spears on the town's host and go over to {f.Name}!");
                battle.Ours.Add(fu);
                continue;
            }
            lines.Add(new Line { Unit = fu, Ally = c, Hp = c.Strength, Bonus = r.AllyBonus });
        }
        var heroes = s.Heroes.Select(g => town.Agent(g.HeroId)).Where(h => !h.Dead && h.Hero!.Hp > 0).ToList();
        if (heroes.Count > 0)
        {
            double hw = heroes.Sum(h => h.Hero!.Level * r.HeroLevel);
            var fu = new FieldUnit { Kind = "heroes", Name = Expedition.ListNames(heroes.Select(h => h.GivenName).ToList()), Look = "heroes", Role = "foot", Men = heroes.Count, Start = hw };
            lines.Add(new Line { Unit = fu, Hp = hw, Bonus = 99 });
        }
        foreach (var l in lines) battle.Ours.Add(l.Unit);
        double ours0 = lines.Sum(l => l.Hp);
        double foe0 = foe;
        battle.Theirs.Left.Clear();
        log.Insert(0, $"The host of {town.Name}, some {Js.Str(Js.Round(ours0))} strong, meets {f.Name}, some {Js.Str(Js.Round(foe))} strong, {(f.Kind == "settlement" ? "before its gates" : "on the road")}. The order is to {order.Name.ToLowerInvariant()}.");

        double give = 1, take = 1, rattle = 0;
        bool slain = false;
        bool foeBroke = false, weBroke = false;
        for (int round = 1; round <= r.Rounds && !foeBroke && !weBroke; round++)
        {
            // The heroes go for their goals as the lines meet.
            if (round == 2)
                foreach (var hg in s.Heroes)
                {
                    var h = town.Agent(hg.HeroId);
                    if (h.Dead || h.Hero!.Hp <= 0) continue;
                    if (hg.Goal == "leader")
                    {
                        if (battle.Goals.Any(g => g.Goal == "leader")) continue;
                        var party = s.Heroes.Where(x => x.Goal == "leader").Select(x => town.Agent(x.HeroId)).Where(x => !x.Dead && x.Hero!.Hp > 0).ToList();
                        bool done = Duel(town, f, party, battle, log);
                        foreach (var p in party) battle.Goals.Add((p.Id, "leader", done));
                        if (done)
                        {
                            slain = true;
                            rattle += Goal(town, "leader")?.Rattle ?? 0.3;
                            foe *= 0.7;
                        }
                        continue;
                    }
                    var g = Goal(town, hg.Goal);
                    if (g == null) continue;
                    int roll = rng.Range(1, 21);
                    bool ok = roll + h.Hero.Level + Rng.Modifier(Math.Max(h.Abilities.Str, h.Abilities.Dex)) >= g.Dc;
                    battle.Goals.Add((h.Id, g.Id, ok));
                    if (ok)
                    {
                        give *= g.Give;
                        take *= g.Take;
                        rattle += g.Rattle;
                        Notoriety.Deed(town, h, "slay", 4);
                        log.Add($"{h.GivenName} {g.Blurb}");
                    }
                    else log.Add($"{h.GivenName} tries to {g.Name.ToLowerInvariant()}, and is thrown back.");
                }

            double dealt = 0, worth = 0;
            // A line only loses heart on a natural 1 when the day is going against the host; a winning host just stumbles.
            bool outmatched = lines.Where(l => l.Unit.Routed == 0).Sum(l => l.Hp) < foe;
            foreach (var l in lines)
            {
                if (l.Unit.Routed > 0 || l.Hp <= 0)
                {
                    l.Unit.Muddled.Add(false);
                    continue;
                }
                int d20 = rng.Range(1, 21);
                bool muddled = l.Bonus < 99 && (d20 == 1 || d20 + l.Bonus + (int)Math.Round(l.Lead * r.Led) < order.Dc);
                l.Unit.Muddled.Add(muddled);
                if (l.Bonus < 99 && d20 == 1 && outmatched)
                {
                    l.Unit.Routed = round;
                    l.Hp *= 0.9;
                    log.Add($"{Saga.Cap(l.Unit.Name)} lose heart and run.");
                    continue;
                }
                double hit = l.Hp * (r.Low + rng.Next() * (r.High - r.Low)) * order.Give * give;
                if (muddled) hit *= r.Muddled;
                if (order.Favours.Contains(l.Unit.Role) && (!order.First || round == 1)) hit *= order.Favour;
                if (l.Def != null)
                {
                    if (l.Def.Beats.Contains(foeRole)) hit *= r.Beats;
                    if (l.Def.Bane.Contains(f.Kind)) hit *= r.Bane;
                }
                if (foeRole == "flier" && l.Unit.Role is not ("ranged" or "flier" or "arcane")) hit *= r.Unreached;
                dealt += hit;
                worth += l.Hp;
            }
            double back = foe * (r.Low + rng.Next() * (r.High - r.Low)) * order.Take * take;
            foe = Math.Max(0, foe - dealt * r.Bite / r.Rounds * 2);
            // The foe's blows fall on the line by its worth.
            foreach (var l in lines)
            {
                if (l.Unit.Routed > 0 && l.Unit.Routed < round || worth <= 0) continue;
                if (l.Unit.Routed == 0 && l.Hp > 0) l.Hp = Math.Max(0, l.Hp - back * r.Bite / r.Rounds * 2 * (l.Hp / worth));
            }
            foreach (var l in lines) l.Unit.Left.Add(Js.Round(l.Hp));
            battle.Theirs.Left.Add(Js.Round(foe));
            double ours = lines.Where(l => l.Unit.Routed == 0).Sum(l => l.Hp);
            foeBroke = foe < foe0 * (r.Break + rattle) || (slain && round >= 2);
            weBroke = ours < ours0 * r.Break;
            log.Add($"Round {round}: the host deals {Js.Str(Js.Round(dealt))} and takes {Js.Str(Js.Round(back))}. {Js.Str(Js.Round(ours))} of ours stand against {Js.Str(Js.Round(foe))}.");
        }

        // The cost: soldiers dead, allied hosts thinned, heroes hurt.
        foreach (var l in lines)
        {
            double lost = Math.Max(0, l.Unit.Start - l.Hp);
            if (l.Regiment is { } reg)
            {
                var u = Army.Unit(town, reg);
                int n = (int)Math.Min(reg.Soldiers.Count, Math.Floor(reg.Soldiers.Count * (lost / Math.Max(1, l.Unit.Start)) * r.Lethal * (1 - u.Mend) + rng.Next()));
                foreach (var a in Army.Fall(town, reg, n)) battle.Fallen.Add(a.Id);
            }
            else if (l.Ally is { } c) c.Strength = Math.Max(0, Js.Round(l.Hp));
        }
        if (battle.Fallen.Count > 0) log.Add($"{Expedition.ListNames(battle.Fallen.Select(id => town.Agent(id).Name).ToList())} {(battle.Fallen.Count == 1 ? "falls" : "fall")} on the field.");

        battle.End = foeBroke ? (slain ? FieldEnd.Slain : FieldEnd.Won) : weBroke ? FieldEnd.Lost : FieldEnd.Drawn;
        Outcome(town, battle, f, foe, foe0, s);
        town.Army.Fields.Add(battle);
        while (town.Army.Fields.Count > r.Keep) town.Army.Fields.RemoveAt(0);
    }

    /// <summary>The heroes go for the foe's leader, as champions do at the walls. Returns whether the leader fell.</summary>
    private static bool Duel(Town town, Foe f, List<Agent> party, FieldBattle battle, List<string> log)
    {
        if (party.Count == 0) return false;
        EncounterDef? boss = f.Key == "warband" ? town.Content.Enemy.WarlordBattle :
            MenaceOf(town, f.Key) is { } m && Quests.Site(town, m.SeatId) is { } seat ? seat.Def.Boss : null;
        if (boss == null) return false;
        log.Add($"{Expedition.ListNames(party.Select(a => a.GivenName).ToList())} cut through the press toward {f.Leader}.");
        var site = new SiteDef { Id = "field", Name = "the field", Tier = 0, Distance = 0, Encounters = [], Boss = boss, Loot = [0, 0] };
        var result = Expedition.Run(town.Rng, town.Content, site, party);
        log.AddRange(result.Lines.Skip(1).Where(l =>
            !l.StartsWith("At last", StringComparison.Ordinal) &&
            !l.Contains("is cleared. The hoard", StringComparison.Ordinal) &&
            !l.StartsWith("No one comes back", StringComparison.Ordinal)));
        foreach (int id in result.Dead.Order())
        {
            var h = town.Agent(id);
            h.Dead = true;
            Population.Depart(town, h);
            battle.Fallen.Add(id);
        }
        foreach (var c in party)
        {
            if (result.Dead.Contains(c.Id)) continue;
            Notoriety.Deed(town, c, "slay", 10);
            c.Hero!.Kills += result.Kills.Get(c.Id, 0);
        }
        return result.Outcome == ExpeditionOutcome.Cleared;
    }

    private static void Outcome(Town town, FieldBattle b, Foe f, double foe, double foe0, Sortie s)
    {
        var r = Rules(town);
        var log = b.Lines;
        bool won = b.End is FieldEnd.Won or FieldEnd.Slain;
        if (f.Key == "warband")
        {
            var en = town.Enemy;
            var e = town.Content.Enemy;
            if (b.End == FieldEnd.Slain)
            {
                log.Add($"{e.Leader} is dead on the field. The warband breaks and scatters into the hills.");
                en.Stage = ThreatStage.Defeated;
                en.Strength = 0;
                en.ArrivalTick = null;
                town.Mandate = Math.Min(100, town.Mandate + 25);
                Saga.WarlordSlain(town);
            }
            else if (won)
            {
                log.Add($"The warband breaks and runs. {e.Leader} drags what is left of it back to the hills.");
                en.Stage = ThreatStage.Raids;
                en.ArrivalTick = null;
                en.Strength = Math.Max(e.Stages.Probing, foe);
                en.NextRaidTick = town.Tick + 6 * SimTime.HoursPerDay;
            }
            else en.Strength = foe;
        }
        else if (Conquest.Target(town, f.Key) is { } target) Conquest.Fought(town, target, b, won);
        else if (MenaceOf(town, f.Key) is { } m)
        {
            if (b.End == FieldEnd.Slain)
            {
                log.Add($"{m.Leader} falls on the field!");
                m.ArrivalTick = null;
                Menaces.End(town, m);
            }
            else if (won)
            {
                log.Add($"{Saga.Cap(m.Name)} breaks and turns for home.");
                m.ArrivalTick = null;
                m.Power = foe;
                var mr = town.Content.Saga.Menaces;
                m.NextAssaultTick = town.Tick + town.Rng.Range(mr.AssaultEveryDays[0], mr.AssaultEveryDays[1] + 1) * SimTime.HoursPerDay;
            }
            else m.Power = foe;
        }
        string head = b.End switch
        {
            FieldEnd.Slain => $"Victory in the field! {Saga.Cap(f.Leader)} is slain and {f.Name} broken.",
            FieldEnd.Won => $"Victory in the field: {f.Name} breaks and runs.",
            FieldEnd.Lost when f.Kind == "settlement" => $"Defeat at the gates of {f.Name}: the host breaks and runs for home.",
            _ when f.Kind == "settlement" => $"A bloody draw at the gates of {f.Name}: the host draws off.",
            FieldEnd.Lost => $"Defeat in the field: the host breaks, and {f.Name} marches on toward the walls.",
            _ => $"A bloody draw in the field: both sides draw off, and {f.Name} marches on.",
        };
        log.Insert(0, head);
        town.Mandate = Math.Clamp(town.Mandate + (won ? r.WonMandate : b.End == FieldEnd.Lost ? r.LostMandate : 0), 0, 100);
        if (won)
            foreach (var c in town.Army.Calls.Where(c => s.Allies.Contains(c.Id) && c.State == CallState.Arrived)) AllyCalls.Of(town, c).Standing += town.Content.Army.Allies.Fought;
        town.Log(LedgerCategory.War, Importance.Major, head + $" ({Js.Str(Js.Round(foe0 - foe))} of the foe fell, and {b.Fallen.Count} of ours.)", b.Fallen.ToList());
    }
}
