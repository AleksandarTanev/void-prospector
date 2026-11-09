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
    /// Player model: taps one meteor at a steady rate, auto-fire and drones help once bought, chunks arrive after a
    /// delay, sells when the hold is full or when selling pays for the next upgrade, always buys the cheapest upgrade,
    /// and always mines at the deepest unlocked depth. Once the jump drive is available it stops buying upgrades,
    /// saves for the jump, buys it and travels to the new sector.
    /// </summary>
    public static class EconomySimulator
    {
        public sealed class Settings
        {
            public int seed = 1;
            public double duration = 1800;
            /// <summary>0 = no tapping (idle: only auto-fire mines).</summary>
            public double tapsPerSecond = 3;
            public double step = 0.05;
            /// <summary>Seconds from spill to the cargo hold.</summary>
            public double collectDelay = 1.5;
            /// <summary>Seconds to pick a new target after a meteor breaks.</summary>
            public double retargetDelay = 0.4;
            public bool pushDepth = true;
            /// <summary>Buys upgrades and the jump drive. Off = nothing is bought (idle income estimate).</summary>
            public bool buyUpgrades = true;
            /// <summary>Saves for and buys the jump drive once it's available, then travels to the new sector.</summary>
            public bool buyJumpDrive = true;
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
            /// <summary>When each jump drive was bought, with the sector it unlocked.</summary>
            public readonly List<(double time, string sectorId)> jumps = new List<(double, string)>();

            public double FirstJumpTime => jumps.Count > 0 ? jumps[0].time : double.PositiveInfinity;

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

        public static Result Run(GameDatabase db, SectorDefinition sector, Settings s) =>
            Run(new GameSession(db, sector, s.seed), s);

        /// <summary>Plays on an existing session (e.g. one restored from a save). <see cref="Settings.seed"/> is not used.</summary>
        public static Result Run(GameSession session, Settings s)
        {
            var mining = session.Mining;
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
            var ready = new List<int>();

            void Spill(int count, int core)
            {
                for (int i = 0; i < count && nextChunk < spawn.minerals.Length; i++)
                {
                    var chunk = session.RollChunk(spawn.minerals[nextChunk++], spawn.depth, i >= count - core);
                    pending.Add((t + s.collectDelay, chunk));
                }
            }

            // Drones ignore the retarget delay: in the game they switch to another meteor at once.
            void Hit(HitRoll roll, bool drone = false)
            {
                if (retarget > 0 && !drone) return;
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
                if (s.tapsPerSecond > 0)
                {
                    tapTimer -= s.step;
                    while (tapTimer <= 0) { tapTimer += 1 / s.tapsPerSecond; Hit(session.RollHit(true)); }
                }
                double interval = session.AutoFireInterval;
                if (interval > 0)
                {
                    fireTimer -= s.step;
                    while (fireTimer <= 0) { fireTimer += interval; Hit(session.RollHit(false)); }
                }
                session.TickDrones(s.step, ready);
                foreach (int d in ready)
                {
                    Hit(session.RollDroneHit(), drone: true);
                    session.Drones.Fired(d, session.DroneInterval);
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

                if (s.buyUpgrades)
                {
                    if (s.buyJumpDrive && session.JumpState >= JumpState.Saving)
                    {
                        if (TryJump(session, result, t))
                        {
                            NewMeteor();
                            retarget = s.retargetDelay;
                        }
                    }
                    else BuyCheapest(session, result, t);
                }
            }

            result.deepest = session.Depths.Deepest;
            return result;
        }

        /// <summary>Buys the jump drive when the credits (after selling) and minerals are there, then travels.</summary>
        static bool TryJump(GameSession session, Result result, double t)
        {
            var next = session.NextSector;
            var credits = next.UnlockCredits;
            if (!session.Wallet.CanAfford(credits) && session.Wallet.Credits + session.SellableValue >= credits) session.SellCargo();
            if (!session.TryBuyJumpDrive()) return false;

            result.jumps.Add((t, next.id));
            result.purchases.Add(new Purchase { time = t, upgrade = "jump_drive", level = next.index, cost = credits, depth = session.Depth });
            session.TravelTo(next);
            return true;
        }

        static void BuyCheapest(GameSession session, Result result, double t)
        {
            while (true)
            {
                UpgradeTrack cheapest = null;
                foreach (var track in session.Upgrades.All)
                {
                    if (track.IsMaxed || track.Definition.costMineral != null || !session.IsAvailable(track)) continue;
                    if (cheapest == null || track.NextCost < cheapest.NextCost) cheapest = track;
                }
                if (cheapest == null) return;

                var cost = cheapest.NextCost;
                if (session.Wallet.Credits + session.SellableValue < cost) return;
                if (session.Wallet.Credits < cost) session.SellCargo();
                if (session.TryBuy(cheapest, BuyMode.One) == 0) return;

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
