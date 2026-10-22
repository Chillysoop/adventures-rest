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

/// <summary>A spell the court can hold a trial under, once the town is big enough and someone at home can cast it.</summary>
public sealed record TruthDef
{
    /// <summary>The library spell (SRD).</summary>
    public string Spell { get; init; } = "";
    public string Icon { get; init; } = "";

    /// <summary>The town's charter tier it opens at (city.json tier id).</summary>
    public string Tier { get; init; } = "town";

    /// <summary>A lie can't be told under it: the guilty are always found so.</summary>
    public bool Certain { get; init; }

    /// <summary>Otherwise, added to the evidence.</summary>
    public int Bonus { get; init; }

    /// <summary>Gold each trial pays the caster from the treasury.</summary>
    public double Fee { get; init; }
    public string Text { get; init; } = "";
}

/// <summary>How a trial weighs the evidence against the lies (laws.json "trial"). No dice: the stronger case wins.</summary>
public sealed record TrialRules
{
    /// <summary>Evidence a crime done in front of a crowd (a taproom brawl) carries.</summary>
    public int Witnesses { get; init; } = 2;

    /// <summary>A point of evidence for each this many watchmen, up to the most.</summary>
    public int WatchPer { get; init; } = 4;
    public int WatchMost { get; init; } = 3;

    /// <summary>Mandate a guilty one walking free costs.</summary>
    public double Acquitted { get; init; } = 1;

    /// <summary>Trials the court's record keeps.</summary>
    public int Kept { get; init; } = 12;
    public List<TruthDef> Truth { get; init; } = [];

    /// <summary>{evidence}, {lies}, {magic}.</summary>
    public string Freed { get; init; } = "";
    public string Under { get; init; } = "";

    public TruthDef? Of(string spell) => Truth.FirstOrDefault(t => t.Spell == spell);
}

/// <summary>A trial before the court: who stood accused, the case each side made, and the verdict.</summary>
public sealed class Trial
{
    public int Day { get; init; }
    public int AgentId { get; init; }
    public string Crime { get; init; } = "";
    public int Evidence { get; init; }
    public int Lies { get; init; }
    public string Magic { get; init; } = "";
    public bool Guilty { get; init; }
}

public sealed record LawRules
{
    public string Default { get; init; } = "fine";
    public TrialRules Trial { get; init; } = new();
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

    // ---------- Trials ----------

    /// <summary>Heroes at home who can cast a spell: their class has it, and they have reached its circle.</summary>
    public static List<Agent> Casters(Town town, string spell)
    {
        var sp = town.Content.Library.Spell(spell);
        if (sp == null) return [];
        return town.Heroes
            .Where(a => !a.IsAway && sp.Classes.Contains(a.Hero!.Cls.Id) && Moments.SpellLevel(a.Hero.Cls.Magic, a.Hero.Level) >= sp.Level)
            .OrderByDescending(a => a.Hero!.Level).ThenBy(a => a.Id).ToList();
    }

    /// <summary>Why the court can't hold trials under this spell now, or null.</summary>
    public static string? TruthBlocked(Town town, TruthDef t)
    {
        if (!City.Reached(town, t.Tier)) return $"needs a {City.TierName(town, t.Tier).ToLowerInvariant()}";
        if (Casters(town, t.Spell).Count == 0) return "no one at home can cast it";
        return null;
    }

    /// <summary>The council sets the truth magic trials are held under ("" for none).</summary>
    public static bool SetTrialMagic(Town town, string spell)
    {
        var r = Rules(town).Trial;
        if (spell == town.TrialMagic) return false;
        if (spell != "" && (r.Of(spell) is not { } t || TruthBlocked(town, t) != null)) return false;
        town.TrialMagic = spell;
        string name = spell == "" ? "" : town.Content.Library.Spell(spell)?.Name ?? spell;
        town.Log(LedgerCategory.Council, Importance.Notable, spell == ""
            ? "The council orders trials held as of old: the watch's word against the accused's."
            : $"The council orders every trial held under {Library.Prose(name)}: no one will lie to the court again.");
        return true;
    }

