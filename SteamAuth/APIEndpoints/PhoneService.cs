using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SteamAuth.APIEndpoints.Base;

namespace SteamAuth.APIEndpoints {
    /// <summary>
    /// Steam WebAPI: /IPhoneService "interface"
    /// </summary>
    public class PhoneService : Base {
        public const string Path = "/IPhoneService";

        private ConfirmAddPhoneToAccount? _confirmAddPhoneToAccount;
        private IsAccountWaitingForEmailConfirmation? _isAccountWaitingForEmailConfirmation;
        private SendPhoneVerificationCode? _sendPhoneVerificationCode;
        private SetAccountPhoneNumber? _setAccountPhoneNumber;
        private VerifyAccountPhoneWithCode? _verifyAccountPhoneWithCode;

        public ConfirmAddPhoneToAccount ConfirmAddPhoneToAccount {
            get {
                _confirmAddPhoneToAccount ??= new ConfirmAddPhoneToAccount(this);
                return _confirmAddPhoneToAccount;
            }
        }
        public IsAccountWaitingForEmailConfirmation IsAccountWaitingForEmailConfirmation {
            get {
                _isAccountWaitingForEmailConfirmation ??= new IsAccountWaitingForEmailConfirmation(this);
                return _isAccountWaitingForEmailConfirmation;
            }
        }
        public SendPhoneVerificationCode SendPhoneVerificationCode {
            get {
                _sendPhoneVerificationCode ??= new SendPhoneVerificationCode(this);
                return _sendPhoneVerificationCode;
            }
        }
        public SetAccountPhoneNumber SetAccountPhoneNumber {
            get {
                _setAccountPhoneNumber ??= new SetAccountPhoneNumber(this);
                return _setAccountPhoneNumber;
            }
        }
        public VerifyAccountPhoneWithCode VerifyAccountPhoneWithCode {
            get {
                _verifyAccountPhoneWithCode ??= new VerifyAccountPhoneWithCode(this);
                return _verifyAccountPhoneWithCode;
            }
        }


        public PhoneService(ulong? steamId = null, string? accessToken = null, CookieContainer? cookies = null) : base(steamId, accessToken, cookies) { }
    }

    public class ConfirmAddPhoneToAccount {
        public const string Path = "/SetAccountPhoneNumber/v1/";
        public const string FullPath = SteamWebAPIBase + PhoneService.Path + Path;

        private readonly PhoneService _parent;
        public ConfirmAddPhoneToAccount(PhoneService parent) => _parent = parent;

        public class ConfirmAddPhoneToAccountResponse {
        }

        public Task<ConfirmAddPhoneToAccountResponse?> Execute() {
            throw new NotImplementedException();
        }
    }

    public class IsAccountWaitingForEmailConfirmation {
        public const string Path = "/IsAccountWaitingForEmailConfirmation/v1/";
        public const string FullPath = SteamWebAPIBase + PhoneService.Path + Path;

        private readonly PhoneService _parent;
        public IsAccountWaitingForEmailConfirmation(PhoneService phoneService) => _parent = phoneService;

        public class IsAccountWaitingForEmailConfirmationResponse {
            [JsonPropertyName("awaiting_email_confirmation")]
            public bool AwaitingEmailConfirmation { get; set; }

            [JsonPropertyName("seconds_to_wait")]
            public int SecondsToWait { get; set; }
        }

        /// <summary>
        /// Checks whether the account is waiting for an email confirmation code
        /// </summary>
        /// <returns>If this account is waiting for an email confirmation.</returns>
        public async Task<IsAccountWaitingForEmailConfirmationResponse?> Execute() {
            string responseString = await _parent.POST(FullPath);

            if (responseString != null)
                return JsonSerializer.Deserialize<BaseResponse<IsAccountWaitingForEmailConfirmationResponse>>(responseString, JsonHelpers.Options)?.Response;

            return null;
        }
    }


    public class SendPhoneVerificationCode {
        public const string Path = "/SendPhoneVerificationCode/v1/";
        public const string FullPath = SteamWebAPIBase + PhoneService.Path + Path;

        private readonly PhoneService _parent;
        public SendPhoneVerificationCode(PhoneService parent) => _parent = parent;

        /// <summary>
        /// Send a SMS verification code to the user's registered phone number
        /// </summary>
        /// <returns><see langword="true" /></returns>
        public async Task<bool> Execute() {
            await _parent.POST(FullPath);
            return true;
        }
    }

    public class SetAccountPhoneNumber {
        public const string Path = "/SetAccountPhoneNumber/v1/";
        public const string FullPath = SteamWebAPIBase + PhoneService.Path + Path;

        private readonly PhoneService _parent;
        public SetAccountPhoneNumber(PhoneService phoneService) => _parent = phoneService;

        public class SetAccountPhoneNumberResponse {
            [JsonPropertyName("confirmation_email_address")]
            public string? ConfirmationEmailAddress { get; set; }

            [JsonPropertyName("phone_number_formatted")]
            public string? PhoneNumberFormatted { get; set; }
        }

        /// <summary>
        /// Registers the account's authenticator phone number.
        /// </summary>
        /// <param name="phoneNumber">Phone number.</param>
        /// <param name="countryCode">Phone number country code.</param>
        /// <returns>Steam's response.</returns>
        public async Task<SetAccountPhoneNumberResponse?> Execute(string phoneNumber, string countryCode) {
            var body = _parent.PostBody;
            body.Set("phone_number", phoneNumber);
            body.Set("phone_country_code", countryCode);

            string responseString = await _parent.POST(FullPath, body);

            if (!string.IsNullOrEmpty(responseString))
                return JsonSerializer.Deserialize<BaseResponse<SetAccountPhoneNumberResponse>>(responseString, JsonHelpers.Options)?.Response;

            return null;
        }
    }

    public class VerifyAccountPhoneWithCode {
        public const string Path = "/VerifyAccountPhoneWithCode/v1/";
        public const string FullPath = SteamWebAPIBase + PhoneService.Path + Path;

        private readonly PhoneService _parent;
        public VerifyAccountPhoneWithCode(PhoneService parent) => _parent = parent;

        public class VerifyAccountPhoneWithCodeResponse {
        }

        public Task<VerifyAccountPhoneWithCodeResponse?> Execute() {
            throw new NotImplementedException();
        }
    }
}
