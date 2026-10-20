namespace CrownAndCompass.Sim.Systems;

/// <summary>What a minor power does for the town once allied (content/game/powers.json).</summary>
public sealed record PowerBoonDef
{
    /// <summary>Strength it adds at the walls, in the units of the town's defence.</summary>
    public double Defence { get; init; }

    /// <summary>Gold it pays into the treasury a day.</summary>
    public double Income { get; init; }

    /// <summary>Power the town's worst enemy loses a day to its hex.</summary>
    public double Hex { get; init; }

    /// <summary>Chance an enemy's blow (not a raid) is stopped by its warning.</summary>
    public double Warn { get; init; }

    /// <summary>Every this many days it kills a captain of the town's worst enemy, which loses this share of its power.</summary>
    public int Contract { get; init; }
    public double Cut { get; init; }
    public string Text { get; init; } = "";
}

/// <summary>What a minor power does to the town once an enemy has bought it.</summary>
public sealed record PowerBaneDef
{
    /// <summary>Power it adds to its patron a day, and gold its people skim from the treasury a day.</summary>
    public double Arm { get; init; }
    public double Skim { get; init; }

    /// <summary>Warriors it adds to its patron's host at the walls.</summary>
    public double Host { get; init; }

    /// <summary>The kind of blow it strikes (<see cref="Blows"/>), and how often.</summary>
    public string Strike { get; init; } = "";
    public int StrikeEvery { get; init; }
    public string Text { get; init; } = "";
}

/// <summary>One minor power: a strong outside faction with a leader, a goal and a price.</summary>
public sealed record PowerDef
{
    public required string Id { get; init; }

    /// <summary>The library faction it is (content/original/factions.json), for its name and lore.</summary>
    public string Ref { get; init; } = "";
    public string Face { get; init; } = "help";
    public string Title { get; init; } = "";
    public string Leader { get; init; } = "";
    public string Goal { get; init; } = "";
    public string Seat { get; init; } = "";

    /// <summary>Who brings word of what it does: news through someone, never out of nowhere.</summary>
    public string Witness { get; init; } = "";

    /// <summary>Dark powers sour the good ones on the town that keeps them.</summary>
    public bool Evil { get; init; }

    /// <summary>"any": enemies can buy it; "none": it never serves the town's enemies.</summary>
    public string Serves { get; init; } = "any";

    /// <summary>Menace kinds it will not serve.</summary>
    public IReadOnlyList<string> Shuns { get; init; } = [];
    public double Base { get; init; }

    /// <summary>Weights on the town's signals (<see cref="Powers.Signal"/>).</summary>
    public IReadOnlyDictionary<string, double> Likes { get; init; } = new Dictionary<string, double>();
    public double AllyAt { get; init; } = 30;
    public double Price { get; init; }
    public double Tithe { get; init; }
    public double Gift { get; init; } = 50;
    public PowerBoonDef Ally { get; init; } = new();
    public PowerBaneDef Enemy { get; init; } = new();
    public string ContractText { get; init; } = "";
}

public sealed record PowerTexts
{
    public string Courted { get; init; } = "";
    public string Refused { get; init; } = "";
    public string Turned { get; init; } = "";
    public string Allied { get; init; } = "";
    public string Left { get; init; } = "";
    public string Broke { get; init; } = "";
    public string Freed { get; init; } = "";
    public string WonBack { get; init; } = "";
    public string Hexed { get; init; } = "";
}

public sealed record PowerRules
{
    public int CourtFrom { get; init; } = 60;
    public int CourtEvery { get; init; } = 45;
    public int NoticeDays { get; init; } = 8;
    public double OfferPerPower { get; init; } = 0.14;
    public IReadOnlyDictionary<string, double> Cunning { get; init; } = new Dictionary<string, double>();
    public double AllyResist { get; init; } = 25;
    public int PerEnemy { get; init; } = 2;
    public double Drift { get; init; } = 2;
    public double FavourFade { get; init; } = 0.25;
    public double GiftFavour { get; init; } = 12;
    public double GiftFalloff { get; init; } = 0.35;
    public int GiftMemory { get; init; } = 20;
    public int LapseDays { get; init; } = 5;
    public double LeaveBelow { get; init; } = 5;
    public double RivalHit { get; init; } = 20;
    public double WinBack { get; init; } = 40;
    public double FreedFavour { get; init; } = -15;
    public string WarnText { get; init; } = "";
    public IReadOnlyList<PowerDef> Powers { get; init; } = [];
    public PowerTexts Text { get; init; } = new();
}

