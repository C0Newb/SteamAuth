using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SteamAuth.APIEndpoints.Base;

namespace SteamAuth.APIEndpoints {
    /// <summary>
    /// Steam WebAPI: /IUserAccountService "interface"
    /// </summary>
    public class UserAccountService : Base {
        public const string Path = "/IUserAccountService";

        private GetUserCountry? _getUserCountry;

        public GetUserCountry GetUserCountry {
            get {
                _getUserCountry ??= new GetUserCountry(this);
                return _getUserCountry;
            }
        }

        public UserAccountService(ulong? steamId = null, string? accessToken = null, CookieContainer? cookies = null) : base(steamId, accessToken, cookies) { }
    }

    public class GetUserCountry {
        public const string Path = "/GetUserCountry/v1/";
        public const string FullPath = SteamWebAPIBase + UserAccountService.Path + Path;

        private readonly UserAccountService _parent;
        public GetUserCountry(UserAccountService parent) => _parent = parent;

        public class GetUserCountryResponse {
            [JsonPropertyName("country")]
            public string Country { get; set; } = string.Empty;
        }

        /// <summary>
        /// Obtains the user's set (two digit?) country code.
        /// </summary>
        /// <returns>User's country code.</returns>
        public async Task<GetUserCountryResponse?> Execute() {
            string responseString = await _parent.POST(FullPath);

            if (responseString != null)
                return JsonSerializer.Deserialize<BaseResponse<GetUserCountryResponse>>(responseString, JsonHelpers.Options)?.Response;
            return null;
        }
    }
}
