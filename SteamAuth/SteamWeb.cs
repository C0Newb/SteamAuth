using System.Collections.Specialized;
using System.Net;

namespace SteamAuth {
    /// <summary>
    /// Helper class to make web requests, such as POST and GET.
    /// </summary>
    public class SteamWeb {
        /// <summary>
        /// Our mobile user agent. Meant to mimic the Steam app.
        /// </summary>
        public const string MOBILE_APP_USER_AGENT = "Dalvik/2.1.0 (Linux; U; Android 9; Valve Steam App Version/3)";

        private static HttpClient CreateHttpClient(CookieContainer cookies) {
            var handler = new HttpClientHandler { CookieContainer = cookies };
            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(MOBILE_APP_USER_AGENT);

            return client;
        }

        /// <summary>
        /// Makes a GET (receive from server) request and returns the resulting string.
        /// </summary>
        /// <param name="url">Where to send the GET request.</param>
        /// <param name="cookies">Cookies to use in the <see cref="HttpClient"/>.</param>
        /// <returns>Response from a GET request to <paramref name="url"/>.</returns>
        public static async Task<string> GET(string url, CookieContainer? cookies = null) {
            using var client = CreateHttpClient(cookies ?? new CookieContainer());
            var response = await client.GetStringAsync(url);
            return response;
        }

        /// <summary>
        /// Makes a POST (send to server) request and returns the resulting string.
        /// </summary>
        /// <param name="url">Where to send the POST request.</param>
        /// <param name="cookies">Cookies to use in the <see cref="HttpClient"/>.</param>
        /// <param name="body">Data to POST.</param>
        /// <returns>Response from a POST request to <paramref name="url"/>.</returns>
        public static async Task<string> POST(string url, NameValueCollection? body = null, CookieContainer? cookies = null) {
            body ??= new NameValueCollection();
            // Convert the NameValueCollection to IEnumerable<KeyValuePair<string, string>>
            IEnumerable<KeyValuePair<string, string>> bodyPairs = body.AllKeys
                .Cast<string>()
                .SelectMany(
                    key => body.GetValues(key) ?? Array.Empty<string>(),
                    (key, value) => new KeyValuePair<string, string>(key, value)
                );


            using var client = CreateHttpClient(cookies ?? new CookieContainer());
            var response = await client.PostAsync(url, new FormUrlEncodedContent(bodyPairs));

            response.EnsureSuccessStatusCode();
            string responseString = await response.Content.ReadAsStringAsync();
            return responseString;
        }
    }
}