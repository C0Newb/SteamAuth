using SteamAuth.APIEndpoints;
using SteamAuth.Enums;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SteamAuth {
    public class SteamGuardAccount {
        [JsonPropertyName("shared_secret")]
        public string? SharedSecret { get; set; }

        [JsonPropertyName("serial_number")]
        public string? SerialNumber { get; set; }

        [JsonPropertyName("revocation_code")]
        public string RevocationCode { get; set; } = string.Empty;

        [JsonPropertyName("uri")]
        public string? URI { get; set; }

        [JsonPropertyName("server_time")]
        public long ServerTime { get; set; }

        [JsonPropertyName("account_name")]
        public string? AccountName { get; set; }

        [JsonPropertyName("token_gid")]
        public string? TokenGID { get; set; }

        [JsonPropertyName("identity_secret")]
        public string? IdentitySecret { get; set; }

        [JsonPropertyName("secret_1")]
        public string? Secret1 { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("device_id")]
        public string? DeviceID { get; set; }

        /// <summary>
        /// Set to true if the authenticator has actually been applied to the account.
        /// </summary>
        [JsonPropertyName("fully_enrolled")]
        public bool FullyEnrolled { get; set; }

        public SessionData? Session { get; set; }

        private static readonly byte[] steamGuardCodeTranslations = new byte[] { 50, 51, 52, 53, 54, 55, 56, 57, 66, 67, 68, 70, 71, 72, 74, 75, 77, 78, 80, 81, 82, 84, 86, 87, 88, 89 };

        /// <summary>
        /// Remove steam guard from this account.
        /// </summary>
        /// <param name="scheme">Which Steam Guard method to return to.</param>
        /// <returns></returns>
        public async Task<bool> DeactivateAuthenticator(SteamGuardScheme scheme) {
            TwoFactorService twoFactorService = new TwoFactorService(null, Session?.AccessToken);
            var response = await twoFactorService.RemoveAuthenticator.Execute(RevocationCode, scheme);

            return response?.Success == true;
        }

        public string GenerateSteamGuardCode() {
            return GenerateSteamGuardCodeForTime(TimeAligner.GetSteamTime());
        }

        public async Task<string> GenerateSteamGuardCodeAsync() {
            return GenerateSteamGuardCodeForTime(await TimeAligner.GetSteamTimeAsync());
        }

        public string GenerateSteamGuardCodeForTime(long time) {
            if (string.IsNullOrEmpty(SharedSecret))
                return string.Empty;

            byte[] sharedSecretArray = Convert.FromBase64String(Regex.Unescape(SharedSecret));
            byte[] timeArray = new byte[8];

            time /= 30L;

            for (int i = 8; i > 0; i--) {
                timeArray[i - 1] = (byte)time;
                time >>= 8;
            }

            HMACSHA1 hMACSHA1 = new HMACSHA1();
            hMACSHA1.Key = sharedSecretArray;
            byte[] hashedData = hMACSHA1.ComputeHash(timeArray);
            byte[] codeArray = new byte[5];
            try {
                byte b = (byte)(hashedData[19] & 0xF);
                int codePoint = (hashedData[b] & 0x7F) << 24 | (hashedData[b + 1] & 0xFF) << 16 | (hashedData[b + 2] & 0xFF) << 8 | (hashedData[b + 3] & 0xFF);

                for (int i = 0; i < 5; ++i) {
                    codeArray[i] = steamGuardCodeTranslations[codePoint % steamGuardCodeTranslations.Length];
                    codePoint /= steamGuardCodeTranslations.Length;
                }
            } catch (Exception) {
                return string.Empty;
            }
            return Encoding.UTF8.GetString(codeArray);
        }

        public Confirmation[]? FetchConfirmations() {
            string url = GenerateConfirmationURL();
            string response = SteamWeb.GET(url, Session?.GetCookies()).Result;
            return FetchConfirmationInternal(response);
        }

        public async Task<Confirmation[]?> FetchConfirmationsAsync() {
            string url = this.GenerateConfirmationURL();
            string response = await SteamWeb.GET(url, Session?.GetCookies());
            return FetchConfirmationInternal(response);
        }

        private Confirmation[]? FetchConfirmationInternal(string response) {
            var confirmationsResponse = JsonSerializer.Deserialize<ConfirmationsResponse>(response);

            if (confirmationsResponse == null)
                throw new Exception("Failed to fetch confirmations.");

            if (!confirmationsResponse.Success) {
                throw new Exception(confirmationsResponse.Message);
            }

            if (confirmationsResponse.NeedsAuthentication) {
                throw new Exception("Failed to fetch confirmations, needs authentication");
            }

            return confirmationsResponse?.Confirmations;
        }

        public async Task<bool> AcceptMultipleConfirmations(Confirmation[] confirmations) {
            return await SendMultiConfirmationAjax(confirmations, "allow");
        }

        public async Task<bool> DenyMultipleConfirmations(Confirmation[] confirmations) {
            return await SendMultiConfirmationAjax(confirmations, "cancel");
        }

        public async Task<bool> AcceptConfirmation(Confirmation confirmation) {
            return await SendConfirmationAjax(confirmation, "allow");
        }

        public async Task<bool> DenyConfirmation(Confirmation confirmation) {
            return await SendConfirmationAjax(confirmation, "cancel");
        }

        private async Task<bool> SendConfirmationAjax(Confirmation confirmation, string op) {
            string url = Base.CommunityBase + "/mobileconf/ajaxop";
            string queryString = "?op=" + op + "&";
            // tag is different from op now
            string tag = op == "allow" ? "accept" : "reject";
            queryString += GenerateConfirmationQueryParameters(tag);
            queryString += "&cid=" + confirmation.ID + "&ck=" + confirmation.Key;
            url += queryString;

            string response = await SteamWeb.GET(url, Session?.GetCookies());
            if (response == null) return false;

            SendConfirmationResponse? confirmationResponse = JsonSerializer.Deserialize<SendConfirmationResponse>(response);
            return confirmationResponse?.Success == true;
        }

        private async Task<bool> SendMultiConfirmationAjax(Confirmation[] confirmations, string op) {
            string url = Base.CommunityBase + "/mobileconf/multiajaxop";
            // tag is different from op now
            string tag = op == "allow" ? "accept" : "reject";
            string query = "op=" + op + "&" + GenerateConfirmationQueryParameters(tag);
            foreach (var confirmation in confirmations) {
                query += "&cid[]=" + confirmation.ID + "&ck[]=" + confirmation.Key;
            }

            string response;
            using (CookieAwareWebClient wc = new CookieAwareWebClient()) {
                wc.Encoding = Encoding.UTF8;
                wc.CookieContainer = Session?.GetCookies() ?? new CookieContainer();
                wc.Headers[HttpRequestHeader.UserAgent] = SteamWeb.MOBILE_APP_USER_AGENT;
                wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded; charset=UTF-8";
                response = await wc.UploadStringTaskAsync(new Uri(url), "POST", query);
            }
            if (response == null) return false;

            SendConfirmationResponse? sendConfirmationResponse = JsonSerializer.Deserialize<SendConfirmationResponse>(response);
            return sendConfirmationResponse?.Success == true;
        }

        public string GenerateConfirmationURL(string tag = "conf") {
            string endpoint = Base.CommunityBase + "/mobileconf/getlist?";
            string queryString = GenerateConfirmationQueryParameters(tag);
            return endpoint + queryString;
        }

        public string GenerateConfirmationQueryParameters(string tag) {
            if (String.IsNullOrEmpty(DeviceID))
                throw new ArgumentException("Device ID is not present");

            var queryParameters = GenerateConfirmationQueryParametersAsNVC(tag);

            return string.Join("&", queryParameters.AllKeys.Select(key => $"{key}={queryParameters[key]}"));
        }

        public NameValueCollection GenerateConfirmationQueryParametersAsNVC(string tag) {
            if (String.IsNullOrEmpty(DeviceID))
                throw new ArgumentException("Device ID is not present");

            long time = TimeAligner.GetSteamTime();

            var ret = new NameValueCollection {
                { "p", DeviceID },
                { "a", Session?.SteamID.ToString() },
                { "k", GenerateConfirmationHashForTime(time, tag) },
                { "t", time.ToString() },
                { "m", "react" },
                { "tag", tag }
            };

            return ret;
        }

        private string GenerateConfirmationHashForTime(long time, string tag) {
            if (IdentitySecret == null)
                throw new NullReferenceException("IdentitySecret cannot be null.");

            byte[] decode = Convert.FromBase64String(IdentitySecret);
            int n2 = 8;
            if (tag != null) {
                if (tag.Length > 32) {
                    n2 = 8 + 32;
                } else {
                    n2 = 8 + tag.Length;
                }
            }
            byte[] array = new byte[n2];
            int n3 = 8;
            while (true) {
                int n4 = n3 - 1;
                if (n3 <= 0) {
                    break;
                }
                array[n4] = (byte)time;
                time >>= 8;
                n3 = n4;
            }
            if (tag != null) {
                Array.Copy(Encoding.UTF8.GetBytes(tag), 0, array, 8, n2 - 8);
            }

            try {
                HMACSHA1 hMACSHA1 = new HMACSHA1();
                hMACSHA1.Key = decode;
                byte[] hashedData = hMACSHA1.ComputeHash(array);
                string encodedData = Convert.ToBase64String(hashedData, Base64FormattingOptions.None);
                string hash = WebUtility.UrlEncode(encodedData);
                return hash;
            } catch {
                return string.Empty;
            }
        }

        public class WGTokenInvalidException : Exception {
        }

        public class WGTokenExpiredException : Exception {
        }


        private class SendConfirmationResponse {
            [JsonPropertyName("success")]
            public bool Success { get; set; }
        }

        private class ConfirmationDetailsResponse {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("html")]
            public string HTML { get; set; } = string.Empty;
        }
    }
}