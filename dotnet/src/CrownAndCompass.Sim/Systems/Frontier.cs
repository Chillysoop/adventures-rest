namespace CrownAndCompass.Sim.Systems;

/// <summary>One way the gate is kept (frontier.json "gates"): open, watched or shut.</summary>
public sealed record GateDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Blurb { get; init; } = "";

    /// <summary>Share of strangers and enemy agents the gate turns away.</summary>
    public double Turned { get; init; }

    /// <summary>Added to the town's draw on settlers: a closed gate puts newcomers off.</summary>
    public double Draw { get; init; }

    /// <summary>Watchmen the gate keeps off the walls at night.</summary>
    public int Guards { get; init; }
}

/// <summary>The town's frontier: walls that need men on them, a gate that keeps strangers out, fields beyond (content/game/frontier.json).</summary>
public sealed record FrontierRules
{
    /// <summary>Fields inside a standing wall yield this share: there is never room enough for the plough.</summary>
    public double InsideFarm { get; init; } = 1;

    /// <summary>Wall, in tiles, one watchman holds at night; less than that and the wall gives only part of its strength.</summary>
    public double TilesPerGuard { get; init; } = 10;

    /// <summary>Share of a wall's strength left with nobody on it.</summary>
    public double Unmanned { get; init; } = 1;

    /// <summary>Below this share manned the Captain asks for more watch and the alert stands.</summary>
    public double WantManned { get; init; } = 0.8;

    /// <summary>Chance a field beyond the walls with no guard over it is stripped tonight, at full danger.</summary>
    public double FieldChance { get; init; }

    /// <summary>Days of a farm's food lost when its field is stripped.</summary>
    public double FieldDays { get; init; } = 1;

    /// <summary>Danger to the fields: each stage of the warband from probing on, and each hostile menace alive.</summary>
    public double DangerPerStage { get; init; } = 0.25;
    public double DangerPerMenace { get; init; } = 0.15;

    /// <summary>Beasts and outlaws drawn by a big town's fields whatever else is abroad: this much per thousand people, up to dangerMost.</summary>
    public double DangerPerThousand { get; init; }
    public double DangerMost { get; init; } = 1;

    /// <summary>People above which an open town draws footpads, cutpurses and worse.</summary>
    public int StrangersFrom { get; init; } = 100000;

    /// <summary>One incident a day for this many people, with the gates wide.</summary>
    public double StrangerPeople { get; init; } = 400;

    /// <summary>Share of a victim's coin the stranger takes, and Mandate lost a time.</summary>
    public double StrangerTake { get; init; } = 0.15;
    public double StrangerMandate { get; init; } = 0.5;

    /// <summary>Raiders come in greater numbers for a rich town: times 1 + lure × ln(1 + treasury / lureGold).</summary>
    public double Lure { get; init; }
    public double LureGold { get; init; } = 2000;

    /// <summary>The mood a town expects rises by a point for this many people, up to expectMost.</summary>
    public double ExpectPeople { get; init; } = 1e9;
    public double ExpectMost { get; init; }

    public IReadOnlyList<GateDef> Gates { get; init; } = [];

    public GateDef Gate(string id) => Gates.FirstOrDefault(g => g.Id == id) ?? Gates.FirstOrDefault() ?? new GateDef { Id = "open" };
}

public sealed class FrontierState
{
    /// <summary>How the gate is kept: a <see cref="GateDef"/> id.</summary>
    public string Gate { get; set; } = "open";

    /// <summary>Yesterday's fields stripped beyond the walls and the food lost, for the alert and the Defence window.</summary>
    public int FieldsLost { get; set; }
    public double FoodLost { get; set; }

    /// <summary>Strangers who did harm in town yesterday, and those the gate turned away.</summary>
    public int Strangers { get; set; }
    public int Turned { get; set; }
}

/// <summary>
/// The frontier: what makes walls worth building and keeping. Fields want open land, so a walled town farms
/// beyond its walls, where raiders and beasts strip any field no guard stands over. A wall is only as strong
/// as the watch on it, and a long wall wants a big watch. With no wall and no gate anyone walks in, and a big
/// open town draws footpads and worse; a wall's gate, kept watched or shut, turns them away, along with the
/// foe's agents, at the cost of fewer newcomers. And a rich town draws bigger raids.
/// </summary>
public static class Frontier
{
    public static FrontierRules Rules(Town town) => town.Content.Frontier;

    /// <summary>The outermost ring whose wall stands (palisade, stone or the city wall), or -1.</summary>
    public static int Ring(Town town) => WallLines.Standing(town, 1) ? 1 : WallLines.Standing(town, 0) ? 0 : -1;

    public static bool Walled(Town town) => Ring(town) >= 0;

