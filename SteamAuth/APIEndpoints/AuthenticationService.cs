using SteamAuth.Enums;
using System.Collections.Specialized;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using static SteamAuth.APIEndpoints.Base;

namespace SteamAuth.APIEndpoints {
    /// <summary>
    /// Steam WebAPI: /IAuthenticationService "interface"
    /// </summary>
    public class AuthenticationService : Base {
        public const string Path = "/IAuthenticationService";

        private BeginAuthSessionViaCredentials? _beginAuthSessionViaCredentials;
        private EnumerateTokens? _enumerateTokens;
        private GenerateAccessTokenForApp? _generateAccessTokenForApp;
        private GetPasswordRSAPublicKey? _getPasswordRSAPublicKey;
        private MigrateMobileSession? _migrateMobileSession;
        private PollAuthSessionStatus? _pollAuthSessionStatus;
        private RevokeRefreshToken? _revokeRefreshToken;
        private RevokeToken? _revokeToken;
        private UpdateAuthSessionWithSteamGuardCode? _updateAuthSessionWithSteamGuardCode;

        public BeginAuthSessionViaCredentials BeginAuthSessionViaCredentials {
            get {
                _beginAuthSessionViaCredentials ??= new BeginAuthSessionViaCredentials(this);
                return _beginAuthSessionViaCredentials;
            }
        }
        public EnumerateTokens EnumerateTokens {
            get {
                _enumerateTokens ??= new EnumerateTokens(this);
                return _enumerateTokens;
            }
        }
        public GenerateAccessTokenForApp GenerateAccessTokenForApp {
            get {
                _generateAccessTokenForApp ??= new GenerateAccessTokenForApp(this);
                return _generateAccessTokenForApp;
            }
        }
        public GetPasswordRSAPublicKey GetPasswordRSAPublicKey {
            get {
                _getPasswordRSAPublicKey ??= new GetPasswordRSAPublicKey(this);
                return _getPasswordRSAPublicKey;
            }
        }
        public MigrateMobileSession MigrateMobileSession {
            get {
                _migrateMobileSession ??= new MigrateMobileSession(this);
                return _migrateMobileSession;
            }
        }
        public PollAuthSessionStatus PollAuthSessionStatus {
            get {
                _pollAuthSessionStatus ??= new PollAuthSessionStatus(this);
                return _pollAuthSessionStatus;
            }
        }
        public RevokeRefreshToken RevokeRefreshToken {
            get {
                _revokeRefreshToken ??= new RevokeRefreshToken(this);
                return _revokeRefreshToken;
            }
        }
        public RevokeToken RevokeToken {
            get {
                _revokeToken ??= new RevokeToken(this);
                return _revokeToken;
            }
        }
        public UpdateAuthSessionWithSteamGuardCode UpdateAuthSessionWithSteamGuardCode {
            get {
                _updateAuthSessionWithSteamGuardCode ??= new UpdateAuthSessionWithSteamGuardCode(this);
                return _updateAuthSessionWithSteamGuardCode;
            }
        }



        internal async Task<string> GenerateSignatureAsync(string dataToSign) {
            MobileAuthService mobileAuthService = new MobileAuthService(SteamId, AccessToken);
            var wgTokens = await mobileAuthService.GetWGToken.Execute();
            byte[] steamGuardCodeBytes = Encoding.UTF8.GetBytes(dataToSign ?? string.Empty);
            byte[] wgTokenBytes = Encoding.UTF8.GetBytes(wgTokens?.Token ?? string.Empty);

            string signature = "";
            using (var hmac = new HMACSHA1(wgTokenBytes)) {
                byte[] hashBytes = hmac.ComputeHash(steamGuardCodeBytes);
                signature = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
            return signature;
        }

        internal string GenerateSignature(string dataToSign) {
            var task = GenerateSignatureAsync(dataToSign);
            task.Wait();
            return task.Result;
        }

        public AuthenticationService(ulong? steamId = null, string? accessToken = null, CookieContainer? cookies = null) : base(steamId, accessToken, cookies) { }
    }

    public class BeginAuthSessionViaCredentials {
        public const string Path = "/BeginAuthSessionViaCredentials/v1/";
        public const string FullPath = SteamWebAPIBase + AuthenticationService.Path + Path;

        private readonly AuthenticationService _parent;
        public BeginAuthSessionViaCredentials(AuthenticationService parent) => _parent = parent;

