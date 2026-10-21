using System.Text.Json.Serialization;

namespace CrownAndCompass.Sim.Systems;

/// <summary>A kind of soldier the town can raise (content/game/army.json).</summary>
public sealed record UnitDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";

    /// <summary>One of them, in the Ledger ("swordsman").</summary>
    public string One { get; init; } = "";

    /// <summary>foot, pike, ranged, horse, flier, arcane, beast or construct: what it is good against (<see cref="Beats"/>) is by role.</summary>
    public string Role { get; init; } = "foot";

    /// <summary>The training halls that can raise it (buildings with <c>trains</c>); none for the levy.</summary>
    public IReadOnlyList<string> Halls { get; init; } = [];

    /// <summary>The charter the town needs first.</summary>
    public string Tier { get; init; } = "";

    /// <summary>What else the town needs: institution:id, ally:people, building:id.</summary>
    public IReadOnlyList<string> Needs { get; init; } = [];

    /// <summary>Gold, and goods off the market, to arm each recruit.</summary>
    public double Cost { get; init; }

    [JsonConverter(typeof(GoodsListConverter))]
    public IReadOnlyList<KeyValuePair<Good, double>> Goods { get; init; } = [];

    /// <summary>Gold a day each soldier is paid.</summary>
    public double Wage { get; init; }

    /// <summary>Each fully drilled soldier's strength, and how much of it tells on the walls, in the field, on patrol and as a guard.</summary>
    public double Strength { get; init; } = 1;
    public double Walls { get; init; } = 1;
    public double Field { get; init; } = 1;
    public double Patrol { get; init; } = 1;
    public double Guard { get; init; } = 1;

    /// <summary>How fast it marches, against foot at 1.</summary>
    public double Speed { get; init; } = 1;

    /// <summary>Days of drill to bring a raw recruit to full strength.</summary>
    public double Drill { get; init; } = 20;

    /// <summary>Roles it is strong against in the field.</summary>
    public IReadOnlyList<string> Beats { get; init; } = [];

    /// <summary>Kinds of foe (monster types) it is sworn against.</summary>
    public IReadOnlyList<string> Bane { get; init; } = [];

    /// <summary>Share of the regiment's losses its healers save.</summary>
    public double Mend { get; init; }

    /// <summary>Its colours, for the client (hex).</summary>
    public string Colour { get; init; } = "8c7a5a";
    public string Blurb { get; init; } = "";

    public bool IsLevy => Halls.Count == 0;
}

/// <summary>What a regiment at a post does: its share at a siege and at a night raid, and how fast it drills there.</summary>
public sealed record PostDef
{
    public double Siege { get; init; } = 1;
    public double Raid { get; init; }
    public double Drill { get; init; } = 1;

    /// <summary>Patrol: what each soldier adds to the watch's eye, up to the cap.</summary>
    public double Watch { get; init; }
    public double WatchCap { get; init; }

    /// <summary>Guard: what each soldier adds to the odds a knife in the night is stopped, up to the cap.</summary>
    public double Bodyguard { get; init; }
    public double BodyguardCap { get; init; }
}

public sealed record PostRules
{
    public PostDef Walls { get; init; } = new();
    public PostDef Reserve { get; init; } = new();
    public PostDef Patrol { get; init; } = new();
    public PostDef Guard { get; init; } = new();
}

/// <summary>How well a hero leads soldiers.</summary>
public sealed record CommandRules
{
    public double PerLevel { get; init; } = 0.02;
    public double PerCha { get; init; } = 0.04;

    /// <summary>Leadership a class brings, by class id.</summary>
    public IReadOnlyDictionary<string, double> Classes { get; init; } = new Dictionary<string, double>();

    /// <summary>Soldiers a hero can lead: a base, and more a level.</summary>
    public int Soldiers { get; init; } = 12;
    public int SoldiersPerLevel { get; init; } = 4;

    /// <summary>How much faster a commanded regiment drills, times leadership.</summary>
    public double Drill { get; init; } = 0.5;
}

/// <summary>Calling up the townsfolk under the town's laws.</summary>
public sealed record LevyRules
{
    public string Unit { get; init; } = "levy";

    /// <summary>Gold to arm each one called up.</summary>
    public double Arms { get; init; } = 2;

    /// <summary>Their wage a day under a paid levy.</summary>
    public double Wage { get; init; } = 0.8;

    /// <summary>Mood a townsman needs to volunteer: for a paid levy, and an unpaid one.</summary>
    public double VolunteerMood { get; init; } = 55;
    public double UnpaidMood { get; init; } = 65;

    /// <summary>Most of the townsfolk who can be called: volunteering, or conscripted.</summary>
    public double VolunteerShare { get; init; } = 0.15;
    public double ConscriptShare { get; init; } = 0.3;

