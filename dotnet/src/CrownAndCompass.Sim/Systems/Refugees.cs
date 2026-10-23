namespace CrownAndCompass.Sim.Systems;

/// <summary>How readily refugees pick this town over somewhere else (refugees.json draw).</summary>
public sealed record RefugeeDraw
{
    public double Base { get; init; } = 0.7;
    public double Palisade { get; init; } = 0.1;
    public double StoneWall { get; init; } = 0.2;
    public double CityWall { get; init; } = 0.3;

    /// <summary>Per point of Mandate above 50, and per point of the town's dread.</summary>
    public double Mandate { get; init; } = 0.005;
    public double Dread { get; init; } = 0.004;
    public double Least { get; init; } = 0.3;
    public double Most { get; init; } = 1.6;
}

/// <summary>Folk who flee each kind of blow: least and most.</summary>
public sealed record RefugeeSources
{
    public int[] Razed { get; init; } = [8, 16];
    public int[] Raided { get; init; } = [2, 4];
    public int[] Trouble { get; init; } = [2, 5];
    public int[] Pushed { get; init; } = [2, 5];
    public int[] Shadow { get; init; } = [1, 3];
}

/// <summary>Days a refugee waits in the camp: at the least, and at most once the town keeps building for them.</summary>
public sealed record RefugeePatience
{
    public int Least { get; init; } = 14;
    public int Most { get; init; } = 60;
}

/// <summary>What taking a refugee in, or turning one away, does to the Mandate and to their people's standing.</summary>
public sealed record RefugeeWelcome
{
    public double Mandate { get; init; } = 0.4;
    public double Thanks { get; init; } = 1;
    public double ThanksMost { get; init; } = 10;
    public double TurnedMandate { get; init; } = 0.6;
    public double TurnedStanding { get; init; } = 3;
}

/// <summary>A razed settlement its folk go home to rebuild: what it starts again with.</summary>
public sealed record RefugeeHome
{
    public double Prosperity { get; init; } = 20;
    public double Security { get; init; } = 30;
    public double Standing { get; init; } = 100;
}

/// <summary>A step of haven fame: refugees sheltered to reach it, the town's title, and what it brings.</summary>
public sealed record HavenDef
{
    public int At { get; init; }
    public string Title { get; init; } = "";

    /// <summary>Added to the refugees' draw and to the town's settler appeal.</summary>
    public double Draw { get; init; }
    public double Appeal { get; init; }

    /// <summary>Mandate, and standing with every settlement the town knows, once on reaching it.</summary>
    public double Mandate { get; init; }
    public double Standing { get; init; }
    public string Text { get; init; } = "";
}

/// <summary>Refugees (content/game/refugees.json).</summary>
public sealed record RefugeeRules
{
    public RefugeeDraw Draw { get; init; } = new();
    public RefugeeSources Sources { get; init; } = new();

    /// <summary>Chance each arrival is a couple, the gold most bring, and the chance and gold of one who saved enough to build.</summary>
    public double Couple { get; init; } = 0.35;
    public int[] Poor { get; init; } = [0, 12];
    public double WellOff { get; init; } = 0.15;
    public int[] Savings { get; init; } = [60, 160];

    /// <summary>The trades each people's refugees know, by people id, for this share of them.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Trades { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
    public double TradeShare { get; init; } = 0.75;
    public RefugeePatience Patience { get; init; } = new();

    /// <summary>Food a day the council buys each refugee in the camp who can't buy their own.</summary>
    public double Ration { get; init; } = 1;

    /// <summary>A column this big asks the mayor first, and the share "take some" lets in.</summary>
    public int CardAt { get; init; } = 5;
    public double Some { get; init; } = 0.5;
    public RefugeeWelcome Welcome { get; init; } = new();

    /// <summary>Refugees in the camp before it grumbles and may sicken: Mandate a day per one over, the most a day, and a fever's daily chance (before healers).</summary>
    public int CrowdAt { get; init; } = 12;
    public double Grumble { get; init; } = 0.03;
    public double GrumbleMost { get; init; } = 0.3;
    public double Fever { get; init; } = 0.02;
    public RefugeeHome Home { get; init; } = new();
    public IReadOnlyList<HavenDef> Havens { get; init; } = [];
    public IReadOnlyDictionary<string, string> Texts { get; init; } = new Dictionary<string, string>();
}

