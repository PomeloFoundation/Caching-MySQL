// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Pomelo.Extensions.Caching.MySqlConfig.Tools
{
	internal class MySqlQueries
	{
		private const string CreateTableFormat =
			// Maximum size of primary key column is 900 bytes (898 bytes from key + 2 additional bytes used by the 
			// Sql Server). In the case where the key is greater than 898 bytes, then it gets truncated.
			// - Add collation to the key column to make it case-sensitive
			"CREATE TABLE `{0}` (" +
				"`Id` varchar(250) NOT NULL," +
				"`AbsoluteExpiration` datetime(6) DEFAULT NULL," +
				"`ExpiresAtTime` datetime(6) NOT NULL," +
				"`SlidingExpirationInSeconds` bigint(20) DEFAULT NULL," +
				"`Value` longblob NOT NULL," +
				"PRIMARY KEY(`Id`)," +
				"KEY `Index_ExpiresAtTime` (`ExpiresAtTime`)" +
			")";

		private const string CreateNonClusteredIndexOnExpirationTimeFormat
			= "CREATE NONCLUSTERED INDEX Index_ExpiresAtTime ON {0}(ExpiresAtTime)";

		private const string TableInfoFormat =
			 "SELECT TABLE_CATALOG, TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE " +
			 "FROM INFORMATION_SCHEMA.TABLES " +
			 "WHERE TABLE_SCHEMA = '{0}' " +
			 "AND TABLE_NAME = '{1}'";

		public MySqlQueries(string databaseName, string tableName)
		{
			if (string.IsNullOrEmpty(databaseName))
			{
				throw new ArgumentException("Database name cannot be empty or null");
			}
			if (string.IsNullOrEmpty(tableName))
			{
				throw new ArgumentException("Table name cannot be empty or null");
			}

			var tableNameWithDatabase = string.Format(
				"{1}", DelimitIdentifier(databaseName), DelimitIdentifier(tableName));
			CreateTable = string.Format(CreateTableFormat, tableNameWithDatabase);
			CreateNonClusteredIndexOnExpirationTime = string.Format(
				CreateNonClusteredIndexOnExpirationTimeFormat,
				tableNameWithDatabase);
			TableInfo = string.Format(TableInfoFormat, EscapeLiteral(databaseName), EscapeLiteral(tableName));
		}

		public string CreateTable { get; }

		public string CreateNonClusteredIndexOnExpirationTime { get; }

		public string TableInfo { get; }

		// From EF's SqlServerQuerySqlGenerator
		private string DelimitIdentifier(string identifier)
		{
			return identifier;
		}

		private string EscapeLiteral(string literal)
		{
			return literal.Replace("'", "''");
		}
	}
}
