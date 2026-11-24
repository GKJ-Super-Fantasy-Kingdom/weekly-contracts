using SuperFantasyKingdom;

namespace WeeklyContracts
{
    // These numbers are stored in checkpoints. Zero also covers pre-picker bread contracts.
    internal enum OrderKind { Bread = 0, Timber = 1, Stone = 2, Boards = 3, Grain = 4, Fish = 5, Water = 6 }

    internal sealed class OrderTerms
    {
        internal readonly OrderKind Kind;
        internal readonly string Title;
        internal readonly ResourceType Slot;
        internal readonly int Quantity;
        internal readonly int Coins;

        private OrderTerms(OrderKind kind, string title, ResourceType slot, int quantity, int coins)
        {
            Kind = kind;
            Title = title;
            Slot = slot;
            Quantity = quantity;
            Coins = coins;
        }

        private static readonly OrderTerms[] Catalog =
        {
            new OrderTerms(OrderKind.Bread, "Bread for the Watch", ResourceType.Bread, 6, 0),
            new OrderTerms(OrderKind.Timber, "Firewood for Camp", ResourceType.Wood, 3, 2),
            new OrderTerms(OrderKind.Stone, "Mend the Wall", ResourceType.Stone, 4, 3),
            new OrderTerms(OrderKind.Boards, "Repair the Bridge", ResourceType.Board, 5, 5),
            new OrderTerms(OrderKind.Grain, "Stock the Granary", ResourceType.Wheat, 4, 3),
            new OrderTerms(OrderKind.Fish, "Feed the Scouts", ResourceType.Fish, 3, 2),
            new OrderTerms(OrderKind.Water, "Fill the Canteens", ResourceType.Water, 2, 1)
        };

        internal static OrderTerms Get(OrderKind kind) => Catalog[(int)kind];
        internal string Reward => Coins == 0 ? "Choose 1 of 3 run relics" : Coins == 1 ? "1 coin" : $"{Coins} coins";
        internal string Difficulty => Quantity <= 3 ? "Easy" : Quantity == 4 ? "Standard" : "Hard";
    }
}