/// <summary>Refugees stopped on the road, waiting on the mayor's word.</summary>
public sealed class RefugeeColumn
{
    public int Count { get; set; }
    public int? From { get; set; }
    public int? By { get; set; }
    public string FromName { get; set; } = "";
    public int Day { get; set; }
}

/// <summary>The town's dealings with refugees, and its haven fame.</summary>
public sealed class RefugeeState
{
    /// <summary>Refugees taken in, turned away, settled, given up on the camp and gone home.</summary>
    public int Sheltered { get; set; }
    public int TurnedAway { get; set; }
    public int Settled { get; set; }
    public int GaveUp { get; set; }
    public int WentHome { get; set; }

    /// <summary>Steps of haven fame reached (refugees.json havens).</summary>
    public int Haven { get; set; }
    public RefugeeColumn? Column { get; set; }

    /// <summary>The camp grumbled yesterday: Mandate it cost.</summary>
    public double Grumbled { get; set; }
}

/// <summary>
/// Folk fleeing a struck settlement or a menace (<see cref="Flee"/>, called wherever a blow drives people out) come up the
/// road as travellers (<see cref="Lodging"/>): they camp free outside town, wait far longer than other travellers, longer
/// still while homes and workplaces are going up, and start work, buy or build like anyone. A big column asks the mayor
/// first (story <c>refugee-column</c>). Taking them in earns Mandate, their people's thanks and the town's haven fame;
/// once the menace that drove them out falls, those still in the camp go home and rebuild. Settled ones stay for good.
/// </summary>
public static class Refugees
{
    public const string Source = "refugees";
    public const string StoryId = "refugee-column";

    public static RefugeeRules Rules(Town town) => town.Content.Refugees;

    private static string Text(Town town, string key) => Rules(town).Texts.GetValueOrDefault(key, "");

    /// <summary>Refugees in the camp now, not yet settled.</summary>
    public static IEnumerable<Agent> Camped(Town town) => town.Agents.Where(a => a.Refugee && a.Traveller && !a.Departed);

    public static int Camping(Town town) => Camped(town).Count();

    /// <summary>A column waits on the mayor's word.</summary>
    public static bool Waiting(Town town) => town.Refuge.Column != null;

    /// <summary>The town's haven title, or "" before it has earned one.</summary>
    public static string Title(Town town) => town.Refuge.Haven > 0 ? Rules(town).Havens[Math.Min(town.Refuge.Haven, Rules(town).Havens.Count) - 1].Title : "";

    private static HavenDef? Haven(Town town) => town.Refuge.Haven > 0 ? Rules(town).Havens[Math.Min(town.Refuge.Haven, Rules(town).Havens.Count) - 1] : null;

    /// <summary>Settler appeal the haven fame adds.</summary>
    public static double Appeal(Town town) => Haven(town)?.Appeal ?? 0;

    /// <summary>How readily refugees choose this town: its walls, its Mandate and fame, less its fear.</summary>
    public static double Draw(Town town)
    {
        var d = Rules(town).Draw;
        var w = town.Defenses;
        double walls = w.CityWall == PalisadeState.Built ? d.CityWall : w.StoneWall == PalisadeState.Built ? d.StoneWall : w.Palisade == PalisadeState.Built ? d.Palisade : 0;
        double v = d.Base + walls + (town.Mandate - 50) * d.Mandate + (Haven(town)?.Draw ?? 0) - town.Saga.Dread * d.Dread;
        return Math.Clamp(v, d.Least, d.Most);
    }

