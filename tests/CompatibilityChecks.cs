using System;
using System.Linq;
using System.Text.Json;
using SuperFantasyKingdom;
using WeeklyContracts;

internal static class CompatibilityChecks
{
    private static ResourceData Food => Kingdom.Bread;
    private static ResourceStorage Storage => CityManager.Instance.Store.Storage;
    private static RelicManager Relics => RelicManager.Instance;
    private static AddressablesManager Assets => AddressablesManager.Instance;

    private static Contract Start()
    {
        RaceManager.Instance = new();
        CityManager.Instance = new();
        ResourceManager.Instance = new();
        StatisticsManager.Instance = new();
        RelicManager.Instance = new();
        AddressablesManager.Instance = new();
        var c = new Contract { run = "fixture" };
        c.AtDawn(1);
        c.Decide(true);
        return c;
    }

    private static Relic Define(string id)
    {
        var relic = new Relic { Id = id };
        Assets.Definitions[id] = relic;
        Relics.Unlocked.Add(id);
        return relic;
    }

    private static bool Deliver(Contract c) => Kingdom.Deliver(c, Food);

    private static bool Collect(Contract contract, string id) =>
        Kingdom.CollectRelic(contract, id, Kingdom.ClaimTarget(id), e => throw e);

    private static string Save(Contract c)
    {
        Plugin.Current = c;
        string raw = JsonSerializer.Serialize(new
        {
            runID = c.run, hash = "save-hash", caravanCommissionsV1 = new { pending = true },
            coins = ResourceManager.Instance.Coins, packets = Storage.Packets.ToArray(), relics = Relics.Owned.Keys.ToArray()
        }) + "\n";
        SaveHooks.Save.Prefix("Human_game", ref raw);
        return raw;
    }

    private static Contract Load(string raw)
    {
        SaveHooks.BeginRead();
        SaveHooks.Observe("Human_game", raw);
        SaveHooks.Restore(UnityEngine.JsonUtility.FromJson<GameData>(raw));
        SaveHooks.EndRead();
        var json = JsonDocument.Parse(raw).RootElement;
        ResourceManager.Instance.Coins = json.GetProperty("coins").GetInt32();
        Storage.Packets.Clear();
        Storage.Packets.AddRange(json.GetProperty("packets").Deserialize<ResourcePacket[]>());
        Relics.Owned.Clear();
        foreach (var r in json.GetProperty("relics").EnumerateArray())
            Relics.Owned[r.GetString()] = Assets.GetRelic(r.GetString());
        return Plugin.Restored;
    }

