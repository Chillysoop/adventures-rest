namespace CrownAndCompass.Sim.Systems;

/// <summary>Numbers for the pantheon in town (content/game/faith.json); the gods themselves are in the library.</summary>
public sealed record FaithRules
{
    /// <summary>The god the charter shrine is raised to.</summary>
    public string CharterDeity { get; init; } = "lantern-mother";

    /// <summary>Each god's holy day comes round once in this many days, on its own day of the cycle.</summary>
    public int HolyDayCycle { get; init; } = 10;

    /// <summary>Share of newcomers who keep no god at all.</summary>
    public double Faithless { get; init; } = 0.12;

    // How strongly race, trade, class and temperament draw a newcomer to a god.
    public double RaceWeight { get; init; } = 4;
    public double ProfessionWeight { get; init; } = 3;
    public double ClassWeight { get; init; } = 10;
    public double VirtueWeight { get; init; } = 2;
    public double ViceWeight { get; init; } = 2;

    /// <summary>Hero classes that arrive already sworn to their god.</summary>
    public IReadOnlyList<string> ClergyClasses { get; init; } = ["cleric", "paladin"];

    public double HolyDayMood { get; init; } = 10;
    public double WorshipMood { get; init; } = 3;

    /// <summary>Share of a follower's purse given at a holy day gathering, up to MaxOffering.</summary>
    public double Offering { get; init; } = 0.05;
    public double MaxOffering { get; init; } = 2;

    /// <summary>Chance a follower at a holy day gathering takes the god's vows.</summary>
    public double VowChance { get; init; } = 0.15;

    /// <summary>Followers a god needs in town before they pool for a shrine of their own.</summary>
    public int ShrineFollowers { get; init; } = 4;

    /// <summary>Residents the town needs before a second god's followers can raise a shrine.</summary>
    public int ShrinePopulation { get; init; } = 30;

    /// <summary>Most a devout follower gives to the shrine fund on the god's holy day (half for the rest).</summary>
    public double ShrineGift { get; init; } = 1.5;
    public int MaxShrines { get; init; } = 4;

    // Divine favour: a god acts (a miracle) once its favour in town reaches Miracle.
    public double FavorPerFollower { get; init; } = 0.15;
    public double FavorPerDevout { get; init; } = 0.6;
    public double FavorHolyDay { get; init; } = 4;
    public double FavorShrine { get; init; } = 0.5;
    public double FavorTemple { get; init; } = 1.5;
    public double Miracle { get; init; } = 100;

    /// <summary>A blessing before a delve: weapon bonus for the expedition, and what the priests ask for it.</summary>
    public int BlessBonus { get; init; } = 1;
    public double BlessCost { get; init; } = 3;

    /// <summary>Chance followers of rival gods come to words on a holy day.</summary>
    public double RivalScuffle { get; init; } = 0.3;

    /// <summary>Suspicion a temple's sermon heaps on the secret faction its god hates.</summary>
    public double SermonHeat { get; init; } = 12;

    // What miracles do.
    public double ValorDefense { get; init; } = 25;
    public int ValorDays { get; init; } = 10;
    public int JusticeDays { get; init; } = 10;
    public double JusticeWatch { get; init; } = 0.25;
    public double JusticeHeat { get; init; } = 30;
    public double WardHeat { get; init; } = 30;
    public double HarvestFood { get; init; } = 60;
    public double ForgeTools { get; init; } = 15;
    public double TradeGold { get; init; } = 8;
    public int[] LuckGold { get; init; } = [15, 40];
    public double StormShare { get; init; } = 0.12;
}

/// <summary>A hero blessed at their god's altar before an expedition; the bonus lifts when the party is home.</summary>
public sealed record Blessing(int AgentId, int PartyId, int Bonus);

/// <summary>A god acting in town.</summary>
public sealed record Miracle(int Tick, string Deity, string Boon);

/// <summary>The pantheon as it stands in town: which god each house of worship serves, and each god's favour.</summary>
public sealed class FaithState
{
    /// <summary>Holy building id to the deity it is dedicated to.</summary>
    public OrderedMap<int, string> Dedications { get; } = new();

    /// <summary>Divine favour by deity id; a miracle spends it.</summary>
    public OrderedMap<string, double> Favor { get; } = new();

    /// <summary>Gold followers have pooled towards a shrine of their god's own.</summary>
    public OrderedMap<string, double> Funds { get; } = new();

