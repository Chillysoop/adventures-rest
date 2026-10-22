using System.Text.Json.Serialization;

namespace CrownAndCompass.Sim.Systems;

public sealed record SeasonFestivalDef
{
    public string Name { get; init; } = "";
    public int Days { get; init; } = 2;
    public double Mood { get; init; }

    /// <summary>Visitors while it lasts, times the season's own.</summary>
    public double Tourists { get; init; } = 1;
    public string Text { get; init; } = "";
}

public sealed record SeasonDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Blurb { get; init; } = "";

    /// <summary>The Ledger's line as the season turns; {town} is the town.</summary>
    public string Turn { get; init; } = "";

    /// <summary>Mood every townsperson carries through the season.</summary>
    public double Mood { get; init; }

    /// <summary>How fast building sites rise (frozen ground is slow).</summary>
    public double Build { get; init; } = 1;

    /// <summary>Added to the town's draw for settlers.</summary>
    public double Arrivals { get; init; }

    /// <summary>What each good's makers bring in, times their usual.</summary>
    [JsonConverter(typeof(GoodsListConverter))]
    public IReadOnlyList<KeyValuePair<Good, double>> Yields { get; init; } = [];

    /// <summary>How busy each kind of menace is: it grows and strikes this much faster.</summary>
    public IReadOnlyDictionary<string, double> Menace { get; init; } = new Dictionary<string, double>();

    /// <summary>The same for the warband.</summary>
    public double Warband { get; init; } = 1;

    /// <summary>Days added to any army's march on the town (snow on the roads).</summary>
    public int MarchDays { get; init; }
    public double Tourists { get; init; } = 1;
    public double ImportPremium { get; init; } = 3;

    /// <summary>The export floor, times the town's usual.</summary>
    public double ExportShare { get; init; } = 1;

    /// <summary>A trade fair opens the season: caravans bring what the market is short of.</summary>
    public bool Fair { get; init; }

    /// <summary>Cellars and granaries keep food back from the barges.</summary>
    public bool Hold { get; init; }
    public SeasonFestivalDef? Festival { get; init; }

    public double YieldOf(Good g)
    {
        foreach (var (k, v) in Yields) if (k == g) return v;
        return 1;
    }
}

public sealed record TouristRules
{
    /// <summary>Visitors a day for each soul in town, in an ordinary season.</summary>
    public double PerSoul { get; init; } = 0.006;

    /// <summary>Gold each visitor spends.</summary>
    public double Spend { get; init; } = 2.5;

    /// <summary>Share of it spent in taverns; the rest goes to the shops.</summary>
    public double TavernShare { get; init; } = 0.5;

    /// <summary>More visitors for every tier the town has climbed.</summary>
    public double TierBonus { get; init; } = 0.15;

    /// <summary>More visitors for a pleasant town: this much more when the decorations draw all they can.</summary>
    public double Appeal { get; init; } = 0.25;
}

public sealed record StoreRules
{
    /// <summary>Food the cellars of each household keep through the winter.</summary>
    public double HoldPerHousehold { get; init; } = 6;

    /// <summary>Winter stores in each granary, times its food store.</summary>
    public double GranaryHold { get; init; } = 1;

    /// <summary>What the traders charge to lay in stores, times base price, at least (half the season's import premium if dearer).</summary>
    public double LayInPremium { get; init; } = 1.25;

    /// <summary>Share of the empty stores one order lays in.</summary>
    public double LayInShare { get; init; } = 0.5;
}

public sealed record FairRules
{
    /// <summary>A fair's caravans fill each good short of its target up to this share of it.</summary>
    public double FillTo { get; init; } = 0.6;
}

public sealed record SeasonRules
{
    public int Days { get; init; } = 30;

    /// <summary>The season and the day of it on which a campaign opens.</summary>
    public int StartSeason { get; init; }
    public int StartDay { get; init; }

    /// <summary>Days either side of a turn over which the land changes colour (client).</summary>
    public int TurnBlend { get; init; } = 4;
    public IReadOnlyList<SeasonDef> Seasons { get; init; } = [];
    public TouristRules Tourists { get; init; } = new();
    public StoreRules Stores { get; init; } = new();
    public FairRules Fair { get; init; } = new();
}

/// <summary>The town's year: what this season's visitors spent, and how long the festival has left.</summary>
public sealed class SeasonState
{
    /// <summary>Index of the season the town last saw turn, so each turn is announced once.</summary>
    public int Seen { get; set; } = -1;
    public int FestivalDays { get; set; }

    /// <summary>Visitors and the gold they left yesterday.</summary>
    public int Tourists { get; set; }
    public double TouristGold { get; set; }

    /// <summary>The day the council last laid in stores.</summary>
    public int LaidInDay { get; set; } = -99;

    /// <summary>All the gold visitors have left this season.</summary>
    public double SeasonGold { get; set; }
}

