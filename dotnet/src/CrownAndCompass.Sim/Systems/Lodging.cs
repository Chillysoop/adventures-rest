namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Rooms for the night (content/game/lodging.json). Taverns let a few rooms and inns many (<see cref="BuildingDef.Rooms"/>);
/// every night the heroes in town and the travellers looking it over take them, heroes first, and pay the house. Whoever
/// finds none camps outside town: a hero come looking for work who camps <see cref="LodgingRules.CampNights"/> nights in a row rides on, unless the
/// town has given them a reason to stay. Travellers are newcomers who came up the road (<see cref="Population.DailyArrivals"/>):
/// they stay a few nights, settle once they find work, a home or a business of their own, and otherwise move on.
/// </summary>
public static class Lodging
{
    public static LodgingRules Rules(Town town) => town.Content.Lodging;

    /// <summary>The houses letting rooms tonight, by id.</summary>
    public static IEnumerable<Building> Houses(Town town) =>
        town.Buildings.Where(b => b.Def.Rooms > 0 && b.Complete && !b.Closed && !b.ForSale && !b.Razed);

    /// <summary>Rooms the town's inns and taverns let, all told.</summary>
    public static int Rooms(Town town) => Houses(town).Sum(b => b.Def.Rooms);

    /// <summary>Guests in a house's rooms last night.</summary>
    public static int Guests(Town town, Building b) => town.Agents.Count(a => a.InnId == b.Id && !a.Departed);

    /// <summary>A hero who sleeps under a roof of their own: a house in town, their company's bastion, the town hall's bed for the sworn, a hold.</summary>
    public static bool Housed(Town town, Agent a) => a.HomeId != null || Bastions.Lodges(town, a) || a.Hero!.Sworn || a.Hero.Holding != "";

    /// <summary>A hero the town has tied to it, or who never came looking for work, who never rides on for want of a room.</summary>
    private static bool Bound(Agent a)
    {
        var s = a.Hero!;
        return !s.Wanderer || s.Knight || s.Champion || s.FiefId != null || s.BastionId != null || s.Duty != HeroDuty.None || s.TurnedTo != null;
    }

    /// <summary>
    /// Nightly: heroes in town, then travellers, take the free rooms (in the house with most free, ties by id) and pay for
    /// them; the rest camp. A long rest heals, a bed faster than a bedroll. Heroes camped too long ride on.
    /// </summary>
    public static void Nightly(Town town)
    {
        var r = Rules(town);
        var houses = Houses(town).OrderBy(b => b.Id).ToList();
        var free = houses.ToDictionary(b => b.Id, b => b.Def.Rooms);
        foreach (var a in town.Agents) a.InnId = null;
        int guests = 0, camped = 0;

        bool Room(Agent a)
        {
            if (a.Gold < r.Price) return false;
            Building? best = null;
            foreach (var b in houses)
                if (free[b.Id] > 0 && (best == null || free[b.Id] > free[best.Id])) best = b;
            if (best == null) return false;
            free[best.Id]--;
            a.InnId = best.Id;
            a.Gold -= r.Price;
            best.Cash += r.Price;
            guests++;
            return true;
        }

        var leaving = new List<Agent>();
        foreach (var a in town.Heroes.OrderBy(a => a.Id))
        {
            var s = a.Hero!;
            if (s.AwayUntil != null) continue;
            bool home = Housed(town, a);
            s.Lodging = home || Room(a);
            if (s.Lodging) s.CampNights = 0;
            else
            {
                camped++;
                // Only waiting on a room wears them out: a hero with an empty purse sleeps rough and stays for the next bounty.
                if (a.Gold >= r.Price) s.CampNights++;
                if (s.CampNights >= r.CampNights && !Bound(a)) leaving.Add(a);
            }
            // A long rest heals; a bed heals faster, and the shrine or temple more.
            int healed = (int)Math.Ceiling(s.MaxHp * (s.Lodging ? r.RoomHeal : r.CampHeal) * Curses.Heal(town.Content, s));
            if (s.Hp < s.MaxHp) healed = Shrines.Rest(town, a, healed);
            s.Hp = Math.Min(s.MaxHp, s.Hp + healed);
        }
        // Refugees camp free outside town: the town feeds them (Refugees.Daily), so they never take a room.
        foreach (var a in town.Agents.Where(a => a.Traveller && !a.Departed).OrderBy(a => a.Id))
            if (a.Refugee || !Room(a)) camped++;

        foreach (var a in leaving)
        {
            town.Log(LedgerCategory.Departure, Importance.Notable, $"{a.Name} gives up waiting for a room in {town.Name} and rides on.", [a.Id]);
            Population.Depart(town, a);
        }
        var st = town.Lodging;
        st.Guests = guests;
        st.Camped = camped;
        st.RodeOn += leaving.Count;
    }

