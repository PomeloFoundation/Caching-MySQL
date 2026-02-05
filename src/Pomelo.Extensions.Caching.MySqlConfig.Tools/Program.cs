// Copyright (c) Pomelo Foundation. All rights reserved.
// Licensed under the MIT License

using MySqlConnector;
using System;
using System.CommandLine;
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

				var rootCommand = new RootCommand(description);

				// Create command
				var createCommand = new Command("create", description);
				
				var createConnectionStringArg = new Argument<string>("connectionString")
				{
					Description = "The connection string to connect to the database."
				};
				
				var createDatabaseNameOpt = new Option<string>("--databaseName")
				{
					Description = "Name of the database. If not existing or set in connection string."
				};
				createDatabaseNameOpt.Aliases.Add("-d");
				
				var createTableNameArg = new Argument<string>("tableName")
				{
					Description = "Name of the table to be created."
				};
				
				createCommand.Arguments.Add(createConnectionStringArg);
				createCommand.Options.Add(createDatabaseNameOpt);
				createCommand.Arguments.Add(createTableNameArg);
				
				createCommand.SetAction(parseResult =>
				{
					var connectionString = parseResult.GetValue(createConnectionStringArg);
					var databaseName = parseResult.GetValue(createDatabaseNameOpt);
					var tableName = parseResult.GetValue(createTableNameArg);

					if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(tableName))
					{
						Error.WriteLine("Invalid input");
						Error.WriteLine("Usage: create <connectionString> <tableName> [--databaseName <name>]");
						return 2;
					}

					_connectionString = connectionString;
					_databaseName = databaseName;
					_tableName = tableName;

					return CreateTableAndIndexes().GetAwaiter().GetResult();
				});

				// Script command
				var scriptCommand = new Command("script", "Generate creation script");
				
				var scriptDatabaseNameOpt = new Option<string>("--databaseName")
				{
					Description = "Name of the database. If not existing or set in connection string."
				};
				scriptDatabaseNameOpt.Aliases.Add("-d");
				
				var scriptTableNameArg = new Argument<string>("tableName")
				{
					Description = "Name of the table to be created."
				};
				
				scriptCommand.Options.Add(scriptDatabaseNameOpt);
				scriptCommand.Arguments.Add(scriptTableNameArg);
				
				scriptCommand.SetAction(parseResult =>
				{
					var databaseName = parseResult.GetValue(scriptDatabaseNameOpt);
					var tableName = parseResult.GetValue(scriptTableNameArg);

					if (string.IsNullOrEmpty(tableName))
					{
						Error.WriteLine("Invalid input");
						Error.WriteLine("Usage: script <tableName> [--databaseName <name>]");
						return 2;
					}

					_databaseName = databaseName;
					_tableName = tableName;

					return GenerateScript().GetAwaiter().GetResult();
				});

				rootCommand.Subcommands.Add(createCommand);
				rootCommand.Subcommands.Add(scriptCommand);

			// Temporarily redirect Console.Out and Console.Error to custom streams
			// so System.CommandLine can write to them
			var originalOut = Console.Out;
			var originalError = Console.Error;
			try
			{
				Console.SetOut(Out);
				Console.SetError(Error);
				return rootCommand.Parse(args).Invoke();
			}
			finally
			{
				Console.SetOut(originalOut);
				Console.SetError(originalError);
			}
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