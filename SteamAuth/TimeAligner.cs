using SteamAuth.APIEndpoints;

namespace SteamAuth {
    /// <summary>
    /// Class to help align system time with the Steam server time. Not super advanced; probably not taking some things into account that it should.
    /// Necessary to generate up-to-date codes. In general, this will have an error of less than a second, assuming Steam is operational.
    /// </summary>
    public class TimeAligner {
        public const int RealignInterval = 6; // 24 would probably be fine here :p

        /// <summary>
        /// Last time we've aligned out time with the Steam servers.
        /// </summary>
        public static DateTime LastAlignment {
            get => _lastAlignment;
        }
        private static DateTime _lastAlignment = DateTime.MinValue;

        /// <summary>
        /// If we have aligned within the last realignment threshold, <see cref="RealignInterval"/>.
        /// </summary>
        public static bool IsAligned {
            get => LastAlignment.AddHours(RealignInterval) > DateTime.UtcNow;
        }

        /// <summary>
        /// Amount of time we're off from the Steam servers.
        /// </summary>
        public static int TimeOffset {
            get => _timeOffset;
        }

        // Time 
        private static int _timeOffset = 0;



        /// <summary>
        /// Obtains the current Steam server time, using an delta between this machine's time and their response.
        /// </summary>
        /// <returns>Current Steam server time.</returns>
        public static long GetSteamTime() {
            if (!IsAligned)
                AlignTime();

            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _timeOffset;
        }

        /// <inheritdoc cref="GetSteamTime"/>
        public static async Task<long> GetSteamTimeAsync() {
            if (!IsAligned)
                await AlignTimeAsync();

            return DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _timeOffset;
        }


        /// <summary>
        /// Recalculates the time offset between this machine and the Steam servers
        /// </summary>
        public static void AlignTime() {
            AlignTimeAsync().Wait();
        }
        /// <inheritdoc cref="AlignTime"/>
        /// <returns>Query time request.</returns>
        public static async Task AlignTimeAsync() {
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            TwoFactorService twoFactorService = new TwoFactorService(0);
            var response = await twoFactorService.QueryTime.Execute();
            _timeOffset = (int)(response?.ServerTime - currentTime ?? _timeOffset);
            _lastAlignment = DateTime.UtcNow;
        }


    }
}