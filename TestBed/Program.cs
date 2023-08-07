using SteamAuth;
using SteamAuth.APIEndpoints;
using SteamAuth.Enums;
using System.Text.Json;
using static SteamAuth.SteamGuardAccount;
using static SteamKit2.GC.CSGO.Internal.CProductInfo_SetRichPresenceLocalization_Request;

namespace TestBed {
    class Program {
        public static void Main() {
            MainInteraction interactor = new MainInteraction();
            interactor.MainMenu();
        }

        public static string GetPassword() {
            string pwd = string.Empty;
            while (true) {
                ConsoleKeyInfo i = Console.ReadKey(true);
                if (i.Key == ConsoleKey.Enter) {
                    break;
                } else if (i.Key == ConsoleKey.Backspace) {
                    if (pwd.Length > 0) {
                        _ = pwd.Remove(pwd.Length - 1);
                        Console.Write("\b \b");
                    }
                } else if (i.KeyChar != '\u0000') // KeyChar == '\u0000' if the key pressed does not correspond to a printable character, e.g. F1, Pause-Break, etc
                  {
                    pwd += i.KeyChar;
                    Console.Write("*");
                }
            }
            return pwd;
        }

        internal class MainInteraction {
            private SteamGuardAccount? selectedAccount;

            private readonly List<(string, Action)> menuOptions;

            public MainInteraction() {
                menuOptions = new List<(string, Action)>
                {
                    ("Setup mobile authenticator", SetupMobileAuthenticator),
                    ("Select account", SelectAccount),
                    ("Generate auth code", GenerateAuthCode),
                    ("Create emergency codes", GenerateEmergencyCodes),
                    ("Check if an auth code is valid", ValidateCode),
                    ("Destroy emergency codes", DestroyEmergencyCodes),
                    ("List tokens", ListTokens),
                    ("Revoke token", RevokeToken),
                    ("Remove mobile authenticator", RemoveMobileAuthenticator),
                    ("Remove mobile authenticator via SMS code", RemoveMobileAuthenticatorViaChallenge)
                };
            }

            private static string GetSMSCode() {
                string smsCode = string.Empty;
                while (string.IsNullOrEmpty(smsCode)) {
                    Console.Write("SMS Code: ");
                    smsCode = Console.ReadLine() ?? string.Empty;
                }
                return smsCode;
            }

            private bool IsAccountSelected() {
                if (selectedAccount == null) {
                    Console.WriteLine("No account selected!");
                    PressAnyKey();
                    return false;
                }
                return true;
            }

            private static void PressAnyKey() {
                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }

            public void MainMenu() {
                while (true) {
                    Console.Clear();
                    DisplayBanner();

                    for (int i = 0; i < menuOptions.Count; i++) {
                        Console.WriteLine($"{i + 1}) {menuOptions[i].Item1}");
                    }

                    Console.WriteLine("0) Exit");

                    Console.WriteLine();
                    Console.Write("Enter your choice: ");

                    if (int.TryParse(Console.ReadLine(), out int choice)) {
                        if (choice == 0) {
                            break; // Exit the menu loop
                        }

                        int index = choice - 1;
                        if (index >= 0 && index < menuOptions.Count) {
                            Console.WriteLine();
                            Console.WriteLine();
                            try {
                                menuOptions[index].Item2(); // Execute the selected action
                            } catch (Exception e) {
                                Console.WriteLine("");
                                Console.Error.WriteLine(e.ToString());
                                PressAnyKey();
                            }
                        } else {
                            Console.WriteLine("Invalid choice. Please try again.");
                            Console.ReadLine();
                        }
                    } else {
                        Console.WriteLine("Invalid input. Please enter a valid number.");
                        Console.ReadLine();
                    }
                }
            }

            private void DisplayBanner() {
                Console.WriteLine($"Selected account: {selectedAccount?.AccountName ?? "none"}");
                Console.WriteLine("-----------");
            }

