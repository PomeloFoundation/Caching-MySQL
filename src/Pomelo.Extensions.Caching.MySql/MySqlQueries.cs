// Copyright (c) Pomelo Foundation. All rights reserved.
// Licensed under the MIT License

namespace Pomelo.Extensions.Caching.MySql
{
	internal class MySqlQueries
	{
		//private const string TableInfoFormat =
		//	"SELECT TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE " +
		//	"FROM INFORMATION_SCHEMA.TABLES " +
		//	"WHERE TABLE_SCHEMA = '{0}' " +
		//	"AND TABLE_NAME = '{1}'";

		private const string UpdateCacheItemFormat =
		"UPDATE {0} " +
		"SET " + Columns.Names.ExpiresAtTime + " = " +
			"(CASE " +
				"WHEN TIME_TO_SEC(TIMEDIFF(" + Columns.Names.AbsoluteExpiration + ", @UtcNow)) <= " + Columns.Names.SlidingExpirationInSeconds + " " +
					"THEN " + Columns.Names.AbsoluteExpiration + " " +
				"ELSE " +
					"DATE_ADD(@UtcNow, INTERVAL " + Columns.Names.SlidingExpirationInSeconds + " SECOND) " +
			"END) " +
		"WHERE " + Columns.Names.CacheItemId + " = @Id " +
		"AND @UtcNow <= " + Columns.Names.ExpiresAtTime + " " +
		"AND " + Columns.Names.SlidingExpirationInSeconds + " IS NOT NULL " +
		"AND (" + Columns.Names.AbsoluteExpiration + " IS NULL OR " + Columns.Names.AbsoluteExpiration + " <> " + Columns.Names.ExpiresAtTime + ");";

		private const string GetCacheItemFormat =
			"SELECT Id, " + Columns.Names.ExpiresAtTime + ", " + Columns.Names.SlidingExpirationInSeconds + ", " + Columns.Names.AbsoluteExpiration + ", " + Columns.Names.CacheItemValue + " " +
			"FROM {0} WHERE " + Columns.Names.CacheItemId + " = @Id AND @UtcNow <= " + Columns.Names.ExpiresAtTime + ";";

		private const string SetCacheItemFormat =
			"SET @ExpiresAtTime = " +
			"(CASE " +
					"WHEN (@SlidingExpirationInSeconds IS NUll) " +
						"THEN @AbsoluteExpiration " +
					"ELSE " +
						"DATE_ADD(@UtcNow, INTERVAL @SlidingExpirationInSeconds SECOND) " +
			"END);" +
			 "INSERT INTO {0} (" + Columns.Names.CacheItemId + ", " + Columns.Names.CacheItemValue + ", " +
				Columns.Names.ExpiresAtTime + ", " + Columns.Names.SlidingExpirationInSeconds + ", " +
				Columns.Names.AbsoluteExpiration + ") " +
			 "VALUES (@Id, @Value, @ExpiresAtTime, @SlidingExpirationInSeconds, @AbsoluteExpiration)" +
			" ON DUPLICATE KEY " +
					  "UPDATE " + Columns.Names.CacheItemValue + " = @Value, " + Columns.Names.ExpiresAtTime + " = @ExpiresAtTime," +
					  Columns.Names.SlidingExpirationInSeconds + " = @SlidingExpirationInSeconds, " + Columns.Names.AbsoluteExpiration + " = @AbsoluteExpiration " +
					  ";";

		private const string DeleteCacheItemFormat = "DELETE FROM {0} WHERE " + Columns.Names.CacheItemId + " = @Id";

		public const string DeleteExpiredCacheItemsFormat = "DELETE FROM {0} WHERE @UtcNow > " + Columns.Names.ExpiresAtTime;

		public MySqlQueries(string schemaName, string tableName)
		{
			var tableNameWithSchema = string.Format("{0}{1}",
				(string.IsNullOrEmpty(schemaName) ? "" : EscapeIdentifier(DelimitIdentifier(schemaName)) + '.'),
				EscapeIdentifier(DelimitIdentifier(tableName))
			);

			// when retrieving an item, we do an UPDATE first and then a SELECT
			GetCacheItem = string.Format(UpdateCacheItemFormat + GetCacheItemFormat, tableNameWithSchema);
			GetCacheItemWithoutValue = string.Format(UpdateCacheItemFormat, tableNameWithSchema);
			DeleteCacheItem = string.Format(DeleteCacheItemFormat, tableNameWithSchema);
			DeleteExpiredCacheItems = string.Format(DeleteExpiredCacheItemsFormat, tableNameWithSchema);
			SetCacheItem = string.Format(SetCacheItemFormat, tableNameWithSchema);
			//TableInfo = string.Format(TableInfoFormat, EscapeLiteral(schemaName), EscapeLiteral(tableName));
		}

		//public string TableInfo { get; }

		public string GetCacheItem { get; }

		public string GetCacheItemWithoutValue { get; }

		public string SetCacheItem { get; }

		public string DeleteCacheItem { get; }

		public string DeleteExpiredCacheItems { get; }

		// From EF's MySqlServerQuerySqlGenerator
		private string DelimitIdentifier(string identifier)
		{
			return identifier;
		}

		private string EscapeLiteral(string literal)
		{
			return literal.Replace("'", "''");
		}

		private string EscapeIdentifier(string identifier)
		{
			return $"`{identifier}`";
		}
	}
}
