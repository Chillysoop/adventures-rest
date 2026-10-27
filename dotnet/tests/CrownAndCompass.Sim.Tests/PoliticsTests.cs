using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class PoliticsTests
{
    private static Simulation Town(int seed = 2, string government = "", int days = 40)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoCouncil = false, Government = government });
        sim.RunDays(days);
        return sim;
    }

    [Fact]
    public void EveryLawShowsTheBlocsItPleasesAndAngers()
    {
        var sim = Town();
        var t = sim.Town;
        var (pleased, angered) = Politics.Reactions(t, "policy:curfew", "dusk");
        Assert.NotEmpty(pleased.Concat(angered));
        foreach (var b in pleased) Assert.True(Politics.Reaction(t, b, "policy:curfew", "dusk") > 0);
        foreach (var b in angered) Assert.True(Politics.Reaction(t, b, "policy:curfew", "dusk") < 0);
        // The gate and the levy carry tags too, and every policy level names its own.
        Assert.NotEmpty(Politics.Tags(t, "gate", "shut"));
        Assert.NotEmpty(Politics.Tags(t, "service", "conscription"));
        foreach (var p in Politics.Rules(t).Policies)
            foreach (var l in p.Levels.Skip(1)) Assert.NotEmpty(l.Tags);
    }

    [Fact]
    public void ALawInForceMovesItsBlocsAndPhasesIn()
    {
        var sim = Town();
        var t = sim.Town;
        var order = Politics.Blocs(t).First(b => b.Likes.Contains("order"));
        double before = Politics.Standing(t, order.Id);
        Assert.True(sim.SetPolicy("curfew", "dusk"));
        Assert.Equal("dusk", Politics.Current(t, "policy:curfew"));
        double first = Politics.Standing(t, order.Id);
        sim.RunDays(Politics.Rules(t).PhaseDays);
        double full = Politics.Standing(t, order.Id);
        Assert.True(full > first, $"{before} -> {first} -> {full}");
        Assert.True(full > before);
    }

    [Fact]
    public void ChangingALawCostsMandateAndFlipFloppingCostsMore()
    {
        var sim = Town();
        var t = sim.Town;
        t.Mandate = 80;
        double cost = Politics.Cost(t, "policy:tithe", "tenth");
        Assert.True(cost > 0);
        Assert.True(sim.SetPolicy("tithe", "tenth"));
        Assert.Equal(80 - cost, t.Mandate, 6);
        double again = Politics.Cost(t, "policy:tithe", "none");
        Assert.True(again > cost, $"{cost} then {again}");
        t.Mandate = again - 1;
        Assert.Equal("not enough Mandate", Politics.Blocked(t, "policy:tithe", "none"));
        Assert.False(sim.SetPolicy("tithe", "none"));
    }

    [Fact]
    public void PoliciesCostGoldEveryDay()
    {
        var sim = Town();
        var t = sim.Town;
        t.Mandate = 80;
        Assert.Equal(0, Politics.Upkeep(t));
        Assert.True(sim.SetPolicy("dole", "all"));
        Assert.True(Politics.Upkeep(t) > 0);
        sim.RunDays(1);
        Assert.True(t.AccountsYesterday.Get(TreasuryAccount.Laws, 0) < 0);
    }

    [Fact]
    public void AFreeTownPutsTaxesToAVoteInsteadOfSpendingMandate()
    {
        var sim = Town(government: "republic");
        var t = sim.Town;
        Assert.True(Politics.Voted(t, "tax"));
        double mandate = t.Mandate, rate = t.TaxRate;
        Assert.True(sim.SetTax(rate - 0.03));
        Assert.Equal(rate, t.TaxRate, 6);
        Assert.NotNull(Politics.Open(t, "tax"));
        Assert.Equal("the town is voting on it", Politics.Blocked(t, "tax", Js.Str(Js.Round((rate - 0.05) * 100) / 100)));
        sim.RunDays(Politics.Rules(t).Vote.Days + 1);
        var vote = t.Politics.Referenda.Single();
        Assert.True(vote.Yes >= 0);
        if (vote.Yes > 0.5) Assert.Equal(rate - 0.03, t.TaxRate, 6);
        else Assert.Equal(rate, t.TaxRate, 6);
    }

    [Fact]
    public void TheMayorCanPutMattersToTheTownVoteOrTakeThemBack()
    {
        var sim = Town();
        var t = sim.Town;
        t.Mandate = 80;
        Assert.False(Politics.Voted(t, "punish:theft"));
        Assert.True(sim.SetTownVote("laws", true));
        Assert.True(Politics.Voted(t, "punish:theft"));
        Assert.True(t.Mandate < 80);
        Assert.True(sim.SetTownVote("laws", false));
        Assert.False(Politics.Voted(t, "punish:theft"));
        // A crown holds no votes.
        Assert.True(sim.SetGovernment("monarchy"));
        Assert.NotNull(Politics.VoteBlocked(t, "laws", true));
    }

    [Fact]
    public void IronRuleSendsTheCouncilHomeAndNoMotionsCome()
    {
        var sim = Town(seed: 3, government: "tyranny", days: 80);
        var t = sim.Town;
        Assert.All(t.Seats, s => Assert.Null(s.HolderId));
        Assert.Empty(t.Motions);
        Assert.False(Politics.HasCouncil(t));
        Assert.True(Politics.WatchFactor(t) > 1);
    }

    [Fact]
    public void AFreeTownHoldsItsMootAtTheYearsEnd()
    {
        var sim = Town(seed: 3, government: "republic", days: 0);
        var t = sim.Town;
        int year = (int)Seasons.YearLength(t);
        sim.RunDays(year + 1);
        Assert.Equal(1, t.Politics.Moots);
        Assert.True(t.Politics.Share >= 0);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("holds its moot"));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("The moot seats") || e.Text.Contains("keeps the seat"));
    }

    [Fact]
    public void AMayorWhoLosesTheMootIsTurnedOut()
    {
        var sim = Town(seed: 3, government: "republic", days: 0);
        var t = sim.Town;
        int year = (int)Seasons.YearLength(t);
        sim.RunDays(year - 3);
        for (int d = 0; d < 3 && t.Politics.Moots == 0; d++)
        {
            foreach (var b in Politics.Blocs(t)) t.City.Blocs[b.Id] = -100;
            t.Mandate = 0;
            sim.RunDays(1);
        }
        Assert.True(t.Politics.Moots == 1 && t.Politics.Share < 0.5, $"moots {t.Politics.Moots}, share {t.Politics.Share}");
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("lost the moot"));
        // The stand-in names the town's choice at once.
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("The town elects"));
        Assert.False(t.Mayoralty.Ousted);
        Assert.False(Mayoralty.Dead(t));
    }

    [Fact]
    public void AnOustedMayorCannotBeRaised()
    {
        var sim = Town(seed: 3, government: "republic", days: 5);
        var t = sim.Town;
        Mayoralty.Unseat(t, "lost the moot");
        Assert.True(Mayoralty.Dead(t));
        Assert.Equal("voted out", Mayoralty.RaiseRefused(t));
        Assert.True(sim.NameSuccessor(0));
        Assert.False(t.Mayoralty.Ousted);
    }

    [Fact]
    public void CouncillorsSpeakForTwoOrMoreBlocs()
    {
        var sim = Town(seed: 2, days: 60);
        var t = sim.Town;
        var seated = t.Seats.Where(s => s.HolderId != null).ToList();
        Assert.NotEmpty(seated);
        foreach (var s in seated) Assert.True(Politics.SeatBlocs(t, t.Agent(s.HolderId!.Value)).Count >= 2);
        // Their loyalty follows their blocs.
        var a = t.Agent(seated[0].HolderId!.Value);
        foreach (var (b, _) in Politics.SeatBlocs(t, a)) t.City.Blocs[b.Id] = -80;
        Politics.Changed(t);
        Assert.True(Politics.SeatLoyalty(t, a) < 0);
        foreach (var (b, _) in Politics.SeatBlocs(t, a)) t.City.Blocs[b.Id] = 80;
        Politics.Changed(t);
        Assert.True(Politics.SeatLoyalty(t, a) > 0);
    }

    [Fact]
    public void SituationsStartAboveOneLineAndEndBelowAnother()
    {
        var sim = Town(seed: 2, days: 60);
        var t = sim.Town;
        var bread = Politics.Rules(t).Situations.First(s => s.Reading == "unrest");
        t.Taxes.Unrest = bread.Start + 1;
        sim.RunDays(1);
        Assert.True(t.Politics.Situations.ContainsKey(bread.Id));
        t.Taxes.Unrest = (bread.Start + bread.End) / 2;
        sim.RunDays(1);
        Assert.True(t.Politics.Situations.ContainsKey(bread.Id));
        t.Taxes.Unrest = 0;
        sim.RunDays(1);
        Assert.False(t.Politics.Situations.ContainsKey(bread.Id));
    }

    [Fact]
    public void AKeptPromiseRaisesTrustAndABrokenOneCostsIt()
    {
        var sim = Town(seed: 2, days: 60);
        var t = sim.Town;
        var r = Politics.Rules(t).Demands;
        var bloc = Politics.Blocs(t).First(b => Politics.Wish(t, b) != null);
        var (law, option) = Politics.Wish(t, bloc)!.Value;
        t.Politics.Demands.Add(new Demand { Id = 99, Bloc = bloc.Id, Law = law, Option = option, Day = t.Day });
        Assert.NotNull(Politics.Ask(t));
        Assert.Contains(Alerts.Standing(t), a => a.Kind == Alerts.Ask);
        Assert.True(sim.AnswerBloc(99, true));
        double trust = t.Politics.Trust;
        t.Mandate = 100;
        if (law == "tax") Assert.True(sim.SetTax(double.Parse(option, System.Globalization.CultureInfo.InvariantCulture)));
        else Assert.True(Politics.Change(t, law, option));
        sim.RunDays(1);
        Assert.True(t.Politics.Trust > trust);
        Assert.Empty(t.Politics.Demands);

        // A second promise, left to lapse.
        t.Politics.Demands.Add(new Demand { Id = 100, Bloc = bloc.Id, Law = "policy:tithe", Option = Politics.Current(t, "policy:tithe") == "great" ? "none" : "great", Day = t.Day });
        Assert.True(sim.AnswerBloc(100, true));
        trust = t.Politics.Trust;
        sim.RunDays(r.DueDays + 1);
        Assert.True(t.Politics.Trust < trust);
    }

    [Fact]
    public void AFuriousBlocBreedsRadicalsWhoStrike()
    {
        var sim = Town(seed: 2, days: 60);
        var t = sim.Town;
        var r = Politics.Rules(t).Radicals;
        var bloc = Politics.Blocs(t).OrderByDescending(b => Politics.Size(t, b)).First();
        t.Watch = 0;
        // The watch may break a cell up before it acts; the bloc, still furious, breeds another.
        for (int d = 0; d < 120 && Politics.CellOf(t, bloc.Id) is not { Stage: >= 1 }; d++)
        {
            t.City.Blocs[bloc.Id] = -100;
            sim.RunDays(1);
        }
        var cell = Politics.CellOf(t, bloc.Id);
        Assert.True(cell != null, $"{bloc.Id} feels {Politics.Feeling(t, bloc.Id)}, furious {t.Politics.Furious.GetValueOrDefault(bloc.Id)}: " + string.Join(" | ", t.Ledger.Entries.Where(e => e.Text.Contains("radical") || e.Text.Contains("swear")).Select(e => e.Text)));
        Assert.True(cell!.Stage >= 1);
        Assert.Contains(Alerts.Standing(t), a => a.Kind == Alerts.Radicals);
        // Won back, they melt away.
        for (int d = 0; d < r.CalmDays + 2; d++)
        {
            t.City.Blocs[bloc.Id] = 50;
            sim.RunDays(1);
        }
        Assert.Null(Politics.CellOf(t, bloc.Id));
    }

    [Fact]
    public void TheDemocraticAndAuthoritarianRoadsReadTheGovernment()
    {
        var sim = Town(seed: 2, days: 10);
        var t = sim.Town;
        var free = Victory.Def(t, "commonwealth")!;
        var crown = Victory.Def(t, "crown")!;
        Assert.False(Victory.Terms(t, free)[0].Met);
        Assert.False(Victory.Terms(t, crown)[0].Met);
        t.Mandate = 100;
        Assert.True(sim.SetGovernment("republic"));
        Assert.True(Victory.Terms(t, free)[0].Met);
        Assert.False(Victory.Terms(t, crown)[0].Met);
    }
}
