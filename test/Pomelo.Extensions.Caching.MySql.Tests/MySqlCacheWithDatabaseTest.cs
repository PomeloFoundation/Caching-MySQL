// Copyright (c) Pomelo Foundation. All rights reserved.
// Licensed under the MIT License

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Internal;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Pomelo.Extensions.Caching.MySql.Tests
{
	public class MySqlCacheWithDatabaseTest : IClassFixture<DatabaseOptionsFixture>, IDisposable
	{
		private DatabaseOptionsFixture _databaseOptionsFixture;

		public MySqlCacheWithDatabaseTest(DatabaseOptionsFixture databaseOptionsFixture)
		{
			_databaseOptionsFixture = databaseOptionsFixture;
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task ReturnsNullValue_ForNonExistingCacheItem()
		{
			// Arrange
			var sqlServerCache = GetCache();

			// Act
			var value = await sqlServerCache.GetAsync("NonExisting");

			// Assert
			Assert.Null(value);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task SetWithAbsoluteExpirationSetInThePast_Throws()
		{
			// Arrange
			var testClock = new TestClock();
			var key = Guid.NewGuid().ToString();
			var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
			var sqlServerCache = GetCache(testClock);

			// Act & Assert
			var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			{
				return sqlServerCache.SetAsync(
					key,
					expectedValue,
					new DistributedCacheEntryOptions().SetAbsoluteExpiration(testClock.UtcNow.AddHours(-1)));
			});
			Assert.Equal("The absolute expiration value must be in the future.", exception.Message);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task SetCacheItem_SucceedsFor_KeyEqualToMaximumSize()
		{
			// Arrange
			// Create a key with the maximum allowed key length. Here a key of length 499 bytes is created.
			var key = new string('a', MySqlParameterCollectionExtensions.CacheItemIdColumnWidth);
			var testClock = new TestClock();
			var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
			var sqlServerCache = GetCache(testClock);

			// Act
			await sqlServerCache.SetAsync(
				key, expectedValue,
				new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));

			// Assert
			var cacheItem = await GetCacheItemFromDatabaseAsync(key);
			Assert.Equal(expectedValue, cacheItem.Value);

			// Act
			await sqlServerCache.RemoveAsync(key);

			// Assert
			var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
			Assert.Null(cacheItemInfo);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task SetCacheItem_FailsFor_KeyGreaterThanMaximumSize()
		{
			// Arrange
			// Create a key which is greater than the maximum length.
			var key = new string('b', MySqlParameterCollectionExtensions.CacheItemIdColumnWidth + 1);
			var testClock = new TestClock();
			var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
			var sqlServerCache = GetCache(testClock);

			Exception exception = null;
			try
			{
				// Act
				await sqlServerCache.SetAsync(
					key, expectedValue,
					new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(30)));

				// Assert
				var cacheItem = await GetCacheItemFromDatabaseAsync(key);
				Assert.Null(cacheItem);
			}
			catch (Exception ex)
			{
				exception = ex;
			}
			if (exception != null)
				Assert.Equal("Data too long for column 'Id' at row 1", exception.Message);
		}

		// Arrange
		[IgnoreWhenNoSqlSetupFact]
		public async Task SetWithSlidingExpiration_ReturnsNullValue_ForExpiredCacheItem()
		{
			// Arrange
			var testClock = new TestClock();
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache(testClock);
			await sqlServerCache.SetAsync(
				key,
				Encoding.UTF8.GetBytes("Hello, World!"),
				new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(10)));

			// set the clock's UtcNow far in future
			testClock.Add(TimeSpan.FromHours(10));

			// Act
			var value = await sqlServerCache.GetAsync(key);

			// Assert
			Assert.Null(value);
		}

		[IgnoreWhenNoSqlSetupTheory]
		[InlineData(5, 15)]
		[InlineData(10, 20)]
		public async Task SetWithSlidingExpiration_ExtendsExpirationTime(int accessItemAt, int expected)
		{
			// Arrange
			var testClock = new TestClock();
			var slidingExpirationWindow = TimeSpan.FromSeconds(10);
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache(testClock);
			var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
			var expectedExpirationTime = testClock.UtcNow.AddSeconds(expected);
			await sqlServerCache.SetAsync(
				key,
				expectedValue,
				new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpirationWindow));

			testClock.Add(TimeSpan.FromSeconds(accessItemAt));
			// Act
			await AssertGetCacheItemFromDatabaseAsync(
				sqlServerCache,
				key,
				expectedValue,
				slidingExpirationWindow,
				absoluteExpiration: null,
				expectedExpirationTime: expectedExpirationTime);
		}

		[IgnoreWhenNoSqlSetupTheory]
		[InlineData(8)]
		[InlineData(50)]
		public async Task SetWithSlidingExpirationAndAbsoluteExpiration_ReturnsNullValue_ForExpiredCacheItem(
			int accessItemAt)
		{
			// Arrange
			var testClock = new TestClock();
			var utcNow = testClock.UtcNow;
			var slidingExpiration = TimeSpan.FromSeconds(5);
			var absoluteExpiration = utcNow.Add(TimeSpan.FromSeconds(20));
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache(testClock);
			var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
			await sqlServerCache.SetAsync(
				key,
				expectedValue,
				// Set both sliding and absolute expiration
				new DistributedCacheEntryOptions()
				.SetSlidingExpiration(slidingExpiration)
				.SetAbsoluteExpiration(absoluteExpiration));

			// Act
			utcNow = testClock.Add(TimeSpan.FromSeconds(accessItemAt)).UtcNow;
			var value = await sqlServerCache.GetAsync(key);

			// Assert
			Assert.Null(value);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task SetWithAbsoluteExpirationRelativeToNow_ReturnsNullValue_ForExpiredCacheItem()
		{
			// Arrange
			var testClock = new TestClock();
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache(testClock);
			await sqlServerCache.SetAsync(
				key,
				Encoding.UTF8.GetBytes("Hello, World!"),
				new DistributedCacheEntryOptions().SetAbsoluteExpiration(relative: TimeSpan.FromSeconds(10)));

			// set the clock's UtcNow far in future
			testClock.Add(TimeSpan.FromHours(10));

			// Act
			var value = await sqlServerCache.GetAsync(key);

			// Assert
			Assert.Null(value);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task SetWithAbsoluteExpiration_ReturnsNullValue_ForExpiredCacheItem()
		{
			// Arrange
			var testClock = new TestClock();
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache(testClock);
			await sqlServerCache.SetAsync(
				key,
				Encoding.UTF8.GetBytes("Hello, World!"),
				new DistributedCacheEntryOptions()
				.SetAbsoluteExpiration(absolute: testClock.UtcNow.Add(TimeSpan.FromSeconds(30))));

			// set the clock's UtcNow far in future
			testClock.Add(TimeSpan.FromHours(10));

			// Act
			var value = await sqlServerCache.GetAsync(key);

			// Assert
			Assert.Null(value);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task ThrowsException_OnNoSlidingOrAbsoluteExpirationOptions()
		{
			// Arrange
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache();
			var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");

			// Act & Assert
			var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			{
				return sqlServerCache.SetAsync(
					key,
					expectedValue,
					new DistributedCacheEntryOptions());
			});
			Assert.Equal("Either absolute or sliding expiration needs to be provided.", exception.Message);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task DoesNotThrowException_WhenOnlyAbsoluteExpirationSupplied_AbsoluteExpirationRelativeToNow()
		{
			// Arrange
			var testClock = new TestClock();
			var absoluteExpirationRelativeToUtcNow = TimeSpan.FromSeconds(10);
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache(testClock);
			var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
			var expectedAbsoluteExpiration = testClock.UtcNow.Add(absoluteExpirationRelativeToUtcNow);

			// Act
			await sqlServerCache.SetAsync(
				key,
				expectedValue,
				new DistributedCacheEntryOptions()
				.SetAbsoluteExpiration(relative: absoluteExpirationRelativeToUtcNow));

			// Assert
			await AssertGetCacheItemFromDatabaseAsync(
				sqlServerCache,
				key,
				expectedValue,
				slidingExpiration: null,
				absoluteExpiration: expectedAbsoluteExpiration,
				expectedExpirationTime: expectedAbsoluteExpiration);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task DoesNotThrowException_WhenOnlyAbsoluteExpirationSupplied_AbsoluteExpiration()
		{
			// Arrange
			var testClock = new TestClock();
			var expectedAbsoluteExpiration = new DateTimeOffset(DateTime.Now.Year + 8/*long in the future*/, 1, 1, 1, 0, 0, TimeSpan.Zero);
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache();
			var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");

			// Act
			await sqlServerCache.SetAsync(
				key,
				expectedValue,
				new DistributedCacheEntryOptions()
				.SetAbsoluteExpiration(absolute: expectedAbsoluteExpiration));

			// Assert
			await AssertGetCacheItemFromDatabaseAsync(
				sqlServerCache,
				key,
				expectedValue,
				slidingExpiration: null,
				absoluteExpiration: expectedAbsoluteExpiration,
				expectedExpirationTime: expectedAbsoluteExpiration);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task SetCacheItem_UpdatesAbsoluteExpirationTime()
		{
			// Arrange
			var testClock = new TestClock();
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache(testClock);
			var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
			var absoluteExpiration = testClock.UtcNow.Add(TimeSpan.FromSeconds(10));

			// Act & Assert
			// Creates a new item
			await sqlServerCache.SetAsync(
				key,
				expectedValue,
				new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpiration));
			await AssertGetCacheItemFromDatabaseAsync(
				sqlServerCache,
				key,
				expectedValue,
				slidingExpiration: null,
				absoluteExpiration: absoluteExpiration,
				expectedExpirationTime: absoluteExpiration);

			// Updates an existing item with new absolute expiration time
			absoluteExpiration = testClock.UtcNow.Add(TimeSpan.FromMinutes(30));
			await sqlServerCache.SetAsync(
				key,
				expectedValue,
				new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpiration));
			await AssertGetCacheItemFromDatabaseAsync(
				sqlServerCache,
				key,
				expectedValue,
				slidingExpiration: null,
				absoluteExpiration: absoluteExpiration,
				expectedExpirationTime: absoluteExpiration);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task SetCacheItem_WithValueLargerThan_DefaultColumnWidth()
		{
			// Arrange
			var testClock = new TestClock();
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache(testClock);
			var expectedValue = new byte[MySqlParameterCollectionExtensions.DefaultValueColumnWidth + 100];
			var absoluteExpiration = testClock.UtcNow.Add(TimeSpan.FromSeconds(10));

			// Act
			// Creates a new item
			await sqlServerCache.SetAsync(
				key,
				expectedValue,
				new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpiration));

			// Assert
			await AssertGetCacheItemFromDatabaseAsync(
				sqlServerCache,
				key,
				expectedValue,
				slidingExpiration: null,
				absoluteExpiration: absoluteExpiration,
				expectedExpirationTime: absoluteExpiration);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task ExtendsExpirationTime_ForSlidingExpiration()
		{
			// Arrange
			var testClock = new TestClock();
			var slidingExpiration = TimeSpan.FromSeconds(10);
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache(testClock);
			var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
			// The operations Set and Refresh here extend the sliding expiration 2 times.
			var expectedExpiresAtTime = testClock.UtcNow.AddSeconds(15);
			await sqlServerCache.SetAsync(
				key,
				expectedValue,
				new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpiration));

			// Act
			testClock.Add(TimeSpan.FromSeconds(5));
			await sqlServerCache.RefreshAsync(key);

			// Assert
			// verify if the expiration time in database is set as expected
			var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
			Assert.NotNull(cacheItemInfo);
			Assert.Equal(slidingExpiration, cacheItemInfo.SlidingExpirationInSeconds);
			Assert.Null(cacheItemInfo.AbsoluteExpiration);
			Assert.Equal(expectedExpiresAtTime, cacheItemInfo.ExpiresAtTime);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task GetItem_SlidingExpirationDoesNot_ExceedAbsoluteExpirationIfSet()
		{
			// Arrange
			var testClock = new TestClock();
			var utcNow = testClock.UtcNow;
			var slidingExpiration = TimeSpan.FromSeconds(5);
			var absoluteExpiration = utcNow.Add(TimeSpan.FromSeconds(20));
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache(testClock);
			var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
			await sqlServerCache.SetAsync(
				key,
				expectedValue,
				// Set both sliding and absolute expiration
				new DistributedCacheEntryOptions()
				.SetSlidingExpiration(slidingExpiration)
				.SetAbsoluteExpiration(absoluteExpiration));

			// Act && Assert
			var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
			Assert.NotNull(cacheItemInfo);
			Assert.Equal(utcNow.AddSeconds(5), cacheItemInfo.ExpiresAtTime);

			// Accessing item at time...
			utcNow = testClock.Add(TimeSpan.FromSeconds(5)).UtcNow;
			await AssertGetCacheItemFromDatabaseAsync(
				sqlServerCache,
				key,
				expectedValue,
				slidingExpiration,
				absoluteExpiration,
				expectedExpirationTime: utcNow.AddSeconds(5));

			// Accessing item at time...
			utcNow = testClock.Add(TimeSpan.FromSeconds(5)).UtcNow;
			await AssertGetCacheItemFromDatabaseAsync(
				sqlServerCache,
				key,
				expectedValue,
				slidingExpiration,
				absoluteExpiration,
				expectedExpirationTime: utcNow.AddSeconds(5));

			// Accessing item at time...
			utcNow = testClock.Add(TimeSpan.FromSeconds(5)).UtcNow;
			// The expiration extension must not exceed the absolute expiration
			await AssertGetCacheItemFromDatabaseAsync(
				sqlServerCache,
				key,
				expectedValue,
				slidingExpiration,
				absoluteExpiration,
				expectedExpirationTime: absoluteExpiration);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task DoestNotExtendsExpirationTime_ForAbsoluteExpiration()
		{
			// Arrange
			var testClock = new TestClock();
			var absoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
			var expectedExpiresAtTime = testClock.UtcNow.Add(absoluteExpirationRelativeToNow);
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache(testClock);
			var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
			await sqlServerCache.SetAsync(
				key,
				expectedValue,
				new DistributedCacheEntryOptions().SetAbsoluteExpiration(absoluteExpirationRelativeToNow));
			testClock.Add(TimeSpan.FromSeconds(25));

			// Act
			var value = await sqlServerCache.GetAsync(key);

			// Assert
			Assert.NotNull(value);
			Assert.Equal(expectedValue, value);

			// verify if the expiration time in database is set as expected
			var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
			Assert.NotNull(cacheItemInfo);
			Assert.Equal(expectedExpiresAtTime, cacheItemInfo.ExpiresAtTime);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task RefreshItem_ExtendsExpirationTime_ForSlidingExpiration()
		{
			// Arrange
			var testClock = new TestClock();
			var slidingExpiration = TimeSpan.FromSeconds(10);
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache(testClock);
			var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
			// The operations Set and Refresh here extend the sliding expiration 2 times.
			var expectedExpiresAtTime = testClock.UtcNow.AddSeconds(15);
			await sqlServerCache.SetAsync(
				key,
				expectedValue,
				new DistributedCacheEntryOptions().SetSlidingExpiration(slidingExpiration));

			// Act
			testClock.Add(TimeSpan.FromSeconds(5));
			await sqlServerCache.RefreshAsync(key);

			// Assert
			// verify if the expiration time in database is set as expected
			var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
			Assert.NotNull(cacheItemInfo);
			Assert.Equal(slidingExpiration, cacheItemInfo.SlidingExpirationInSeconds);
			Assert.Null(cacheItemInfo.AbsoluteExpiration);
			Assert.Equal(expectedExpiresAtTime, cacheItemInfo.ExpiresAtTime);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task GetCacheItem_IsCaseSensitive()
		{
			// Arrange
			var key = Guid.NewGuid().ToString().ToLower(); // lower case
			var sqlServerCache = GetCache();
			await sqlServerCache.SetAsync(
				key,
				Encoding.UTF8.GetBytes("Hello, World!"),
				new DistributedCacheEntryOptions().SetAbsoluteExpiration(relative: TimeSpan.FromHours(1)));

			// Act
			var value = await sqlServerCache.GetAsync(key.ToUpper()); // key made upper case

			// Assert
			Assert.Null(value);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task GetCacheItem_DoesNotTrimTrailingSpaces()
		{
			// Arrange
			var key = string.Format("  {0}  ", Guid.NewGuid()); // with trailing spaces
			var sqlServerCache = GetCache();
			var expectedValue = Encoding.UTF8.GetBytes("Hello, World!");
			await sqlServerCache.SetAsync(
				key,
				expectedValue,
				new DistributedCacheEntryOptions().SetSlidingExpiration(offset: TimeSpan.FromHours(1)));

			// Act
			var value = await sqlServerCache.GetAsync(key);

			// Assert
			Assert.NotNull(value);
			Assert.Equal(expectedValue, value);
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task DeletesCacheItem_OnExplicitlyCalled()
		{
			// Arrange
			var key = Guid.NewGuid().ToString();
			var sqlServerCache = GetCache();
			await sqlServerCache.SetAsync(
				key,
				Encoding.UTF8.GetBytes("Hello, World!"),
				new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(10)));

			// Act
			await sqlServerCache.RemoveAsync(key);

			// Assert
			var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
			Assert.Null(cacheItemInfo);
		}

		[IgnoreWhenNoSqlSetupTheory]
		[InlineData(10, 10)]
		[InlineData(4, 100)]
		public async Task Concurrent_Access(int threadCount, int accessCount)
		{
			var testClock = new TestClock();
			var sqlServerCache = GetCache(testClock);

			// Define the cancellation token.
			CancellationTokenSource source = new CancellationTokenSource();
			CancellationToken token = source.Token;

			List<Task<Tuple<int, string>[]>> tasks = new List<Task<Tuple<int, string>[]>>();
			TaskFactory factory = new TaskFactory(token);
			for (int taskCtr = 0; taskCtr < threadCount; taskCtr++)
			{
				tasks.Add(factory.StartNew(() =>
				{
					var values = new Tuple<int, string>[accessCount];
					for (int ctr = 0; ctr < accessCount; ctr++)
					{
						bool is_even = (ctr % 2 == 0);//is even number

						Tuple<int, string> value = new Tuple<int, string>(
							taskCtr,
							ctr.ToString("D8")//zero padding, string to int requires it
						);
						values[ctr] = value;

						var key = $"{nameof(Concurrent_Access)}_iteration_{value.Item1}_{value.Item2}";
						sqlServerCache.Set(
							key,
							Encoding.UTF8.GetBytes($"{value.Item1}_{value.Item2}"),
							new DistributedCacheEntryOptions()
								//expire eve number later that odd ones
								.SetAbsoluteExpiration(is_even ? TimeSpan.FromSeconds(20) : TimeSpan.FromSeconds(10)));
					}
					return values;
				}, token));
			}
			try
			{
				await factory.ContinueWhenAll(tasks.ToArray(),
					(all_tasks) =>
					{
						//expire odd number, even must stay
						testClock.Add(TimeSpan.FromSeconds(15));

						int sum = 0;
						foreach (var task in all_tasks)
						{
							foreach (var value in task.Result)
							{
								var key = $"{nameof(Concurrent_Access)}_iteration_{value.Item1}_{value.Item2}";
								var value_as_int = int.Parse(value.Item2);
								bool is_even = (value_as_int % 2 == 0);//is even number

								var fetched_bytes = sqlServerCache.Get(key);
								if (is_even)//only even should be fetched
								{
									Assert.NotNull(fetched_bytes);

									var fetched_string = Encoding.UTF8.GetString(fetched_bytes);
									var fetched_tab = fetched_string.Split("_".ToCharArray(), 2);
									var fetched_int = int.Parse(fetched_tab[1]);
									Assert.Equal(fetched_int, value_as_int);
									Assert.Equal(fetched_string, $"{value.Item1}_{value.Item2}");

									sum += value_as_int;
								}
								else
									Assert.Null(fetched_bytes);
							}
						}

						return sum;

					}, token)
					.ContinueWith((fTask) =>
					{
						Console.WriteLine("Sum is {0}.", fTask.Result);
						//counting only even nums:(0+2+4+6+8=20) * 10 => 200
						Assert.Equal(fTask.Result, (Enumerable.Range(0, accessCount).Where(x => x % 2 == 0).Sum()) * threadCount);

						//expiring even numbers - should be none left
						testClock.Add(TimeSpan.FromSeconds(10));
						for (int taskCtr = 0; taskCtr < threadCount; taskCtr++)
						{
							for (int ctr = 0; ctr < accessCount; ctr++)
							{
								var key = $"{nameof(Concurrent_Access)}_iteration_{taskCtr}_{ctr.ToString("D8")}";

								var fetched_bytes = sqlServerCache.Get(key);
								Assert.Null(fetched_bytes);
							}
						}

					}, token);
			}
			finally
			{
				source.Dispose();
			}
		}

		[IgnoreWhenNoSqlSetupTheory]
		[InlineData(100, 200, 1000)]
		[InlineData(5, 2000, 10000)]
		[InlineData(10, 500, 10)]
		public void Profiling(int threadCount, int outerLoop, int innerLoop)
		{
			// Runs several concurrent threads that access an item that periodically expires and is re-created.
			var cache = GetCache();
			string key = "MyKey";

			var options = new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMilliseconds(50));

			var tasks = new List<Task>();
			for (int threads = 0; threads < threadCount; threads++)
			{
				var task = Task.Run(async () =>
				{
					for (int i = 0; i < outerLoop; i++)
					{
						byte[] bytes = await cache.GetAsync(key);
						if (bytes == null)
						{
							// Fake expensive object creation.
							for (int j = 0; j < innerLoop; j++)
							{
							}

							await cache.SetAsync(key, new byte[] { new byte() }, options);
						}
					}
				});
				tasks.Add(task);
			}

			Console.WriteLine("Running");
			Task.WaitAll(tasks.ToArray());
			Console.WriteLine("Done");
		}

		[IgnoreWhenNoSqlSetupFact]
		public async Task IsDuplicateKeyException()
		{
			MySqlCache cache = GetCache();
			var options = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(10));
			string key = "MyKey";
			var expectedValue = Encoding.UTF8.GetBytes("MyValue");

			await cache.SetAsync(key, expectedValue, options);
			
			CacheItemInfo value = new CacheItemInfo
			{
				Id = key,
				Value = expectedValue,
				ExpiresAtTime = DateTimeOffset.Now,
				//AbsoluteExpiration = DateTimeOffset.Now,
				SlidingExpirationInSeconds = TimeSpan.FromSeconds(10)
			};

			var exception = await Assert.ThrowsAsync<MySqlException>(async () =>
			{
				await SetCacheItemFromDatabaseAsync(key, value);
			});
			Assert.NotNull(exception);
			Assert.Equal(1062, exception.Number);

			await cache.SetAsync(key, expectedValue, options);
		}
		
		[IgnoreWhenNoSqlSetupFact]
		public async Task DeleteExpiredCacheItems()
		{
			// Arrange
			var key = Guid.NewGuid().ToString();
			var testClock = new TestClock();
			var sqlServerCache = GetCache(testClock);
			await sqlServerCache.SetAsync(
				key,
				Encoding.UTF8.GetBytes("Small expiration time element"),
				new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMilliseconds(250)));

			// Act
			testClock.Add(TimeSpan.FromMilliseconds(100));

			// Assert
			var cacheItemInfo = await sqlServerCache.GetAsync(key);
			Assert.NotNull(cacheItemInfo);

			// Act
			testClock.Add(TimeSpan.FromMilliseconds(200));

			// Assert
			cacheItemInfo = await sqlServerCache.GetAsync(key);
			Assert.Null(cacheItemInfo);
		}

		private MySqlCache GetCache(ISystemClock testClock = null)
		{
			var options = _databaseOptionsFixture.Options.Value;
			options.SystemClock = testClock ?? new TestClock();
			options.ExpiredItemsDeletionInterval = TimeSpan.FromHours(2);

			return new MySqlCache(new TestMySqlCacheOptions(options));
		}

		private async Task AssertGetCacheItemFromDatabaseAsync(
			MySqlCache cache,
			string key,
			byte[] expectedValue,
			TimeSpan? slidingExpiration,
			DateTimeOffset? absoluteExpiration,
			DateTimeOffset expectedExpirationTime)
		{
			var value = await cache.GetAsync(key);
			Assert.NotNull(value);
			Assert.Equal(expectedValue, value);
			var cacheItemInfo = await GetCacheItemFromDatabaseAsync(key);
			Assert.NotNull(cacheItemInfo);
			Assert.Equal(slidingExpiration, cacheItemInfo.SlidingExpirationInSeconds);
			Assert.Equal(absoluteExpiration, cacheItemInfo.AbsoluteExpiration);
			Assert.Equal(expectedExpirationTime, cacheItemInfo.ExpiresAtTime);
		}

		private async Task<CacheItemInfo> GetCacheItemFromDatabaseAsync(string key)
		{
			using (var connection = new MySqlConnection(_databaseOptionsFixture.Options.Value.ReadConnectionString))
			{
				var command = new MySqlCommand(
					"SELECT Id, Value, ExpiresAtTime, SlidingExpirationInSeconds, AbsoluteExpiration " +
					$"FROM {_databaseOptionsFixture.Options.Value.TableName} WHERE Id = @Id",
					connection);
				command.Parameters.AddWithValue("Id", key);

				await connection.OpenAsync();

				var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);

				// NOTE: The following code is made to run on Mono as well because of which
				// we cannot use GetFieldValueAsync etc.
				if (await reader.ReadAsync())
				{
					var cacheItemInfo = new CacheItemInfo
					{
						Id = key,
						Value = (byte[])reader[1],
						ExpiresAtTime = DateTimeOffset.Parse(reader[2].ToString(), System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat,
						System.Globalization.DateTimeStyles.AssumeUniversal)
					};

					if (!await reader.IsDBNullAsync(3))
					{
						cacheItemInfo.SlidingExpirationInSeconds = TimeSpan.FromSeconds(reader.GetInt64(3));
					}

					if (!await reader.IsDBNullAsync(4))
					{
						cacheItemInfo.AbsoluteExpiration = DateTimeOffset.Parse(reader[4].ToString(), System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat,
							System.Globalization.DateTimeStyles.AssumeUniversal);
					}

					return cacheItemInfo;
				}
				else
				{
					return null;
				}
			}
		}

		private async Task<bool> SetCacheItemFromDatabaseAsync(string key, CacheItemInfo value, CancellationToken token = default(CancellationToken))
		{
			using (var connection = new MySqlConnection(_databaseOptionsFixture.Options.Value.WriteConnectionString))
			{
				var command = new MySqlCommand(
					$"INSERT INTO {_databaseOptionsFixture.Options.Value.TableName} " +
					"(Id, Value, ExpiresAtTime, SlidingExpirationInSeconds, AbsoluteExpiration) " +
					"VALUES (@Id, @Value, @ExpiresAtTime, @SlidingExpirationInSeconds, @AbsoluteExpiration)",
					connection);
				command.Parameters.AddWithValue("Id", value.Id);
				command.Parameters.AddWithValue("Value", value.Value);
				command.Parameters.AddWithValue("ExpiresAtTime", value.ExpiresAtTime);
				command.Parameters.AddWithValue("SlidingExpirationInSeconds", value.SlidingExpirationInSeconds);
				command.Parameters.AddWithValue("AbsoluteExpiration", value.AbsoluteExpiration);

				await connection.OpenAsync(token);

				var changed_rows = await command.ExecuteNonQueryAsync(token);
				return changed_rows > 0;
			}
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects).


					//ClearAllDatabaseEntriesAsync().Wait();
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~MySqlCacheWithDatabaseTest() {
		//   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
		//   Dispose(false);
		// }

		// This code added to correctly implement the disposable pattern.
		public void Dispose()
		{
			// Do not change this code. Put cleanup code in Dispose(bool disposing) above.
			Dispose(true);
			// TODO: uncomment the following line if the finalizer is overridden above.
			// GC.SuppressFinalize(this);
		}
		#endregion
	}
}