    /// <summary>Whether a point lies within the town's outermost standing wall.</summary>
    public static bool Within(Town town, double x, double y)
    {
        int ring = Ring(town);
        if (ring < 0) return false;
        double dx = x - Town.Center, dy = y - Town.Center;
        return Math.Sqrt(dx * dx + dy * dy) < City.Wall(town, ring, Math.Atan2(dy, dx));
    }

    public static bool Within(Town town, Building b) => Within(town, town.Lot(b.LotId).X, town.Lot(b.LotId).Y);

    /// <summary>How the gate is kept now; open whatever the order while there is no wall to keep it in.</summary>
    public static GateDef Gate(Town town) => Rules(town).Gate(Walled(town) ? town.Frontier.Gate : "open");

    /// <summary>The player's order for the gate. Watched and shut need a standing wall.</summary>
    public static string? GateBlocked(Town town, string id)
    {
        if (Rules(town).Gates.All(g => g.Id != id)) return "no such order";
        if (id != "open" && !Walled(town)) return "needs a wall with a gate";
        return null;
    }

    public static bool SetGate(Town town, string id)
    {
        if (GateBlocked(town, id) != null || town.Frontier.Gate == id) return false;
        town.Frontier.Gate = id;
        var g = Rules(town).Gate(id);
        town.Log(LedgerCategory.Council, Importance.Notable, id switch
        {
            "shut" => "The mayor orders the gates shut. Nobody comes in but on the town's business.",
            "watched" => "The mayor sets the watch on the gates: every stranger is asked their name and their business.",
            _ => "The mayor throws the gates open: any traveller is welcome in.",
        });
        return true;
    }

    /// <summary>Wall length, in tiles, of the outermost standing wall.</summary>
    public static double Length(Town town)
    {
        int ring = Ring(town);
        if (ring < 0) return 0;
        const int steps = 32;
        double total = 0, px = 0, py = 0;
        for (int i = 0; i <= steps; i++)
        {
            double a = i * Math.PI * 2 / steps, r = City.Wall(town, ring, a);
            double x = Math.Cos(a) * r, y = Math.Sin(a) * r;
            if (i > 0) total += Js.Hypot(x - px, y - py);
            (px, py) = (x, y);
        }
        return total;
    }

    /// <summary>Watchmen the wall wants at night.</summary>
    public static int Wanted(Town town) => Walled(town) ? (int)Math.Ceiling(Length(town) / Rules(town).TilesPerGuard) : 0;

    /// <summary>Watchmen on the wall at night: the night watch, less those the gate keeps.</summary>
    public static double OnWalls(Town town) => Math.Max(0, Garrison.NightWatch(town) - Gate(town).Guards);

    /// <summary>Share of the wall manned at night, 0 to 1; 1 with no wall.</summary>
    public static double Manned(Town town)
    {
        int want = Wanted(town);
        return want <= 0 ? 1 : Math.Clamp(OnWalls(town) / want, 0, 1);
    }

    /// <summary>The share of a wall's strength the town gets from it, by how well it is manned.</summary>
    public static double WallShare(Town town)
    {
        var r = Rules(town);
        return r.Unmanned + (1 - r.Unmanned) * Manned(town);
    }

    /// <summary>What a field's harvest is worth where it stands: cramped within the walls.</summary>
    public static double FarmYield(Town town, Building b) => b.Def.Zone == Zone.Farmland && Within(town, b) ? Rules(town).InsideFarm : 1;

    /// <summary>Raiders' numbers for a town this rich.</summary>
    public static double Lure(Town town)
    {
        var r = Rules(town);
        return 1 + r.Lure * Math.Log(1 + Math.Max(0, town.Treasury) / r.LureGold);
    }

    /// <summary>How dangerous the country round the fields is, 0 to 1: the warband, hostile menaces, and the town's own size and wealth.</summary>
    public static double Danger(Town town)
    {
        var r = Rules(town);
        double d = 0;
        var stage = town.Enemy.Stage;
        if (stage is ThreatStage.Probing or ThreatStage.Raids or ThreatStage.Marching) d += r.DangerPerStage * ((int)stage - (int)ThreatStage.Omens);
        foreach (var m in town.Saga.Menaces)
            if (m.Alive && Menaces.Hostile(town, m)) d += r.DangerPerMenace;
        d += Math.Min(r.DangerMost, town.Population / 1000.0 * r.DangerPerThousand);
        return Math.Clamp(d * Lure(town) * Storyteller.RaidStrength(town), 0, 1);
    }

    /// <summary>Fields beyond the walls that no guard flag stands over.</summary>
    public static List<Building> OpenFields(Town town) =>
        town.Buildings.Where(b => b.Complete && b.Def.Zone == Zone.Farmland && !Within(town, b) && !Garrison.Covered(town, b)).OrderBy(b => b.Id).ToList();

    /// <summary>The share of strangers and agents the gate turns away.</summary>
    public static double Turned(Town town) => Gate(town).Turned;

    /// <summary>Added to the town's draw on settlers by how the gate is kept.</summary>
    public static double Draw(Town town) => Gate(town).Draw;

