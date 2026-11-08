using System;
using System.Collections.Generic;
using VoidProspector.Data;

namespace VoidProspector.Economy
{
    /// <summary>All upgrade tracks of a session, in database order. Buying spends credits from the wallet.</summary>
    public sealed class Upgrades
    {
        /// <summary>Raised after a purchase with the track and the number of levels bought.</summary>
        public event Action<UpgradeTrack, int> Bought;

        readonly List<UpgradeTrack> _tracks = new List<UpgradeTrack>();
        readonly Dictionary<UpgradeEffect, UpgradeTrack> _byEffect = new Dictionary<UpgradeEffect, UpgradeTrack>();
        readonly Wallet _wallet;

        public Upgrades(IEnumerable<UpgradeDefinition> definitions, Wallet wallet)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            foreach (var def in definitions)
            {
                if (def == null || _byEffect.ContainsKey(def.effect)) continue;
                var track = new UpgradeTrack(def);
                _tracks.Add(track);
                _byEffect[def.effect] = track;
            }
        }

        public IReadOnlyList<UpgradeTrack> All => _tracks;

        public UpgradeTrack Get(UpgradeEffect effect) => _byEffect.TryGetValue(effect, out var t) ? t : null;

        /// <summary>The effect's current value, or <paramref name="fallback"/> when no upgrade has that effect.</summary>
        public double Value(UpgradeEffect effect, double fallback)
        {
            var t = Get(effect);
            return t != null ? t.Value : fallback;
        }

        public UpgradeQuote Quote(UpgradeTrack track, BuyMode mode) => track.Quote(mode, _wallet.Credits);

        /// <summary>Buys per the mode's quote. Returns the number of levels bought (0 if unaffordable or maxed).</summary>
        public int TryBuy(UpgradeTrack track, BuyMode mode)
        {
            if (track == null || !_byEffect.ContainsValue(track)) throw new ArgumentException("Unknown track", nameof(track));
            if (track.Definition.costMineral != null) return 0;   // mineral costs arrive with Sector 2

            var quote = Quote(track, mode);
            if (!quote.affordable || quote.count == 0) return 0;
            if (!_wallet.TrySpend(quote.cost)) return 0;
            track.AddLevels(quote.count);
            Bought?.Invoke(track, quote.count);
            return quote.count;
        }

        /// <summary>Sets a level without paying (debug tools and, later, loading a save).</summary>
        public void SetLevel(UpgradeEffect effect, int level)
        {
            var t = Get(effect);
            if (t == null) return;
            t.SetLevel(level);
            Bought?.Invoke(t, 0);
        }
    }
}