            #region Setup
            private void SetupMobileAuthenticator() {
                string username = string.Empty;
                while (string.IsNullOrEmpty(username)) {
                    Console.Write("Enter username: ");
                    username = Console.ReadLine() ?? string.Empty;
                }

                Console.Write("Enter password: ");
                string password = GetPassword();

                Console.WriteLine();
                Console.WriteLine("Authenticating...");


                var responder = new SteamGuardCodeProvider(selectedAccount);
                var loginTask = UserLogin.LoginWithCredentials(username, password, responder);
                loginTask.Wait();
                var loginResponse = loginTask.Result;

                if (loginResponse?.SteamId == null) {
                    Console.Error.WriteLine("Failed to login!");
                    PressAnyKey();
                    return;
                }

                // Build a SessionData object
                SessionData sessionData = new SessionData() {
                    SteamId = loginResponse.SteamId,
                    AccessToken = loginResponse?.AccessToken ?? string.Empty,
                    RefreshToken = loginResponse?.RefreshToken ?? string.Empty,
                };




                Console.WriteLine();
                Console.WriteLine("SteamId: " + sessionData.SteamId);
                Console.WriteLine();
                Console.WriteLine("AccessToken: " + sessionData.AccessToken);
                Console.WriteLine();
                Console.WriteLine("RefreshToken: " + sessionData.RefreshToken);
                Console.WriteLine();

                // Init AuthenticatorLinker
                AuthenticatorLinker linker = new AuthenticatorLinker(sessionData);

                Console.WriteLine("If the account has no phone number, enter one now: (+1 XXXXXXXXXX)");
                string? phoneNumber = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(phoneNumber)) {
                    linker.PhoneNumber = string.Empty;
                } else {
                    linker.PhoneNumber = phoneNumber;
                }

                int tries = 0;
                AuthenticatorLinker.LinkResult result = AuthenticatorLinker.LinkResult.GeneralFailure;
                bool migrate = false;
                while (tries <= 5) {
                    tries++;

                    // Add authenticator
                    var addTask = linker.AddAuthenticator();
                    addTask.Wait();
                    result = addTask.Result;

                    if (result == AuthenticatorLinker.LinkResult.MustConfirmEmail) {
                        Console.WriteLine("Click the link sent to your email address: " + linker.ConfirmationEmailAddress);
                        Console.WriteLine("Press enter when done");
                        Console.ReadLine();
                        continue;
                    }

                    if (result == AuthenticatorLinker.LinkResult.MustProvidePhoneNumber) {
                        Console.WriteLine("Account requires a phone number. Login again and enter one.");
                        break;
                    }

                    if (result == AuthenticatorLinker.LinkResult.AuthenticatorPresent) {
                        Console.WriteLine("Account already has an authenticator linked.");
                        /*Console.WriteLine("Would you like to migrate it over?");
                        ConsoleKey key = Console.ReadKey(true).Key;
                        if (key == ConsoleKey.Y)
                            migrate = true;*/
                        break;
                    }

                    if (result != AuthenticatorLinker.LinkResult.AwaitingFinalization) {
                        Console.WriteLine("Failed to add authenticator: " + result);
                        break;
                    }

                    // Write maFile
                    try {
                        string sgFile = JsonSerializer.Serialize(linker.LinkedAccount ?? new SteamGuardAccount(), new JsonSerializerOptions {
                            WriteIndented = true,
                        });
                        string fileName = linker.LinkedAccount?.AccountName + ".maFile";
                        File.WriteAllText(fileName, sgFile);
                        break;
                    } catch (Exception e) {
                        Console.WriteLine(e.Message);
                        Console.WriteLine("EXCEPTION saving maFile. For security, authenticator will not be finalized.");
                        break;
                    }
                }

                if (result != AuthenticatorLinker.LinkResult.AwaitingFinalization && !migrate) {
                    PressAnyKey();
                    return;
                }

                /*if (migrate) {
                    AuthenticationService authenticationService = new AuthenticationService(sessionData.SteamId,sessionData.AccessToken);
                    while (true) {
                        string code = string.Empty;
                        while (string.IsNullOrEmpty(code)) {
                            Console.WriteLine("Mobile auth code: ");
                            code = Console.ReadLine() ?? string.Empty;
                        }
                        var task = authenticationService.MigrateMobileSession.Execute(code);
                        task.Wait();
                        if (task.Result.AccessToken != null)
                            break;
                    }
                }*/