/// <summary>
/// The turning year (GDD expansion 2): four seasons that change what the fields yield, how the town
/// feels, how fast sites rise, what the caravans charge, how many visitors come, and how busy each kind
/// of enemy is. Autumn's harvest fills the cellars and granaries for a lean winter; the council can lay
/// in more stores from the traders. Each season opens with a festival, and spring and autumn with a fair.
/// </summary>
public static class Seasons
{
    public static SeasonRules Rules(Town town) => town.Content.Seasons;

    /// <summary>Days since the year began (day 0 is the first of spring).</summary>
    private static int DayOfYear(Town town, int day) => day - 1 + Rules(town).StartSeason * Rules(town).Days + Rules(town).StartDay;

    /// <summary>The count of seasons since the campaign's first spring.</summary>
    public static int Index(Town town) => Index(town, town.Day);

    private static int Index(Town town, int day) => DayOfYear(town, day) / Rules(town).Days;

    public static SeasonDef Current(Town town) => Of(town, Index(town));

    private static SeasonDef Of(Town town, int index) => Rules(town).Seasons[index % Rules(town).Seasons.Count];

    public static SeasonDef Next(Town town) => Of(town, Index(town) + 1);

    /// <summary>Year of the campaign, from 1.</summary>
    public static int Year(Town town) => Index(town) / Rules(town).Seasons.Count + 1;

    /// <summary>Day of the season, from 1.</summary>
    public static int DayOf(Town town) => DayOfYear(town, town.Day) % Rules(town).Days + 1;

    public static int DaysLeft(Town town) => Rules(town).Days - DayOf(town) + 1;

    public static bool Festival(Town town) => town.Seasons.FestivalDays > 0;

    // Effects, read by the other systems ---------------------------------------------------------

    public static double Mood(Town town)
    {
        var s = Current(town);
        return s.Mood + (Festival(town) && s.Festival != null ? s.Festival.Mood : 0) + Omens.Mood(town);
    }

    // The year's omen folds into the season it falls in (Omens).
    public static double Yield(Town town, Good g) => Current(town).YieldOf(g) * Omens.Yield(town, g);

    public static double Build(Town town) => Current(town).Build * Omens.Build(town);

    public static double Arrivals(Town town) => Current(town).Arrivals + Omens.Arrivals(town);

    /// <summary>How busy a kind of menace is this season.</summary>
    public static double Activity(Town town, string kind) => (Current(town).Menace.TryGetValue(kind, out double a) ? a : 1) * Omens.Menace(town);

    public static double Warband(Town town) => Current(town).Warband * Omens.Warband(town);

    public static int MarchDays(Town town) => Current(town).MarchDays + Omens.MarchDays(town);

    // The day ----------------------------------------------------------------------------------

    /// <summary>Turn the season, run the festival, set the traders' prices and the stores, and host the visitors.</summary>
    public static void Daily(Town town)
    {
        var st = town.Seasons;
        int index = Index(town);
        var s = Current(town);
        if (st.Seen != index)
        {
            bool first = st.Seen < 0;
            st.Seen = index;
            st.SeasonGold = 0;
            if (!first)
            {
                town.Log(LedgerCategory.Town, Importance.Notable, s.Turn.Replace("{town}", town.Name));
                if (s.Fair) Fair(town, s);
                if (s.Festival is { } f)
                {
                    st.FestivalDays = f.Days;
                    town.Log(LedgerCategory.Social, Importance.Notable, f.Text.Replace("{town}", town.Name).Replace("{festival}", f.Name));
                }
            }
        }
        else if (st.FestivalDays > 0) st.FestivalDays--;

        var m = town.Market;
        m.ImportPremium = s.ImportPremium;
        m.ExportShare = town.Content.City.ExportShare * s.ExportShare;
        m.Hold(Good.Food, s.Hold ? Stores(town) : 0);
        Visitors(town, s);
    }

    /// <summary>Food the town keeps back for the winter: every household's cellar and every granary.</summary>
    public static double Stores(Town town)
    {
        var r = Rules(town).Stores;
        return Math.Round(town.Population * r.HoldPerHousehold + Planning.FoodStore(town) * r.GranaryHold);
    }

    /// <summary>How full the stores are now, 0 to 1 (more than 1 when the market has more besides).</summary>
    public static double StoresFull(Town town)
    {
        double cap = Stores(town);
        return cap <= 0 ? 0 : town.Market.Stock(Good.Food) / cap;
    }

    /// <summary>What laying in stores costs now, and how much food it buys; zero when it can't be done.</summary>
    public static (double Food, double Cost) LayInQuote(Town town)
    {
        var r = Rules(town).Stores;
        var s = Current(town);
        if (!s.Hold) return (0, 0);
        double room = Stores(town) - town.Market.Stock(Good.Food);
        if (room < 5) return (0, 0);
        double food = Math.Ceiling(Math.Max(5, room * r.LayInShare));
        // Dearer once the snow is on the roads.
        double cost = Js.Round(food * town.Market.BasePrice(Good.Food) * Math.Max(r.LayInPremium, s.ImportPremium * 0.5));
        return (food, cost);
    }

