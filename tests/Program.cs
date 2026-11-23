using System;
using System.Linq;
using System.Text.Json;
using WeeklyContracts;

internal static class Program
{
    private static readonly string[] Choices = { "one", "two", "three" };
    private static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    private static int passed;

    private static Contract Offer(int day = 1)
    {
        var contract = new Contract { run = "test-kingdom" };
        if (!contract.AtDawn(day)) throw new Exception("Missing offer");
        return contract;
    }

    private static Contract Reload(Contract c) =>
        JsonSerializer.Deserialize<Contract>(JsonSerializer.Serialize(c, Json), Json);

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
        passed++;
    }

    private static void Main()
    {
        var c = Offer();
        Check(c.deadline == 7 && !c.AtDawn(1), "One offer on repeated dawn");
        c.Decide(false);
        c = Reload(c);
        Check(c.stage == Stage.Declined && !c.AtDawn(2), "Decline survives checkpoint");
        Check(c.AtDawn(8) && c.deadline == 14, "New week has its own deadline");
        c.Advance(8, false, false);
        c.Decide(true);
        Check(c.stage == Stage.Expired, "Unanswered offer lapses when morning ends");

        c = Offer(5);
        c.Decide(true);
        c = Reload(c);
        Check(c.stage == Stage.Active && c.deadline == 7, "Midweek installation and active restore");
        int bread = 5;
        bool Spend()
        {
            if (bread < Contract.Price) return false;
            bread -= Contract.Price;
            return true;
        }
        Check(!c.Deliver(Choices, Spend) && bread == 5, "Insufficient bread");
        bread = 7;
        Check(!c.Deliver(new[] { "one", "one", "two" }, Spend) && bread == 7,
            "Three distinct relics required before taking bread");
        c.Advance(7, false, false);
        Check(c.Deliver(Choices, Spend) && bread == 1, "Delivery before boss night");
        Check(!c.Deliver(Choices, Spend) && bread == 1, "Repeated delivery");
        c = Reload(c);
        c.Advance(7, false, true);
        Check(c.stage == Stage.Earned && !c.AtDawn(8) && c.rewards.SequenceEqual(Choices),
            "Paid reward persists across deadline, dawn and reload");
        Check(!c.Collect("other", _ => true) && !c.Collect("two", _ => false) && c.stage == Stage.Earned,
            "Invalid choice or failed grant preserves reward");
        int grants = 0;
        Check(c.Collect("two", _ => ++grants == 1), "Successful grant");
        c = Reload(c);
        Check(!c.Collect("two", _ => ++grants == 1) && c.claimed == "two" && grants == 1,
            "Claimed reward survives reload without a second grant");

        c = Offer(21);
        c.Decide(true);
        c.Advance(21, false, true);
        Check(!c.Deliver(Choices, Spend) && c.stage == Stage.Expired, "Late confirmation rejected");
        Check(c.AtDawn(22) && c.deadline == 28, "Week four targets night 28");
        Check(!new Contract().AtDawn(29), "No offer without a future weekly boss");
        c = Offer();
        c.Decide(true);
        bool nestedPayment = false, nestedClaim = false;
        c.Deliver(Choices, () =>
        {
            nestedPayment = c.Deliver(Choices, () => true);
            return true;
        });
        c.Collect("one", _ =>
        {
            nestedClaim = c.Collect("one", __ => true);
            return true;
        });
        Check(!nestedPayment && !nestedClaim && c.stage == Stage.Claimed,
            "Synchronous game callbacks cannot pay or claim twice");
        Console.WriteLine($"{passed} contract checks passed.");
    }
}