    /// <summary>
    /// Daily: travellers who found work, a home or a business of their own settle; those whose time is up move on with
    /// their spouse, saying why. A couple decides together.
    /// </summary>
    public static void Decide(Town town)
    {
        var travellers = town.Agents.Where(a => a.Traveller && !a.Departed).OrderBy(a => a.Id).ToList();
        if (travellers.Count == 0) return;
        var owners = new HashSet<int>();
        foreach (var b in town.Buildings)
            if (b.OwnerId is int o) owners.Add(o);
        bool Rooted(Agent a) => a.JobId != null || a.HomeId != null || a.RegimentId != null || owners.Contains(a.Id);
        int day = town.Tick / SimTime.HoursPerDay;
        foreach (var a in travellers)
        {
            if (!a.Traveller || a.Departed) continue;
            var spouse = a.SpouseId is int sid && town.Agent(sid) is { Departed: false, Traveller: true } sp ? sp : null;
            if (Rooted(a) || (spouse != null && Rooted(spouse)))
            {
                Settle(town, a, spouse);
                continue;
            }
            if (day < a.StayUntil) continue;
            string why = Why(town);
            string who = spouse == null ? a.Name : $"{a.Name} and {spouse.GivenName}";
            town.Log(LedgerCategory.Departure, Importance.Routine,
                a.Refugee ? Refugees.GaveUp(town, a, who, spouse != null, why) : $"{who} {(spouse == null ? "moves" : "move")} on from {town.Name}: {why}.", [a.Id]);
            Population.Depart(town, a);
            if (spouse != null) Population.Depart(town, spouse);
            town.Lodging.MovedOn += spouse == null ? 1 : 2;
            town.Lodging.Why = why;
        }
    }

    private static void Settle(Town town, Agent a, Agent? spouse)
    {
        string what = (a.RegimentId ?? spouse?.RegimentId) != null ? "a place in the host"
            : (a.JobId ?? spouse?.JobId) is int job ? $"work at {town.Building(job).Name}"
            : (a.HomeId ?? spouse?.HomeId) is int home ? $"a roof at {town.Building(home).Name}"
            : "land of their own";
        foreach (var x in new[] { a, spouse })
        {
            if (x == null) continue;
            x.Traveller = false;
        }
        string who = spouse == null ? a.Name : $"{a.Name} and {spouse.GivenName}";
        town.Log(LedgerCategory.Arrival, Importance.Routine,
            a.Refugee ? Refugees.Settled(town, a, who, spouse != null, what) : $"{who} {(spouse == null ? "decides" : "decide")} to stay in {town.Name}, having found {what}.", [a.Id]);
        town.Lodging.Settled += spouse == null ? 1 : 2;
    }

    /// <summary>Why a traveller found nothing to keep them, read off the town: the first of no work, nowhere to live, no room.</summary>
    public static string Why(Town town)
    {
        int jobs = 0, beds = 0;
        foreach (var b in town.Buildings)
        {
            if (b.Crew == 0) jobs += b.OpenJobs;
            beds += b.OpenBeds;
        }
        if (jobs == 0 && !town.Buildings.Any(b => b.IsSite)) return "no work going";
        if (beds == 0 && Land.OpenPlots(town, Zone.Residential) == 0) return "no land to build a home on";
        if (Rooms(town) == 0) return "no room at an inn to wait for work";
        return "no work to be had";
    }

