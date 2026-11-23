using System;
using System.Linq;

namespace WeeklyContracts
{
    internal enum Stage { None, Offered, Active, Earned, Declined, Expired, Claimed, Refunded }

    [Serializable]
    internal sealed class Contract
    {
        internal const int Price = 6;
        public string run;
        public int offeredDay;
        public int deadline;
        public Stage stage;
        public string[] rewards;
        public string claimed;

        internal bool AtDawn(int day)
        {
            Advance(day, true, false);
            if (day < 1 || day > 28 || day <= deadline || stage == Stage.Earned)
                return false;
            offeredDay = day;
            deadline = ((day + 6) / 7) * 7;
            stage = Stage.Offered;
            rewards = null;
            claimed = null;
            return true;
        }

        internal void Advance(int day, bool morning, bool night)
        {
            if (stage == Stage.Offered && (day != offeredDay || !morning))
                stage = Stage.Expired;
            if (stage == Stage.Active && (day > deadline || (day == deadline && night)))
                stage = Stage.Expired;
        }

        internal void Decide(bool accept)
        {
            if (stage == Stage.Offered)
                stage = accept ? Stage.Active : Stage.Declined;
        }

        internal bool Deliver(string[] choices, Func<bool> spend)
        {
            if (stage != Stage.Active || choices == null || choices.Length != 3 ||
                choices.Any(string.IsNullOrEmpty) || choices.Distinct().Count() != 3)
                return false;
            rewards = choices;
            stage = Stage.Earned;
            // Native resource/relic notifications can synchronously invoke other mods.
            if (spend()) return true;
            rewards = null;
            stage = Stage.Active;
            return false;
        }

        internal bool Collect(string id, Func<string, bool> grant)
        {
            if (stage != Stage.Earned || !rewards.Contains(id)) return false;
            claimed = id;
            stage = Stage.Claimed;
            if (grant(id)) return true;
            stage = Stage.Earned;
            claimed = null;
            return false;
        }
    }
}
