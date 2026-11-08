using System;
using VoidProspector.Core;

namespace VoidProspector.Economy
{
    /// <summary>The player's credits.</summary>
    public sealed class Wallet
    {
        public event Action Changed;

        public GameNumber Credits { get; private set; }

        public Wallet(GameNumber startingCredits = default) => Credits = startingCredits;

        public bool CanAfford(GameNumber amount) => Credits >= amount;

        public void Add(GameNumber amount)
        {
            if (amount < GameNumber.Zero) throw new ArgumentOutOfRangeException(nameof(amount), "Use TrySpend to remove credits");
            if (amount == GameNumber.Zero) return;
            Credits += amount;
            Changed?.Invoke();
        }

        public bool TrySpend(GameNumber amount)
        {
            if (amount < GameNumber.Zero) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!CanAfford(amount)) return false;
            Credits -= amount;
            Changed?.Invoke();
            return true;
        }
    }
}
