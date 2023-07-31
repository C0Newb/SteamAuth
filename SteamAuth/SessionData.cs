using SteamAuth.APIEndpoints;
using System.Collections.Specialized;
using System.Net;
using System.Text.Json;

namespace SteamAuth {
    public class SessionData {
        public ulong SteamID { get; set; }

        public string AccessToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public string SessionID { get; set; } = string.Empty;

        public async Task RefreshAccessToken() {
            if (string.IsNullOrEmpty(RefreshToken))
                throw new Exception("Refresh token is empty");

            if (IsTokenExpired(RefreshToken))
                throw new Exception("Refresh token is expired");

            GenerateAccessTokenForApp.GenerateAccessTokenForAppResponse? response;
            try {
                AuthenticationService authenticationService = new AuthenticationService(SteamID);

                var postData = new NameValueCollection {
                    { "refresh_token", RefreshToken },
                    { "steamid", SteamID.ToString() }
                };
                response = await authenticationService.GenerateAccessTokenForApp.Execute(RefreshToken);
            } catch (Exception ex) {
                throw new Exception("Failed to refresh token: " + ex.Message);
            }

            AccessToken = response?.AccessToken ?? throw new Exception("Failed to refresh token: empty response.");
        }

        public bool IsAccessTokenExpired() {
            if (string.IsNullOrEmpty(AccessToken))
                return true;

            return IsTokenExpired(AccessToken);
        }

        public bool IsRefreshTokenExpired() {
            if (string.IsNullOrEmpty(RefreshToken))
                return true;

            return IsTokenExpired(RefreshToken);
        }

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
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() > jwt?.exp;
        }

        public CookieContainer GetCookies() {
            SessionID ??= GenerateSessionID();

            var cookies = new CookieContainer();
            foreach (string domain in new string[] { "steamcommunity.com", "store.steampowered.com" }) {
                cookies.Add(new Cookie("steamLoginSecure", GetSteamLoginSecure(), "/", domain));
                cookies.Add(new Cookie("sessionid", SessionID, "/", domain));
                cookies.Add(new Cookie("mobileClient", "android", "/", domain));
                cookies.Add(new Cookie("mobileClientVersion", "777777 3.6.4", "/", domain));
            }
            return cookies;
        }

        private string GetSteamLoginSecure() {
            return SteamID.ToString() + "%7C%7C" + AccessToken;
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
            public long exp { get; set; }
        }
    }
}