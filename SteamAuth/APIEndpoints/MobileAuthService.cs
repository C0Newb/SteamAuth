using System.Collections.Specialized;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using static SteamAuth.APIEndpoints.Base;
using static SteamKit2.GC.CSGO.Internal.CProductInfo_SetRichPresenceLocalization_Request;

namespace SteamAuth.APIEndpoints {
    /// <summary>
    /// Steam Web API: /IMobileAuthService "interface"
    /// </summary>
    public class MobileAuthService : Base {
        public const string Path = "/IMobileAuthService";

        private GetWGToken? _getWGToken;

        public GetWGToken GetWGToken {
            get {
                _getWGToken ??= new GetWGToken(this);
                return _getWGToken;
            }
        }

        public MobileAuthService(ulong? steamId = null, string? accessToken = null, CookieContainer? cookies = null) : base(steamId, accessToken, cookies) { }
    }

    /// <summary>
    /// Gets the current server time
    /// </summary>
    public class GetWGToken {
        public const string Path = "/GetWGToken/v1/";
        public const string FullPath = SteamWebAPIBase + MobileAuthService.Path + Path;

        private readonly MobileAuthService _parent;
        public GetWGToken(MobileAuthService mobileAuthService) => _parent = mobileAuthService;

        public class GetWGTokenResponse {
            [JsonPropertyName("token")]
            public string? Token { get; set; }

            [JsonPropertyName("token_secure")]
            public string? TokenSecure { get; set; }
        }

        /// <summary>
        /// Not sure
        /// </summary>
        /// <returns></returns>
        public async Task<GetWGTokenResponse?> Execute() {
            string responseString = await _parent.POST(FullPath);
            if (string.IsNullOrEmpty(responseString)) return null;
            return JsonSerializer.Deserialize<BaseResponse<GetWGTokenResponse>>(responseString, JsonHelpers.Options)?.Response;
        }

        public GetWGTokenResponse? GetTokens() {
            var task = Execute();
            task.Wait();
            return task.Result;
        }
    }
}