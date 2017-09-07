// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Pomelo.Extensions.Caching.MySql.Tests
{
	public class MySqlCacheServicesExtensionsTest
	{
		[Fact]
		public void AddDistributedSqlServerCache_AddsAsSingleRegistrationService()
		{
			// Arrange
			var services = new ServiceCollection();

			// Act
			MySqlServerCachingServicesExtensions.AddMySqlCacheServices(services);

			// Assert
			Assert.Equal(1, services.Count);
			var serviceDescriptor = services[0];
			Assert.Equal(typeof(IDistributedCache), serviceDescriptor.ServiceType);
			Assert.Equal(typeof(MySqlCache), serviceDescriptor.ImplementationType);
			Assert.Equal(ServiceLifetime.Singleton, serviceDescriptor.Lifetime);
		}

		[Fact]
		public void AddDistributedSqlServerCache_ReplacesPreviouslyUserRegisteredServices()
		{
			// Arrange
			var services = new ServiceCollection();
			services.AddScoped(typeof(IDistributedCache), sp => Mock.Of<IDistributedCache>());

			// Act
			services.AddDistributedMySqlCache(options =>
			{
				options.ReadConnectionString = "FakeRead";
				options.WriteConnectionString = "FakeWrite";
				options.SchemaName = "Fake";
				options.TableName = "Fake";
			});

			// Assert
			var serviceProvider = services.BuildServiceProvider();

			var distributedCache = services.FirstOrDefault(desc => desc.ServiceType == typeof(IDistributedCache));

			Assert.NotNull(distributedCache);
			Assert.Equal(ServiceLifetime.Scoped, distributedCache.Lifetime);
			Assert.IsType<MySqlCache>(serviceProvider.GetRequiredService<IDistributedCache>());
		}

		[Theory]
		[InlineData("FakeReadConnStr", "FakeWriteConnStr", "FakeConnStr")]
		[InlineData(null, "FakeWriteConnStr", "FakeConnStr")]
		[InlineData("FakeReadConnStr", null, "FakeConnStr")]
		[InlineData("FakeReadConnStr", "FakeWriteConnStr", null)]
		[InlineData("FakeReadConnStr", null, null)]
		[InlineData(null, "FakeWriteConnStr", null)]
		[InlineData(null, null, "FakeConnStr")]
		public void AddDistributedSqlServerCache_VariousConnectionStrings(string readConnectionString, string writeConnectionString,
			string connectionString)
		{
			// Arrange
			var services = new ServiceCollection();

			// Act
			services.AddDistributedMySqlCache(options =>
			{
				options.ReadConnectionString = readConnectionString;
				options.WriteConnectionString = writeConnectionString;
				options.ConnectionString = connectionString;
				options.SchemaName = "Fake";
				options.TableName = "Fake";
			});

			// Assert
			var serviceProvider = services.BuildServiceProvider();

			var distributedCache = services.FirstOrDefault(desc => desc.ServiceType == typeof(IDistributedCache));

			Assert.NotNull(distributedCache);
			Assert.IsType<MySqlCache>(serviceProvider.GetRequiredService<IDistributedCache>());
		}

		[Theory]
		[InlineData(null, null, null)]
		[InlineData("", "", "")]
		public async Task AddDistributedSqlServerCache_BadOrEmptyConnectionStrings(string readConnectionString, string writeConnectionString,
			string connectionString)
		{
			// Arrange
			var services = new ServiceCollection();

			// Act
			services.AddDistributedMySqlCache(options =>
			{
				options.ReadConnectionString = readConnectionString;
				options.WriteConnectionString = writeConnectionString;
				options.ConnectionString = connectionString;
				options.SchemaName = "Fake";
				options.TableName = "Fake";
			});

			var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
			{
				// Assert
				var serviceProvider = services.BuildServiceProvider();

				Assert.IsType<MySqlCache>(serviceProvider.GetRequiredService<IDistributedCache>());
				return null;
			});
			Assert.Equal("ReadConnectionString and WriteConnectionString and ConnectionString cannot be empty or null at the same time.", exception.Message);
		}

		[Fact]
		public void AddDistributedSqlServerCache_allows_chaining()
		{
			var services = new ServiceCollection();

			Assert.Same(services, services.AddDistributedMySqlCache(_ => { }));
		}
	}
}
