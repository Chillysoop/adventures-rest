namespace CrownAndCompass.Sim.Systems;

/// <summary>How an enemy's agent in town behaves and reads (crises.json "lurkers").</summary>
public sealed record LurkerRules
{
    /// <summary>The hour of the night the agent strikes, and the fewest hours it spends looking first; 0 hours strikes at once (tests).</summary>
    public int StrikeHour { get; init; } = 22;
    public int MinHours { get; init; } = 10;

    /// <summary>What the townsfolk see: a hooded pedlar, a cloaked woman, a tinker who sells nothing.</summary>
    public IReadOnlyList<string> Disguises { get; init; } = [];

    /// <summary>The Ledger when someone notices them: {disguise}, {where}.</summary>
    public string Seen { get; init; } = "";

    /// <summary>The Ledger when the mayor has them seized, by kind of blow: {guard}, {disguise}, {where}, {by}, {leader}, {victim}.</summary>
    public IReadOnlyDictionary<string, string> Caught { get; init; } = new Dictionary<string, string>();

    /// <summary>Mandate a catch wins, and tension it eases.</summary>
    public double Mandate { get; init; } = 2;
    public double Calm { get; init; } = 4;

    /// <summary>Chance an agent's coming leaves a sign the mayor hears of: one of the stories that need a lurker (<see cref="Stories.Sign"/>).</summary>
    public double Sign { get; init; } = 0.35;
}

/// <summary>An enemy's agent loose in town, looking over what it means to burn, knife or spoil before it strikes.</summary>
public sealed class Lurker
{
    public int Id { get; init; }

    /// <summary>The blow it has come to strike (<see cref="Blows"/>).</summary>
    public string Kind { get; init; } = "";
    public string Disguise { get; init; } = "";

    /// <summary>Who sent it, for the Ledger.</summary>
    public string By { get; init; } = "";
    public string Leader { get; init; } = "";
    public string Seat { get; init; } = "";

    /// <summary>What it is watching: a building (to fire, or where its victim lives or its work lies) and, for a knife, whom.</summary>
    public int? BuildingId { get; init; }
    public int? MarkId { get; init; }

    /// <summary>Where it loiters, in tiles from the town's corner.</summary>
    public double X { get; init; }
    public double Y { get; init; }
    public int SeenTick { get; init; }
    public int StrikeTick { get; init; }

    /// <summary>The blow's strength (<see cref="Blows.Strike"/>).</summary>
    public double Raiders { get; init; }
    public double Cunning { get; init; }

    /// <summary>The crisis (by id) or grudge (by foe) that sent it, to hear how it went.</summary>
    public int? CrisisId { get; init; }
    public string? Foe { get; init; }
}

/// <summary>
/// Catch them in the act, after Anno's street quests: fire, a knife in the night and sabotage are struck by an agent who
/// slips into town by day to look the place over first. The townsfolk notice someone who doesn't belong, the strip
/// says where, and the agent loiters there in the crowd until nightfall. Spot them on the map and have them seized
/// (<see cref="Seize"/>) and the blow never falls; leave them and it is struck as it always was, the watch and the
/// heroes at home meeting it. The stand-in mayor never seizes anyone, so the town it plays fares as before.
/// </summary>
public static class Lurkers
{
    private static LurkerRules Rules(Town town) => town.Content.Crises.Lurkers;

    /// <summary>Blows an agent in town strikes.</summary>
    public static bool Covert(string kind) => kind is Blows.Burn or Blows.Assassinate or Blows.Walls or Blows.Treasury or Blows.Stores or Blows.Discord;

    public static Lurker? Of(Town town, int id) => town.Lurkers.FirstOrDefault(l => l.Id == id);

