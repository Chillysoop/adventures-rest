using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class LivingEnemiesTests
{
    [Fact]
    public void ABandIsSeenOnTheRoadTheDayBeforeItStrikesAndLairsKeepTheirOwnCalendar()
    {
        // Not every campaign has a lair send a band in its first year: try a few.
        Simulation sim = null!;
        Town t = null!;
        Sighting? seen = null;
        var raids = new List<(string Site, int Day)>();
        foreach (int seed in new[] { 11, 12, 13, 7, 3, 5 })
        {
            sim = new Simulation(new SimulationOptions { Seed = seed });
            t = sim.Town;
            raids.Clear();
            var last = new Dictionary<string, int?>();
            for (int d = 0; d < 300 && seen == null; d++)
            {
                sim.RunDays(1);
                foreach (var s in t.Sites.OrderBy(s => s.Def.Id, StringComparer.Ordinal))
                {
                    if (s.LastRaidDay != last.GetValueOrDefault(s.Def.Id) && s.LastRaidDay is int day) raids.Add((s.Def.Id, day));
                    last[s.Def.Id] = s.LastRaidDay;
                }
                seen = t.Sightings.FirstOrDefault(g => g.Kind == Sightings.Band);
            }
            if (seen != null) break;
        }
        Assert.NotNull(seen);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("runs in with news", StringComparison.Ordinal));
        Assert.Contains(Alerts.Standing(t), a => a.Kind == Alerts.Sighted && a.BuildingId == seen!.TargetId);
        var lair = t.Sites.First(s => Lairs.Key(s) == seen!.Key);
        Assert.Equal(seen!.TargetId, lair.BandTarget);
        Assert.NotEqual("", Lairs.Habit(t, lair));
        sim.RunDays(2);
        Assert.Null(lair.BandTarget);
        Assert.DoesNotContain(t.Sightings, g => g.Kind == Sightings.Band && g.Key == seen.Key);
        // No two lairs set out within the stagger of each other.
        foreach (var (site, day) in raids)
            Assert.DoesNotContain(raids, r => r.Site != site && Math.Abs(r.Day - day) < t.Content.Lairs.StaggerDays);
    }

    [Fact]
    public void SomeFiresTakeButStoneNeverBurns()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        var t = sim.Town;
        sim.RunDays(40);
        var by = new Culprit("the cult", "a hooded man", "the Pit");
        // Everyone home to guard it, whoever was out exploring that night.
        foreach (var h in t.Heroes) h.Hero!.AwayUntil = null;
        var timber = t.Buildings.Where(b => b.Complete && !b.Def.IsGrand && b.Def.Zone is Zone.Residential or Zone.Commercial or Zone.Industry or Zone.Farmland).ToList();
        Assert.NotEmpty(timber);
        int landed = 0;
        for (int i = 0; i < 90; i++)
        {
            // Rebuilt at once, so the odds stay the same each night.
            foreach (var b in timber) b.BuildProgress = b.Def.BuildHours;
            if (Blows.Arson(t, by, 1)) landed++;
        }
        // The watch would stop every one of these; about a third slip through anyway.
        Assert.InRange(landed, 15, 45);

        // Turn every mark to stone: nothing catches.
        var stone = t.Content.Buildings.First(b => b.Stone > 0 && b.Zone == Zone.Residential);
        foreach (var b in timber) b.Def = stone;
        foreach (var b in timber) b.BuildProgress = b.Def.BuildHours;
        for (int i = 0; i < 30; i++) Assert.False(Blows.Arson(t, by, 1000));
        Assert.All(timber, b => Assert.True(b.Complete));
    }

    [Fact]
    public void TheStripWarnsBeforeFearEmptiesTheTownAndCountsTheDays()
    {
        var t = new Simulation(new SimulationOptions { Seed = 4 }).Town;
        var d = t.Content.Saga.Dread;
        t.Saga.Dread = d.WarnAt - 1;
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Kind == Alerts.Fear);
        t.Saga.Dread = d.WarnAt + 1;
        Assert.Contains(Alerts.Standing(t), a => a.Kind == Alerts.Fear && a.Text.StartsWith("Fear", StringComparison.Ordinal));
        t.Saga.Dread = d.AbandonAt + 1;
        t.Saga.AbandonDays = d.AbandonDays - 5;
        Assert.Contains(Alerts.Standing(t), a => a.Kind == Alerts.Fear && a.Text == "Town gives up in 5 days");
    }

    [Fact]
    public void ChecksGetHarderEachAct()
    {
        var t = new Simulation(new SimulationOptions { Seed = 5 }).Town;
        var o = new StoryOption { Dc = 14 };
        t.Saga.Act = 0;
        int early = Stories.DcOf(t, o);
        t.Saga.Act = 2;
        Assert.True(Stories.DcOf(t, o) > early);
    }
}
