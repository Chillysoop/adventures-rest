using System.Text;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class GuildsTests
{
    /// <summary>A player's town (the council left to the mayor) grown until a guild has formed.</summary>
    private static (Simulation Sim, Town T, Guild G) Town(int seed = 1)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoCouncil = false, AutoHonours = false, Enemy = false });
        var t = sim.Town;
        for (int d = 0; d < 200 && t.Guilds.Count == 0; d++) sim.RunDays(1);
        Assert.NotEmpty(t.Guilds);
        var g = t.Guilds.OrderByDescending(x => Guilds.Members(t, x).Count).First();
        return (sim, t, g);
    }

    [Fact]
    public void AGuildFormsWithAMasterAHallAndASeat()
    {
        var (_, t, g) = Town();
        Assert.NotNull(g.MasterId);
        Assert.Contains(Guilds.Members(t, g), a => a.Id == g.MasterId);
        Assert.NotNull(g.HallId);
        Assert.NotNull(Council.FindSeat(t, g.Seat));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains(Guilds.Name(t, g), StringComparison.OrdinalIgnoreCase) && e.Text.Contains("guild master"));
    }

    [Fact]
    public void TheStandInMayorsTownFormsNoGuilds()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, Enemy = false });
        sim.RunDays(120);
        Assert.Empty(sim.Town.Guilds);
    }

    [Fact]
    public void AGuildLeansOnItsCouncillorsVote()
    {
        var (_, t, g) = Town();
        var d = Guilds.Def(t, g);
        var (kind, w) = d.Motions.First(kv => kv.Value != 0);
        Assert.Equal(w * Guilds.Rules(t).Backing, Guilds.Lean(t, g.Seat, kind), 6);
        Assert.Equal(0, Guilds.Lean(t, "no-such-seat", kind));
    }

    [Fact]
    public void ALapsedDemandAtTheBottomBringsAStrike()
    {
        var (sim, t, g) = Town();
        var r = Guilds.Rules(t);
        g.Want = Guilds.Def(t, g).Wants[0].Kind;
        g.Target = 1e9;
        g.Due = t.Day;
        g.Standing = r.StrikeAt + r.Lapsed - 1;
        sim.RunDays(1);
        Assert.True(Guilds.OnStrike(t, g));
        var member = Guilds.Members(t, g).First();
        Assert.True(Guilds.Striking(t, member));
        Assert.Contains(Alerts.Standing(t), a => a.Key == $"strike:{g.Id}");
        // It ends on its own after its days, sullen.
        sim.RunDays(r.StrikeDays + 1);
        Assert.False(Guilds.OnStrike(t, g));
        Assert.False(Guilds.Striking(t, member));
    }

    [Fact]
    public void TheWatchBreaksAStrikeAtACostInMandate()
    {
        var (sim, t, g) = Town();
        var r = Guilds.Rules(t);
        Guilds.Strike(t, g);
        t.Watch = 0;
        Assert.False(sim.BreakStrike(g.Id));
        t.Watch = r.BreakWatch;
        t.Mandate = 50;
        Assert.True(sim.BreakStrike(g.Id));
        Assert.False(Guilds.OnStrike(t, g));
        Assert.Equal(50 - r.BreakMandate, t.Mandate, 6);
        Assert.Equal(r.Broken, g.Standing, 6);
    }

    [Fact]
    public void PayingAGuildOffEndsItsStrike()
    {
        var (sim, t, g) = Town();
        Guilds.Strike(t, g);
        t.Treasury = 10_000;
        double cost = Guilds.PayCost(t, g);
        Assert.True(sim.PayOffGuild(g.Id));
        Assert.False(Guilds.OnStrike(t, g));
        Assert.Equal(10_000 - cost, t.Treasury, 6);
        Assert.False(sim.PayOffGuild(g.Id));
    }

    [Fact]
    public void MeetingATaxDemandCutsTaxesAndPleasesTheGuild()
    {
        var (sim, t, _) = Town();
        var g = t.Guilds.FirstOrDefault(x => Guilds.Def(t, x).Wants.Any(w => w.Kind == "tax"));
        if (g == null) return;
        t.TaxRate = 0.14;
        g.Want = "tax";
        g.Target = 0.12;
        g.Due = t.Day + 3;
        double before = g.Standing;
        Assert.True(sim.MeetGuildDemand(g.Id));
        Assert.Equal(0.12, t.TaxRate, 6);
        Assert.Equal("", g.Want);
        Assert.True(g.Standing > before);
    }

    [Fact]
    public void RaisingTaxesCrossesGuildsThatHateThem()
    {
        var (sim, t, g) = Town();
        var hater = t.Guilds.FirstOrDefault(x => Guilds.Def(t, x).Motions.TryGetValue(MotionKind.RaiseTax, out var w) && w < 0);
        if (hater == null) return;
        double before = hater.Standing;
        t.TaxRate = 0.1;
        Assert.True(sim.SetTax(0.12));
        Assert.True(hater.Standing < before);
    }

    [Fact]
    public void AGuildsTownSavesAndPlaysOnIdentically()
    {
        var (sim, _, g) = Town();
        Guilds.Strike(sim.Town, g);
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        sim.RunDays(20);
        loaded.RunDays(20);
        Assert.Equal(sim.Town.Ledger.Entries.Select(e => e.Format()), loaded.Town.Ledger.Entries.Select(e => e.Format()));
        Assert.Equal(Encoding.UTF8.GetString(SaveGame.StateJson(sim)), Encoding.UTF8.GetString(SaveGame.StateJson(loaded)));
    }

    [Fact]
    public void AGuildsMasterIsANotableAndWinningThemCalmsTheGuild()
    {
        var (sim, t, g) = Town();
        // One master rises a day, guild by guild.
        for (int d = 0; d < 10 && !t.Notables.Any(x => !x.Gone && x.AgentId == g.MasterId); d++) sim.RunDays(1);
        var n = t.Notables.FirstOrDefault(x => !x.Gone && x.AgentId == g.MasterId);
        Assert.NotNull(n);
        Assert.Same(g, Notables.GuildOf(t, n!));
        g.Standing = 40;
        n!.Regard = 100;
        double before = g.Standing;
        sim.RunDays(1);
        var r = t.Content.Guilds;
        // The master's friendship outpulls the guild's own drift back toward its rest.
        Assert.True(g.Standing > before + (r.Rest - before) * r.Drift, $"standing {before} -> {g.Standing}");
    }
}
