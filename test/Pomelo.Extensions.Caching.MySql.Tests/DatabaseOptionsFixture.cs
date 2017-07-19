// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace Pomelo.Extensions.Caching.MySql.Tests
{
	public class DatabaseOptionsFixture : IDisposable
	{
		private const string ConnectionStringKey = "ConnectionString";
		private const string SchemaNameKey = "SchemaName";
		private const string TableNameKey = "TableName";

		private readonly string _tableName;
		private readonly string _schemaName;
		private readonly string _connectionString;

		public string TableName => _tableName;

		public Func<Task> FinalCleanup { get; internal set; }

		public string SchemaName => _schemaName;

		public string ConnectionString => _connectionString;

		public DatabaseOptionsFixture()
		{
			// TODO: Figure how to use config.json which requires resolving IApplicationEnvironment which currently
			// fails.

			/*var memoryConfigurationData = new Dictionary<string, string>
			{
				{ ConnectionStringKey, "server=192.168.0.2;user id=SessionTest;password=SessionTest;persistsecurityinfo=True;port=3306;database=SessionTest;SslMode=None;Allow User Variables=True" },
				{ SchemaNameKey, "SessionTest" },
				{ TableNameKey, "CacheTest" },
			};*/

			var configurationBuilder = new ConfigurationBuilder();
			configurationBuilder
				//.AddInMemoryCollection(memoryConfigurationData)
				.AddJsonFile("config.json")
				.AddEnvironmentVariables();

			var configuration = configurationBuilder.Build();
			_tableName = configuration[TableNameKey];
			_schemaName = configuration[SchemaNameKey];
			_connectionString = configuration[ConnectionStringKey];
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
					FinalCleanup?.Invoke();
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
