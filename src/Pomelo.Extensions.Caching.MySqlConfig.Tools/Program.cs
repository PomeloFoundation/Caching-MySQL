// Copyright (c) Pomelo Foundation. All rights reserved.
// Licensed under the MIT License

using Microsoft.Extensions.CommandLineUtils;
using MySqlConnector;
using System;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Extensions.Caching.MySqlConfig.Tools
{
	public class Program : IDisposable
	{
		private string _connectionString = null;
		private string _databaseName = null;
		private string _tableName = null;

		public Program()
		{
		}

		public static int Main(string[] args)
		{
			using (var p = new Program())
			{
				return p.Run(args);
			}
		}

		public int Run(string[] args)
		{
			try
			{
				var description = "Creates table and indexes in MySQL Server database " +
					"to be used for distributed caching";

				var app = new CommandLineApplication();
				app.FullName = "MySQL Server Cache Command Line Tool";
				app.Name = "dotnet-mysql-cache";
				app.Description = description;
				app.ShortVersionGetter = () =>
				{
					var assembly = typeof(Program).GetTypeInfo().Assembly;
					var infoVersion = assembly
						?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
						?.InformationalVersion;
					return string.IsNullOrWhiteSpace(infoVersion)
						? assembly?.GetName().Version.ToString()
						: infoVersion;
				};
				app.HelpOption("-?|-h|--help");

				app.Command("create", command =>
				{
					command.Description = description;
					var connectionStringArg = command.Argument(
						"[connectionString]",
						"The connection string to connect to the database.");
					var databaseNameArg = command.Argument("[databaseName]", "Name of the database.");
					var tableNameArg = command.Argument("[tableName]", "Name of the table to be created.");
					command.HelpOption("-?|-h|--help");

					command.OnExecute(async () =>
					{
						if (string.IsNullOrEmpty(connectionStringArg.Value)
						|| string.IsNullOrEmpty(databaseNameArg.Value)
						|| string.IsNullOrEmpty(tableNameArg.Value))
						{
							await Console.Error.WriteLineAsync("Invalid input");
							app.ShowHelp();
							return 2;
						}

						_connectionString = connectionStringArg.Value;
						_databaseName = databaseNameArg.Value;
						_tableName = tableNameArg.Value;

						return await CreateTableAndIndexes();
					});
				});

				// Show help information if no subcommand/option was specified.
				app.OnExecute(() =>
				{
					app.ShowHelp();
					return 2;
				});

				return app.Execute(args);
			}
			catch (Exception exception)
			{
				Console.Error.WriteLine($"An error occurred. {exception.Message}");
				return 1;
			}
		}

		private async Task<int> CreateTableAndIndexes(CancellationToken token = default(CancellationToken))
		{
			ValidateConnectionString();

			using (var connection = new MySqlConnection(_connectionString))
			{
				await connection.OpenAsync(token);

				var sqlQueries = new MySqlQueries(_databaseName, _tableName);
				using (var command = new MySqlCommand(sqlQueries.TableInfo, connection))
				{
					using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, token))
					{
						if (await reader.ReadAsync(token))
						{
							Console.Error.WriteLine(
								$"Table '{_tableName}' from database '{_databaseName}' already exists. " +
								"Provide a different table name and try again.");
							return 1;
						}
					}
				}

				using (var transaction = connection.BeginTransaction())
				{
					try
					{
						using (var command = new MySqlCommand(sqlQueries.CreateTable,
							connection, transaction))
						{
							await command.ExecuteNonQueryAsync(token);
						}

						//using (var command = new MySqlCommand(sqlQueries.CreateNonClusteredIndexOnExpirationTime,
						//	connection, transaction))
						//{
						//	await command.ExecuteNonQueryAsync(token);
						//}

						transaction.Commit();

						await Console.Out.WriteLineAsync("Table and index were created successfully.");
					}
					catch (Exception ex)
					{
						await Console.Error.WriteLineAsync(
							$"An error occurred while trying to create the table and index. {ex.Message}");
						transaction.Rollback();

						return 1;
					}
				}
			}

			return 0;
		}

		private void ValidateConnectionString()
		{
			try
			{
				new MySqlConnectionStringBuilder(_connectionString);
			}
			catch (Exception ex)
			{
				throw new ArgumentException(
					$"Invalid MySql server connection string '{_connectionString}'. {ex.Message}", ex);
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
				}

				// TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
				// TODO: set large fields to null.

				disposedValue = true;
			}
		}

		// TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
		// ~Program() {
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