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

    /// <summary>The favours it asks of the town (<see cref="PowerFavourRules"/>): "clear", "slay", "route".</summary>
    public IReadOnlyList<string> Asks { get; init; } = [];

    /// <summary>Strange prices its envoys may ask instead of gold, and the chance they do.</summary>
    public IReadOnlyList<StrangePriceDef> Strange { get; init; } = [];
    public double StrangeChance { get; init; }
}

/// <summary>
/// A price that isn't gold: "name" (a townsperson walks off to it and is never seen again), "harvest" (the farms
/// yield <see cref="Amount"/> less for <see cref="Days"/>), "voice" (the mayor loses <see cref="Amount"/> Mandate).
/// </summary>
public sealed record StrangePriceDef
{
    public required string Id { get; init; }
    public string Kind { get; init; } = "";
    public double Amount { get; init; }
    public int Days { get; init; }

    /// <summary>The price as the envoy says it, and what the Ledger says when it is paid.</summary>
    public string Text { get; init; } = "";
    public string Paid { get; init; } = "";
}

/// <summary>
/// Envoys the powers send the town (content/game/powers.json "envoys"): an alliance at a discount when a power
/// likes the town nearly enough, a pact (it takes no enemy's gold for pactDays) when it is cordial, a demand for
/// tribute when it is hostile. One comes every <see cref="Every"/> days from <see cref="From"/>; each waits
/// <see cref="AnswerDays"/> for the town's answer.
/// </summary>
public sealed record PowerEnvoyRules
{
    public int From { get; init; } = 40;
    public int Every { get; init; } = 25;
    public int AnswerDays { get; init; } = 10;
    public int Rest { get; init; } = 60;
    public double AllianceWithin { get; init; } = 10;
    public double AllianceDiscount { get; init; } = 0.7;
    public double PactAt { get; init; } = -10;
    public int PactDays { get; init; } = 120;
    public double PactPrice { get; init; } = 1.5;
    public double PactFavour { get; init; } = 5;
    public double TributeBelow { get; init; } = -25;
    public double Tribute { get; init; } = 2;
    public double TributeFavour { get; init; } = 15;
    public double Spurned { get; init; } = 10;
    public double Declined { get; init; } = 3;
    public string Alliance { get; init; } = "";
    public string Pact { get; init; } = "";
    public string Demand { get; init; } = "";
    public string Accepted { get; init; } = "";
    public string Refused { get; init; } = "";
    public string Lapsed { get; init; } = "";
    public string PactEnds { get; init; } = "";
    public string Bandwagon { get; init; } = "";
}

/// <summary>
/// Favours a power's envoy asks of the town (powers.json "favours"): clear a known lair ("clear"), see the worst enemy
/// dead ("slay") or open one more trade route ("route"), within <see cref="Days"/>. Done, it buys <see cref="Done"/>
/// favour and gift x <see cref="Gold"/> gold; failed, it costs <see cref="Failed"/>.
/// </summary>
public sealed record PowerFavourRules
{
    public int Days { get; init; } = 90;
    public int MaxTier { get; init; } = 3;
    public double Done { get; init; } = 25;
    public double Gold { get; init; } = 1;
    public double Failed { get; init; } = 8;
    public IReadOnlyDictionary<string, string> Ask { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> Task { get; init; } = new Dictionary<string, string>();
    public string Kept { get; init; } = "";
    public string Broken { get; init; } = "";
}

/// <summary>Two powers with a quarrel (powers.json "feuds").</summary>
public sealed record FeudDef
{
    public required string A { get; init; }
    public required string B { get; init; }
    public string Why { get; init; } = "";
}

/// <summary>
/// Feuds between the powers: from <see cref="From"/>, every <see cref="Every"/> days a quarrel that has rested longest
/// flares for <see cref="Days"/>. While it burns, the side whose foe is the town's ally holds it against the town
/// (<see cref="Hit"/> standing) and, if it strikes at all, strikes the town every <see cref="StrikeEvery"/> days. The
/// town's ally calls on it for aid: gift x <see cref="Aid"/> gold ends the feud in the ally's favour.
/// </summary>
public sealed record PowerFeudRules
{
    public int From { get; init; } = 120;
    public int Every { get; init; } = 70;
    public int Days { get; init; } = 60;
    public int Rest { get; init; } = 200;
    public double Hit { get; init; } = 25;
    public int StrikeEvery { get; init; } = 30;
    public double Aid { get; init; } = 2;
    public double AidFavour { get; init; } = 20;
    public double Lost { get; init; } = 20;
    public double Spurned { get; init; } = 15;
    public IReadOnlyList<FeudDef> Pairs { get; init; } = [];
    public string Begins { get; init; } = "";
    public string CallToArms { get; init; } = "";
    public string Won { get; init; } = "";
    public string Ends { get; init; } = "";
    public string Strikes { get; init; } = "";
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

