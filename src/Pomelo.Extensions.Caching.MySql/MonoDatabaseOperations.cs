// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using Pomelo.Data.MySql;
using System.Data;
using System.Threading.Tasks;

namespace Pomelo.Extensions.Caching.MySql
{
	internal class MonoDatabaseOperations : DatabaseOperations
	{
		public MonoDatabaseOperations(
		    string readConnectionString, string writeConnectionString, string schemaName, string tableName, ISystemClock systemClock)
			: base(readConnectionString, writeConnectionString, schemaName, tableName, systemClock)
		{
		}

		protected override byte[] GetCacheItem(string key, bool includeValue)
		{
			var utcNow = SystemClock.UtcNow;

			string query;
			if (includeValue)
			{
				query = MySqlQueries.GetCacheItem;
			}
			else
			{
				query = MySqlQueries.GetCacheItemWithoutValue;
			}

			byte[] value = null;
			//TimeSpan? slidingExpiration = null;
			//DateTimeOffset? absoluteExpiration = null;
			//DateTimeOffset expirationTime;
			using (var connection = new MySqlConnection(ReadConnectionString))
			{
				using (var command = new MySqlCommand(query, connection))
				{
					command.Parameters
						.AddCacheItemId(key)
						.AddWithValue("UtcNow", MySqlDbType.DateTime, utcNow.UtcDateTime);

					connection.Open();

					using (var reader = command.ExecuteReader(CommandBehavior.SingleRow | CommandBehavior.SingleResult))
					{
						if (reader.Read())
						{
							/*var id = reader.GetString(Columns.Indexes.CacheItemIdIndex);

							expirationTime = DateTimeOffset.Parse(reader[Columns.Indexes.ExpiresAtTimeIndex].ToString());

							if (!reader.IsDBNull(Columns.Indexes.SlidingExpirationInSecondsIndex))
							{
								slidingExpiration = TimeSpan.FromSeconds(
									reader.GetInt64(Columns.Indexes.SlidingExpirationInSecondsIndex));
							}

							if (!reader.IsDBNull(Columns.Indexes.AbsoluteExpirationIndex))
							{
								absoluteExpiration = DateTimeOffset.Parse(
									reader[Columns.Indexes.AbsoluteExpirationIndex].ToString());
							}*/

							if (includeValue)
							{
								value = (byte[])reader[Columns.Indexes.CacheItemValueIndex];
							}
						}
						else
						{
							return null;
						}
					}
				}
			}

			return value;
		}

		protected override async Task<byte[]> GetCacheItemAsync(string key, bool includeValue)
		{
			var utcNow = SystemClock.UtcNow;

			string query;
			if (includeValue)
			{
				query = MySqlQueries.GetCacheItem;
			}
			else
			{
				query = MySqlQueries.GetCacheItemWithoutValue;
			}

			byte[] value = null;
			//TimeSpan? slidingExpiration = null;
			//DateTime? absoluteExpiration = null;
			//DateTime expirationTime;
			using (var connection = new MySqlConnection(ReadConnectionString))
			{
				using (var command = new MySqlCommand(MySqlQueries.GetCacheItem, connection))
				{
					command.Parameters
						.AddCacheItemId(key)
						.AddWithValue("UtcNow", MySqlDbType.DateTime, utcNow.UtcDateTime);

					await connection.OpenAsync();

					using (var reader = await command.ExecuteReaderAsync(
						CommandBehavior.SingleRow | CommandBehavior.SingleResult))
					{
						if (await reader.ReadAsync())
						{
							/*var id = reader.GetString(Columns.Indexes.CacheItemIdIndex);

							expirationTime = DateTime.Parse(reader[Columns.Indexes.ExpiresAtTimeIndex].ToString());

							if (!await reader.IsDBNullAsync(Columns.Indexes.SlidingExpirationInSecondsIndex))
							{
								slidingExpiration = TimeSpan.FromSeconds(
									Convert.ToInt64(reader[Columns.Indexes.SlidingExpirationInSecondsIndex].ToString()));
							}

							if (!await reader.IsDBNullAsync(Columns.Indexes.AbsoluteExpirationIndex))
							{
								absoluteExpiration = DateTime.Parse(
									reader[Columns.Indexes.AbsoluteExpirationIndex].ToString());
							}*/

							if (includeValue)
							{
								value = (byte[])reader[Columns.Indexes.CacheItemValueIndex];
							}
						}
						else
						{
							return null;
						}
					}
				}
			}

			return value;
		}

		public override void SetCacheItem(string key, byte[] value, DistributedCacheEntryOptions options)
		{
			var utcNow = SystemClock.UtcNow;

			var absoluteExpiration = GetAbsoluteExpiration(utcNow, options);
			ValidateOptions(options.SlidingExpiration, absoluteExpiration);

			using (var connection = new MySqlConnection(WriteConnectionString))
			{
				using (var upsertCommand = new MySqlCommand(MySqlQueries.SetCacheItem, connection))
				{
					upsertCommand.Parameters
						.AddCacheItemId(key)
						.AddCacheItemValue(value)
						.AddSlidingExpirationInSeconds(options.SlidingExpiration)
						.AddAbsoluteExpirationMono(absoluteExpiration)
						.AddWithValue("UtcNow", MySqlDbType.DateTime, utcNow.UtcDateTime);

					connection.Open();

					try
					{
						upsertCommand.ExecuteNonQuery();
					}
					catch (MySqlException ex)
					{
						if (IsDuplicateKeyException(ex))
						{
							// There is a possibility that multiple requests can try to add the same item to the cache, in
							// which case we receive a 'duplicate key' exception on the primary key column.
						}
						else
						{
							throw;
						}
					}
				}
			}
		}

		public override async Task SetCacheItemAsync(string key, byte[] value, DistributedCacheEntryOptions options)
		{
			var utcNow = SystemClock.UtcNow;

			var absoluteExpiration = GetAbsoluteExpiration(utcNow, options);
			ValidateOptions(options.SlidingExpiration, absoluteExpiration);

			using (var connection = new MySqlConnection(WriteConnectionString))
			{
				using (var upsertCommand = new MySqlCommand(MySqlQueries.SetCacheItem, connection))
				{
					upsertCommand.Parameters
						.AddCacheItemId(key)
						.AddCacheItemValue(value)
						.AddSlidingExpirationInSeconds(options.SlidingExpiration)
						.AddAbsoluteExpirationMono(absoluteExpiration)
						.AddWithValue("UtcNow", MySqlDbType.DateTime, utcNow.UtcDateTime);

					await connection.OpenAsync();

					try
					{
						await upsertCommand.ExecuteNonQueryAsync();
					}
					catch (MySqlException ex)
					{
						if (IsDuplicateKeyException(ex))
						{
							// There is a possibility that multiple requests can try to add the same item to the cache, in
							// which case we receive a 'duplicate key' exception on the primary key column.
						}
						else
						{
							throw;
						}
					}
				}
			}
		}

		/*public override void DeleteExpiredCacheItems()
		{
			var utcNow = SystemClock.UtcNow;

			using (var connection = new MySqlConnection(WriteConnectionString))
			{
				using (var command = new MySqlCommand(MySqlQueries.DeleteExpiredCacheItems, connection))
				{
					command.Parameters.AddWithValue("UtcNow", MySqlDbType.DateTime, utcNow.UtcDateTime);

					connection.Open();

					var effectedRowCount = command.ExecuteNonQuery();
				}
			}
		}*/
	}
}