namespace CrownAndCompass.Sim.Systems;

/// <summary>Something a menace's strength rests on: a war chest, a supply camp, a recruiter, a hoard.</summary>
public sealed record AssetDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Menace kinds that keep it; empty for any.</summary>
    public IReadOnlyList<string> Kinds { get; init; } = [];

    /// <summary>Growth it adds to the menace, as a share.</summary>
    public double Growth { get; init; } = 0.1;

    /// <summary>Gold the town takes when it is seized.</summary>
    public int[] Gold { get; init; } = [];

    /// <summary>Seized, it is a magic item for the town hall.</summary>
    public bool Relic { get; init; }

    /// <summary>The least wits tier that keeps it (a phylactery takes a mastermind); empty for any.</summary>
    public string MinCunning { get; init; } = "";

    /// <summary>Hidden far from the menace, never at its seat: a site somewhere in the wilds the town must hunt for.</summary>
    public bool Far { get; init; }

    /// <summary>While it stands the menace cannot end: beaten at its last seat, it reforms where this lies.</summary>
    public bool Undying { get; init; }
}

/// <summary>What a menace is working toward: something the town can learn of and get in the way of.</summary>
public sealed record PlanDef
{
    public string Id { get; init; } = "";
    public IReadOnlyList<string> Kinds { get; init; } = [];

    /// <summary>Ambitions it serves; empty for any.</summary>
    public IReadOnlyList<string> Ambitions { get; init; } = [];
    public int Days { get; init; } = 30;

    /// <summary>"settlement" (any near it), "poor" (a struggling one near it), "town" or "none".</summary>
    public string Target { get; init; } = "none";

    /// <summary>toll, sway, drive, camp, ritual, raid or recruit.</summary>
    public string Effect { get; init; } = "";

    /// <summary>What stops it at the settlement: "security" (a safe one) or "prosperity" (a thriving one); empty for nothing.</summary>
    public string Foil { get; init; } = "";
    public string Text { get; init; } = "";
    public string Done { get; init; } = "";
    public string Foiled { get; init; } = "";
}

/// <summary>Villains' plans and what they rest on (saga.json menaces.plots).</summary>
public sealed record PlotRules
{
    public int[] FirstDays { get; init; } = [15, 30];
    public int[] EveryDays { get; init; } = [30, 55];

    /// <summary>Chance a new holding keeps one of its assets.</summary>
    public double HoldingAsset { get; init; } = 0.5;
    public double FoilSecurity { get; init; } = 70;
    public double FoilProsperity { get; init; } = 50;

    /// <summary>Days a plan loses when the town takes one of its holdings.</summary>
    public int SetbackDays { get; init; } = 12;

    /// <summary>Daily chance word of a named menace's plan reaches the town on its own.</summary>
    public double LearnChance { get; init; } = 0.04;

    /// <summary>Regard a contact near it needs to warn the town.</summary>
    public double LearnRegard { get; init; } = 40;

    /// <summary>Days before it is done that a known plan raises an alert.</summary>
    public int WarnDays { get; init; } = 8;
    public double LieutenantChance { get; init; } = 0.4;

    /// <summary>Menace kinds with lieutenants worth the name: a beast's brood scatters, it does not plot.</summary>
    public IReadOnlyList<string> LieutenantKinds { get; init; } = [];

    /// <summary>Kinds too great to plot in secret: once named, the town watches every plan from the start.</summary>
    public IReadOnlyList<string> Loud { get; init; } = [];

    /// <summary>Told when a menace that cannot die reforms where its vessel lies: {leader}, {seat}, {asset}, {region}.</summary>
    public string Reformed { get; init; } = "";

    /// <summary>Raid chance added on the road a menace holds.</summary>
    public double TollRaid { get; init; } = 0.25;
    public IReadOnlyList<AssetDef> Assets { get; init; } = [];
    public IReadOnlyList<PlanDef> Plans { get; init; } = [];
    public string Seized { get; init; } = "";
    public string SeizedGold { get; init; } = "";
    public string SeizedRelic { get; init; } = "";
    public string Carried { get; init; } = "";
    public string Lieutenant { get; init; } = "";
    public string Learned { get; init; } = "";
    public string Warned { get; init; } = "";

    public AssetDef? Asset(string id) => Assets.FirstOrDefault(a => a.Id == id);
    public PlanDef? Plan(string id) => Plans.FirstOrDefault(p => p.Id == id);
}

