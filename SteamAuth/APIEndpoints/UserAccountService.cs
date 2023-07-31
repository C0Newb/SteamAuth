using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SteamAuth.APIEndpoints.FinalizeAddAuthenticator;

namespace SteamAuth.APIEndpoints {
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
        public const string FullPath = Base.SteamWebAPIBase + UserAccountService.Path + Path;

        private readonly UserAccountService _parent;
        public GetUserCountry(UserAccountService parent) => _parent = parent;

        public class GetUserCountryResponse {
            [JsonPropertyName("country")]
            public string Country { get; set; } = string.Empty;
        }

        public async Task<GetUserCountryResponse?> Execute() {
            string responseString = await _parent.POST(FullPath);

            if (responseString != null)
                return JsonSerializer.Deserialize<Base.BaseResponse<GetUserCountryResponse>>(responseString, JsonHelpers.Options)?.Response;
            return null;
        }
    }
}