    /// <summary>The council buys grain off the traders for the winter stores.</summary>
    public static bool LayIn(Town town)
    {
        var (food, cost) = LayInQuote(town);
        if (food <= 0 || town.Treasury < cost) return false;
        town.Book(TreasuryAccount.Stores, -cost);
        town.Market.Seed(Good.Food, food);
        town.Seasons.LaidInDay = town.Day;
        town.Log(LedgerCategory.Town, Importance.Notable, $"The council buys {Js.Str(food)} loads of grain off the traders for the winter stores, for {Js.Str(cost)} gold.");
        return true;
    }

    /// <summary>The stand-in mayor lays in stores when the cellars are short going into winter and the treasury can spare it.</summary>
    public static void StandIn(Town town)
    {
        if (!Current(town).Hold || StoresFull(town) >= 0.6 || town.Day - town.Seasons.LaidInDay < 7) return;
        var (food, cost) = LayInQuote(town);
        if (food > 0 && town.Treasury >= cost * 3 + 200) LayIn(town);
    }

    /// <summary>The caravans of a fair bring what the market is short of.</summary>
    private static void Fair(Town town, SeasonDef s)
    {
        var m = town.Market;
        double fill = Rules(town).Fair.FillTo;
        var brought = new List<string>();
        foreach (var g in Goods.All)
        {
            double want = m.Target(g) * fill - m.Stock(g);
            if (want < 3) continue;
            m.Seed(g, Math.Ceiling(want));
            brought.Add(g.ToString().ToLowerInvariant());
        }
        if (brought.Count > 0)
            town.Log(LedgerCategory.Business, Importance.Routine, $"The {s.Name.ToLowerInvariant()} caravans bring {Expedition.ListNames(brought)} to the market.");
    }

    /// <summary>Visitors come by the season, the town's size and fame and how pleasant it is, and spend at the taverns and shops.</summary>
    private static void Visitors(Town town, SeasonDef s)
    {
        var r = Rules(town).Tourists;
        var st = town.Seasons;
        double festival = Festival(town) && s.Festival != null ? s.Festival.Tourists : 1;
        // Gardens, fountains and statues the settlers come for draw visitors too.
        double appeal = Decor.Draw(town) / Math.Max(0.01, Decor.Rules(town).DrawCap);
        double n = City.Souls(town) * r.PerSoul * s.Tourists * Omens.Tourists(town) * festival * Tournaments.Tourists(town) * (1 + r.TierBonus * town.City.Tier + r.Appeal * appeal);
        st.Tourists = (int)Math.Floor(n);
        st.TouristGold = 0;
        if (st.Tourists == 0) return;
        double gold = st.Tourists * r.Spend;
        var taverns = town.Taverns;
        var shops = town.Buildings.Where(b => b.Complete && !b.Closed && !b.ForSale && b.Def.IsBusiness && !b.Def.Venue && !b.Def.Holy && b.Def.Zone == Zone.Commercial).ToList();
        double tax = town.TaxRate;
        void Spend(List<Building> at, double sum)
        {
            if (at.Count == 0 || sum <= 0) return;
            double each = sum / at.Count;
            foreach (var b in at)
            {
                double t = each * tax;
                b.Cash += each - t;
                b.RevenueToday += each;
                town.Book(TreasuryAccount.SalesTax, t);
            }
            st.TouristGold += sum;
        }
        double inTaverns = shops.Count == 0 ? gold : gold * r.TavernShare;
        Spend(taverns, taverns.Count == 0 ? 0 : inTaverns);
        Spend(shops, gold - (taverns.Count == 0 ? 0 : inTaverns));
        st.SeasonGold += st.TouristGold;
    }

    // For the client ------------------------------------------------------------------------------

    /// <summary>How far the land has turned toward each season at this hour, blended across the turns: (spring, summer, autumn, winter).</summary>
    public static (double Spring, double Summer, double Autumn, double Winter) Look(Town town)
    {
        var r = Rules(town);
        double t = DayOfYear(town, town.Day) + town.Hour / 24.0;
        double len = r.Days, blend = Math.Max(0.5, r.TurnBlend);
        int n = r.Seasons.Count;
        var w = new double[n];
        // Each season's weight rises over the blend before its first day and falls over the blend after its last.
        for (int i = 0; i < n; i++)
        {
            double phase = ((t - i * len) % (n * len) + n * len) % (n * len);
            double up = Math.Clamp((phase + blend) / (2 * blend), 0, 1);
            double down = Math.Clamp((len + blend - phase) / (2 * blend), 0, 1);
            double wrap = Math.Clamp((phase - (n * len - blend)) / (2 * blend), 0, 1);
            w[i] = Math.Max(Math.Min(up, down), wrap);
        }
        double sum = w.Sum();
        return n < 4 || sum <= 0 ? (1, 0, 0, 0) : (w[0] / sum, w[1] / sum, w[2] / sum, w[3] / sum);
    }
}
