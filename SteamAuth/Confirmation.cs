using System.Text.Json.Serialization;

namespace SteamAuth {
    /// <summary>
    /// Representation of a mobile authenticator confirmation, such as a trade or market confirmation.
    /// </summary>
    public class Confirmation {
        [JsonPropertyName("id")]
        public ulong Id { get; set; }

        [JsonPropertyName("nonce")]
        public ulong Key { get; set; }

        [JsonPropertyName("creator_id")]
        public ulong Creator { get; set; }

        [JsonPropertyName("headline")]
        public string Headline { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public List<string>? Summary { get; set; }

        [JsonPropertyName("accept")]
        public string Accept { get; set; } = string.Empty;

        [JsonPropertyName("cancel")]
        public string Cancel { get; set; } = string.Empty;

        [JsonPropertyName("icon")]
        public string Icon { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public EMobileConfirmationType ConfirmationType { get; set; } = EMobileConfirmationType.Invalid;

        [JsonIgnore]
        public SteamGuardAccount? Account { get; set; }



        /// <summary>
        /// Accepts this confirmation.
        /// </summary>
        /// <returns>Whether this confirmation was accepted.</returns>
        public bool AcceptConfirmation() {
            if (Account == null)
                throw new NoSteamGuardAccountException();

            return Account.AcceptConfirmation(this);
        }
        /// <inheritdoc cref="AcceptConfirmation"/>
        public async Task<bool> AcceptConfirmationAsync() {
            if (Account == null)
                throw new NoSteamGuardAccountException();

            return await Account.AcceptConfirmationAsync(this);
        }


        // Deny
        /// <summary>
        /// Denies this confirmation.
        /// </summary>
        /// <returns>Whether this confirmation was denied.</returns>
        public bool DenyConfirmation() {
            if (Account == null)
                throw new NoSteamGuardAccountException();

            return Account.DenyConfirmation(this);
        }
        /// <inheritdoc cref="DenyConfirmation"/>
        public async Task<bool> DenyConfirmationAsync() {
            if (Account == null)
                throw new NoSteamGuardAccountException();

            return await Account.DenyConfirmationAsync(this);
        }



        public enum EMobileConfirmationType {
            Invalid = 0,
            Test = 1,
            Trade = 2,
            MarketListing = 3,
            FeatureOptOut = 4,
            PhoneNumberChange = 5,
            AccountRecovery = 6
        }


        /// <summary>
        /// There is no Steam Guard account associated with this confirmation.
        /// </summary>
        [Serializable]
        public class NoSteamGuardAccountException : Exception {
            public NoSteamGuardAccountException() { }
            public NoSteamGuardAccountException(string message) : base(message) { }
            public NoSteamGuardAccountException(string message, Exception inner) : base(message, inner) { }
            protected NoSteamGuardAccountException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
        }
    }

    public class ConfirmationsResponse {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("needauth")]
        public bool NeedsAuthentication { get; set; }

        [JsonPropertyName("conf")]
        public Confirmation[]? Confirmations { get; set; }
    }
}