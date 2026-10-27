namespace CrownAndCompass.Sim.Systems;

/// <summary>Where the mayor spends their days (reign.json "stances").</summary>
public sealed record StanceDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public double Mandate { get; init; }
    public double Loyalty { get; init; }
    public int Edge { get; init; }
}

public sealed record MayoraltyHpRules
{
    public int Base { get; init; } = 8;
    public int PerLevel { get; init; } = 5;
}

public sealed record MayoraltyRideRules
{
    public int Xp { get; init; } = 25;
    public int XpCleared { get; init; } = 60;
    public double PerDead { get; init; } = 0.25;
    public List<double> Wound { get; init; } = [0.3, 1.1];
}

public sealed record MayoraltyRaiseRules
{
    public double Cost { get; init; } = 600;
    public double PerLevel { get; init; } = 150;
    public double Reserve { get; init; } = 200;
}

/// <summary>The mayor as a living person (content/game/reign.json).</summary>
public sealed record MayoraltyRules
{
    public MayoraltyHpRules Hp { get; init; } = new();
    public double Heal { get; init; } = 0.15;
    public List<StanceDef> Stances { get; init; } = [];
    public string Start { get; init; } = "keep";
    public double Assassin { get; init; } = 0.35;
    public List<double> AssassinWound { get; init; } = [0.5, 1.3];
    public double Breach { get; init; } = 0.3;
    public List<double> BreachWound { get; init; } = [0.25, 0.8];
    public MayoraltyRideRules Ride { get; init; } = new();
    public MayoraltyRaiseRules Raise { get; init; } = new();
    public int Candidates { get; init; } = 3;
    public double SuccessorXp { get; init; } = 0.5;
    public double Leaderless { get; init; } = 0.8;
    public int DecideDays { get; init; } = 7;

    public StanceDef? Stance(string id) => Stances.FirstOrDefault(s => s.Id == id);
}

/// <summary>A mayor who held the charter before the one in office now.</summary>
public sealed class PastMayor
{
    public string Name { get; set; } = "";
    public string Background { get; set; } = "";
    public int Level { get; set; }
    public int From { get; set; }
    public int To { get; set; }
    public string Cause { get; set; } = "";
}

public sealed class MayoraltyState
{
    /// <summary>Set on New game: the mayor's death ends the campaign.</summary>
    public bool Hardcore { get; set; }
    public string Stance { get; set; } = "";

    /// <summary>Hit points lost; the most is <see cref="Mayoralty.MaxHp"/>.</summary>
    public int Wounds { get; set; }

    /// <summary>The company the mayor rides with, until it is home.</summary>
    public int? PartyId { get; set; }

    /// <summary>The day the mayor died, while the town waits on a raising or a successor.</summary>
    public int? FallenDay { get; set; }
    public string Cause { get; set; } = "";

    /// <summary>Died where nobody could carry the body home: no raising.</summary>
    public bool Lost { get; set; }

    /// <summary>Turned out at the moot rather than dead.</summary>
    public bool Ousted { get; set; }

    /// <summary>The day the mayor in office took it up.</summary>
    public int Since { get; set; }
    public int Raised { get; set; }
    public List<PastMayor> Past { get; } = [];
}

/// <summary>
/// The mayor is a person in the town, not just a sheet: they have hit points, spend their days in the hall
/// (safe, nothing gained), in the streets (Mandate and heroes' loyalty, but within an assassin's or a breach's
/// reach) or riding out with a company (an edge on its checks, XP, and its dangers). Wounds heal by the day.
/// A dead mayor can be raised at the temple, or the council names a successor from rolled candidates; in
/// Hardcore the campaign ends. Its rolls use their own dice (seeded from seed, tick and what's rolled for),
/// never <see cref="Town.Rng"/>, so the mayor's life never shifts the main stream.
/// </summary>
public static class Mayoralty
{
    private static MayoraltyRules Rules(Town town) => town.Content.Mayoralty;

    public static StanceDef Stance(Town town) =>
        Rules(town).Stance(town.Mayoralty.Stance) ?? Rules(town).Stance(Rules(town).Start) ?? Rules(town).Stances.FirstOrDefault() ?? new StanceDef { Id = "keep" };

    public static bool Dead(Town town) => town.Mayoralty.FallenDay != null;

