using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Emberhold.Online
{
    /// <summary>
    /// Keeps the local save (still the main save, works offline) in step with the cloud.
    ///
    ///   var sync = new CloudSaveSync(OnlineServices.Current);
    ///   await sync.ConnectAsync("Peter");                // at start, after the city is loaded
    ///   if (sync.Advice == RestoreAdvice.CloudIsNewer) { ...ask "Load your cloud city?" -> await sync.LoadCloudCityAsync() }
    ///   sync.Tick(state, Time.unscaledDeltaTime);       // every frame: uploads every PushIntervalSeconds
    ///   sync.PushSoon();                                // after something important (raid end, purchase)
    ///   _ = sync.PushAsync(state);                      // in OnApplicationPause(true)
    ///
    /// Each upload brings back what happened on the server and applies it to the GameState:
    /// raid losses / loot (resources), ruined buildings, and a clamp if resources were impossibly high.
    /// Listen to the events to show toasts and to ruin the live Building objects.
    /// </summary>
    public class CloudSaveSync
    {
        public enum RestoreAdvice { None, CloudIsNewer }

        public float PushIntervalSeconds = 60f;

        /// <summary>Someone raided you: res is what you lost (negative), info.by the attacker, info.ruined the buildings.</summary>
        public event Action<LedgerEntry> Raided;
        /// <summary>Loot from your raid arrived in your city.</summary>
        public event Action<LedgerEntry> LootArrived;
        /// <summary>A building of yours became a ruin in a raid. Call Building.SetRuined(true) on the live one with this uid.
        /// With no listener, only BuildingData.ruined is set (fine before the city is spawned).</summary>
        public event Action<string> BuildingRuined;
        /// <summary>The server lowered resources that were too high (cheat or bug). Values are already applied.</summary>
        public event Action<Resources5> Clamped;

        public HelloReply Hello { get; private set; }
        public RestoreAdvice Advice { get; private set; }
        public bool Connected { get { return Hello != null && Hello.ok; } }
        public int Trophies { get; private set; }
        public long ShieldUntil { get; private set; }
        public string LastError { get; private set; }

        readonly IOnlineService service;
        float timer;
        bool busy;

        const string RevKey = "emberhold_cloud_rev";

        public CloudSaveSync(IOnlineService service) { this.service = service; }

        /// <summary>Signs in. Sets Advice = CloudIsNewer when the cloud has a city this phone has not seen (new phone / reinstall / other device).</summary>
        public async Task<HelloReply> ConnectAsync(string displayName)
        {
            Hello = await service.SignInAsync(displayName);
            Advice = RestoreAdvice.None;
            if (!Hello.ok) { LastError = Hello.error; return Hello; }
            if (Hello.hasCloudCity && Hello.cloudRev > LocalRev) Advice = RestoreAdvice.CloudIsNewer;
            return Hello;
        }

        /// <summary>The cloud city, to replace the local one (then save it locally). Null if none or offline.</summary>
        public async Task<GameState> LoadCloudCityAsync()
        {
            var r = await service.LoadCityAsync();
            if (!r.ok || string.IsNullOrEmpty(r.city)) { LastError = r.error; return null; }
            var s = Json.From<GameState>(r.city);
            if (s != null) { LocalRev = r.rev; Advice = RestoreAdvice.None; }
            return s;
        }

        /// <summary>Keep the phone's city (overwrites the cloud at the next upload).</summary>
        public void KeepLocal() { Advice = RestoreAdvice.None; }

        public void Tick(GameState state, float deltaTime)
        {
            if (!Connected || Advice != RestoreAdvice.None || state == null) return;
            timer += deltaTime;
            if (timer >= PushIntervalSeconds && !busy) { timer = 0f; PushInBackground(state); }
        }

        async void PushInBackground(GameState state)
        {
            try { await PushAsync(state); }
            catch (Exception e) { Debug.LogWarning("[Online] cloud save failed: " + e.Message); }
        }

        /// <summary>Upload at the next Tick.</summary>
        public void PushSoon() { timer = PushIntervalSeconds; }

        public async Task<SaveCityReply> PushAsync(GameState state)
        {
            if (!Connected || busy || state == null || Advice != RestoreAdvice.None) return null;
            busy = true;
            try
            {
                var r = await service.SaveCityAsync(state);
                if (!r.ok) { LastError = r.error; return r; }
                LastError = null;
                LocalRev = r.rev;
                Trophies = r.trophies;
                ShieldUntil = r.shieldUntil;
                Apply(state, r);
                return r;
            }
            finally { busy = false; }
        }

        /// <summary>Applies the server's changes to the GameState (public for tests).</summary>
        public void Apply(GameState state, SaveCityReply r)
        {
            if (r.deltas != null)
                foreach (var d in r.deltas)
                {
                    if (d.res != null)
                        foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
                            if (t != ResourceType.None && d.res.Get(t) != 0) state.Add(t, d.res.Get(t));
                    if (d.IsLoss && d.info != null && d.info.ruined != null)
                        foreach (var uid in d.info.ruined)
                        {
                            var b = state.buildings.Find(x => x.uid == uid);
                            if (b == null) continue;
                            if (BuildingRuined != null) BuildingRuined(uid);
                            else { b.ruined = true; b.hp = 0; b.upgradeEndUtcTicks = 0; }
                        }
                    if (d.IsLoss) { if (Raided != null) Raided(d); }
                    else if (LootArrived != null) LootArrived(d);
                }
            if (r.clampedAny && r.clamped != null)
            {
                foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
                {
                    if (t == ResourceType.None) continue;
                    int cap = r.clamped.Get(t);
                    if (cap > 0 && state.Get(t) > cap) state.Add(t, cap - state.Get(t));
                }
                if (Clamped != null) Clamped(r.clamped);
            }
        }

        static int LocalRev
        {
            get { return PlayerPrefs.GetInt(RevKey, 0); }
            set { PlayerPrefs.SetInt(RevKey, value); PlayerPrefs.Save(); }
        }
    }
}
