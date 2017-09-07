// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using System;

namespace Pomelo.Extensions.Caching.MySql
{
	/// <summary>
	/// Configuration options for <see cref="MySqlCache"/>.
	/// </summary>
	public class MySqlCacheOptions : IOptions<MySqlCacheOptions>
	{
		/// <summary>
		/// An abstraction to represent the clock of a machine in order to enable unit testing.
		/// </summary>
		public ISystemClock SystemClock { get; set; }

		/// <summary>
		/// The periodic interval to scan and delete expired items in the cache. Default is 30 minutes.
		/// </summary>
		public TimeSpan? ExpiredItemsDeletionInterval { get; set; }

		private string _connectionString;

		/// <summary>
		/// The connection string to the database.
		/// Can be used when ReadConnection and WriteConnection are the same.
		/// </summary>
		public string ConnectionString
		{
			get => _connectionString;
			set
			{
				_connectionString = value;

				if (ReadConnectionString == null)
					ReadConnectionString = value;
				if (WriteConnectionString == null)
					WriteConnectionString = value;
			}
		}

		/// <summary>
		/// The connection string to the database used for reading data.
		/// </summary>
		public string ReadConnectionString { get; set; }

		/// <summary>
		/// The connection string to the database used for writing data.
		/// </summary>
		public string WriteConnectionString { get; set; }

		/// <summary>
		/// The schema name of the table.
		/// </summary>
		public string SchemaName { get; set; }

		/// <summary>
		/// Name of the table where the cache items are stored.
		/// </summary>
		public string TableName { get; set; }

		/// <summary>
		/// The default sliding expiration set for a cache entry if neither Absolute or SlidingExpiration has been set explicitly.
		/// By default, its 20 minutes.
		/// </summary>
		public TimeSpan DefaultSlidingExpiration { get; set; } = TimeSpan.FromMinutes(20);

		MySqlCacheOptions IOptions<MySqlCacheOptions>.Value
		{
			get
			{
				//correct empty conn strings if possible more inteligently
				WriteConnectionString = WriteConnectionString ?? ConnectionString;
				ReadConnectionString = (ReadConnectionString ?? ConnectionString) ?? WriteConnectionString;

				return this;
			}
		}
	}
}