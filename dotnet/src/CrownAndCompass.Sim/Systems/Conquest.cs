namespace CrownAndCompass.Sim.Systems;

/// <summary>Marching on other settlements, what a won one becomes, and what holding it costs (army.json "conquest").</summary>
public sealed record ConquestRules
{
    /// <summary>A settlement's defenders: its host as an ally would send it, times this behind its own walls.</summary>
    public double Defend { get; init; } = 1.5;

    /// <summary>Standing lost by the attacked, its kin elsewhere, every ally (more when the target was one) and every other people.</summary>
    public double TargetHit { get; init; } = 60;
    public double KinHit { get; init; } = 30;
    public double AllyHit { get; init; } = 8;
    public double BetrayHit { get; init; } = 25;
    public double OthersHit { get; init; } = 4;

    /// <summary>Standing gained with peoples who bear the target's people a grudge, and with one who asked for the blow.</summary>
    public double GrudgeGain { get; init; } = 10;
    public double AskGain { get; init; } = 20;

    /// <summary>Peoples who bear each other a grudge, by people id.</summary>
    public IReadOnlyList<string[]> Grudges { get; init; } = [];

    /// <summary>Daily chance an ally with a grudge asks the town to strike a settlement of its foes, and days the ask stands.</summary>
    public double AskChance { get; init; } = 0.01;
    public int AskDays { get; init; } = 30;

    /// <summary>Pillage: gold per point of prosperity, and the place is burned. Cripple: a share of that, and its prosperity and safety cut.</summary>
    public double Loot { get; init; } = 12;
    public double CrippleLoot { get; init; } = 0.5;
    public double Crippled { get; init; } = 0.4;

    /// <summary>Occupation: tribute per point of prosperity a day; resistance's daily rise, less per point of garrison strength; prosperity's daily wear.</summary>
    public double Tribute { get; init; } = 0.15;
    public double Resist { get; init; } = 3;
    public double ResistPerGarrison { get; init; } = 0.08;
    public double Wear { get; init; } = 0.2;

    /// <summary>Daily chance of a blow from the resistance (sabotage, a knife), per point of resistance.</summary>
    public double BlowPerResist { get; init; } = 0.004;

    /// <summary>Resistance at which the place rises to take itself back, and what the rising costs it either way.</summary>
    public double Rising { get; init; } = 100;
    public double Crushed { get; init; } = 45;

    /// <summary>Standing a freed settlement gives back.</summary>
    public double Freed { get; init; } = 25;

    /// <summary>Buying a settlement's charter: what it asks (a base, per point of prosperity and of its host) and the standing it needs to sell.</summary>
    public CharterRules Charter { get; init; } = new();
}

/// <summary>A settlement sells its charter and swears to the town without a fight (<see cref="Conquest.Buy"/>).</summary>
public sealed record CharterRules
{
    public double Standing { get; init; } = 40;
    public double Base { get; init; } = 4000;
    public double PerProsperity { get; init; } = 300;
    public double PerHost { get; init; } = 40;
}

public enum ConquestChoice
{
    Occupy,
    Pillage,
    Cripple,
    Spare,
}

/// <summary>A settlement the town holds: since when, how hard its people resist, and who garrisons it.</summary>
public sealed class Occupation
{
    public int SettlementId { get; init; }
    public int Day { get; init; }
    public double Resistance { get; set; }
    public int? GarrisonId { get; set; }
    public double Tribute { get; set; }

    /// <summary>Bought, not taken: its people sold their charter and do not resist.</summary>
    public bool Bought { get; init; }
}

/// <summary>An ally's ask: strike a settlement of its foes before the day.</summary>
public sealed class WarAsk
{
    public int AllyId { get; init; }
    public int TargetId { get; init; }
    public int UntilDay { get; init; }
}

/// <summary>
/// Marching on other settlements. The host can strike anyone the town has met, which every other people sees: the
/// attacked and its kin become enemies, allies trust the town less (much less if it turns on one of them), and only
/// those who bear the target's people a grudge are glad, more so an ally that asked for it. A won settlement waits on
/// the mayor: occupy it (tribute every day, while its people resist: sabotage, knives in the garrison and in the end a
/// rising to take it back, held down by a regiment left there), pillage and burn it, cripple it, or spare it.
/// </summary>
public static class Conquest
{
    public static ConquestRules Rules(Town town) => town.Content.Army.Conquest;

    public const string Prefix = "settlement:";

    public static Settlement? Target(Town town, string key) =>
        key.StartsWith(Prefix, StringComparison.Ordinal) && int.TryParse(key[Prefix.Length..], out int id) ? town.Settlements.FirstOrDefault(s => s.Id == id) : null;

