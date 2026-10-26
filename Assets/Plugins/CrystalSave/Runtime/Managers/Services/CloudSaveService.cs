#if MEMORYPACK && ARAWN_REMEMBERME
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
using Unity.Services.Authentication;
using Unity.Services.Authentication.PlayerAccounts;
using Unity.Services.Core;
#if REMEMBERME_CLOUDSAVE_PRESENT
using UnityCloudSaveService = Unity.Services.CloudSave.CloudSaveService;
#endif
#endif

namespace Arawn.CrystalSave.Runtime
{
        /// <summary>
        /// Handles cloud related authentication and slot refresh logic.
        /// </summary>
        public class CloudSaveService
        {
                readonly SaveManager manager;
#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                string cachedEmail;
                string cachedPassword;
                bool   cachedCreateAccount;
#endif
                public CloudSaveService(SaveManager manager)
                {
                        this.manager = manager;
                }

                // Public callers share SaveManager's coordinator. Internal refresh preparation
                // returns detached data and never publishes, deletes, or raises events.
                public Task RefreshRemoteSlotsAsync() => manager.RefreshRemoteSlotsAsync();

                internal async Task<List<SaveSlot>> PrepareRefreshAsync(
                    IEnumerable<SaveSlot> localSlots, List<SaveSlot> obsolete)
                {
                    var local = localSlots.ToDictionary(slot => slot.SlotNumber, SaveManager.CopySlot);
                    if (!manager.SaveSettings.enableCloudSave) return local.Values.ToList();

                    List<SaveSlot> remote;
                    switch (manager.SaveSystemInternal)
                    {
                        case SupabaseSaveSystem supabase:
                            remote = await supabase.ListRemoteSlotsAsync();
                            break;
                        case FirebaseSaveSystem firebase:
                            remote = await firebase.ListRemoteSlotsAsync();
                            break;
                        case MySqlSaveSystem mysql:
                            remote = await mysql.ListRemoteSlotsAsync();
                            break;
                        case SaveSystem standard:
                            remote = await standard.ListRemoteSlotsAsync();
                            break;
                        default:
                            throw new NotSupportedException("The configured backend does not support remote slot discovery.");
                    }
                    SaveIoScope.CheckCancellation();
                    if (remote == null || remote.Any(slot => slot == null || slot.SlotNumber < 1) ||
                        remote.Select(slot => slot.SlotNumber).Distinct().Count() != remote.Count)
                        throw new System.IO.InvalidDataException("Remote slot discovery returned invalid or duplicate metadata.");

                    if (!manager.SaveSettings.keepLocalMirror)
                    {
                        var remoteNumbers = new HashSet<int>(remote.Select(slot => slot.SlotNumber));
                        foreach (var slot in local.Values.ToArray())
                        {
                            // Keep the existing recent-write grace period for eventually consistent providers.
                            if (slot.LastSaved == DateTime.MinValue || remoteNumbers.Contains(slot.SlotNumber) ||
                                (DateTime.UtcNow - slot.LastSaved).TotalSeconds <= 10) continue;
                            obsolete.Add(SaveManager.CopySlot(slot));
                            local[slot.SlotNumber] = new SaveSlot(slot.SlotNumber, $"Slot {slot.SlotNumber}",
                                DateTime.MinValue, string.Empty, string.Empty);
                        }
                    }
                    foreach (var slot in remote)
                    {
                        if (!local.TryGetValue(slot.SlotNumber, out var previous) || slot.LastSaved >= previous.LastSaved)
                            local[slot.SlotNumber] = SaveManager.CopySlot(slot);
                    }
                    // Every field comes from one generation. Empty metadata and screenshots are valid clears.
                    return local.Values.OrderBy(slot => slot.SlotNumber).ToList();
                }

                internal async Task DeleteObsoleteLocalMetadataAsync(IEnumerable<SaveSlot> obsolete)
                {
                    foreach (var slot in obsolete)
                    {
                        switch (manager.SaveSystemInternal)
                        {
                            case SupabaseSaveSystem supabase: await supabase.DeleteLocalSlotAsync(slot); break;
                            case FirebaseSaveSystem firebase: await firebase.DeleteLocalSlotAsync(slot); break;
                            case MySqlSaveSystem mysql: await mysql.DeleteLocalSlotAsync(slot); break;
                            case SaveSystem standard: await standard.DeleteLocalSlotAsync(slot); break;
                        }
                    }
                }

                public async void OnSupabaseLoggedIn(string uid)
                {
                        Debug.Log($"Supabase logged in with UID: {uid}");
                        if (manager.SaveSettings.backend != SaveBackend.Supabase)
                                return;

                        if (manager.SaveSettings.userFolderStrategy == UserFolderStrategy.Custom)
                                manager.IsSupabaseCustomLoggedInInternal = true;

                        try { await manager.ForceRefreshSlotsAsync(); }
                        catch (Exception ex)
                        {
                            Logger.Log($"Supabase slot refresh failed: {ex.Message}", LogCategory.CloudSave, LogLevel.Warning);
                        }
                }