        public class BeginAuthSessionViaCredentialsResponse {
            [JsonPropertyName("agreement_session_url")]
            public string? AgreementSessionURL { get; set; }

            [JsonPropertyName("allowed_confirmations")]
            public AllowedConfirmation[]? AllowedConfirmations { get; set; }

            [JsonPropertyName("client_id")]
            public ulong ClientId { get; set; }

            [JsonPropertyName("extended_error_message")]
            public string ExtendedErrorMessage { get; set; } = string.Empty;

            [JsonPropertyName("interval")]
            public int Interval { get; set; }

            [JsonPropertyName("request_id")]
            public string? RequestId { get; set; }

            [JsonPropertyName("steamid")]
            public ulong SteamId { get; set; }

            [JsonPropertyName("weak_token")]
            public string? WeakToken { get; set; }
        }

        public class AllowedConfirmation {
            [JsonPropertyName("associated_message")]
            public string? AssociatedMessage { get; set; }

            [JsonPropertyName("confirmation_type")]
            public ConfirmationType ConfirmationType { get; set; }
        }

        public async Task<BeginAuthSessionViaCredentialsResponse?> Execute(string username, string password, bool rememberLogin) {
            // need to encrypt the password
            string encryptedPassword = "";
            var publicKey = await (new GetPasswordRSAPublicKey(_parent)).Execute(username).ConfigureAwait(false);
            using (var rsa = new RSACryptoServiceProvider()) {
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var rsaParameters = new RSAParameters {
                    Modulus = Util.HexStringToByteArray(publicKey?.PublicKeyModulus ?? string.Empty),
                    Exponent = Util.HexStringToByteArray(publicKey?.PublicKeyExponent ?? string.Empty),
                };
                rsa.ImportParameters(rsaParameters);
                var encryptedPasswordBytes = rsa.Encrypt(passwordBytes, RSAEncryptionPadding.Pkcs1);
                encryptedPassword = Convert.ToBase64String(encryptedPasswordBytes);
            }

            NameValueCollection body = _parent.PostBody;
            body.Set("account_name", username);
            body.Set("persistence", rememberLogin? "1" : "0");
            body.Set("website_id", "Mobile");
            //body.Set("guard_data", "");
            body.Set("encrypted_password", encryptedPassword);
            body.Set("encryption_timestamp", publicKey?.Timestamp.ToString());

            JsonObject deviceDetails = new JsonObject {
                ["device_friendly_name"] = $"{Environment.MachineName} (SteamAuth)",
                ["platform_type"] = "3",
                ["os_type"] = "-496"
            };
            body.Set("device_details", deviceDetails.ToJsonString());

            string responseString = await _parent.POST(FullPath, body);
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<BeginAuthSessionViaCredentialsResponse>>(responseString, JsonHelpers.Options)?.Response;
        }
    }

    /*public class BeginAuthSessionViaQR {
        public const string Path = "/BeginAuthSessionViaQR/v1/";
        public const string FullPath = SteamWebAPIBase + AuthenticationService.Path + Path;

        private readonly AuthenticationService _parent;
        public BeginAuthSessionViaQR(AuthenticationService parent) => _parent = parent;
    }*/


    public class GenerateAccessTokenForApp {
        public const string Path = "/GenerateAccessTokenForApp/v1/";
        public const string FullPath = SteamWebAPIBase + AuthenticationService.Path + Path;

        private readonly AuthenticationService _parent;
        public GenerateAccessTokenForApp(AuthenticationService parent) => _parent = parent;

        public class GenerateAccessTokenForAppResponse {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }
        }

        /// <summary>
        /// Regenerates the user's access token using their refresh token.
        /// </summary>
        /// <param name="refreshToken">User's session refresh token, <see cref="SteamGuardAccount.SessionData.RefreshToken"/>.</param>
        /// <returns>New access token for the user.</returns>
        public async Task<GenerateAccessTokenForAppResponse?> Execute(string refreshToken) {
            NameValueCollection body = _parent.PostBody;
            body.Set("refresh_token", refreshToken);

            string responseString = await _parent.POST(FullPath, body);
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<GenerateAccessTokenForAppResponse>>(responseString, JsonHelpers.Options)?.Response;
        }
    }

    public class EnumerateTokens {
        public const string Path = "/EnumerateTokens/v1/";
        public const string FullPath = SteamWebAPIBase + AuthenticationService.Path + Path;

