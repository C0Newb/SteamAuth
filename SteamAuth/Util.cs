using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamAuth {
    internal class Util {
        public static byte[] HexStringToByteArray(string hex = "") {
            if (string.IsNullOrEmpty(hex))
                return Array.Empty<byte>();

            int hexLen = hex.Length;
            byte[] ret = new byte[hexLen / 2];
            for (int i = 0; i < hexLen; i += 2) {
                ret[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return ret;
        }
    }
}
