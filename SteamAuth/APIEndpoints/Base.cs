using System.Collections.Specialized;
using System.Net;
using System.Text.Json.Serialization;

namespace SteamAuth.APIEndpoints {
    public class Base {
        public const string CommunityBase = "https://steamcommunity.com";
        public const string SteamWebAPIBase = "https://api.steampowered.com";

        internal ulong? SteamId;
        internal string AccessToken;
        internal CookieContainer Cookies;
        internal NameValueCollection Parameters = new NameValueCollection();

        internal NameValueCollection PostBody {
            get {
                NameValueCollection body = new NameValueCollection();
                if (SteamId != null)
                    body.Set("steamid", SteamId.ToString());
                return body;
            }
        }
        

        public Base(ulong? steamId = null, string? accessToken = null, CookieContainer? cookies = null) {
            SteamId = steamId;
            AccessToken = accessToken ?? string.Empty;
            if (!string.IsNullOrEmpty(accessToken))
                Parameters.Set("access_token", accessToken);

            Cookies = cookies ?? new CookieContainer();
        }


        /// <summary>
        /// This is the first layer in Steam's response
        /// </summary>
        public sealed class BaseResponse<T> {
            [JsonPropertyName("response")]
            public T? Response { get; set; }

            public BaseResponse() => Response = default;
        }


        public Task<string> GET(string url) {
            string queryString = string.Join("&", Parameters.AllKeys
                .Where(key => !string.IsNullOrEmpty(key))
                .Where(key => Parameters[key] != null)
                .Select(key => $"{Uri.EscapeDataString(key ?? string.Empty)}={Uri.EscapeDataString(Parameters[key] ?? string.Empty)}"));

            if (!string.IsNullOrEmpty(queryString)) {
                if (url.Contains('?'))
                    url += "&" + queryString;
                else
                    url += "?" + queryString;
            }

            return SteamWeb.GET(url, Cookies);
        }

        public Task<string> POST(string url, NameValueCollection? body = null) {
            string queryString = string.Join("&", Parameters.AllKeys
                .Where(key => !string.IsNullOrEmpty(key))
                .Where(key => Parameters[key] != null)
                .Select(key => $"{Uri.EscapeDataString(key ?? string.Empty)}={Uri.EscapeDataString(Parameters[key] ?? string.Empty)}"));

            if (!string.IsNullOrEmpty(queryString)) {
                if (url.Contains('?'))
                    url += "&" + queryString;
                else
                    url += "?" + queryString;
            }

            return SteamWeb.POST(url, body ?? PostBody, Cookies);
        }
    }
}
