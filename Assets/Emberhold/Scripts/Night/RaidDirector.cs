using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>One attacker's raid: who sends it and which troops.</summary>
    public class RaidPlan
    {
        public string attackerName;
        public List<EnemyKind> troops = new List<EnemyKind>();
    }

    /// <summary>
    /// Where raids come from. Today: computer-controlled rival lords.
    /// Later: real friends attacking online (a server-backed source implementing the same interface).
    /// </summary>
    public interface IRaidSource
    {
        /// <summary>Called by the director to ask for the next chain of raids, or null if nobody attacks now.</summary>
        List<RaidPlan> NextRaids(int nightsSurvived);
    }

    public class AiRaidSource : IRaidSource
    {
        static readonly string[] Lords = { "A raptor pack", "The Red Claw pack", "A hungry herd", "The Swamp Hunters", "A wild T-rex clan", "The Ridge Stalkers", "The River Biters", "A stampede" };

        public List<RaidPlan> NextRaids(int nightsSurvived)
        {
            // how many attackers come one after another (up to 6 when the city is strong)
            int maxAttackers = Mathf.Clamp(1 + nightsSurvived / 2, 1, 6);
            int count = Random.Range(1, maxAttackers + 1);
            var list = new List<RaidPlan>();
            var used = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                int li;
                do { li = Random.Range(0, Lords.Length); } while (used.Contains(li) && used.Count < Lords.Length);
                used.Add(li);
                var plan = new RaidPlan { attackerName = Lords[li] };
                int raiders = 2 + nightsSurvived / 2 + Random.Range(0, 3);
                int brutes = nightsSurvived >= 3 ? Random.Range(0, 1 + nightsSurvived / 3) : 0;
                for (int r = 0; r < raiders; r++) plan.troops.Add(EnemyKind.Raider);
                for (int b = 0; b < brutes; b++) plan.troops.Add(EnemyKind.Brute);
                int flyers = nightsSurvived >= 2 ? Random.Range(0, 2 + nightsSurvived / 3) : 0;
                for (int f = 0; f < flyers; f++) plan.troops.Add(EnemyKind.Flyer);
                list.Add(plan);
            }
            return list;
        }
    }

    /// <summary>
    /// Raids at any time of day: several attackers can strike one after another.
    /// Directions are never shown; the player only hears the horn.
    /// </summary>
    public class RaidDirector : MonoBehaviour
    {
        public IRaidSource source = new AiRaidSource();

        readonly Queue<RaidPlan> chain = new Queue<RaidPlan>();
        float nextRaidCheck;
        float nextInChain;
        GameManager gm;

        public bool RaidInProgress { get { return chain.Count > 0 || (gm != null && !gm.Cycle.IsNight && Enemy.All.Count > 0); } }

        public void Init(GameManager game)
        {
            gm = game;
            nextRaidCheck = Time.time + Random.Range(70f, 120f);
        }

        /// <summary>Stops the raid chain (the HQ fell or the attack timed out).</summary>
        public void CancelAll()
        {
            chain.Clear();
            attackStarted = -1f;
        }

        /// <summary>Start a raid right now (also used by the "Test raid" menu button).</summary>
        public void TriggerNow()
        {
            foreach (var p in source.NextRaids(Mathf.Max(2, gm.State.night))) chain.Enqueue(p);
            nextInChain = Time.time;
        }

        /// <summary>An attack lasts at most this long, then the dinosaurs give up and run (short sessions).</summary>
        public const float AttackSeconds = 150f;
        float attackStarted = -1f;

        void Update()
        {
            if (gm == null) return;

            if (Enemy.All.Count > 0 || chain.Count > 0)
            {
                if (attackStarted < 0f) attackStarted = Time.time;
                if (Time.time - attackStarted > AttackSeconds)
                {
                    chain.Clear();
                    int ran = Enemy.FleeAll();       // first, so EndWave does not just delete them
                    gm.Spawner.EndWave();
                    if (ran > 0) gm.Toast("The dinosaurs ran back into the jungle!");
                    if (gm.Cycle.Phase == Phase.Night) gm.Cycle.EndNight();
                    attackStarted = -1f;
                }
            }
            else attackStarted = -1f;

            if (chain.Count > 0 && Time.time >= nextInChain)
            {
                var plan = chain.Dequeue();
                Launch(plan, chain.Count);
                nextInChain = Time.time + Random.Range(18f, 35f);
            }

            // Daytime raids start after the first night; at night the regular wave comes instead.
            if (Time.time >= nextRaidCheck)
            {
                nextRaidCheck = Time.time + Random.Range(110f, 220f);
                if (gm.State.night >= 1 && gm.Cycle.Phase == Phase.Day && chain.Count == 0 && Random.value < 0.7f)
                {
                    var plans = source.NextRaids(gm.State.night);
                    if (plans != null) foreach (var p in plans) chain.Enqueue(p);
                    nextInChain = Time.time;
                }
            }
        }

        void Launch(RaidPlan plan, int stillComing)
        {
            AudioManager.Play("horn");
            gm.Toast(plan.attackerName + " is attacking!" + (stillComing > 0 ? "  (" + stillComing + " more coming)" : ""));
            gm.Spawner.SpawnGroup(plan.troops, Random.Range(0f, 360f), Mathf.Max(1, gm.State.night));
        }
    }
}
