namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// A themed stretch of the wilds (mountains, deep forest, caverns, castle ruins...) in one ring around
/// the town. The client draws it as a block of territories at <see cref="Bearing"/>, <see cref="Days"/> out.
/// </summary>
public sealed class Region
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required ThemeDef Theme { get; init; }

    /// <summary>Ring index, 0 nearest the town.</summary>
    public int Ring { get; init; }
    public int Days { get; init; }

    /// <summary>Party levels its sites are built for.</summary>
    public required int[] Levels { get; init; }

    /// <summary>Compass bearing from the town in degrees (0 north, clockwise) and the arc it spans.</summary>
    public double Bearing { get; init; }
    public double Arc { get; init; }

    /// <summary>Heard of in the taverns: heroes may choose to go there.</summary>
    public bool Known { get; set; }

    /// <summary>A party has been there and described the terrain.</summary>
    public bool Visited { get; set; }
    public int? SettlementId { get; set; }
    public List<string> SiteIds { get; } = [];
}

/// <summary>A pocket of light out in the wilds: a village, a dwarf hold, an elf enclave.</summary>
public sealed class Settlement
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required PeopleDef Kin { get; init; }
    public int RegionId { get; init; }

    /// <summary>How its people feel about the town, -100 to 100.</summary>
    public double Standing { get; set; }
    public bool Allied { get; set; }

    /// <summary>Explorers or envoys have met its people: it shows on the town's map.</summary>
    public bool Met { get; set; }

    /// <summary>Burned, emptied or bent to a menace's will (<see cref="Menaces"/>): it pleads, trades and allies no more.</summary>
    public bool Razed { get; set; }
    public int Envoys { get; set; }
    public int NextPleaDay { get; set; }

    // Its own life (<see cref="Neighbours"/>) ---------------------------------------------------

    /// <summary>Who leads it; empty until the settlement is first looked at.</summary>
    public string Leader { get; set; } = "";

    /// <summary>How well it is doing and how safe it is, 0 to 100.</summary>
    public double Prosperity { get; set; } = 50;
    public double Security { get; set; } = 50;
    public SettlementTrouble? Trouble { get; set; }
    public int NextTroubleDay { get; set; }
    public LocalBand? Band { get; set; }

    /// <summary>Projects the town has paid for there, and the one being built and when it is done.</summary>
    public List<string> Projects { get; } = [];
    public string? Works { get; set; }
    public int WorksDoneDay { get; set; }

    /// <summary>The day it last sent relief to the town.</summary>
    public int ReliefDay { get; set; } = -999;

    /// <summary>"the dwarves of Ironhold".</summary>
    public string People => Kin.Name.Replace("{S}", Name);
}

public enum PleaStatus
{
    Open,
    Taken,
    Answered,
    Lapsed,
}

/// <summary>A settlement asking for help with a site near it; it pays the heroes who clear it.</summary>
public sealed class Plea
{
    public int Id { get; init; }
    public int SettlementId { get; init; }
    public required string SiteId { get; init; }
    public double Reward { get; init; }
    public int ExpiresDay { get; init; }
    public PleaStatus Status { get; set; } = PleaStatus.Open;
    public int? PartyId { get; set; }
}

/// <summary>
/// The wilds beyond the town: rings of themed regions, each with adventure sites built for a band of
/// party levels, and the peoples who live there. The map is generated once from the seed (its own
/// stream, so the town's dice are untouched by how big the world is); after that, regions become
/// known through returning parties and allied maps, settlements plead for help, and peoples whose
/// standing rises high enough ally with the town and send it goods and heroes.
/// </summary>
public static class World
{
    public static Region? RegionOf(Town town, SiteState site) => site.RegionId is int id ? town.Regions[id] : null;

    public static Settlement? SettlementIn(Town town, Region r) => r.SettlementId is int id ? town.Settlements[id] : null;

    /// <summary>
    /// Whether the site's region is within the town's reach of talk: taverns only turn up rumours of
    /// sites in regions someone has heard of. Sites outside any region always are.
    /// </summary>
    public static bool RegionKnown(Town town, SiteState site) => RegionOf(town, site)?.Known ?? true;

