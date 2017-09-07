// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using Pomelo.Data.MySql;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Pomelo.Extensions.Caching.MySql
{
	internal class DatabaseOperations : IDatabaseOperations
	{
		/// <summary>
		/// Since there is no specific exception type representing a 'duplicate key' error, we are relying on
		/// the following message number which represents the following text in MySql Server database.
		///     "Violation of %ls constraint '%.*ls'. Cannot insert duplicate key in object '%.*ls'.
		///     The duplicate key value is %ls."
		/// You can find the list of system messages by executing the following query:
		/// "SELECT * FROM sys.messages WHERE [text] LIKE '%duplicate%'"
		/// </summary>
		private const int DuplicateKeyErrorId = (int)MySqlErrorCode.DuplicateKey;

		protected const string GetTableSchemaErrorText =
			"Could not retrieve information of table with schema '{0}' and " +
			"name '{1}'. Make sure you have the table setup and try again. " +
			"Connection string: {2}";

		public DatabaseOperations(
			string readConnectionString, string writeConnectionString, string schemaName, string tableName, ISystemClock systemClock)
		{
			ReadConnectionString = readConnectionString;
			WriteConnectionString = writeConnectionString;
			SchemaName = schemaName;
			TableName = tableName;
			SystemClock = systemClock;
			MySqlQueries = new MySqlQueries(schemaName, tableName);
		}

		protected MySqlQueries MySqlQueries { get; }

		protected string ReadConnectionString { get; }
		protected string WriteConnectionString { get; }

		protected string SchemaName { get; }

		protected string TableName { get; }

		protected ISystemClock SystemClock { get; }

		public void DeleteCacheItem(string key)
		{
			using (var connection = new MySqlConnection(WriteConnectionString))
			{
				using (var command = new MySqlCommand(MySqlQueries.DeleteCacheItem, connection))
				{
					command.Parameters.AddCacheItemId(key);

					connection.Open();

					command.ExecuteNonQuery();
				}
			}
		}

		public async Task DeleteCacheItemAsync(string key)
		{
			using (var connection = new MySqlConnection(WriteConnectionString))
			{
				using (var command = new MySqlCommand(MySqlQueries.DeleteCacheItem, connection))
				{
					command.Parameters.AddCacheItemId(key);

					await connection.OpenAsync();

					await command.ExecuteNonQueryAsync();
				}
			}
		}

		public virtual byte[] GetCacheItem(string key)
		{
			return GetCacheItem(key, includeValue: true);
		}

		public virtual async Task<byte[]> GetCacheItemAsync(string key)
		{
			return await GetCacheItemAsync(key, includeValue: true);
		}

		public void RefreshCacheItem(string key)
		{
			GetCacheItem(key, includeValue: false);
		}

		public async Task RefreshCacheItemAsync(string key)
		{
			await GetCacheItemAsync(key, includeValue: false);
		}

		public int DeleteExpiredCacheItems()
		{
			var utcNow = SystemClock.UtcNow;

			using (var connection = new MySqlConnection(WriteConnectionString))
			{
				using (var command = new MySqlCommand(MySqlQueries.DeleteExpiredCacheItems, connection))
				{
					command.Parameters.AddWithValue("UtcNow", MySqlDbType.DateTime, utcNow.UtcDateTime);

					connection.Open();

					var affectedRowCount = command.ExecuteNonQuery();
					return affectedRowCount;
				}
			}
		}

		public async Task<int> DeleteExpiredCacheItemsAsync()
		{
			var utcNow = SystemClock.UtcNow;

			using (var connection = new MySqlConnection(WriteConnectionString))
			{
				using (var command = new MySqlCommand(MySqlQueries.DeleteExpiredCacheItems, connection))
				{
					command.Parameters.AddWithValue("UtcNow", MySqlDbType.DateTime, utcNow.UtcDateTime);

					await connection.OpenAsync();

					var affectedRowCount = await command.ExecuteNonQueryAsync();
					return affectedRowCount;
				}
			}
		}

		public virtual void SetCacheItem(string key, byte[] value, DistributedCacheEntryOptions options)
		{
			var utcNow = SystemClock.UtcNow;

			var absoluteExpiration = GetAbsoluteExpiration(utcNow, options);
			ValidateOptions(options.SlidingExpiration, absoluteExpiration);
			var _absoluteExpiration = absoluteExpiration?.DateTime;

			using (var connection = new MySqlConnection(WriteConnectionString))
			{
				using (var upsertCommand = new MySqlCommand(MySqlQueries.SetCacheItem, connection))
				{
					upsertCommand.Parameters
						.AddCacheItemId(key)
						.AddCacheItemValue(value)
						.AddSlidingExpirationInSeconds(options.SlidingExpiration)
						.AddAbsoluteExpiration(_absoluteExpiration)
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

		public virtual async Task SetCacheItemAsync(string key, byte[] value, DistributedCacheEntryOptions options)
		{
			var utcNow = SystemClock.UtcNow;

			var absoluteExpiration = GetAbsoluteExpiration(utcNow, options);
			ValidateOptions(options.SlidingExpiration, absoluteExpiration);
			var _absoluteExpiration = absoluteExpiration?.DateTime;

			using (var connection = new MySqlConnection(WriteConnectionString))
			{
				using (var upsertCommand = new MySqlCommand(MySqlQueries.SetCacheItem, connection))
				{
					upsertCommand.Parameters
						.AddCacheItemId(key)
						.AddCacheItemValue(value)
						.AddSlidingExpirationInSeconds(options.SlidingExpiration)
						.AddAbsoluteExpiration(_absoluteExpiration)
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

		protected virtual byte[] GetCacheItem(string key, bool includeValue)
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
			using (var connection = new MySqlConnection(WriteConnectionString))
			{
				using (var command = new MySqlCommand(query, connection))
				{
					command.Parameters
						.AddCacheItemId(key)
						.AddWithValue("UtcNow", MySqlDbType.DateTime, utcNow.UtcDateTime);

					connection.Open();

					using (var reader = command.ExecuteReader(
						CommandBehavior.SequentialAccess | CommandBehavior.SingleRow | CommandBehavior.SingleResult))
					{
						if (reader.Read())
						{
							/*var id = reader.GetFieldValue<string>(Columns.Indexes.CacheItemIdIndex);

							expirationTime = reader.GetFieldValue<DateTime>(Columns.Indexes.ExpiresAtTimeIndex);

							if (!reader.IsDBNull(Columns.Indexes.SlidingExpirationInSecondsIndex))
							{
								slidingExpiration = TimeSpan.FromSeconds(
									reader.GetFieldValue<long>(Columns.Indexes.SlidingExpirationInSecondsIndex));
							}

							if (!reader.IsDBNull(Columns.Indexes.AbsoluteExpirationIndex))
							{
								absoluteExpiration = reader.GetFieldValue<DateTimeOffset>(
									Columns.Indexes.AbsoluteExpirationIndex);
							}*/

							if (includeValue)
							{
								value = reader.GetFieldValue<byte[]>(Columns.Indexes.CacheItemValueIndex);
							}
						}
						else
						{
							return null;
						}
					}
				}

				return value;
			}
		}

		protected virtual async Task<byte[]> GetCacheItemAsync(string key, bool includeValue)
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
			using (var connection = new MySqlConnection(WriteConnectionString))
			{
				using (var command = new MySqlCommand(query, connection))
				{
					command.Parameters
						.AddCacheItemId(key)
						.AddWithValue("UtcNow", MySqlDbType.DateTime, utcNow.UtcDateTime);

					await connection.OpenAsync();

					using (var reader = await command.ExecuteReaderAsync(
						CommandBehavior.SequentialAccess | CommandBehavior.SingleRow | CommandBehavior.SingleResult))
					{

						if (await reader.ReadAsync())
						{
							/*var id = await reader.GetFieldValueAsync<string>(Columns.Indexes.CacheItemIdIndex);

							expirationTime = await reader.GetFieldValueAsync<DateTime>(
								Columns.Indexes.ExpiresAtTimeIndex);

							if (!await reader.IsDBNullAsync(Columns.Indexes.SlidingExpirationInSecondsIndex))
							{
								slidingExpiration = TimeSpan.FromSeconds(
									await reader.GetFieldValueAsync<long>(Columns.Indexes.SlidingExpirationInSecondsIndex));
							}

							if (!await reader.IsDBNullAsync(Columns.Indexes.AbsoluteExpirationIndex))
							{
								absoluteExpiration = await reader.GetFieldValueAsync<DateTime>(
									Columns.Indexes.AbsoluteExpirationIndex);
							}*/

							if (includeValue)
							{
								value = await reader.GetFieldValueAsync<byte[]>(Columns.Indexes.CacheItemValueIndex);
							}
						}
						else
						{
							return null;
						}
					}
				}
				return value;
			}
		}

		protected bool IsDuplicateKeyException(MySqlException ex)
		{
			if (ex.Data != null)
			{
				if (ex.Data is MySqlError)
					return ex.Data.Cast<MySqlError>().Any(error => error.Code == DuplicateKeyErrorId);
				else if (ex.Number == DuplicateKeyErrorId)
					return true;
			}
			return false;
		}

		protected DateTimeOffset? GetAbsoluteExpiration(DateTimeOffset utcNow, DistributedCacheEntryOptions options)
		{
			// calculate absolute expiration
			DateTimeOffset? absoluteExpiration = null;
			if (options.AbsoluteExpirationRelativeToNow.HasValue)
			{
				absoluteExpiration = utcNow.Add(options.AbsoluteExpirationRelativeToNow.Value);
			}
			else if (options.AbsoluteExpiration.HasValue)
			{
				if (options.AbsoluteExpiration.Value <= utcNow)
				{
					throw new InvalidOperationException("The absolute expiration value must be in the future.");
				}

				absoluteExpiration = options.AbsoluteExpiration.Value;
			}
			return absoluteExpiration;
		}

		protected void ValidateOptions(TimeSpan? slidingExpiration, DateTimeOffset? absoluteExpiration)
		{
			if (!slidingExpiration.HasValue && !absoluteExpiration.HasValue)
			{
				throw new InvalidOperationException("Either absolute or sliding expiration needs " +
					"to be provided.");
			}
		}
	}
}