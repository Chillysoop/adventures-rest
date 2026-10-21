namespace CrownAndCompass.Sim.Systems;

public sealed record CrimeDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
}

/// <summary>A punishment the law book can set for a crime, and what each sentence moves.</summary>
public sealed record PunishmentDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";

    /// <summary>How the sentence reads after "the watch catches them": "and the magistrate fines them {fine} gold".</summary>
    public string Sentenced { get; init; } = "";

    /// <summary>Times the usual fine.</summary>
    public double Fine { get; init; } = 1;

    /// <summary>Unrest added per sentence (negative calms the town), and the share of would-be offenders it scares off.</summary>
    public double Order { get; init; }
    public double Fear { get; init; }

    /// <summary>Standing added to every guild, favour to the temple's god, Mandate, and loyalty of the punished hero.</summary>
    public double Guilds { get; init; }
    public double Temple { get; init; }
    public double Mandate { get; init; }
    public double Loyalty { get; init; }
}

public sealed record LawRules
{
    public string Default { get; init; } = "fine";
    public List<CrimeDef> Crimes { get; init; } = [];
    public List<PunishmentDef> Punishments { get; init; } = [];
    public string Changed { get; init; } = "";

    public PunishmentDef? Punishment(string id) => Punishments.FirstOrDefault(p => p.Id == id);
}

/// <summary>
/// The council's law book: a punishment set for each crime (laws.json). The watch's catches go through it, so each
/// sentence moves order, fear, the guilds and the temple, and the town's fear keeps some offenders home. A crime with no
/// law written keeps the plain fine.
/// </summary>
public static class Laws
{
    public static LawRules Rules(Town town) => town.Content.Laws;

    /// <summary>The punishment the book sets for a crime.</summary>
    public static PunishmentDef For(Town town, string crime) =>
        Rules(town).Punishment(town.Laws.GetValueOrDefault(crime, Rules(town).Default)) ?? Rules(town).Punishments[0];

    /// <summary>Times the chance an offender tries it, for the fear the punishment puts in them.</summary>
    public static double Deter(Town town, string crime) => 1 - For(town, crime).Fear;

    /// <summary>The council sets the punishment for a crime.</summary>
    public static bool Set(Town town, string crime, string punishment)
    {
        var r = Rules(town);
        var c = r.Crimes.FirstOrDefault(x => x.Id == crime);
        var p = r.Punishment(punishment);
        if (c == null || p == null || For(town, crime).Id == p.Id) return false;
        town.Laws[crime] = p.Id;
        if (r.Changed != "")
            town.Log(LedgerCategory.Council, Importance.Notable, r.Changed.Replace("{crime}", c.Name.ToLowerInvariant()).Replace("{punishment}", p.Name.ToLowerInvariant()));
        return true;
    }

    /// <summary>The watch has caught a hero at a crime: carry out the sentence the book sets, and say how it went.</summary>
    public static string Sentence(Town town, string crime, Agent a, double fine)
    {
        var p = For(town, crime);
        double take = Math.Min(Math.Floor(a.Gold), Math.Floor(fine * p.Fine));
        a.Gold -= take;
        if (take > 0) town.Book(TreasuryAccount.Fines, take);
        Loyalty.Sway(a, town.Content.Notoriety.Crime.CaughtLoyalty + p.Loyalty);
        if (p.Order != 0) town.Taxes.Unrest = Math.Clamp(town.Taxes.Unrest + p.Order, 0, 100);
        if (p.Mandate != 0) town.Mandate = Math.Clamp(town.Mandate + p.Mandate, 0, 100);
        if (p.Guilds != 0)
            foreach (var g in town.Guilds) g.Standing = Math.Clamp(g.Standing + p.Guilds, 0, 100);
        if (p.Temple != 0 && Faith.TempleGod(town) is { } god)
            town.Faiths.Favor[god.Id] = Math.Max(0, town.Faiths.Favor.Get(god.Id, 0) + p.Temple);
        return take <= 0 && p.Fine > 0 ? "" : " " + p.Sentenced.Replace("{fine}", Js.Str(take));
    }
}