        private readonly AuthenticationService _parent;
        public EnumerateTokens(AuthenticationService parent) => _parent = parent;

        public class EnumerateTokensResponse {
            [JsonPropertyName("refresh_tokens")]
            public RefreshTokenInfo[] RefreshTokens { get; set; } = Array.Empty<RefreshTokenInfo>();
        }

        /// <summary>
        /// Obtain a list of active refresh tokens.
        /// </summary>
        /// <returns>List of active refresh tokens (sessions).</returns>
        public async Task<EnumerateTokensResponse?> Execute() {
            string responseString = await _parent.POST(FullPath);
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<EnumerateTokensResponse>>(responseString, JsonHelpers.Options)?.Response ?? new EnumerateTokensResponse();
        }
    }

    public class GetPasswordRSAPublicKey {
        public const string Path = "/GetPasswordRSAPublicKey/v1/";
        public const string FullPath = SteamWebAPIBase + AuthenticationService.Path + Path;

        private readonly AuthenticationService _parent;
        public GetPasswordRSAPublicKey(AuthenticationService parent) => _parent = parent;

        public class GetPasswordRSAPublicKeyResponse {
            [JsonPropertyName("publickey_mod")]
            public string? PublicKeyModulus { get; set; }

            [JsonPropertyName("publickey_exp")]
            public string? PublicKeyExponent { get; set; }

            [JsonPropertyName("timestamp")]
            public ulong Timestamp { get; set; }
        }

        /// <summary>
        /// Retrieves the RSA key used to encrypt the user's password before sending it to Steam.
        /// </summary>
        /// <param name="accountName">Which RSA key to obtain.</param>
        /// <returns>RSA details for the account.</returns>
        public async Task<GetPasswordRSAPublicKeyResponse?> Execute(string accountName) {
            var parameters = new NameValueCollection {
                { "account_name", accountName }
            };

            string responseString = await _parent.GET(FullPath, parameters);
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<GetPasswordRSAPublicKeyResponse>>(responseString, JsonHelpers.Options)?.Response;
        }
    }

    public class MigrateMobileSession {
        public const string Path = "/MigrateMobileSession/v1/";
        public const string FullPath = SteamWebAPIBase + MobileAuthService.Path + Path;

        private readonly AuthenticationService _parent;
        public MigrateMobileSession(AuthenticationService parent) => _parent = parent;

        public class MigrateMobileSessionResponse {
            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }
        }