    /// <summary>Mood a conscript loses each hour of service, and an unpaid one more.</summary>
    public double ConscriptMood { get; init; } = -6;
    public double UnpaidDayMood { get; init; } = -8;

    /// <summary>Mandate the town loses when conscription becomes law, and for each conscript called up.</summary>
    public double ConscriptMandate { get; init; } = -6;
    public double RaiseMandate { get; init; } = -0.2;
}

/// <summary>The town's own army (content/game/army.json).</summary>
public sealed record ArmyRules
{
    public IReadOnlyList<int> Sizes { get; init; } = [10, 20, 40];
    public int MaxSize { get; init; } = 60;
    public int RecruitPerDay { get; init; } = 4;
    public double DrillFloor { get; init; } = 0.4;
    public int MinAge { get; init; } = 16;
    public int MaxAge { get; init; } = 55;

    /// <summary>Share of a regiment that walks off for each day it goes unpaid.</summary>
    public double Desert { get; init; } = 0.25;

    /// <summary>Mood soldiers of a standing regiment get from the colours.</summary>
    public double Pride { get; init; } = 3;

    /// <summary>Share of the defenders' losses at a siege that falls on the army's soldiers.</summary>
    public double Losses { get; init; } = 0.5;

    public PostRules Posts { get; init; } = new();
    public CommandRules Command { get; init; } = new();
    public LevyRules Levy { get; init; } = new();
    public AllyRules Allies { get; init; } = new();
    public FieldRules Field { get; init; } = new();
    public IReadOnlyList<UnitDef> Units { get; init; } = [];
}

/// <summary>Where a regiment stands.</summary>
public enum ArmyPost
{
    /// <summary>In its hall, drilling; it turns out for a siege a little late.</summary>
    Reserve,

    /// <summary>On the walls and at the gates.</summary>
    Walls,

    /// <summary>Walking the streets and the farms as the town guard.</summary>
    Patrol,

    /// <summary>The mayor's guard, at the town hall with the council.</summary>
    Guard,
}

/// <summary>Whether the levy takes only the willing, or anyone fit.</summary>
public enum ServiceLaw
{
    Volunteers,
    Conscription,
}

/// <summary>A body of the town's soldiers under one banner: townsfolk drilled at a hall, or the levy called from their work.</summary>
public sealed class Regiment
{
    public int Id { get; init; }
    public required string Unit { get; init; }
    public string Name { get; set; } = "";

    /// <summary>The townsfolk in it, by id.</summary>
    public List<int> Soldiers { get; } = [];

    /// <summary>How many it is raised to; recruits come in each day until it is full.</summary>
    public int Wanted { get; set; }

    /// <summary>The hall that trains and quarters it; null for the levy.</summary>
    public int? HallId { get; set; }

    public ArmyPost Post { get; set; }

    /// <summary>The hero leading it, if any.</summary>
    public int? CommanderId { get; set; }

    /// <summary>How well drilled it is, 0 raw to 1 full.</summary>
    public double Drill { get; set; }
    public int RaisedDay { get; init; }

    /// <summary>Recruits it could not find, or arm, today: why the regiment is short.</summary>
    public string Short { get; set; } = "";

    /// <summary>Soldiers it has lost in the town's fights.</summary>
    public int Fallen { get; set; }

    /// <summary>Picked to march out with the host (the rest stay on the walls).</summary>
    public bool Marches { get; set; }
}

/// <summary>The army's state: its regiments and the levy laws.</summary>
public sealed class ArmyState
{
    public List<Regiment> Regiments { get; } = [];
    public int NextId { get; set; } = 1;
    public ServiceLaw Service { get; set; } = ServiceLaw.Volunteers;
    public bool LevyPaid { get; set; } = true;

    /// <summary>How many of each unit the town has raised, for the regiments' numbers.</summary>
    public OrderedMap<string, int> Raised { get; } = new();

    /// <summary>Calls to arms sent to allies, and the hosts they bring.</summary>
    public List<AllyCall> Calls { get; } = [];
    public int NextCall { get; set; } = 1;

    /// <summary>The host out in the field, the goals heroes will take when it marches, and the field battles fought.</summary>
    public Sortie? Sortie { get; set; }
    public List<HeroGoal> Goals { get; } = [];
    public List<FieldBattle> Fields { get; } = [];
    public int NextField { get; set; } = 1;

    [NotSaved] internal List<Building>? Rounds;
    [NotSaved] internal int RoundsDay = -1;
}