public enum PowerSide
{
    Neutral,
    Town,
    Enemy,
}

/// <summary>Where a minor power stands with the town, and whose side it is on.</summary>
public sealed class PowerState
{
    public required string Id { get; init; }

    /// <summary>What it thinks of the town, -100 to 100: its likes, then the favour gifts bought.</summary>
    public double Standing { get; set; }
    public double Favour { get; set; }

    /// <summary>Gifts sent lately, each worth less than the last; one fades every giftMemory days.</summary>
    public double RecentGifts { get; set; }
    public PowerSide Side { get; set; }

    /// <summary>The enemy (menace id) it serves, or that is courting it.</summary>
    public int? MenaceId { get; set; }
    public int? CourtedBy { get; set; }
    public double Offer { get; set; }
    public int DecideDay { get; set; }
    public int SideDay { get; set; }
    public int NextActDay { get; set; }
    public int Unpaid { get; set; }

    /// <summary>The last thing heard of it, for its card.</summary>
    public string LastHeard { get; set; } = "";
}

/// <summary>
/// Minor powers: strong outside factions (a cartel, assassins, a spy ring, a league of lords, a wizards'
/// conclave, a dragon's court, a hag coven) that are neither town nor enemy. Each has its own leader and goal,
/// likes and dislikes what the town does, and has a price. The town courts them with gifts and buys an
/// alliance; the clever enemies of the wilds court them too, and a power the town lets them buy arms its new
/// patron, strikes at the town and joins its host at the walls. Every change of side reaches the town as word
/// from a witness. Numbers in content/game/powers.json.
/// </summary>
public static class Powers
{
    private static PowerRules Rules(Town town) => town.Content.Powers;

    public static PowerDef Def(Town town, string id) => Rules(town).Powers.First(p => p.Id == id);

    public static PowerState? Find(Town town, string id) => town.Powers.FirstOrDefault(p => p.Id == id);

    /// <summary>Its name as the Ledger says it mid-sentence: "the Black Ledger Company".</summary>
    public static string Name(Town town, PowerDef d)
    {
        string name = town.Content.Library.Faction(d.Ref)?.Name ?? d.Id;
        return name.StartsWith("The ", StringComparison.Ordinal) ? "the" + name[3..] : name;
    }

    public static string Name(Town town, PowerState p) => Name(town, Def(town, p.Id));

    public static Menace? Patron(Town town, PowerState p) => p.Side == PowerSide.Enemy && p.MenaceId is int id ? town.Saga.Menaces.FirstOrDefault(m => m.Id == id) : null;

    public static Menace? Suitor(Town town, PowerState p) => p.CourtedBy is int id ? town.Saga.Menaces.FirstOrDefault(m => m.Id == id) : null;

    public static IEnumerable<PowerState> Allies(Town town) => town.Powers.Where(p => p.Side == PowerSide.Town);

    /// <summary>The powers an enemy has bought.</summary>
    public static IEnumerable<PowerState> Backing(Town town, Menace m) => town.Powers.Where(p => p.Side == PowerSide.Enemy && p.MenaceId == m.Id);

    public static void Init(Town town)
    {
        if (town.Powers.Count > 0) return;
        foreach (var d in Rules(town).Powers)
            town.Powers.Add(new PowerState { Id = d.Id, Standing = Math.Round(d.Base), NextActDay = d.Enemy.StrikeEvery });
    }

    // Standing -----------------------------------------------------------------------------------

