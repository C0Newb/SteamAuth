using SteamAuth.APIEndpoints;
using System.Collections.Specialized;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SteamAuth {
    /// <summary>
    /// <see cref="SteamGuardAccount"/> session data, used to authenticate with the Steam WebAPI.
    /// </summary>
    public class SessionData {
        /// <summary>
        /// Steam account user id
        /// </summary>
        [JsonPropertyName("SteamID")]
        public ulong SteamId { get; set; }

        /// <summary>
        /// Session id
        /// </summary>
        [JsonPropertyName("SessionID")]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Session key
        /// </summary>
        [JsonPropertyName("AccessToken")]
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Session refresh key, used to refresh (reset) the <see cref="AccessToken"/>
        /// </summary>
        [JsonPropertyName("RefreshToken")]
        public string RefreshToken { get; set; } = string.Empty;


        /// <summary>
        /// Refreshes the session access token using the refresh token.
        /// </summary>
        /// <returns>Always true. If refreshing fails, an exception is thrown.</returns>
        /// <exception cref="InvalidRefreshTokenException">Refresh token is invalid/empty.</exception>
        /// <exception cref="AccessTokenRefreshException">An error was encountered while refreshing the access token.</exception>
        public async Task<bool> RefreshAccessToken() {
            if (string.IsNullOrEmpty(RefreshToken))
                throw new InvalidRefreshTokenException("Refresh token is empty");

            if (IsTokenExpired(RefreshToken))
                throw new InvalidRefreshTokenException("Refresh token is expired");

            GenerateAccessTokenForApp.GenerateAccessTokenForAppResponse? response;
            try {
                AuthenticationService authenticationService = new AuthenticationService(SteamId);

                var postData = new NameValueCollection {
                    { "refresh_token", RefreshToken },
                    { "steamid", SteamId.ToString() }
                };
                response = await authenticationService.GenerateAccessTokenForApp.Execute(RefreshToken);
            } catch (Exception ex) {
                throw new AccessTokenRefreshException("Failed to refresh token: " + ex.Message, ex);
            }

            AccessToken = response?.AccessToken ?? throw new InvalidRefreshTokenException("Failed to refresh token: empty response.");
            return true;
        }

        /// <summary>
        /// Checks whether the session access token has expired or not.
        /// The access token can be regenerated using the refresh token.
        /// </summary>
        /// <returns>If the access token has expired.</returns>
        public bool IsAccessTokenExpired() => string.IsNullOrEmpty(AccessToken) || IsTokenExpired(AccessToken);

        /// <summary>
        /// Checks whether the session refresh token has expired or not.
        /// </summary>
        /// <returns>If the refresh token has expired.</returns>
        public bool IsRefreshTokenExpired() => string.IsNullOrEmpty(RefreshToken) || IsTokenExpired(RefreshToken);


        /// <summary>
        /// Returns the cookies required to interact with this session.
        /// </summary>
        /// <returns>Session cookies.</returns>
        public CookieContainer GetCookies() {
            SessionId ??= GenerateSessionID();

            var cookies = new CookieContainer();
            foreach (string domain in new string[] { "steamcommunity.com", "store.steampowered.com" }) {
                cookies.Add(new Cookie("steamLoginSecure", GetSteamLoginSecure(), "/", domain));
                cookies.Add(new Cookie("sessionid", SessionId, "/", domain));
                cookies.Add(new Cookie("mobileClient", "android", "/", domain));
                cookies.Add(new Cookie("mobileClientVersion", "777777 3.6.4", "/", domain));
            }
            return cookies;
        }


        #region Helpers
        private static bool IsTokenExpired(string token) {
            var tokenComponents = token.Split('.');
            // Fix up base64url to normal base64
            var base64 = tokenComponents[1].Replace('-', '+').Replace('_', '/');

            if (base64.Length % 4 != 0) {
                base64 += new string('=', 4 - base64.Length % 4);
            }

            var payloadBytes = Convert.FromBase64String(base64);
            var jwt = JsonSerializer.Deserialize<SteamAccessToken>(payloadBytes);

            // Compare expire time of the token to the current time
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() > jwt?.Exp;
        }

        private string GetSteamLoginSecure() {
            return SteamId.ToString() + "%7C%7C" + AccessToken;
        }

        private static string GenerateSessionID() {
            return GetRandomHexNumber(32);
        }

        private static string GetRandomHexNumber(int digits) {
            Random random = new Random();
            byte[] buffer = new byte[digits / 2];
            random.NextBytes(buffer);
            string result = String.Concat(buffer.Select(x => x.ToString("X2")).ToArray());
            if (digits % 2 == 0)
                return result;
            return result + random.Next(16).ToString("X");
        }

        private class SteamAccessToken {
            [JsonPropertyName("exp")]
            public long Exp { get; set; }
        }


        /// <summary>
        /// The attempt to refresh the access token failed.
        /// </summary>
        [Serializable]
        public class AccessTokenRefreshException : Exception {
            public AccessTokenRefreshException() { }
            public AccessTokenRefreshException(string message) : base(message) { }
            public AccessTokenRefreshException(string message, Exception inner) : base(message, inner) { }
            protected AccessTokenRefreshException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
        }

        /// <summary>
        /// The session refresh token was either null, empty, or has expired.
        /// </summary>
        [Serializable]
        public class InvalidRefreshTokenException : Exception {
            public InvalidRefreshTokenException() { }
            public InvalidRefreshTokenException(string message) : base(message) { }
            public InvalidRefreshTokenException(string message, Exception inner) : base(message, inner) { }
            protected InvalidRefreshTokenException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
        }
        #endregion
    }
}