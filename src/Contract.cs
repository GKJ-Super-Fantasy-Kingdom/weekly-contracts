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
        public int paidType;
        public OrderKind selected;
        public OrderKind[] offers;
        internal OrderTerms Terms => OrderTerms.Get(selected);
        [NonSerialized] private bool settling;
        [NonSerialized] private Func<int, string[], bool> checkpointReceipt;
        [NonSerialized] private string pendingChoice;

        internal string OfferMessage => $"Choose one this morning. Deliver before nightfall on day {deadline}.";

        // A save provider may run synchronously on either side of the inventory write.
        // Use the inventory in that checkpoint, without exposing a second live claim.
        internal Contract ForCheckpoint(int coins, string[] relics)
        {
            if (checkpointReceipt == null || !checkpointReceipt(coins, relics)) return this;
            var saved = (Contract)MemberwiseClone();
            saved.stage = Stage.Claimed;
            saved.claimed = pendingChoice;
            return saved;
        }

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
            paidType = 0;
            selected = OrderKind.Bread;
            offers = null;
            return true;
        }

        internal bool IsPastDeadline(int day, bool night) => day > deadline || (day == deadline && night);

        internal void Advance(int day, bool morning, bool night)
        {
            if (stage == Stage.Offered && (day != offeredDay || !morning))
                stage = Stage.Expired;
            if (stage == Stage.Active && IsPastDeadline(day, night))
                stage = Stage.Expired;
        }

        internal void Decide(bool accept)
        {
            if (stage == Stage.Offered)
                stage = accept ? Stage.Active : Stage.Declined;
        }

        internal bool Accept(OrderKind kind)
        {
            if (stage != Stage.Offered || offers == null || !offers.Contains(kind)) return false;
            selected = kind;
            stage = Stage.Active;
            return true;
        }

        internal bool Deliver(string[] choices, Func<bool> spend, int material = 0)
        {
            if (stage != Stage.Active || (Terms.Coins == 0 && (choices == null || choices.Length != 3 ||
                choices.Any(string.IsNullOrEmpty) || choices.Distinct().Count() != 3)))
                return false;
            if (settling) return false;
            settling = true;
            try
            {
                if (!spend()) return false;
                rewards = Terms.Coins == 0 ? choices : null;
                paidType = material;
                stage = Stage.Earned;
                return true;
            }
            finally { settling = false; }
        }

        internal bool Collect(string id, string savedName, Action grant, Func<bool> ownsTarget, Action<Exception> warn)
        {
            if (settling || stage != Stage.Earned || Terms.Coins != 0 || rewards == null ||
                !rewards.Contains(id) || ownsTarget()) return false;
            return SettleReward(id, grant, ownsTarget, (_, relics) => relics?.Contains(savedName) == true, warn);
        }

        internal bool CollectCoins(Func<int> balance, Action<int> credit, Action<Exception> warn)
        {
            if (settling || stage != Stage.Earned || Terms.Coins <= 0) return false;
            int amount = Terms.Coins;
            long expected = (long)balance() + amount;
            return SettleReward(null, () => credit(amount), () => balance() >= expected,
                (coins, _) => coins >= expected, warn);
        }

        private bool SettleReward(string choice, Action grant, Func<bool> received,
            Func<int, string[], bool> savedReceipt, Action<Exception> warn)
        {
            settling = true;
            pendingChoice = choice;
            checkpointReceipt = savedReceipt;
            try
            {
                try { grant(); }
                catch (Exception error) { warn(error); }
                if (!received()) return false;
                claimed = choice;
                stage = Stage.Claimed;
                return true;
            }
            finally
            {
                checkpointReceipt = null;
                pendingChoice = null;
                settling = false;
            }
        }

        internal bool Refund(Func<string, bool> available, Func<bool> restore)
        {
            if (settling || stage != Stage.Earned || Terms.Coins != 0 || (rewards != null && rewards.Any(available))) return false;
            settling = true;
            try
            {
                if (!restore()) return false;
                stage = Stage.Refunded;
                return true;
            }
            finally { settling = false; }
        }
    }
}