    /// <summary>What a power can see of the town, mostly 0 to 1.</summary>
    public static double Signal(Town town, string key) => key switch
    {
        "wealth" => Math.Clamp(town.Treasury / 3000, 0, 1),
        "routes" => Math.Clamp(Trade.Open(town).Count() / 3.0, 0, 1),
        "walls" => Math.Clamp(Threat.Fortification(town) - 1, 0, 1),
        "slain" => Math.Clamp(town.Saga.Menaces.Count(m => !m.Alive && m.Origin != Menaces.FromWarband) / 4.0, 0, 1),
        "infamy" => Math.Clamp(town.Heroes.Sum(h => h.Hero!.Infamy) / 12.0, 0, 1),
        "dread" => Math.Clamp(town.Saga.Dread / 100, 0, 1),
        "growth" => Math.Clamp(town.Population / 400.0, 0, 1),
        "heroes" => Math.Clamp(town.Heroes.Count / 8.0, 0, 1),
        "dark" => town.Powers.Count(p => p.Side == PowerSide.Town && Def(town, p.Id).Evil),
        _ => 0,
    };

    /// <summary>Where its standing settles: its base, what it likes and dislikes, its rivals' alliances, and favour bought.</summary>
    public static double Target(Town town, PowerState p)
    {
        var d = Def(town, p.Id);
        double t = d.Base + d.Likes.OrderBy(kv => kv.Key, StringComparer.Ordinal).Sum(kv => kv.Value * Signal(town, kv.Key));
        t -= Rules(town).RivalHit * Rivals(town, d).Count(r => Find(town, r)?.Side == PowerSide.Town);
        return Math.Clamp(t + p.Favour, -100, 100);
    }

    private static IEnumerable<string> Rivals(Town town, PowerDef d) =>
        (town.Content.Library.Faction(d.Ref)?.Rivals ?? []).Where(r => r != d.Id);

    // The day ------------------------------------------------------------------------------------

    public static void Daily(Town town, bool autoMayor)
    {
        if (!town.Saga.Enabled || town.Outcome != null) return;
        Init(town);
        var r = Rules(town);
        foreach (var p in town.Powers)
        {
            var d = Def(town, p.Id);
            p.Favour -= Math.Sign(p.Favour) * Math.Min(Math.Abs(p.Favour), r.FavourFade);
            if (town.Day % Math.Max(1, r.GiftMemory) == 0) p.RecentGifts = Math.Max(0, p.RecentGifts - 1);
            double target = Target(town, p);
            p.Standing += Math.Clamp(target - p.Standing, -r.Drift, r.Drift);
            switch (p.Side)
            {
                case PowerSide.Town: Serve(town, p, d); break;
                case PowerSide.Enemy: Bane(town, p, d); break;
            }
            if (p.CourtedBy != null && town.Day >= p.DecideDay) Decide(town, p, d);
        }
        Court(town);
        if (autoMayor) Steward(town);
    }

    private static string Fill(Town town, string text, PowerState p, PowerDef d, Menace? enemy = null) => Saga.Sentences(text
        .Replace("{witness}", d.Witness)
        .Replace("{name}", Name(town, d))
        .Replace("{leader}", d.Leader)
        .Replace("{title}", d.Title)
        .Replace("{seat}", d.Seat)
        .Replace("{town}", town.Name)
        .Replace("{enemy}", enemy?.Name ?? "")
        .Replace("{enemyLeader}", enemy?.Leader ?? "")
        .Replace("{days}", Math.Max(0, p.DecideDay - town.Day).ToString()));

    private static void News(Town town, PowerState p, string text, Importance importance = Importance.Major)
    {
        p.LastHeard = text;
        town.Log(LedgerCategory.Faction, importance, text);
    }

    /// <summary>The worst enemy abroad: what the town's allies strike at.</summary>
    private static Menace? Worst(Town town) =>
        Menaces.Alive(town).Where(m => m.Origin != Menaces.FromWarband && m.Stage >= MenaceStage.Rising)
            .OrderByDescending(m => m.Power).ThenBy(m => m.Id).FirstOrDefault();