    /// <summary>The stand-in mayor holds trials under the surest truth spell the town can use.</summary>
    public static void StandIn(Town town)
    {
        if (town.TrialMagic != "") return;
        var t = Rules(town).Trial.Truth.OrderByDescending(x => x.Certain).FirstOrDefault(x => TruthBlocked(town, x) == null);
        if (t != null) SetTrialMagic(town, t.Spell);
    }

    /// <summary>The truth magic this trial can be held under: the one the council chose, if its caster is home and the treasury pays the fee.</summary>
    public static (TruthDef? Def, Agent? Caster) TruthNow(Town town)
    {
        if (town.TrialMagic == "" || Rules(town).Trial.Of(town.TrialMagic) is not { } t) return (null, null);
        var caster = Casters(town, t.Spell).FirstOrDefault();
        if (caster == null || town.Treasury < t.Fee) return (t, null);
        return (t, caster);
    }

    /// <summary>The case against someone: 10, the captain's Investigation (or the watch's), the watch's numbers, witnesses and any truth magic's help.</summary>
    public static int Evidence(Town town, bool witnessed, TruthDef? magic)
    {
        var r = Rules(town).Trial;
        var captain = Council.Holder(town, SeatId.Captain);
        int skill = captain != null ? Skills.Bonus(town, captain, "investigation") : Skills.WatchBonus(town, "investigation");
        return 10 + skill + Math.Min(r.WatchMost, town.Watch / Math.Max(1, r.WatchPer)) + (witnessed ? r.Witnesses : 0) + (magic?.Bonus ?? 0);
    }

    /// <summary>The story the accused tells: 10 and their Deception.</summary>
    public static int Lies(Town town, Agent a) => 10 + Skills.Bonus(town, a, "deception");

    /// <summary>
    /// Bring someone the watch caught before the court. No dice: the case against them (the captain's Investigation,
    /// the watch, witnesses) against the tale they tell (their Deception), the stronger winning and ties going to the
    /// court. Under a truth spell such as Zone of Truth no lie holds. Returns the trial, kept on the court's record.
    /// </summary>
    public static Trial Try(Town town, string crime, Agent a, bool witnessed)
    {
        var r = Rules(town).Trial;
        var (magic, caster) = TruthNow(town);
        if (caster == null) magic = null;
        if (magic != null && magic.Fee > 0)
        {
            town.Book(TreasuryAccount.Fines, -magic.Fee);
            caster!.Gold += magic.Fee;
        }
        int evidence = Evidence(town, witnessed, magic), lies = Lies(town, a);
        var trial = new Trial
        {
            Day = town.Day, AgentId = a.Id, Crime = crime, Evidence = evidence, Lies = lies, Magic = magic?.Spell ?? "",
            Guilty = magic?.Certain == true || evidence >= lies,
        };
        town.Trials.Add(trial);
        if (town.Trials.Count > r.Kept) town.Trials.RemoveRange(0, town.Trials.Count - r.Kept);
        return trial;
    }

    private static string Fill(Town town, string text, Trial t) =>
        text.Replace("{evidence}", t.Evidence.ToString()).Replace("{lies}", t.Lies.ToString())
            .Replace("{magic}", t.Magic == "" ? "" : Library.Prose(town.Content.Library.Spell(t.Magic)?.Name ?? t.Magic));

    /// <summary>
    /// The watch has caught a hero at a crime: they stand trial, and if found guilty the sentence the book sets is
    /// carried out. Says how it went.
    /// </summary>
    public static string Sentence(Town town, string crime, Agent a, double fine, bool witnessed = false)
    {
        var r = Rules(town).Trial;
        var trial = Try(town, crime, a, witnessed);
        string under = trial.Magic != "" ? Fill(town, r.Under, trial) : "";
        if (!trial.Guilty)
        {
            town.Mandate = Math.Clamp(town.Mandate - r.Acquitted, 0, 100);
            return Fill(town, r.Freed, trial);
        }
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
        return under + (take <= 0 && p.Fine > 0 ? "" : " " + p.Sentenced.Replace("{fine}", Js.Str(take)));
    }
}
