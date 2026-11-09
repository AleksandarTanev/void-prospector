using System;
using System.Collections.Generic;

namespace VoidProspector.Drones
{
    /// <summary>
    /// Fire timers for the drones. Tick-based and pure, so the simulator (and offline earnings) run drones exactly
    /// like the game. A drone that's ready but has no target stays ready until it fires.
    /// </summary>
    public sealed class DroneBay
    {
        readonly List<double> _timers = new List<double>();

        public int Count => _timers.Count;

        /// <summary>Adds or removes drones to match <paramref name="count"/>. New drones are staggered so they don't fire together.</summary>
        public void Sync(int count, double interval)
        {
            count = Math.Max(0, count);
            while (_timers.Count > count) _timers.RemoveAt(_timers.Count - 1);
            while (_timers.Count < count) _timers.Add(interval * (_timers.Count + 1) / (count + 1));
        }

        /// <summary>Advances the timers and lists the drones ready to fire in <paramref name="ready"/> (cleared first).</summary>
        public void Tick(double dt, List<int> ready)
        {
            ready.Clear();
            for (int i = 0; i < _timers.Count; i++)
            {
                _timers[i] -= dt;
                if (_timers[i] <= 0) ready.Add(i);
            }
        }

        /// <summary>
        /// Call after drone <paramref name="index"/> fired. Time spent waiting for a target isn't made up with
        /// extra shots: the next shot is at least half an interval away.
        /// </summary>
        public void Fired(int index, double interval)
        {
            if (index < 0 || index >= _timers.Count) return;
            _timers[index] = Math.Max(_timers[index] + interval, interval * 0.5);
        }

        public bool IsReady(int index) => index >= 0 && index < _timers.Count && _timers[index] <= 0;
    }
}