/// <summary>An asset a menace keeps at one of its sites.</summary>
public sealed class MenaceAsset
{
    public string Def { get; set; } = "";
    public string SiteId { get; set; } = "";
}

/// <summary>A plan a menace is working on.</summary>
public sealed class MenacePlan
{
    public string Def { get; set; } = "";
    public int? SettlementId { get; set; }
    public int StartDay { get; set; }
    public int DueDay { get; set; }

    /// <summary>The town has learned of it.</summary>
    public bool Known { get; set; }

    /// <summary>The town has been warned it is half done.</summary>
    public bool Portent { get; set; }
}

/// <summary>
/// What makes a villain more than a name: a current plan the town can learn of and get in the way of
/// ("buying boats to seize the river crossing"), and assets its strength rests on, each kept at one of
/// its sites. Clearing a holding takes what is kept there (the war chest's gold comes home, a stolen
/// relic goes to the town hall) and sets its plan back. A leader who flees carries the seat's assets to
/// the refuge; one who dies may have a lieutenant who slips away with the best of them and becomes the
/// next name to fear. Plans that finish leave lasting marks: a road held and tolled, a settlement
/// swayed, beasts driven toward the town.
/// </summary>
public static class Plots
{
    private static PlotRules Rules(Town town) => town.Content.Saga.Menaces.Plots;

    public static PlanDef? PlanOf(Town town, Menace m) => m.Plan is { } p ? Rules(town).Plan(p.Def) : null;

    public static AssetDef? AssetOf(Town town, MenaceAsset a) => Rules(town).Asset(a.Def);

    /// <summary>What the menace's plan is, told as the town would hear it.</summary>
    public static string Describe(Town town, Menace m) =>
        PlanOf(town, m) is { } d ? Fill(town, m, d.Text) : "";

    /// <summary>Growth the menace's assets add, as a multiplier.</summary>
    public static double Growth(Town town, Menace m)
    {
        double g = 1;
        foreach (var a in m.Assets) g += AssetOf(town, a)?.Growth ?? 0;
        return g;
    }

    /// <summary>The living menace that holds the road to this settlement, if any.</summary>
    public static Menace? Tolled(Town town, Settlement s) => town.Saga.Menaces.FirstOrDefault(m => m.Alive && m.TollOn == s.Id);

    /// <summary>Raid chance a held road adds.</summary>
    public static double TollRaid(Town town, Settlement s) => Tolled(town, s) != null ? Rules(town).TollRaid : 0;

    // ---- Assets ----

    /// <summary>A new menace keeps one asset at its seat, of a kind that suits it.</summary>
    internal static void Arm(Town town, Menace m)
    {
        if (m.Assets.Count > 0 || m.Origin == Menaces.FromWarband) return;
        var def = Pick(town, m);
        if (def != null) m.Assets.Add(new MenaceAsset { Def = def.Id, SiteId = def.Far && Hideout(town, m) is { } far ? far.Def.Id : m.SeatId });
        m.NextPlanDay = town.Day + town.Rng.Range(Rules(town).FirstDays[0], Rules(town).FirstDays[1] + 1);
    }

    /// <summary>A menace took in a holding: half the time it keeps something there.</summary>
    internal static void Stock(Town town, Menace m, SiteState holding)
    {
        if (!town.Rng.Chance(Rules(town).HoldingAsset)) return;
        var def = Pick(town, m, holding: true);
        if (def != null) m.Assets.Add(new MenaceAsset { Def = def.Id, SiteId = holding.Def.Id });
    }

    private static AssetDef? Pick(Town town, Menace m, bool holding = false)
    {
        var have = m.Assets.Select(a => a.Def).ToHashSet();
        int wits = Menaces.CunningRank(town, m.Cunning);
        var fit = Rules(town).Assets
            .Where(a => (a.Kinds.Count == 0 || a.Kinds.Contains(m.Kind)) && !have.Contains(a.Id))
            .Where(a => a.MinCunning == "" || wits >= Menaces.CunningRank(town, a.MinCunning))
            .Where(a => !holding || !a.Far)
            .ToList();
        return fit.Count == 0 ? null : fit[town.Rng.Range(0, fit.Count)];
    }

