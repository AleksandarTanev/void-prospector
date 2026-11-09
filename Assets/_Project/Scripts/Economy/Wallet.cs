using System;
using R3;
using VoidProspector.Core;

namespace VoidProspector.Economy
{
    /// <summary>The player's credits.</summary>
    public sealed class Wallet
    {
        readonly ReactiveProperty<GameNumber> _credits;

        public Wallet(GameNumber startingCredits = default) => _credits = new ReactiveProperty<GameNumber>(startingCredits);

        public GameNumber Credits => _credits.Value;
        /// <summary>Credits for UI binding; emits the current value on subscribe, then every change.</summary>
        public ReadOnlyReactiveProperty<GameNumber> CreditsRx => _credits;

        public bool CanAfford(GameNumber amount) => Credits >= amount;

        public void Add(GameNumber amount)
        {
            if (amount < GameNumber.Zero) throw new ArgumentOutOfRangeException(nameof(amount), "Use TrySpend to remove credits");
            if (amount == GameNumber.Zero) return;
            _credits.Value = Credits + amount;
        }

        public bool TrySpend(GameNumber amount)
        {
            if (amount < GameNumber.Zero) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!CanAfford(amount)) return false;
            _credits.Value = Credits - amount;
            return true;
        }

        /// <summary>Sets the credits directly when loading a save.</summary>
        public void Restore(GameNumber credits) => _credits.Value = credits < GameNumber.Zero ? GameNumber.Zero : credits;
    }
}
