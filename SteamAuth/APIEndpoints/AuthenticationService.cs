using System.Collections.Specialized;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SteamAuth.APIEndpoints.Base;

namespace SteamAuth.APIEndpoints {
    /// <summary>
    /// Steam WebAPI: /IAuthenticationService "interface"
    /// </summary>
    public class AuthenticationService : Base {
        public const string Path = "/IAuthenticationService";

        private GenerateAccessTokenForApp? _generateAccessTokenForApp;

        public GenerateAccessTokenForApp GenerateAccessTokenForApp {
            get {
                _generateAccessTokenForApp ??= new GenerateAccessTokenForApp(this);
                return _generateAccessTokenForApp;
            }
        }


        public AuthenticationService(ulong? steamId = null, string? accessToken = null, CookieContainer? cookies = null) : base(steamId, accessToken, cookies) { }
    }

    public class GenerateAccessTokenForApp {
        public const string Path = "/GenerateAccessTokenForApp/v1/";
        public const string FullPath = SteamWebAPIBase + AuthenticationService.Path + Path;

        private readonly AuthenticationService _parent;
        public GenerateAccessTokenForApp(AuthenticationService parent) {
            _parent = parent;
        }

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

            if (responseString != null)
                return JsonSerializer.Deserialize<BaseResponse<GenerateAccessTokenForAppResponse>>(responseString, JsonHelpers.Options)?.Response;

            return null;
        }
    }
}