                Console.WriteLine("Successfully linked, awaiting finalization before switching to Steam Guard Mobile Authenticator (still using email/none)!");
                Console.WriteLine($"!!! write that down ----> Revocation code: {linker?.LinkedAccount?.RevocationCode} <---- write that down !!!");

                Console.WriteLine();
                Console.Write("To enforce compliance, type the revocation code: ");
                string? revocationCode = Console.ReadLine();

                if (revocationCode != linker?.LinkedAccount?.RevocationCode || string.IsNullOrEmpty(revocationCode)) {
                    Console.WriteLine(":/ you didn't enter the right code!");
                    Console.WriteLine("Canceling!");

                    try {
                        linker?.LinkedAccount?.DeactivateAuthenticator(SteamGuardScheme.Disable);
                    } catch (Exception e) {
                        Console.WriteLine($"Failed to remove! But don't worry, you can still login without it. Error: {e.Message}");
                    }

                    PressAnyKey();
                    return;
                }

                tries = 0;
                while (tries <= 5) {
                    string? smsCode = GetSMSCode();

                    var finalizeTask = linker?.FinalizeAddAuthenticator(smsCode ?? string.Empty);
                    finalizeTask?.Wait();
                    var linkResult = finalizeTask?.Result;

                    if (linkResult != AuthenticatorLinker.FinalizeResult.Success) {
                        Console.WriteLine("Failed to finalize authenticator: " + linkResult);
                        continue;
                    }

                    Console.WriteLine("Authenticator finalized!");
                    break;
                }

                Console.WriteLine("Authenticator added!");
                selectedAccount = linker?.LinkedAccount;

                PressAnyKey();
            }
            #endregion

            #region Saved account options
            private void SelectAccount() {
                var accountFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.maFile");
                if (accountFiles.Length == 0) {
                    Console.WriteLine("No account files found.");
                    Console.ReadLine();
                    return;
                }

                Console.WriteLine("Available accounts:");
                for (int i = 0; i < accountFiles.Length; i++) {
                    string fileName = Path.GetFileNameWithoutExtension(accountFiles[i]);
                    Console.WriteLine($"{i + 1}) {fileName}");
                }

                Console.WriteLine();
                Console.Write("Enter the account number: ");
                if (int.TryParse(Console.ReadLine(), out int accountNumber) && accountNumber >= 1 && accountNumber <= accountFiles.Length) {
                    string fileContents = File.ReadAllText(accountFiles[accountNumber - 1]);

                    //selectedAccount = JsonConvert.DeserializeObject<SteamGuardAccount>(fileContents); <-- this is for backwards compatibility
                    selectedAccount = JsonSerializer.Deserialize<SteamGuardAccount>(fileContents);

                    Console.WriteLine();
                    Console.WriteLine($"Selected account: {selectedAccount?.AccountName}");
                    Console.WriteLine($"SteamId: {selectedAccount?.Session?.SteamId}");
                } else {
                    Console.WriteLine("Invalid input. Please enter a valid number.");
                }

                PressAnyKey();
            }

            private void GenerateAuthCode() {
                if (!IsAccountSelected())
                    return;

                Console.WriteLine($"{selectedAccount?.AccountName}'s auth code: {selectedAccount?.GenerateSteamGuardCode()}");

                PressAnyKey();
            }

            private void GenerateEmergencyCodes() {
                if (!IsAccountSelected())
                    return;
                try {
                    var codes = selectedAccount?.GenerateEmergencyCodes(GetSMSCode) ?? Array.Empty<string>();
                    Console.WriteLine("Emergency codes: ");
                    foreach (string code in codes) {
                        Console.WriteLine(code);
                    }
                    Console.WriteLine();
                    Console.WriteLine("These codes can be used like mobile authenticator codes and therefore are secretive. Please store this somewhere safe.");
                } catch (InvalidSMSCodeException e) {
                    Console.Error.WriteLine(e.Message);
                }
                PressAnyKey();
            }
            private void DestroyEmergencyCodes() {
                if (selectedAccount == null) {
                    Console.WriteLine("No account selected!");
                    return;
                }
                var success = selectedAccount.DestroyEmergencyCodes();
                Console.WriteLine(success ? "Removed successfully!" : "Unable to remove emergency codes.");
                PressAnyKey();
            }

