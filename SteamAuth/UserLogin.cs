using SteamAuth.APIEndpoints;
using static SteamAuth.APIEndpoints.BeginAuthSessionViaCredentials;
using SteamAuth.Enums;
using SteamKit2.Authentication;
using SteamKit2;
using SteamKit2.Internal;
using System.Net;

namespace SteamAuth
{
    /// <summary>
    /// Handles logging the user into the mobile Steam website. Necessary to generate OAuth token and session cookies.
    /// </summary>
    public class UserLogin {
        /// <summary>
        /// A class used to respond to 2FA requests. Use this to implement the logic that will provide the <see cref="LoginWithCredentials"/> with the proper Steam Guard code.
        /// </summary>
        public abstract class SteamGuardResponder {
            public readonly SteamGuardAccount? SteamGuardAccount;

            public SteamGuardResponder(SteamGuardAccount? account = null) {
                SteamGuardAccount = account;
            }

            /// <summary>
            /// Returns a valid Steam Guard code for the user 2FA login.
            /// </summary>
            /// <param name="confirmation">Details about the confirmation method.</param>
            /// <param name="previousCodeInvalid">Whether the previously entered Steam Guard code was invalid or not.</param>
            /// <returns>A valid Steam Guard code.</returns>
            public abstract Task<string> GetSteamGuardCode(ulong steamId, AllowedConfirmation confirmation, bool previousCodeInvalid);
        }


        // Used to adapt the responder to SteamKit
        private class SteamKitResponder : SteamGuardResponder, IAuthenticator {
            private SteamGuardResponder? SteamGuardResponder;

            public SteamKitResponder(SteamGuardResponder? steamGuardResponder) {
                SteamGuardResponder = steamGuardResponder;
            }

            public Task<bool> AcceptDeviceConfirmationAsync() {
                Console.WriteLine("Please accept the confirmation on your device before continuing...");
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
                return Task.FromResult(true);
            }

            public Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect) {
                AllowedConfirmation confirmation = new AllowedConfirmation {
                    ConfirmationType = ConfirmationType.DeviceCode,
                };
                return GetSteamGuardCode(SteamGuardAccount?.Session != null ? SteamGuardAccount.Session.SteamId : 0, confirmation, previousCodeWasIncorrect);
            }

            public Task<string> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect) {
                AllowedConfirmation confirmation = new AllowedConfirmation {
                    AssociatedMessage = email,
                    ConfirmationType = ConfirmationType.EmailCode,
                };
                return GetSteamGuardCode(0, confirmation, previousCodeWasIncorrect);
            }