    private static void Serve(Town town, PowerState p, PowerDef d)
    {
        var r = Rules(town);
        if (town.Treasury >= d.Tithe)
        {
            town.Book(TreasuryAccount.Powers, -d.Tithe);
            p.Unpaid = 0;
        }
        else p.Unpaid++;
        if (p.Unpaid >= r.LapseDays || p.Standing < r.LeaveBelow)
        {
            Leave(town, p, d, p.Unpaid >= r.LapseDays ? "its tithe has gone unpaid" : "it no longer likes what the town has become");
            return;
        }
        if (d.Ally.Income > 0) town.Book(TreasuryAccount.Powers, d.Ally.Income);
        var worst = Worst(town);
        if (worst == null) return;
        if (d.Ally.Hex > 0) worst.Power = Math.Max(0, worst.Power - d.Ally.Hex);
        if (d.Ally.Contract > 0 && town.Day - p.SideDay > 0 && (town.Day - p.SideDay) % d.Ally.Contract == 0)
        {
            worst.Power *= 1 - d.Ally.Cut;
            News(town, p, Fill(town, d.ContractText, p, d, worst));
        }
    }

    private static void Leave(Town town, PowerState p, PowerDef d, string why)
    {
        p.Side = PowerSide.Neutral;
        p.Favour -= Rules(town).RivalHit;
        p.Unpaid = 0;
        News(town, p, Fill(town, Rules(town).Text.Left.Replace("{why}", why), p, d));
    }

    private static void Bane(Town town, PowerState p, PowerDef d)
    {
        var r = Rules(town);
        var m = Patron(town, p);
        if (m == null || !m.Alive)
        {
            p.Side = PowerSide.Neutral;
            p.MenaceId = null;
            p.Favour = r.FreedFavour;
            News(town, p, Fill(town, r.Text.Freed, p, d, m));
            return;
        }
        if (p.Standing >= r.WinBack)
        {
            p.Side = PowerSide.Neutral;
            p.MenaceId = null;
            News(town, p, Fill(town, r.Text.WonBack, p, d, m));
            return;
        }
        m.Power += d.Enemy.Arm;
        if (d.Enemy.Skim > 0 && town.Treasury > 0) town.Book(TreasuryAccount.Powers, -Math.Min(town.Treasury, d.Enemy.Skim));
        if (d.Enemy.Strike != "" && d.Enemy.StrikeEvery > 0 && town.Day >= p.NextActDay)
        {
            p.NextActDay = town.Day + d.Enemy.StrikeEvery;
            Blows.Strike(town, d.Enemy.Strike, new Culprit(Name(town, d), d.Leader, d.Seat), 20 + d.Enemy.Host * 0.25, 1);
        }
    }

    // Courting -----------------------------------------------------------------------------------

    /// <summary>What an enemy's envoys can offer a power, in standing: its power, times its wits.</summary>
    public static double OfferOf(Town town, Menace m) =>
        Js.Round(m.Power * Rules(town).Cunning.GetValueOrDefault(m.Cunning, 1) * Rules(town).OfferPerPower);

    /// <summary>How far a power would hold out against an offer: its standing, and more if it is already the town's ally.</summary>
    public static double Resolve(Town town, PowerState p) => p.Standing + (p.Side == PowerSide.Town ? Rules(town).AllyResist : 0);

    /// <summary>Whether this enemy could buy this power at all.</summary>
    public static bool Buyable(Town town, PowerState p, Menace m)
    {
        var d = Def(town, p.Id);
        return d.Serves == "any" && !d.Shuns.Contains(m.Kind) && p.Side != PowerSide.Enemy && p.CourtedBy == null;
    }

