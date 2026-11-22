using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Sends a wave each night from random, unannounced directions (forest sides and the shore, never over the mountain).
    /// The first 3 nights are gentle. Every 5th night has a boss.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        readonly Queue<EnemyKind> pending = new Queue<EnemyKind>();
        float nextSpawn;
        float groupAngle;
        int night;
        public bool WaveActive { get; private set; }

        public void BeginWave(int nightNumber)
        {
            night = nightNumber;
            pending.Clear();
            int raiders, brutes;
            if (night <= 1) { raiders = 3; brutes = 0; }
            else if (night == 2) { raiders = 5; brutes = 0; }
            else if (night == 3) { raiders = 6; brutes = 1; }
            else { raiders = 4 + night * 2; brutes = night / 2; }
            for (int i = 0; i < raiders; i++) pending.Enqueue(EnemyKind.Raider);
            for (int i = 0; i < brutes; i++) pending.Enqueue(EnemyKind.Brute);
            if (night % 5 == 0) pending.Enqueue(EnemyKind.Boss);
            groupAngle = Random.Range(0f, 360f);
            nextSpawn = Time.time + 3f;
            WaveActive = true;
        }

        public bool Finished { get { return WaveActive && pending.Count == 0 && Enemy.All.Count == 0; } }

        public void EndWave()
        {
            WaveActive = false;
            pending.Clear();
            foreach (var e in Enemy.All.ToArray()) Destroy(e.gameObject);
        }

        void Update()
        {
            if (!WaveActive || pending.Count == 0 || Time.time < nextSpawn) return;
            // Enemies come in small groups; each group picks a new hidden direction.
            if (Random.value < 0.3f) groupAngle = Random.Range(0f, 360f);
            float gentle = night <= 3 ? 2.2f : 1f;
            nextSpawn = Time.time + Random.Range(1.2f, 3f) * gentle;
            Enemy.Spawn(pending.Dequeue(), SpawnPoint(groupAngle, Random.Range(-5f, 5f)), night, transform);
        }

        /// <summary>Spawn a raid group from one hidden direction (used for daytime raids).</summary>
        public void SpawnGroup(List<EnemyKind> troops, float angleDeg, int strength)
        {
            StartCoroutine(SpawnSpread(troops, angleDeg, strength));
        }

        System.Collections.IEnumerator SpawnSpread(List<EnemyKind> troops, float angleDeg, int strength)
        {
            foreach (var k in troops)
            {
                Enemy.Spawn(k, SpawnPoint(angleDeg, Random.Range(-4f, 4f)), strength, transform);
                yield return new WaitForSeconds(Random.Range(0.5f, 1.4f));
            }
        }

        /// <summary>
        /// A point on the open edges of the map: the west forest, the southern shore or the east forest.
        /// Never on the mountain behind the castle - it is a natural wall.
        /// Nearby angles give nearby points, so a group arrives together. Enemies start outside the
        /// city and walk in, so the player sees them coming.
        /// </summary>
        public static Vector3 SpawnPoint(float angleDeg, float jitter)
        {
            float t = Mathf.Repeat(angleDeg, 360f) / 360f;      // 0..1 around a U-shaped path
            const float zTop = 10f, zBottom = -17f, edge = 33f;
            float side = zTop - zBottom, bottom = edge * 2f;
            float total = side * 2f + bottom;
            // the group spreads along the path itself, clamped at its ends, so it never splits across the map
            float d = Mathf.Clamp(t * total + jitter, 0f, total);
            Vector3 p;
            if (d < side) p = new Vector3(-edge, 0f, zTop - d);                                  // west forest
            else if (d < side + bottom) p = new Vector3(-edge + (d - side), 0f, zBottom - 1f);    // southern shore
            else p = new Vector3(edge, 0f, zBottom + (d - side - bottom));                       // east forest
            p.y = WorldBuilder.HeightAt(p);
            return p;
        }
    }
}
