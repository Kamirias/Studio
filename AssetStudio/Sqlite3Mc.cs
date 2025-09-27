using System;
using SQLitePCL;

namespace AssetStudio
{
	internal static class Sqlite3Mc
	{
		public const int SQLITE_OK = raw.SQLITE_OK;
		public const int SQLITE_ROW = raw.SQLITE_ROW;
		public const int SQLITE_DONE = raw.SQLITE_DONE;
		public const int SQLITE_OPEN_READWRITE = raw.SQLITE_OPEN_READWRITE;
		public const int SQLITE_OPEN_CREATE = raw.SQLITE_OPEN_CREATE;

		static Sqlite3Mc()
		{
			Batteries_V2.Init();
		}

		public static sqlite3 Open(string path, int flags = SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE)
		{
			int rc = raw.sqlite3_open_v2(path, out sqlite3 db, flags, null);
			if (rc != SQLITE_OK || db == null)
			{
				var err = db != null ? GetErrMsg(db) : "(no handle)";
				throw new InvalidOperationException($"sqlite3_open_v2('{path}') failed rc={rc} errmsg={err}");
			}
			return db;
		}

		public static void Close(sqlite3 db)
		{
			if (db != null)
			{
				raw.sqlite3_close(db);
			}
		}

		public static string GetErrMsg(sqlite3 db)
		{
			return raw.sqlite3_errmsg(db).utf8_to_string() ?? string.Empty;
		}

		public static int Exec(sqlite3 db, string sql, out string errorMsg)
		{
			int rc = raw.sqlite3_exec(db, sql, null, null, out errorMsg);
			return rc;
		}

		public static int MC_Config(sqlite3 db, string paramName, int value)
		{
			string sql = $"SELECT sqlite3mc_config('{paramName}', {value});";
			return raw.sqlite3_exec(db, sql, null, null, out _);
		}

		public static int Key_SetBytes(sqlite3 db, ReadOnlySpan<byte> key)
		{
			var arr = key.ToArray();
			return raw.sqlite3_key(db, arr);
		}

		public static bool ValidateReadable(sqlite3 db, out string errorMsg)
		{
			int rc = Exec(db, "SELECT name FROM sqlite_master LIMIT 1;", out errorMsg);
			return rc == SQLITE_OK;
		}

		public static void ForEachRow(string sql, sqlite3 db, Action<sqlite3_stmt> rowCallback)
		{
			int rc = raw.sqlite3_prepare_v2(db, sql, out sqlite3_stmt stmt);
			if (rc != SQLITE_OK)
			{
				throw new InvalidOperationException($"prepare failed rc={rc} errmsg={GetErrMsg(db)} sql={sql}");
			}

			try
			{
				while (true)
				{
					rc = raw.sqlite3_step(stmt);
					if (rc == SQLITE_ROW)
					{
						rowCallback(stmt);
					}
					else if (rc == SQLITE_DONE)
					{
						break;
					}
					else
					{
						throw new InvalidOperationException($"step failed rc={rc} errmsg={GetErrMsg(db)}");
					}
				}
			}
			finally
			{
				raw.sqlite3_finalize(stmt);
			}
		}

		public static string ColumnText(sqlite3_stmt stmt, int index)
		{
			return raw.sqlite3_column_text(stmt, index).utf8_to_string();
		}

		public static long ColumnInt64(sqlite3_stmt stmt, int index)
		{
			return raw.sqlite3_column_int64(stmt, index);
		}
	}
}