    internal static void Run(Action<bool,string> check)
    {
        foreach (bool external in new[] { false, true })
        {
            var c = Start();
            var phalanx = new MoreRelics.Entry { Def = new() { id = "ownly_Phalanx", bossOnly = true } };
            var millstone = new MoreRelics.Entry { Def = new() { id = "ownly_Millstone" }, Available = false };
            MoreRelics.Registry.Entries = new[] { phalanx, millstone };
            RewardPolicy.Bind(external ? typeof(MoreRelics.Registry).Assembly : null, s => throw new Exception(s));
            var ids = external ? new[] { "native", "ownly_Cornucopia", "ownly_SousChef" } :
                new[] { "one", "two", "three" };
            foreach (var id in ids) Define(id);
            if (external)
            {
                Define("ownly_Phalanx");
                Define("ownly_Millstone");
                check(!Kingdom.CanClaim("ownly_Phalanx") && !Kingdom.CanClaim("ownly_Millstone"),
                    "External source excludes boss-only and superseded definitions");
                // BakeryUsesWheat changes the input recipe. MoreRelics omits Millstone
                // when that plugin is present. Tavern food is separate from free city stock.
                Storage.Packets.Add(new ResourcePacket(1, Food.type, 2, true));
                Storage.GenerateResource(ResourceType.Alternate, 12);
            }
            Storage.GenerateResource(Food.type, 7);
            check(Deliver(c) && c.rewards.Order().SequenceEqual(ids.Order()) &&
                Kingdom.Stock(Food) == 1 && !Deliver(c), $"{(external ? "Modded" : "Standalone")} catalog and six-bread delivery");
            if (external) check(Storage.Packets.Single(p => p.Reserved).Amount == 2,
                "Worker reservation survives delivery from bakery output");
            string earned = Save(c);
            c = Load(earned);
            Assets.Definitions.Remove(ids[0]);
            check(!Collect(c, ids[0]) && c.stage == Stage.Earned,
                "Withdrawn definition after paid checkpoint preserves remaining choices");
            bool repeated = false;
            Relics.Found = () => repeated = Collect(c, ids[1]);
            check(Collect(c, ids[1]) && !repeated && Relics.Owned.Count == 1,
                "One native grant under a competing claim");
            string claimed = Save(c);
            c = Load(claimed);
            check(!Collect(c, ids[2]) && Kingdom.Stock(Food) == 1 &&
                Relics.Owned.Keys.Single() == ids[1], "Reload retains one charge and one reward");
            check(JsonDocument.Parse(claimed).RootElement.GetProperty("caravanCommissionsV1").GetProperty("pending").GetBoolean(),
                "Neighbor checkpoint extension preserved");

            // Cold tavern load, backup selection, then reserialization of vanilla data.
            SaveHooks.BeginRead();
            SaveHooks.Observe("Human_game", claimed);
            SaveHooks.Observe("Human_game", earned);
            SaveHooks.Restore(new GameData { runID = c.run, hash = "different" });
            check(Plugin.Restored == null, "Foreign checkpoint hash rejected");
            SaveHooks.Restore(new GameData { runID = c.run, hash = "save-hash" });
            SaveHooks.EndRead();
            string tavernSave = "{\"runID\":\"fixture\",\"hash\":\"next\"}\n";
            SaveHooks.Save.Prefix("Human_game", ref tavernSave);
            check(JsonDocument.Parse(tavernSave).RootElement.GetProperty("sfkWeeklyContracts")
                .GetProperty("stage").GetInt32() == (int)Stage.Earned, "Tavern rewrite carries selected backup state");
        }

        var paid = Start();
        RaceManager.Instance.Food.type = ResourceType.Alternate;
        Storage.Packets.Add(new ResourcePacket(1, Food.type, 8, false));
        Storage.Packets.Add(new ResourcePacket(2, Food.type, 2, true));
        var a = Define("A"); var b = Define("B"); Define("C");
        ResourceManager.Instance.Limit = 5;
        check(!Deliver(paid) && Storage.Packets.Count == 2, "Patched availability veto preserves stacked stock");
        ResourceManager.Instance.Limit = int.MaxValue;
        check(Deliver(paid) && Kingdom.Stock(Food) == 2 && Storage.Packets.Any(p => p.Id == 2 && p.Reserved),
            "Kingdom equivalent: charge six units from one eight-unit stack");
        a.Level = 99; b.Law = true; Relics.Unlocked.Remove("C");
        check(!Kingdom.CanClaim("A") && !Kingdom.CanClaim("B") && !Kingdom.CanClaim("C"),
            "Changed level, law and unlock gates are rechecked");
        paid = Load(Save(paid));
        RaceManager.Instance.Food.type = ResourceType.Bread;
        check(!Kingdom.ReturnPayment(paid) && paid.stage == Stage.Earned,
            "Missing paid equivalent leaves refund pending");
        RaceManager.Instance.Food.type = ResourceType.Alternate;
        check(Kingdom.ReturnPayment(paid) &&
            Kingdom.Stock(Food) == 8, "All choices ineligible: refund six original units");
        paid = Load(Save(paid));
        check(!paid.Refund(Kingdom.CanClaim, () => true) && !Collect(paid, "A") &&
            Relics.Owned.Count == 0 && Kingdom.Stock(Food) == 8, "Refund checkpoint closes both settlement paths");

        var competingSpend = Start();
        Define("one"); Define("two"); Define("three");
        Storage.GenerateResource(Food.type, 9);
        ResourceManager.Instance.AllowSpend = false;
        check(!Deliver(competingSpend) && Kingdom.Stock(Food) == 9, "Native affordability veto leaves payment open");
        ResourceManager.Instance.AllowSpend = true;
        int notices = 0;
        ResourceManager.Instance.Changed = () =>
        {
            notices++;
            Storage.TakeResourcePacket(Storage.Packets[0].Id, false);
            check(!Deliver(competingSpend) && competingSpend.stage == Stage.Earned,
                "Competing spender sees the paid checkpoint and cannot re-enter payment");
        };
        check(Deliver(competingSpend) && notices == 1 && Kingdom.Stock(Food) == 2,
            "Nine bread minus one delivery and one competing withdrawal");

        Start();
        Storage.GenerateResource(Food.type, 7);
        Storage.BlockAfter = 2;
        check(!Kingdom.Spend(Food) && Kingdom.Stock(Food) == 7, "Partial packet debit rolls back");
        Storage.UsesPackets = false;
        Storage.Loose = 8;
        check(Kingdom.Spend(Food) && Storage.Loose == 2, "Non-packet storage pays exact units");
        CityManager.Instance.Store = null;
        var starting = ResourceManager.Instance.Starting;
        starting[Food.type] = 6;
        check(Kingdom.Spend(Food) && starting[Food.type] == 0 && !Kingdom.Spend(Food),
            "Starting stock without a building");
        var competing = Define("competing");
        var upgrade = Define("upgrade");
        competing.Upgrade = upgrade;
        Relics.Owned["competing"] = competing;
        check(Kingdom.ClaimTarget("competing") == upgrade && !Kingdom.CanClaim("upgrade"),
            "Competing acquisition changes the displayed claim target to the registered upgrade");
        Assets.Definitions.Remove("competing");
        check(!Kingdom.CanClaim("competing"), "A stale upgradable owned instance cannot hide a removed definition");
        Define("race").Race = Races.Undead;
        Define("unit").Unit = "missing";
        Define("racial").Racial = true;
        Define("generic").Generic = true;
        check(!Kingdom.CanClaim("race") && !Kingdom.CanClaim("unit") && !Kingdom.CanClaim("racial") &&
            Kingdom.CanClaim("generic"), "Current race, unit dependency and relic category");

        var order = Start();
        order.stage = Stage.Offered;
        var wood = new ResourceData { type = ResourceType.Wood };
        RaceManager.Instance.Materials[ResourceType.Wood] = wood;
        RaceManager.Instance.Materials[ResourceType.Stone] = wood;
        Storage.GenerateResource(ResourceType.Wood, 5);
        Storage.Packets.Add(new ResourcePacket(90, ResourceType.Board, 12, true));
        RaceManager.Instance.Materials[ResourceType.Board] = new ResourceData { type = ResourceType.Board };
        order.offers = Kingdom.Offers(1);
        check(order.offers.SequenceEqual(new[] { OrderKind.Timber }),
            "Offer menu excludes reserved boards, duplicate kingdom equivalents and unavailable relics");
        order = Load(Save(order));
        check(!order.Accept(OrderKind.Boards) && order.Accept(OrderKind.Timber) && !order.Accept(OrderKind.Stone),
            "Checkpointed menu accepts exactly one listed contract");
        order = Load(Save(order));
        check(order.Terms.Quantity == 3 && order.Terms.Coins == 2 &&
            Kingdom.Deliver(order, wood) && !Kingdom.Deliver(order, wood) && Kingdom.Stock(wood) == 2,
            "Accepted supply contract retains terms and pays only its three units");
        order = Load(Save(order));
        bool nested = false;
        ResourceManager.Instance.Changed = () => nested = Kingdom.CollectCoins(order, e => throw e);
        check(Kingdom.CollectCoins(order, e => throw e) && !nested && !order.Refund(_ => false, () => true) &&
            ResourceManager.Instance.Coins == 2, "Coin payout settles before reentrant reward or refund");
        order = Load(Save(order));
        check(!Kingdom.CollectCoins(order, e => throw e) && ResourceManager.Instance.Coins == 2,
            "Claimed coin checkpoint prevents repeat grant");

        CheckFailedPayouts(check);
        CheckOffersAndUpgrades(check);
    }