    /// <summary>Every courtEvery days the strongest risen enemy with the wits for it courts the power it could most easily buy.</summary>
    private static void Court(Town town)
    {
        var r = Rules(town);
        if (town.Day < r.CourtFrom || (town.Day - r.CourtFrom) % Math.Max(1, r.CourtEvery) != 0) return;
        var suitors = Menaces.Alive(town)
            .Where(m => m.Origin != Menaces.FromWarband && m.ArrivalTick == null && (m.Stage >= MenaceStage.Rising || m.BigBad) && r.Cunning.GetValueOrDefault(m.Cunning, 1) > 0)
            .Where(m => !(town.Saga.YokeId == m.Id && !town.Saga.Defied))
            .Where(m => town.Powers.Count(p => (p.Side == PowerSide.Enemy && p.MenaceId == m.Id) || p.CourtedBy == m.Id) < r.PerEnemy)
            .OrderByDescending(m => OfferOf(town, m)).ThenBy(m => m.Id);
        foreach (var m in suitors)
        {
            double offer = OfferOf(town, m);
            var p = town.Powers.Where(p => Buyable(town, p, m) && offer > Resolve(town, p))
                .OrderBy(p => Resolve(town, p)).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
            if (p == null) continue;
            p.CourtedBy = m.Id;
            p.Offer = offer;
            p.DecideDay = town.Day + r.NoticeDays;
            News(town, p, Fill(town, r.Text.Courted, p, Def(town, p.Id), m));
            return;
        }
    }

    /// <summary>The day it answers the enemy's envoys: the town's standing against the enemy's offer.</summary>
    private static void Decide(Town town, PowerState p, PowerDef d)
    {
        var r = Rules(town);
        var m = Suitor(town, p);
        p.CourtedBy = null;
        if (m == null || !m.Alive || Resolve(town, p) >= p.Offer)
        {
            if (m != null) News(town, p, Fill(town, r.Text.Refused, p, d, m));
            return;
        }
        bool wasAlly = p.Side == PowerSide.Town;
        p.Side = PowerSide.Enemy;
        p.MenaceId = m.Id;
        p.SideDay = town.Day;
        p.NextActDay = town.Day + Math.Max(5, d.Enemy.StrikeEvery / 2);
        p.Favour = Math.Min(p.Favour, 0);
        News(town, p, Fill(town, r.Text.Turned.Replace("{act}", d.Enemy.Text), p, d, m) + (wasAlly ? " It was the town's ally until today." : ""));
    }

    // The player's levers ------------------------------------------------------------------------

    /// <summary>Gold a gift to this power costs.</summary>
    public static double GiftCost(Town town, string id) => Def(town, id).Gift;

    /// <summary>Favour the next gift would buy: each one lately is worth less.</summary>
    public static double GiftWorth(Town town, PowerState p) => Js.Round(Rules(town).GiftFavour / (1 + Rules(town).GiftFalloff * p.RecentGifts));

    /// <summary>Send a power gifts: favour that lifts its standing now and fades slowly.</summary>
    public static bool Court(Town town, string id)
    {
        var p = Find(town, id);
        if (p == null || town.Treasury < GiftCost(town, id)) return false;
        double worth = GiftWorth(town, p);
        town.Book(TreasuryAccount.Powers, -GiftCost(town, id));
        p.Favour += worth;
        p.Standing = Math.Clamp(p.Standing + worth, -100, 100);
        p.RecentGifts++;
        town.Log(LedgerCategory.Faction, Importance.Routine, $"The council sends {Name(town, Def(town, id))} gifts worth {Js.Str(GiftCost(town, id))} gold.");
        return true;
    }

    /// <summary>Why the town can't ally with a power now, or null if it can.</summary>
    public static string? AllyBlocked(Town town, string id)
    {
        var p = Find(town, id);
        if (p == null) return "unknown";
        var d = Def(town, id);
        if (p.Side == PowerSide.Town) return "already allied";
        if (p.Side == PowerSide.Enemy) return $"it serves {Patron(town, p)?.Name ?? "an enemy"}";
        if (p.Standing < d.AllyAt) return $"it needs standing {Js.Str(d.AllyAt)}";
        if (town.Treasury < d.Price) return $"its price is {Js.Str(d.Price)} gold";
        return null;
    }

