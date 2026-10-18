namespace CrownAndCompass.Sim.Systems;

/// <summary>Clockwork wardens as the workshops build them (content/game/clockwork.json).</summary>
public sealed record ClockworkRules
{
    /// <summary>The charter the town needs, and the buildings (any one) that can build and wind them.</summary>
    public string Tier { get; init; } = "city";
    public IReadOnlyList<string> Workshops { get; init; } = ["mage_tower", "guildhall"];
    public double Cost { get; init; } = 900;

    /// <summary>Oil, springs and a tinker's time, a day per warden.</summary>
    public double Upkeep { get; init; } = 6;
    public int Max { get; init; } = 6;

    /// <summary>Each warden's strength at the walls, and the chance it adds to the watch catching a schemer's agents.</summary>
    public double Strength { get; init; } = 30;
    public double Catch { get; init; } = 0.05;

    /// <summary>Mood each warden costs the townsfolk, who don't like being watched by things that never blink, up to a cap.</summary>
    public double Distrust { get; init; } = 0.4;
    public double DistrustCap { get; init; } = 2;

    /// <summary>Daily chance per warden that the Quiet Hand or a scheming menace rewrites its orders; halved while a mage tower keeps the keys.</summary>
    public double Seize { get; init; } = 0.003;
    public string Faction { get; init; } = "quiet-hand";
    public string Schemers { get; init; } = "schemers";
    public double SeizedPower { get; init; } = 25;
    public string Keys { get; init; } = "mage_tower";
}

public sealed class ClockworkState
{
    public int Wardens { get; set; }
    public int Built { get; set; }
    public int Seized { get; set; }
}

/// <summary>
/// Clockwork wardens: a late buy for a city with a mage tower or a guildhall. Tireless guards that stand on the
/// walls and take no bribes (they help the watch catch a schemer's agents however crooked its captain is), but the
/// townsfolk don't trust them, and the Quiet Hand or a scheming menace can rewrite one's orders and walk it off.
/// </summary>
public static class Clockwork
{
    public static ClockworkRules Rules(Town town) => town.Content.Clockwork;

    public static bool Workshop(Town town) =>
        town.Buildings.Any(b => Rules(town).Workshops.Contains(b.Def.Id) && b.Complete && !b.Closed);

    /// <summary>Why the town can't build one now, or null when it can.</summary>
    public static string? BuildBlocked(Town town)
    {
        var r = Rules(town);
        if (r.Tier != "" && !City.Reached(town, r.Tier)) return $"needs the town to be a {City.TierName(town, r.Tier).ToLowerInvariant()}";
        if (!Workshop(town)) return "needs a mage tower or a guildhall to build them";
        if (town.Clockwork.Wardens >= r.Max) return "the workshops can wind no more";
        if (town.Treasury < r.Cost) return "not enough gold";
        return null;
    }

    public static bool Build(Town town)
    {
        if (BuildBlocked(town) != null) return false;
        town.Book(TreasuryAccount.Clockwork, -Rules(town).Cost);
        town.Clockwork.Wardens++;
        town.Clockwork.Built++;
        town.Log(LedgerCategory.War, Importance.Notable, town.Clockwork.Built == 1
            ? "The first clockwork warden clanks out of the workshop and takes its place on the wall. It does not sleep, and it cannot be bought. People cross the street to stay out of its sight."
            : $"Another clockwork warden is wound and set on the wall. {town.Clockwork.Wardens} of them keep watch now.");
        return true;
    }

    /// <summary>Melt one down; nothing comes back.</summary>
    public static bool Scrap(Town town)
    {
        if (town.Clockwork.Wardens == 0) return false;
        town.Clockwork.Wardens--;
        town.Log(LedgerCategory.War, Importance.Routine, "A clockwork warden is unwound and broken up for its brass. The street it watched breathes easier.");
        return true;
    }

    public static double Wages(Town town) => town.Clockwork.Wardens * Rules(town).Upkeep;

    /// <summary>Strength the wardens add at the walls.</summary>
    public static double Defense(Town town) => town.Clockwork.Wardens * Rules(town).Strength;

    /// <summary>Chance they add to the watch catching agents; no bribed captain spoils it.</summary>
    public static double Catch(Town town) => town.Clockwork.Wardens * Rules(town).Catch;

    /// <summary>Mood the townsfolk lose to things that watch them and never blink.</summary>
    public static double Mood(Town town) => -Math.Min(Rules(town).DistrustCap, town.Clockwork.Wardens * Rules(town).Distrust);

    /// <summary>Daily: wind them (an empty treasury lets one run down), and let a faction or a schemer try to take one.</summary>
    public static void Daily(Town town, bool autoMayor)
    {
        var r = Rules(town);
        var c = town.Clockwork;
        if (autoMayor && c.Wardens < r.Max && town.Day % 15 == 0 && town.Treasury > r.Cost * 4 && BuildBlocked(town) == null) Build(town);
        if (c.Wardens == 0) return;
        if (town.Treasury < Wages(town))
        {
            c.Wardens--;
            town.Log(LedgerCategory.War, Importance.Notable, "With no coin for oil and springs, a clockwork warden winds down on the wall and stands there, still.");
            return;
        }
        town.Book(TreasuryAccount.Clockwork, -Wages(town));

        var hand = Factions.Find(town, r.Faction);
        var schemer = Menaces.Alive(town).Where(m => m.Kind == r.Schemers && m.Named).OrderByDescending(m => m.Power).ThenBy(m => m.Id).FirstOrDefault();
        if (hand is not { Active: true } && schemer == null) return;
        double chance = r.Seize * c.Wardens * (town.Buildings.Any(b => b.Def.Id == r.Keys && b.Complete && !b.Closed) ? 0.5 : 1);
        if (!town.Rng.Chance(chance)) return;
        c.Wardens--;
        c.Seized++;
        town.Tension += 15;
        if (schemer != null)
        {
            schemer.Power += r.SeizedPower;
            town.Log(LedgerCategory.War, Importance.Major, $"Someone in the pay of {schemer.Name} gets at a clockwork warden's mainspring in the night. By morning it has walked out of the gate and gone to them.");
        }
        else town.Log(LedgerCategory.War, Importance.Major, "A clockwork warden turns from the wall at midnight and walks into the Quiet Hand's cellars. Someone there knew how to rewrite its orders.");
    }
}