    /// <summary>Out with a company, and not in town.</summary>
    public static bool Away(Town town) => town.Mayoralty.PartyId != null;

    /// <summary>Walking the streets, within reach of knives and breaches.</summary>
    public static bool Exposed(Town town) => !Dead(town) && !Away(town) && Stance(town).Id == "town";

    public static int MaxHp(Town town)
    {
        var r = Rules(town).Hp;
        int con = Rng.Modifier(Stories.Scores(town).Con);
        int lv = town.Stories.Mayor.Level;
        return Math.Max(1, r.Base + con) + (lv - 1) * Math.Max(1, r.PerLevel + con);
    }

    public static int Hp(Town town) => Dead(town) ? 0 : Math.Max(0, MaxHp(town) - town.Mayoralty.Wounds);

    public static void Init(Town town, bool hardcore)
    {
        town.Mayoralty.Hardcore = hardcore;
        town.Mayoralty.Stance = Rules(town).Start;
        town.Mayoralty.Since = town.Day;
    }

    // What the stance does ----------------------------------------------------------------------

    /// <summary>Mandate a day from the mayor being seen about town.</summary>
    public static double Mandate(Town town) => Exposed(town) ? Stance(town).Mandate : Dead(town) ? -Rules(town).Leaderless : 0;

    public static IEnumerable<(string Why, double Points)> LoyaltyFactors(Town town)
    {
        if (Exposed(town) && Stance(town).Loyalty != 0) yield return ("the mayor walks among us", Stance(town).Loyalty);
        if (Away(town) && Stance(town).Loyalty != 0) yield return ("the mayor rides with us", Stance(town).Loyalty);
    }

    public static bool SetStance(Town town, string id)
    {
        if (Rules(town).Stance(id) == null || Dead(town)) return false;
        if (town.Mayoralty.Stance == id) return true;
        town.Mayoralty.Stance = id;
        var m = town.Stories.Mayor;
        string text = id switch
        {
            "keep" => $"Mayor {m.Name} keeps to the hall, out of harm's way.",
            "town" => $"Mayor {m.Name} goes out among the townsfolk.",
            _ => $"Mayor {m.Name} buckles on a sword and will ride with the next company to march.",
        };
        town.Log(LedgerCategory.Story, Importance.Routine, text);
        return true;
    }

    // Danger ---------------------------------------------------------------------------------

    /// <summary>The mayor's own dice for one roll, apart from the town's stream.</summary>
    private static Rng Dice(Town town, int salt) =>
        new(unchecked(town.Seed * 6007 + town.Tick * 92821 + salt * 389 + town.Mayoralty.Past.Count * 53 + town.Mayoralty.Wounds * 7 + 29));

    private static int Roll(Town town, Rng dice, List<double> share)
    {
        double lo = share.Count > 0 ? share[0] : 0.5, hi = share.Count > 1 ? share[1] : lo;
        return Math.Max(1, (int)Math.Ceiling(MaxHp(town) * (lo + dice.Next() * (hi - lo))));
    }

    /// <summary>Hurt the mayor; at 0 hit points they die. Returns whether they died.</summary>
    public static bool Wound(Town town, int hp, string cause, bool lost = false)
    {
        if (Dead(town) || hp <= 0) return false;
        town.Mayoralty.Wounds += hp;
        var m = town.Stories.Mayor;
        if (town.Mayoralty.Wounds < MaxHp(town))
        {
            town.Log(LedgerCategory.Story, Importance.Notable, $"Mayor {m.Name} is hurt ({cause}): {Hp(town)} of {MaxHp(town)} hit points left.");
            return false;
        }
        Die(town, cause, lost);
        return true;
    }

    private static void Die(Town town, string cause, bool lost)
    {
        var s = town.Mayoralty;
        var m = town.Stories.Mayor;
        s.FallenDay = town.Day;
        s.Cause = cause;
        s.Lost = lost;
        s.PartyId = null;
        if (s.Hardcore)
        {
            town.Log(LedgerCategory.Story, Importance.Major, $"Mayor {m.Name} is dead ({cause}). The charter was sworn to them alone, and it dies with them.");
            Saga.Fall(town, $"Mayor {m.Name} died ({cause}), and the charter of {town.Name} died with them.");
            return;
        }
        string after = lost ? "There is no body to raise." : Shrines.Temple(town) != null ? "The priests could raise them, for a price, or the council names another." : "The council must name another.";
        town.Log(LedgerCategory.Story, Importance.Major, $"Mayor {m.Name} is dead ({cause}). {after}");
    }