    /// <summary>The town learns of a site from people who should know; a better source replaces a worse one.</summary>
    public static void Tell(Town town, SiteState site, int trust, string from)
    {
        if (site.Known == SiteKnowledge.Confirmed || (site.Known == SiteKnowledge.Rumoured && site.Trust >= trust)) return;
        Rumors.Hear(town, site, Rumors.Occupants(town.Content, site.Def), site.Def.Tier, trust);
        site.HeardFrom = from;
    }

    /// <summary>A fixed hash to [0, 1) for placing things; never the town's dice. The client's Geo.Hash is the same function.</summary>
    public static double Hash(int n)
    {
        uint x = unchecked((uint)n * 0x45d9f3bu);
        x = unchecked(((x >> 16) ^ x) * 0x45d9f3bu);
        x = (x >> 16) ^ x;
        return x / 4294967296.0;
    }

    /// <summary>
    /// Where a site lies: its compass bearing (degrees, 0 north, clockwise) and how far out it is in
    /// days of open-ground march. A region spreads its sites across its arc; sites outside any region
    /// (the war camp) sit between the hand-written ones.
    /// </summary>
    public static (double Bearing, double Reach) Place(Town town, SiteState s)
    {
        int i = town.Sites.IndexOf(s);
        double reach = s.Def.Distance * (0.82 + Hash(i * 7 + 3) * 0.3);
        if (RegionOf(town, s) is { } region)
        {
            int k = region.SiteIds.IndexOf(s.Def.Id);
            int n = region.SiteIds.Count;
            double bearing = region.Bearing + (k - (n - 1) / 2.0) * region.Arc * 0.6 / Math.Max(1, n);
            return (bearing, reach + (k % 2) * 0.14);
        }
        int baseCount = Math.Max(1, town.Content.Sites.Count);
        double slot = i < baseCount ? i : i - baseCount + 0.5;
        return (slot / baseCount * 360 + 20.05, reach);
    }

    /// <summary>The site's spot in day units from the town: x east, y south.</summary>
    public static (double X, double Y) Spot(Town town, SiteState s)
    {
        var (b, r) = Place(town, s);
        double a = b * Math.PI / 180;
        return (r * Math.Sin(a), -r * Math.Cos(a));
    }

    /// <summary>Experience for clearing a site: the encounter budget of its level.</summary>
    public static int Milestone(Town town, SiteState site)
    {
        var b = town.Content.World.Budget;
        return b.Length == 0 ? 0 : b[Math.Min(LevelOf(site), b.Length) - 1];
    }

    /// <summary>Party level a site is built for, read from its tier.</summary>
    public static int LevelOf(SiteState site) => Math.Max(1, site.Def.Tier);

    // ---- Generation ----

