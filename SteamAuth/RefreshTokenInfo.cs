using SteamAuth.Enums;
using SteamKit2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SteamAuth {
    public class RefreshTokenInfo {
        [JsonPropertyName("token_id")]
        public string TokenId { get; set; } = string.Empty;

        [JsonPropertyName("token_description")]
        public string DeviceFriendlyName { get; set; } = string.Empty;

        [JsonPropertyName("time_updated")]
        public ulong TimeUpdated { get; set; }

        [JsonPropertyName("platform_type")]
        public PlatformType PlatformType { get; set; }

        [JsonPropertyName("os_type")]
        public EOSType OSType { get; set; }

        [JsonPropertyName("auth_type")]
        public ConfirmationType ConfirmationMethod { get; set; }

        [JsonPropertyName("first_seen")]
        public SeenProperties FirstSeen { get; set; } = new SeenProperties();

        [JsonPropertyName("last_seen")]
        public SeenProperties LastSeen { get; set; } = new SeenProperties();


        public class SeenProperties {
            [JsonPropertyName("time")]
            public ulong Time { get; set; }

            [JsonPropertyName("ip")]
            public SeenIP? IP { get; set; }

            [JsonPropertyName("country")]
            public string? Country { get; set; }

            [JsonPropertyName("state")]
            public string? State { get; set; }

            [JsonPropertyName("city")]
            public string? City { get; set; }
        }

        public class SeenIP {
            [JsonPropertyName("v4")]
            public ulong RawIPv4 { get; set; }
            public string IPv4 {
                get {
                    try {
                        byte[] bytes = BitConverter.GetBytes(RawIPv4);
                        if (BitConverter.IsLittleEndian) {
                            Array.Reverse(bytes);
                        }
                        return string.Join(".", bytes.Skip(4));
                    } catch {
                        return string.Empty;
                    }
                }
            }

            public override string ToString() {
                return IPv4;
            }
        }
    }
}