            private void ValidateCode() {
                if (!IsAccountSelected())
                    return;
                var code = string.Empty;
                while (string.IsNullOrEmpty(code)) {
                    Console.Write("Code to validate: ");
                    code = Console.ReadLine();
                }

                var success = selectedAccount?.ValidateSteamGuardCode(code) ?? false;
                Console.WriteLine(success ? "Code valid." : "Code is not valid.");
                PressAnyKey();
            }

            private void ListTokens() {
                if (!IsAccountSelected())
                    return;

                var tokens = selectedAccount?.GetRefreshTokens() ?? Array.Empty<RefreshTokenInfo>();
                foreach (RefreshTokenInfo token in tokens) {
                    Console.WriteLine();
                    Console.WriteLine($"Token id: {token.TokenId}");
                    Console.WriteLine($"\tDevice name: \"{token.DeviceFriendlyName}\"");
                    Console.WriteLine($"\tOn platform \"{token.PlatformType}\" and OS \"{token.OSType}\"");
                    var lastSeenEntry = token.LastSeen;
                    if (lastSeenEntry != null && lastSeenEntry.IP != null) {
                        DateTime unixEpoch = DateTime.UnixEpoch;
                        unixEpoch = unixEpoch.AddSeconds(lastSeenEntry.Time);
                        unixEpoch = unixEpoch.ToLocalTime();
                        Console.WriteLine($"\tLast seen from IP address {lastSeenEntry.IP.ToString() ?? "unknown"}, inside {lastSeenEntry.City ?? "unknow city"}, {lastSeenEntry.State ?? ""} {lastSeenEntry.Country ?? ""} at {unixEpoch}.");
                    }
                }

                PressAnyKey();
            }

            private void RevokeToken() {
                if (!IsAccountSelected())
                    return;

                Console.WriteLine("Enter the token to revoke below. Type \"all\" to revoke all tokens!");
                string tokenToRevoke = string.Empty;
                while (string.IsNullOrEmpty(tokenToRevoke)) {
                    Console.Write("Token: ");
                    tokenToRevoke = Console.ReadLine() ?? string.Empty;
                }

                if (tokenToRevoke.ToLower() == "all") {
                    var tokens = selectedAccount?.GetRefreshTokens() ?? Array.Empty<RefreshTokenInfo>();
                    foreach (RefreshTokenInfo token in tokens) {
                        Console.WriteLine($"Token {token.TokenId} revoked? {selectedAccount?.RevokeToken(token.TokenId)}");
                    }
                } else {
                    Console.WriteLine($"Token {tokenToRevoke} revoked? {selectedAccount?.RevokeToken(tokenToRevoke)}");
                }

                PressAnyKey();
            }



            private void RemoveMobileAuthenticator() {
                if (!IsAccountSelected())
                    return;

                Console.WriteLine("Hit Y to return to email authentication, receiving auth codes via email.");
                Console.WriteLine("Hit D to disable Steam Guard.");
                Console.WriteLine("Any other key to cancel.");

                ConsoleKey key = Console.ReadKey(true).Key;
                bool successful;
                if (key == ConsoleKey.Y || key == ConsoleKey.D) {
                    Console.WriteLine("Removing mobile authenticator...");

                    SteamGuardScheme scheme = key == ConsoleKey.Y ? SteamGuardScheme.ReturnToEmail : SteamGuardScheme.Disable;

                    Console.WriteLine();
                    Console.WriteLine($"{selectedAccount?.AccountName}'s auth code: {selectedAccount?.GenerateSteamGuardCode()}");
                    var task = selectedAccount?.DeactivateAuthenticator(scheme);
                    task?.Wait();
                    successful = task?.Result ?? false;
                } else {
                    Console.WriteLine("Canceled!");
                    Console.ReadLine();
                    return;
                }

                if (successful) {
                    Console.WriteLine("Mobile authenticator removed successfully!");
                    Console.WriteLine();
                    Console.WriteLine("Would you like to delete the maFile?");
                    Console.WriteLine("\t(Y)es");
                    Console.WriteLine("\tany other key for no.");
                    if (Console.ReadKey(true).Key == ConsoleKey.Y) {
                        if (File.Exists(selectedAccount?.AccountName + ".maFile"))
                            File.Delete(selectedAccount?.AccountName + ".maFile");

                        Console.WriteLine($"Removed {selectedAccount?.AccountName} successfully.");
                        selectedAccount = null;
                    } else {
                        return;
                    }
                } else {
                    Console.WriteLine("Failed to remove the authenticator!!");
                }

                PressAnyKey();
            }

