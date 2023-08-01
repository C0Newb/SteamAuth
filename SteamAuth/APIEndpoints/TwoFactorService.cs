using SteamAuth.Enums;
using System.Collections.Specialized;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SteamAuth.APIEndpoints.Base;

namespace SteamAuth.APIEndpoints {
    /// <summary>
    /// Steam WebAPI: /ITwoFactorService "interface"
    /// </summary>
    public class TwoFactorService : Base {
        public const string Path = "/ITwoFactorService";

        private AddAuthenticator? _addAuthenticator;
        private FinalizeAddAuthenticator? _finalizeAddAuthenticator;
        private QueryTime? _queryTime;
        private RemoveAuthenticator? _removeAuthenticator;

        public AddAuthenticator AddAuthenticator {
            get {
                _addAuthenticator ??= new AddAuthenticator(this);
                return _addAuthenticator;
            }
        }
        public FinalizeAddAuthenticator FinalizeAddAuthenticator {
            get {
                _finalizeAddAuthenticator ??= new FinalizeAddAuthenticator(this);
                return _finalizeAddAuthenticator;
            }
        }
        public QueryTime QueryTime {
            get {
                _queryTime ??= new QueryTime(this);
                return _queryTime;
            }
        }
        public RemoveAuthenticator RemoveAuthenticator {
            get {
                _removeAuthenticator ??= new RemoveAuthenticator(this);
                return _removeAuthenticator;
            }
        }


        public TwoFactorService(ulong? steamId = null, string? accessToken = null, CookieContainer? cookies = null) : base(steamId, accessToken, cookies) { }
    }

    public class AddAuthenticator {
        public const string Path = "/AddAuthenticator/v1/";
        public const string FullPath = SteamWebAPIBase + TwoFactorService.Path + Path;

        private readonly TwoFactorService _parent;
        public AddAuthenticator(TwoFactorService parent) => _parent = parent;

        /// <summary>
        /// Adds an authenticator to the user's account.
        /// </summary>
        /// <param name="deviceId">Your simulated device id, must be unique!</param>
        /// <param name="smsPhoneId">Set to 1.</param>
        /// <param name="authenticatorType">Set to 1.</param>
        /// <returns>A <see cref="SteamGuardAccount"/>, the user's account.</returns>
        public async Task<SteamGuardAccount?> Execute(string deviceId, string smsPhoneId = "1", int authenticatorType = 1) {
            NameValueCollection body = _parent.PostBody;
            body.Set("authenticator_time", (await TimeAligner.GetSteamTimeAsync()).ToString());
            body.Set("authenticator_type", authenticatorType.ToString());
            body.Set("device_identifier", deviceId);
            body.Set("sms_phone_id", smsPhoneId);

            string responseString = await _parent.POST(FullPath, body);

            if (responseString != null)
                return JsonSerializer.Deserialize<BaseResponse<SteamGuardAccount>>(responseString, JsonHelpers.Options)?.Response;

            return null;
        }
    }


    public class FinalizeAddAuthenticator {
        public const string Path = "/FinalizeAddAuthenticator/v1/";
        public const string FullPath = SteamWebAPIBase + TwoFactorService.Path + Path;

        private readonly TwoFactorService _parent;
        public FinalizeAddAuthenticator(TwoFactorService parent) => _parent = parent;

        public class FinalizeAddAuthenticatorResponse {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("want_more")]
            public bool WantMore { get; set; }

            [JsonPropertyName("server_time")]
            public long ServerTime { get; set; }

            [JsonPropertyName("status")]
            public int Status { get; set; }
        }