    /// <summary>
    /// The town turns the mayor out at the moot. Like a death, but no raising: the council names a successor from the
    /// rolled candidates, and in Hardcore the campaign is lost.
    /// </summary>
    public static void Unseat(Town town, string cause)
    {
        var s = town.Mayoralty;
        var m = town.Stories.Mayor;
        if (Dead(town)) return;
        s.FallenDay = town.Day;
        s.Cause = cause;
        s.Lost = true;
        s.Ousted = true;
        s.PartyId = null;
        if (s.Hardcore)
        {
            town.Log(LedgerCategory.Council, Importance.Major, $"Mayor {m.Name} {cause}. The charter was sworn to them alone, and the town has turned them out.");
            Saga.Fall(town, $"Mayor {m.Name} {cause}, and {town.Name} turned them out.");
            return;
        }
        town.Log(LedgerCategory.Council, Importance.Major, $"Mayor {m.Name} {cause}. The town must name another.");
    }

    /// <summary>An assassin's blow that would have fallen on someone else may be for the mayor, walking the streets.</summary>
    internal static bool Marked(Town town) => Exposed(town) && Dice(town, 1).Chance(Rules(town).Assassin * (1 - Regalia.Ward(town)));

    /// <summary>The assassin's knife reaches the mayor.</summary>
    internal static void Knifed(Town town, Culprit by)
    {
        var dice = Dice(town, 2);
        Wound(town, Roll(town, dice, Rules(town).AssassinWound), $"a knife from {by.Name}");
    }

    /// <summary>Fighting spills into the streets: a mayor out in them may be caught in it.</summary>
    internal static void Breached(Town town, int breaches, string foe)
    {
        if (!Exposed(town)) return;
        var dice = Dice(town, 3);
        for (int i = 0; i < breaches && !Dead(town); i++)
            if (dice.Chance(Rules(town).Breach)) Wound(town, Roll(town, dice, Rules(town).BreachWound), $"fighting {foe} in the streets");
    }

    // Riding out -----------------------------------------------------------------------------

    /// <summary>Join the newest company to march today, if the mayor means to ride.</summary>
    private static void Join(Town town)
    {
        var s = town.Mayoralty;
        if (Dead(town) || Away(town) || Stance(town).Id != "ride") return;
        var party = town.Parties
            .Where(p => !p.Returned && p.Result == null && p.DepartTick >= town.Tick - SimTime.HoursPerDay)
            .OrderBy(p => p.Id)
            .LastOrDefault();
        if (party == null) return;
        s.PartyId = party.Id;
        party.Edge += Stance(town).Edge;
        town.Log(LedgerCategory.Quest, Importance.Notable, $"Mayor {town.Stories.Mayor.Name} rides out with the {party.Name}.", party.Members.ToList());
    }

    /// <summary>The company is home: the mayor with it, or what the road left of them.</summary>
    private static void ComeHome(Town town)
    {
        var s = town.Mayoralty;
        if (s.PartyId is not int id) return;
        var party = town.Parties[id - 1];
        if (!party.Returned) return;
        s.PartyId = null;
        var m = town.Stories.Mayor;
        var r = Rules(town).Ride;
        var result = party.Result;
        if (result == null)
        {
            town.Log(LedgerCategory.Quest, Importance.Routine, $"Mayor {m.Name} comes home with the {party.Name}.");
            return;
        }
        if (result.Outcome == ExpeditionOutcome.Wiped)
        {
            Die(town, $"fell with the {party.Name} at {Quests.Site(town, party.SiteId)?.Def.Name ?? party.SiteId}", true);
            return;
        }
        var dice = Dice(town, 4 + id);
        foreach (int _ in result.Dead)
            if (dice.Chance(r.PerDead) && Wound(town, Roll(town, dice, r.Wound), $"riding with the {party.Name}", false)) return;
        Stories.GainXp(town, result.Outcome == ExpeditionOutcome.Cleared ? r.XpCleared : r.Xp);
        town.Log(LedgerCategory.Quest, Importance.Notable, $"Mayor {m.Name} comes home with the {party.Name}, {Hp(town)} of {MaxHp(town)} hit points, with a tale to tell in the hall.");
    }

