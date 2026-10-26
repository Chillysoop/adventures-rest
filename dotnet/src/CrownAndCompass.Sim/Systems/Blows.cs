namespace CrownAndCompass.Sim.Systems;

/// <summary>What one kind of blow reads like, landed or stopped: {by}, {leader}, {seat}, {town}, {where}, {owner}, {victim}, {victims}, {guard}, {gold}.</summary>
public sealed record BlowText
{
    public string Landed { get; init; } = "";
    public string Stopped { get; init; } = "";

    /// <summary>For fire: set but failed to catch (<see cref="Research.Fireproof"/>), and nothing left that burns.</summary>
    public string Smoulder { get; init; } = "";
    public string Stone { get; init; } = "";
}

/// <summary>Who struck the blow, for the Ledger: a menace, a guild, a cult.</summary>
public sealed record Culprit(string Name, string Leader, string Seat);

/// <summary>
/// Hostile acts against the town by an enemy that is not at the walls: arson, assassination, sabotage of the
/// walls, the treasury and the stores, sedition, the dead walking, and plain terror. Any system with a foe
/// can strike with them (a menace's crisis, a guild's revenge); each says whether it landed. Fire and the dead
/// are met by the watch and the heroes at home, agents by the watch's vigilance. A blow's strength is the
/// foe's raiders for fire and the dead, and a multiplier on how hard agents are to catch for the rest.
/// </summary>
public static class Blows
{
    public const string Raid = "raid";
    public const string Burn = "burn";
    public const string Assassinate = "assassinate";
    public const string Walls = "walls";
    public const string Treasury = "treasury";
    public const string Stores = "stores";
    public const string Discord = "discord";
    public const string Dead = "dead";
    public const string Terror = "terror";

    private static CrisisRules Rules(Town town) => town.Content.Crises;

    private static BlowText Text(Town town, string kind) => Rules(town).Blows.GetValueOrDefault(kind) ?? new BlowText();

    /// <summary>The walls' strength while saboteurs' damage is unmended.</summary>
    public static double WallFactor(Town town) => town.Crises.WallsWeakUntil > town.Day ? Rules(town).WallsWeak : 1;

    /// <summary>
    /// Strike one blow of a kind (not a raid, which is the menace's own). Returns whether it landed, or null when an
    /// agent has slipped into town to strike it later (<see cref="Lurkers"/>): fire, a knife and sabotage are done
    /// by someone who has to come and look first, and can be caught at it. A crisis or a grudge (by its foe) that
    /// sent the agent hears how it went then.
    /// </summary>
    public static bool? Strike(Town town, string kind, Culprit by, double raiders, double cunning, Agent? mark = null, int? crisis = null, string? foe = null)
    {
        if (Lurkers.Covert(kind) && Frontier.StopsAgent(town, by)) return false;
        if (Lurkers.Covert(kind) && Lurkers.Slip(town, kind, by, raiders, cunning, mark, crisis, foe)) return null;
        return Resolve(town, kind, by, raiders, cunning, mark, null);
    }

    /// <summary>The blow itself, at once: at the building or victim given, or one picked now.</summary>
    internal static bool Resolve(Town town, string kind, Culprit by, double raiders, double cunning, Agent? mark, Building? at)
    {
        if (Powers.Forewarned(town, by)) return false;
        switch (kind)
        {
            case Burn:
                return Arson(town, by, raiders, at);
            case Assassinate:
                return Murder(town, by, cunning, mark);
            case Walls:
            case Treasury:
            case Stores:
            case Discord:
                return Sabotage(town, kind, by, cunning);
            case Dead:
                return Rising(town, by, raiders);
            case Terror:
                Fear(town, by, 2 * cunning);
                return true;
            default:
                return false;
        }
    }

    internal static string Fill(Town town, string text, Culprit by) => Saga.Sentences(text
        .Replace("{by}", by.Name)
        .Replace("{leader}", by.Leader)
        .Replace("{seat}", by.Seat)
        .Replace("{town}", town.Name));

    /// <summary>The best hero at home, or the watch: who meets a blow in the night.</summary>
    internal static (string Name, int? Id) Guard(Town town, Agent? not = null)
    {
        var h = town.Heroes.Where(a => !a.IsAway && a != not).OrderByDescending(a => a.Hero!.Level).ThenBy(a => a.Id).FirstOrDefault();
        return h != null ? ($"{h.Name} and the watch", h.Id) : ("the watch", null);
    }

    /// <summary>Whether fire can take a building: stone never burns.</summary>
    public static bool Burns(Building b) => b.Def.Stone <= 0;

