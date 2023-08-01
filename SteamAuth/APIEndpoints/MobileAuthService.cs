using System.Collections.Specialized;
using System.Net;
using System.Text.Json;
using static SteamAuth.APIEndpoints.Base;

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

        /// <summary>
        /// Not sure
        /// </summary>
        /// <returns></returns>
        public async Task<GetWGTokenResponse?> Execute() {
            string responseString = await _parent.POST(FullPath, null);

            if (responseString != null)
                return JsonSerializer.Deserialize<BaseResponse<GetWGTokenResponse>>(responseString, JsonHelpers.Options)?.Response;

            return null;
        }

        /// <summary>
        /// API response.
        /// </summary>
        public class GetWGTokenResponse {
            // ????
        }
    }

    public class MigrateMobileSession {
        public const string Path = "/MigrateMobileSession/v1/";
        public const string FullPath = SteamWebAPIBase + MobileAuthService.Path + Path;

        private readonly MobileAuthService _parent;
        public MigrateMobileSession(MobileAuthService parent) => _parent = parent;

        /// <summary>
        /// Not implemented. Will send the request, no idea what it'll do ! :)
        /// </summary>
        /// <param name="token"></param>
        /// <param name="signature"></param>
        /// <param name="deviceDetails"></param>
        /// <returns></returns>
        public Task<string> POST(string? token, string? signature, string deviceDetails) {
            NameValueCollection body = _parent.PostBody;
            if (token != null)
                body.Set("token", token);
            if (signature != null)
                body.Set("signature", signature);
            if (deviceDetails != null)
                body.Set("device_details", deviceDetails);

            return _parent.POST(FullPath, body); /// ????
        }
    }
}