    public static Occupation? Held(Town town, Settlement s) => town.Army.Held.FirstOrDefault(o => o.SettlementId == s.Id);

    /// <summary>The settlements the host can march on: met, standing and not already the town's.</summary>
    public static List<Settlement> Targets(Town town) =>
        town.Settlements.Where(s => s.Met && !s.Razed && Held(town, s) == null).OrderBy(s => town.Regions[s.RegionId].Days).ThenBy(s => s.Id).ToList();

    public static double Defenders(Town town, Settlement s) => Js.Round(AllyCalls.Host(town, s) * Rules(town).Defend);

    public static Field.Foe FoeOf(Town town, Settlement s, int arrival) =>
        new($"{Prefix}{s.Id}", s.Name, "settlement", s.Leader != "" ? s.Leader : $"the ruler of {s.Name}", Defenders(town, s), arrival, 0);

    public static string? AttackBlocked(Town town, Settlement s)
    {
        if (!s.Met) return "not met";
        if (s.Razed) return "burned";
        if (Held(town, s) != null) return "already ours";
        if (town.Army.Pending.Contains(s.Id)) return "waiting on your word";
        return Reaches.Unreached(town, s);
    }

    /// <summary>Whether two peoples bear each other a grudge.</summary>
    public static bool Grudge(Town town, string a, string b) =>
        Rules(town).Grudges.Any(g => g.Length == 2 && (g[0] == a && g[1] == b || g[0] == b && g[1] == a));

    /// <summary>The host marches on a settlement: every people sees it.</summary>
    public static void Declare(Town town, Settlement target)
    {
        var r = Rules(town);
        bool betrayal = target.Allied;
        target.Allied = false;
        target.Standing = Math.Min(target.Standing - r.TargetHit, -40);
        foreach (var s in town.Settlements.Where(x => x.Id != target.Id && !x.Razed).OrderBy(x => x.Id))
        {
            if (s.Kin.Id == target.Kin.Id) s.Standing -= r.KinHit;
            else if (Grudge(town, s.Kin.Id, target.Kin.Id)) s.Standing += r.GrudgeGain;
            else if (s.Allied) s.Standing -= betrayal ? r.BetrayHit : r.AllyHit;
            else s.Standing -= r.OthersHit;
            s.Standing = Math.Clamp(s.Standing, -100, 100);
            if (s.Allied && s.Standing < 0)
            {
                s.Allied = false;
                town.Log(LedgerCategory.War, Importance.Major, $"{Saga.Cap(s.People)} break their alliance with {town.Name}: a town that marches on {target.People} may march on them next.");
            }
        }
        // An ally that asked for this blow is glad of it.
        foreach (var ask in town.Army.Asks.Where(a => a.TargetId == target.Id).ToList())
        {
            if (town.Settlements.FirstOrDefault(x => x.Id == ask.AllyId) is { } asker) asker.Standing = Math.Min(100, asker.Standing + r.AskGain);
            town.Army.Asks.Remove(ask);
        }
        town.Log(LedgerCategory.War, Importance.Major, betrayal
            ? $"{town.Name} turns on its own ally: the host marches on {target.People}. Every friend the town has takes note."
            : $"The host marches on {target.People}. Word runs ahead of it through the wilds.");
    }

    /// <summary>A battle at a settlement's gates is over.</summary>
    public static void Fought(Town town, Settlement s, FieldBattle b, bool won)
    {
        if (!won)
        {
            b.Lines.Add($"{Saga.Cap(s.People)} hold their gates. The host turns for home.");
            return;
        }
        town.Army.Pending.Add(s.Id);
        b.Lines.Add($"The gates of {s.Name} are open. It waits on the mayor's word.");
        Spoils.Conquered(town, s.Kin.Id, s.Name);
    }

    public static string? SettleBlocked(Town town, Settlement s, ConquestChoice choice) =>
        !town.Army.Pending.Contains(s.Id) ? "nothing to settle" : null;