    /// <summary>
    /// Fire set in a timber house, shop, workshop or farm, on the side the watch's flags leave open. The watch
    /// stops most, but some slip through (crises.json arsonSlips), and slate and lime (<see cref="Research.Fireproof"/>)
    /// put some out. Burned, it stands as a shell until its owner (or the town) pays to rebuild it.
    /// </summary>
    public static bool Arson(Town town, Culprit by, double raiders, Building? at = null)
    {
        var rng = town.Rng;
        var t = Text(town, Burn);
        var target = at is { Complete: true } && Burns(at) ? at : Kindling(town);
        if (target == null)
        {
            if (t.Stone != "" && town.Buildings.Any(b => b.Complete)) town.Log(LedgerCategory.War, Importance.Notable, Fill(town, t.Stone, by));
            return false;
        }
        if (Threat.LocalDefense(town) * Garrison.FlagFactor(town, target) >= raiders * (0.7 + rng.Next() * 0.6) && !rng.Chance(Rules(town).ArsonSlips))
        {
            var g = Guard(town);
            town.Log(LedgerCategory.War, Importance.Notable, Saga.Sentences(Fill(town, t.Stopped, by).Replace("{where}", target.Name).Replace("{guard}", g.Name)), g.Id is int gid ? [gid] : []);
            town.Tension += 4;
            return false;
        }
        if (rng.Chance(Research.Fireproof(town)))
        {
            town.Log(LedgerCategory.War, Importance.Notable, Fill(town, t.Smoulder, by).Replace("{where}", target.Name));
            town.Tension += 4;
            return false;
        }
        Wreck(town, target, 0.5 + rng.Next() * 0.3);
        town.Log(LedgerCategory.War, Importance.Major, Fill(town, t.Landed, by).Replace("{where}", target.Name).Replace("{owner}", town.OwnerName(target.OwnerId)), target.OwnerId is int o ? [o] : []);
        Fires.Spread(town, target);
        town.Tension += 15;
        town.Mandate = Math.Max(0, town.Mandate - 2);
        Saga.Scar(town, 1);
        return true;
    }

    /// <summary>What an arsonist would fire: a timber house, shop, workshop or farm, on the side the watch's flags leave open; null when nothing burns.</summary>
    public static Building? Kindling(Town town)
    {
        var timber = town.Buildings
            .Where(b => b.Complete && !b.Def.IsGrand && b.Def.Zone is Zone.Residential or Zone.Commercial or Zone.Industry or Zone.Farmland && Burns(b))
            .OrderBy(b => b.Id)
            .ToList();
        if (timber.Count == 0) return null;
        var open = Garrison.Unguarded(town, timber);
        var pool = open.Count > 0 ? open : timber;
        return pool[town.Rng.Range(0, pool.Count)];
    }

    /// <summary>
    /// A building burned or broken: it becomes a site again, short of this share of its work. Its hands are
    /// laid off and its stock is lost; whoever lives there camps in the shell until it is rebuilt.
    /// </summary>
    public static void Wreck(Town town, Building b, double share)
    {
        foreach (int id in b.Workers.ToList())
        {
            var a = town.Agent(id);
            if (a.JobId == b.Id) a.JobId = null;
            Population.Remove(b.Workers, id);
        }
        foreach (var g in b.Stock.Keys.ToList()) b.Stock[g] = 0;
        b.Cash = Math.Floor(b.Cash * 0.5);
        b.BuildProgress = Math.Floor(b.Def.BuildHours * (1 - Math.Clamp(share, 0.1, 0.9)));
        b.Opened = false;
        b.LaborBudget = 0;
        b.Contentment = Math.Min(b.Contentment, 0.5);
        b.Wrecked = true;
    }

    /// <summary>A councillor or a hero at home, killed in the night unless the watch is quicker.</summary>
    public static bool Murder(Town town, Culprit by, double cunning, Agent? mark = null)
    {
        var rng = town.Rng;
        // One the foe has sworn to kill (or its agent has been watching), if they are in town.
        var victim = mark is { Dead: false, Departed: false } && !mark.IsAway ? mark : Victim(town);
        if (victim == null) return false;
        // A mayor out in the streets may be the one the knife is for (their own dice: the stream is untouched).
        bool mayor = mark == null && Mayoralty.Marked(town);
        string who = mayor ? $"Mayor {town.Stories.Mayor.Name}" : Who(town, victim);
        var t = Text(town, Assassinate);
        // A seasoned hero is hard to knife in the dark.
        double guard = Council.WatchStrength(town) / Math.Max(0.5, cunning) + (mayor ? town.Stories.Mayor.Level * 0.04 + Army.Bodyguard(town) + Regalia.Ward(town) : (victim.Hero is { } hs ? hs.Level * 0.04 : 0) + (town.Seats.Any(s => s.HolderId == victim.Id) ? Army.Bodyguard(town) : 0));
        if (rng.Chance(Math.Min(0.9, guard)))
        {
            var g = Guard(town, mayor ? null : victim);
            town.Log(LedgerCategory.War, Importance.Notable, Saga.Sentences(Fill(town, t.Stopped, by).Replace("{victim}", who).Replace("{guard}", g.Name)), mayor ? [] : [victim.Id]);
            town.Tension += 6;
            return false;
        }
        if (mayor)
        {
            town.Log(LedgerCategory.War, Importance.Major, Saga.Sentences($"An assassin of {by.Name} steps out of the crowd at {who}."));
            Mayoralty.Knifed(town, by);
            town.Tension += 20;
            return true;
        }
        Threat.Kill(town, victim);
        town.Log(LedgerCategory.War, Importance.Major, Saga.Sentences(Fill(town, t.Landed, by).Replace("{victim}", who)), [victim.Id]);
        town.Tension += 20;
        town.Mandate = Math.Max(0, town.Mandate - 5);
        Saga.Scar(town, 2);
        return true;
    }