    private static void CheckFailedPayouts(Action<bool, string> check)
    {
        foreach (bool coins in new[] { false, true })
        foreach (bool after in new[] { false, true })
        {
            var c = Start();
            foreach (string id in new[] { "base", "two", "three" }) Define(id);
            c.selected = coins ? OrderKind.Stone : OrderKind.Bread;
            var material = Food;
            if (coins)
            {
                material = new ResourceData { type = ResourceType.Stone };
                RaceManager.Instance.Materials[ResourceType.Stone] = material;
            }
            Storage.GenerateResource(material.type, c.Terms.Quantity);
            check(Kingdom.Deliver(c, material), "Paid fixture for provider failure");
            var rm = ResourceManager.Instance;
            rm.Coins = 11;
            var target = Assets.GetRelic("base");
            int warnings = 0;
            string inFlight = null;
            bool Claim() => coins ? Kingdom.CollectCoins(c, _ => warnings++) :
                Kingdom.CollectRelic(c, "base", target, _ => warnings++);
            void Fail()
            {
                if (Claim()) throw new InvalidOperationException("Duplicate grant");
                inFlight = Save(c);
                throw new Exception("Injected payout failure");
            }
            if (coins && after) rm.AfterCredit = Fail;
            if (coins && !after) rm.BeforeCredit = Fail;
            if (!coins && after) Relics.Found = Fail;
            if (!coins && !after) Relics.BeforeGrant = Fail;
            string label = (coins ? "Coin" : "Relic") + (after ? " post-transfer" : " pre-transfer");
            check(Claim() == after && warnings == 1 && c.stage == (after ? Stage.Claimed : Stage.Earned), label + " reconciled");
            rm.BeforeCredit = rm.AfterCredit = Relics.BeforeGrant = Relics.Found = null;
            string settled = Save(c);
            foreach (string checkpoint in new[] { inFlight, settled })
            {
                c = Load(checkpoint);
                check(c.stage == (after ? Stage.Claimed : Stage.Earned) && Claim() == !after && !Claim() &&
                    rm.Coins == (coins ? 14 : 11) && Relics.Owned.Count == (coins ? 0 : 1) &&
                    Kingdom.Stock(material) == 0, label + " checkpoint permits exactly one payout and one payment");
            }
        }

        var supply = Start();
        supply.selected = OrderKind.Timber;
        supply.stage = Stage.Earned;
        ResourceManager.Instance.Changed = () => { ResourceManager.Instance.Coins -= 2; throw new Exception("listener failed"); };
        int notices = 0;
        check(Kingdom.CollectCoins(supply, _ => notices++) && notices == 1 && ResourceManager.Instance.Coins == 0 &&
            !Kingdom.CollectCoins(Load(Save(supply)), _ => notices++) && StatisticsManager.Instance.Earned == 2,
            "Competing spend in credit notification cannot erase a completed payout");
    }

