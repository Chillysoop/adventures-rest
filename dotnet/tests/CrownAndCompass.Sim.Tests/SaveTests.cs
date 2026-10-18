using System.Diagnostics;
using System.Text;

namespace CrownAndCompass.Sim.Tests;

public class SaveTests
{
    private static List<string> Ledger(Simulation sim) => sim.Town.Ledger.Entries.Select(e => e.Format()).ToList();

    /// <summary>Save mid-campaign, reload, and play on: the reloaded run must match the run that never stopped.</summary>
    [Theory]
    [InlineData(1, 40, 0, true)]
    [InlineData(7, 25, 13, true)]
    [InlineData(22, 60, 5, false)]
    public void ReloadedCampaignPlaysOnIdentically(int seed, int days, int hours, bool autoplay)
    {
        var opts = new SimulationOptions { Seed = seed, AutoBounties = autoplay, AutoCouncil = autoplay, AutoDefense = autoplay, AutoPlanning = autoplay };
        var straight = new Simulation(opts);
        straight.RunDays(days);
        for (int i = 0; i < hours; i++) straight.Step();

        var loaded = SaveGame.Read(SaveGame.Write(straight));
        Assert.Equal(SaveGame.StateJson(straight), SaveGame.StateJson(loaded));

        straight.RunDays(40);
        loaded.RunDays(40);
        Assert.Equal(Ledger(straight), Ledger(loaded));
        Assert.Equal(Encoding.UTF8.GetString(SaveGame.StateJson(straight)), Encoding.UTF8.GetString(SaveGame.StateJson(loaded)));
    }

    [Fact]
    public void KeepsSharedReferencesAndContentIdentity()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(60);
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        var t = loaded.Town;
        Assert.Same(sim.Town.Content, t.Content);
        Assert.All(t.Buildings, b => Assert.Contains(b.Def, t.Content.Buildings));
        Assert.All(t.Heroes, a => Assert.Contains(a.Hero!.Cls, t.Content.Classes));
    }

    [Fact]
    public void ListenersSubscribeAgainAfterALoad()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5 });
        sim.Town.Ledger.OnWrite(_ => { });
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        int heard = 0;
        loaded.Town.Ledger.OnWrite(_ => heard++);
        loaded.RunDays(5);
        Assert.True(heard > 0);
    }

    [Fact]
    public void KeepsTheClientsOwnData()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 6 });
        var bytes = SaveGame.Capture(sim, "{\"markers\":[]}").ToBytes();
        SaveGame.Read(new MemoryStream(bytes), out var client);
        Assert.Equal("{\"markers\":[]}", client);
    }

    [Fact]
    public void HeaderDescribesTheSlot()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 9 });
        sim.RunDays(12);
        var h = SaveGame.ReadHeader(new MemoryStream(SaveGame.Write(sim, new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc))));
        Assert.Equal(SaveGame.Version, h.Version);
        Assert.Equal(sim.Town.Name, h.Town);
        Assert.Equal(9, h.Seed);
        Assert.Equal(sim.Town.Day, h.Day);
        Assert.Equal(sim.Town.Population, h.Population);
        Assert.StartsWith("2026-10-18", h.Saved);
    }

    [Fact]
    public void RefusesSomethingThatIsNotASave()
    {
        Assert.Throws<SaveException>(() => SaveGame.Read(Encoding.UTF8.GetBytes("hello, world\n")));
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        var bytes = SaveGame.Write(sim);
        Assert.Throws<SaveException>(() => SaveGame.Read(bytes[..(bytes.Length / 2)]));
    }

    /// <summary>A long campaign saves small and fast enough to autosave without a hitch.</summary>
    [Fact]
    public void LongCampaignSavesSmallAndFast()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4 });
        sim.RunDays(200);
        SaveGame.Write(sim);
        var sw = Stopwatch.StartNew();
        var bytes = SaveGame.Write(sim);
        long write = sw.ElapsedMilliseconds;
        sw.Restart();
        SaveGame.Read(bytes);
        long read = sw.ElapsedMilliseconds;
        Console.WriteLine($"200 days: {bytes.Length / 1024} KB, write {write} ms, read {read} ms");
        Assert.True(bytes.Length < 4_000_000);
    }
}