    /// <summary>
    /// A blow drives folk out of a settlement (or out of a menace's way): some come to the town, as many as its draw
    /// says. A small group pitches its tents at once (logged with <paramref name="said"/>, given the count, or the
    /// rules' line); a column of <see cref="RefugeeRules.CardAt"/> or more stops on the road and asks the mayor.
    /// Returns how many came straight in.
    /// </summary>
    public static int Flee(Town town, Settlement? from, Menace? by, int[] range, Func<int, string>? said = null, string? fromName = null)
    {
        if (town.Outcome != null || range.Length == 0) return 0;
        var rng = town.Rng;
        double want = rng.Range(range[0], range[^1] + 1) * Draw(town);
        int n = (int)want;
        if (rng.Chance(want - n)) n++;
        if (n <= 0) return 0;
        string where = from?.Name ?? fromName ?? "the wilds";
        var r = Rules(town);
        if (n >= r.CardAt && town.Content.Stories.Story(StoryId) is { } def)
        {
            if (town.Refuge.Column is { } col) col.Count += n;
            else
            {
                town.Refuge.Column = new RefugeeColumn { Count = n, From = from?.Id, By = by?.Id, FromName = where, Day = town.Day };
                Stories.Contact(town, def, new Story { SettlementId = from?.Id, MenaceId = by?.Id });
            }
            town.Log(LedgerCategory.Arrival, Importance.Major, Fill(town, Text(town, "column"), town.Refuge.Column!.Count, where));
            return 0;
        }
        var come = Arrive(town, n, from, by);
        string line = said?.Invoke(n) ?? Fill(town, Text(town, "come"), n, where);
        town.Log(LedgerCategory.Arrival, Importance.Notable, line, come);
        Welcome(town, n, from);
        return n;
    }

    private static string Fill(Town town, string text, int n, string from) =>
        Saga.Cap(text.Replace("{n}", Js.Str(n)).Replace("{from}", from).Replace("{town}", town.Name));

    /// <summary>The mayor's word on the column at the road: 1 take them all, 2 take some, 3 turn them away.</summary>
    public static string Decide(Town town, int what)
    {
        if (town.Refuge.Column is not { } col) return "";
        town.Refuge.Column = null;
        var r = Rules(town);
        var from = col.From is int f ? town.Settlements[f] : null;
        var by = col.By is int b ? town.Saga.Menaces[b - 1] : null;
        int taken = what == 1 ? col.Count : what == 2 ? (int)Math.Ceiling(col.Count * r.Some) : 0;
        int turned = col.Count - taken;
        var said = new List<string>();
        if (taken > 0)
        {
            var come = Arrive(town, taken, from, by);
            town.Log(LedgerCategory.Arrival, Importance.Notable, Fill(town, Text(town, "taken"), taken, col.FromName), come);
            Welcome(town, taken, from);
            said.Add($"+{taken} refugees");
        }
        if (turned > 0)
        {
            town.Refuge.TurnedAway += turned;
            town.Mandate = Math.Max(0, town.Mandate - r.Welcome.TurnedMandate * turned);
            if (from != null) Thank(town, from, -Math.Min(r.Welcome.ThanksMost, r.Welcome.TurnedStanding * turned));
            town.Log(LedgerCategory.Arrival, Importance.Notable, Fill(town, Text(town, "turned"), turned, col.FromName));
            said.Add($"{turned} turned away");
        }
        return string.Join(", ", said);
    }

    /// <summary>Taking them in: Mandate, their people's thanks, and the haven's fame.</summary>
    private static void Welcome(Town town, int n, Settlement? from)
    {
        var r = Rules(town);
        town.Mandate = Math.Min(100, town.Mandate + r.Welcome.Mandate * n);
        if (from != null) Thank(town, from, Math.Min(r.Welcome.ThanksMost, r.Welcome.Thanks * n));
        var st = town.Refuge;
        st.Sheltered += n;
        while (st.Haven < r.Havens.Count && st.Sheltered >= r.Havens[st.Haven].At)
        {
            var h = r.Havens[st.Haven++];
            town.Mandate = Math.Min(100, town.Mandate + h.Mandate);
            foreach (var s in town.Settlements.Where(s => s.Met && !s.Razed).OrderBy(s => s.Id)) World.Shift(town, s, h.Standing);
            town.Log(LedgerCategory.Town, Importance.Major, Saga.Cap(h.Text.Replace("{town}", town.Name)));
            Records.Pin(town, "haven", h.Title);
        }
    }