    /// <summary>The mayor's word on a won settlement.</summary>
    public static bool Settle(Town town, int settlementId, ConquestChoice choice)
    {
        var s = town.Settlements.FirstOrDefault(x => x.Id == settlementId);
        if (s == null || SettleBlocked(town, s, choice) != null) return false;
        var r = Rules(town);
        town.Army.Pending.Remove(s.Id);
        switch (choice)
        {
            case ConquestChoice.Occupy:
                town.Army.Held.Add(new Occupation { SettlementId = s.Id, Day = town.Day, Resistance = Math.Max(0, 30 - s.Standing / 4) });
                s.Allied = false;
                town.Log(LedgerCategory.War, Importance.Major, $"{town.Name} takes {s.Name} and holds it. {Saga.Cap(s.People)} pay tribute now, and hate it.");
                break;
            case ConquestChoice.Pillage:
            {
                double loot = Js.Round(s.Prosperity * r.Loot);
                town.Book(TreasuryAccount.Army, loot);
                s.Razed = true;
                s.Allied = false;
                s.Standing = -100;
                town.Log(LedgerCategory.War, Importance.Major, $"The host sacks {s.Name} and puts it to the torch: {Js.Str(loot)} gold comes home in the wagons.");
                break;
            }
            case ConquestChoice.Cripple:
            {
                double loot = Js.Round(s.Prosperity * r.Loot * r.CrippleLoot);
                town.Book(TreasuryAccount.Army, loot);
                s.Prosperity *= r.Crippled;
                s.Security *= r.Crippled;
                s.Standing = Math.Max(-100, s.Standing - 20);
                town.Log(LedgerCategory.War, Importance.Major, $"The host breaks {s.Name}'s walls, takes {Js.Str(loot)} gold and leaves it to limp on.");
                break;
            }
            default:
                s.Standing = Math.Min(100, s.Standing + 15);
                town.Log(LedgerCategory.War, Importance.Notable, $"The mayor spares {s.Name}. Its people remember the mercy, if not the march.");
                break;
        }
        return true;
    }

    /// <summary>What a settlement asks for its charter.</summary>
    public static double CharterPrice(Town town, Settlement s)
    {
        var c = Rules(town).Charter;
        return Js.Round(c.Base + s.Prosperity * c.PerProsperity + AllyCalls.Host(town, s) * c.PerHost);
    }

    /// <summary>Why the town can't buy this settlement's charter now, or null.</summary>
    public static string? BuyBlocked(Town town, Settlement s)
    {
        var c = Rules(town).Charter;
        if (s.Razed) return "they are gone";
        if (!Neighbours.Known(town, s)) return "the town hasn't met them";
        if (Held(town, s) != null) return "the town holds it already";
        if (Shadows.Shadowed(s)) return "something in the dark holds it";
        if (Reaches.Unreached(town, s) is { } high) return high;
        if (s.Standing < c.Standing) return $"they won't sell to a town they think so little of (standing {Js.Str(Js.Round(s.Standing))}, needs {Js.Str(c.Standing)})";
        if (town.Treasury < CharterPrice(town, s)) return "not enough gold";
        return null;
    }

    /// <summary>Buy a settlement's charter: it swears to the town, pays tribute and never resists, and stays a friend.</summary>
    public static bool Buy(Town town, int settlementId)
    {
        var s = town.Settlements.FirstOrDefault(x => x.Id == settlementId);
        if (s == null || BuyBlocked(town, s) != null) return false;
        double price = CharterPrice(town, s);
        town.Book(TreasuryAccount.Buyouts, -price);
        town.Army.Held.Add(new Occupation { SettlementId = s.Id, Day = town.Day, Bought = true });
        town.Army.Pending.Remove(s.Id);
        World.Shift(town, s, 10);
        town.Log(LedgerCategory.Town, Importance.Major, $"{Saga.Cap(s.People)} sell {s.Name}'s charter to {town.Name} for {Js.Str(price)} gold. Its banner hangs under the town's now.");
        return true;
    }

    /// <summary>Leave a regiment to hold an occupied settlement (or bring it home with null).</summary>
    public static bool SetGarrison(Town town, int settlementId, int? regimentId)
    {
        var o = town.Army.Held.FirstOrDefault(x => x.SettlementId == settlementId);
        if (o == null) return false;
        if (regimentId is int id)
        {
            if (Army.Find(town, id) is not { } reg || Field.Away(town, reg) || town.Army.Held.Any(x => x.GarrisonId == id)) return false;
            reg.Marches = false;
        }
        o.GarrisonId = regimentId;
        return true;
    }

    /// <summary>Whether a regiment holds an occupied settlement (and is away from the town).</summary>
    public static bool Garrisoning(Town town, Regiment r) => town.Army.Held.Any(o => o.GarrisonId == r.Id);

