using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using VoidProspector.Core;
using VoidProspector.Data;
using VoidProspector.Mining;

namespace VoidProspector.Economy
{
    /// <summary>
    /// Plays the game without visuals, using the real <see cref="GameSession"/> and data, to check pacing.
    /// Player model: taps one meteor at a steady rate, auto-fire helps once bought, chunks arrive after a delay,
    /// sells when the hold is full or when selling pays for the next upgrade, always buys the cheapest upgrade,
    /// and always mines at the deepest unlocked depth.
    /// </summary>
    public static class EconomySimulator
    {
        public sealed class Settings
        {
            public int seed = 1;
            public double duration = 1800;
            public double tapsPerSecond = 3;
            public double step = 0.05;
            /// <summary>Seconds from spill to the cargo hold.</summary>
            public double collectDelay = 1.5;
            /// <summary>Seconds to pick a new target after a meteor breaks.</summary>
            public double retargetDelay = 0.4;
            public bool pushDepth = true;
        }

        public struct Purchase
        {
            public double time;
            public string upgrade;
            public int level;
            public GameNumber cost;
            public int depth;
        }

        public sealed class Result
        {
            public readonly List<Purchase> purchases = new List<Purchase>();
            public GameNumber earned;
            public int meteorsBroken;
            public int deepest;

            /// <summary>Seconds between consecutive purchases made in [from, to), counting from the start for the first.</summary>
            public List<double> Gaps(double from, double to)
            {
                var gaps = new List<double>();
                double last = 0;
                foreach (var p in purchases)
                {
                    if (p.time >= to) break;
                    if (p.time >= from) gaps.Add(p.time - last);
                    last = p.time;
                }
                return gaps;
            }

            public double MedianGap(double from, double to)
            {
                var g = Gaps(from, to).OrderBy(x => x).ToList();
                if (g.Count == 0) return double.PositiveInfinity;
                return g.Count % 2 == 1 ? g[g.Count / 2] : (g[g.Count / 2 - 1] + g[g.Count / 2]) / 2;
            }

            public string ToCsv()
            {
                var sb = new StringBuilder("time_s,upgrade,level,cost,depth\n");
                foreach (var p in purchases)
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:0.0},{1},{2},{3},{4}",
                        p.time, p.upgrade, p.level, p.cost.Value, p.depth + 1));
                return sb.ToString();
            }
        }

        public static Result Run(GameDatabase db, SectorDefinition sector, Settings s)
        {
            var session = new GameSession(db, sector, s.seed);
            var mining = db.mining;
            var result = new Result();
            var pending = new List<(double time, ChunkData chunk)>();

            MeteorSpawn spawn = null;
            MeteorState meteor = null;
            int nextChunk = 0;
            void NewMeteor()
            {
                spawn = session.RollMeteor();
                meteor = new MeteorState(spawn.hp, spawn.minerals.Length, mining.crackThresholds, mining.crackSpillFraction);
                nextChunk = 0;
            }
            NewMeteor();

            double t = 0, tapTimer = 0, fireTimer = 0, retarget = 0;

            void Spill(int count, int core)
            {
                for (int i = 0; i < count && nextChunk < spawn.minerals.Length; i++)
                {
                    var chunk = session.RollChunk(spawn.minerals[nextChunk++], spawn.depth, i >= count - core);
                    pending.Add((t + s.collectDelay, chunk));
                }
            }

            void Hit(bool tap)
            {
                if (retarget > 0) return;
                var roll = session.RollHit(tap);
                var r = meteor.Hit(roll.damage);
                Spill(r.crackChunks, 0);
                if (!r.broke) return;
                Spill(r.breakChunks, spawn.size.hasCore ? mining.coreChunks : 0);
                session.RegisterBreak(spawn.depth);
                result.meteorsBroken++;
                NewMeteor();
                retarget = s.retargetDelay;
            }

            while (t < s.duration)
            {
                t += s.step;
                if (s.pushDepth && session.Depths.Deepest > session.Depth) session.Depths.TrySetDepth(session.Depths.Deepest);

                if (retarget > 0) retarget -= s.step;
                tapTimer -= s.step;
                while (tapTimer <= 0) { tapTimer += 1 / s.tapsPerSecond; Hit(true); }
                double interval = session.AutoFireInterval;
                if (interval > 0)
                {
                    fireTimer -= s.step;
                    while (fireTimer <= 0) { fireTimer += interval; Hit(false); }
                }

                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    if (pending[i].time > t) continue;
                    var chunk = pending[i].chunk;
                    pending.RemoveAt(i);
                    result.earned += chunk.value;
                    if (session.TryCollect(chunk) == CollectResult.CargoFull)
                    {
                        session.SellCargo();
                        session.TryCollect(chunk);
                    }
                }

                BuyCheapest(session, result, t);
            }

            result.deepest = session.Depths.Deepest;
            return result;
        }

        static void BuyCheapest(GameSession session, Result result, double t)
        {
            while (true)
            {
                UpgradeTrack cheapest = null;
                foreach (var track in session.Upgrades.All)
                {
                    if (track.IsMaxed || track.Definition.costMineral != null) continue;
                    if (cheapest == null || track.NextCost < cheapest.NextCost) cheapest = track;
                }
                if (cheapest == null) return;

                var cost = cheapest.NextCost;
                if (session.Wallet.Credits + session.Cargo.Value < cost) return;
                if (session.Wallet.Credits < cost) session.SellCargo();
                if (session.Upgrades.TryBuy(cheapest, BuyMode.One) == 0) return;

                result.purchases.Add(new Purchase
                {
                    time = t,
                    upgrade = cheapest.Definition.id,
                    level = cheapest.Level,
                    cost = cost,
                    depth = session.Depth,
                });
            }
        }
    }
}
