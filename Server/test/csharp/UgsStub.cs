// Compile-check stand-ins for the Unity Gaming Services API used by UgsOnlineService (signatures only).
using System.Collections.Generic; using System.Threading.Tasks;
namespace Unity.Services.Core { public enum ServicesInitializationState { Uninitialized, Initializing, Initialized }
  public static class UnityServices { public static ServicesInitializationState State; public static Task InitializeAsync() { return Task.FromResult(0); } } }
namespace Unity.Services.Authentication { public interface IAuthenticationService { bool IsSignedIn { get; } string PlayerId { get; } Task SignInAnonymouslyAsync(); }
  public static class AuthenticationService { public static IAuthenticationService Instance; } }
namespace Unity.Services.CloudCode { public interface ICloudCodeService { Task<T> CallEndpointAsync<T>(string function, Dictionary<string, object> args = null); }
  public static class CloudCodeService { public static ICloudCodeService Instance; } }
