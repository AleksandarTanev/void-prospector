using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using VoidProspector.Core;
using VoidProspector.Data;

namespace VoidProspector.Mining
{
    /// <summary>
    /// Owns all loose chunks: spawns them from a pool, moves them to the ship and hands them to the cargo hold.
    /// Chunks inside the collector radius fly to the ship; the rest drift slowly toward it, so nothing is lost
    /// unless the hold is full (a lesson from the prototype).
    /// </summary>
    public class ChunkField : MonoBehaviour
    {
        GameRoot _root;
        Transform _container;
        ObjectPool<Chunk> _pool;
        readonly List<Chunk> _active = new List<Chunk>();

        public int ActiveCount => _active.Count;

        GameSession Session => _root.Session;
        MiningSettings S => Session.Mining;

        public void Init(GameRoot root)
        {
            _root = root;
            _container = new GameObject("Chunks").transform;
            _pool = new ObjectPool<Chunk>(Create, null, c => c.gameObject.SetActive(false), c => Destroy(c.gameObject),
                collectionCheck: false, defaultCapacity: 64);
        }

        Chunk Create()
        {
            var go = new GameObject("Chunk");
            go.transform.SetParent(_container, false);
            var c = go.AddComponent<Chunk>();
            c.Build();
            return c;
        }

        public void Spawn(Vector2 pos, ChunkData data, Vector2 velocity)
        {
            var c = _pool.Get();
            c.Launch(pos, data, Session.Purity[data.grade], velocity);
            _active.Add(c);

            if (data.grade >= S.rareGrade) Celebrate(pos, data);
        }

        void Celebrate(Vector2 pos, ChunkData data)
        {
            var grade = Session.Purity[data.grade];
            _root.Fx.Toast($"{grade.name} {data.mineral.displayName}!", Color.Lerp(data.mineral.color, Color.white, 0.5f));
            _root.Fx.Burst(pos, Color.white, 20, 4f, 0.18f);
            _root.Audio.Rare(data.grade);
            if (data.grade == Session.Purity.TopGrade) _root.Fx.HitStop(S.topGradeHitStop);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector2 ship = _root.Ship.position;
            float radius = Session.CollectorRadius;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var c = _active[i];
                var state = c.Tick(dt, ship, radius, S);
                if (state == Chunk.TickResult.ReachedShip) TryCollect(i);
                else if (state == Chunk.TickResult.Faded) Release(i);
            }
        }

        void TryCollect(int index)
        {
            var c = _active[index];
            if (Session.TryCollect(c.Data))
            {
                _root.Audio.Pickup(c.Data.grade);
                _root.Fx.Burst(_root.Ship.position, c.Tint, 3, 1.5f, 0.1f);
                Release(index);
            }
            else
            {
                c.Reject();
                if (_root.Fx.CargoFull()) _root.Audio.Deny();
            }
        }

        void Release(int index)
        {
            var c = _active[index];
            _active.RemoveAt(index);
            _pool.Release(c);
        }
    }
}