            public override Task<string> GetSteamGuardCode(ulong steamId, AllowedConfirmation confirmation, bool previousCodeInvalid) {
                if (SteamGuardResponder != null)
                    return SteamGuardResponder.GetSteamGuardCode(steamId, confirmation, previousCodeInvalid);
                else
                    throw new NullReferenceException(nameof(SteamGuardResponder));
            }
        }


        /// <summary>
        /// Login to the user account and generate SessionData which will can be used to link an authenticator.
        /// Will not return until the login succeeds or fails.
        /// </summary>
        /// <param name="username">Steam account name.</param>
        /// <param name="password">Steam account password.</param>
        /// <param name="rememberLogin">Whether to inform Steam to remember this login.</param>
        /// <param name="steamGuardResponder">A <see cref="SteamGuardResponder"/> that will be able to provide Steam Guard codes (either mobile or Email).</param>
        /// <param name="cancellationToken">Possible way for you to cancel the login.</param>
        /// <returns>Session data of the now logged in user.</returns>
        /// <exception cref="InvalidOperationException">Thrown when an invalid state occurs, such as an unsupported confirmation method or invalid response by the <see cref="SteamGuardResponder"/>.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="steamGuardResponder"/> was null although the requested 2FA method requires user input to be supplied by the responder.</exception>
        /// <exception cref="NotImplementedException">The requested Steam Guard method is not yet supported by SteamAuth.</exception>
        /// <exception cref="AuthenticationException">Error polling the 2FA status or failed to log the user in.</exception>
        public static async Task<SteamGuardAuthenticationResponse> LoginWithCredentials(string username, string password, bool rememberLogin = false, SteamGuardResponder? steamGuardResponder = null, CancellationToken cancellationToken = default) {

            // Use SteamKit2 for now :/
            SteamKitResponder steamKitResponder = new SteamKitResponder(steamGuardResponder);


            // Create a new auth session
            SteamClient steamClient = new SteamClient();
            steamClient.Connect();
            if (!steamClient.IsConnected) {
                // Really basic way to wait until Steam is connected
                while (!steamClient.IsConnected) {
                    Thread.Sleep(500);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            CredentialsAuthSession authSession = await steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails {
                Username = username,
                Password = password,
                IsPersistentSession = false,
                PlatformType = EAuthTokenPlatformType.k_EAuthTokenPlatformType_MobileApp,
                ClientOSType = EOSType.Android9,
                Authenticator = steamKitResponder,
            });

            // Starting polling Steam for authentication response
            var pollResponse = await authSession.PollingWaitForResultAsync(cancellationToken);

            return new SteamGuardAuthenticationResponse {
                AccessToken = pollResponse.AccessToken,
                AccountName = pollResponse.AccountName,
                NewGuardData = pollResponse.NewGuardData,
                RefreshToken = pollResponse.RefreshToken,
                SteamId = authSession.SteamID,
            };

            /*AuthenticationService authenticationService = new AuthenticationService();
            var login = await authenticationService.BeginAuthSessionViaCredentials.Execute(username, password, rememberLogin);
            if (login == null)
                throw new AuthenticationException("Failed to login the user.");


            var preferredConfirmation = login.AllowedConfirmations?.FirstOrDefault();
            if (preferredConfirmation == null || preferredConfirmation.ConfirmationType == ConfirmationType.Unknown) {
                throw new InvalidOperationException("There are no allowed confirmations");
            }

            bool loop = false;

            switch (preferredConfirmation.ConfirmationType) {
                case ConfirmationType.None: // They're good
                    break;
                case ConfirmationType.DeviceConfirmation: // Waiting for user to accept on their device
                    loop = true;
                    break;

                case ConfirmationType.EmailCode:
                case ConfirmationType.DeviceCode:
                    if (steamGuardResponder == null)
                        throw new ArgumentNullException(nameof(steamGuardResponder));

                    int invalidCodeResult = preferredConfirmation.ConfirmationType switch {
                        ConfirmationType.EmailCode => 65,
                        ConfirmationType.DeviceCode => 88,
                        _ => throw new NotImplementedException(),
                    };
                    bool waitingForValidCode = true;
                    bool previousCodeInvalid = false;

                    while (waitingForValidCode) {
                        // Check if caller wants to cancel
                        cancellationToken.ThrowIfCancellationRequested();

                        try {
                            Task<string> getCodeTask = steamGuardResponder.GetSteamGuardCode(login.SteamId, preferredConfirmation, previousCodeInvalid);
                            string code = await getCodeTask.ConfigureAwait(false);

                            // Unknown amount of time has passed, check if caller wants to cancel
                            cancellationToken.ThrowIfCancellationRequested();

                            if (string.IsNullOrEmpty(code))
                                throw new InvalidOperationException("No code was provided by the Steam Guard responder.");

                            await authenticationService.UpdateAuthSessionWithSteamGuardCode.Execute(login.TokenId, code, preferredConfirmation.ConfirmationType);
                            waitingForValidCode = false; // well, we didn't raise an exception so we're good!
                        } catch (Exception ex) {
                            // Probably got an error thrown with the UpdateAuthSession ...
                            previousCodeInvalid = true;
                        }
                    }

                    break;
            }

            if (!loop) {
                cancellationToken.ThrowIfCancellationRequested();
                authenticationService.Parameters.Set("client_id", login.TokenId.ToString());
                authenticationService.SteamId = login.SteamId;
                var pollResponse = await authenticationService.PollAuthSessionStatus.Execute(login.TokenId, login.RequestId);
                if (pollResponse != null && pollResponse.NewClientId != default)
                    login.TokenId = pollResponse.NewClientId;
                if (pollResponse?.RefreshToken?.Length > 0) {
                    return new SteamGuardAuthenticationResponse {
                        AccessToken = pollResponse.AccessToken ?? string.Empty,
                        AccountName = pollResponse.AccountName ?? string.Empty,
                        RefreshToken = pollResponse.RefreshToken ?? string.Empty,
                        NewGuardData = pollResponse.NewGuardData ?? string.Empty,
                        SteamId = login.SteamId,
                    };
                } else {
                    throw new AuthenticationException("Failed on PollAuthSessionStatus");
                }
            }

            while (true) {
                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
                var pollResponse = await authenticationService.PollAuthSessionStatus.Execute(login.TokenId, login.RequestId);
                if (pollResponse != null)
                    return new SteamGuardAuthenticationResponse {
                        AccessToken = pollResponse.AccessToken ?? string.Empty,
                        AccountName = pollResponse.AccountName ?? string.Empty,
                        RefreshToken = pollResponse.RefreshToken ?? string.Empty,
                        NewGuardData = pollResponse.NewGuardData ?? string.Empty,
                        SteamId = login.SteamId,
                    };
            }
            */
        }
        /// <inheritdoc cref="LoginWithCredentials(string, string, bool, SteamGuardResponder?, CancellationToken)"/>
        public static async Task<SteamGuardAuthenticationResponse> LoginWithCredentials(string username, string password, SteamGuardResponder? steamGuardResponder = null, CancellationToken cancellationToken = default) =>  await LoginWithCredentials(username, password, false, steamGuardResponder, cancellationToken);

        /// <remarks>Only use this if you are certain no 2FA code will be required to login.</remarks>
        /// <inheritdoc cref="LoginWithCredentials(string, string, bool, SteamGuardResponder?, CancellationToken)"/>
        public static async Task<SteamGuardAuthenticationResponse> LoginWithCredentials(string username, string password, CancellationToken cancellationToken = default) =>  await LoginWithCredentials(username, password, false, null, cancellationToken);


        /// <summary>
        /// Logged in and accepted 2FA method.
        /// </summary>
        public class SteamGuardAuthenticationResponse {
            /// <summary>
            /// Account name of authenticating account.
            /// </summary>
            public string? AccountName { get; set; }

            /// <summary>
            /// New refresh token.
            /// </summary>
            public string? RefreshToken { get; set; }

            /// <summary>
            /// New token subordinate to <see cref="RefreshToken"/>.
            /// </summary>
            public string? AccessToken { get; set; }

            /// <summary>
            /// May contain remembered machine ID for future login, usually when account uses email based Steam Guard.
            /// </summary>
            public string? NewGuardData { get; set; }

            /// <summary>
            /// The user's SteamId
            /// </summary>
            public ulong SteamId { get; set; }
        }



        [Serializable]
        public class AuthenticationException : Exception {
            public AuthenticationException() { }
            public AuthenticationException(string message) : base(message) { }
            public AuthenticationException(string message, Exception inner) : base(message, inner) { }
            protected AuthenticationException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
        }
    }
}
