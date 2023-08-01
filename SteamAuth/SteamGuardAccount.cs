using SteamAuth.APIEndpoints;
using SteamAuth.Enums;
using System.Collections.Specialized;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SteamAuth {
    /// <summary>
    /// Represents a Steam account.
    /// Class to used to get trade/market confirmations, act on confirmations, remove the authenticator, and, of course, generate Steam Guard codes.
    /// </summary>
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
        /// Whether the mobile authenticator has been activated, connected and is the current Steam Guard method.
        /// </summary>
        [JsonPropertyName("fully_enrolled")]
        public bool FullyEnrolled { get; set; }

        /// <summary>
        /// Steam API session data
        /// </summary>
        [JsonPropertyName("Session")]
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

        /// <summary>
        /// Returns the current Steam Guard code for this account.
        /// </summary>
        /// <remarks>
        /// Calling this aligns the time with the Steam servers.
        /// </remarks>
        /// <returns>Current Steam Guard code.</returns>
        public string GenerateSteamGuardCode() {
            return GenerateSteamGuardCodeForTime(TimeAligner.GetSteamTime());
        }

        /// <inheritdoc cref="GenerateSteamGuardCode"/>
        public async Task<string> GenerateSteamGuardCodeAsync() {
            return GenerateSteamGuardCodeForTime(await TimeAligner.GetSteamTimeAsync());
        }

        /// <summary>
        /// Generates a Steam Guard code given a specific time.
        /// </summary>
        /// <param name="time">The time the code is to generate off of.</param>
        /// <returns>A Steam Guard code given a particular time.</returns>
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

            HMACSHA1 hMACSHA1 = new HMACSHA1 {
                Key = sharedSecretArray
            };
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

        /// <summary>
        /// Generates a Steam Guard code given a specific time and given a Steam Confirmation tag.
        /// </summary>
        /// <param name="time">The time the code is to generate off of.</param>
        /// <param name="tag">Confirmation tag.</param>
        /// <returns>A Steam Guard code given a particular time.</returns>
        /// <exception cref="NullReferenceException"><see cref="IdentitySecret"/> is null.</exception>
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
                HMACSHA1 hMACSHA1 = new HMACSHA1 {
                    Key = decode
                };
                byte[] hashedData = hMACSHA1.ComputeHash(array);
                string encodedData = Convert.ToBase64String(hashedData, Base64FormattingOptions.None);
                string hash = WebUtility.UrlEncode(encodedData);
                return hash;
            } catch {
                return string.Empty;
            }
        }


        #region Confirmations
        /// <summary>
        /// Retrieve an array of the current confirmations for this account.
        /// </summary>
        /// <returns>Array of confirmations for this account.</returns>
        public Confirmation[] FetchConfirmations() {
            string url = GenerateConfirmationURL();
            string response = SteamWeb.GET(url, Session?.GetCookies()).Result;
            return FetchConfirmationInternal(response);
        }

        /// <inheritdoc cref="FetchConfirmations"/>
        public async Task<Confirmation[]> FetchConfirmationsAsync() {
            string url = GenerateConfirmationURL();
            string response = await SteamWeb.GET(url, Session?.GetCookies());
            return FetchConfirmationInternal(response);
        }

        // Check if the confirmations response is valid
        private Confirmation[] FetchConfirmationInternal(string response) {
            var confirmationsResponse = JsonSerializer.Deserialize<ConfirmationsResponse>(response);

            if (confirmationsResponse == null)
                return Array.Empty<Confirmation>();

            if (!confirmationsResponse.Success)
                throw new ConfirmationFetchException(confirmationsResponse.Message);

            if (confirmationsResponse.NeedsAuthentication)
                throw new NeedsAuthenticationException();

            return confirmationsResponse?.Confirmations ?? Array.Empty<Confirmation>();
        }


        /*
         * 
         * Accept / deny
         * 
         */


        // Accept
        /// <summary>
        /// Accept a confirmation.
        /// </summary>
        /// <param name="confirmation">Confirmation to accept.</param>
        /// <returns>Whether the confirmation was accepted.</returns>
        public bool AcceptConfirmation(Confirmation confirmation) => SendConfirmationAjaxAsync(confirmation, "allow").Result;
        /// <inheritdoc cref="AcceptConfirmation"/>
        public async Task<bool> AcceptConfirmationAsync(Confirmation confirmation) => await SendConfirmationAjaxAsync(confirmation, "allow");

        /// <summary>
        /// Accept an array of confirmations.
        /// </summary>
        /// <param name="confirmations">Confirmations to accept.</param>
        /// <returns>Whether the confirmations were all accepted.</returns>
        public bool AcceptMultipleConfirmations(Confirmation[] confirmations) => SendMultipleConfirmationsAjaxAsync(confirmations, "allow").Result;
        /// <inheritdoc cref="AcceptConfirmation"/>
        public async Task<bool> AcceptMultipleConfirmationsAsync(Confirmation[] confirmations) => await SendMultipleConfirmationsAjaxAsync(confirmations, "allow");

        // Deny
        /// <summary>
        /// Deny a confirmation.
        /// </summary>
        /// <param name="confirmation">Confirmation to deny.</param>
        /// <returns>Whether the confirmation was denied.</returns>
        public bool DenyConfirmation(Confirmation confirmation) => SendConfirmationAjaxAsync(confirmation, "cancel").Result;
        /// <inheritdoc cref="DenyConfirmation"/>
        public async Task<bool> DenyConfirmationAsync(Confirmation confirmation) => await SendConfirmationAjaxAsync(confirmation, "cancel");

        /// <summary>
        /// Deny an array of confirmations.
        /// </summary>
        /// <param name="confirmations">Confirmations to deny.</param>
        /// <returns>Whether the confirmations were all denied</returns>
        public bool DenyMultipleConfirmations(Confirmation[] confirmations) => SendMultipleConfirmationsAjaxAsync(confirmations, "cancel").Result;
        /// <inheritdoc cref="DenyMultipleConfirmations"/>
        public async Task<bool> DenyMultipleConfirmationsAsync(Confirmation[] confirmations) => await SendMultipleConfirmationsAjaxAsync(confirmations, "cancel");



        #region Helpers
        private async Task<bool> SendConfirmationAjaxAsync(Confirmation confirmation, string op) {
            string url = Base.CommunityBase + "/mobileconf/ajaxop";
            string queryString = "?op=" + op + "&";
            // tag is different from op now
            string tag = op == "allow" ? "accept" : "reject";
            queryString += GenerateConfirmationQueryParameters(tag);
            queryString += "&cid=" + confirmation.Id + "&ck=" + confirmation.Key;
            url += queryString;

            string response = await SteamWeb.GET(url, Session?.GetCookies());
            if (response == null) return false;

            SendConfirmationResponse? confirmationResponse = JsonSerializer.Deserialize<SendConfirmationResponse>(response);
            return confirmationResponse?.Success == true;
        }

        private async Task<bool> SendMultipleConfirmationsAjaxAsync(Confirmation[] confirmations, string op) {
            string url = Base.CommunityBase + "/mobileconf/multiajaxop";
            // tag is different from op now
            string tag = op == "allow" ? "accept" : "reject";
            string query = "op=" + op + "&" + GenerateConfirmationQueryParameters(tag);
            foreach (var confirmation in confirmations) {
                query += "&cid[]=" + confirmation.Id + "&ck[]=" + confirmation.Key;
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

        private string GenerateConfirmationURL(string tag = "conf") {
            string endpoint = Base.CommunityBase + "/mobileconf/getlist?";
            string queryString = GenerateConfirmationQueryParameters(tag);
            return endpoint + queryString;
        }

        private string GenerateConfirmationQueryParameters(string tag) {
            if (string.IsNullOrEmpty(DeviceID))
                throw new NullReferenceException(nameof(DeviceID));

            long time = TimeAligner.GetSteamTime();
            NameValueCollection queryParameters = new NameValueCollection {
                { "p", DeviceID },
                { "a", Session?.SteamID.ToString() },
                { "k", GenerateConfirmationHashForTime(time, tag) },
                { "t", time.ToString() },
                { "m", "react" },
                { "tag", tag }
            };

            return string.Join("&", queryParameters.AllKeys.Select(key => $"{key}={queryParameters[key]}"));
        }
        #endregion

        private class SendConfirmationResponse {
            [JsonPropertyName("success")]
            public bool Success { get; set; }
        }


        #region Exceptions
        /// <summary>
        /// An error occurred while trying to retrieve a list of current account confirmations.
        /// </summary>
        [Serializable]
        public class ConfirmationFetchException : Exception {
            public ConfirmationFetchException() { }
            public ConfirmationFetchException(string message) : base(message) { }
            public ConfirmationFetchException(string message, Exception inner) : base(message, inner) { }
            protected ConfirmationFetchException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
        }

        /// <summary>
        /// Unable to retrieve a list of the current account confirmations as Steam requires authentication from this authenticator.
        /// </summary>
        [Serializable]
        public class NeedsAuthenticationException : ConfirmationFetchException {
            public NeedsAuthenticationException() { }
            public NeedsAuthenticationException(string message) : base(message) { }
            public NeedsAuthenticationException(string message, Exception inner) : base(message, inner) { }
            protected NeedsAuthenticationException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
        }
        #endregion
        #endregion // Confirmations

        #region Exceptions

        public class WGTokenInvalidException : Exception {
        }

        public class WGTokenExpiredException : Exception {
        }

        #endregion
    }
}