    /// <summary>Every settlement of the refugees' people hears how they were treated.</summary>
    private static void Thank(Town town, Settlement from, double by)
    {
        foreach (var s in town.Settlements.Where(s => s.Kin.Id == from.Kin.Id && !s.Razed).OrderBy(s => s.Id))
        {
            if (by > 0) World.Shift(town, s, by);
            else s.Standing = Math.Max(-100, s.Standing + by);
        }
    }

    /// <summary>n refugees come up the road, singles and couples, of their people's race and trades, most with next to nothing.</summary>
    private static List<int> Arrive(Town town, int n, Settlement? from, Menace? by)
    {
        var r = Rules(town);
        var rng = town.Rng;
        string? race = from != null && from.Kin.Race != "" && town.Content.Races.Any(x => x.Id == from.Kin.Race) ? from.Kin.Race : null;
        var trades = from != null ? r.Trades.GetValueOrDefault(from.Kin.Id) : null;
        var come = new List<int>();
        Agent One(string? family)
        {
            string? trade = trades is { Count: > 0 } && rng.Chance(r.TradeShare) ? trades[rng.Range(0, trades.Count)] : null;
            double gold = rng.Chance(r.WellOff) ? rng.Range(r.Savings[0], r.Savings[^1] + 1) : rng.Range(r.Poor[0], r.Poor[^1] + 1);
            var a = Population.CreateAgent(town, new NewAgentOptions { Race = race, Profession = trade, Gold = gold, FamilyName = family });
            a.Refugee = true;
            a.RefugeeFrom = from?.Id;
            a.RefugeeBy = by?.Id;
            Lodging.Arrive(town, a, r.Patience.Least);
            come.Add(a.Id);
            return a;
        }
        while (come.Count < n)
        {
            var a = One(null);
            if (come.Count >= n || !rng.Chance(r.Couple)) continue;
            var b = One(a.FamilyName);
            a.SpouseId = b.Id;
            b.SpouseId = a.Id;
            a.ChangeOpinion(b.Id, 70);
            b.ChangeOpinion(a.Id, 70);
        }
        return come;
    }