    public List<Blessing> Blessings { get; } = [];
    public List<Miracle> Miracles { get; } = [];

    /// <summary>Day a war god's valour, or a law god's justice, wears off.</summary>
    public int ValorUntil { get; set; }
    public int JusticeUntil { get; set; }
}

/// <summary>
/// The pantheon (GDD rule W8: the gods are present). Every newcomer leans to a god by race, trade,
/// class and temperament; clerics and paladins arrive sworn. The charter shrine is the Lantern
/// Mother's; the temple goes to the largest congregation, and any other god with enough followers
/// pools their coin for a shrine of its own. Each god keeps its holy day at its own house, the
/// faithful gather, offer and take vows, rival faiths quarrel, and a temple preaches against the
/// secret factions its god hates. Devotion builds divine favour; when it brims over the god acts:
/// healing light, a blessed harvest, valour on the walls, a storm on the warband's camp. Heroes kneel
/// at their god's altar before they march. Evil gods have no houses; only cults feed them.
/// </summary>
public static class Faith
{
    private static FaithRules Rules(Town town) => town.Content.Faith;

    public static IReadOnlyList<LibDeity> Gods(Town town) => town.Content.Library.Deities;

    public static LibDeity? God(Town town, string? id) => string.IsNullOrEmpty(id) ? null : town.Content.Library.Deity(id);

    /// <summary>An agent's god, or null for the faithless (and newcomers not yet settled).</summary>
    public static LibDeity? Of(Town town, Agent a) => God(town, a.Faith);

    /// <summary>The god a house of worship serves.</summary>
    public static LibDeity? Of(Town town, Building b) => God(town, town.Faiths.Dedications.Get(b.Id, ""));

    /// <summary>"The Lantern Mother" reads "the Lantern Mother" in a sentence.</summary>
    public static string Prose(LibDeity god) => god.Name.StartsWith("The ", StringComparison.Ordinal) ? "t" + god.Name[1..] : god.Name;

    /// <summary>The god's short name for a building: "Haldor", "the Lantern Mother", "Nim".</summary>
    public static string Short(LibDeity god)
    {
        string n = Prose(god);
        if (n.StartsWith("the ", StringComparison.Ordinal)) return n;
        int cut = n.IndexOf(" the ", StringComparison.Ordinal);
        int of = n.IndexOf(" of ", StringComparison.Ordinal);
        if (of >= 0 && (cut < 0 || of < cut)) cut = of;
        return cut > 0 ? n[..cut] : n;
    }

    /// <summary>Complete houses of worship dedicated to a god, temple first.</summary>
    public static List<Building> Places(Town town, string deityId) =>
        town.Faiths.Dedications
            .Where(kv => kv.Value == deityId)
            .Select(kv => town.Building(kv.Key))
            .Where(b => b.Complete)
            .OrderBy(b => b.Def.Id == town.Content.Shrines.Temple ? 0 : 1)
            .ThenBy(b => b.Id)
            .ToList();

    /// <summary>Where an agent goes to pray: their own god's house if it has one.</summary>
    public static Building? PlaceFor(Town town, Agent a) => a.Faith is null or "" ? null : Places(town, a.Faith).FirstOrDefault();

    /// <summary>Whether the god has a finished house in town (Places without the sorting, for every agent every hour).</summary>
    public static bool HasPlace(Town town, string deityId)
    {
        foreach (var kv in town.Faiths.Dedications)
            if (kv.Value == deityId && town.Building(kv.Key).Complete) return true;
        return false;
    }

    public static List<Agent> Followers(Town town, string deityId) => town.Residents.Where(a => a.Faith == deityId).ToList();

    /// <summary>The god whose temple stands (or is rising) in town.</summary>
    public static LibDeity? TempleGod(Town town)
    {
        var temple = town.Buildings.FirstOrDefault(b => b.Def.Id == town.Content.Shrines.Temple);
        return temple == null ? null : Of(town, temple);
    }

    public static bool IsHolyDay(Town town, LibDeity god)
    {
        int cycle = Rules(town).HolyDayCycle;
        int index = 0;
        foreach (var g in Gods(town))
        {
            if (g.Id == god.Id) break;
            index++;
        }
        return town.Day % cycle == index % cycle;
    }

    /// <summary>Mood an agent draws from their faith this hour: their god's holy day, and the comfort of the vows.</summary>
    public static double Mood(Town town, Agent a)
    {
        var god = Of(town, a);
        if (god == null || a.Faith is null or "" || !HasPlace(town, a.Faith)) return 0;
        var r = Rules(town);
        return (IsHolyDay(town, god) ? r.HolyDayMood : 0) + (a.Devout ? r.WorshipMood : 0);
    }