                public void LogoutFromSupabase()
                {
                        if (!manager.SaveSettings.enableCloudSave || manager.SaveSettings.backend != SaveBackend.Supabase)
                                return;

                        manager.IsSupabaseCustomLoggedInInternal = false;

                        if (manager.SaveSettings.userFolderStrategy == UserFolderStrategy.Custom &&
                            manager.SaveSettings.customUserFolderResolver is CustomFolderAuthResolver cr)
                        {
                                cr.ClearCredentials();
                        }

                        Logger.Log("Supabase: logged out.", LogCategory.CloudSave, LogLevel.Info);
                }

#if REMEMBERME_AUTHENTICATION_PRESENT && REMEMBERME_CORESERVICES_PRESENT
                /* ───────────── Unity Cloud Sign-In ───────────── */
               public Task<bool> SignInUsernamePasswordAsync(string email, string password, bool createAccount = true)
                {
                        cachedEmail         = email;
                        cachedPassword      = password;
                        cachedCreateAccount = createAccount;
                        return SignInToCloudAsync(AuthProvider.UsernamePassword);
                }

               public async Task OnCloudSignedIn()
               {
#if REMEMBERME_CLOUDSAVE_PRESENT
                       // Unity Cloud Save initializes automatically when UnityServices.InitializeAsync completes.
                       // Older versions exposed InitAsync, which has since been removed.
#endif
                       await manager.ForceRefreshSlotsAsync();
               }

                public async Task<bool> SignInToCloudAsync(AuthProvider? overrideProvider = null)
                {
                        if (!manager.SaveSettings.enableCloudSave)
                                return false;

                        if (AuthenticationService.Instance.IsSignedIn)
                                return true;

                        AuthProvider original = manager.SaveSettings.defaultAuthProvider;
                        if (overrideProvider.HasValue)
                                manager.SaveSettings.defaultAuthProvider = overrideProvider.Value;
                        try
                        {
                                await InitializeCloudServicesAsync();
                        }
                        catch (Exception ex)
                        {
                                Debug.LogError($"Cloud sign-in failed: {ex.Message}\n{ex.StackTrace}");
                        }
                        finally
                        {
                                manager.SaveSettings.defaultAuthProvider = original;
                        }
                        return AuthenticationService.Instance.IsSignedIn;
                }

                public async Task<bool> UseEmailLoginAsync(string email, string password)
                {
                        cachedEmail = email;
                        cachedPassword = password;
                        if (AuthenticationService.Instance.IsSignedIn)
                        {
                                await AuthenticationService.Instance.AddUsernamePasswordAsync(email, password);
                        }
                        else
                        {
                                await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(email, password);
                        }
                        return true;
                }

                public async Task<bool> LinkIdentityAsync(AuthProvider provider)
                {
                        if (!AuthenticationService.Instance.IsSignedIn)
                        {
                                Logger.Log("LinkIdentityAsync: player is not signed-in yet.", LogCategory.CloudSave, LogLevel.Warning);
                                return false;
                        }

                        try
                        {
                                switch (provider)
                                {
                                        case AuthProvider.UsernamePassword:
                                                {
                                                        string email = cachedEmail;
                                                        string password = cachedPassword;
                                                        await UseEmailLoginAsync(email, password);
                                                        break;
                                                }
                                        case AuthProvider.Unity:
                                                {
                                                        var acct = PlayerAccountService.Instance;
                                                        if (!acct.IsSignedIn)
                                                                await acct.StartSignInAsync();
                                                        await AuthenticationService.Instance.LinkWithUnityAsync(acct.AccessToken);
                                                        break;
                                                }
#if REMEMBERME_GOOGLEPLAY_PRESENT
                                        case AuthProvider.GooglePlay:
                                                {
                                                        string code = await GooglePlayHelper.GetServerAuthCodeAsync();
                                                        await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(code);
                                                        break;
                                                }
#endif
#if REMEMBERME_APPLE_SIGNIN_PRESENT
                                        case AuthProvider.Apple:
                                                {
                                                        string jwt = await AppleSignInHelper.GetJwtAsync();
                                                        await AuthenticationService.Instance.LinkWithAppleAsync(jwt);
                                                        break;
                                                }
#endif
#if REMEMBERME_FACEBOOK_SDK_PRESENT
                                        case AuthProvider.Facebook:
                                                {
                                                        string fb = await FacebookSDK.GetAccessTokenAsync();
                                                        await AuthenticationService.Instance.LinkWithFacebookAsync(fb);
                                                        break;
                                                }
#endif
#if REMEMBERME_STEAMWORKS_PRESENT
                                        case AuthProvider.Steam:
                                                {
                                                        string ticket = SteamHelper.GetSessionTicket();
                                                        string identity = SteamHelper.GetIdentity() ?? "steam";
                                                        await AuthenticationService.Instance.LinkWithSteamAsync(ticket, identity);
                                                        break;
                                                }
#endif
                                        case AuthProvider.Anonymous:
                                        default:
                                                Logger.Log("LinkIdentityAsync: provider 'Anonymous' does not add a new identity.", LogCategory.CloudSave, LogLevel.Info);
                                                return false;
                                }

                                Logger.Log($"Linked {provider} identity to player {AuthenticationService.Instance.PlayerId}.", LogCategory.CloudSave, LogLevel.Info);
                                return true;
                        }
                        catch (AuthenticationException ex)
                        {
                                Logger.Log($"LinkIdentityAsync failed: {ex.Message}\nStack Trace: {ex.StackTrace}", LogCategory.CloudSave, LogLevel.Error);
                                return false;
                        }
                }

