namespace CrownAndCompass.Sim.Systems;

/// <summary>Whether a spark holds, who or what it is in this town ("Brunhild, a dwarf smith") and the icon of its source.</summary>
public readonly record struct Spark(bool Holds, string Why, string Icon);

/// <summary>
/// Sparks: the one way the town notices it has the makings of something, shared by research discoveries and hidden
/// institutions. A token names a source the player can see and grow: newcomers (craft:, folk:), trade (partner:,
/// routes:), what was built (building:, tier:), the library (book:, books:, wizard:, circle:), heroes (hero:,
/// heroes:), the wilds (found: an explorers' find, slain: a creature type a company brought down, cleared:, delves:),
/// minor powers (ally:), foes met or broken (foe:, broken:), peoples met (met:), Skywright relics brought home (relic:, relics:) and what is already known (known:). Why always names the person, place or thing behind it.
/// </summary>
public static class Sparks
{
    /// <summary>The icon (godot/assets/icons) for each source.</summary>
    public const string Newcomer = "population", Trader = "gold", Built = "tab-build", Book = "books", Magic = "lore",
        Hero = "heroes", Wilds = "compass", Slain = "threat", Ally = "standing", Known = "tab-library", Charter = "mandate",
        Foe = "rivals", People = "population", Relic = "item-stone";

