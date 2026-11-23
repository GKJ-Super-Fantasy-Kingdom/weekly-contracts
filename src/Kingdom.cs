using System;
using System.Linq;
using SuperFantasyKingdom;
using SuperFantasyKingdom.Buildings;

namespace WeeklyContracts
{
    internal static class Kingdom
    {
        private const ResourceType Bread = ResourceType.Bread;

        internal static bool SupportsBread =>
            RaceManager.Instance.GetBuildingTypeToTakeFrom(Bread) != BuildingType.None;

        internal static int Stock()
        {
            var building = CityManager.Instance.GetBuilding(RaceManager.Instance.GetBuildingTypeToTakeFrom(Bread));
            if (building == null) return ResourceManager.Instance.GetResourceAvailableAmount(Bread);
            // SpendResource takes free packets. The displayed resource total can include reserved stock.
            return building.GetStorage().GetAllResourcePackets()
                .Count(p => p != null && p.GetResourceType() == Bread && !p.IsReserved());
        }

        internal static bool Spend()
        {
            int before = Stock();
            if (before < Contract.Price) return false;
            // The native starting-stock branch deducts the amount but returns false.
            ResourceManager.Instance.SpendResource(Bread, Contract.Price);
            return Stock() == before - Contract.Price;
        }

        internal static string[] Choices()
        {
            var relics = RelicManager.Instance;
            var assets = AddressablesManager.Instance;
            var race = RaceManager.Instance.GetRace();
            int level = RaceDataManager.Instance.GetKingdomLevel();
            var upgrades = assets.GetAllRelics().Values.Where(r => r != null && r.GetUpgrade() != null)
                .Select(r => r.GetUpgrade().GetIdentifier()).ToHashSet();
            var candidates = relics.GetUnlockedRelics().Distinct().Where(id =>
            {
                var relic = assets.GetRelic(id);
                return relic != null && !upgrades.Contains(id) && !relic.IsLaw() && !relic.IsRacial() &&
                    (relic.IsGeneric() || relic.GetKingdom() == race || relic.GetKingdom() == Races.None) &&
                    relic.GetRequiredKingdomLevel() <= level && !relics.HasRelic(id) && relics.CanAddRelic(id);
            }).ToList();
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

        internal static bool Grant(string id) =>
            RelicManager.Instance.CanAddRelic(id) && RelicManager.Instance.AddRelic(id) != null;
    }
}
