using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class BastionsTests
{
    /// <summary>Run until the first company asks for a bastion (the player answers, so it waits).</summary>
    private static (Simulation Sim, BastionRequest Request) Asked(int seed = 3)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoBastions = false });
        for (int d = 0; d < 400; d++)
        {
            sim.RunDays(1);
            if (Bastions.Pending(sim.Town).FirstOrDefault() is { } r) return (sim, r);
        }
        throw new Exception("no company asked for a bastion");
    }

    [Fact]
    public void AFamousCompanyAsksAndTheAlertWaitsOnTheCouncil()
    {
        var (sim, r) = Asked();
        var t = sim.Town;
        var members = Intel.Members(t, r.Company);
        Assert.True(members.Count >= t.Content.Bastions.Found.Members);
        Assert.True(members.Max(m => m.Hero!.Level) >= t.Content.Bastions.Found.Level);
        Assert.Contains(Alerts.Standing(t), a => a.Kind == Alerts.Bastion);
        Assert.Empty(t.Bastions);
    }

    [Fact]
    public void GrantingRaisesAKeepTheTownBuildsAndCompanyMembersMoveIn()
    {
        var (sim, r) = Asked();
        var t = sim.Town;
        double treasury = t.Treasury;
        Assert.True(sim.AnswerBastion(r.Id, BastionAnswer.Grant));
        Assert.False(sim.AnswerBastion(r.Id, BastionAnswer.Refuse));
        sim.RunDays(1);
        var b = Assert.Single(t.Bastions);
        Assert.Equal(r.Company, b.Company);
        Assert.Equal(BastionRequestStatus.Founded, r.Status);
        Assert.True(t.Treasury > treasury - 50, "the land was sold to them");
        var keep = Bastions.Keep(t, b);
        Assert.True(keep.Def.Bastion);
        Assert.All(Bastions.Members(t, b), m => Assert.Equal(b.Id, m.Hero!.BastionId));

        sim.RunDays(40);
        Assert.True(keep.Complete);
        // Members come and go on delves; within a fortnight one is home and sleeping in the keep.
        for (int d = 0; d < 14 && !Bastions.Members(t, b).Any(m => !m.IsAway); d++) sim.RunDays(1);
        Assert.Contains(Bastions.Members(t, b).Where(m => !m.IsAway), m => Bastions.Lodges(t, m) && m.Hero!.Lodging);
        Assert.Contains(Systems.Loyalty.Factors(t, Bastions.Members(t, b)[0]), f => f.Why.StartsWith("lives at"));
    }

    [Fact]
    public void FacilitiesHireTownsfolkAndStandOnTheWalls()
    {
        // The first of a few towns where a company is at home in a bastion with facilities on the day.
        var (t, found) = new[] { 2, 1, 3, 4 }.Select(seed =>
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            sim.RunDays(320);
            return (sim.Town, sim.Town.Bastions.FirstOrDefault(x => !x.Abandoned && !x.Idle && x.Facilities.Count > 0));
        }).First(x => x.Item2 != null);
        var b = found!;
        var staff = b.Facilities.Select(t.Building).Where(f => f.Complete).SelectMany(f => f.Workers).ToList();
        Assert.NotEmpty(staff);
        Assert.All(staff, id => Assert.Null(t.Agent(id).Hero));
        Assert.True(b.Turns > 0);
        Assert.NotEmpty(b.Recent);
        if (Bastions.Sentries(t) > 0) Assert.True(Bastions.Defense(t) > 0);
        // Nothing of the town's builds on a company's grounds but its own facilities.
        var plan = t.Plan;
        for (int i = 0; i < plan.Grounds.Length; i++)
            if (plan.Grounds[i] != 0 && plan.Taken[i] != 0)
                Assert.True(t.Lot(plan.Taken[i] - 1).BuildingId is int bid && t.Building(bid).Def.Bastion);
    }

    [Fact]
    public void RefusingSoursTheCompanyAndKeepsThemFromAskingAgain()
    {
        var (sim, r) = Asked();
        var t = sim.Town;
        var members = Intel.Members(t, r.Company);
        double before = members.Sum(m => m.Hero!.Loyalty);
        Assert.True(sim.AnswerBastion(r.Id, BastionAnswer.Refuse));
        Assert.True(members.Sum(m => m.Hero!.Loyalty) < before);
        Assert.True(Bastions.Refused(t, r.Company));
        sim.RunDays(20);
        Assert.DoesNotContain(t.BastionRequests, x => x.Company == r.Company && x.Id != r.Id);
        Assert.DoesNotContain(t.Bastions, x => x.Company == r.Company);
    }

    [Fact]
    public void SilenceIsTakenAsLeave()
    {
        var (sim, r) = Asked();
        sim.RunDays(sim.Town.Content.Bastions.Council.AnswerDays + 1);
        Assert.NotEqual(BastionRequestStatus.Pending, r.Status);
        Assert.NotEqual(BastionRequestStatus.Refused, r.Status);
    }
}
