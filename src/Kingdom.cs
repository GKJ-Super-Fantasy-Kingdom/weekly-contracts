using System;
using System.Collections.Generic;
using System.Linq;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;

namespace WeeklyContracts
{
    internal static class Kingdom
    {
        internal static ResourceData Bread => RaceManager.Instance.GetResourceData(ResourceType.Bread);

        internal static ResourceData Material(OrderKind kind) => RaceManager.Instance.GetResourceData(OrderTerms.Get(kind).Slot);

        internal static OrderKind[] Offers(int day)
        {
            var supplies = new[] { OrderKind.Timber, OrderKind.Stone, OrderKind.Boards, OrderKind.Grain,
                OrderKind.Fish, OrderKind.Water };
            var menu = new List<OrderKind>();
            var materials = new HashSet<ResourceType>();
            var bread = Bread;
            bool offerBread = Supported(bread) && Choices().Length == 3;
            if (offerBread) materials.Add(bread.type);
            int rotation = (Math.Max(0, day - 1) / 7) * 2;
            for (int i = 0; i < supplies.Length && menu.Count < 2; ++i)
            {
                var kind = supplies[(rotation + i) % supplies.Length];
                var material = Material(kind);
                int cost = OrderTerms.Get(kind).Quantity;
                if (Supported(material) && Stock(material) >= cost &&
                    ResourceManager.Instance.CanBeBuilt(new ResourceAmount(material.type, cost)) && materials.Add(material.type))
                    menu.Add(kind);
            }
            if (offerBread) menu.Add(OrderKind.Bread);
            return menu.ToArray();
        }

        internal static bool Supported(ResourceData food) =>
            food != null && food.type != ResourceType.None && food.buildingToTakeFrom != BuildingType.None;

        internal static ResourceData PaidFood(Contract contract)
        {
            // Older checkpoints only charged the literal Bread resource.
            var type = contract.paidType == 0 ? ResourceType.Bread : (ResourceType)contract.paidType;
            var food = Bread;
            if (!Supported(food) || food.type != type) food = RaceManager.Instance.GetResourceData(type);
            return Supported(food) && food.type == type ? food : null;
        }

        private static IEnumerable<ResourcePacket> FreePackets(ResourceStorage storage, ResourceType type) =>
            storage.GetAllResourcePackets().Where(p => p != null && p.GetResourceType() == type &&
                !p.IsReserved() && p.GetAmount() > 0);

        internal static int Stock(ResourceData food)
        {
            if (!Supported(food)) return 0;
            var resources = ResourceManager.Instance;
            var building = CityManager.Instance.GetBuilding(food.buildingToTakeFrom);
            int physical;
            if (building == null)
                resources.GetStartingResourcesDictionary().TryGetValue(food.type, out physical);
            else
            {
                var storage = building.GetStorage();
                physical = storage.ShouldGenerateResourcePackets()
                    ? FreePackets(storage, food.type).Sum(p => p.GetAmount())
                    : storage.GetResourceQuantity(food.type, false);
            }
            return Math.Max(0, Math.Min(physical, resources.GetResourceAvailableAmount(food.type, false)));
        }

        internal static bool Spend(ResourceData food, int quantity = Contract.Price)
        {
            if (Stock(food) < quantity ||
                !ResourceManager.Instance.CanBeBuilt(new ResourceAmount(food.type, quantity))) return false;
            var building = CityManager.Instance.GetBuilding(food.buildingToTakeFrom);
            if (building == null)
            {
                ResourceManager.Instance.GetStartingResourcesDictionary()[food.type] -= quantity;
                return true;
            }
            var storage = building.GetStorage();
            if (!storage.ShouldGenerateResourcePackets())
            {
                storage.RemoveResourceWithoutPackage(food.type, quantity);
                return true;
            }

            // The native shop spends whole packets. Transfer actual units, returning change
            // from the last stack. Keep receipts until all six units are accounted for.
            var taken = new List<ResourcePacket>();
            int remaining = quantity;
            foreach (var packet in FreePackets(storage, food.type).ToArray())
            {
                if (packet.IsReserved()) continue;
                var receipt = storage.TakeResourcePacket(packet.GetIdentifier(), false);
                if (receipt == null) continue;
                taken.Add(receipt);
                remaining -= receipt.GetAmount();
                if (remaining <= 0)
                {
                    if (remaining < 0) storage.GenerateResource(food.type, -remaining);
                    return true;
                }
            }
            foreach (var receipt in taken) storage.AddResourcePacket(receipt, false);
            return false;
        }

