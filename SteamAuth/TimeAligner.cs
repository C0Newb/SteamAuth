using System;
using System.Threading.Tasks;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using SteamAuth.APIEndpoints;

namespace SteamAuth {
    /// <summary>
    /// Class to help align system time with the Steam server time. Not super advanced; probably not taking some things into account that it should.
    /// Necessary to generate up-to-date codes. In general, this will have an error of less than a second, assuming Steam is operational.
    /// </summary>
    public class TimeAligner {
        private static bool _aligned = false;
        private static int _timeDifference = 0;

        public static long GetSteamTime() {
            if (!_aligned)
                AlignTime();

            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _timeDifference;
        }

        public static async Task<long> GetSteamTimeAsync() {
            if (!_aligned)
                await AlignTimeAsync();

            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _timeDifference;
        }
        
        public static async Task AlignTimeAsync() {
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            TwoFactorService twoFactorService = new TwoFactorService(0);
            var response = await twoFactorService.QueryTime.Execute();
            _timeDifference = (int)(response?.ServerTime - currentTime ?? _timeDifference);
            _aligned = true;
        }

        public static void AlignTime() {
            AlignTimeAsync().Wait();
        }
    }
}