                public async Task InitializeCloudServicesAsync()
                {
                        try
                        {
                                await UnityServices.InitializeAsync();
                                Logger.Log("Unity Services initialised.", LogCategory.CloudSave, LogLevel.Info);
                        }
                        catch (Exception ex)
                        {
                                Debug.LogError($"Unity Services initialization failed: {ex.Message}\n{ex.StackTrace}");
                                return;
                        }

                        if (AuthenticationService.Instance.IsSignedIn)
                        {
                                await OnCloudSignedIn();
                                return;
                        }

                        try
                        {
                                switch (manager.SaveSettings.defaultAuthProvider)
                                {
                                        case AuthProvider.UsernamePassword:
                                                {
                                                       string email    = cachedEmail;
                                                       string password = cachedPassword;
                                                       bool create     = cachedCreateAccount;

                                                       if (create)
                                                               await AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(email, password);
                                                       else
                                                               await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(email, password);

                                                       break;
                                               }
                                        case AuthProvider.Unity:
                                                {
                                                        var playerSvc = PlayerAccountService.Instance;
                                                        if (!playerSvc.IsSignedIn)
                                                        {
                                                                var tcs = new TaskCompletionSource<bool>();
                                                                void OnSignedIn()
                                                                {
                                                                        playerSvc.SignedIn -= OnSignedIn;
                                                                        tcs.TrySetResult(true);
                                                                }
                                                                playerSvc.SignedIn += OnSignedIn;
                                                                await playerSvc.StartSignInAsync();
                                                                await tcs.Task;
                                                        }
                                                        await AuthenticationService.Instance.SignInWithUnityAsync(
                                                                playerSvc.AccessToken,
                                                                new SignInOptions { CreateAccount = true });
                                                        break;
                                                }
#if REMEMBERME_GOOGLEPLAY_PRESENT
                                        case AuthProvider.GooglePlay:
                                                {
                                                        string authCode = await GooglePlayHelper.GetServerAuthCodeAsync();
                                                        await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(
                                                                authCode, new SignInOptions { CreateAccount = true });
                                                        break;
                                                }
#endif
#if REMEMBERME_APPLE_SIGNIN_PRESENT
                                        case AuthProvider.Apple:
                                                {
                                                        string jwt = await AppleSignInHelper.GetJwtAsync();
                                                        await AuthenticationService.Instance.SignInWithAppleAsync(
                                                                jwt, new SignInOptions { CreateAccount = true });
                                                        break;
                                                }
#endif
#if REMEMBERME_FACEBOOK_SDK_PRESENT
                                        case AuthProvider.Facebook:
                                                {
                                                        string fbToken = await FacebookSDK.GetAccessTokenAsync();
                                                        await AuthenticationService.Instance.SignInWithFacebookAsync(
                                                                fbToken, new SignInOptions { CreateAccount = true });
                                                        break;
                                                }
#endif
#if REMEMBERME_STEAMWORKS_PRESENT
                                        case AuthProvider.Steam:
                                                {
                                                        string ticket = SteamHelper.GetSessionTicket();
                                                        string identity = SteamHelper.GetIdentity() ?? "steam";
                                                        await AuthenticationService.Instance.SignInWithSteamAsync(
                                                                ticket, identity, new SignInOptions { CreateAccount = true });
                                                        break;
                                                }
#endif
                                        case AuthProvider.Anonymous:
                                        default:
                                                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                                                break;
                                }

                                Logger.Log($"Player signed in with {manager.SaveSettings.defaultAuthProvider}.", LogCategory.CloudSave, LogLevel.Info);
                                await OnCloudSignedIn();
                        }
                        catch (RequestFailedException ex)
                        {
                                Debug.LogError($"Authentication failed ({ex.ErrorCode}): {ex.Message}\n{ex.StackTrace}");
                        }
                        catch (Exception ex)
                        {
                                Debug.LogError($"Authentication failed: {ex.Message}\n{ex.StackTrace}");
                        }
                }
#endif // Authentication
        }
}
#endif
