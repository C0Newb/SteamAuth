using SteamAuth.APIEndpoints;
using System.ComponentModel;
using static SteamAuth.APIEndpoints.SetAccountPhoneNumber;

namespace SteamAuth {
    /// <summary>
    /// Handles the linking process for a new mobile authenticator.
    /// </summary>
    public class AuthenticatorLinker {
        #region Properties
        /// <summary>
        /// Session data containing an access token for a steam account generated with k_EAuthTokenPlatformType_MobileApp
        /// </summary>
        private readonly SessionData _session;

        #region Linking details
        /// <summary>
        /// Phone number country code used in linking.
        /// </summary>
        public string PhoneCountryCode = string.Empty;

        /// <summary>
        /// Set to register a new phone number when linking.
        /// If a phone number is not set on the account, this must be set. If a phone number is set on the account, this must be null.
        /// </summary>
        public string PhoneNumber = string.Empty;


        /// <summary>
        /// Randomly-generated device Id\. Should only be generated once per linker.
        /// </summary>
        public string DeviceID { get; private set; }


        /// <summary>
        /// Email address the confirmation was sent to
        /// </summary>
        public string ConfirmationEmailAddress { get; private set; } = string.Empty;

        /// <summary>
        /// Whether the confirmation email was sent to authorize linking
        /// </summary>
        public bool ConfirmationEmailSent { get; private set; } = false;
        #endregion

        /// <summary>
        /// After the initial link step, if successful, this will be the SteamGuard data for the account. PLEASE save this somewhere after generating it; it's vital data.
        /// </summary>
        public SteamGuardAccount? LinkedAccount { get; private set; }

        /// <summary>
        /// True if the authenticator has been fully finalized.
        /// </summary>
        public bool Finalized = false;
        #endregion


        private readonly TwoFactorService TwoFactorService;
        private readonly PhoneService PhoneService;


        /// <summary>
        /// Create a new instance of AuthenticatorLinker
        /// </summary>
        /// <param name="accessToken">Access token for a Steam account created with k_EAuthTokenPlatformType_MobileApp</param>
        /// <param name="steamid">64 bit formatted steamid for the account</param>
        public AuthenticatorLinker(SessionData sessionData) {
            _session = sessionData;
            DeviceID = GenerateDeviceID();
            TwoFactorService = new TwoFactorService(sessionData.SteamId, sessionData.AccessToken);
            PhoneService = new PhoneService(_session.SteamId, _session.AccessToken);
        }

        /// <summary>
        /// Generates a random "device id" in the form of <c>android:00000000-0000-0000-0000-000000000000</c>
        /// </summary>
        /// <returns></returns>
        public static string GenerateDeviceID() {
            return "android:" + Guid.NewGuid().ToString();
        }

        /// <summary>
        /// First step in adding a mobile authenticator to an account
        /// </summary>
        public async Task<LinkResult> AddAuthenticator() {
            // This method will be called again once the user confirms their phone number email
            if (ConfirmationEmailSent) {
                // Check if email was confirmed
                bool isStillWaiting = await IsAccountWaitingForEmailConfirmation();
                if (isStillWaiting) {
                    return LinkResult.MustConfirmEmail;
                } else {
                    // Now send the SMS to the phone number
                    await SendPhoneVerificationCode();
                    await Task.Delay(1500);
                }
            }

            // Make request to ITwoFactorService/AddAuthenticator
            var addAuthenticatorResponse = await TwoFactorService.AddAuthenticator.Execute(DeviceID);

            if (addAuthenticatorResponse == null)
                return LinkResult.GeneralFailure;

            // Status 2 means no phone number is on the account
            if (addAuthenticatorResponse.Status == 2) {
                if (PhoneNumber == null) {
                    return LinkResult.MustProvidePhoneNumber;
                } else {
                    // Add phone number

                    // Get country code
                    string countryCode = PhoneCountryCode;

                    // If given country code is null, use the one from the Steam account
                    if (string.IsNullOrEmpty(countryCode)) {
                        countryCode = await GetUserCountry() ?? "US";
                    }

                    // Set the phone number
                    var res = await SetAccountPhoneNumber(this.PhoneNumber, countryCode);

                    // Make sure it's successful then respond that we must confirm via email
                    if (res != null && res.ConfirmationEmailAddress != null) {
                        ConfirmationEmailAddress = res.ConfirmationEmailAddress;
                        ConfirmationEmailSent = true;
                        return LinkResult.MustConfirmEmail;
                    }

                    // If something else fails, we end up here
                    return LinkResult.FailureAddingPhone;
                }
            }

            if (addAuthenticatorResponse.Status == 29)
                return LinkResult.AuthenticatorPresent;

            if (addAuthenticatorResponse.Status != 1)
                return LinkResult.GeneralFailure;

            // Setup this.LinkedAccount
            LinkedAccount = addAuthenticatorResponse;
            LinkedAccount.DeviceID = DeviceID;
            LinkedAccount.Session = _session;

            return LinkResult.AwaitingFinalization;
        }