    /// <summary>Who an assassin would come for: a councillor, or a hero at home; null when there is nobody.</summary>
    public static Agent? Victim(Town town)
    {
        var rng = town.Rng;
        var councillors = town.Seats.Where(s => s.HolderId is int h && !town.Agent(h).Dead).OrderBy(s => s.Def.Id, StringComparer.Ordinal).ToList();
        var heroes = town.Heroes.Where(a => !a.IsAway).OrderBy(a => a.Id).ToList();
        if (councillors.Count > 0 && (heroes.Count == 0 || rng.Chance(0.6))) return town.Agent(councillors[rng.Range(0, councillors.Count)].HolderId!.Value);
        return heroes.Count > 0 ? heroes[rng.Range(0, heroes.Count)] : null;
    }

    /// <summary>A victim as the Ledger names them: by their class, or their seat on the council.</summary>
    public static string Who(Town town, Agent a)
    {
        if (a.Hero != null) return $"{a.Name} the {a.Hero.Cls.Name.ToLowerInvariant()}";
        var seat = town.Seats.FirstOrDefault(s => s.HolderId == a.Id);
        return seat != null ? $"{a.Name}, the town's {seat.Def.Title.ToLowerInvariant()}" : a.Name;
    }

    /// <summary>Agents at the walls, in the treasury, in the granaries or among the wards. The watch may catch them.</summary>
    public static bool Sabotage(Town town, string kind, Culprit by, double cunning)
    {
        var r = Rules(town);
        var t = Text(town, kind);
        if (town.Rng.Chance(Math.Min(0.9, Council.WatchStrength(town) / Math.Max(0.5, cunning))))
        {
            town.Log(LedgerCategory.War, Importance.Notable, Fill(town, t.Stopped, by));
            return false;
        }
        double gold = 0;
        switch (kind)
        {
            case Walls:
                town.Crises.WallsWeakUntil = town.Day + r.WallsWeakDays;
                break;
            case Treasury:
                gold = Math.Floor(Math.Max(0, town.Treasury) * 0.08 * cunning);
                if (gold <= 0) return false;
                town.Book(TreasuryAccount.Discrepancies, -gold);
                break;
            case Stores:
                town.Market.Seed(Good.Food, -Math.Floor(town.Market.Stock(Good.Food) * 0.33));
                foreach (var b in town.Buildings.Where(b => b.Complete && b.StockOf(Good.Food) > 0).OrderBy(b => b.Id))
                    b.Stock[Good.Food] = Math.Floor(b.StockOf(Good.Food) * 0.67);
                break;
            case Discord:
                town.Mandate = Math.Max(0, town.Mandate - 6 * cunning);
                town.Tension += 20;
                break;
        }
        town.Log(LedgerCategory.War, Importance.Major, Fill(town, t.Landed, by).Replace("{gold}", Js.Str(gold)));
        Saga.Scar(town, 1);
        return true;
    }

    /// <summary>The dead walk the lanes at night: some of the townsfolk are taken unless the watch and the heroes at home hold them.</summary>
    public static bool Rising(Town town, Culprit by, double raiders)
    {
        var rng = town.Rng;
        var t = Text(town, Dead);
        if (Threat.LocalDefense(town) * (0.5 + Council.WatchStrength(town)) >= raiders * (0.7 + rng.Next() * 0.6))
        {
            var g = Guard(town);
            town.Log(LedgerCategory.War, Importance.Notable, Saga.Sentences(Fill(town, t.Stopped, by).Replace("{guard}", g.Name)), g.Id is int gid ? [gid] : []);
            town.Tension += 6;
            return false;
        }
        var people = town.Workers.OrderBy(a => a.Id).ToList();
        int n = Math.Min(people.Count, 1 + (int)(raiders / 60));
        var dead = new List<Agent>();
        for (int i = 0; i < n; i++)
        {
            int k = rng.Range(0, people.Count);
            dead.Add(people[k]);
            people.RemoveAt(k);
        }
        if (dead.Count == 0) return false;
        foreach (var a in dead) Threat.Kill(town, a);
        town.Log(LedgerCategory.War, Importance.Major, Fill(town, t.Landed, by).Replace("{victims}", Expedition.ListNames(dead.Select(a => a.Name).ToList())), dead.Select(a => a.Id).ToList());
        town.Tension += 15 + dead.Count * 5;
        Saga.Scar(town, 1 + dead.Count);
        return true;
    }

    /// <summary>Nothing anyone can stop: dread, and a night nobody sleeps.</summary>
    public static void Fear(Town town, Culprit by, double scar)
    {
        Saga.Scar(town, scar);
        town.Tension += 10;
        string text = Text(town, Terror).Landed;
        if (text != "") town.Log(LedgerCategory.War, Importance.Notable, Fill(town, text, by));
    }
}