/// <summary>
/// The town's own army. A standing regiment takes townsfolk out of their trades into a training hall, arms each recruit
/// from the treasury and the market, drills them up over the weeks and pays them a wage; the levy calls townsfolk up
/// from their work under the town's laws, willing or not, paid or not. Every regiment stands at a post: on the walls,
/// in reserve at its hall, on patrol as the town guard (raids at night, thieves and spies), or as the mayor's guard
/// (knives in the night). A hero in command lends their leadership to as many soldiers as they can lead. In a siege
/// the whole army at home fights, and its soldiers are the first to fall.
/// </summary>
public static class Army
{
    public static ArmyRules Rules(Town town) => town.Content.Army;

    public static UnitDef Unit(Town town, string id) =>
        Rules(town).Units.FirstOrDefault(u => u.Id == id) ?? throw new KeyNotFoundException($"Unknown unit {id}");

    public static UnitDef Unit(Town town, Regiment r) => Unit(town, r.Unit);

    public static Regiment? Find(Town town, int id) => town.Army.Regiments.FirstOrDefault(r => r.Id == id);

    public static Regiment? Levy(Town town) => town.Army.Regiments.FirstOrDefault(r => Unit(town, r).IsLevy);

    public static PostDef Post(Town town, ArmyPost p)
    {
        var r = Rules(town).Posts;
        return p switch { ArmyPost.Walls => r.Walls, ArmyPost.Patrol => r.Patrol, ArmyPost.Guard => r.Guard, _ => r.Reserve };
    }

    /// <summary>The regiment a townsman serves in, if any.</summary>
    public static Regiment? Of(Town town, Agent a) => a.RegimentId is int id ? Find(town, id) : null;

    /// <summary>Soldiers of every regiment.</summary>
    public static int Soldiers(Town town) => town.Army.Regiments.Sum(r => r.Soldiers.Count);

    // Strength ----------------------------------------------------------------------------------

    /// <summary>
    /// A hero's leadership: their level, their presence and their class. A regiment larger than they can lead gets the
    /// share of it they can.
    /// </summary>
    public static double Leadership(Town town, Agent hero)
    {
        var c = Rules(town).Command;
        var s = hero.Hero!;
        return s.Level * c.PerLevel + Math.Max(0, Rng.Modifier(hero.Abilities.Cha)) * c.PerCha + c.Classes.GetValueOrDefault(s.Cls.Id);
    }

    /// <summary>Soldiers a hero can lead.</summary>
    public static int CanLead(Town town, Agent hero) => Rules(town).Command.Soldiers + Rules(town).Command.SoldiersPerLevel * hero.Hero!.Level;

    public static Agent? Commander(Town town, Regiment r) => r.CommanderId is int id && id >= 1 && id <= town.Agents.Count ? town.Agent(id) : null;

    /// <summary>What the commander adds to the regiment, as a share of its strength.</summary>
    public static double Command(Town town, Regiment r)
    {
        if (Commander(town, r) is not { } h || h.IsAway || h.Dead || r.Soldiers.Count == 0) return 0;
        return Leadership(town, h) * Math.Min(1, CanLead(town, h) / (double)r.Soldiers.Count);
    }

    /// <summary>The regiment's strength where it stands: its soldiers' worth, drill and stoutness, and its commander.</summary>
    public static double Strength(Town town, Regiment r)
    {
        if (r.Soldiers.Count == 0) return 0;
        var u = Unit(town, r);
        double stout = 0;
        foreach (int id in r.Soldiers)
        {
            var a = town.Agent(id);
            stout += Rng.Modifier(a.Abilities.Str) + Rng.Modifier(a.Abilities.Con);
        }
        double floor = Rules(town).DrillFloor;
        double each = u.Strength * (floor + (1 - floor) * r.Drill);
        return r.Soldiers.Count * each * (1 + 0.03 * stout / r.Soldiers.Count) * (1 + Command(town, r));
    }

    /// <summary>The regiment's worth at the walls in a siege: its strength, its post's share, and how it fights from a wall.</summary>
    public static double AtWalls(Town town, Regiment r) => Strength(town, r) * Post(town, r.Post).Siege * Unit(town, r).Walls;

    /// <summary>Strength the army at home adds at the walls.</summary>
    public static double Defense(Town town)
    {
        double d = 0;
        foreach (var r in town.Army.Regiments) if (!Field.Away(town, r)) d += AtWalls(town, r);
        return d;
    }

    /// <summary>Strength the army adds against a raid at night outside the walls: patrols most, the rest as they turn out.</summary>
    public static double RaidDefense(Town town)
    {
        double d = 0;
        foreach (var r in town.Army.Regiments) if (!Field.Away(town, r)) d += Strength(town, r) * Post(town, r.Post).Raid * Unit(town, r).Patrol;
        return d;
    }

