using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AssetStudio
{
    public static class UmaJPManager
    {
        private const string HardcodedDbKeyHex = "9C2BAB97BCF8C0C4F1A9EA7881A213F6C9EBF9D8D4C6A8E43CE5A259BDE7E9FD";
        private const string HardcodedAbKeyHexOrAscii = "532B4631E4A7B9473E7CFB"; 

        private static byte[] _abBaseKey = Array.Empty<byte>();
        private static string _dbKeyHex = string.Empty;
        private static readonly Dictionary<string, string> _bundleKeyMap = new(StringComparer.OrdinalIgnoreCase);

        static UmaJPManager()
        {
            TryLoad();
        }

        public static void Reload() => TryLoad(force: true);

        private static void TryLoad(bool force = false)
        {
            try
            {
                if (force)
                {
                    _bundleKeyMap.Clear();
                }

                _abBaseKey = ParseHexOrAscii(HardcodedAbKeyHexOrAscii);
                _dbKeyHex = HardcodedDbKeyHex;
            }
            catch (Exception e)
            {
                Logger.Error("[UmaJP] Failed to initialize built-in keys", e);
                _abBaseKey = Array.Empty<byte>();
                _dbKeyHex = string.Empty;
            }
        }

        public static bool HasAnyBundleKeys() => _bundleKeyMap != null && _bundleKeyMap.Count > 0;
        public static bool HasABKey() => _abBaseKey != null && _abBaseKey.Length > 0;
        public static string GetDbKeyHex() => _dbKeyHex;

        public static void UpdateBundleKeys(IDictionary<string, string> map)
        {
            if (map == null) return;
            foreach (var kv in map)
            {
                _bundleKeyMap[Normalize(kv.Key)] = kv.Value;
            }
        }

        public static void SetABKeyFromHexOrAscii(string s) => _abBaseKey = ParseHexOrAscii(s);

        public static bool TryGetXorPadFor(string bundlePathOrName, out byte[] xorpad)
        {
            xorpad = null;
            if (_abBaseKey == null || _abBaseKey.Length == 0)
                return false;
            if (string.IsNullOrEmpty(bundlePathOrName))
                return false;

            var normalized = Normalize(bundlePathOrName);
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(normalized))
            {
                candidates.Add(normalized);

                var segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0)
                {
                    for (int i = 0; i < segments.Length; i++)
                    {
                        var suffix = string.Join('/', segments.Skip(i));
                        if (!string.IsNullOrEmpty(suffix))
                        {
                            candidates.Add(suffix);
                        }
                    }
                }
            }

            Logger.Info($"[UmaJP] Resolving bundle key for '{bundlePathOrName}' via [{string.Join(", ", candidates.Distinct(StringComparer.OrdinalIgnoreCase))}]");

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (_bundleKeyMap.TryGetValue(candidate, out var keyStr) && TryParseLong(keyStr, out var keyNum))
                {
                    xorpad = DerivePad(_abBaseKey, keyNum);
                    Logger.Info($"[UmaJP] Matched bundle key using '{candidate}'");
                    return true;
                }
            }

            Logger.Info($"[UmaJP] Bundle key not found for '{bundlePathOrName}'");
            return false;
        }

        private static byte[] DerivePad(byte[] baseKey, long keyNum)
        {
            var keyBytes = BitConverter.GetBytes(keyNum); 
            var pad = new byte[baseKey.Length * 8];
            for (int i = 0; i < baseKey.Length; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    pad[i * 8 + j] = (byte)(baseKey[i] ^ keyBytes[j]);
                }
            }
            return pad;
        }

        private static bool TryParseLong(string value, out long number)
        {
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return long.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number);
            }
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
        }

        private static string Normalize(string p)
        {
            return p.Replace('\\', '/');
        }

        private static byte[] ParseHexOrAscii(string s)
        {
            if (string.IsNullOrEmpty(s)) return Array.Empty<byte>();
            if (s.All(IsHexChar) && s.Length % 2 == 0)
            {
                try
                {
                    var len = s.Length / 2;
                    var data = new byte[len];
                    for (int i = 0; i < len; i++)
                    {
                        data[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
                    }
                    return data;
                }
                catch
                {
                    
                }
            }
            return System.Text.Encoding.UTF8.GetBytes(s);
        }

        private static bool IsHexChar(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

    }
}
