// Copyright (c) Pomelo Foundation. All rights reserved.
// Licensed under the MIT License

using Microsoft.Extensions.CommandLineUtils;
using MySqlConnector;
using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Pomelo.Extensions.Caching.MySqlConfig.Tools
{
	/// <summary>
	/// Based (if not entirely) of off https://github.com/dotnet/aspnetcore/blob/main/src/Tools/dotnet-sql-cache/src/Program.cs
	/// </summary>
	public class Program
	{
		private string _connectionString = null;
		private string _databaseName = null;
		private string _tableName = null;

		internal TextWriter Error { get; set; } = Console.Error;
		internal TextWriter Out { get; set; } = Console.Out;

		public Program()
		{
		}

		public static int Main(string[] args)
		{
			var p = new Program();

			return p.Run(args);
		}

		public int Run(string[] args)
		{
			try
			{
				var description = "Creates table and indexes in MySQL Server database " +
					"to be used for distributed caching";

				var cliApp = new CommandLineApplication();
				cliApp.Error = Error;
				cliApp.Out = Out;

				cliApp.FullName = "MySQL Server Cache Command Line Tool";
				cliApp.Name = "dotnet-mysql-cache";
				cliApp.Description = description;
				cliApp.ShortVersionGetter = () =>
				{
					var assembly = typeof(Program).GetTypeInfo().Assembly;
					var infoVersion = assembly
						?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
						?.InformationalVersion;
					return string.IsNullOrWhiteSpace(infoVersion)
						? assembly?.GetName().Version.ToString()
						: infoVersion;
				};
				cliApp.HelpOption("-?|-h|--help");

				cliApp.Command("create", command =>
				{
					command.Error = Error;//internal command Out/Erro are not yet changed, possible shortcomming
					command.Out = Out;

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
							await Error.WriteLineAsync("Invalid input");
							cliApp.ShowHelp(command.Name);
							return 2;
						}

						_connectionString = connectionStringArg.Value;
						_databaseName = databaseNameArg.Value;
						_tableName = tableNameArg.Value;

						return await CreateTableAndIndexes();
					});
				});

				cliApp.Command("script", command =>
				{
					command.Error = Error;//internal command Out/Erro are not yet changed, possible shortcomming
					command.Out = Out;

					command.Description = "Generate creation script";
					var databaseNameArg = command.Argument("[databaseName]", "Name of the database.");
					var tableNameArg = command.Argument("[tableName]", "Name of the table to be created.");
					command.HelpOption("-?|-h|--help");

					command.OnExecute(async () =>
					{
						if (string.IsNullOrEmpty(databaseNameArg.Value)
							|| string.IsNullOrEmpty(tableNameArg.Value))
						{
							await Error.WriteLineAsync("Invalid input");
							cliApp.ShowHelp(command.Name);
							return 2;
						}

						_databaseName = databaseNameArg.Value;
						_tableName = tableNameArg.Value;

						return await GenerateScript();
					});
				});

				// Show help information if no subcommand/option was specified.
				cliApp.OnExecute(() =>
				{
					cliApp.ShowHelp();
					return 2;
				});

				return cliApp.Execute(args);
			}
			catch (Exception ex)
			{
				Error.WriteLine($"An error occurred. {ex.Message}");
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
							Error.WriteLine(
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

						await Out.WriteLineAsync("Table and index were created successfully.");
					}
					catch (Exception ex)
					{
						await Error.WriteLineAsync(
							$"An error occurred while trying to create the table and index. {ex.Message}");
						transaction.Rollback();

						return 1;
					}
				}
			}

			return 0;
		}

		private async Task<int> GenerateScript(CancellationToken token = default(CancellationToken))
		{
			try
			{
				var sqlQueries = new MySqlQueries(_databaseName, _tableName);
				string cmd = sqlQueries.CreateTable;

				await Out.WriteLineAsync($"{Environment.NewLine}{cmd}{Environment.NewLine}");
			}
			catch (Exception ex)
			{
				await Error.WriteLineAsync($"An error occurred while trying to create the table and index. {ex.Message}");

				return 1;
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
	}
}