    public static Spark Check(Town town, string token)
    {
        var p = token.Split(':');
        int N(int i) => p.Length > i && int.TryParse(p[i], out int n) ? n : 1;
        string Arg(int i) => p.Length > i ? p[i] : "";
        switch (p[0])
        {
            case "craft":
            {
                var who = town.Residents.Where(a => a.Profession == Arg(1)).OrderByDescending(a => a.Id).ToList();
                string why = who.Count == 0 ? $"{Plural(Arg(1), 2)} in town"
                    : who.Count == 1 ? $"{who[0].Name}, {Article(Arg(1))} {Arg(1)}"
                    : $"{who[0].Name} and {who.Count - 1} other {Plural(Arg(1), who.Count - 1)}";
                return new(who.Count >= N(2), why, Newcomer);
            }
            case "folk":
            {
                var who = town.Residents.Where(a => a.Race == Arg(1)).OrderByDescending(a => a.Id).ToList();
                string why = who.Count == 0 ? $"{Arg(1)} neighbours"
                    : who.Count == 1 ? $"{who[0].Name}, {Article(Arg(1))} {Arg(1)}{(who[0].Profession is { Length: > 0 } pr ? " " + pr : "")}"
                    : $"{who[0].Name} and {who.Count - 1} other {Arg(1)} {(who.Count == 2 ? "neighbour" : "neighbours")}";
                return new(who.Count >= N(2), why, Newcomer);
            }
            case "partner":
            {
                var s = Trade.Open(town).Select(r => town.Settlements[r.SettlementId]).FirstOrDefault(s => s.Kin.Id == Arg(1));
                return new(s != null, s != null ? $"the caravans to {s.Name}" : $"trade with {Arg(1)}", Trader);
            }
            case "routes":
            {
                int n = Trade.Open(town).Count();
                return new(n >= N(1), $"{n} trade {(n == 1 ? "route" : "routes")}", Trader);
            }
            case "building":
            {
                bool has = town.Buildings.Any(b => b.Def.Id == Arg(1) && b.Complete);
                return new(has, $"the town's {BuildingName(town, Arg(1))}", Built);
            }
            case "book":
            {
                var b = town.Research.Books.Where(b => b.School == Arg(1) && b.Circle >= N(2)).OrderBy(b => b.Id).FirstOrDefault();
                return new(b != null, b != null ? b.Name : $"a book of {Arg(1)} of the {Research.Ordinal(N(2))} circle", Book);
            }
            case "books":
                return new(town.Research.Books.Count >= N(1), "the library's books", Book);
            case "wizard":
            {
                var w = town.Heroes.Where(a => a.Hero!.Cls.Id == "wizard" && a.Hero.Level >= N(1)).OrderBy(a => a.Id).FirstOrDefault();
                return new(w != null, w != null ? $"{w.Name} the wizard" : $"a wizard of level {N(1)}", Magic);
            }
            case "circle":
                return new(Research.Circle(town) >= N(1), $"wizards who can cast the {Research.Ordinal(N(1))} circle", Magic);
            case "hero":
            {
                int level = p.Length > 2 ? N(2) : 1;
                var h = town.Heroes.Where(a => (Arg(1) == "any" || a.Hero!.Cls.Id == Arg(1)) && a.Hero!.Level >= level).OrderBy(a => a.Id).FirstOrDefault();
                return new(h != null, h != null ? h.Name : Arg(1) == "any" ? "a hero" : $"a {Arg(1)}", Hero);
            }
            case "heroes":
            {
                int n = town.Heroes.Count;
                return new(n >= N(1), $"{n} {(n == 1 ? "hero" : "heroes")} in town", Hero);
            }
            case "delves":
            {
                int n = town.Parties.Count(x => x.Returned && x.Result?.Outcome == ExpeditionOutcome.Cleared);
                return new(n >= N(1), $"{n} cleared {(n == 1 ? "delve" : "delves")}", Wilds);
            }
            case "cleared":
            {
                var site = town.Sites.Where(s => s.Cleared && s.Def.Tier >= N(1)).OrderByDescending(s => s.Def.Tier).ThenBy(s => s.Def.Id, StringComparer.Ordinal).FirstOrDefault();
                return new(site != null, site != null ? site.Def.Name : $"a cleared site of tier {N(1)}", Wilds);
            }
            case "found":
            {
                var finds = town.Finds.Where(f => f.Known && f.Kind == Arg(1)).OrderByDescending(f => f.Id).ToList();
                return new(finds.Count >= N(2), finds.Count > 0 ? $"explorers' word of {Explore.What(town, finds[0])}" : $"a {Arg(1)} found in the wilds", Wilds);
            }
            case "slain":
            {
                int n = town.Research.Slain.Get(Arg(1), 0);
                string why = town.Research.Slayers.TryGetValue(Arg(1), out string? by) ? by : $"a slain {Arg(1)}";
                return new(n >= N(2), why, Slain);
            }
            case "ally":
            {
                var def = town.Content.Powers.Powers.FirstOrDefault(x => x.Id == Arg(1));
                // Reads only: an allied power, never the purchase of one.
                return new(def != null && Powers.Find(town, Arg(1)) is { Side: PowerSide.Town }, def != null ? Powers.Name(town, def) : Arg(1), Ally);
            }
            case "foe":
            {
                // A menace of this lineage the town knows of, alive or broken.
                var m = town.Saga.Menaces.Where(x => x.Lineage == Arg(1) && Discovery.Known(town, x)).OrderBy(x => x.Id).FirstOrDefault();
                return new(m != null, m != null ? m.Name : Arg(1), Foe);
            }
            case "broken":
            {
                var m = town.Saga.Menaces.Where(x => x.Lineage == Arg(1) && !x.Alive).OrderBy(x => x.Id).FirstOrDefault();
                return new(m != null, m != null ? $"the fall of {m.Name}" : Arg(1), Foe);
            }
            case "met":
            {
                var s = town.Settlements.Where(x => x.Met && x.Kin.Id == Arg(1)).OrderBy(x => x.Id).FirstOrDefault();
                return new(s != null, s != null ? $"the folk of {s.Name}" : Arg(1), People);
            }
            case "relic":
            {
                var def = Lofted.Relic(town, Arg(1));
                return new(Lofted.Holds(town, Arg(1)), def?.Name ?? Arg(1), Relic);
            }
            case "relics":
            {
                int n = Lofted.Held(town);
                return new(n >= N(1), $"{n} Skywright {(n == 1 ? "relic" : "relics")}", Relic);
            }
            case "seam":
            {
                var s = Crystal.Seams(town).FirstOrDefault();
                return new(s != null, s != null ? $"the crystal seam on {s.Def.Name}" : "a seam of lift-crystal", Wilds);
            }
            case "known":
                return new(Research.IsKnown(town, Arg(1)), town.Content.Research.Discovery(Arg(1))?.Name.ToLowerInvariant() ?? Arg(1), Known);
            case "tier":
                return new(City.Reached(town, Arg(1)), $"the charter of a {City.TierName(town, Arg(1)).ToLowerInvariant()}", Charter);
            default:
                return new(false, token, Known);
        }
    }

    /// <summary>The creature type ("dragon", "undead") of a monster the content knows, from the library.</summary>
    public static string CreatureType(Town town, string monster)
    {
        if (monster.Length == 0) return "";
        MonsterDef m;
        try { m = town.Content.Monster(monster); }
        catch (KeyNotFoundException) { return ""; }
        var lm = town.Content.Library.Monster(m.Ref.Length > 0 ? m.Ref : monster);
        return lm?.Type.ToLowerInvariant() ?? (m.Undead ? "undead" : "");
    }

    public static string BuildingName(Town town, string building) =>
        town.Content.Buildings.FirstOrDefault(b => b.Id == building)?.Name.ToLowerInvariant() ?? building;

    public static string Plural(string s, int n) => n == 1 ? s : s.EndsWith('y') ? s[..^1] + "ies" : s + "s";

    private static string Article(string s) => s.Length > 0 && "aeiou".Contains(s[0]) ? "an" : "a";
}