        internal static bool Deliver(Contract contract, ResourceData food)
        {
            var terms = contract.Terms;
            if (!Supported(food) || !contract.Deliver(terms.Coins == 0 ? Choices() : null,
                () => Spend(food, terms.Quantity), (int)food.type)) return false;
            // Observers can produce, reserve, spend or save synchronously. Publish after
            // the inventory and the contract agree, without using their net delta as a receipt.
            ResourceManager.Instance.ResourceAmountChanged(food.type);
            return true;
        }

        internal static bool ReturnPayment(Contract contract)
        {
            var food = PaidFood(contract);
            if (!contract.Refund(CanClaim, () => Refund(food))) return false;
            ResourceManager.Instance.ResourceAmountChanged(food.type);
            return true;
        }

        internal static bool Refund(ResourceData food)
        {
            if (!Supported(food)) return false;
            var building = CityManager.Instance.GetBuilding(food.buildingToTakeFrom);
            if (building != null) building.GetStorage().GenerateResource(food.type, Contract.Price);
            else ResourceManager.Instance.RegisterStartingResource(new ResourceAmount(food.type, Contract.Price));
            return true;
        }

        private static bool Eligible(string id)
        {
            if (string.IsNullOrEmpty(id) || !RewardPolicy.Allows(id)) return false;
            var relic = AddressablesManager.Instance.GetRelic(id);
            var manager = RelicManager.Instance;
            if (relic == null || relic.GetIdentifier() != id || !manager.IsUnlocked(id) ||
                relic.IsLaw() || relic.IsRacial() ||
                relic.GetRequiredKingdomLevel() > RaceDataManager.Instance.GetKingdomLevel()) return false;
            var race = relic.GetKingdom();
            if (!relic.IsGeneric() && race != Races.None && race != RaceManager.Instance.GetRace()) return false;
            string unit = relic.GetRelatedUnit();
            return (string.IsNullOrEmpty(unit) || UnitManager.Instance.GetUnit(unit) != null) &&
                manager.CanAddRelic(id);
        }

        private static HashSet<string> Upgrades() => AddressablesManager.Instance.GetAllRelics().Values
            .Where(r => r != null && r.GetUpgrade() != null).Select(r => r.GetUpgrade().GetIdentifier()).ToHashSet();

        internal static Relic ClaimTarget(string id)
        {
            if (Upgrades().Contains(id) || !Eligible(id)) return null;
            var manager = RelicManager.Instance;
            if (!manager.HasRelic(id)) return AddressablesManager.Instance.GetRelic(id);
            var owned = manager.GetRelic(id);
            var upgrade = owned != null ? owned.GetUpgrade() : null;
            if (upgrade == null) return null;
            var upgradeId = upgrade.GetIdentifier();
            return Eligible(upgradeId) && !manager.HasRelic(upgradeId) &&
                AddressablesManager.Instance.GetRelic(upgradeId) == upgrade ? upgrade : null;
        }

        internal static bool CanClaim(string id) => ClaimTarget(id) != null;

        internal static string[] Choices()
        {
            var upgrades = Upgrades();
            var candidates = AddressablesManager.Instance.GetAllRelics().Keys.Where(id =>
                !upgrades.Contains(id) && Eligible(id) && !RelicManager.Instance.HasRelic(id)).ToList();
            var random = new Random();
            for (int i = 0; i < Math.Min(3, candidates.Count); ++i)
            {
                int j = random.Next(i, candidates.Count);
                var picked = candidates[j];
                candidates[j] = candidates[i];
                candidates[i] = picked;
            }
            return candidates.Take(3).ToArray();
        }

        internal static bool CollectRelic(Contract contract, string id, Relic target, Action<Exception> warn)
        {
            if (target == null || ClaimTarget(id) != target) return false;
            var manager = RelicManager.Instance;
            var targetId = target.GetIdentifier();
            return contract.Collect(id, target.name, () => manager.AddRelic(id), () => manager.HasRelic(targetId), warn);
        }

        internal static bool CollectCoins(Contract contract, Action<Exception> warn)
        {
            var resources = ResourceManager.Instance;
            // AddResource normally updates stats, credits the purse, then notifies listeners.
            // Isolate the actual credit so a listener spending coins cannot erase its receipt.
            if (!contract.CollectCoins(resources.GetCoins, resources.AddToCoins, warn)) return false;
            try { StatisticsManager.Instance.AddCoins(contract.Terms.Coins); }
            catch (Exception error) { warn(error); }
            try { resources.ResourceAmountChanged(ResourceType.Coins); }
            catch (Exception error) { warn(error); }
            return true;
        }
    }
}
