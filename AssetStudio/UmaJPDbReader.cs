using System;
using System.Collections.Generic;
using System.IO;
using SQLitePCL;

namespace AssetStudio
{
    public static class UmaJPDbReader
    {
        private const int CipherIndex = 3;

        public static void LoadMetaAndPopulate(string metaPath, string dbKeyHex)
        {
            if (string.IsNullOrEmpty(metaPath) || !File.Exists(metaPath))
            {
                throw new FileNotFoundException($"Meta database not found: {metaPath}");
            }
            if (string.IsNullOrEmpty(dbKeyHex))
            {
                throw new ArgumentException("DB key is required to decrypt meta");
            }

            var keyBytes = HexToBytes(dbKeyHex);
            if (keyBytes.Length == 0)
            {
                throw new InvalidDataException("DB key hex parsed to empty byte array");
            }

            sqlite3 db = null;
            try
            {
                db = Sqlite3Mc.Open(metaPath, Sqlite3Mc.SQLITE_OPEN_READWRITE);

                var rcCfg = Sqlite3Mc.MC_Config(db, "cipher", CipherIndex);
                if (rcCfg != Sqlite3Mc.SQLITE_OK)
                {
                    Logger.Verbose($"[UmaJP] sqlite3mc_config returned rc={rcCfg}; continuing");
                }

                var rcKey = Sqlite3Mc.Key_SetBytes(db, keyBytes);
                if (rcKey != Sqlite3Mc.SQLITE_OK)
                {
                    var err = Sqlite3Mc.GetErrMsg(db);
                    throw new InvalidOperationException($"sqlite3_key failed rc={rcKey} errmsg={err}");
                }

                if (!Sqlite3Mc.ValidateReadable(db, out var validateErr))
                {
                    throw new InvalidOperationException($"Validation query failed after key apply: {validateErr}");
                }

                var bundleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Sqlite3Mc.ForEachRow("SELECT h, e FROM a", db, stmt =>
                {
                    var path = Sqlite3Mc.ColumnText(stmt, 0) ?? string.Empty;
                    if (string.IsNullOrEmpty(path))
                    {
                        return;
                    }

                    var keyNum = Sqlite3Mc.ColumnInt64(stmt, 1);
                    var normalized = Normalize(path);
                    bundleMap[normalized] = keyNum.ToString();

                    var fileName = Path.GetFileName(normalized);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        bundleMap[fileName] = keyNum.ToString();
                    }
                });

                if (bundleMap.Count == 0)
                {
                    throw new InvalidDataException("Meta DB opened but no bundle keys were found");
                }

                UmaJPManager.UpdateBundleKeys(bundleMap);
                Logger.Info($"[UmaJP] Loaded {bundleMap.Count} bundle keys from meta");
            }
            finally
            {
                if (db != null)
                {
                    Sqlite3Mc.Close(db);
                }
            }
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }

        private static byte[] HexToBytes(string hex)
        {
            hex = hex.Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hex = hex[2..];
            }

            if (hex.Length % 2 != 0)
            {
                throw new FormatException("Key hex length must be even");
            }

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}