        /// <summary>
        /// Not implemented. Will send the request, no idea what it'll do ! :)
        /// </summary>
        /// <param name="token"></param>
        /// <param name="signature"></param>
        /// <param name="deviceDetails"></param>
        /// <returns></returns>
        public async Task<MigrateMobileSessionResponse?> Execute(string code) {
            NameValueCollection body = _parent.PostBody;
            if (code != null)
                body.Set("token", code);

            string signature = _parent.GenerateSignature(code ?? string.Empty);
            if (signature != null)
                body.Set("signature", signature);

            MobileAuthService mobileAuthService = new MobileAuthService(_parent.SteamId, _parent.AccessToken);
            var wgTokens = await mobileAuthService.GetWGToken.Execute();
            body.Set("token", wgTokens?.Token);

            JsonObject deviceDetails = new JsonObject {
                ["device_friendly_name"] = $"{Environment.MachineName} (SteamAuth)",
                ["platform_type"] = "3",
                ["os_type"] = "-496"
            };
            body.Set("device_details", deviceDetails.ToJsonString());

            string responseString = await _parent.POST(FullPath, body, new NameValueCollection {
                { "token", wgTokens?.Token},
                { "signature", signature }
            }); /// ????
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<MigrateMobileSessionResponse>>(responseString, JsonHelpers.Options)?.Response;
        }
    }


    public class PollAuthSessionStatus {
        public const string Path = "/PollAuthSessionStatus/v1/";
        public const string FullPath = SteamWebAPIBase + AuthenticationService.Path + Path;

        private readonly AuthenticationService _parent;
        public PollAuthSessionStatus(AuthenticationService parent) => _parent = parent;

        public class PollAuthSessionStatusResponse {
            [JsonPropertyName("new_client_id")]
            public ulong NewClientId { get; set; }

            [JsonPropertyName("new_challenge_url")]
            public string? NewChallengeURL { get; set; }

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }

            [JsonPropertyName("had_remote_interaction")]
            public string? HadRemoteInteraction { get; set; }

            [JsonPropertyName("account_name")]
            public string? AccountName { get; set; }

            [JsonPropertyName("new_guard_data")]
            public string? NewGuardData { get; set; }

            [JsonPropertyName("AgreementSessionURL")]
            public string? AgreementSessionURL { get; set; }
        }

        /// <summary>
        /// Polls for the status of the authentication once.
        /// </summary>
        /// <param name="clientId">The client id returned by <see cref="BeginAuthSessionViaCredentials"/>.</param>
        /// <param name="requestId">Request id returned by <see cref="BeginAuthSessionViaCredentials"/>.</param>
        /// <param name="tokenToRevoke">If this is set to a token owned by this user, that token will be retired.</param>
        /// <returns>Authentication session status.</returns>
        public async Task<PollAuthSessionStatusResponse?> Execute(ulong clientId, string requestId, string? tokenToRevoke = null) {
            var body = _parent.PostBody;
            body.Set("client_id", clientId.ToString());
            body.Set("request_id", requestId);
            if (!string.IsNullOrEmpty(tokenToRevoke))
                body.Set("token_to_revoke", tokenToRevoke);

            string responseString = await _parent.POST(FullPath, body, new NameValueCollection {
                { "client_id", clientId.ToString() },
                { "request_id", requestId },
            });
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<PollAuthSessionStatusResponse>>(responseString, JsonHelpers.Options)?.Response;
        }
    }

    public class RevokeRefreshToken {
        public const string Path = "/RevokeToken/v1/";
        public const string FullPath = SteamWebAPIBase + AuthenticationService.Path + Path;

        private readonly AuthenticationService _parent;
        public RevokeRefreshToken(AuthenticationService parent) => _parent = parent;

        public class RevokeRefreshTokenResponse {

        }

        public async Task<RevokeRefreshTokenResponse?> Execute(string token, TokenRevokeAction revokeAction = TokenRevokeAction.Logout) {
            string signature = _parent.GenerateSignature(token);
            var body = _parent.PostBody;
            body.Set("token", token);
            body.Set("revoke_action", ((int)revokeAction).ToString());
            body.Set("signature", signature);

            string responseString = await _parent.POST(FullPath, body, new NameValueCollection {
                { "token", token },
                { "signature", signature }
            });
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<RevokeRefreshTokenResponse>>(responseString, JsonHelpers.Options)?.Response;
        }
    }

    public class RevokeToken {
        public const string Path = "/RevokeToken/v1/";
        public const string FullPath = SteamWebAPIBase + AuthenticationService.Path + Path;

        private readonly AuthenticationService _parent;
        public RevokeToken(AuthenticationService parent) => _parent = parent;

        public class RevokeTokenResponse {

        }

        public async Task<RevokeTokenResponse?> Execute(string token, TokenRevokeAction revokeAction = TokenRevokeAction.Logout) {
            var body = _parent.PostBody;
            body.Set("token", token);
            body.Set("revoke_action", ((int)revokeAction).ToString(token));

            string responseString = await _parent.POST(FullPath, body);
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<RevokeTokenResponse>>(responseString, JsonHelpers.Options)?.Response;
        }
    }

    public class UpdateAuthSessionWithSteamGuardCode {
        public const string Path = "/UpdateAuthSessionWithSteamGuardCodeRequest/v1/";
        public const string FullPath = SteamWebAPIBase + AuthenticationService.Path + Path;

        private readonly AuthenticationService _parent;
        public UpdateAuthSessionWithSteamGuardCode(AuthenticationService parent) => _parent = parent;

        public class UpdateAuthSessionWithSteamGuardCodeResponse {
            [JsonPropertyName("agreement_session_url")]
            public string? AgreementSessionURL { get; set; }
        }

        public async Task<UpdateAuthSessionWithSteamGuardCodeResponse?> Execute(ulong clientId, string steamGuardCode, ConfirmationType confirmationType) {
            var body = _parent.PostBody;
            body.Set("client_id", clientId.ToString());
            body.Set("code", steamGuardCode);
            body.Set("code_type", ((int)confirmationType).ToString());

            string responseString = await _parent.POST(FullPath, body);
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<UpdateAuthSessionWithSteamGuardCodeResponse>>(responseString, JsonHelpers.Options)?.Response;
        }
    }
}