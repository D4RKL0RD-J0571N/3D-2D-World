#if ARAWN_REMEMBERME && MEMORYPACK
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Arawn.CrystalSave.Runtime
{
    public static class SupabaseRestAuth
    {
        const string JSON = "application/json";
        const int RequestTimeoutSeconds = 15;

        /* ───── DTOs ───── */
        [Serializable] sealed class PasswordRequest
        {
            public string email;
            public string password;
        }
        [Serializable] sealed class MagicLinkRequest
        {
            public string email;
            public bool create_user = true;
            public string type = "magiclink";
        }
        [Serializable] struct UserJson   { public string id; }          // auth.uid()
        [Serializable] sealed class SessionJson
        {
            public string    id;            // confirmation-required signup returns the user directly
            public string    access_token;
            public int       expires_in;
            public string    refresh_token;
            public string    token_type;
            public UserJson  user;          // default(id=null) if backend omits user object
        }
        [Serializable] sealed class JwtClaims { public string sub; }
        
        /* ─────────────────────────────────────────────────────────────── */
        /*  Sign-up (create a new user)                                    */
        /*  - returns (jwt, userId, needsConfirmation)                     */
        /* ─────────────────────────────────────────────────────────────── */
        public static IEnumerator SignUpWithEmailPassword(
            string projectUrl, string anonKey,
            string email, string password,
            Action<string, string, bool> onSuccess,    // jwt, userId, needsConfirm
            Action<string>               onError)
        {
            string url = $"{projectUrl.TrimEnd('/')}/auth/v1/signup";
            using var req = CreateRequest(url, anonKey, new PasswordRequest { email = email, password = password });

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                if (!TryReadSession(req.downloadHandler.text, false, out var session, out string uid))
                {
                    onError?.Invoke("Supabase sign-up returned an invalid user or session response.");
                    yield break;
                }
                string jwt = session.access_token;           // may be empty
                bool needsConfirm = string.IsNullOrEmpty(jwt);

                onSuccess?.Invoke(jwt, uid, needsConfirm);
            }
            else
            {
                Debug.LogError(
                    $"Supabase Sign-up {req.responseCode}: {req.downloadHandler.text}");
                onError?.Invoke(req.error);
            }
        }


        /* ─────────────────────────────── sign-in with e-mail + password */
        public static IEnumerator SignInWithPassword(
            string projectUrl, string anonKey,
            string email, string password,
            Action<string, string> onSuccess,   // jwt, userId
            Action<string> onError)
        {
            string url = $"{projectUrl.TrimEnd('/')}/auth/v1/token?grant_type=password";
            using var req = CreateRequest(url, anonKey, new PasswordRequest { email = email, password = password });

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                if (!TryReadSession(req.downloadHandler.text, true, out var session, out string uid))
                {
                    onError?.Invoke("Supabase sign-in returned an invalid user or session response.");
                    yield break;
                }
                onSuccess?.Invoke(session.access_token, uid);
            }
            else
            {
                Debug.LogError(
                    $"Supabase Auth {req.responseCode}: {req.downloadHandler.text}");
                onError?.Invoke(req.error);
            }
        }


        /* legacy overload (JWT only) */
        public static IEnumerator SignInWithPassword(
            string projectUrl, string anonKey,
            string email, string password,
            Action<string> onSuccess, Action<string> onError)
        {
            return SignInWithPassword(
                projectUrl, anonKey, email, password,
                (jwt, _uid) => onSuccess?.Invoke(jwt),
                onError);
        }

        /* ─────────────────────────────── send magic-link (OTP) */
        public static IEnumerator SendMagicLink(
            string projectUrl, string anonKey,
            string email,
            Action onOk, Action<string> onError,
            string redirectTo = null)
        {
            string url = $"{projectUrl.TrimEnd('/')}/auth/v1/otp";
            // GoTrue reads redirect_to from the query/form or header, not the JSON body.
            if (!string.IsNullOrEmpty(redirectTo))
                url += "?redirect_to=" + Uri.EscapeDataString(redirectTo);
            using var req = CreateRequest(url, anonKey, new MagicLinkRequest { email = email });

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onOk?.Invoke();
            else
                onError?.Invoke(req.error);
        }

        static UnityWebRequest CreateRequest<T>(string url, string anonKey, T body)
        {
            var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(CloudMetadataJson.Encode(body))),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = RequestTimeoutSeconds
            };
            req.SetRequestHeader("apikey", anonKey);
            req.SetRequestHeader("Content-Type", JSON);
            req.SetRequestHeader("Accept", JSON);
            return req;
        }

        static bool TryReadSession(string json, bool requireToken, out SessionJson session, out string userId)
        {
            session = null;
            userId = null;
            try
            {
                session = CloudMetadataJson.Decode<SessionJson>(json);
                if (session == null || (requireToken && string.IsNullOrEmpty(session.access_token))) return false;
                userId = !string.IsNullOrEmpty(session.user.id) ? session.user.id :
                    !string.IsNullOrEmpty(session.id) ? session.id : ExtractSubFromJwt(session.access_token);
                return !string.IsNullOrEmpty(userId);
            }
            catch (System.IO.InvalidDataException) { return false; }
        }

        /* ─────────────────────────────── helper: extract "sub" from JWT */
        static string ExtractSubFromJwt(string jwt)
        {
            if (string.IsNullOrEmpty(jwt)) return null;
            var parts = jwt.Split('.');
            if (parts.Length < 2) return null;

            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "=";  break;
            }

            try
            {
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                return CloudMetadataJson.Decode<JwtClaims>(json)?.sub;
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif
