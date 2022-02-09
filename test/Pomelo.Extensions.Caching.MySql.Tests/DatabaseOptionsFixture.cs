// Copyright (c) Pomelo Foundation. All rights reserved.
// Licensed under the MIT License

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MySqlConnector;
using System;
using System.Collections.Generic;
using Xunit;

namespace Pomelo.Extensions.Caching.MySql.Tests
{
	public class IgnoreWhenNoSqlSetupFactAttribute : FactAttribute
	{
		internal static bool IsSqlSetupInvalid =>
			string.IsNullOrEmpty(ConfigOptionsFixture.Configuration["ConnectionString"]) &&
			string.IsNullOrEmpty(ConfigOptionsFixture.Configuration["ReadConnectionString"]) &&
			string.IsNullOrEmpty(ConfigOptionsFixture.Configuration["WriteConnectionString"]);

		public IgnoreWhenNoSqlSetupFactAttribute()
		{
			if (IgnoreWhenNoSqlSetupFactAttribute.IsSqlSetupInvalid)
			{
				Skip = "This test requires database server to be setup";
			}
		}
	}

	public class IgnoreWhenNoSqlSetupTheoryAttribute : TheoryAttribute
	{
		public IgnoreWhenNoSqlSetupTheoryAttribute()
		{
			if (IgnoreWhenNoSqlSetupFactAttribute.IsSqlSetupInvalid)
			{
				Skip = "This test requires database server to be setup";
			}
		}
	}

	public class IgnoreWhenNotEnabledCreateDropTableTestingFactAttribute : FactAttribute
	{
		public IgnoreWhenNotEnabledCreateDropTableTestingFactAttribute()
		{
			string temp = ConfigOptionsFixture.Configuration["TestCreateDropTable"];
			var testCreateDropTable = !string.IsNullOrEmpty(temp) && temp.Equals(true.ToString(), StringComparison.InvariantCultureIgnoreCase);

			if (!testCreateDropTable || IgnoreWhenNoSqlSetupFactAttribute.IsSqlSetupInvalid)
			{
				Skip = "CREATE/DROP table test disabled";
			}
		}
	}

	public class ConfigOptionsFixture
	{
		protected const string ReadConnectionStringKey = "ReadConnectionString", WriteConnectionStringKey = "WriteConnectionString", ConnectionStringKey = "ConnectionString";
		protected const string SchemaNameKey = "SchemaName";
		protected const string TableNameKey = "TableName";

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
						.AddInMemoryCollection(new Dictionary<string, string>
						{
							//{ "ConnectionString", "server=127.0.0.1;user id=SessionTest;password=XXXXXXXXXX;persistsecurityinfo=True;port=3306;database=SessionTest;Allow User Variables=True" },
							//{ "ReadConnectionString", "server=127.0.0.1;user id=SessionTestRead;password=XXXXXXXXXX;persistsecurityinfo=True;port=3306;database=SessionTest;Allow User Variables=True" },
							//{ "WriteConnectionString", "server=127.0.0.1;user id=SessionTest;password=XXXXXXXXXX;persistsecurityinfo=True;port=3306;database=SessionTest;Allow User Variables=True" },
							{ "SchemaName", "SessionTest" },
							{ "TableName", "CacheTest" },
							{ "TestCreateDropTable", "false" }
						})
#if DEBUG
						.AddUserSecrets(typeof(DatabaseOptionsFixture).Assembly, true)
#endif
						.AddEnvironmentVariables();

					var configuration = configurationBuilder.Build();
					_configuration = configuration;
				}

				return _configuration;
			}
		}

		public ConfigOptionsFixture()
		{
			Options = new MySqlCacheOptions()
			{
				TableName = Configuration[TableNameKey],
				SchemaName = Configuration[SchemaNameKey],
				ConnectionString = Configuration[ConnectionStringKey],
				ReadConnectionString = Configuration[ReadConnectionStringKey],
				WriteConnectionString = Configuration[WriteConnectionStringKey]
			};
		}
	}

	public class DatabaseOptionsFixture : ConfigOptionsFixture, IDisposable
	{
		public DatabaseOptionsFixture() : base()
		{
			EnsureCreated();
		}

		private void EnsureCreated()
		{
			if (string.IsNullOrEmpty(Options.Value.WriteConnectionString)) return;

			string create_table = new MySqlConfig.Tools.MySqlQueries(Options.Value.SchemaName, Options.Value.TableName).CreateTable;

			using (var connection = new MySqlConnection(Options.Value.WriteConnectionString))
			{
				using (var command = new MySqlCommand(string.Format(create_table, Options.Value.TableName), connection))
				{
					connection.Open();

					command.ExecuteNonQuery();
				}
			}
		}

		private void ClearAllDatabaseEntries()
		{
			if (string.IsNullOrEmpty(Options.Value.WriteConnectionString)) return;

			using (var connection = new MySqlConnection(Options.Value.WriteConnectionString))
			{
				using (var command = new MySqlCommand($"DELETE FROM {Options.Value.TableName}", connection))
				{
					connection.Open();

					command.ExecuteNonQuery();
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
					ClearAllDatabaseEntries();
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
