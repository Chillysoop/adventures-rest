namespace CrownAndCompass.Sim.Systems;

public sealed record PoachingRules
{
    /// <summary>The foe (foes.json) who poaches, the first day she may, and the days between her jobs.</summary>
    public string Foe { get; init; } = "dark-ranger";
    public int After { get; init; } = 45;
    public int[] Every { get; init; } = [10, 18];

    /// <summary>The hardest site she takes on alone, and the share of the reward she leaves the town when tolerated.</summary>
    public int MaxLevel { get; init; } = 4;
    public double Tolerated { get; init; } = 0.5;

    /// <summary>Her price to ride for the town, and the level she arrives at.</summary>
    public double Hire { get; init; } = 400;
    public int Level { get; init; } = 5;
    public IReadOnlyDictionary<string, string> Texts { get; init; } = new Dictionary<string, string>();
}

/// <summary>The dark ranger's dealings with the town: how many jobs she took and what the mayor decided.</summary>
public sealed class RangerState
{
    public int Poached { get; set; }
    public int NextDay { get; set; }

    /// <summary>"" undecided, "hired", "tolerated" or "hunted".</summary>
    public string Dealt { get; set; } = "";
}

/// <summary>
/// The rival dark ranger (the "dark-ranger" foe): from her camp she rides out and takes the town's open bounties
/// before its heroes do, clearing the site herself and keeping the escrow. After her first job a card asks the mayor
/// what to do: buy her sword (she rides for the town as a hero), tolerate her (she leaves half the reward), or hunt
/// her down (her camp goes on the map for a bounty, and she keeps poaching until it is cleared).
/// </summary>
public static class Poaching
{
    public const string Source = "ranger";

    private static PoachingRules Rules(Town town) => town.Content.Foes.Poaching;

    /// <summary>Her camp, while she holds it.</summary>
    public static SiteState? Camp(Town town) =>
        town.Sites.FirstOrDefault(s => s.Def.Foe == Rules(town).Foe && !s.Cleared);

    public static string Name(Town town) =>
        Camp(town) is { } c && Annals.Lineage(town.Content.History, "black-bough") is { Leaders.Count: > 0 } l
            ? l.Leaders[(int)(Voices.Hash(c.Def.Id) % (uint)l.Leaders.Count)]
            : "the dark ranger";

    /// <summary>A card is waiting on what to do about her.</summary>
    public static bool Waiting(Town town) => town.Ranger is { Poached: > 0, Dealt: "" } && Camp(town) != null;

    public static void Daily(Town town)
    {
        var r = Rules(town);
        if (town.Day < r.After || Camp(town) is not { } camp) return;
        var st = town.Ranger ??= new RangerState { NextDay = town.Day };
        if (st.Dealt == "hired" || town.Day < st.NextDay) return;
        var job = town.Bounties
            .Where(b => b.Status == BountyStatus.Open && b.SiteId != camp.Def.Id)
            .Select(b => (b, s: Quests.Site(town, b.SiteId)))
            .Where(x => x.s is { Cleared: false } && World.LevelOf(x.s) <= r.MaxLevel && x.s.Def.Foe == "" && !x.s.Def.Aloft && Lone(town, x.s))
            .OrderByDescending(x => x.b.Reward)
            .ThenBy(x => x.b.PostedTick)
            .ThenBy(x => x.b.Id)
            .Select(x => ((Bounty, SiteState)?)(x.b, x.s!))
            .FirstOrDefault();
        if (job is not var (bounty, site)) return;
        st.NextDay = town.Day + r.Every[0] + (int)(Voices.Hash("poach", town.Seed, town.Day) % (uint)(r.Every[1] - r.Every[0] + 1));
        st.Poached++;
        site.Cleared = true;
        site.ThoughtCleared = true;
        site.ClearedTick = town.Tick;
        bounty.Status = BountyStatus.Failed;
        string left = "";
        if (st.Dealt == "tolerated")
        {
            double back = Math.Floor(bounty.Reward * r.Tolerated);
            town.Book(TreasuryAccount.Bounties, back);
            left = $" She leaves {Js.Str(back)} gold of it on the council's table, as agreed.";
        }
        string text = r.Texts.GetValueOrDefault("poach", "")
            .Replace("{ranger}", Name(town)).Replace("{site}", site.Def.Name).Replace("{gold}", Js.Str(bounty.Reward)).Replace("{camp}", camp.Def.Name);
        town.Log(LedgerCategory.War, Importance.Major, text + left);
        town.Mandate = Math.Max(0, town.Mandate - (st.Dealt == "tolerated" ? 0 : 1));
        if (st.Poached == 1 && st.Dealt == "") Stories.OpenAt(town, "dark-ranger", camp.Def.Id);
    }

    /// <summary>A lair she can take alone: not the warband's camp, the undercity, or the seat of a grown menace.</summary>
    private static bool Lone(Town town, SiteState s) =>
        s.Def.Id != town.Content.Enemy.Outpost.Id && !Undercity.IsUnder(town, s) && !town.Saga.Menaces.Any(m => m.SeatId == s.Def.Id);

    /// <summary>The mayor's answer on her card: 1 hire her, 2 tolerate her, 3 hunt her.</summary>
    public static string Decide(Town town, int what)
    {
        var r = Rules(town);
        if (town.Ranger is not { } st || Camp(town) is not { } camp) return "";
        string name = Name(town);
        switch (what)
        {
            case 1:
                st.Dealt = "hired";
                town.Book(TreasuryAccount.Stories, -r.Hire);
                var cls = town.Content.Classes.FirstOrDefault(c => c.Id == "ranger") ?? town.Content.Classes[0];
                var a = Quests.CreateHero(town, cls, r.Level);
                int gap = name.IndexOf(' ');
                (a.GivenName, a.FamilyName) = gap > 0 ? (name[..gap], name[(gap + 1)..]) : (name, "");
                camp.Cleared = true;
                camp.ThoughtCleared = true;
                camp.ClearedTick = town.Tick;
                return r.Texts.GetValueOrDefault("hired", "").Replace("{ranger}", name) + $" (-{Js.Str(r.Hire)} gold)";
            case 2:
                st.Dealt = "tolerated";
                return r.Texts.GetValueOrDefault("tolerated", "").Replace("{ranger}", name);
            default:
                st.Dealt = "hunted";
                Rumors.Hear(town, camp, Rumors.Occupants(town.Content, camp.Def), camp.Def.Tier, 80);
                return r.Texts.GetValueOrDefault("hunted", "").Replace("{ranger}", name).Replace("{camp}", camp.Def.Name);
        }
    }
}