            private void RemoveMobileAuthenticatorViaChallenge() {
                if (!IsAccountSelected())
                    return;

                Console.WriteLine("Are you sure? Press Y to continue.");
                Console.WriteLine("Any other key to cancel.");

                ConsoleKey key = Console.ReadKey(true).Key;
                bool successful;
                if (key == ConsoleKey.Y) {
                    Console.WriteLine("Removing mobile authenticator...");

                    var task = selectedAccount?.DeactivateAuthenticatorViaChallenge(GetSMSCode);
                    task?.Wait();
                    successful = task?.Result ?? false;
                } else {
                    Console.WriteLine("Canceled!");
                    Console.ReadLine();
                    return;
                }

                if (successful) {
                    Console.WriteLine("Mobile authenticator removed successfully!");
                    Console.WriteLine();
                    Console.WriteLine("Would you like to delete the maFile?");
                    Console.WriteLine("\t(Y)es");
                    Console.WriteLine("\tany other key for no.");
                    if (Console.ReadKey(true).Key == ConsoleKey.Y) {
                        if (File.Exists(selectedAccount?.AccountName + ".maFile"))
                            File.Delete(selectedAccount?.AccountName + ".maFile");

                        Console.WriteLine($"Removed {selectedAccount?.AccountName} successfully.");
                        selectedAccount = null;
                    } else {
                        return;
                    }
                } else {
                    Console.WriteLine("Failed to remove the authenticator!!");
                }

                PressAnyKey();
            }
            #endregion

            class SteamGuardCodeProvider : UserLogin.SteamGuardResponder {
                public SteamGuardCodeProvider(SteamGuardAccount? account) : base(account) { }

                public override Task<string> GetSteamGuardCode(ulong steamId, BeginAuthSessionViaCredentials.AllowedConfirmation confirmation, bool previousCodeInvalid) {
                    static Task<string> getCode(string message) {
                        while (true) {
                            Console.Write(message);
                            var code = Console.ReadLine();
                            if (!string.IsNullOrEmpty(code))
                                return Task.FromResult(code);
                        }
                    };

                    if (confirmation.ConfirmationType == ConfirmationType.DeviceCode) {
                        if (SteamGuardAccount != null && SteamGuardAccount.Session?.SteamId == steamId && !previousCodeInvalid) {
                            return SteamGuardAccount.GenerateSteamGuardCodeAsync();
                        }
                        // Request mobile code ??
                        Console.WriteLine($"Please enter the Steam Guard code from your mobile authenticator. {confirmation.AssociatedMessage}.");
                        return getCode("Mobile authenticator code: ");

                    } else if (confirmation.ConfirmationType == ConfirmationType.EmailCode) {
                        // get email code

                        Console.WriteLine($"Steam Guard code was sent to your email at {confirmation.AssociatedMessage}.");
                        Console.Write("Email code: ");
                        return getCode("Email code: ");
                    }

                    Console.WriteLine("A 2FA code is required to login to your Steam account. Please enter that code below.");
                    Console.WriteLine($"Message: {confirmation.AssociatedMessage}");
                    return getCode("Auth code: ");
                }
            }
        }
    }
}