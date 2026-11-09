using System;
using UnityEngine;

namespace VoidProspector.Data
{
    /// <summary>Balance rules for drones. Part of <see cref="GameDatabase"/>; count, damage and targeting are upgrades.</summary>
    [Serializable]
    public class DroneSettings
    {
        [Tooltip("Seconds between shots of one drone")]
        [Min(0.05f)] public float fireInterval = 0.8f;
    }
}