    /// <summary>Where a menace hides what it cannot lose: a free site in another region, as far from its seat as can be.</summary>
    private static SiteState? Hideout(Town town, Menace m)
    {
        var seat = Quests.Site(town, m.SeatId);
        int ring = seat?.RegionId is int rid ? town.Regions[rid].Ring : 0;
        var free = town.Sites.Where(x => x.Generated && x.RegionId != seat?.RegionId && Menaces.Free(town, x)).ToList();
        if (free.Count == 0) return null;
        // The farthest rings first, then any: dice pick among them.
        int far = free.Max(x => town.Regions[x.RegionId!.Value].Ring);
        var pool = free.Where(x => town.Regions[x.RegionId!.Value].Ring >= Math.Max(ring, far - 1)).OrderBy(x => x.Def.Id, StringComparer.Ordinal).ToList();
        return pool[town.Rng.Range(0, pool.Count)];
    }

    /// <summary>What keeps it from dying, if it stands anywhere but the seat that just fell.</summary>
    internal static SiteState? Undying(Town town, Menace m, SiteState fallen) =>
        m.Assets
            .Where(a => AssetOf(town, a) is { Undying: true } && a.SiteId != fallen.Def.Id)
            .Select(a => Quests.Site(town, a.SiteId))
            .FirstOrDefault(x => x is { Cleared: false });

