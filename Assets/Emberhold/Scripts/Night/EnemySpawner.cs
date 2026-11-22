using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Sends a wave each night from random, unannounced directions (the forest sides only: never over the mountain, never from the sea).
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
            int flyers = night >= 2 ? 1 + night / 2 : 0;     // pterosaurs fly over walls: defend inside too
            for (int i = 0; i < flyers; i++) pending.Enqueue(EnemyKind.Flyer);
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
        /// A point on the open land edges of the map: the west forest or the east forest.
        /// Never on the mountain behind the castle (a natural wall) and never from the sea (a safe edge).
        /// Nearby angles give nearby points, so a group arrives together. Enemies start outside the
        /// city and walk in, so the player sees them coming.
        /// </summary>
        public static Vector3 SpawnPoint(float angleDeg, float jitter)
        {
            float t = Mathf.Repeat(angleDeg, 360f) / 360f;      // 0..0.5 west forest, 0.5..1 east forest
            const float zTop = 12f, zBottom = -14f, edge = 33f;
            bool west = t < 0.5f;
            float u = (west ? t : t - 0.5f) * 2f;
            // the group spreads along its own side, clamped at the ends, so it never splits across the map
            float z = Mathf.Clamp(Mathf.Lerp(zTop, zBottom, u) + jitter, zBottom, zTop);
            var p = new Vector3(west ? -edge : edge, 0f, z);
            p.y = WorldBuilder.HeightAt(p);
            return p;
        }
    }
}
