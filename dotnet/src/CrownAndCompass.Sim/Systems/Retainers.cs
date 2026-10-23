namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Companies kept on the town's retainer for good. The council pays a signing price that climbs with the company's
/// levels and renown, then a daily wage. A retained company leans hard toward every job the council offers it, brings
/// home what it saw on its own trips without selling it, and its members don't drift off. When the treasury can't pay
/// the wage for a few days the company walks, sore. Numbers in bounties.json offers.retainer.
/// </summary>
public static class Retainers
{
    private static RetainerRules Rules(Town town) => town.Content.Bounties.Offers.Retainer;

    /// <summary>Whether the company rides on the town's retainer.</summary>
    public static bool Held(Town town, string? company) =>
        company != null && Intel.Find(town, company) is { Retained: true };

    /// <summary>Whether this hero rides with a retained company.</summary>
    public static bool Holds(Town town, Agent a) => Held(town, a.Hero?.PartyName);

    /// <summary>What it costs to sign the company: its members' levels (steeply) and its renown.</summary>
    public static double Price(Town town, string company)
    {
        var r = Rules(town);
        int levels = Intel.Members(town, company).Sum(m => m.Hero!.Level);
        return Math.Round(r.Base + r.PerLevel * Math.Pow(levels, r.LevelPower) + r.PerRenown * Notoriety.RenownOf(town, company));
    }

    /// <summary>The daily wage the company would ask once signed.</summary>
    public static double Wage(Town town, string company) => Math.Round(Price(town, company) * Rules(town).Wage);

    /// <summary>Why the company won't sign (gone, held, sore, no gold), or null.</summary>
    public static string? Blocked(Town town, string company)
    {
        var members = Intel.Members(town, company);
        if (members.Count == 0) return "no one rides under that name any more";
        if (Held(town, company)) return "they are on the retainer already";
        if (Intel.Find(town, company)?.SulkUntil is int until && town.Tick < until) return "they are still sore at the council";
        if (Intel.Relation(town, company, members) < Rules(town).Relation) return "they don't think enough of the council";
        if (town.Treasury < Price(town, company)) return $"the treasury can't cover {Js.Str(Price(town, company))}g";
        return null;
    }

    public static bool Retain(Town town, string company)
    {
        if (Blocked(town, company) != null) return false;
        double price = Price(town, company);
        var c = Intel.Company(town, company);
        town.Book(TreasuryAccount.Retainers, -price);
        c.GoldPaid += price;
        c.Retained = true;
        c.Wage = Wage(town, company);
        c.RetainedDay = town.Day;
        c.Unpaid = 0;
        var members = Intel.Members(town, company);
        town.Log(LedgerCategory.Quest, Importance.Notable, $"The {company} sign on as {town.Name}'s own for {Js.Str(price)} gold and {Js.Str(c.Wage)} a day.", members.Select(m => m.Id).ToList());
        return true;
    }

    public static bool Release(Town town, string company)
    {
        if (!Held(town, company)) return false;
        Intel.Company(town, company).Retained = false;
        town.Log(LedgerCategory.Quest, Importance.Notable, $"The council lets the {company} go from its retainer.", Intel.Members(town, company).Select(m => m.Id).ToList());
        return true;
    }

    /// <summary>A retained company's leaning toward a job the council offers it.</summary>
    public static double Bonus(Town town, string company) => Held(town, company) ? Rules(town).Bonus : 0;

    /// <summary>A retained hero's loyalty factor.</summary>
    public static double Loyalty(Town town, Agent a) => Holds(town, a) ? Rules(town).Loyalty : 0;

    /// <summary>Pays each retained company its wage; one unpaid too long, or with nobody left, walks.</summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        foreach (var c in town.Companies.Where(c => c.Retained).OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            var members = Intel.Members(town, c.Name);
            if (members.Count == 0)
            {
                c.Retained = false;
                continue;
            }
            if (town.Treasury >= c.Wage)
            {
                town.Book(TreasuryAccount.Retainers, -c.Wage);
                c.GoldPaid += c.Wage;
                foreach (var m in members) m.Gold += c.Wage / members.Count;
                c.Unpaid = 0;
                continue;
            }
            if (++c.Unpaid < r.LapseDays) continue;
            c.Retained = false;
            c.Unpaid = 0;
            Intel.Deal(town, c.Name, r.Dealings);
            town.Log(LedgerCategory.Quest, Importance.Major, $"Unpaid for {r.LapseDays} days, the {c.Name} quit {town.Name}'s retainer.", members.Select(m => m.Id).ToList());
        }
    }
}