    /// <summary>How much more an enemy's envoys can offer for each power already marching with it.</summary>
    public double Bandwagon { get; init; }
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
    public PowerEnvoyRules Envoys { get; init; } = new();
    public PowerFavourRules Favours { get; init; } = new();
    public PowerFeudRules Feuds { get; init; } = new();
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

    /// <summary>Its envoy waiting on the town's answer: "alliance", "pact", "tribute" or "" for none.</summary>
    public string Envoy { get; set; } = "";

    /// <summary>The gold the envoy asks, or the strange price (its id) it asks instead.</summary>
    public double EnvoyGold { get; set; }
    public string EnvoyPrice { get; set; } = "";
    public int EnvoyUntil { get; set; }
    public int NextEnvoyDay { get; set; }

    /// <summary>The last day of its pact with the town: until then it takes no enemy's gold.</summary>
    public int PactUntil { get; set; }

    /// <summary>
    /// The favour it asked of the town ("clear", "slay", "route"), its target (a site id, a menace id, a route count),
    /// and the last day to do it once the town took it on (<see cref="TaskTaken"/>).
    /// </summary>
    public string Task { get; set; } = "";
    public string TaskSite { get; set; } = "";
    public int TaskMenace { get; set; }
    public int TaskCount { get; set; }
    public int TaskUntil { get; set; }
    public bool TaskTaken { get; set; }

    /// <summary>The power it is feuding with ("" for none), until when, and when its agents next strike the town over it.</summary>
    public string FeudWith { get; set; } = "";
    public int FeudUntil { get; set; }
    public int FeudStrikeDay { get; set; }
    public int NextFeudDay { get; set; }

    /// <summary>A strange price still being paid, and its last day ("harvest").</summary>
    public string Owed { get; set; } = "";
    public int OwedUntil { get; set; }
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
        if (p.FeudWith != "" && Find(town, p.FeudWith)?.Side == PowerSide.Town) t -= Rules(town).Feuds.Hit;
        return Math.Clamp(t + p.Favour, -100, 100);
    }

    private static IEnumerable<string> Rivals(Town town, PowerDef d) =>
        (town.Content.Library.Faction(d.Ref)?.Rivals ?? []).Where(r => r != d.Id);

    // The day ------------------------------------------------------------------------------------