    /// <summary>What the patrols add to the watch's eye for spies, thieves, knives and fire-raisers.</summary>
    public static double StreetWatch(Town town)
    {
        var p = Rules(town).Posts.Patrol;
        double w = 0;
        foreach (var r in town.Army.Regiments.Where(r => r.Post == ArmyPost.Patrol && !Field.Away(town, r)))
            w += r.Soldiers.Count * p.Watch * Unit(town, r).Patrol * (Rules(town).DrillFloor + (1 - Rules(town).DrillFloor) * r.Drill);
        return Math.Min(p.WatchCap, w);
    }

    /// <summary>What the mayor's guard adds to the odds a knife in the night is stopped at a councillor's door.</summary>
    public static double Bodyguard(Town town)
    {
        var g = Rules(town).Posts.Guard;
        double w = 0;
        foreach (var r in town.Army.Regiments.Where(r => r.Post == ArmyPost.Guard && !Field.Away(town, r)))
            w += r.Soldiers.Count * g.Bodyguard * Unit(town, r).Guard;
        return Math.Min(g.BodyguardCap, w);
    }

    /// <summary>Wages the army draws a day.</summary>
    public static double Wages(Town town) => town.Army.Regiments.Sum(r => r.Soldiers.Count * Wage(town, r));

    public static double Wage(Town town, Regiment r)
    {
        var u = Unit(town, r);
        return u.IsLevy ? (town.Army.LevyPaid ? Rules(town).Levy.Wage : 0) : u.Wage;
    }

    // Raising ----------------------------------------------------------------------------------

    /// <summary>What the town still needs before it can raise this unit, or null.</summary>
    public static string? Locked(Town town, UnitDef u)
    {
        if (u.Tier != "" && !City.Reached(town, u.Tier)) return $"needs the town to be a {City.TierName(town, u.Tier).ToLowerInvariant()}";
        foreach (string need in u.Needs)
        {
            int colon = need.IndexOf(':');
            string kind = need[..colon], id = need[(colon + 1)..];
            switch (kind)
            {
                case "institution":
                    if (Institutions.Find(town, id) == null) return $"needs {Institutions.Rules(town).Def(id)?.Name ?? id}";
                    break;
                case "ally":
                    if (!town.Settlements.Any(s => s.Allied && !s.Razed && s.Kin.Id == id))
                        return $"needs an alliance with {town.Content.World.Peoples.FirstOrDefault(p => p.Id == id)?.People ?? id}";
                    break;
                case "building":
                    if (!town.Buildings.Any(b => b.Complete && b.Def.Id == id)) return $"needs a {town.Content.Building(id).Name}";
                    break;
            }
        }
        if (!u.IsLevy && Halls(town, u).Count == 0) return $"needs a {town.Content.Building(u.Halls[0]).Name}";
        return null;
    }

    /// <summary>The finished halls that can train the unit, by id.</summary>
    public static List<Building> Halls(Town town, UnitDef u) =>
        town.Buildings.Where(b => b.Complete && u.Halls.Contains(b.Def.Id)).OrderBy(b => b.Id).ToList();

    /// <summary>Soldiers a hall has room for still, counting the regiments it is filling.</summary>
    public static int Room(Town town, Building hall) =>
        hall.Def.Trains - town.Army.Regiments.Where(r => r.HallId == hall.Id).Sum(r => Math.Max(r.Wanted, r.Soldiers.Count));

    /// <summary>The hall with the most room for the unit.</summary>
    public static Building? HallFor(Town town, UnitDef u, int size) =>
        Halls(town, u).Where(h => Room(town, h) >= size).OrderByDescending(h => Room(town, h)).ThenBy(h => h.Id).FirstOrDefault();

    /// <summary>What arming each recruit costs today.</summary>
    public static double RecruitCost(Town town, UnitDef u)
    {
        if (u.IsLevy) return Rules(town).Levy.Arms;
        double c = u.Cost;
        foreach (var (g, n) in u.Goods) c += town.Market.Quote(g, n);
        return c;
    }

    /// <summary>Why the town can't raise a regiment of this unit and size now, or null.</summary>
    public static string? RaiseBlocked(Town town, string unitId, int size)
    {
        var u = Rules(town).Units.FirstOrDefault(x => x.Id == unitId);
        if (u == null) return "no such soldiers";
        if (u.IsLevy) return "call the levy instead";
        if (size < 1 || size > Rules(town).MaxSize) return $"between 1 and {Rules(town).MaxSize}";
        if (Locked(town, u) is string why) return why;
        if (HallFor(town, u, size) == null) return "no hall has room";
        if (town.Treasury < RecruitCost(town, u) * Math.Min(size, Rules(town).RecruitPerDay)) return "not enough gold";
        return null;
    }

