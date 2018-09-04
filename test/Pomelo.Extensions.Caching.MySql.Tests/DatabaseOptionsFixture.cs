// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Pomelo.Data.MySql;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Pomelo.Extensions.Caching.MySql.Tests
{
	public class IgnoreWhenNoSqlSetupFactAttribute : FactAttribute
	{
		public IgnoreWhenNoSqlSetupFactAttribute()
		{
			if (string.IsNullOrEmpty(DatabaseOptionsFixture.Configuration["ConnectionString"]) &&
				string.IsNullOrEmpty(DatabaseOptionsFixture.Configuration["ReadConnectionString"]) &&
				string.IsNullOrEmpty(DatabaseOptionsFixture.Configuration["WriteConnectionString"]))
			{
				Skip = "This test requires database server to be setup";
			}
		}
	}

	public class IgnoreWhenNoSqlSetupTheoryAttribute : TheoryAttribute
	{
		public IgnoreWhenNoSqlSetupTheoryAttribute()
		{
			if (string.IsNullOrEmpty(DatabaseOptionsFixture.Configuration["ConnectionString"]) &&
				string.IsNullOrEmpty(DatabaseOptionsFixture.Configuration["ReadConnectionString"]) &&
				string.IsNullOrEmpty(DatabaseOptionsFixture.Configuration["WriteConnectionString"]))
			{
				Skip = "This test requires database server to be setup";
			}
		}
	}

	public class DatabaseOptionsFixture : IDisposable
	{
		private const string ReadConnectionStringKey = "ReadConnectionString", WriteConnectionStringKey = "WriteConnectionString", ConnectionStringKey = "ConnectionString";
		private const string SchemaNameKey = "SchemaName";
		private const string TableNameKey = "TableName";
		//internal const string SetToNullAfterPreparingConfigSetupForDBTests = "This test requires database server to be setup";

		public IOptions<MySqlCacheOptions> Options { get; private set; }

		private static IConfiguration _configuration;
		internal static IConfiguration Configuration
		{
			get
			{
				if (_configuration == null)
				{
					var configurationBuilder = new ConfigurationBuilder();
					configurationBuilder
						.AddJsonFile("config.json")
						.AddEnvironmentVariables()
#if DEBUG
						.AddUserSecrets(typeof(DatabaseOptionsFixture).Assembly)
#endif
						;
					var configuration = configurationBuilder.Build();
					_configuration = configuration;
				}

				return _configuration;
			}
		}

		public DatabaseOptionsFixture()
		{
			Options = new MySqlCacheOptions()
			{
				TableName = Configuration[TableNameKey],
				SchemaName = Configuration[SchemaNameKey],
				ConnectionString = Configuration[ConnectionStringKey],
				ReadConnectionString = Configuration[ReadConnectionStringKey],
				WriteConnectionString = Configuration[WriteConnectionStringKey]
			};

			EnsureDBCreated().Wait();
		}

		private async Task EnsureDBCreated()
		{
			string create_table = MySqlConfig.Tools.MySqlQueries.CreateTableFormat;

			using (var connection = new MySqlConnection(Options.Value.WriteConnectionString))
			{
				using (var command = new MySqlCommand(string.Format(create_table, Options.Value.TableName), connection))
				{
					await connection.OpenAsync();

					await command.ExecuteNonQueryAsync();
				}
			}
		}

		private async Task ClearAllDatabaseEntriesAsync()
		{
			using (var connection = new MySqlConnection(Options.Value.WriteConnectionString))
			{
				using (var command = new MySqlCommand($"DELETE FROM {Options.Value.TableName}", connection))
				{
					await connection.OpenAsync();

					await command.ExecuteNonQueryAsync();
				}
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
					ClearAllDatabaseEntriesAsync().Wait();
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~DatabaseOptionsFixture() {
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