    /// <summary>Extra defence at the walls while a war god's valour lasts.</summary>
    public static double Defense(Town town) => town.Day < town.Faiths.ValorUntil ? Rules(town).ValorDefense : 0;

    /// <summary>Extra watch strength while a law god's justice lasts.</summary>
    public static double WatchBonus(Town town) => town.Day < town.Faiths.JusticeUntil ? Rules(town).JusticeWatch : 0;

    // Dedication ----------------------------------------------------------------------------------

    /// <summary>Dedicate a house of worship and name it for its god.</summary>
    public static void Dedicate(Town town, Building b, LibDeity god)
    {
        town.Faiths.Dedications[b.Id] = god.Id;
        string kind = b.Def.Id == town.Content.Shrines.Temple ? "Temple" : "shrine";
        b.Name = $"the {kind} of {Short(god)}";
    }

    /// <summary>The god the charter shrine is raised to.</summary>
    public static LibDeity CharterGod(Town town) => God(town, Rules(town).CharterDeity) ?? Gods(town).First(g => !g.Evil);

    /// <summary>Who a new temple serves: the largest congregation, counting the sworn twice.</summary>
    public static LibDeity TempleChoice(Town town) =>
        Gods(town)
            .Where(g => !g.Evil)
            .OrderByDescending(g => town.Residents.Where(a => a.Faith == g.Id).Sum(a => a.Devout ? 2 : 1))
            .ThenBy(g => g.Id == Rules(town).CharterDeity ? 0 : 1)
            .ThenBy(g => g.Id, StringComparer.Ordinal)
            .First();

    // Who believes what ---------------------------------------------------------------------------

    /// <summary>Settle a newcomer's faith: none, or a god weighed by race, trade, class and temperament.</summary>
    public static void Choose(Town town, Agent a)
    {
        var r = Rules(town);
        string cls = a.Hero?.Cls.Id ?? "";
        bool clergy = r.ClergyClasses.Contains(cls);
        if (!clergy && town.Rng.Chance(r.Faithless))
        {
            a.Faith = "";
            return;
        }
        string race = a.Race.ToLowerInvariant().Replace(' ', '-');
        var traits = a.Traits.Select(t => t.ToString()).ToHashSet();
        var gods = Gods(town).Where(g => !g.Evil).ToList();
        var weights = gods
            .Select(g =>
            {
                double w = 1;
                if (g.Races.Contains(race)) w += r.RaceWeight;
                if (g.Professions.Contains(a.Profession)) w += r.ProfessionWeight;
                if (cls != "" && g.Classes.Contains(cls)) w += r.ClassWeight;
                w += r.VirtueWeight * g.Virtues.Count(traits.Contains);
                w -= r.ViceWeight * g.Vices.Count(traits.Contains);
                return Math.Max(0.25, w);
            })
            .ToList();
        double roll = town.Rng.Next() * weights.Sum();
        var pick = gods[^1];
        for (int i = 0; i < gods.Count; i++)
        {
            if (roll < weights[i])
            {
                pick = gods[i];
                break;
            }
            roll -= weights[i];
        }
        a.Faith = pick.Id;
        if (clergy)
        {
            a.Devout = true;
            town.Log(LedgerCategory.Faith, Importance.Routine, $"{a.Name} wears the holy symbol of {Prose(pick)}, {pick.Symbol}.", [a.Id]);
        }
    }

    // The day -------------------------------------------------------------------------------------

    public static void Daily(Town town)
    {
        foreach (var a in town.Residents) if (a.Faith == null) Choose(town, a);
        Unbless(town);
        Bless(town);
        foreach (var god in Gods(town))
        {
            if (god.Evil || !IsHolyDay(town, god)) continue;
            var followers = Followers(town, god.Id);
            if (followers.Count > 0) HolyDay(town, god, followers);
        }
        Petitions(town);
        Favor(town);
    }

