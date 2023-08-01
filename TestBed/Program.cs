using SteamAuth;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.Internal;
using System.Text.Json;
using SteamAuth.Enums;

namespace TestBed {
    class Program {
        private static readonly SteamClient steamClient = new SteamClient();

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
                    ("Remove mobile authenticator", RemoveMobileAuthenticator),
                };
            }

            private static void PressAnyKey() {
                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
            }

            public void MainMenu() {
                // Connect to Steam
                Console.WriteLine("Connecting...");
                steamClient.Connect();

                // Really basic way to wait until Steam is connected
                while (!steamClient.IsConnected)
                    Thread.Sleep(500);


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
                            menuOptions[index].Item2(); // Execute the selected action
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
                if (!steamClient.IsConnected) {
                    // Connect to Steam
                    Console.WriteLine("Connecting to Steam...");
                    steamClient.Connect();

                    // Really basic way to wait until Steam is connected
                    while (!steamClient.IsConnected)
                        Thread.Sleep(500);
                }

                Console.Write("Enter username: ");
                string? username = Console.ReadLine();

                Console.Write("Enter password: ");
                string? password = GetPassword();

                Console.WriteLine();
                Console.WriteLine("Authenticating...");

                // Create a new auth session
                CredentialsAuthSession authSession;
                try {
                    var authenticationTask = steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(new AuthSessionDetails {
                        Username = username,
                        Password = password,
                        IsPersistentSession = false,
                        PlatformType = EAuthTokenPlatformType.k_EAuthTokenPlatformType_MobileApp,
                        ClientOSType = EOSType.Android9,
                        Authenticator = new UserConsoleAuthenticator(),
                    });
                    authenticationTask.Wait();
                    authSession = authenticationTask.Result;
                } catch (Exception ex) {
                    Console.WriteLine("Error logging in: " + ex.Message);
                    PressAnyKey();
                    return;
                }

                // Starting polling Steam for authentication response
                var pollingTask = authSession.PollingWaitForResultAsync();
                pollingTask.Wait();
                var pollResponse = pollingTask.Result;

                // Build a SessionData object
                SessionData sessionData = new SessionData() {
                    SteamID = authSession.SteamID.ConvertToUInt64(),
                    AccessToken = pollResponse.AccessToken,
                    RefreshToken = pollResponse.RefreshToken,
                };

                Console.WriteLine();
                Console.WriteLine("SteamId: " + sessionData.SteamID);
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
                        break;
                    }

                    if (result != AuthenticatorLinker.LinkResult.AwaitingFinalization) {
                        Console.WriteLine("Failed to add authenticator: " + result);
                        break;
                    }

                    // Write maFile
                    try {
                        string sgFile = System.Text.Json.JsonSerializer.Serialize<SteamGuardAccount>(linker.LinkedAccount ?? new SteamGuardAccount(), new JsonSerializerOptions {
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

                if (result != AuthenticatorLinker.LinkResult.AwaitingFinalization) {
                    PressAnyKey();
                    return;
                }

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
                    Console.WriteLine("Please enter SMS code: ");
                    string? smsCode = Console.ReadLine();
                    
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
                    selectedAccount = System.Text.Json.JsonSerializer.Deserialize<SteamGuardAccount>(fileContents);

                    Console.WriteLine();
                    Console.WriteLine($"Selected account: {selectedAccount?.AccountName}");
                    Console.WriteLine($"SteamID: {selectedAccount?.Session?.SteamID}");
                } else {
                    Console.WriteLine("Invalid input. Please enter a valid number.");
                }

                PressAnyKey();
            }

            private void GenerateAuthCode() {
                if (selectedAccount == null) {
                    Console.WriteLine("No account selected!");
                    return;
                }

                Console.WriteLine($"{selectedAccount.AccountName}'s auth code: {selectedAccount.GenerateSteamGuardCode()}");

                PressAnyKey();
            }

            private void RemoveMobileAuthenticator() {
                if (selectedAccount == null) {
                    Console.WriteLine("No account selected!");
                    return;
                }
                Console.WriteLine("Removing mobile authenticator...");

                Console.WriteLine("Hit Y to return to email authentication, receiving auth codes via email.");
                Console.WriteLine("Hit D to disable Steam Guard.");
                Console.WriteLine("Any other key to cancel.");

                ConsoleKey key = Console.ReadKey(true).Key;
                bool successful;
                if (key == ConsoleKey.Y || key == ConsoleKey.D) {
                    SteamGuardScheme scheme = key == ConsoleKey.Y ? SteamGuardScheme.ReturnToEmail : SteamGuardScheme.Disable;
                    
                    Console.WriteLine();
                    Console.WriteLine($"{selectedAccount.AccountName}'s auth code: {selectedAccount.GenerateSteamGuardCode()}");
                    var task = selectedAccount.DeactivateAuthenticator(scheme);
                    task.Wait();
                    successful = task.Result;
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
                        if (File.Exists(selectedAccount.AccountName + ".maFile"))
                            File.Delete(selectedAccount.AccountName + ".maFile");

                        Console.WriteLine($"Removed {selectedAccount.AccountName} successfully.");
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
        }
    }
}