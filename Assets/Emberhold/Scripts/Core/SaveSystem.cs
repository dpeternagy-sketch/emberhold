using System;
using System.IO;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Local JSON save. Kept behind a tiny interface so a cloud save backend can replace it later.
    /// </summary>
    public interface ISaveBackend
    {
        GameState Load();
        void Save(GameState state);
        void Delete();
    }

    public class LocalSaveBackend : ISaveBackend
    {
        static string PathOnDisk { get { return Path.Combine(Application.persistentDataPath, "emberhold_save.json"); } }

        public GameState Load()
        {
            try
            {
                if (!File.Exists(PathOnDisk)) return null;
                return JsonUtility.FromJson<GameState>(File.ReadAllText(PathOnDisk));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Emberhold] Could not load save: " + e.Message);
                return null;
            }
        }

        public void Save(GameState state)
        {
            try
            {
                state.lastSavedUtcTicks = DateTime.UtcNow.Ticks;
                var tmp = PathOnDisk + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(state));
                if (File.Exists(PathOnDisk)) File.Delete(PathOnDisk);
                File.Move(tmp, PathOnDisk);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Emberhold] Could not save: " + e.Message);
            }
        }

        public void Delete()
        {
            if (File.Exists(PathOnDisk)) File.Delete(PathOnDisk);
        }
    }
}