    private static void HolyDay(Town town, LibDeity god, List<Agent> followers)
    {
        var r = Rules(town);
        var rng = town.Rng;
        var place = Places(town, god.Id).FirstOrDefault();
        var present = followers.Where(a => !a.IsAway).ToList();
        if (present.Count == 0) return;
        bool temple = place != null && place.Def.Id == town.Content.Shrines.Temple;
        string day = god.HolyDay;
        town.Faiths.Favor[god.Id] = town.Faiths.Favor.Get(god.Id, 0) + r.FavorHolyDay * (place != null ? 1 : 0.5);

        if (place == null)
        {
            if (present.Count >= r.ShrineFollowers)
                town.Log(LedgerCategory.Faith, Importance.Routine, $"The {present.Count} followers of {Prose(god)} keep {day} in the open and talk of raising a shrine.");
            return;
        }

        double offered = 0;
        var sworn = new List<Agent>();
        foreach (var a in present)
        {
            a.Loneliness = Math.Max(0, a.Loneliness - 15);
            double gift = Math.Min(r.MaxOffering, Math.Floor(a.Gold * r.Offering * 10) / 10);
            if (gift > 0)
            {
                a.Gold -= gift;
                offered += gift;
            }
            if (!a.Devout && rng.Chance(r.VowChance))
            {
                a.Devout = true;
                sworn.Add(a);
            }
        }
        place.Cash += offered;

        if (temple)
        {
            // The temple feeds the town on its god's day, if its coffers allow.
            var sr = town.Content.Shrines;
            bool feast = place.Cash >= sr.HolyDayCost;
            if (feast)
            {
                place.Cash -= sr.HolyDayCost;
                town.FestivalDays = Math.Max(town.FestivalDays, 1);
            }
            town.Log(
                LedgerCategory.Faith,
                Importance.Notable,
                $"{Cap(place.Name)} keeps {day}: {present.Count} faithful of {Prose(god)} carry {god.Symbol} through the streets" +
                (feast ? ", and the whole town eats at the temple's table." : "."));
            Sermon(town, god, place);
        }
        else
        {
            town.Log(
                LedgerCategory.Faith,
                present.Count >= 4 ? Importance.Notable : Importance.Routine,
                $"{present.Count} {(present.Count == 1 ? "follower" : "followers")} of {Prose(god)} keep {day} at {place.Name} and leave {Js.ToFixed(offered, 0)} gold on the altar.");
        }
        foreach (var a in sworn) town.Log(LedgerCategory.Faith, Importance.Routine, $"{a.Name} takes the vows of {Prose(god)} on {day}.", [a.Id]);

        // Rival faiths come to words.
        foreach (string rivalId in god.Rivals)
        {
            var rivals = Followers(town, rivalId).Where(x => !x.IsAway).ToList();
            if (rivals.Count == 0 || !rng.Chance(r.RivalScuffle)) continue;
            var a = present[rng.Range(0, present.Count)];
            var b = rivals[rng.Range(0, rivals.Count)];
            a.ChangeOpinion(b.Id, -12);
            b.ChangeOpinion(a.Id, -12);
            var rival = God(town, rivalId)!;
            town.Log(
                LedgerCategory.Faith,
                Importance.Routine,
                $"{b.Name}, who keeps {Prose(rival)}, mocks the {day} procession, and {a.Name} has hard words for {b.GivenName}.",
                [a.Id, b.Id]);
            break;
        }
    }

    /// <summary>A temple of a lawful or good god preaches against the secret faction its god most hates.</summary>
    private static void Sermon(Town town, LibDeity god, Building temple)
    {
        if (!god.Alignment.Contains('L') && !god.Alignment.Contains('G')) return;
        var target = Factions.Denounce(town, god, Rules(town).SermonHeat);
        if (target == null) return;
        town.Log(LedgerCategory.Faith, Importance.Notable, $"From the steps of {temple.Name}, the priests of {Prose(god)} denounce {target.Name} and call on the faithful to name its members.");
    }

