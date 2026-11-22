#if EMBERHOLD_UGS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

namespace Emberhold.Online
{
    /// <summary>
    /// Unity Gaming Services backend. Needs the packages com.unity.services.authentication and
    /// com.unity.services.cloudcode, the project linked to a Unity Cloud project, the Cloud Code script
    /// Server/cloudcode/emberhold.js deployed, and the scripting define EMBERHOLD_UGS. See Server/README.md.
    /// </summary>
    public class UgsOnlineService : IOnlineService
    {
        const string Script = "emberhold";

        public bool SignedIn { get { return UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn; } }
        public string PlayerId { get { return SignedIn ? AuthenticationService.Instance.PlayerId : null; } }

        public async Task<HelloReply> SignInAsync(string displayName)
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                // Guest account. The session token is cached on the phone, so the same player comes back.
                // Later: AuthenticationService.Instance.LinkWithAppleAsync / LinkWithGoogleAsync to keep it across phones.
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Online] sign-in failed: " + e.Message);
                return new HelloReply { ok = false, error = "network" };
            }
            return await Call<HelloReply>("hello", Args.Name(displayName));
        }

        public Task<NameReply> SetNameAsync(string n) { return Call<NameReply>("setName", Args.Name(n)); }
        public Task<LoadCityReply> LoadCityAsync() { return Call<LoadCityReply>("loadCity", ""); }
        public Task<SaveCityReply> SaveCityAsync(GameState s) { return Call<SaveCityReply>("saveCity", Args.City(s)); }
        public Task<FriendsReply> GetFriendsAsync() { return Call<FriendsReply>("getFriends", ""); }
        public Task<AddFriendReply> AddFriendAsync(string code) { return Call<AddFriendReply>("addFriend", Args.Code(code)); }
        public Task<RespondFriendReply> RespondFriendAsync(string id, bool accept) { return Call<RespondFriendReply>("respondFriend", Args.Player(id, accept)); }
        public Task<OnlineReply> RemoveFriendAsync(string id) { return Call<OnlineReply>("removeFriend", Args.Player(id, false)); }
        public Task<StartRaidReply> StartRaidAsync(string id, string revengeOf = null) { return Call<StartRaidReply>("startRaid", Args.StartRaid(id, revengeOf)); }
        public Task<FinishRaidReply> FinishRaidAsync(RaidReport r) { return Call<FinishRaidReply>("finishRaid", Json.To(r)); }
        public Task<RaidLogReply> GetRaidLogAsync() { return Call<RaidLogReply>("getRaidLog", ""); }
        public Task<ReplayReply> GetReplayAsync(string raidId) { return Call<ReplayReply>("getReplay", Args.RaidId(raidId)); }

        async Task<T> Call<T>(string action, string payload) where T : OnlineReply, new()
        {
            if (!SignedIn) return new T { ok = false, error = "not_signed_in" };
            try
            {
                var args = new Dictionary<string, object> { { "action", action }, { "payload", payload ?? "" } };
                string json = await CloudCodeService.Instance.CallEndpointAsync<string>(Script, args);
                var reply = Json.From<T>(json);
                if (reply == null) return new T { ok = false, error = "server_error" };
                ServerClock.Observe(reply.serverNow);
                return reply;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Online] " + action + " failed: " + e.Message);
                return new T { ok = false, error = "network" };
            }
        }
    }
}
#endif