    /// <summary>
    /// Send an agent in to strike a blow tonight. Returns false (strike now) when there is nothing for it to watch
    /// or the rules strike at once.
    /// </summary>
    public static bool Slip(Town town, string kind, Culprit by, double raiders, double cunning, Agent? mark, int? crisis, string? foe)
    {
        var r = Rules(town);
        if (r.MinHours <= 0 || town.Outcome != null) return false;
        var rng = town.Rng;
        Building? at;
        Agent? victim = null;
        switch (kind)
        {
            case Blows.Burn:
                at = Blows.Kindling(town);
                if (at == null) return false;
                break;
            case Blows.Assassinate:
                victim = mark is { Dead: false, Departed: false } && !mark.IsAway ? mark : Blows.Victim(town);
                if (victim == null) return false;
                at = Home(town, victim) ?? Economy.Hall(town);
                break;
            case Blows.Stores:
                at = First(town, "granary", "market") ?? Economy.Hall(town);
                break;
            case Blows.Discord:
                at = First(town, "tavern", "market") ?? Economy.Hall(town);
                break;
            case Blows.Treasury:
                at = Economy.Hall(town);
                break;
            default:
                at = null;
                break;
        }
        double x, y;
        if (at != null)
        {
            var lot = town.Lot(at.LotId);
            // Across the lane from it, not on its doorstep.
            double a = rng.Next() * Math.PI * 2, d = 1.2 + rng.Next() * 0.8;
            (x, y) = (lot.X + Math.Cos(a) * d, lot.Y + Math.Sin(a) * d);
        }
        else
        {
            // The walls: just inside the gate, or the town's edge with no wall yet.
            var (wx, wy) = Garrison.WallPoint(town, 0);
            (x, y) = (Town.Center + (wx - Town.Center) * 0.85, Town.Center + (wy - Town.Center) * 0.85);
        }
        int strike = town.Tick + r.MinHours;
        while (strike % SimTime.HoursPerDay != r.StrikeHour % SimTime.HoursPerDay) strike++;
        var l = new Lurker
        {
            Id = ++town.LurkerCount,
            Kind = kind,
            Disguise = r.Disguises.Count > 0 ? r.Disguises[rng.Range(0, r.Disguises.Count)] : "a stranger",
            By = by.Name,
            Leader = by.Leader,
            Seat = by.Seat,
            BuildingId = at?.Id,
            MarkId = victim?.Id,
            X = x,
            Y = y,
            SeenTick = town.Tick,
            StrikeTick = strike,
            Raiders = raiders,
            Cunning = cunning,
            CrisisId = crisis,
            Foe = foe,
        };
        town.Lurkers.Add(l);
        if (r.Seen != "") town.Log(LedgerCategory.War, Importance.Notable, Saga.Sentences(Fill(r.Seen, town, l)));
        if (r.Sign > 0 && town.Rng.Chance(r.Sign)) Stories.Sign(town, l);
        return true;
    }

    private static Building? First(Town town, params string[] ids)
    {
        foreach (string id in ids)
            if (town.Buildings.Where(b => b.Complete && b.Def.Id == id).OrderBy(b => b.Id).FirstOrDefault() is { } b) return b;
        return null;
    }

    private static Building? Home(Town town, Agent a) => a.HomeId is int h ? town.Buildings.FirstOrDefault(b => b.Id == h) : null;

    /// <summary>Where the agent loiters, for the Ledger and the strip: the building, or the gate.</summary>
    public static string Where(Town town, Lurker l) =>
        l.BuildingId is int b && town.Buildings.FirstOrDefault(x => x.Id == b) is { } at ? at.Name : "the " + Garrison.Place(town, 0);

    private static string Fill(string text, Town town, Lurker l) => text
        .Replace("{disguise}", l.Disguise)
        .Replace("{where}", Where(town, l))
        .Replace("{by}", l.By)
        .Replace("{leader}", l.Leader)
        .Replace("{victim}", l.MarkId is int m ? Blows.Who(town, town.Agent(m)) : "someone");

    /// <summary>Agents whose night has come strike their blow.</summary>
    public static void Hourly(Town town)
    {
        if (town.Lurkers.Count == 0) return;
        foreach (var l in town.Lurkers.Where(l => town.Tick >= l.StrikeTick).OrderBy(l => l.Id).ToList())
        {
            town.Lurkers.Remove(l);
            if (town.Outcome != null) continue;
            var at = l.BuildingId is int b ? town.Buildings.FirstOrDefault(x => x.Id == b) : null;
            var mark = l.MarkId is int m ? town.Agent(m) : null;
            bool landed = Blows.Resolve(town, l.Kind, new Culprit(l.By, l.Leader, l.Seat), l.Raiders, l.Cunning, mark, at);
            Report(town, l, landed);
        }
    }

    /// <summary>The crisis or grudge that sent the agent hears how it went.</summary>
    private static void Report(Town town, Lurker l, bool landed)
    {
        if (l.CrisisId is int c && town.Crises.All.FirstOrDefault(x => x.Id == c) is { } crisis)
        {
            if (landed) crisis.BlowsLanded++;
            else crisis.BlowsStopped++;
        }
        if (landed && l.Kind == Blows.Assassinate && l.Foe != null && l.MarkId is int m && Vengeance.Of(town, l.Foe) is { } g) g.Marked.Remove(m);
    }

    /// <summary>The mayor's word: seize the agent the townsfolk pointed out, before nightfall. Returns whether there was one to seize.</summary>
    public static bool Seize(Town town, int id)
    {
        var r = Rules(town);
        var l = Of(town, id);
        if (l == null || town.Tick >= l.StrikeTick) return false;
        town.Lurkers.Remove(l);
        var g = Blows.Guard(town);
        string text = r.Caught.GetValueOrDefault(l.Kind) ?? r.Caught.GetValueOrDefault("") ?? "";
        if (text != "") town.Log(LedgerCategory.War, Importance.Major, Saga.Sentences(Fill(text, town, l).Replace("{guard}", g.Name)), g.Id is int gid ? [gid] : []);
        town.Mandate = Math.Min(100, town.Mandate + r.Mandate);
        town.Tension = Math.Max(0, town.Tension - r.Calm);
        Report(town, l, false);
        return true;
    }
}