    /// <summary>The stand-in mayor leaves the powers to the player: its gifts and tithes cost the finale more than they saved.</summary>
    public static void Daily(Town town)
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
            if (p.Envoy != "" && (p.Side == PowerSide.Enemy || (p.Side == PowerSide.Town && p.Envoy is "alliance" or "pact" or "tribute"))) Done(town, p);
            else if (p.Envoy != "" && town.Day >= p.EnvoyUntil) Lapse(town, p, d);
            if (p.TaskTaken) Task(town, p, d);
            if (p.FeudWith != "") Feud(town, p, d);
            if (p.PactUntil == town.Day) News(town, p, Fill(town, r.Envoys.PactEnds, p, d), Importance.Notable);
            if (p.Owed != "" && town.Day >= p.OwedUntil) p.Owed = "";
        }
        Court(town);
        StartFeud(town);
        SendEnvoy(town);
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
    /// <summary>Each power already marching with it makes the next one likelier to join: the bandwagon.</summary>
    public static double OfferOf(Town town, Menace m) =>
        Js.Round(m.Power * Rules(town).Cunning.GetValueOrDefault(m.Cunning, 1) * Rules(town).OfferPerPower * (1 + Rules(town).Bandwagon * Backing(town, m).Count()));

    /// <summary>How far a power would hold out against an offer: its standing, and more if it is already the town's ally.</summary>
    public static double Resolve(Town town, PowerState p) => p.Standing + (p.Side == PowerSide.Town ? Rules(town).AllyResist : 0);

    /// <summary>Whether this enemy could buy this power at all.</summary>
    public static bool Buyable(Town town, PowerState p, Menace m)
    {
        var d = Def(town, p.Id);
        return d.Serves == "any" && !d.Shuns.Contains(m.Kind) && p.Side != PowerSide.Enemy && p.CourtedBy == null && !Pacted(town, p);
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
            string news = Fill(town, r.Text.Courted, p, Def(town, p.Id), m);
            var backers = Backing(town, m).OrderBy(b => b.Id, StringComparer.Ordinal).Select(b => Name(town, b)).ToList();
            if (backers.Count > 0) news += " " + Fill(town, r.Envoys.Bandwagon.Replace("{backers}", Expedition.ListNames(backers)), p, Def(town, p.Id), m);
            News(town, p, news);
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

    // Envoys ------------------------------------------------------------------------------------

    /// <summary>Whether a pact with the town keeps it from taking any enemy's gold today.</summary>
    public static bool Pacted(Town town, PowerState p) => p.PactUntil > town.Day;

    /// <summary>What its envoy would come about today: an alliance, a pact, a demand for tribute, or nothing.</summary>
    public static string EnvoyKind(Town town, PowerState p)
    {
        var e = Rules(town).Envoys;
        var d = Def(town, p.Id);
        if (p.Side == PowerSide.Enemy) return "";
        bool favour = !p.TaskTaken && p.Standing >= e.PactAt && FavourFor(town, p, d) != null;
        if (p.Side == PowerSide.Town) return favour ? "favour" : "";
        if (p.Standing < e.TributeBelow) return d.Evil ? "tribute" : "";
        if (p.Standing >= d.AllyAt - e.AllianceWithin) return "alliance";
        if (favour) return "favour";
        if (p.Standing >= e.PactAt && d.Serves == "any" && !Pacted(town, p)) return "pact";
        return "";
    }

    /// <summary>The gold an envoy of this kind asks.</summary>
    public static double EnvoyAsk(Town town, PowerDef d, string kind)
    {
        var e = Rules(town).Envoys;
        return Js.Round(kind switch
        {
            "alliance" => d.Price * e.AllianceDiscount,
            "pact" => d.Gift * e.PactPrice,
            "tribute" => d.Gift * e.Tribute,
            _ => 0,
        });
    }

    /// <summary>
    /// Every few days one power sends the town an envoy: a demand from the most hostile first, then an alliance from
    /// the friendliest, then a pact. Its own dice choose whether a power with strange prices asks one.
    /// </summary>
    private static void SendEnvoy(Town town)
    {
        var e = Rules(town).Envoys;
        if (town.Day < e.From || (town.Day - e.From) % Math.Max(1, e.Every) != 0) return;
        var p = town.Powers
            .Where(p => p.Envoy == "" && town.Day >= p.NextEnvoyDay && EnvoyKind(town, p) != "")
            .OrderBy(p => EnvoyKind(town, p) switch { "tribute" => 0, "alliance" => 1, "favour" => 2, _ => 3 })
            .ThenBy(p => EnvoyKind(town, p) == "tribute" ? p.Standing : -(p.Standing - Def(town, p.Id).AllyAt))
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (p == null) return;
        var d = Def(town, p.Id);
        string kind = EnvoyKind(town, p);
        p.Envoy = kind;
        p.EnvoyGold = EnvoyAsk(town, d, kind);
        var dice = Dice(town, p);
        p.EnvoyPrice = kind is "alliance" or "pact" && d.Strange.Count > 0 && dice.Chance(d.StrangeChance) ? dice.Pick(d.Strange).Id : "";
        p.EnvoyUntil = town.Day + e.AnswerDays;
        if (kind == "favour")
        {
            var (ask, site, menace, count) = FavourFor(town, p, d)!.Value;
            p.Task = ask;
            p.TaskSite = site;
            p.TaskMenace = menace;
            p.TaskCount = count;
            News(town, p, Fill(town, TaskText(town, p, Rules(town).Favours.Ask.GetValueOrDefault(ask, "")).Replace("{answer}", e.AnswerDays.ToString()), p, d));
            return;
        }
        string text = kind switch { "alliance" => e.Alliance, "pact" => e.Pact, _ => e.Demand };
        News(town, p, Fill(town, text.Replace("{price}", PriceText(town, p)).Replace("{boon}", d.Ally.Text).Replace("{answer}", e.AnswerDays.ToString()), p, d));
    }

    /// <summary>The envoys' own dice, seeded from the town, the day and the power, so they never shift the main stream.</summary>
    private static Rng Dice(Town town, PowerState p) =>
        new(unchecked(town.Seed * 7919 + town.Day * 104729 + p.Id.Aggregate(17, (h, c) => h * 31 + c) + 29));

    public static StrangePriceDef? Strange(Town town, PowerState p) =>
        p.EnvoyPrice == "" ? null : Def(town, p.Id).Strange.FirstOrDefault(x => x.Id == p.EnvoyPrice);

    /// <summary>What the envoy asks, as the town hears it: gold, or something stranger.</summary>
    public static string PriceText(Town town, PowerState p) => Strange(town, p)?.Text ?? $"{Js.Str(p.EnvoyGold)} gold";

    /// <summary>Why the town can't accept the envoy's terms now, or null if it can.</summary>
    public static string? AnswerBlocked(Town town, PowerState p)
    {
        if (p.Envoy == "") return "no envoy";
        if (p.Envoy == "favour") return null;
        if (Strange(town, p) is { } price)
            return price.Kind == "name" && Named(town) == null ? "no one in town to give" : null;
        return town.Treasury < p.EnvoyGold ? $"it asks {Js.Str(p.EnvoyGold)} gold" : null;
    }

    /// <summary>Who a "name" price takes: a grown townsperson who is no hero.</summary>
    private static List<Agent> Nameable(Town town) =>
        town.Residents.Where(a => !a.Dead && a.Hero == null && a.Age >= 16).OrderBy(a => a.Id).ToList();

    private static Agent? Named(Town town) => Nameable(town).FirstOrDefault();

    /// <summary>Pay a strange price, and say how it was paid.</summary>
    private static void PayStrange(Town town, PowerState p, PowerDef d, StrangePriceDef price)
    {
        string person = "";
        switch (price.Kind)
        {
            case "name":
                var a = town.Rng.Pick(Nameable(town));
                person = a.Name;
                Population.Depart(town, a);
                break;
            case "harvest":
                p.Owed = price.Id;
                p.OwedUntil = town.Day + price.Days;
                break;
            case "voice":
                town.Mandate = Math.Max(0, town.Mandate - price.Amount);
                break;
        }
        if (price.Paid != "") News(town, p, Fill(town, price.Paid.Replace("{person}", person), p, d), Importance.Notable);
    }

    /// <summary>What the farms still bring in while a "harvest" price is being paid.</summary>
    public static double Harvest(Town town)
    {
        double y = 1;
        foreach (var p in town.Powers)
            if (p.Owed != "" && Def(town, p.Id).Strange.FirstOrDefault(x => x.Id == p.Owed) is { Kind: "harvest" } h)
                y *= 1 - h.Amount;
        return y;
    }

    /// <summary>Answer a power's envoy: accept its terms and pay, or send it home.</summary>
    public static bool Answer(Town town, string id, bool accept)
    {
        var p = Find(town, id);
        if (p == null || p.Envoy == "") return false;
        var d = Def(town, id);
        var e = Rules(town).Envoys;
        if (!accept)
        {
            Refuse(town, p, d, e.Refused);
            return true;
        }
        if (AnswerBlocked(town, p) != null) return false;
        string kind = p.Envoy;
        if (kind == "favour")
        {
            p.TaskTaken = true;
            p.TaskUntil = town.Day + Rules(town).Favours.Days;
            Done(town, p);
            News(town, p, Fill(town, TaskText(town, p, Rules(town).Favours.Task.GetValueOrDefault(p.Task, "")), p, d), Importance.Notable);
            return true;
        }
        if (kind == "aid")
        {
            town.Book(TreasuryAccount.Powers, -p.EnvoyGold);
            Done(town, p);
            WinFeud(town, p, d);
            return true;
        }
        string price = PriceText(town, p);
        if (Strange(town, p) is { } strange) PayStrange(town, p, d, strange);
        else town.Book(TreasuryAccount.Powers, -p.EnvoyGold);
        switch (kind)
        {
            case "alliance":
                p.Side = PowerSide.Town;
                p.SideDay = town.Day;
                p.Unpaid = 0;
                break;
            case "pact":
                p.PactUntil = town.Day + e.PactDays;
                p.Favour += e.PactFavour;
                break;
            case "tribute":
                p.Favour += e.TributeFavour;
                p.Standing = Math.Clamp(p.Standing + e.TributeFavour, -100, 100);
                break;
        }
        // A power that has struck a bargain with the town sends the enemy's envoys home.
        if (kind != "tribute" && Suitor(town, p) is { } m)
        {
            p.CourtedBy = null;
            News(town, p, Fill(town, Rules(town).Text.Refused, p, d, m), Importance.Notable);
        }
        string terms = kind switch
        {
            "alliance" => $"an alliance: {d.Ally.Text}",
            "pact" => $"a truce: {Name(town, d)} takes no enemy's gold for {Js.Str(e.PactDays)} days",
            _ => "tribute",
        };
        Done(town, p);
        News(town, p, Fill(town, e.Accepted.Replace("{terms}", terms).Replace("{price}", price), p, d));
        return true;
    }

    private static void Lapse(Town town, PowerState p, PowerDef d) => Refuse(town, p, d, Rules(town).Envoys.Lapsed);

    private static void Refuse(Town town, PowerState p, PowerDef d, string text)
    {
        var e = Rules(town).Envoys;
        bool demand = p.Envoy is "tribute" or "aid";
        p.Favour -= p.Envoy == "aid" ? Rules(town).Feuds.Spurned : demand ? e.Spurned : e.Declined;
        if (p.Envoy == "favour") ClearTask(p);
        Done(town, p);
        News(town, p, Fill(town, text, p, d) + (demand ? $" {Capital(Name(town, d))} will remember it." : ""), demand ? Importance.Major : Importance.Notable);
    }

    private static string Capital(string name) => name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    private static void Done(Town town, PowerState p)
    {
        if (!p.TaskTaken) ClearTask(p);
        p.Envoy = "";
        p.EnvoyGold = 0;
        p.EnvoyPrice = "";
        p.NextEnvoyDay = town.Day + Rules(town).Envoys.Rest;
    }

    /// <summary>The envoy's terms for its card.</summary>
    public static string EnvoyText(Town town, PowerState p)
    {
        var d = Def(town, p.Id);
        var e = Rules(town).Envoys;
        string price = PriceText(town, p);
        return p.Envoy switch
        {
            "alliance" => $"offers an alliance for {price} (its price is {Js.Str(d.Price)} gold), then {Js.Str(d.Tithe)} gold a day",
            "pact" => $"offers a truce for {price}: it takes no enemy's gold for {Js.Str(e.PactDays)} days",
            "tribute" => $"demands tribute of {price}, or it will remember the slight",
            "favour" => $"asks a favour: {TaskText(town, p, "{task}")} within {Js.Str(Rules(town).Favours.Days)} days, for its friendship and {Js.Str(Js.Round(d.Gift * Rules(town).Favours.Gold))} gold",
            "aid" => $"calls on the town for {price} to win its feud with {Name(town, Find(town, p.FeudWith) is { } f ? Def(town, f.Id) : d)}",
            _ => "",
        };
    }

    // Favours ------------------------------------------------------------------------------------

    /// <summary>The first favour it could ask today, starting from a different one of its asks each envoy round.</summary>
    public static (string Ask, string Site, int Menace, int Count)? FavourFor(Town town, PowerState p, PowerDef d)
    {
        var f = Rules(town).Favours;
        if (d.Asks.Count == 0) return null;
        int start = town.Day / Math.Max(1, Rules(town).Envoys.Every) % d.Asks.Count;
        for (int i = 0; i < d.Asks.Count; i++)
        {
            string ask = d.Asks[(start + i) % d.Asks.Count];
            switch (ask)
            {
                case "clear":
                    var site = town.Sites.Where(s => !s.Cleared && s.Known == SiteKnowledge.Confirmed && s.Def.Tier <= f.MaxTier)
                        .OrderBy(s => s.Def.Tier).ThenBy(s => s.Def.Id, StringComparer.Ordinal).FirstOrDefault();
                    if (site != null) return (ask, site.Def.Id, 0, 0);
                    break;
                case "slay":
                    if (Worst(town) is { } m) return (ask, "", m.Id, 0);
                    break;
                case "route":
                    int open = Trade.Open(town).Count();
                    if (open < Trade.Cap(town)) return (ask, "", 0, open + 1);
                    break;
            }
        }
        return null;
    }

    /// <summary>A favour's wording, with its target filled in: "{task}" is what to do.</summary>
    public static string TaskText(Town town, PowerState p, string text)
    {
        string task = p.Task switch
        {
            "clear" => $"clear {Quests.Site(town, p.TaskSite)?.Def.Name ?? "a lair"}",
            "slay" => $"see {town.Saga.Menaces.FirstOrDefault(m => m.Id == p.TaskMenace)?.Name ?? "the worst of the town's enemies"} destroyed",
            "route" => $"open a new trade route ({Js.Str(p.TaskCount)} running)",
            _ => "",
        };
        return text.Replace("{task}", task).Replace("{days}", Js.Str(Math.Max(0, p.TaskUntil - town.Day)));
    }

    /// <summary>Whether the favour the town took on is done.</summary>
    public static bool TaskDone(Town town, PowerState p) => p.Task switch
    {
        "clear" => Quests.Site(town, p.TaskSite)?.Cleared ?? true,
        "slay" => town.Saga.Menaces.FirstOrDefault(m => m.Id == p.TaskMenace) is not { Alive: true },
        "route" => Trade.Open(town).Count() >= p.TaskCount,
        _ => true,
    };

    private static void Task(Town town, PowerState p, PowerDef d)
    {
        var f = Rules(town).Favours;
        if (TaskDone(town, p))
        {
            double gold = Js.Round(d.Gift * f.Gold);
            p.Favour += f.Done;
            p.Standing = Math.Clamp(p.Standing + f.Done, -100, 100);
            if (gold > 0) town.Book(TreasuryAccount.Powers, gold);
            News(town, p, Fill(town, TaskText(town, p, f.Kept).Replace("{gold}", Js.Str(gold)), p, d));
            ClearTask(p);
        }
        else if (town.Day >= p.TaskUntil)
        {
            p.Favour -= f.Failed;
            News(town, p, Fill(town, TaskText(town, p, f.Broken), p, d), Importance.Notable);
            ClearTask(p);
        }
    }

    private static void ClearTask(PowerState p)
    {
        p.Task = "";
        p.TaskSite = "";
        p.TaskMenace = 0;
        p.TaskCount = 0;
        p.TaskUntil = 0;
        p.TaskTaken = false;
    }

    // Feuds --------------------------------------------------------------------------------------

    private static string FeudFill(Town town, string text, PowerState p, PowerDef d, PowerState foe, string why = "") =>
        Fill(town, text.Replace("{foe}", Name(town, Def(town, foe.Id))).Replace("{foeLeader}", Def(town, foe.Id).Leader).Replace("{why}", why), p, d);

    /// <summary>Every few days the feud that has rested longest flares; the town's ally in it calls for aid.</summary>
    private static void StartFeud(Town town)
    {
        var r = Rules(town).Feuds;
        if (town.Day < r.From || (town.Day - r.From) % Math.Max(1, r.Every) != 0) return;
        var pair = r.Pairs
            .Select(f => (Def: f, A: Find(town, f.A), B: Find(town, f.B)))
            .Where(x => x.A != null && x.B != null && x.A.FeudWith == "" && x.B.FeudWith == "" && town.Day >= x.A.NextFeudDay
                        && x.A.Side != PowerSide.Enemy && x.B.Side != PowerSide.Enemy)
            .OrderBy(x => x.A!.NextFeudDay).FirstOrDefault();
        if (pair.A == null || pair.B == null) return;
        var (a, b) = (pair.A, pair.B);
        foreach (var (x, y) in new[] { (a, b), (b, a) })
        {
            x.FeudWith = y.Id;
            x.FeudUntil = town.Day + r.Days;
            x.FeudStrikeDay = town.Day + r.StrikeEvery / 2;
        }
        a.NextFeudDay = town.Day + r.Days + r.Rest;
        var da = Def(town, a.Id);
        News(town, a, FeudFill(town, r.Begins, a, da, b, pair.Def.Why));
        // A call to arms comes before whatever else its envoys were here about.
        var ally = new[] { a, b }.FirstOrDefault(x => x.Side == PowerSide.Town);
        if (ally == null) return;
        if (ally.Envoy != "") Done(town, ally);
        var dally = Def(town, ally.Id);
        ally.Envoy = "aid";
        ally.EnvoyGold = Js.Round(dally.Gift * r.Aid);
        ally.EnvoyPrice = "";
        ally.EnvoyUntil = town.Day + Rules(town).Envoys.AnswerDays;
        News(town, ally, FeudFill(town, r.CallToArms.Replace("{price}", $"{Js.Str(ally.EnvoyGold)} gold"), ally, dally, Find(town, ally.FeudWith)!));
    }

    /// <summary>A feud burning: a side that strikes at all strikes the town that keeps its foe; it cools at its end.</summary>
    private static void Feud(Town town, PowerState p, PowerDef d)
    {
        var r = Rules(town).Feuds;
        var foe = Find(town, p.FeudWith);
        if (foe == null || town.Day >= p.FeudUntil)
        {
            if (foe != null && string.CompareOrdinal(p.Id, foe.Id) < 0) News(town, p, FeudFill(town, r.Ends, p, d, foe), Importance.Notable);
            p.FeudWith = "";
            if (p.Envoy == "aid") Done(town, p);
            return;
        }
        if (foe.Side == PowerSide.Town && p.Side != PowerSide.Town && d.Evil && d.Enemy.Strike != "" && town.Day >= p.FeudStrikeDay)
        {
            p.FeudStrikeDay = town.Day + r.StrikeEvery;
            News(town, p, FeudFill(town, r.Strikes, p, d, foe), Importance.Notable);
            Blows.Strike(town, d.Enemy.Strike, new Culprit(Name(town, d), d.Leader, d.Seat), 15 + d.Enemy.Host * 0.2, 1);
        }
    }

    /// <summary>The town's gold wins its ally's feud: the ally is grateful, its foe sues for peace and remembers.</summary>
    private static void WinFeud(Town town, PowerState ally, PowerDef d)
    {
        var r = Rules(town).Feuds;
        var foe = Find(town, ally.FeudWith);
        ally.Favour += r.AidFavour;
        ally.FeudWith = "";
        if (foe == null) return;
        foe.Favour -= r.Lost;
        foe.FeudWith = "";
        News(town, ally, FeudFill(town, r.Won, ally, d, foe));
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
        Def(town, p.Id).Enemy.Text.Replace("{enemy}", Patron(town, p)?.Name ?? Suitor(town, p)?.Name ?? "its new master");

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
}