    /// <summary>A newcomer off the road: a traveller who stays some nights before deciding, fewer when there's no room to be had.</summary>
    public static void Arrive(Town town, Agent a, int nights)
    {
        a.Traveller = true;
        a.StayUntil = town.Tick / SimTime.HoursPerDay + nights;
    }

    /// <summary>Where the road comes into town: by the outermost wall's main gate once one stands, else a heading of the seed's.</summary>
    public static double Bearing(Town town)
    {
        for (int ring = City.WallRings - 1; ring >= 0; ring--)
            if (WallLines.Gates(town, ring) is { Count: > 0 } gates) return gates[0];
        return (town.Seed * 0.6180339887 % 1 + 1) % 1 * 2 * Math.PI;
    }

    /// <summary>The camp outside town, in tiles from its corner: just past the furthest building, toward the road.</summary>
    public static (double X, double Y) Camp(Town town) => Out(town, Rules(town).CampOut);

    /// <summary>Where newcomers come into sight on the road, further out past the camp.</summary>
    public static (double X, double Y) Road(Town town) => Out(town, Rules(town).CampOut + Rules(town).RoadOut);

    private static (double X, double Y) Out(Town town, double past)
    {
        double a = Bearing(town), dx = Math.Cos(a), dy = Math.Sin(a);
        double reach = 3, most = Town.Grid / 2.0 - 1;
        // Past the last building, painted cell or lane along the heading, a tile either side of it.
        var plan = town.Plan;
        for (double r = 3; r <= most; r += 0.25)
            for (double side = -1; side <= 1; side += 0.5)
            {
                double x = Town.Center + dx * r - dy * side, y = Town.Center + dy * r + dx * side;
                int cx = (int)(x * Land.Res), cy = (int)(y * Land.Res);
                if (cx < 0 || cy < 0 || cx >= Land.Size || cy >= Land.Size) continue;
                int i = cy * Land.Size + cx;
                if (plan.Zones[i] != 0 || plan.Taken[i] != 0 || plan.Paved[i] != 0) reach = r;
            }
        double d = Math.Min(most, reach + past);
        return (Town.Center + dx * d, Town.Center + dy * d);
    }

    /// <summary>People sleeping rough tonight: the homeless, heroes without a bed and travellers without a room.</summary>
    public static int Campers(Town town)
    {
        int n = 0;
        foreach (var a in town.Agents)
        {
            if (a.Departed || a.Dead || a.IsAway) continue;
            if (a.Hero != null ? !a.Hero.Lodging : a.Traveller ? a.InnId == null : a.HomeId == null) n++;
        }
        return n;
    }
}

/// <summary>Numbers for rooms, travellers and camping (content/game/lodging.json).</summary>
public sealed record LodgingRules
{
    /// <summary>Gold a night for a room.</summary>
    public double Price { get; init; } = 1;

    /// <summary>Nights a traveller stays to look the town over (least, most), and the most when they must camp.</summary>
    public int StayLeast { get; init; } = 2;
    public int StayMost { get; init; } = 4;
    public int CampStay { get; init; } = 1;

    /// <summary>Nights in a row a hero camps before riding on.</summary>
    public int CampNights { get; init; } = 3;

    /// <summary>Share of the daily chance a hero comes when every room is taken.</summary>
    public double CampArrivals { get; init; } = 0.5;

    /// <summary>Share of their hit points a night's rest heals: in a bed, and in a bedroll.</summary>
    public double RoomHeal { get; init; } = 0.5;
    public double CampHeal { get; init; } = 0.25;

    /// <summary>Tiles past the furthest building the camp stands, and further still where newcomers come into sight.</summary>
    public double CampOut { get; init; } = 1.6;
    public double RoadOut { get; init; } = 7;
}

/// <summary>Last night's lodging, and how the town's travellers have decided.</summary>
public sealed class LodgingState
{
    public int Guests { get; set; }
    public int Camped { get; set; }
    public int Settled { get; set; }
    public int MovedOn { get; set; }
    public int RodeOn { get; set; }

    /// <summary>Why the last traveller moved on, or "".</summary>
    public string Why { get; set; } = "";
}