    /// <summary>Pay a power's price: it sides with the town for its tithe, and its rivals take it badly.</summary>
    public static bool Ally(Town town, string id)
    {
        if (AllyBlocked(town, id) != null) return false;
        var p = Find(town, id)!;
        var d = Def(town, id);
        town.Book(TreasuryAccount.Powers, -d.Price);
        p.Side = PowerSide.Town;
        p.SideDay = town.Day;
        p.Unpaid = 0;
        News(town, p, Fill(town, Rules(town).Text.Allied.Replace("{boon}", d.Ally.Text), p, d));
        return true;
    }

    /// <summary>End an alliance: no more tithe, and it remembers.</summary>
    public static bool Break(Town town, string id)
    {
        var p = Find(town, id);
        if (p == null || p.Side != PowerSide.Town) return false;
        var d = Def(town, id);
        p.Side = PowerSide.Neutral;
        p.Favour -= Rules(town).RivalHit;
        News(town, p, Fill(town, Rules(town).Text.Broke, p, d), Importance.Notable);
        return true;
    }

    /// <summary>What it does for the town as an ally, and against it for an enemy, as its card says it.</summary>
    public static string BoonText(Town town, PowerDef d) => d.Ally.Text;

    public static string BaneText(Town town, PowerState p) =>
        Def(town, p.Id).Enemy.Text.Replace("{enemy}", Patron(town, p)?.Name ?? Suitor(town, p)?.Name ?? "the enemy that buys it");

    // What they bring ----------------------------------------------------------------------------

    /// <summary>The town's allies' strength at the walls.</summary>
    public static double Defense(Town town) => Allies(town).Sum(p => Def(town, p.Id).Ally.Defence);

    /// <summary>Warriors the powers an enemy has bought bring to its host at the walls, named for the Ledger.</summary>
    public static double JoinHost(Town town, Menace m, List<string> lines)
    {
        double sent = 0;
        foreach (var p in Backing(town, m).OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            var d = Def(town, p.Id);
            if (d.Enemy.Host <= 0) continue;
            sent += d.Enemy.Host;
            lines.Add(Saga.Sentences($"{Name(town, d)} march{(Name(town, d).EndsWith('s') ? "" : "es")} with {m.Name}, some {Js.Str(d.Enemy.Host)} strong."));
        }
        return sent;
    }

    /// <summary>An enemy's blow the town's spies saw coming. Uses the dice only while such an ally serves.</summary>
    public static bool Forewarned(Town town, Culprit by)
    {
        foreach (var p in Allies(town).OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            var d = Def(town, p.Id);
            if (d.Ally.Warn <= 0 || !town.Rng.Chance(d.Ally.Warn)) continue;
            News(town, p, Saga.Sentences(Rules(town).WarnText.Replace("{witness}", d.Witness).Replace("{name}", Name(town, d)).Replace("{by}", by.Name)), Importance.Notable);
            return true;
        }
        return false;
    }

    // The stand-in mayor -------------------------------------------------------------------------

    /// <summary>Outbid an enemy when it is cheap enough to, and buy one good ally once the treasury can carry its tithe.</summary>
    private static void Steward(Town town)
    {
        foreach (var p in town.Powers.Where(p => p.CourtedBy != null).OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            var cost = GiftCost(town, p.Id);
            for (int i = 0; i < 4 && Resolve(town, p) < p.Offer && town.Treasury >= cost * 3 + 1000; i++) Court(town, p.Id);
        }
        if (Allies(town).Count() >= 2) return;
        var pick = town.Powers
            .Where(p => AllyBlocked(town, p.Id) == null && !Def(town, p.Id).Evil)
            .Where(p => town.Treasury >= Def(town, p.Id).Price + Def(town, p.Id).Tithe * 150 + 2000)
            .OrderByDescending(p => Def(town, p.Id).Ally.Defence).ThenBy(p => p.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (pick != null) Ally(town, pick.Id);
    }
}
