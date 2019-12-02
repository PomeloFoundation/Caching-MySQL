// Copyright (c) Pomelo Foundation. All rights reserved.
// Licensed under the MIT License

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Extensions.Caching.MySql
{
	/// <summary>
	/// Distributed cache implementation using Microsoft MySql Server database.
	/// </summary>
	public class MySqlCache : IDistributedCache
	{
		private static readonly TimeSpan MinimumExpiredItemsDeletionInterval = TimeSpan.FromMinutes(5);
		private static readonly TimeSpan DefaultExpiredItemsDeletionInterval = TimeSpan.FromMinutes(30);

		private readonly IDatabaseOperations _dbOperations;
		private readonly ISystemClock _systemClock;
		private readonly TimeSpan _expiredItemsDeletionInterval;
		private DateTimeOffset _lastExpirationScan;
		private readonly Func<Task<int>> _deleteExpiredCachedItemsDelegateAsync;
		private readonly Func<int> _deleteExpiredCachedItemsDelegate;
		private readonly TimeSpan _defaultSlidingExpiration;

		public MySqlCache(IOptions<MySqlCacheOptions> options)
		{
			var cacheOptions = options.Value;

			if (string.IsNullOrEmpty(cacheOptions.WriteConnectionString)
				&& string.IsNullOrEmpty(cacheOptions.ConnectionString)
				&& string.IsNullOrEmpty(cacheOptions.ReadConnectionString))
			{
				throw new ArgumentException($"{nameof(cacheOptions.ReadConnectionString)} and {nameof(cacheOptions.WriteConnectionString)}"
					+ $" and {nameof(cacheOptions.ConnectionString)} cannot be empty or null at the same time.");
			}

			if (string.IsNullOrEmpty(cacheOptions.SchemaName))
			{
				throw new ArgumentException(
					$"{nameof(cacheOptions.SchemaName)} cannot be empty or null.");
			}
			if (string.IsNullOrEmpty(cacheOptions.TableName))
			{
				throw new ArgumentException(
					$"{nameof(cacheOptions.TableName)} cannot be empty or null.");
			}
			if (cacheOptions.ExpiredItemsDeletionInterval.HasValue &&
				cacheOptions.ExpiredItemsDeletionInterval.Value < MinimumExpiredItemsDeletionInterval)
			{
				throw new ArgumentException(
					$"{nameof(cacheOptions.ExpiredItemsDeletionInterval)} cannot be less the minimum " +
					$"value of {MinimumExpiredItemsDeletionInterval.TotalMinutes} minutes.");
			}
			if (cacheOptions.DefaultSlidingExpiration <= TimeSpan.Zero)
			{
				throw new ArgumentOutOfRangeException(
					nameof(cacheOptions.DefaultSlidingExpiration),
					cacheOptions.DefaultSlidingExpiration,
					"The sliding expiration value must be positive.");
			}

			_systemClock = cacheOptions.SystemClock ?? new SystemClock();
			_expiredItemsDeletionInterval =
				cacheOptions.ExpiredItemsDeletionInterval ?? DefaultExpiredItemsDeletionInterval;
			_defaultSlidingExpiration = cacheOptions.DefaultSlidingExpiration;

			// MySqlClient library on Mono doesn't have support for DateTimeOffset and also
			// it doesn't have support for apis like GetFieldValue, GetFieldValueAsync etc.
			// So we detect the platform to perform things differently for Mono vs. non-Mono platforms.
			if (PlatformHelper.IsMono)
			{
				_dbOperations = new MonoDatabaseOperations(
					cacheOptions.ReadConnectionString,
					cacheOptions.WriteConnectionString,
					cacheOptions.SchemaName,
					cacheOptions.TableName,
					_systemClock);
			}
			else
			{
				_dbOperations = new DatabaseOperations(
					cacheOptions.ReadConnectionString,
					cacheOptions.WriteConnectionString,
					cacheOptions.SchemaName,
					cacheOptions.TableName,
					_systemClock);
			}
			_deleteExpiredCachedItemsDelegateAsync = _dbOperations.DeleteExpiredCacheItemsAsync;
			_deleteExpiredCachedItemsDelegate = _dbOperations.DeleteExpiredCacheItems;
		}

		public byte[] Get(string key)
		{
			if (key == null)
			{
				throw new ArgumentNullException(nameof(key));
			}

			var value = _dbOperations.GetCacheItem(key);

			ScanForExpiredItemsIfRequired();

			return value;
		}

        public async Task<byte[]> GetAsync(string key, CancellationToken token = default(CancellationToken))
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

			token.ThrowIfCancellationRequested();

            var value = await _dbOperations.GetCacheItemAsync(key, token: token);
			await ScanForExpiredItemsIfRequiredAsync();

			return value;
		}

		public void Refresh(string key)
		{
			if (key == null)
			{
				throw new ArgumentNullException(nameof(key));
			}

			_dbOperations.RefreshCacheItem(key);

			ScanForExpiredItemsIfRequired();
		}

        public async Task RefreshAsync(string key, CancellationToken token = default(CancellationToken))
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

			token.ThrowIfCancellationRequested();

            await _dbOperations.RefreshCacheItemAsync(key, token: token);

			await ScanForExpiredItemsIfRequiredAsync();
		}

		public void Remove(string key)
		{
			if (key == null)
			{
				throw new ArgumentNullException(nameof(key));
			}

			_dbOperations.DeleteCacheItem(key);

			ScanForExpiredItemsIfRequired();
		}

        public async Task RemoveAsync(string key, CancellationToken token = default(CancellationToken))
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

			token.ThrowIfCancellationRequested();

            await _dbOperations.DeleteCacheItemAsync(key, token: token);

			await ScanForExpiredItemsIfRequiredAsync();
		}

		public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
		{
			if (key == null)
			{
				throw new ArgumentNullException(nameof(key));
			}

			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			if (options == null)
			{
				throw new ArgumentNullException(nameof(options));
			}

			//GetOptions(ref options);

			_dbOperations.SetCacheItem(key, value, options);

			ScanForExpiredItemsIfRequired();
		}

        public async Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
			CancellationToken token = default(CancellationToken))
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

			if (value == null)
			{
				throw new ArgumentNullException(nameof(value));
			}

			if (options == null)
			{
				throw new ArgumentNullException(nameof(options));
			}

			token.ThrowIfCancellationRequested();

            //GetOptions(ref options);

            await _dbOperations.SetCacheItemAsync(key, value, options, token);

			await ScanForExpiredItemsIfRequiredAsync();
		}

		// Called by multiple actions to see how long it's been since we last checked for expired items.
		// If sufficient time has elapsed then a scan is initiated on a background task.
		private async Task ScanForExpiredItemsIfRequiredAsync()
		{
			var utcNow = _systemClock.UtcNow;
			// TODO: Multiple threads could trigger this scan which leads to multiple calls to database.
			if ((utcNow - _lastExpirationScan) > _expiredItemsDeletionInterval)
			{
				_lastExpirationScan = utcNow;

				//await Task.Delay(1000);
				//await _deleteExpiredCachedItemsDelegateAsync();

				//Task.Delay(1000);
				await Task.Run(_deleteExpiredCachedItemsDelegate);
			}
		}

		private void ScanForExpiredItemsIfRequired()
		{
			var utcNow = _systemClock.UtcNow;
			if ((utcNow - _lastExpirationScan) > _expiredItemsDeletionInterval)
			{
				_lastExpirationScan = utcNow;
				_deleteExpiredCachedItemsDelegate.Invoke();
			}
		}

		private void GetOptions(ref DistributedCacheEntryOptions options)
		{
			if (!options.AbsoluteExpiration.HasValue
				&& !options.AbsoluteExpirationRelativeToNow.HasValue
				&& !options.SlidingExpiration.HasValue)
			{
				options = new DistributedCacheEntryOptions()
				{
					SlidingExpiration = _defaultSlidingExpiration
				};
			}
		}
	}
}