    private static string Ordinal(int n) =>
        n + (n % 100 is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });

    /// <summary>Raise a regiment: it takes recruits each day until it is full, arming each as they come.</summary>
    public static Regiment? Raise(Town town, string unitId, int size, ArmyPost post = ArmyPost.Reserve)
    {
        if (RaiseBlocked(town, unitId, size) != null) return null;
        var u = Unit(town, unitId);
        var hall = HallFor(town, u, size)!;
        int n = town.Army.Raised.Get(u.Id, 0) + 1;
        town.Army.Raised[u.Id] = n;
        var r = new Regiment { Id = town.Army.NextId++, Unit = u.Id, Name = $"{Ordinal(n)} {u.Name}", Wanted = size, HallId = hall.Id, Post = post, RaisedDay = town.Day };
        town.Army.Regiments.Add(r);
        town.Log(LedgerCategory.War, Importance.Notable, $"The council raises the {r.Name} at the {hall.Name}: {size} {(size == 1 ? u.One : u.Name.ToLowerInvariant())}, {Js.Str(u.Wage)} gold a day each.");
        Recruit(town, r);
        return r;
    }

    /// <summary>Raise or cut a regiment's numbers; those cut go home to their trades.</summary>
    public static bool Resize(Town town, int regimentId, int size)
    {
        var r = Find(town, regimentId);
        if (r == null || size < 1 || size > Rules(town).MaxSize) return false;
        if (r.HallId is int hid && size > r.Wanted && Room(town, town.Building(hid)) < size - Math.Max(r.Wanted, r.Soldiers.Count)) return false;
        if (Unit(town, r).IsLevy && size > LevyRoom(town) + r.Soldiers.Count) return false;
        r.Wanted = size;
        while (r.Soldiers.Count > size) Release(town, r, r.Soldiers[^1]);
        return true;
    }

    /// <summary>Stand a regiment down: its soldiers go home to their trades.</summary>
    public static bool Disband(Town town, int regimentId)
    {
        var r = Find(town, regimentId);
        if (r == null) return false;
        foreach (int id in r.Soldiers.ToList()) Release(town, r, id);
        StandDownCommander(town, r);
        town.Army.Regiments.Remove(r);
        town.Log(LedgerCategory.War, Importance.Notable, Unit(town, r).IsLevy
            ? $"The levy is stood down. The townsfolk go home to their trades."
            : $"The {r.Name} is stood down and its colours cased.");
        return true;
    }

    public static bool SetPost(Town town, int regimentId, ArmyPost post)
    {
        var r = Find(town, regimentId);
        if (r == null || r.Post == post) return false;
        r.Post = post;
        town.Log(LedgerCategory.War, Importance.Routine, $"The {r.Name} {post switch
        {
            ArmyPost.Walls => "takes the walls and the gates",
            ArmyPost.Patrol => "walks the streets and the farms as the town guard",
            ArmyPost.Guard => "stands as the mayor's guard at the hall",
            _ => "goes back to its hall to drill",
        }}.");
        return true;
    }

    // Command ----------------------------------------------------------------------------------

    /// <summary>Why the hero can't lead the regiment now, or null.</summary>
    public static string? CommandBlocked(Town town, Regiment r, Agent a)
    {
        if (a.Hero == null || a.Dead || a.Departed) return "gone";
        if (r.CommanderId == a.Id) return "already in command";
        if (a.IsAway) return "out of town";
        if (a.Hero.JailedUntil != null) return "in the cells";
        if (town.Army.Regiments.Any(x => x.CommanderId == a.Id)) return "leads another regiment";
        if (a.Hero.Duty is HeroDuty.Walls or HeroDuty.Streets) return "on guard duty";
        if (town.Treasury < Garrison.Wage(town, a)) return "no gold for the wage";
        return null;
    }

    /// <summary>
    /// Give a regiment a hero to lead it, or none. A hero in command stays home on the guard's wage and takes no bounties.
    /// </summary>
    public static bool SetCommander(Town town, int regimentId, int? heroId)
    {
        var r = Find(town, regimentId);
        if (r == null) return false;
        if (heroId == null)
        {
            if (r.CommanderId == null) return false;
            StandDownCommander(town, r);
            return true;
        }
        if (heroId < 1 || heroId > town.Agents.Count) return false;
        var a = town.Agent(heroId.Value);
        if (CommandBlocked(town, r, a) != null) return false;
        StandDownCommander(town, r);
        r.CommanderId = a.Id;
        a.Hero!.Duty = HeroDuty.Command;
        town.Log(LedgerCategory.War, Importance.Notable, $"{a.Name} takes command of the {r.Name}, for {Js.Str(Garrison.Wage(town, a))} gold a day.", [a.Id]);
        return true;
    }

    private static void StandDownCommander(Town town, Regiment r)
    {
        if (Commander(town, r) is { } h && h.Hero!.Duty == HeroDuty.Command) h.Hero.Duty = HeroDuty.None;
        r.CommanderId = null;
    }

    // The levy ---------------------------------------------------------------------------------

    /// <summary>Whether a townsman can be called up at all: of age, in town, not a hero, not on the council, not running a business.</summary>
    public static bool Fit(Town town, Agent a)
    {
        var r = Rules(town);
        if (a.Hero != null || a.Departed || a.Dead || a.RegimentId != null || a.Age < r.MinAge || a.Age > r.MaxAge) return false;
        if (town.Seats.Any(s => s.HolderId == a.Id)) return false;
        return !(a.JobId is int j && town.Building(j).OwnerId == a.Id);
    }

    /// <summary>Whether a townsman would answer the call under the town's laws.</summary>
    public static bool Willing(Town town, Agent a)
    {
        if (town.Army.Service == ServiceLaw.Conscription) return true;
        var l = Rules(town).Levy;
        return a.LastDayMood >= (town.Army.LevyPaid ? l.VolunteerMood : l.UnpaidMood);
    }

    /// <summary>Most townsfolk the levy can hold under the laws.</summary>
    public static int LevyCap(Town town)
    {
        var l = Rules(town).Levy;
        int workers = town.Workers.Count;
        return (int)Math.Floor(workers * (town.Army.Service == ServiceLaw.Conscription ? l.ConscriptShare : l.VolunteerShare));
    }

    /// <summary>More townsfolk who would answer a call now.</summary>
    public static int LevyRoom(Town town)
    {
        int called = Levy(town)?.Soldiers.Count ?? 0;
        int willing = town.Workers.Count(a => Fit(town, a) && Willing(town, a));
        return Math.Max(0, Math.Min(LevyCap(town) - called, willing));
    }

    public static string? LevyBlocked(Town town, int size)
    {
        if (size < 1) return "nobody to call";
        int room = LevyRoom(town);
        if (room == 0) return town.Army.Service == ServiceLaw.Volunteers ? "no more volunteers" : "nobody fit to call";
        if (town.Treasury < Rules(town).Levy.Arms * Math.Min(size, room)) return "not enough gold";
        return null;
    }

    /// <summary>Call up the levy, or more of it: townsfolk leave their work for the walls.</summary>
    public static Regiment? RaiseLevy(Town town, int size, ArmyPost post = ArmyPost.Walls)
    {
        if (LevyBlocked(town, size) != null) return null;
        var levy = Levy(town);
        size = Math.Min(size, LevyRoom(town));
        if (levy == null)
        {
            levy = new Regiment { Id = town.Army.NextId++, Unit = Rules(town).Levy.Unit, Name = $"Levy of {town.Name}", Post = post, RaisedDay = town.Day };
            town.Army.Regiments.Add(levy);
        }
        levy.Wanted = levy.Soldiers.Count + size;
        int before = levy.Soldiers.Count;
        Recruit(town, levy, size);
        int came = levy.Soldiers.Count - before;
        bool conscripts = town.Army.Service == ServiceLaw.Conscription;
        if (conscripts) town.Mandate = Math.Max(0, town.Mandate + Rules(town).Levy.RaiseMandate * came);
        town.Log(LedgerCategory.War, Importance.Notable,
            $"The levy is called: {came} of the townsfolk {(conscripts ? "are marched from their work" : "put down their tools and come")} to take up spears{(town.Army.LevyPaid ? $", for {Js.Str(Rules(town).Levy.Wage)} gold a day" : ", unpaid")}.");
        return levy;
    }

    public static bool SetService(Town town, ServiceLaw law)
    {
        if (town.Army.Service == law) return false;
        town.Army.Service = law;
        if (law == ServiceLaw.Conscription)
        {
            town.Mandate = Math.Max(0, town.Mandate + Rules(town).Levy.ConscriptMandate);
            town.Log(LedgerCategory.Council, Importance.Notable, "Conscription is the law: any townsman fit to hold a spear can be called to the levy. There is grumbling in the taverns.");
        }
        else town.Log(LedgerCategory.Council, Importance.Notable, "The levy takes volunteers only, from now on.");
        return true;
    }

    public static bool SetLevyPay(Town town, bool paid)
    {
        if (town.Army.LevyPaid == paid) return false;
        town.Army.LevyPaid = paid;
        town.Log(LedgerCategory.Council, Importance.Notable, paid
            ? $"The levy will be paid: {Js.Str(Rules(town).Levy.Wage)} gold a day for each townsman called up."
            : "The levy will serve unpaid, for the town's sake.");
        return true;
    }

    // Daily ------------------------------------------------------------------------------------

    /// <summary>The townsfolk a regiment would take next: the idle first, then the worst paid; the willing only for the levy.</summary>
    private static List<Agent> Candidates(Town town, Regiment r)
    {
        bool levy = Unit(town, r).IsLevy;
        return town.Workers
            .Where(a => Fit(town, a) && (!levy || Willing(town, a)))
            .OrderBy(a => a.JobId == null ? 0 : 1)
            .ThenBy(a => a.JobId is int j ? town.Building(j).Def.Wage : 0)
            .ThenBy(a => a.Id)
            .ToList();
    }

    /// <summary>Fill a regiment at once, past the day's pace (for staged scenes and tests).</summary>
    public static void Fill(Town town, Regiment r) => Recruit(town, r, r.Wanted);

    /// <summary>Take recruits, as many as a day allows and the treasury and the market can arm.</summary>
    private static void Recruit(Town town, Regiment r, int? most = null)
    {
        var u = Unit(town, r);
        int want = Math.Min(r.Wanted - r.Soldiers.Count, most ?? Rules(town).RecruitPerDay);
        if (want <= 0) return;
        r.Short = "";
        var pool = Candidates(town, r);
        if (pool.Count == 0)
        {
            r.Short = u.IsLevy ? "no more willing" : "no townsfolk to spare";
            return;
        }
        var hall = r.HallId is int hid ? town.Building(hid) : null;
        foreach (var a in pool.Take(want))
        {
            double cost = RecruitCost(town, u);
            if (town.Treasury < cost)
            {
                r.Short = "no gold to arm them";
                return;
            }
            town.Book(TreasuryAccount.Army, -cost);
            if (!u.IsLevy) foreach (var (g, n) in u.Goods) town.Market.Buy(g, n);
            // A recruit comes in raw and pulls the regiment's drill down with them.
            r.Drill = r.Drill * r.Soldiers.Count / (r.Soldiers.Count + 1);
            r.Soldiers.Add(a.Id);
            a.RegimentId = r.Id;
            if (hall != null)
            {
                if (a.JobId is int j) Population.Remove(town.Building(j).Workers, a.Id);
                a.JobId = hall.Id;
                hall.Workers.Add(a.Id);
            }
        }
    }

    /// <summary>A soldier goes home: a standing one back to look for a trade, the levy to the work they left.</summary>
    private static void Release(Town town, Regiment r, int id)
    {
        Population.Remove(r.Soldiers, id);
        var a = town.Agent(id);
        a.RegimentId = null;
        if (r.HallId is int hid && a.JobId == hid)
        {
            Population.Remove(town.Building(hid).Workers, id);
            a.JobId = null;
        }
    }

    /// <summary>The fallen and the gone leave the rolls; a regiment whose hall is gone is stood down; heroes who left give up command.</summary>
    private static void Sync(Town town)
    {
        foreach (var r in town.Army.Regiments.ToList())
        {
            foreach (int id in r.Soldiers.ToList())
            {
                var a = town.Agent(id);
                if (a.Dead || a.Departed) Population.Remove(r.Soldiers, id);
            }
            if (Commander(town, r) is { } h && (h.Dead || h.Departed || h.Hero!.Duty != HeroDuty.Command)) StandDownCommander(town, r);
            if (r.HallId is int hid && (hid < 0 || hid >= town.Buildings.Count || town.Building(hid).Def.Trains == 0 || !town.Building(hid).Complete))
            {
                town.Log(LedgerCategory.War, Importance.Notable, $"With its hall gone, the {r.Name} has nowhere to drill and is stood down.");
                Disband(town, r.Id);
            }
        }
    }

    /// <summary>The day's pay, from the treasury; a regiment that goes unpaid loses men.</summary>
    private static void Pay(Town town)
    {
        foreach (var r in town.Army.Regiments.OrderBy(r => r.Id).ToList())
        {
            double wage = Wage(town, r);
            if (wage <= 0 || r.Soldiers.Count == 0) continue;
            double all = wage * r.Soldiers.Count;
            if (town.Treasury >= all)
            {
                town.Book(TreasuryAccount.Army, -all);
                foreach (int id in r.Soldiers) town.Agent(id).Gold += wage;
                continue;
            }
            int leave = Math.Max(1, (int)Math.Ceiling(r.Soldiers.Count * Rules(town).Desert));
            for (int i = 0; i < leave && r.Soldiers.Count > 0; i++) Release(town, r, r.Soldiers[^1]);
            r.Wanted = Math.Min(r.Wanted, Math.Max(1, r.Soldiers.Count));
            town.Log(LedgerCategory.War, Importance.Notable, $"With no gold for their pay, {leave} of the {r.Name} hand back their arms and go home.");
        }
    }

    /// <summary>A day's drill: at the post's pace, faster under a hero.</summary>
    private static void DrillDay(Town town)
    {
        foreach (var r in town.Army.Regiments)
        {
            if (r.Soldiers.Count == 0) continue;
            var u = Unit(town, r);
            double pace = Post(town, r.Post).Drill * (1 + Command(town, r) * Rules(town).Command.Drill);
            r.Drill = Math.Min(1, r.Drill + pace / Math.Max(1, u.Drill));
        }
    }

    public static void Daily(Town town)
    {
        if (town.Army.Regiments.Count == 0) return;
        Sync(town);
        Pay(town);
        DrillDay(town);
        foreach (var r in town.Army.Regiments.OrderBy(r => r.Id).ToList())
        {
            if (Unit(town, r).IsLevy) continue;
            Recruit(town, r);
        }
    }

    // In town ----------------------------------------------------------------------------------

    /// <summary>Mood a soldier gets from serving: pride in the colours, or a conscript's and an unpaid man's grudge.</summary>
    public static double Mood(Town town, Agent a)
    {
        if (a.RegimentId == null || Of(town, a) is not { } r) return 0;
        if (!Unit(town, r).IsLevy) return Rules(town).Pride;
        var l = Rules(town).Levy;
        return (town.Army.Service == ServiceLaw.Conscription ? l.ConscriptMood : 0) + (town.Army.LevyPaid ? 0 : l.UnpaidDayMood);
    }

    /// <summary>Whether a townsman spends their working hours soldiering.</summary>
    public static bool Serving(Agent a) => a.RegimentId != null;

    /// <summary>An hour of service: drilling at the hall, on the wall, walking the guard's round, or standing at the hall.</summary>
    public static void Serve(Town town, Agent a)
    {
        var r = Of(town, a);
        if (r == null) return;
        var hall = Economy.Hall(town);
        if (Field.Away(town, r))
        {
            // Out on the road with the host.
            a.Activity = Activity.Marching;
            a.LocationId = r.HallId ?? hall?.Id;
            return;
        }
        switch (r.Post)
        {
            case ArmyPost.Patrol:
                // The round goes building to building through the day, each soldier a few doors from the last.
                a.Activity = Activity.Patrolling;
                var stops = Rounds(town);
                a.LocationId = stops.Count == 0 ? null : stops[(a.Id * 7 + town.Hour * 3) % stops.Count].Id;
                break;
            case ArmyPost.Guard:
                a.Activity = Activity.Guarding;
                a.LocationId = hall?.Id ?? r.HallId;
                break;
            case ArmyPost.Walls:
                a.Activity = Activity.Guarding;
                a.LocationId = r.HallId ?? hall?.Id;
                break;
            default:
                a.Activity = Activity.Drilling;
                a.LocationId = r.HallId ?? hall?.Id;
                break;
        }
    }

    /// <summary>The buildings the guard's round passes, by id: everything finished, farms and camps included (kept for the day).</summary>
    private static List<Building> Rounds(Town town)
    {
        var st = town.Army;
        if (st.RoundsDay == town.Day && st.Rounds is { } stops) return stops;
        st.Rounds = town.Buildings.Where(b => b.Complete && !b.IsSite).OrderBy(b => b.Id).ToList();
        st.RoundsDay = town.Day;
        return st.Rounds;
    }

    // Battle ----------------------------------------------------------------------------------

    /// <summary>
    /// The army's share of a siege's dead: soldiers fall in proportion to what their regiment put on the walls, less what
    /// its healers save. Returns the fallen.
    /// </summary>
    public static List<Agent> Bloodied(Town town, double lost, double defence)
    {
        var dead = new List<Agent>();
        if (town.Army.Regiments.Count == 0 || defence <= 0 || lost <= 0) return dead;
        double share = Math.Min(0.9, lost / defence) * Rules(town).Losses;
        foreach (var r in town.Army.Regiments.OrderBy(r => r.Id).ToList())
        {
            if (Field.Away(town, r)) continue;
            var u = Unit(town, r);
            double w = share * Post(town, r.Post).Siege * (1 - u.Mend);
            int n = (int)Math.Floor(r.Soldiers.Count * w + town.Rng.Next());
            dead.AddRange(Fall(town, r, n));
        }
        return dead;
    }

    /// <summary>Soldiers of a regiment, picked by the dice, fall.</summary>
    public static List<Agent> Fall(Town town, Regiment r, int n)
    {
        var dead = new List<Agent>();
        for (int i = 0; i < n && r.Soldiers.Count > 0; i++)
        {
            var a = town.Agent(r.Soldiers[town.Rng.Range(0, r.Soldiers.Count)]);
            Release(town, r, a.Id);
            Threat.Kill(town, a);
            r.Fallen++;
            dead.Add(a);
        }
        return dead;
    }
}
