using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class CannonsTests
{
    private static Town Walled()
    {
        var t = new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false, Enemy = false }).Town;
        t.Defenses.Palisade = PalisadeState.Built;
        t.Defenses.Towers = 2;
        return t;
    }

    [Fact]
    public void NoGunsUntilBombardsAndAWall()
    {
        var t = Walled();
        Assert.Equal(0, Cannons.Guns(t));
        t.Research.Known.Add("bombards");
        Assert.Equal(3, Cannons.Guns(t));
        t.Defenses.Palisade = PalisadeState.None;
        Assert.Equal(0, Cannons.Guns(t));
    }

    [Fact]
    public void ArcaneGunsHitHarderAndPickTheirMode()
    {
        var t = Walled();
        t.Research.Known.Add("bombards");
        var lines = new List<string>();
        double basic = 100 - Cannons.Volley(t, "warband", "", 100, 1, lines);
        t.Research.Known.Add("arcane-batteries");
        double blast = 100 - Cannons.Volley(t, "warband", "", 100, 1, lines);
        double piercing = 100 - Cannons.Volley(t, "tribe", "cloud-lords", 100, 1, lines);
        Assert.True(blast > basic && piercing > blast, $"{basic} {blast} {piercing}");
        Assert.Equal("flame", Cannons.Mode(t, "undead")!.Id);
        Assert.Equal("piercing", Cannons.Mode(t, "tribe", "storm-kings")!.Id);
    }

    [Fact]
    public void WarWagonsComeOnlyAfterTheFirstSiegeAndWeakenTheGate()
    {
        var t = Walled();
        var lines = new List<string>();
        Assert.Equal(1, Cannons.Wagons(t, "warband", 0, 0, lines));
        Assert.Empty(lines);
        foreach (var h in t.Heroes) h.Hero!.Hp = 0;
        double gate = Cannons.Wagons(t, "warband", 3, 0, lines);
        Assert.NotEmpty(lines);
        Assert.True(gate < 1);
        Assert.Equal(1, Cannons.Wagons(t, "undead", 3, 0, []));
    }
}