        /// <summary>
        /// Switches the Steam Guard method to your added mobile authenticator.
        /// </summary>
        /// <param name="authenticatorCode">Generated Steam Guard code.</param>
        /// <param name="smsCode">SMS verification code texted to the user.</param>
        /// <param name="validateSMSCode">Set to 1.</param>
        /// <returns>Steam's response.</returns>
        public async Task<FinalizeAddAuthenticatorResponse?> Execute(string authenticatorCode, string smsCode, string validateSMSCode = "1") {
            NameValueCollection body = _parent.PostBody;
            body.Set("authenticator_time", (await TimeAligner.GetSteamTimeAsync()).ToString());
            body.Set("authenticator_code", authenticatorCode);
            body.Set("activation_code", smsCode);
            body.Set("sms_phone_id", validateSMSCode);

            string responseString = await _parent.POST(FullPath, body);

            if (responseString != null)
                return JsonSerializer.Deserialize<BaseResponse<FinalizeAddAuthenticatorResponse>>(responseString, JsonHelpers.Options)?.Response;

            return null;
        }
    }

    public class QueryTime {
        public const string Path = "/QueryTime/v1/";
        public const string FullPath = SteamWebAPIBase + TwoFactorService.Path + Path;

        private readonly TwoFactorService _parent;
        public QueryTime(TwoFactorService parent) => _parent = parent;

        public class QueryTimeResponse {
            [JsonPropertyName("server_time")]
            public long ServerTime { get; set; }

            [JsonPropertyName("skew_tolerance_seconds")]
            public int SkewToleranceSeconds { get; set; }

            [JsonPropertyName("large_time_jink")]
            public int LargeTimeJink { get; set; }

            [JsonPropertyName("probe_frequency_seconds")]
            public int ProbeFrequencySeconds { get; set; }

            [JsonPropertyName("adjusted_time_probe_frequency_seconds")]
            public int AdjustedTimeProbeFrequencySeconds { get; set; }

            [JsonPropertyName("hint_probe_frequency_seconds")]
            public int HintProbeFrequencySeconds { get; set; }

            [JsonPropertyName("sync_timeout")]
            public int SyncTimeout { get; set; }

            [JsonPropertyName("try_again_seconds")]
            public int TryAgainSeconds { get; set; }

            [JsonPropertyName("max_attempts")]
            public int MaxAttempts { get; set; }
        }

        /// <summary>
        /// Returns Steam's server time.
        /// </summary>
        /// <returns>Steam's time.</returns>
        public async Task<QueryTimeResponse?> Execute() {
            string responseString = await _parent.POST(FullPath);

            if (responseString != null)
                return JsonSerializer.Deserialize<BaseResponse<QueryTimeResponse>>(responseString, JsonHelpers.Options)?.Response;

            return null;
        }
    }


    public class RemoveAuthenticator {
        public const string Path = "/RemoveAuthenticator/v1";
        public const string FullPath = SteamWebAPIBase + TwoFactorService.Path + Path;

        private readonly TwoFactorService _parent;
        public RemoveAuthenticator(TwoFactorService parent) => _parent = parent;

        public class RemoveAuthenticatorResponse {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("revocation_attempts_remaining")]
            public int RevocationAttemptsRemaining { get; set; }
        }

        /// <summary>
        /// Removes the mobile authenticator from the user's account.
        /// </summary>
        /// <param name="revocationCode">Steam Guard revocation code.</param>
        /// <param name="steamGuardScheme">Which Steam Guard method to switch back to.</param>
        /// <param name="removeAllSteamGuardCookies">I believe this revokes all active sessions.</param>
        /// <param name="revocationReason">Set to 1.</param>
        /// <returns>Steam's response.</returns>
        public async Task<RemoveAuthenticatorResponse?> Execute(string revocationCode, SteamGuardScheme steamGuardScheme = SteamGuardScheme.ReturnToEmail, bool removeAllSteamGuardCookies = false, string revocationReason = "1") {
            var body = _parent.PostBody;
            body.Set("revocation_code", revocationCode);
            body.Set("revocation_reason", revocationReason);
            body.Set("steamguard_scheme", ((int)steamGuardScheme).ToString());
            body.Set("remove_all_steamguard_cookies", removeAllSteamGuardCookies.ToString());

            string responseString = await _parent.POST(FullPath, body);

            if (responseString != null)
                return JsonSerializer.Deserialize<BaseResponse<RemoveAuthenticatorResponse>>(responseString, JsonHelpers.Options)?.Response;

            return null;
        }
    }
}