    /// <summary>
    /// Beaten at its last seat, a menace that cannot die gathers itself where its vessel lies. The town learns
    /// the region it fled to, and only a rumour of the place: the hunt is on.
    /// </summary>
    internal static void Reform(Town town, Menace m, SiteState fallen, SiteState vessel)
    {
        Seize(town, m, fallen);
        var a = m.Assets.First(x => x.SiteId == vessel.Def.Id && AssetOf(town, x) is { Undying: true });
        if (World.RegionOf(town, vessel) is { } region) region.Known = true;
        if (vessel.Known == SiteKnowledge.Unheard) Rumors.Hear(town, vessel, Rumors.Occupants(town.Content, vessel.Def), vessel.Def.Tier, 30);
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Fill(town, m, Rules(town).Reformed)
            .Replace("{seat}", fallen.Def.Name).Replace("{asset}", AssetOf(town, a)!.Name).Replace("{region}", World.RegionOf(town, vessel)?.Name ?? "the wilds")));
    }

    /// <summary>A site of the menace's was cleared: whatever it kept there is the town's, and its plan is set back.</summary>
    internal static void Seize(Town town, Menace m, SiteState site)
    {
        var r = Rules(town);
        var taken = m.Assets.Where(a => a.SiteId == site.Def.Id).ToList();
        if (taken.Count == 0) return;
        foreach (var a in taken)
        {
            m.Assets.Remove(a);
            var def = AssetOf(town, a);
            if (def == null) continue;
            string text;
            if (def.Gold.Length == 2)
            {
                double gold = town.Rng.Range(def.Gold[0], def.Gold[1] + 1);
                town.Book(TreasuryAccount.Region, gold);
                text = r.SeizedGold.Replace("{gold}", Js.Str(gold));
            }
            else if (def.Relic && RelicFor(town, site) is { } item)
            {
                text = r.SeizedRelic;
                Watch.ReceiveRelic(town, item, $"heroes who cleared {site.Def.Name}");
                Spoils.Found(town, item, "the heroes", true, null, site.Def.Id, []);
            }
            else text = r.Seized;
            if (m.Named) town.Log(LedgerCategory.War, Importance.Notable, Saga.Cap(Fill(town, m, text).Replace("{site}", site.Def.Name).Replace("{asset}", def.Name)));
        }
        if (m.Plan is { } p) p.DueDay += r.SetbackDays;
    }

    /// <summary>A rare magic item the town hall doesn't hold yet, about as fine as the site was hard.</summary>
    internal static LibItem? RelicFor(Town town, SiteState site)
    {
        string rarity = site.Def.Tier >= 9 ? "very-rare" : site.Def.Tier >= 5 ? "rare" : "uncommon";
        var items = town.Content.Library.Items
            .Where(i => i.Rarity == rarity && !town.Relics.Contains(i.Id) && !Regalia.Is(town, i))
            .OrderBy(i => i.Id, StringComparer.Ordinal)
            .ToList();
        return items.Count == 0 ? null : items[town.Rng.Range(0, items.Count)];
    }

    /// <summary>
    /// The seat fell. A leader who flees to a refuge carries the seat's assets there; an heir who takes over
    /// finds them gone, and the town has them.
    /// </summary>
    internal static void SeatFell(Town town, Menace m, SiteState seat, SiteState refuge, bool escaped)
    {
        var here = m.Assets.Where(a => a.SiteId == seat.Def.Id).ToList();
        if (here.Count == 0) return;
        if (!escaped)
        {
            Seize(town, m, seat);
            return;
        }
        foreach (var a in here) a.SiteId = refuge.Def.Id;
        string names = Expedition.ListNames(here.Select(a => AssetOf(town, a)?.Name ?? a.Def).ToList());
        if (m.Named) town.Log(LedgerCategory.War, Importance.Notable, Saga.Cap(Fill(town, m, Rules(town).Carried).Replace("{refuge}", refuge.Def.Name).Replace("{assets}", names)));
    }

    /// <summary>
    /// The menace ends with its seat: what it kept there is seized, unless its lieutenant slips away with the
    /// best of it and becomes a menace of their own.
    /// </summary>
    internal static void LastStand(Town town, Menace m, SiteState seat)
    {
        var r = Rules(town);
        var here = m.Assets.Where(a => a.SiteId == seat.Def.Id).ToList();
        if (here.Count == 0) return;
        var best = here.OrderByDescending(a => AssetOf(town, a)?.Growth ?? 0).ThenBy(a => a.Def, StringComparer.Ordinal).First();
        var rules = town.Content.Saga.Menaces;
        bool room = Menaces.Alive(town).Count(x => x.Origin != Menaces.FromWarband && x != m) < rules.MaxByAct[Math.Min(town.Saga.Act, rules.MaxByAct.Length - 1)];
        // Only a menace that rose to be a power has a lieutenant who could carry on, and only once in a line.
        if (!m.BigBad && m.Named && room && m.Origin != "lieutenant" && m.Peak >= MenaceStage.Power && r.LieutenantKinds.Contains(m.Kind) && town.Rng.Chance(r.LieutenantChance))
        {
            m.Assets.Remove(best);
            var heir = Menaces.Rise(town, m.Kind, m.Lieutenant == "" ? "a lieutenant" : m.Lieutenant, Math.Max(1, m.Level - 2), "lieutenant");
            if (heir != null)
            {
                heir.Assets.Add(new MenaceAsset { Def = best.Def, SiteId = heir.SeatId });
                heir.Named = true;
                var hs = Quests.Site(town, heir.SeatId);
                if (hs != null) Rumors.Hear(town, hs, Rumors.Occupants(town.Content, hs.Def), hs.Def.Tier, 50);
                town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Fill(town, m, r.Lieutenant)
                    .Replace("{lieutenant}", heir.Leader).Replace("{old}", m.Leader).Replace("{asset}", AssetOf(town, best)?.Name ?? best.Def)) +
                    $" They call them {heir.Name} now, and they have taken {heir.SeatName} in {heir.Region}.");
            }
        }
        Seize(town, m, seat);
    }

    // ---- Plans ----

    public static void Daily(Town town)
    {
        var r = Rules(town);
        foreach (var m in Menaces.Alive(town))
        {
            if (m.Origin == Menaces.FromWarband || m.BigBad || m.Stage < MenaceStage.Rising) continue;
            if (m.Lieutenant == "") m.Lieutenant = Menaces.NewLeaderName(town, m.Def);
            if (m.Plan is { } p)
            {
                if (!p.Known) Learn(town, m, p);
                else if (!p.Portent && town.Day * 2 >= p.StartDay + p.DueDay) Foretell(town, m, p);
                if (town.Day >= p.DueDay) Finish(town, m, p);
                continue;
            }
            if (m.NextPlanDay == 0) m.NextPlanDay = town.Day + town.Rng.Range(r.FirstDays[0], r.FirstDays[1] + 1);
            if (town.Day < m.NextPlanDay) continue;
            m.NextPlanDay = town.Day + town.Rng.Range(r.EveryDays[0], r.EveryDays[1] + 1);
            Begin(town, m);
        }
    }

    private static void Begin(Town town, Menace m)
    {
        var fits = new List<(PlanDef Def, Settlement? Target)>();
        foreach (var d in Rules(town).Plans)
        {
            if (d.Kinds.Count > 0 && !d.Kinds.Contains(m.Kind)) continue;
            if (d.Ambitions.Count > 0 && !d.Ambitions.Contains(m.Ambition)) continue;
            if (Menaces.CunningOf(town, m).Id == "mindless" && d.Effect is not ("raid" or "drive")) continue;
            Settlement? target = null;
            if (d.Target is "settlement" or "poor")
            {
                target = Near(town, m, d.Target == "poor");
                if (target == null) continue;
                if (d.Effect == "toll" && town.Saga.Menaces.Any(x => x.Alive && x.TollOn == target.Id)) continue;
            }
            fits.Add((d, target));
        }
        if (fits.Count == 0) return;
        var (def, s) = fits[town.Rng.Range(0, fits.Count)];
        m.Plan = new MenacePlan { Def = def.Id, SettlementId = s?.Id, StartDay = town.Day, DueDay = town.Day + def.Days };
        // A great wyrm does nothing quietly: the whole country watches it begin.
        if (m.Named && Rules(town).Loud.Contains(m.Kind))
        {
            m.Plan.Known = true;
            town.Log(LedgerCategory.War, Importance.Notable, Saga.Cap(Fill(town, m, Rules(town).Learned).Replace("{plan}", Describe(town, m))));
        }
    }

    /// <summary>Half done, a plan the town knows of sends its portents ahead: what comes next if nobody stops it.</summary>
    private static void Foretell(Town town, Menace m, MenacePlan p)
    {
        p.Portent = true;
        if (m.Def.Doom.Count == 0) return;
        string line = m.Def.Doom[(m.Id + p.StartDay) % m.Def.Doom.Count];
        town.Log(LedgerCategory.War, Importance.Notable, Saga.Cap(Fill(town, m, line).Replace("{plan}", Describe(town, m)).Replace("{days}", Math.Max(0, p.DueDay - town.Day).ToString())));
    }

    /// <summary>A settlement within a ring of the menace's seat and near its bearing; a poor one for recruiters.</summary>
    private static Settlement? Near(Town town, Menace m, bool poor)
    {
        if (Quests.Site(town, m.SeatId) is not { RegionId: int rid }) return null;
        var home = town.Regions[rid];
        return town.Settlements
            .Where(s => !s.Razed && Math.Abs(town.Regions[s.RegionId].Ring - home.Ring) <= 1 && World.AngleBetween(town.Regions[s.RegionId].Bearing, home.Bearing) <= 60)
            .Where(s => !poor || s.Prosperity < 40)
            .OrderBy(s => s.RegionId == home.Id ? 0 : 1)
            .ThenBy(s => World.AngleBetween(town.Regions[s.RegionId].Bearing, home.Bearing))
            .ThenBy(s => s.Id)
            .FirstOrDefault();
    }

    /// <summary>
    /// How the town learns of a plan: the menace's seat in sight (scried or watched), a friend in a settlement
    /// near it who sends word, or talk on the roads once its name is known.
    /// </summary>
    private static void Learn(Town town, Menace m, MenacePlan p)
    {
        var r = Rules(town);
        var seat = Quests.Site(town, m.SeatId);
        string? text = null;
        if (seat != null && Watch.LiveOf(town, seat) != null) text = r.Learned;
        else if (Friend(town, m) is { } c)
        {
            var s = town.Settlements[c.SettlementId];
            text = r.Warned.Replace("{contact}", c.Name).Replace("{settlement}", s.Name).Replace("{memory}", c.Memories.Count > 0 ? c.Memories[^1] : "old kindnesses");
        }
        else if (m.Named && town.Rng.Chance(r.LearnChance)) text = r.Learned;
        if (text == null) return;
        p.Known = true;
        m.Named = true;
        town.Log(LedgerCategory.War, Importance.Notable, Saga.Cap(Fill(town, m, text).Replace("{plan}", Describe(town, m))));
    }

    /// <summary>A contact near the menace who thinks well enough of the town to warn it.</summary>
    private static Contact? Friend(Town town, Menace m)
    {
        if (Quests.Site(town, m.SeatId) is not { RegionId: int rid }) return null;
        var home = town.Regions[rid];
        return town.Neighbours.Cast
            .Where(c => !c.Gone && c.Regard >= Rules(town).LearnRegard && town.Settlements[c.SettlementId] is { Razed: false } s
                        && Math.Abs(town.Regions[s.RegionId].Ring - home.Ring) <= 1 && World.AngleBetween(town.Regions[s.RegionId].Bearing, home.Bearing) <= 60)
            .OrderByDescending(c => c.Regard)
            .ThenBy(c => c.Id)
            .FirstOrDefault();
    }

    private static void Finish(Town town, Menace m, MenacePlan p)
    {
        var r = Rules(town);
        var def = Rules(town).Plan(p.Def);
        m.Plan = null;
        if (def == null) return;
        var s = p.SettlementId is int sid ? town.Settlements[sid] : null;
        if (s != null && s.Razed) return;
        bool heard = p.Known || m.Named && (s == null || town.Regions[s.RegionId].Known);
        bool foiled = s != null && def.Foil switch
        {
            "security" => s.Security >= r.FoilSecurity,
            "prosperity" => s.Prosperity >= r.FoilProsperity,
            _ => false,
        };
        if (foiled)
        {
            m.Power = Math.Max(0, m.Power - town.Content.Saga.Menaces.FailedDelve);
            if (heard && def.Foiled != "") town.Log(LedgerCategory.War, Importance.Notable, Saga.Cap(Fill(town, m, def.Foiled, s)));
            return;
        }
        var mr = town.Content.Saga.Menaces;
        string extra = "";
        switch (def.Effect)
        {
            case "toll":
                if (s == null) return;
                m.TollOn = s.Id;
                s.Prosperity = Math.Max(0, s.Prosperity - 10);
                break;
            case "sway":
                if (s == null) return;
                m.Domain++;
                s.Standing = Math.Max(-100, Math.Min(s.Standing, 0) - mr.SettlementHit);
                s.Allied = false;
                s.Prosperity = Math.Max(0, s.Prosperity - 15);
                m.Power += mr.FailedDelve;
                break;
            case "recruit":
                if (s == null) return;
                s.Security = Math.Max(0, s.Security - 10);
                m.Power += mr.FailedDelve * 1.5;
                break;
            case "ritual":
                m.Power += (mr.Stages.Power - mr.Stages.Rising) * 0.5;
                break;
            case "raid":
                m.Power += mr.FailedDelve;
                m.NextStrikeTick = Math.Min(m.NextStrikeTick, town.Tick + SimTime.HoursPerDay);
                break;
            case "camp":
            {
                var lair = Menaces.TakeHolding(town, m);
                if (lair == null) return;
                m.Assets.Add(new MenaceAsset { Def = "supply-camp", SiteId = lair.Def.Id });
                m.Power += mr.FailedDelve;
                extra = lair.Def.Name;
                break;
            }
            case "drive":
            {
                var near = Driven(town, m);
                if (near == null) return;
                near.Strength = town.Content.Lairs.MaxStrength;
                near.NextActTick = town.Tick + 2 * SimTime.HoursPerDay;
                Rumors.Hear(town, near, Rumors.Occupants(town.Content, near.Def), near.Def.Tier, Math.Max(near.Trust, 50));
                extra = near.Def.Name;
                break;
            }
        }
        if (heard && def.Done != "") town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Fill(town, m, def.Done, s).Replace("{holding}", extra).Replace("{near}", extra)));
    }

    /// <summary>A standing lair nearer the town, on the menace's bearing, for what it drove out to crowd into.</summary>
    private static SiteState? Driven(Town town, Menace m)
    {
        if (Quests.Site(town, m.SeatId) is not { RegionId: int rid }) return null;
        var home = town.Regions[rid];
        return town.Sites
            .Where(s => !s.Cleared && s.RegionId is int sr && town.Regions[sr].Ring < home.Ring && Menaces.At(town, s.Def.Id) == null)
            .OrderBy(s => World.AngleBetween(town.Regions[s.RegionId!.Value].Bearing, home.Bearing))
            .ThenByDescending(s => town.Regions[s.RegionId!.Value].Ring)
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>Known plans close to done: the town still has a few days to get in the way.</summary>
    public static IEnumerable<Menace> Looming(Town town) =>
        town.Saga.Menaces.Where(m => m.Alive && m.Plan is { Known: true } p && p.DueDay - town.Day <= Rules(town).WarnDays).OrderBy(m => m.Id);

    private static string Fill(Town town, Menace m, string text, Settlement? s = null)
    {
        s ??= m.Plan?.SettlementId is int sid ? town.Settlements[sid] : null;
        return Saga.Fill(town, text.Replace("{settlement}", s?.Name ?? "the settlements"), m);
    }

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