    /// <summary>Give an occupied settlement back its freedom.</summary>
    public static bool Free(Town town, int settlementId)
    {
        var o = town.Army.Held.FirstOrDefault(x => x.SettlementId == settlementId);
        if (o == null) return false;
        var s = town.Settlements.First(x => x.Id == settlementId);
        town.Army.Held.Remove(o);
        s.Standing = Math.Min(100, s.Standing + Rules(town).Freed);
        town.Log(LedgerCategory.War, Importance.Notable, $"{town.Name} gives {s.Name} back to its people and brings its soldiers home.");
        return true;
    }

    /// <summary>Tribute, resistance, its blows and its rising; and allies asking for blows against their foes.</summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        var rng = town.Rng;
        foreach (var o in town.Army.Held.OrderBy(o => o.SettlementId).ToList())
        {
            var s = town.Settlements.First(x => x.Id == o.SettlementId);
            var garrison = o.GarrisonId is int gid ? Army.Find(town, gid) : null;
            if (garrison != null && garrison.Soldiers.Count == 0) garrison = null;
            if (garrison == null) o.GarrisonId = null;
            double held = garrison != null ? Field.Worth(town, garrison) : 0;
            o.Tribute = Js.Round(s.Prosperity * r.Tribute * 10) / 10;
            town.Book(TreasuryAccount.Army, o.Tribute);
            // A sold charter is kept willingly: no wear and no resistance.
            if (o.Bought) continue;
            s.Prosperity = Math.Max(5, s.Prosperity - r.Wear);
            o.Resistance = Math.Clamp(o.Resistance + r.Resist - held * r.ResistPerGarrison, 0, r.Rising);
            if (o.Resistance >= r.Rising)
            {
                Rise(town, o, s, garrison);
                continue;
            }
            if (!rng.Chance(o.Resistance * r.BlowPerResist)) continue;
            if (garrison != null && rng.Chance(0.5))
            {
                var dead = Army.Fall(town, garrison, 1);
                if (dead.Count > 0) town.Log(LedgerCategory.War, Importance.Notable, $"A knife in the night in {s.Name}: {dead[0].Name} of the {garrison.Name} is found in the street.", [dead[0].Id]);
            }
            else
            {
                town.Book(TreasuryAccount.Army, -o.Tribute);
                town.Log(LedgerCategory.War, Importance.Notable, $"The tribute wagon from {s.Name} burns on the road: the resistance there is bold.");
            }
        }

        // An ally with a grudge asks the town to strike its foes.
        town.Army.Asks.RemoveAll(a => a.UntilDay < town.Day);
        if (town.Army.Asks.Count > 0 || r.Grudges.Count == 0) return;
        var allies = town.Settlements.Where(s => s.Allied && !s.Razed).OrderBy(s => s.Id).ToList();
        if (allies.Count == 0 || !rng.Chance(r.AskChance)) return;
        foreach (var ally in allies)
        {
            var foe = Targets(town).FirstOrDefault(t => t.Id != ally.Id && !t.Allied && Grudge(town, ally.Kin.Id, t.Kin.Id));
            if (foe == null) continue;
            town.Army.Asks.Add(new WarAsk { AllyId = ally.Id, TargetId = foe.Id, UntilDay = town.Day + r.AskDays });
            town.Log(LedgerCategory.War, Importance.Major, $"An envoy from {ally.People} asks {town.Name} to march on {foe.People}, their old foes. They would remember it.");
            return;
        }
    }

    /// <summary>The settlement rises to take itself back: the garrison against the people.</summary>
    private static void Rise(Town town, Occupation o, Settlement s, Regiment? garrison)
    {
        var r = Rules(town);
        double rebels = AllyCalls.Host(town, s) * (0.6 + town.Rng.Next() * 0.6);
        double held = garrison != null ? Field.Worth(town, garrison) * (0.8 + town.Rng.Next() * 0.4) : 0;
        if (held >= rebels)
        {
            o.Resistance = r.Crushed;
            if (garrison != null) Army.Fall(town, garrison, (int)Math.Floor(garrison.Soldiers.Count * 0.15));
            s.Prosperity = Math.Max(5, s.Prosperity * 0.85);
            town.Log(LedgerCategory.War, Importance.Major, $"{s.Name} rises against the town, and the {garrison!.Name} put the rising down in blood.");
            return;
        }
        town.Army.Held.Remove(o);
        int lost = 0;
        if (garrison != null) lost = Army.Fall(town, garrison, (int)Math.Ceiling(garrison.Soldiers.Count * 0.5)).Count;
        s.Standing = Math.Min(s.Standing, -80);
        town.Log(LedgerCategory.War, Importance.Major, $"{s.Name} rises and takes itself back!{(lost > 0 ? $" {lost} of the garrison die in the streets; the rest fight their way home." : " There was no one there to hold it.")}");
    }
}