    public static void Generate(Town town)
    {
        var w = town.Content.World;
        if (w.Rings.Count == 0 || w.Themes.Count == 0) return;
        var rng = new Rng(town.Seed * 7919 + 104729);
        var used = new HashSet<string>(town.Sites.Select(s => s.Def.Name));

        for (int ring = 0; ring < w.Rings.Count; ring++)
        {
            var rd = w.Rings[ring];
            var allowed = w.Themes.Where(t => t.MinRing <= ring && ring <= t.MaxRing).ToList();
            double arc = 360.0 / rd.Regions;
            double offset = rng.Next() * 360;
            var picked = new List<ThemeDef>();
            for (int i = 0; i < rd.Regions; i++)
            {
                // Each ring shows as many different kinds of country as it can before repeating one.
                var fresh = allowed.Where(t => !picked.Contains(t)).ToList();
                var theme = WeightedTheme(rng, fresh.Count > 0 ? fresh : allowed.Where(t => picked.Count == 0 || t != picked[^1]).ToList());
                picked.Add(theme);
                double bearing = (offset + i * arc + (rng.Next() - 0.5) * 0.3 * arc) % 360;
                var region = new Region
                {
                    Id = town.Regions.Count,
                    Name = Fill(rng, theme, theme.Regions, used),
                    Theme = theme,
                    Ring = ring,
                    Days = rd.Days,
                    Levels = rd.Levels,
                    Bearing = Js.Round(bearing * 10) / 10.0,
                    Arc = Js.Round(arc * 10) / 10.0,
                    Known = ring == 0,
                };
                town.Regions.Add(region);
            }
        }

        PlaceAuthoredSites(town);

        foreach (var region in town.Regions)
        {
            var rd = w.Rings[region.Ring];
            int count = Math.Max(1, rng.Range(w.SitesPerRegion[0], w.SitesPerRegion[1] + 1) - region.SiteIds.Count);
            for (int i = 0; i < count; i++)
            {
                int lo = rd.Levels[0];
                int hi = rd.Levels[^1];
                // Deeper into the region, harder sites: spread the ring's band across them.
                int level = count == 1 ? rng.Range(lo, hi + 1) : lo + (int)Math.Round((hi - lo) * (double)i / (count - 1));
                var kind = rng.Pick(region.Theme.Sites);
                var def = BuildSite(town.Content, rng, region, kind, level, $"{region.Theme.Id}-{region.Id}-{i}", Fill(rng, region.Theme, kind.Names, used));
                town.Sites.Add(new SiteState(def) { RegionId = region.Id, Generated = true });
                region.SiteIds.Add(def.Id);
            }

            double chance = region.Theme.SettlementChance ?? w.SettlementChance;
            if (region.Theme.Peoples.Count > 0 && rng.Chance(chance))
            {
                // The first people named is the likeliest to live there.
                var fid = rng.Chance(0.6) ? region.Theme.Peoples[0] : rng.Pick(region.Theme.Peoples);
                var people = w.Peoples.First(f => f.Id == fid);
                var s = new Settlement
                {
                    Id = town.Settlements.Count,
                    Name = Fill(rng, region.Theme, people.Settlements, used),
                    Kin = people,
                    RegionId = region.Id,
                    Standing = people.Start,
                    NextPleaDay = 2 + rng.Range(w.Standing.PleaEveryDays[0], w.Standing.PleaEveryDays[1] + 1),
                };
                town.Settlements.Add(s);
                region.SettlementId = s.Id;
            }
        }

        // Last, so the country between the sites never shifts the regions and sites a seed makes.
        Roads.Generate(town, rng);
    }

    private static ThemeDef WeightedTheme(Rng rng, IReadOnlyList<ThemeDef> themes)
    {
        int total = themes.Sum(t => t.Weight);
        int roll = rng.Range(0, total);
        foreach (var t in themes)
        {
            if (roll < t.Weight) return t;
            roll -= t.Weight;
        }
        return themes[0];
    }

    /// <summary>A fresh name from the patterns: {A} a theme word, {B} a theme name.</summary>
    private static string Fill(Rng rng, ThemeDef theme, IReadOnlyList<string> patterns, HashSet<string> used)
    {
        string name = "";
        for (int tries = 0; tries < 30; tries++)
        {
            name = rng.Pick(patterns).Replace("{A}", rng.Pick(theme.Words)).Replace("{B}", rng.Pick(theme.Names));
            if (used.Add(name)) return name;
        }
        for (int n = 2; ; n++)
            if (used.Add($"{name} {Roman(n)}")) return $"{name} {Roman(n)}";
    }

    private static string Roman(int n) => n switch { 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString(System.Globalization.CultureInfo.InvariantCulture) };

    /// <summary>The hand-written sites go to the region whose country fits them best, as near their own distance as can be.</summary>
    private static void PlaceAuthoredSites(Town town)
    {
        foreach (var site in town.Sites)
        {
            int Fit(Region g) =>
                2 * g.Theme.Habitats.Concat(g.Theme.Sites.SelectMany(k => k.Habitats)).Distinct().Count(site.Def.Habitats.Contains) -
                3 * Math.Abs(g.Days - site.Def.Distance);
            var region = town.Regions
                .OrderByDescending(Fit)
                .ThenBy(g => g.SiteIds.Count)
                .ThenBy(g => g.Id)
                .First();
            site.RegionId = region.Id;
            region.SiteIds.Add(site.Def.Id);
        }
    }

