using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Emberhold.EditorTools
{
    /// <summary>
    /// Emberhold is a phone game played upright: the player build is locked to portrait and the
    /// Game view shows a 1080x1920 phone screen, so what we see in the Editor is what the phone shows.
    /// </summary>
    [InitializeOnLoad]
    static class PortraitSetup
    {
        const int W = 1080, H = 1920;
        const string Label = "Emberhold Phone";

        static PortraitSetup() { EditorApplication.delayCall += Apply; }

        [MenuItem("Emberhold/Setup/Portrait phone view", priority = 20)]
        static void Apply()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            try { SelectGameViewSize(); }
            catch (Exception e) { Debug.LogWarning("[Emberhold] Could not set the Game view to portrait at step " + step + ": " + e); }
        }

        static int step;

        static void SelectGameViewSize()
        {
            step = 0;
            step++; var asm = typeof(Editor).Assembly;
            step++; var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            step++; var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            step++; var instance = single.GetProperty("instance").GetValue(null, null);
            step++; var gtProp = sizesType.GetProperty("currentGroupType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            var groupType = gtProp != null ? (GameViewSizeGroupType)gtProp.GetValue(gtProp.GetGetMethod(true).IsStatic ? null : instance, null) : GameViewSizeGroupType.Standalone;
            step++; var group = sizesType.GetMethod("GetGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, new object[] { (int)groupType });
            step++; var groupT = group.GetType();
            int total = (int)groupT.GetMethod("GetTotalCount").Invoke(group, null);
            int index = -1;
            step++; var texts = (string[])groupT.GetMethod("GetDisplayTexts").Invoke(group, null);
            for (int i = 0; i < texts.Length; i++) if (texts[i].StartsWith(Label)) { index = i; break; }
            if (index < 0)
            {
                var sizeT = asm.GetType("UnityEditor.GameViewSize");
                var kindT = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = sizeT.GetConstructor(new[] { kindT, typeof(int), typeof(int), typeof(string) });
                var size = ctor.Invoke(new object[] { Enum.Parse(kindT, "FixedResolution"), W, H, Label });
                groupT.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = total;
            }
            step++; var gvT = asm.GetType("UnityEditor.GameView");
            step++; var gv = EditorWindow.GetWindow(gvT, false, null, false);
            step++; var prop = gvT.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null) prop.SetValue(gv, index, null);
            else gvT.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(gv, new object[] { index, null });
            gv.Repaint();
        }
    }
}