    /// <summary>
    /// Daily: a column whose card is gone comes in; the camp's refugees are fed, and wait longer while the town builds;
    /// those whose menace has fallen go home; a crowded camp grumbles and may sicken.
    /// </summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        var st = town.Refuge;
        if (st.Column != null && !town.Stories.Open.Any(s => s.Def == StoryId)) Decide(town, 1);
        st.Grumbled = 0;
        var camp = Camped(town).OrderBy(a => a.Id).ToList();
        if (camp.Count == 0) return;
        int day = town.Day;
        bool building = town.Buildings.Any(b => b.IsSite && (b.Def.Housing > 0 || b.Def.Jobs > 0));
        double price = town.Market.Price(Good.Food) * r.Ration;
        int hungry = 0;
        var home = new List<Agent>();
        foreach (var a in camp)
        {
            if (a.RefugeeBy is int mid && mid >= 1 && mid <= town.Saga.Menaces.Count && !town.Saga.Menaces[mid - 1].Alive)
            {
                home.Add(a);
                continue;
            }
            // Rations for those who can't buy their own bread.
            if (a.Gold < price)
            {
                if (town.Treasury >= price)
                {
                    town.Book(TreasuryAccount.Refugees, -price);
                    a.Gold += price;
                }
                else
                {
                    hungry++;
                    a.StayUntil--;
                }
            }
            if (building) a.StayUntil = Math.Min(a.ArrivedTick / SimTime.HoursPerDay + r.Patience.Most, Math.Max(a.StayUntil, day) + 1);
        }
        GoHome(town, home);
        int crowd = camp.Count - home.Count;
        if (crowd < r.CrowdAt) return;
        // A crowded camp grumbles, less with a healer and full bellies, and may pass a fever to the town.
        double care = 1 - Sickness.Healers(town);
        double grumble = Math.Min(r.GrumbleMost, r.Grumble * (crowd - r.CrowdAt + 1)) * care * (hungry > 0 ? 1 : 0.5);
        if (grumble > 0)
        {
            town.Mandate = Math.Max(0, town.Mandate - grumble);
            st.Grumbled = grumble;
        }
        var dice = new Rng(unchecked((int)Voices.Hash("refugee-fever", town.Seed, day)));
        if (!dice.Chance(r.Fever * care)) return;
        var (cx, cy) = Lodging.Camp(town);
        var house = town.Buildings
            .Where(b => b.Complete && b.Def.Housing > 0 && b.Residents.Count > 0)
            .OrderBy(b => Js.Hypot(town.Lot(b.LotId).X - cx, town.Lot(b.LotId).Y - cy))
            .ThenBy(b => b.Id)
            .FirstOrDefault();
        if (house != null) Sickness.Seed(town, house, Text(town, "fever"));
    }

    /// <summary>The menace that drove them out is gone: refugees still in the camp go home, and a razed settlement rises again.</summary>
    private static void GoHome(Town town, List<Agent> going)
    {
        var r = Rules(town);
        foreach (var g in going.GroupBy(a => (a.RefugeeFrom ?? -1, a.RefugeeBy!.Value)).OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2))
        {
            var s = g.Key.Item1 >= 0 ? town.Settlements[g.Key.Item1] : null;
            var m = town.Saga.Menaces[g.Key.Item2 - 1];
            int n = 0;
            foreach (var a in g)
            {
                if (a.Departed) continue;
                Population.Depart(town, a);
                n++;
            }
            if (n == 0) continue;
            town.Refuge.WentHome += n;
            string from = s?.Name ?? "their home";
            town.Log(LedgerCategory.Departure, Importance.Notable,
                Saga.Cap(Text(town, "home").Replace("{from}", from).Replace("{by}", m.Name).Replace("{n}", Js.Str(n)).Replace("{town}", town.Name)), g.Select(a => a.Id).ToList());
            if (s == null) continue;
            if (!s.Razed)
            {
                Thank(town, s, Math.Min(r.Welcome.ThanksMost, r.Welcome.Thanks * n));
                continue;
            }
            s.Razed = false;
            s.Prosperity = r.Home.Prosperity;
            s.Security = r.Home.Security;
            s.Trouble = null;
            s.Shadow = null;
            s.Standing = Math.Max(0, s.Standing);
            town.Log(LedgerCategory.Town, Importance.Major, Saga.Cap(Text(town, "rebuilt").Replace("{from}", s.Name).Replace("{town}", town.Name)));
            World.Shift(town, s, r.Home.Standing);
        }
    }

    /// <summary>A refugee in the camp found their feet: the Ledger tells it their way.</summary>
    internal static string Settled(Town town, Agent a, string who, bool pair, string what)
    {
        town.Refuge.Settled += pair ? 2 : 1;
        return Saga.Cap(Text(town, "settle").Replace("{who}", who).Replace("{from}", From(town, a)).Replace("{verb}", pair ? "decide" : "decides").Replace("{town}", town.Name).Replace("{what}", what));
    }

    /// <summary>A refugee whose patience ran out takes the road on.</summary>
    internal static string GaveUp(Town town, Agent a, string who, bool pair, string why)
    {
        town.Refuge.GaveUp += pair ? 2 : 1;
        return Saga.Cap(Text(town, "gaveUp").Replace("{who}", who).Replace("{from}", From(town, a)).Replace("{verb}", pair ? "move" : "moves").Replace("{town}", town.Name).Replace("{why}", why));
    }

    private static string From(Town town, Agent a) => a.RefugeeFrom is int f ? town.Settlements[f].Name : "their home";

    /// <summary>"14 refugees from Ashford", for the column's card.</summary>
    public static string ColumnText(Town town) => town.Refuge.Column is { } c ? $"{c.Count} refugees from {c.FromName}" : "refugees";
}