    /// <summary>A god with enough followers and no house pools their coin for a shrine of its own.</summary>
    private static void Petitions(Town town)
    {
        var r = Rules(town);
        var sr = town.Content.Shrines;
        int shrines = town.Faiths.Dedications.Count(kv => town.Building(kv.Key).Def.Id == sr.Shrine);
        if (shrines >= r.MaxShrines || town.Population < r.ShrinePopulation) return;
        var def = town.Content.Building(sr.Shrine);
        foreach (var god in Gods(town))
        {
            // Followers pass the plate for a shrine on their god's holy day.
            if (god.Evil || !IsHolyDay(town, god) || town.Faiths.Dedications.Values.Contains(god.Id)) continue;
            var followers = Followers(town, god.Id);
            if (followers.Count < r.ShrineFollowers) continue;
            double fund = town.Faiths.Funds.Get(god.Id, 0);
            foreach (var a in followers)
            {
                if (a.IsAway || a.Gold < 30) continue;
                double gift = Math.Min(r.ShrineGift * (a.Devout ? 1 : 0.5), Math.Floor(a.Gold * 0.1 * 10) / 10);
                a.Gold -= gift;
                fund += gift;
            }
            double cost = def.Cost + town.Market.Quote(Good.Timber, def.Timber);
            if (fund < cost)
            {
                town.Faiths.Funds[god.Id] = fund;
                continue;
            }
            var lot = Shrines.CivicLot(town);
            if (lot == null)
            {
                town.Faiths.Funds[god.Id] = fund;
                continue;
            }
            town.Market.Buy(Good.Timber, def.Timber);
            town.Faiths.Funds[god.Id] = fund - cost;
            var b = town.AddBuilding(def, lot, null, "", false);
            b.LaborBudget = def.BuildHours * Brain.ConstructionWage;
            Dedicate(town, b, god);
            town.Log(
                LedgerCategory.Faith,
                Importance.Notable,
                $"The {followers.Count} followers of {Prose(god)} pool {Js.Str(Js.Round(cost))} gold and break ground on {b.Name}.",
                followers.Select(a => a.Id).ToList());
            return;
        }
    }

    // Favour and miracles -------------------------------------------------------------------------