    // Death and after ------------------------------------------------------------------------

    public static double RaiseCost(Town town) => Rules(town).Raise.Cost + Rules(town).Raise.PerLevel * town.Stories.Mayor.Level;

    /// <summary>Why the mayor can't be raised now, or null.</summary>
    public static string? RaiseRefused(Town town)
    {
        if (!Dead(town) || town.Mayoralty.Hardcore) return "not dead";
        if (town.Mayoralty.Ousted) return "voted out";
        if (town.Mayoralty.Lost) return "no body";
        if (Shrines.Temple(town) == null) return "needs a temple";
        if (town.Treasury < RaiseCost(town)) return "not enough gold";
        return null;
    }

    public static bool RaiseMayor(Town town)
    {
        if (RaiseRefused(town) != null) return false;
        var s = town.Mayoralty;
        double cost = RaiseCost(town);
        town.Book(TreasuryAccount.Temple, -cost);
        s.FallenDay = null;
        s.Cause = "";
        s.Wounds = MaxHp(town) - 1;
        s.Raised++;
        var temple = Shrines.Temple(town)!;
        var god = Faith.Of(town, temple);
        town.Log(LedgerCategory.Faith, Importance.Major,
            $"The priests of {(god != null ? Faith.Prose(god) : town.Content.Shrines.Deity)} lay Mayor {town.Stories.Mayor.Name} on the altar and call them back ({Js.Str(cost)} gold from the treasury). The mayor breathes again.");
        return true;
    }

    /// <summary>The mayors the council could name in the dead one's place.</summary>
    public static List<MayorRoll> Candidates(Town town)
    {
        var list = new List<MayorRoll>();
        if (!Dead(town)) return list;
        for (int i = 0; i < Rules(town).Candidates; i++)
            list.Add(Stories.RollMayor(town.Content, unchecked(town.Seed * 131 + (town.Mayoralty.Past.Count + 1) * 7919 + i * 17 + 3)));
        return list;
    }

    public static bool NameSuccessor(Town town, int index)
    {
        var options = Candidates(town);
        if (index < 0 || index >= options.Count || town.Mayoralty.Hardcore) return false;
        var s = town.Mayoralty;
        var m = town.Stories.Mayor;
        s.Past.Add(new PastMayor { Name = m.Name, Background = m.Background, Level = m.Level, From = s.Since, To = s.FallenDay ?? town.Day, Cause = s.Cause });
        int xp = (int)(m.Xp * Rules(town).SuccessorXp);
        string old = m.Name;
        // The new mayor takes up the town's sheet: what the town remembers stays, the person is new.
        m.Level = 1;
        m.Xp = 0;
        m.Inspiration = 0;
        m.Advances.Clear();
        m.Successes = 0;
        m.Failures = 0;
        m.Chosen = false;
        Stories.Adopt(town, options[index]);
        s.FallenDay = null;
        s.Cause = "";
        s.Lost = false;
        bool ousted = s.Ousted;
        s.Ousted = false;
        s.Wounds = 0;
        s.Since = town.Day;
        s.Stance = Rules(town).Start;
        town.Log(LedgerCategory.Council, Importance.Major, $"{(ousted ? "The town elects" : "The council names")} {m.Name} mayor of {town.Name} in {old}'s place.");
        if (xp > 0) Stories.GainXp(town, xp);
        return true;
    }

    // The day --------------------------------------------------------------------------------

    public static void Daily(Town town, bool auto)
    {
        var s = town.Mayoralty;
        if (Dead(town))
        {
            if (s.Hardcore) return;
            if (auto || town.Day - s.FallenDay >= Rules(town).DecideDays)
            {
                if (RaiseRefused(town) == null && (!auto || town.Treasury >= RaiseCost(town) + Rules(town).Raise.Reserve)) RaiseMayor(town);
                else NameSuccessor(town, 0);
            }
            return;
        }
        ComeHome(town);
        if (Dead(town)) return;
        Join(town);
        if (s.Wounds > 0 && !Away(town))
            s.Wounds = Shrines.Temple(town) != null ? 0 : Math.Max(0, s.Wounds - Math.Max(1, (int)Math.Ceiling(MaxHp(town) * Rules(town).Heal)));
    }
}