    /// <summary>Build a site for parties of the given level from the library's creatures of its country.</summary>
    public static SiteDef BuildSite(Content content, Rng rng, Region region, SiteKindDef kind, int level, string id, string name)
    {
        var w = content.World;
        var theme = region.Theme;
        double crLo = Math.Max(0.125, level / 8.0);
        double crHi = Math.Max(0.5, level * 2 / 3.0);
        int budget = w.Budget.Length == 0 ? 100 * level : w.Budget[Math.Min(level, w.Budget.Length) - 1];
        double hoard = w.HoardPerLevel * Math.Pow(level, 1.5);
        int[] Gold(double lo, double hi) => [(int)Math.Round(hoard * lo), (int)Math.Round(hoard * hi)];
        string Text(string k) => theme.Texts.TryGetValue(k, out var lines) && lines.Count > 0 ? rng.Pick(lines) : k;

        var habitats = kind.Habitats.Count > 0 ? kind.Habitats.ToList() : theme.Habitats.ToList();
        var probe = new SiteDef { Id = id, Name = name, Boss = new EncounterDef { Kind = "combat" }, Habitats = habitats };
        if (content.Wanderers(probe, [crLo, crHi]).Count == 0)
        {
            habitats = habitats.Concat(theme.Habitats).Distinct().ToList();
            probe = probe with { Habitats = habitats };
        }

        int dc = 11 + (level + 1) / 2;
        int die = level <= 1 ? 6 : level <= 3 ? 8 : level <= 6 ? 10 : 12;
        var deck = new List<EncounterDef>();
        foreach (var k in kind.Deck)
        {
            deck.Add(k switch
            {
                "wander" => new EncounterDef { Kind = k, Cr = [crLo, crHi], Budget = budget, Text = Text(k) },
                "trap" or "hazard" => new EncounterDef { Kind = k, Dc = dc, DamageDie = die, Text = Text(k) },
                "puzzle" or "parley" => new EncounterDef { Kind = k, Dc = dc + 1, Gold = Gold(0.1, 0.25), Text = Text(k) },
                _ => new EncounterDef { Kind = "treasure", Gold = Gold(0.2, 0.5), MimicChance = Math.Min(0.25, 0.08 + 0.02 * level), Text = Text("treasure") },
            });
        }

        // The boss: a creature of this country about as strong as the party, with lesser ones at its side.
        double bLo = Math.Max(0.5, level * 0.75);
        double bHi = Math.Max(1, level);
        List<LibMonster> bosses = [];
        for (int widen = 0; widen < 12 && bosses.Count == 0; widen++)
        {
            bosses = content.Library.Monsters
                .Where(m => m.Damage != null && m.Band != "" && !m.Alignment.Contains("good") && m.Cr >= bLo && m.Cr <= bHi && m.Habitats.Any(habitats.Contains))
                .ToList();
            bLo = Math.Max(0.125, bLo - 0.5);
            bHi += 1;
        }
        if (bosses.Count == 0)
            bosses = content.Library.Monsters.Where(m => m.Damage != null && m.Band != "" && !m.Alignment.Contains("good") && m.Cr >= 0.5 && m.Cr <= Math.Max(1, level)).ToList();
        // Lair-holders first: the library marks some creatures as bosses.
        var marked = bosses.Where(m => m.Boss).ToList();
        var boss = rng.Pick(marked.Count > 0 && rng.Chance(0.5) ? marked : bosses);
        var escorts = content.Wanderers(probe, [crLo, Math.Max(crLo, Math.Min(crHi, boss.Cr / 2))]).Where(m => m.Id != boss.Id).ToList();
        var bossEnc = new EncounterDef
        {
            Kind = "combat",
            Monster = content.BossOf(boss.Id).Id,
            Count = [1, 1],
            Escort = escorts.Count > 0 ? rng.Pick(escorts).Id : null,
            EscortCount = escorts.Count > 0 ? [1, 1 + level / 3] : null,
            Text = Text("boss"),
        };

        return new SiteDef
        {
            Id = id,
            Name = name,
            Tier = level,
            Kind = Lairs.KindOf(boss.Type, ""),
            Distance = region.Days,
            Encounters = deck,
            Boss = bossEnc,
            Loot = Gold(0.7, 1.4),
            Habitats = habitats,
        };
    }