        public async Task<FinalizeResult> FinalizeAddAuthenticator(string smsCode) {
            if (LinkedAccount == null)
                throw new NullReferenceException(nameof(LinkedAccount));

            int tries = 0;
            while (tries <= 10) {
                var finalizeAuthenticatorResponse = await TwoFactorService.FinalizeAddAuthenticator.Execute(LinkedAccount.GenerateSteamGuardCode(), smsCode);

                if (finalizeAuthenticatorResponse == null || finalizeAuthenticatorResponse == null) {
                    return FinalizeResult.GeneralFailure;
                }

                if (finalizeAuthenticatorResponse.Status == 89) {
                    return FinalizeResult.BadSMSCode;
                }

                if (finalizeAuthenticatorResponse.Status == 88) {
                    if (tries >= 10) {
                        return FinalizeResult.UnableToGenerateCorrectCodes;
                    }
                }

                if (!finalizeAuthenticatorResponse.Success) {
                    return FinalizeResult.GeneralFailure;
                }

                if (finalizeAuthenticatorResponse.WantMore) {
                    tries++;
                    continue;
                }

                LinkedAccount.FullyEnrolled = true;
                return FinalizeResult.Success;
            }

            return FinalizeResult.TooManyTries;
        }

        private async Task<string?> GetUserCountry() {
            UserAccountService userAccountService = new UserAccountService(_session.SteamId, _session.AccessToken);
            var response = await userAccountService.GetUserCountry.Execute();
            return response?.Country;
        }

        private async Task<SetAccountPhoneNumberResponse?> SetAccountPhoneNumber(string phoneNumber, string countryCode) {
            var response = await PhoneService.SetAccountPhoneNumber.Execute(phoneNumber, countryCode);
            return response;
        }

        private async Task<bool> IsAccountWaitingForEmailConfirmation() {
            var response = await PhoneService.IsAccountWaitingForEmailConfirmation.Execute();
            return response?.AwaitingEmailConfirmation == true;
        }

        private async Task<bool> SendPhoneVerificationCode() {
            return await PhoneService.SendPhoneVerificationCode.Execute();
        }

        public enum LinkResult {
            [Description("No phone number linked to the account.")]
            MustProvidePhoneNumber,

            [Description("A phone number is already linked to the account. This must be removed before continuing.")]
            MustRemovePhoneNumber,

            [Description("You need to click the link from in the confirmation email.")]
            MustConfirmEmail,

            [Description("Awaiting finalization, you must provide an SMS code.")]
            AwaitingFinalization,

            [Description("Unknown failure.")]
            GeneralFailure,

            [Description("Authenticator already setup.")]
            AuthenticatorPresent,

            [Description("Unknown issue adding the phone number to the account.")]
            FailureAddingPhone
        }

        public enum FinalizeResult {
            [Description("Unknown failure.")]
            GeneralFailure,

            [Description("Provided SMS confirmation code was incorrect.")]
            BadSMSCode,

            [Description("Unable to generate correct/valid authentication codes.")]
            UnableToGenerateCorrectCodes,

            [Description("Successfully linked.")]
            Success,

            [Description("Too many attempts.")]
            TooManyTries,
        }
    }
}