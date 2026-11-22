using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Sends a wave each night from random, unannounced directions (forest sides, mountain passes, the shore).
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
            Enemy.Spawn(pending.Dequeue(), SpawnPoint(groupAngle + Random.Range(-15f, 15f)), night, transform);
        }

        static Vector3 SpawnPoint(float angleDeg)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 30f;
            p.x = Mathf.Clamp(p.x, -GridMap.Half, GridMap.Half);
            p.z = Mathf.Clamp(p.z, GridMap.SeaZ + 1f, GridMap.MountainZ - 1f); // land from the shore / mountain foot
            return p;
        }
    }
}
