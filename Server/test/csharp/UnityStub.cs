// Minimal stand-ins for the few UnityEngine types the online code and the data classes use,
// so they can be compiled and tested without Unity (mcs / dotnet). Not shipped in the game.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color gray { get { return new Color(.5f, .5f, .5f); } } public static Color white { get { return new Color(1, 1, 1); } } }
    public static class Mathf
    {
        public static int CeilToInt(float f) { return (int)Math.Ceiling(f); }
        public static int FloorToInt(float f) { return (int)Math.Floor(f); }
        public static int RoundToInt(float f) { return (int)Math.Round(f); }
        public static int Clamp(int v, int a, int b) { return v < a ? a : v > b ? b : v; }
        public static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }
        public static int Max(int a, int b) { return Math.Max(a, b); }
        public static int Min(int a, int b) { return Math.Min(a, b); }
        public static float Max(float a, float b) { return Math.Max(a, b); }
        public static float Min(float a, float b) { return Math.Min(a, b); }
    }
    public static class Debug { public static void Log(object o) { Console.WriteLine(o); } public static void LogWarning(object o) { Console.WriteLine("WARN " + o); } }
    public static class JsonUtility
    {
        public static string ToJson(object o) { throw new NotSupportedException("JsonUtility only exists in Unity"); }
        public static T FromJson<T>(string s) { throw new NotSupportedException("JsonUtility only exists in Unity"); }
    }
    public static class PlayerPrefs
    {
        static readonly Dictionary<string, int> ints = new Dictionary<string, int>();
        public static int GetInt(string k, int d) { int v; return ints.TryGetValue(k, out v) ? v : d; }
        public static void SetInt(string k, int v) { ints[k] = v; }
        public static void Save() { }
    }
}
