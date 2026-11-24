using System;
using SuperFantasyKingdom;

namespace WeeklyContracts
{
    public static class CalendarApi
    {
        /// <summary>Reads the accepted, unpaid delivery for the loaded kingdom on the Unity main thread.</summary>
        public static bool TryGetActiveDelivery(out string title, out int quantity, out string resourceName, out int deadlineDay)
        {
            title = resourceName = null;
            quantity = deadlineDay = 0;
            var contract = Plugin.Current ?? Plugin.Restored;
            if (contract == null || contract.stage != Stage.Active) return false;

            var game = GameManager.Instance;
            var data = GameDataManager.Instance;
            var clock = DaytimeManager.Instance;
            if (game == null || !game.IsGameLoaded() || game.IsUnloading() || data == null ||
                clock == null || RaceManager.Instance == null || string.IsNullOrEmpty(contract.run) ||
                contract.run != data.GetRunID()) return false;

            int day = clock.CurrentDay();
            if (contract.offeredDay < 1 || day < contract.offeredDay || contract.IsPastDeadline(day, clock.IsNight()) ||
                !Enum.IsDefined(typeof(OrderKind), contract.selected)) return false;

            var terms = contract.Terms;
            var material = Kingdom.Material(contract.selected);
            title = terms.Title;
            quantity = terms.Quantity;
            resourceName = Kingdom.Supported(material) ? material.GetName() : terms.Slot.ToString();
            deadlineDay = contract.deadline;
            return true;
        }
    }
}