    private static void Favor(Town town)
    {
        var r = Rules(town);
        var sr = town.Content.Shrines;
        foreach (var god in Gods(town))
        {
            if (god.Evil) continue;
            var followers = Followers(town, god.Id);
            var places = Places(town, god.Id);
            double gain =
                followers.Count * r.FavorPerFollower +
                followers.Count(a => a.Devout) * r.FavorPerDevout +
                places.Count(b => b.Def.Id == sr.Shrine) * r.FavorShrine +
                places.Count(b => b.Def.Id == sr.Temple) * r.FavorTemple +
                Decor.Favor(town, god.Id);
            if (places.Count == 0) gain *= 0.25;
            town.Faiths.Favor[god.Id] = town.Faiths.Favor.Get(god.Id, 0) + gain;
        }
        // One god acts a day at most: the one whose favour runs highest over its line, which rises each time it acts.
        var ready = Gods(town)
            .Where(g => town.Faiths.Favor.Get(g.Id, 0) >= Threshold(town, g))
            .OrderByDescending(g => town.Faiths.Favor.Get(g.Id, 0))
            .ThenBy(g => g.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (ready != null) Act(town, ready);
    }

    /// <summary>Favour a god needs before it acts: the base, plus as much again for each time it has already acted here.</summary>
    public static double Threshold(Town town, LibDeity god) => Rules(town).Miracle * (1 + town.Faiths.Miracles.Count(m => m.Deity == god.Id));

    /// <summary>A god acts in town, spending its favour.</summary>
    public static void Act(Town town, LibDeity god)
    {
        var r = Rules(town);
        var rng = town.Rng;
        town.Faiths.Favor[god.Id] = 0;
        var place = Places(town, god.Id).FirstOrDefault();
        string where = place?.Name ?? town.Name;
        var followers = Followers(town, god.Id);
        string? text = null;
        switch (god.Boon)
        {
            case "heal":
                foreach (var h in town.Heroes.Where(h => !h.IsAway)) h.Hero!.Hp = h.Hero.MaxHp;
                foreach (var a in town.Residents) a.Hunger = Math.Min(a.Hunger, 30);
                text = $"A warm light spills from {where} at dawn and runs through the streets. {Cap(Prose(god))} has answered: every wound in {town.Name} closes, and no one goes hungry today.";
                break;
            case "harvest":
                town.Market.Seed(Good.Food, r.HarvestFood);
                text = $"The fields around {town.Name} ripen overnight. The followers of {Prose(god)} say the Green Hush walked the furrows; the granaries fill.";
                break;
            case "forge":
                town.Market.Seed(Good.Tools, r.ForgeTools);
                text = $"Every forge in {town.Name} rings true for a day and a night. {Cap(Prose(god))} guides the hammers, and the market fills with good tools.";
                break;
            case "valor":
                town.Faiths.ValorUntil = town.Day + r.ValorDays;
                text = $"A red banner no one raised is found flying over {where}. While it flies, the town's defenders will not break: {Cap(Prose(god))} is watching.";
                break;
            case "justice":
                town.Faiths.JusticeUntil = town.Day + r.JusticeDays;
                var exposed = Factions.Denounce(town, god, r.JusticeHeat);
                text = $"The scales above {where} tip of their own accord. For days the guilty cannot meet a watchman's eye" +
                       (exposed != null ? $", and whispers about {exposed.Name} spread through every tavern." : ".");
                break;
            case "ward":
                town.Faiths.Favor["hollow-king"] = 0;
                var cult = Factions.Denounce(town, god, r.WardHeat, "hollow-king");
                text = $"Lanterns burn on every grave in {town.Name}'s burying ground and no hand lit them. {Cap(Prose(god))} keeps the crossing" +
                       (cult != null ? $"; whatever {cult.Name} was waking goes back to sleep." : ".");
                break;
            case "lore":
                town.FreeScryingsUsed--;
                text = $"Ink appears on the blank pages of a book left open at {where}: a vision of the wilds. {Cap(Prose(god))} grants the town a free scrying.";
                break;
            case "luck":
                if (followers.Count > 0)
                {
                    var lucky = followers[rng.Range(0, followers.Count)];
                    int gold = rng.Range(r.LuckGold[0], r.LuckGold[1] + 1);
                    lucky.Gold += gold;
                    text = $"{lucky.Name} finds a purse of {gold} gold in the mud of the market square with no name on it. The followers of {Prose(god)} only smile.";
                }
                break;
            case "trade":
                int paid = 0;
                foreach (var b in town.Buildings.Where(b => b.Complete && b.Def.IsBusiness && !b.Def.Holy && b.OwnerId != null && town.Agent(b.OwnerId.Value).Faith == god.Id))
                {
                    b.Cash += r.TradeGold;
                    paid++;
                }
                if (paid > 0) text = $"A caravan with no master stops at {town.Name} and sells at a loss. {paid} {(paid == 1 ? "shop" : "shops")} kept by the followers of {Prose(god)} do a month's trade in a day.";
                break;
            case "storm":
                if (Threat.IsActive(town) && town.Enemy.Strength > 0)
                {
                    double lost = Js.Round(town.Enemy.Strength * r.StormShare);
                    town.Enemy.Strength -= lost;
                    text = $"A storm comes out of a clear sky and breaks over the warband's camp. Riders say tents and men were swept into the river. {Cap(Prose(god))} is not mocked.";
                }
                break;
            case "dark-rite":
                text = Factions.Waken(town);
                break;
        }
        if (text == null) return;
        town.Faiths.Miracles.Add(new Miracle(town.Tick, god.Id, god.Boon));
        town.Log(LedgerCategory.Faith, Importance.Major, text, followers.Select(a => a.Id).ToList());
    }

    // Heroes --------------------------------------------------------------------------------------

    /// <summary>Heroes who marched today kneel at their god's altar: a weapon bonus for the expedition.</summary>
    private static void Bless(Town town)
    {
        var r = Rules(town);
        foreach (var party in town.Parties)
        {
            if (party.Returned || party.DepartTick < town.Tick - SimTime.HoursPerDay) continue;
            if (town.Faiths.Blessings.Any(b => b.PartyId == party.Id)) continue;
            var blessed = new List<Agent>();
            Building? at = null;
            foreach (int id in party.Members)
            {
                var m = town.Agent(id);
                var place = PlaceFor(town, m);
                if (place == null || m.Gold < r.BlessCost) continue;
                m.Gold -= r.BlessCost;
                place.Cash += r.BlessCost;
                m.Hero!.WeaponBonus += r.BlessBonus;
                town.Faiths.Blessings.Add(new Blessing(m.Id, party.Id, r.BlessBonus));
                blessed.Add(m);
                at ??= place;
            }
            if (blessed.Count == 0) continue;
            string names = Expedition.ListNames(blessed.Select(m => m.GivenName).ToList());
            town.Log(
                LedgerCategory.Faith,
                Importance.Routine,
                $"Before the {party.Name} march, {names} {(blessed.Count == 1 ? "kneels" : "kneel")} at {at!.Name} for a blessing on their blades.",
                blessed.Select(m => m.Id).ToList());
        }
    }

    private static void Unbless(Town town)
    {
        var list = town.Faiths.Blessings;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var b = list[i];
            if (!town.Parties[b.PartyId - 1].Returned) continue;
            var sheet = town.Agent(b.AgentId).Hero;
            if (sheet != null) sheet.WeaponBonus -= b.Bonus;
            list.RemoveAt(i);
        }
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