    // ---- The daily tick ----

    public static void Daily(Town town, bool autoMayor)
    {
        var w = town.Content.World;
        if (town.Regions.Count == 0) return;
        foreach (var s in town.Settlements)
        {
            // Feelings cool or warm back toward where they started.
            double d = s.Kin.Start - s.Standing;
            if (!s.Allied && !s.Razed) s.Standing += Math.Sign(d) * Math.Min(Math.Abs(d), w.Standing.Drift);
        }
        LapsePleas(town);
        PostPleas(town);
        Boons(town);
        if (autoMayor) AutoEnvoy(town);
    }

    private static void LapsePleas(Town town)
    {
        foreach (var p in town.Pleas)
        {
            if (p.Status != PleaStatus.Open || town.Day < p.ExpiresDay) continue;
            p.Status = PleaStatus.Lapsed;
            var s = town.Settlements[p.SettlementId];
            Shift(town, s, town.Content.World.Standing.PleaIgnored);
            town.Log(LedgerCategory.Quest, Importance.Routine, $"No one answers the plea from {s.Name} about {Quests.Site(town, p.SiteId)!.Def.Name}. {Cap(s.Kin.People)} will remember it.");
        }
    }

    private static void PostPleas(Town town)
    {
        var rules = town.Content.World.Standing;
        // Runners come only for trouble the town's heroes could face, a few at a time.
        int strongest = town.Heroes.Select(h => h.Hero!.Level).DefaultIfEmpty(0).Max();
        foreach (var s in town.Settlements)
        {
            var region = town.Regions[s.RegionId];
            if (!region.Known || s.Razed || town.Day < s.NextPleaDay) continue;
            if (town.Pleas.Count(p => p.Status is PleaStatus.Open or PleaStatus.Taken) >= rules.MaxOpen) break;
            s.NextPleaDay = town.Day + town.Rng.Range(rules.PleaEveryDays[0], rules.PleaEveryDays[1] + 1);
            if (town.Pleas.Any(p => p.SettlementId == s.Id && p.Status is PleaStatus.Open or PleaStatus.Taken)) continue;
            // The nearest trouble first: their own region, then the rest of their ring.
            var site = town.Sites
                .Where(x => !x.Cleared && LevelOf(x) <= strongest + 1 && x.RegionId is int rid && town.Regions[rid].Ring == region.Ring && town.Regions[rid].Known)
                .Where(x => !town.Pleas.Any(p => p.SiteId == x.Def.Id && p.Status is PleaStatus.Open or PleaStatus.Taken))
                .OrderBy(x => x.Def.Id == s.Trouble?.SiteId ? 0 : 1)
                .ThenBy(x => x.RegionId == region.Id ? 0 : 1)
                .ThenBy(x => x.Def.Tier)
                .ThenBy(x => x.Def.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (site == null) continue;
            Plead(town, s, site, 1);
        }
    }

    /// <summary>A settlement begs the town's heroes for help with a site, offering a reward (more when its own band failed).</summary>
    internal static Plea? Plead(Town town, Settlement s, SiteState site, double bonus)
    {
        var rules = town.Content.World.Standing;
        if (s.Razed || !town.Regions[s.RegionId].Known) return null;
        if (town.Pleas.Any(p => p.SiteId == site.Def.Id && p.Status is PleaStatus.Open or PleaStatus.Taken)) return null;
        double reward = Js.Round(rules.PleaRewardPerLevel * LevelOf(site) * (s.Allied ? 1.5 : 1) * bonus);
        Tell(town, site, rules.PleaTrust, $"a runner from {s.Name}");
        var plea = new Plea
        {
            Id = town.Pleas.Count + 1,
            SettlementId = s.Id,
            SiteId = site.Def.Id,
            Reward = reward,
            ExpiresDay = town.Day + rules.PleaDays,
        };
        town.Pleas.Add(plea);
        town.Log(
            LedgerCategory.Quest,
            Importance.Notable,
            $"A runner from {s.Name} reaches the taverns: {s.People} beg for help with {site.Def.Name} and offer {Js.Str(reward)} gold to whoever deals with it.");
        return plea;
    }

    /// <summary>The peoples allied with the town send goods to market and, now and then, a hero.</summary>
    private static void Boons(Town town)
    {
        foreach (var s in town.Settlements)
        {
            if (!s.Allied) continue;
            var b = s.Kin.Boon;
            if (b.Good is Good g && b.Qty > 0) town.Market.Seed(g, b.Qty * Neighbours.BoonShare(town, s));
            if (b.HeroChance <= 0 || b.Classes.Count == 0 || s.Kin.Race == "") continue;
            if (town.Taverns.Count == 0 || town.Heroes.Count >= City.HeroCap(town) || !town.Rng.Chance(b.HeroChance)) continue;
            var cls = town.Content.Classes.FirstOrDefault(c => c.Id == town.Rng.Pick(b.Classes));
            if (cls == null) continue;
            var a = Quests.CreateHero(town, cls, town.Rng.Chance(0.6) ? 2 : 3, s.Kin.Race, s.Kin.Id);
            town.Log(LedgerCategory.Arrival, Importance.Notable, $"{a.Describe()}, comes from {s.Name} with a letter from {s.People}: they have heard the town needs blades. {Annals.Past(town, a)} ({a.TraitList()})", [a.Id]);
        }
    }

    /// <summary>Stand-in mayor: every so often, send gifts to the friendliest people not yet allied.</summary>
    private static void AutoEnvoy(Town town)
    {
        var rules = town.Content.World.Standing;
        if (town.Day - town.LastEnvoyDay < rules.EnvoyEveryDays || town.Treasury < rules.EnvoyCost + 200) return;
        var target = town.Settlements
            .Where(s => !s.Allied && !s.Razed && town.Regions[s.RegionId].Known)
            .OrderByDescending(s => s.Standing)
            .ThenBy(s => s.Id)
            .FirstOrDefault();
        if (target != null) SendEnvoy(town, target.Id);
    }

    /// <summary>The player's lever: send an envoy with gifts to a settlement the town has heard of.</summary>
    public static bool SendEnvoy(Town town, int settlementId)
    {
        var rules = town.Content.World.Standing;
        if (settlementId < 0 || settlementId >= town.Settlements.Count) return false;
        var s = town.Settlements[settlementId];
        if (!town.Regions[s.RegionId].Known || s.Razed || town.Treasury < rules.EnvoyCost) return false;
        town.Book(TreasuryAccount.Envoys, -rules.EnvoyCost);
        town.LastEnvoyDay = town.Day;
        // Gifts please less each time.
        double gain = rules.Envoy / (1 + 0.25 * s.Envoys);
        s.Envoys++;
        town.Log(LedgerCategory.Town, Importance.Routine, $"The council sends an envoy with {Js.Str(rules.EnvoyCost)} gold of gifts to {s.People}.");
        Shift(town, s, gain + Sheet.Gain(town, "envoy", Sheet.Decide(town, "envoy")));
        return true;
    }

    /// <summary>Move a people's standing, and seal an alliance when it climbs high enough.</summary>
    public static void Shift(Town town, Settlement s, double delta)
    {
        if (s.Razed) return;
        s.Standing = Math.Clamp(s.Standing + delta, -100, 100);
        if (s.Allied || s.Standing < town.Content.World.Standing.Ally) return;
        s.Allied = true;
        var b = s.Kin.Boon;
        town.Log(
            LedgerCategory.Town,
            Importance.Major,
            $"{Cap(s.People)} pledge friendship to {town.Name}." +
            (b.Text != "" ? $" From now on, {b.Text.Replace("{S}", s.Name)}" : "") +
            (b.Troops > 0 ? $"{(b.Text != "" ? ", and" : " They pledge")} their warriors will come if the walls are ever attacked." : "."));
        Annals.Allied(town, s);
        if (b.Maps) Reveal(town, town.Regions[s.RegionId], $"{Cap(s.People)} share their maps", town.Content.World.Standing.MapTrust);
    }

    /// <summary>Defence the town's allies add at its walls.</summary>
    public static double AllyTroops(Town town)
    {
        double t = 0;
        foreach (var s in town.Settlements)
            if (s.Allied) t += s.Kin.Boon.Troops;
        return t + Neighbours.Troops(town);
    }

    // ---- Parties coming home ----

    /// <summary>
    /// A party is back: the region they went to is described, word of the lands beyond spreads, and the
    /// people living there remember who cleared their trouble. Returns gold a settlement pays the party.
    /// </summary>
    public static double AfterDelve(Town town, Party party, SiteState site, ExpeditionOutcome outcome)
    {
        Neighbours.AfterDelve(town, party, site, outcome);
        var plea = party.PleaId is int pid ? town.Pleas[pid - 1] : null;
        var region = RegionOf(town, site);
        if (region == null) return 0;
        double paid = 0;
        // The dead tell no tales: only a party that comes home describes the country, and only to the
        // council when it paid for the trip; anyone else must buy the report (Intel).
        if (outcome != ExpeditionOutcome.Wiped && !party.Withheld) Describe(town, party, region);
        if (outcome == ExpeditionOutcome.Cleared)
        {
            var rules = town.Content.World.Standing;
            var home = SettlementIn(town, region);
            if (home != null && (plea == null || plea.SettlementId != home.Id)) Shift(town, home, rules.Cleared);
            if (plea != null)
            {
                plea.Status = PleaStatus.Answered;
                var s = town.Settlements[plea.SettlementId];
                paid = plea.Reward;
                town.Log(LedgerCategory.Quest, Importance.Notable, $"{Cap(s.People)} pay the {party.Name} {Js.Str(plea.Reward)} gold for {site.Def.Name}, and toast {town.Name}'s heroes.");
                Shift(town, s, rules.Plea + rules.Cleared);
            }
        }
        else if (plea != null) Reopen(plea);
        return paid;
    }

    /// <summary>A party's report reaches the council: the region is described and word of the lands beyond spreads.</summary>
    public static void Describe(Town town, Party party, Region region)
    {
        bool first = !region.Visited;
        region.Visited = true;
        region.Known = true;
        // On the way they hear of the region's other sites from whoever lives there; empty country tells nothing.
        if (SettlementIn(town, region) is { Razed: false } home)
            foreach (var id in region.SiteIds) Tell(town, Quests.Site(town, id)!, town.Content.World.Standing.VisitTrust, home.People);
        if (first) Annals.Visited(town, party, region);
        if (first) Reveal(town, region, $"The {party.Name} bring back word of the lands beyond {region.Name}", 0);
    }

    /// <summary>The party that took a plea came back without clearing the site: it stands open again.</summary>
    public static void Reopen(Plea plea)
    {
        plea.Status = PleaStatus.Open;
        plea.PartyId = null;
    }

    /// <summary>Regions in the next ring out, around this one's bearing, become known by rumor.</summary>
    /// <param name="trust">When above 0, the regions' sites are heard of too, at this trust (an ally's maps).</param>
    private static void Reveal(Town town, Region from, string lead, int trust)
    {
        var news = town.Regions
            .Where(r => !r.Known && (r.Ring == from.Ring + 1 || r.Ring == from.Ring) && AngleBetween(r.Bearing, from.Bearing) <= from.Arc * 0.75 + r.Arc * 0.5)
            .ToList();
        if (news.Count == 0) return;
        foreach (var r in news)
        {
            r.Known = true;
            if (trust <= 0) continue;
            foreach (var id in r.SiteIds)
            {
                var site = Quests.Site(town, id)!;
                Tell(town, site, trust, lead);
                // An ally's map puts the place where it really is.
                site.Found = true;
            }
        }
        string lands = Expedition.ListNames(news.Select(r =>
        {
            var s = SettlementIn(town, r);
            return $"{r.Name} ({r.Theme.Name}{(s != null ? $", home to {s.People}" : "")})";
        }).ToList());
        town.Log(LedgerCategory.Quest, Importance.Notable, $"{lead}: {lands}.");
    }

    public static double AngleBetween(double a, double b)
    {
        double d = Math.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
