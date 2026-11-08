using System;
using UnityEngine;

namespace VoidProspector.Prototype
{
    /// <summary>Game state: credits, cargo, upgrade levels. Everything the UI shows comes from here.</summary>
    public class ProtoGame : MonoBehaviour
    {
        public static ProtoGame I { get; private set; }

        public event Action Changed;

        public double Credits { get; private set; }
        public int CargoCount { get; private set; }

        int[,] _cargo;   // [mineral, grade]
        readonly int[] _levels = new int[4];

        static ProtoConfig C => ProtoBootstrap.I.config;

        void Awake()
        {
            I = this;
            _cargo = new int[C.minerals.Length, C.grades.Length];
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        // ---------- Derived stats ----------

        public int Level(UpgradeId id) => _levels[(int)id];
        public bool IsMaxed(UpgradeId id) => Level(id) >= C.Upgrade(id).maxLevel;
        public double Cost(UpgradeId id) => C.Upgrade(id).CostAt(Level(id));
        public bool CanBuy(UpgradeId id) => !IsMaxed(id) && Credits >= Cost(id);

        public float DrillDamage => C.tapDamageBase + C.tapDamagePerLevel * Level(UpgradeId.Drill);
        public float CollectorRadius => C.collectorRadiusBase + C.collectorRadiusPerLevel * Level(UpgradeId.Collector);
        public int DroneCount => Level(UpgradeId.Drone);

        /// <summary>Seconds between auto-fire shots; 0 when auto-fire isn't bought.</summary>
        public float AutoFireInterval
        {
            get
            {
                int lvl = Level(UpgradeId.AutoFire);
                return lvl == 0 ? 0f : C.autoFireIntervalBase * Mathf.Pow(C.autoFireIntervalPerLevel, lvl - 1);
            }
        }

        public bool CargoFull => CargoCount >= C.cargoCapacity;

        public int CargoOf(int mineral, int grade) => _cargo[mineral, grade];

        public int CargoOfMineral(int mineral)
        {
            int n = 0;
            for (int g = 0; g < C.grades.Length; g++) n += _cargo[mineral, g];
            return n;
        }

        public double CargoValue
        {
            get
            {
                double v = 0;
                for (int m = 0; m < C.minerals.Length; m++)
                for (int g = 0; g < C.grades.Length; g++)
                    v += _cargo[m, g] * C.minerals[m].value * C.grades[g].multiplier;
                return v;
            }
        }

        // ---------- Actions ----------

        public bool TryBuy(UpgradeId id)
        {
            if (!CanBuy(id)) return false;
            Credits -= Cost(id);
            _levels[(int)id]++;
            Changed?.Invoke();
            return true;
        }

        public bool AddCargo(int mineral, int grade)
        {
            if (CargoFull) return false;
            _cargo[mineral, grade]++;
            CargoCount++;
            Changed?.Invoke();
            return true;
        }

        public double Sell()
        {
            double v = CargoValue;
            Credits += v;
            Array.Clear(_cargo, 0, _cargo.Length);
            CargoCount = 0;
            Changed?.Invoke();
            return v;
        }

        public void AddCredits(double amount)
        {
            Credits += amount;
            Changed?.Invoke();
        }

        /// <summary>
        /// Each chunk of the mineral below the top grade: chance to go up one grade (lower at higher grades),
        /// small chance to be lost. Returns (upgraded, lost).
        /// </summary>
        public (int up, int lost) Refine(int mineral)
        {
            int up = 0, lost = 0;
            int top = C.grades.Length - 1;
            // Go from high to low so a chunk is promoted at most once per refine.
            for (int g = top - 1; g >= 0; g--)
            {
                int n = _cargo[mineral, g];
                float upChance = C.refineUpChance * Mathf.Pow(C.refineFalloff, g);
                for (int i = 0; i < n; i++)
                {
                    float r = UnityEngine.Random.value;
                    if (r < C.refineLossChance)
                    {
                        _cargo[mineral, g]--;
                        CargoCount--;
                        lost++;
                    }
                    else if (r < C.refineLossChance + upChance)
                    {
                        _cargo[mineral, g]--;
                        _cargo[mineral, g + 1]++;
                        up++;
                    }
                }
            }
            Changed?.Invoke();
            return (up, lost);
        }

        public int RefinableCount(int mineral)
        {
            int n = 0;
            for (int g = 0; g < C.grades.Length - 1; g++) n += _cargo[mineral, g];
            return n;
        }

        // ---------- Rolls ----------

        public static int RollMineral()
        {
            var ms = C.minerals;
            float total = 0;
            foreach (var m in ms) total += m.weight;
            float r = UnityEngine.Random.value * total;
            for (int i = 0; i < ms.Length; i++)
            {
                r -= ms[i].weight;
                if (r <= 0) return i;
            }
            return 0;
        }

        public static int RollGrade(int bonus = 0)
        {
            var gs = C.grades;
            float total = 0;
            foreach (var g in gs) total += g.chance;
            float r = UnityEngine.Random.value * total;
            int pick = 0;
            for (int i = 0; i < gs.Length; i++)
            {
                r -= gs[i].chance;
                if (r <= 0) { pick = i; break; }
            }
            return Mathf.Min(pick + bonus, gs.Length - 1);
        }

        // ---------- Formatting ----------

        static readonly string[] Suffixes = { "K", "M", "B", "T", "Qa", "Qi" };

        public static string Fmt(double v)
        {
            if (v < 1000)
                return v < 10 && Math.Abs(v % 1) > 0.001 ? v.ToString("0.#") : Math.Floor(v).ToString("0");
            int i = -1;
            while (v >= 1000 && i < Suffixes.Length - 1) { v /= 1000; i++; }
            return v.ToString(v < 10 ? "0.00" : v < 100 ? "0.0" : "0") + Suffixes[i];
        }
    }
}
