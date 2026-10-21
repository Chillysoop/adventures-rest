using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Client;

public partial class Main
{
    /// <summary>
    /// --secrets: a motion before the council, split down the middle, and a secret in hand on every councillor but the one
    /// who moved it, so the Council window shows the levers. Staging only: the town it plays never sees this.
    /// </summary>
    private void StageSecrets()
    {
        var t = _sim.Town;
        var seated = t.Seats.Where(s => s.HolderId != null).ToList();
        if (seated.Count == 0) return;
        if (Council.PendingMotion(t) == null)
        {
            var mover = seated[0];
            t.Motions.Add(new Motion
            {
                Id = t.Motions.Count + 1,
                Kind = MotionKind.Festival,
                Proposer = mover.Def.Id,
                ProposerId = mover.HolderId!.Value,
                Text = "the town hold a festival, at a cost of 120 gold",
                Cost = 120,
                ProposedTick = t.Tick,
                DeadlineTick = t.Tick + 3 * 24,
                Votes = seated.Select((s, i) => new Vote(s.Def.Id, i == 0)).ToList(),
            });
        }
        string[] kinds = ["smuggling", "debts", "lover"];
        int k = 0;
        foreach (var s in seated.Skip(1))
        {
            int id = s.HolderId!.Value;
            t.Secrets.RemoveAll(x => x.AgentId == id);
            t.Secrets.Add(new Secret { AgentId = id, Kind = kinds[k++ % kinds.Length], Known = true });
        }
        _councilSeat = 1;
        SetTab(Tab.Council);
    }
}