    /// <summary>Mood points the townsfolk expect before they credit the mayor: a big town wants more of its mayor.</summary>
    public static double Expect(Town town) => Math.Min(Rules(town).ExpectMost, town.Population / Rules(town).ExpectPeople);

    /// <summary>A foe's agent comes to the gate: whether it is turned away there.</summary>
    public static bool StopsAgent(Town town, Culprit by)
    {
        double t = Turned(town);
        if (t <= 0 || !town.Rng.Chance(t)) return false;
        town.Frontier.Turned++;
        town.Log(LedgerCategory.War, Importance.Routine, $"The gate watch turns away a stranger who cannot say their business. The town will never know they came from {by.Name}.");
        return true;
    }

    /// <summary>The day on the frontier: fields stripped beyond the walls, strangers at work in an open town.</summary>
    public static void Daily(Town town, bool auto)
    {
        var r = Rules(town);
        var s = town.Frontier;
        var rng = town.Rng;
        s.FieldsLost = 0;
        s.FoodLost = 0;
        double danger = Danger(town);
        if (danger > 0 && r.FieldChance > 0)
        {
            foreach (var b in OpenFields(town))
            {
                if (!rng.Chance(r.FieldChance * danger)) continue;
                double food = b.Def.Produces.Where(p => p.Key == Good.Food).Sum(p => p.Value) * b.Workers.Count * 8 * r.FieldDays;
                double lost = Math.Min(town.Market.Stock(Good.Food), food);
                if (lost <= 0) continue;
                town.Market.Seed(Good.Food, -lost);
                s.FieldsLost++;
                s.FoodLost += lost;
            }
            if (s.FieldsLost > 0)
                town.Log(LedgerCategory.War, Importance.Routine, $"Raiders and beasts strip {s.FieldsLost} {(s.FieldsLost == 1 ? "field" : "fields")} beyond the walls in the night: {Js.Str(Js.Round(s.FoodLost))} loads of food lost. No guard stood over them.");
        }
        Strangers(town);
        if (auto) Steward(town);
    }

    /// <summary>
    /// The stand-in mayor keeps the frontier: a guard flag over the fields that lost most, and the gate watched once
    /// strangers come through it.
    /// </summary>
    private static void Steward(Town town)
    {
        var s = town.Frontier;
        if (s.Strangers > 0 && Walled(town) && s.Gate == "open") SetGate(town, "watched");
        if (s.FieldsLost == 0 || town.Watch < 4) return;
        var fields = OpenFields(town);
        if (fields.Count == 0) return;
        double reach = town.Content.Garrison.FlagRadius;
        // The field with the most open fields round it, ties to the oldest.
        var best = fields
            .Select(b => (b, n: fields.Count(o => Js.Hypot(town.Lot(o.LotId).X - town.Lot(b.LotId).X, town.Lot(o.LotId).Y - town.Lot(b.LotId).Y) <= reach)))
            .OrderByDescending(x => x.n).ThenBy(x => x.b.Id).First().b;
        var lot = town.Lot(best.LotId);
        if (Garrison.FlagBlocked(town, lot.X, lot.Y) == null) Garrison.PlantFlag(town, lot.X, lot.Y);
    }

    private static void Strangers(Town town)
    {
        var r = Rules(town);
        var s = town.Frontier;
        var rng = town.Rng;
        s.Strangers = 0;
        int pop = town.Population;
        if (pop < r.StrangersFrom) return;
        double expected = (pop - r.StrangersFrom) / r.StrangerPeople * (1 - Turned(town));
        int n = (int)Math.Floor(expected) + (rng.Chance(expected - Math.Floor(expected)) ? 1 : 0);
        if (n <= 0) return;
        // The watch catches some of them at it.
        double caught = Math.Min(0.6, town.Watch / Math.Max(1.0, pop / 25.0) * 0.3);
        var marks = town.Buildings.Where(b => b.Complete && b.Def.IsBusiness && !b.Def.Civic && b.Cash > 20).OrderBy(b => b.Id).ToList();
        double stolen = 0;
        for (int i = 0; i < n && marks.Count > 0; i++)
        {
            if (rng.Chance(caught)) continue;
            var b = marks[rng.Range(0, marks.Count)];
            double take = Math.Floor(b.Cash * r.StrangerTake);
            b.Cash -= take;
            stolen += take;
            s.Strangers++;
            town.Mandate = Math.Max(0, town.Mandate - r.StrangerMandate);
        }
        if (s.Strangers > 0)
            town.Log(LedgerCategory.Hardship, Importance.Routine, $"Strangers through the {(Walled(town) ? "open gate" : "open streets")}: {s.Strangers} {(s.Strangers == 1 ? "shop is" : "shops are")} robbed, {Js.Str(stolen)} gold gone. {(Walled(town) ? "A watched gate would turn most of them away." : "A wall with a gate would let the watch ask their business.")}");
    }
}
