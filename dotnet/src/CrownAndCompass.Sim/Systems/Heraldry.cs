namespace CrownAndCompass.Sim.Systems;

public sealed record TinctureDef(string Id, string Name, string Hex);

public sealed record DivisionDef(string Id, string Name);

public sealed record ChargeDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Words in a company's name that call for this charge ("the Iron Wolves" bear a wolf's head).</summary>
    public IReadOnlyList<string> Words { get; init; } = [];
}

public sealed record HeraldryRules
{
    public IReadOnlyList<TinctureDef> Tinctures { get; init; } = [];
    public IReadOnlyList<TinctureDef> Metals { get; init; } = [];
    public IReadOnlyList<DivisionDef> Divisions { get; init; } = [];
    public IReadOnlyList<ChargeDef> Charges { get; init; } = [];

    /// <summary>Wear each venture puts on a company's colours, more when one of them died, and what a day at home mends.</summary>
    public double Venture { get; init; } = 0.05;
    public double Bloody { get; init; } = 0.1;
    public double Mend { get; init; } = 0.004;

    /// <summary>Wear past which the company is proud of its rags (once, told in the Ledger).</summary>
    public double Proud { get; init; } = 0.6;
    public IReadOnlyList<string> ProudText { get; init; } = [];
}

/// <summary>A company's arms: a field of a colour (divided with a metal), a charge in the metal, and the blazon in words.</summary>
public sealed record Arms(TinctureDef Field, TinctureDef Metal, DivisionDef Division, ChargeDef Charge)
{
    /// <summary>"Blue, per bend silver, a tower silver", plain enough to read aloud.</summary>
    public string Blazon => Division.Id == "plain"
        ? $"{Cap(Field.Name)}, {Charge.Name} in {Metal.Name}"
        : $"{Cap(Field.Name)} and {Metal.Name} {Division.Name}, {Charge.Name}";

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}

/// <summary>
/// Company heraldry: the colours and badge each company rides under, on its tabards and its banner. Arms come from
/// the company's name (a word in it that suits a charge, else by lot from the name), so a company always bears the
/// same arms; they change nothing in a fight. What changes is the cloth: every venture wears it, a bloody one more,
/// and days at home mend it, so a veteran company's banner is a ragged thing it is proud of.
/// </summary>
public static class Heraldry
{
    private static HeraldryRules Rules(Town town) => town.Content.Heraldry;

    /// <summary>The arms a company bears, always the same for the same name.</summary>
    public static Arms Of(Content content, string company)
    {
        var r = content.Heraldry;
        uint h = Voices.Hash("arms:" + company);
        var charge = r.Charges.FirstOrDefault(c => c.Words.Any(w => company.Contains(w, StringComparison.OrdinalIgnoreCase)))
            ?? r.Charges[(int)(h % (uint)r.Charges.Count)];
        var field = r.Tinctures[(int)(h / 7 % (uint)r.Tinctures.Count)];
        var metal = r.Metals[(int)(h / 61 % (uint)r.Metals.Count)];
        var division = r.Divisions[(int)(h / 433 % (uint)r.Divisions.Count)];
        return new Arms(field, metal, division, charge);
    }

    /// <summary>A company home from a venture: its colours carry the marks of it.</summary>
    public static void Home(Town town, string company, bool bloody)
    {
        var r = Rules(town);
        var c = Intel.Company(town, company);
        c.Ventures++;
        double was = c.Wear;
        c.Wear = Math.Min(1, c.Wear + r.Venture + (bloody ? r.Bloody : 0));
        c.MendedDay = town.Day;
        if (was < r.Proud && c.Wear >= r.Proud && !c.Proud && r.ProudText.Count > 0)
        {
            c.Proud = true;
            town.Log(LedgerCategory.Quest, Importance.Notable,
                r.ProudText[(int)(Voices.Hash("proud:" + company, c.Ventures) % (uint)r.ProudText.Count)].Replace("{company}", company, StringComparison.Ordinal));
        }
    }

    /// <summary>Daily: colours mend while nobody of the company is on the road.</summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        foreach (var c in town.Companies.Where(c => c.Wear > 0).OrderBy(c => c.Name, StringComparer.Ordinal))
            if (!town.Parties.Any(p => !p.Returned && p.Name == c.Name)) c.Wear = Math.Max(0, c.Wear - r.Mend);
    }

    /// <summary>The wear in a word, for the roster.</summary>
    public static string Worn(double wear) => wear switch
    {
        >= 0.8 => "in rags, and proud of it",
        >= 0.5 => "battered and patched",
        >= 0.25 => "weathered",
        > 0.05 => "a little worn",
        _ => "new",
    };
}