    private static void CheckOffersAndUpgrades(Action<bool, string> check)
    {
        Start();
        var race = RaceManager.Instance;
        race.Food.type = ResourceType.Wood;
        Storage.GenerateResource(ResourceType.Wood, 3);
        check(Kingdom.Offers(1).SequenceEqual(new[] { OrderKind.Timber }),
            "Affordable shared material remains available when the bread reward is unavailable");
        foreach (string id in new[] { "base", "two", "three" }) Define(id);
        check(Kingdom.Offers(1).SequenceEqual(new[] { OrderKind.Bread }), "Eligible bread job deduplicates shared material");
        Start();
        foreach (var type in new[] { ResourceType.Wood, ResourceType.Stone, ResourceType.Board,
            ResourceType.Wheat, ResourceType.Fish, ResourceType.Water })
        {
            RaceManager.Instance.Materials[type] = new ResourceData { type = type };
            Storage.GenerateResource(type, 8);
        }
        var weeks = new[] { 1, 8, 15, 22 }.Select(Kingdom.Offers).ToArray();
        check(weeks.All(w => w.Length == 2) && weeks.SelectMany(w => w).Distinct().Count() == 6 &&
            !weeks[0].Intersect(weeks[1]).Any(), "Four well-stocked weeks cover every supply job");

        var c = Start();
        var basis = Define("basis"); Define("two"); Define("three");
        Storage.GenerateResource(Food.type, 6);
        check(Deliver(c), "Three eligible base relics before competing acquisition");
        var upgrade = Define("limited");
        basis.Upgrade = upgrade;
        Relics.Owned[basis.Id] = basis;
        var entry = new MoreRelics.Entry { Def = new() { id = upgrade.Id } };
        MoreRelics.Registry.Entries = new[] { entry };
        RewardPolicy.Bind(typeof(MoreRelics.Registry).Assembly, e => throw new Exception(e));
        check(Kingdom.ClaimTarget(basis.Id) == upgrade, "Eligible registered upgrade is previewed");
        entry.Def.bossOnly = true;
        check(Kingdom.ClaimTarget(basis.Id) == null && !Kingdom.CollectRelic(c, basis.Id, upgrade, e => throw e) &&
            c.stage == Stage.Earned && !Relics.HasRelic(upgrade.Id), "Boss-only upgrade rejected after preview");
        entry.Def.bossOnly = false; entry.Available = false;
        check(!Kingdom.CanClaim(basis.Id) && Collect(c, "two"), "Unavailable upgrade leaves alternate reward claimable");
        RewardPolicy.Bind(null, _ => { });
    }
}
