using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Emberhold.Online
{
    /// <summary>
    /// Small text replay of a raid, so the defender can watch what happened.
    /// It stores what the PLAYER did (where and when each unit was dropped, Commander strikes)
    /// plus when each building fell. Playback: load the base, re-run the battle from the same seed
    /// with the same drops, and force each building down at its recorded time so the result always
    /// matches even if the simulation is not perfectly deterministic.
    ///
    /// Format: v1|seed|D:t,unit,x,z;...|S:t,x,z;...|X:t,uid;...   (t = tenths of a second)
    /// </summary>
    public class ReplayRecorder
    {
        public struct Drop { public float time; public string unitId; public float x, z; }
        public struct Strike { public float time; public float x, z; }
        public struct Fall { public float time; public string uid; }

        public int Seed;
        public readonly List<Drop> Drops = new List<Drop>();
        public readonly List<Strike> Strikes = new List<Strike>();
        public readonly List<Fall> Falls = new List<Fall>();

        public const int MaxChars = 16000;

        public ReplayRecorder(int seed) { Seed = seed; }

        public void RecordDrop(float time, string unitId, float x, float z) { Drops.Add(new Drop { time = time, unitId = unitId, x = x, z = z }); }
        public void RecordStrike(float time, float x, float z) { Strikes.Add(new Strike { time = time, x = x, z = z }); }
        public void RecordFall(float time, string uid) { Falls.Add(new Fall { time = time, uid = uid }); }

        static string T(float t) { return Math.Max(0, (int)Math.Round(t * 10)).ToString(CultureInfo.InvariantCulture); }
        static string P(float v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
        static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append("v1|").Append(Seed).Append("|D:");
            for (int i = 0; i < Drops.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(T(Drops[i].time)).Append(',').Append(Drops[i].unitId).Append(',').Append(P(Drops[i].x)).Append(',').Append(P(Drops[i].z));
            }
            sb.Append("|S:");
            for (int i = 0; i < Strikes.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(T(Strikes[i].time)).Append(',').Append(P(Strikes[i].x)).Append(',').Append(P(Strikes[i].z));
            }
            sb.Append("|X:");
            for (int i = 0; i < Falls.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(T(Falls[i].time)).Append(',').Append(Falls[i].uid);
            }
            // too long (thousands of drops): no replay is sent; the raid result still counts
            var s = sb.ToString();
            return s.Length <= MaxChars ? s : "";
        }

        /// <summary>Parses ToText(); returns null for empty or unknown text.</summary>
        public static ReplayRecorder Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var parts = text.Split('|');
            if (parts.Length < 5 || parts[0] != "v1") return null;
            try
            {
                var r = new ReplayRecorder(int.Parse(parts[1], CultureInfo.InvariantCulture));
                foreach (var e in Items(parts[2], "D:"))
                {
                    var f = e.Split(',');
                    r.RecordDrop(int.Parse(f[0]) / 10f, f[1], F(f[2]), F(f[3]));
                }
                foreach (var e in Items(parts[3], "S:"))
                {
                    var f = e.Split(',');
                    r.RecordStrike(int.Parse(f[0]) / 10f, F(f[1]), F(f[2]));
                }
                foreach (var e in Items(parts[4], "X:"))
                {
                    var f = e.Split(',');
                    r.RecordFall(int.Parse(f[0]) / 10f, f[1]);
                }
                return r;
            }
            catch (Exception) { return null; }
        }

        static IEnumerable<string> Items(string part, string prefix)
        {
            if (!part.StartsWith(prefix)) yield break;
            var body = part.Substring(prefix.Length);
            if (body.Length == 0) yield break;
            foreach (var e in body.Split(';')) yield return e;
        }
    }
}
