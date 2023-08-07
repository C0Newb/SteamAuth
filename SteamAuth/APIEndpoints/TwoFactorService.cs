using SteamAuth.Enums;
using System.Collections.Specialized;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SteamAuth.APIEndpoints.Base;
using static SteamAuth.APIEndpoints.CreateEmergencyCodes;
using static SteamAuth.APIEndpoints.RemoveAuthenticator;

namespace SteamAuth.APIEndpoints {
    /// <summary>
    /// Steam WebAPI: /ITwoFactorService "interface"
    /// </summary>
    public class TwoFactorService : Base {
        public const string Path = "/ITwoFactorService";

        private AddAuthenticator? _addAuthenticator;
        private CreateEmergencyCodes? _createEmergencyCodes;
        private DestroyEmergencyCodes? _destroyEmergencyCodes;
        private FinalizeAddAuthenticator? _finalizeAddAuthenticator;
        private QueryTime? _queryTime;
        private RemoveAuthenticator? _removeAuthenticator;
        private RemoveAuthenticatorViaChallengeStart? _removeAuthenticatorViaChallenge;
        private RemoveAuthenticatorViaChallengeContinue? _removeAuthenticatorViaChallengeContinue;
        private ValidateToken? _validateToken;

        public AddAuthenticator AddAuthenticator {
            get {
                _addAuthenticator ??= new AddAuthenticator(this);
                return _addAuthenticator;
            }
        }
        public CreateEmergencyCodes CreateEmergencyCodes {
            get {
                _createEmergencyCodes ??= new CreateEmergencyCodes(this);
                return _createEmergencyCodes;
            }
        }
        public DestroyEmergencyCodes DestroyEmergencyCodes {
            get {
                _destroyEmergencyCodes ??= new DestroyEmergencyCodes(this);
                return _destroyEmergencyCodes;
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
        public RemoveAuthenticatorViaChallengeStart RemoveAuthenticatorViaChallenge {
            get {
                _removeAuthenticatorViaChallenge ??= new RemoveAuthenticatorViaChallengeStart(this);
                return _removeAuthenticatorViaChallenge;
            }
        }
        public RemoveAuthenticatorViaChallengeContinue RemoveAuthenticatorViaChallengeContinue {
            get {
                _removeAuthenticatorViaChallengeContinue ??= new RemoveAuthenticatorViaChallengeContinue(this);
                return _removeAuthenticatorViaChallengeContinue;
            }
        }
        public ValidateToken ValidateToken {
            get {
                _validateToken ??= new ValidateToken(this);
                return _validateToken;
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
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<SteamGuardAccount>>(responseString, JsonHelpers.Options)?.Response;
        }
    }


    public class CreateEmergencyCodes {
        public const string Path = "/CreateEmergencyCodes/v1/";
        public const string FullPath = SteamWebAPIBase + TwoFactorService.Path + Path;

        private readonly TwoFactorService _parent;
        public CreateEmergencyCodes(TwoFactorService parent) => _parent = parent;

        public class CreateEmergencyCodesResponse {
            [JsonPropertyName("codes")]
            public string[]? Codes { get; set; }
        }

        /// <summary>
        /// Generates a new set of emergency codes. After you fire this the first time, you will need to call it again with the user's SMS code.
        /// </summary>
        /// <param name="smsCode"></param>
        /// <returns></returns>
        public async Task<CreateEmergencyCodesResponse?> Execute(string? smsCode = null) {
            var body = _parent.PostBody;
            if (!string.IsNullOrEmpty(smsCode))
                body.Set("code", smsCode);

            string responseString = await _parent.POST(FullPath, body);
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<CreateEmergencyCodesResponse>>(responseString, JsonHelpers.Options)?.Response;
        }
    }

    public class DestroyEmergencyCodes {
        public const string Path = "/DestroyEmergencyCodes/v1/";
        public const string FullPath = SteamWebAPIBase + TwoFactorService.Path + Path;

        private readonly TwoFactorService _parent;
        public DestroyEmergencyCodes(TwoFactorService parent) => _parent = parent;

        public class DestroyEmergencyCodesResponse {
            [JsonPropertyName("success")]
            public bool Success { get; set; }
        }
        public async Task<DestroyEmergencyCodesResponse?> Execute() {
            string responseString = await _parent.POST(FullPath);
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<DestroyEmergencyCodesResponse>>(responseString, JsonHelpers.Options)?.Response;
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
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<FinalizeAddAuthenticatorResponse>>(responseString, JsonHelpers.Options)?.Response;
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
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<QueryTimeResponse>>(responseString, JsonHelpers.Options)?.Response;
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

            [JsonPropertyName("server_time")]
            public ulong ServerTime { get; set; }

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
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<RemoveAuthenticatorResponse>>(responseString, JsonHelpers.Options)?.Response;
        }
    }



    public class RemoveAuthenticatorViaChallengeStart {
        public const string Path = "/RemoveAuthenticatorViaChallengeStart/v1/";
        public const string FullPath = SteamWebAPIBase + TwoFactorService.Path + Path;

        private readonly TwoFactorService _parent;
        public RemoveAuthenticatorViaChallengeStart(TwoFactorService parent) => _parent = parent;

        public class RemoveAuthenticatorViaChallengeStartResponse {
            /// <summary>
            /// Whether the SMS code was sent to the user.
            /// </summary>
            [JsonPropertyName("success")]
            public bool Success { get; set; } = true;
        }

        /// <summary>
        /// Begins the process of removing the mobile authenticator using an SMS code.
        /// </summary>
        /// <returns>Whether the process has kicked off or not.</returns>
        public async Task<RemoveAuthenticatorViaChallengeStartResponse?> Execute() {
            string responseString = await _parent.POST(FullPath);
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<RemoveAuthenticatorViaChallengeStartResponse>>(responseString, JsonHelpers.Options)?.Response;
        }
    }


    public class RemoveAuthenticatorViaChallengeContinue {
        public const string Path = "/RemoveAuthenticatorViaChallengeContinue/v1/";
        public const string FullPath = SteamWebAPIBase + TwoFactorService.Path + Path;

        private readonly TwoFactorService _parent;
        public RemoveAuthenticatorViaChallengeContinue(TwoFactorService parent) => _parent = parent;

        public class RemoveAuthenticatorViaChallengeContinueResponse {
            /// <summary>
            /// Whether the authenticator was successfully removed/regenerated.
            /// </summary>
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            /// <summary>
            /// If you requested a new token, this would be it. Save this as this is the new "authenticator".
            /// </summary>

            [JsonPropertyName("replacement_token")]
            public SteamGuardAccount? ReplacementToken { get; set; }
        }

        /// <summary>
        /// Removes the mobile authenticator using an SMS code. Optionally can setup a new <see cref="SteamGuardAccount"/> to take over authentication.
        /// </summary>
        /// <param name="smsCode">User supplied SMS code that was sent via <see cref="RemoveAuthenticatorViaChallengeContinue"/>.</param>
        /// <param name="generateNewToken">Whether to generate a new <see cref="SteamGuardAccount"/></param>
        /// <returns>Steam response, including whether the removal was successful and/or the new <see cref="SteamGuardAccount"/>.</returns>
        public async Task<RemoveAuthenticatorViaChallengeContinueResponse?> Execute(string smsCode, bool generateNewToken) {
            var body = _parent.PostBody;
            body.Set("sms_code", smsCode);
            body.Set("generate_new_token", generateNewToken? "1" : "0");
            //body.Set("version", ((int)steamGuardScheme).ToString());

            string responseString = await _parent.POST(FullPath, body);
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<RemoveAuthenticatorViaChallengeContinueResponse>>(responseString, JsonHelpers.Options)?.Response;
        }
    }


    public class ValidateToken {
        public const string Path = "/ValidateToken/v1/";
        public const string FullPath = SteamWebAPIBase + TwoFactorService.Path + Path;

        private readonly TwoFactorService _parent;
        public ValidateToken(TwoFactorService parent) => _parent = parent;

        public class ValidateTokenResponse {
            [JsonPropertyName("valid")]
            public bool Valid { get; set; }
        }
        public async Task<ValidateTokenResponse?> Execute(string authCode) {
            string responseString = await _parent.POST(FullPath, null, new NameValueCollection {
                { "code", authCode }
            });
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<ValidateTokenResponse>>(responseString, JsonHelpers.Options)?.Response;
        }
    }
}
