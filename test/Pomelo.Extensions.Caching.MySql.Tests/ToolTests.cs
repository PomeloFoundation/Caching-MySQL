using MySqlConnector;
using Pomelo.Extensions.Caching.MySqlConfig.Tools;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Pomelo.Extensions.Caching.MySql.Tests
{
	public class ToolTests : IClassFixture<ConfigOptionsFixture>, IDisposable
	{
		private readonly ConfigOptionsFixture _fixture;

		public ToolTests(ConfigOptionsFixture fixture)
		{
			_fixture = fixture;
		}

		private Program SutSetup(out StringBuilder output, out StringBuilder error)
		{
			var sb_output = new StringBuilder();
			var sw_output = new StringWriter(sb_output);
			var sb_error = new StringBuilder();
			var sw_error = new StringWriter(sb_error);

			Program toolApp = new Program();

			toolApp.Out = sw_output;
			toolApp.Error = sw_error;

			output = sb_output;
			error = sb_error;

			return toolApp;
		}

		private void SutTearDown(Program toolApp)
		{
			toolApp.Out.Flush();
			toolApp.Out.Dispose();

			toolApp.Error.Flush();
			toolApp.Error.Dispose();
		}

		private async Task DropCreatedTable(string temp_tab_name)
		{
			using var connection = new MySqlConnection(_fixture.Options.Value.WriteConnectionString);
			await connection.OpenAsync();
			using var transaction = connection.BeginTransaction();
			string cmd = $"DROP TABLE `{_fixture.Options.Value.SchemaName}`.`{temp_tab_name}`";
			using var command = new MySqlCommand(cmd, connection, transaction);
			await command.ExecuteNonQueryAsync();

			transaction.Commit();
		}

		[Theory]
		[InlineData("-?")]
		[InlineData("-h")]
		[InlineData("--help")]
		public void GeneralHelp(params string[] args)
		{
			// Arrange
			Program toolApp = SutSetup(out StringBuilder output, out StringBuilder error);
			try
			{
				// Act
				int ret_val = toolApp.Run(args);

				// Assert
				Assert.True(output.Length > 0);
				Assert.True(error.Length <= 0);
				string version = toolApp.GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
					?.InformationalVersion;
				Assert.Contains($@"MySQL Server Cache Command Line Tool {version}", output.ToString());
			}
			finally
			{
				SutTearDown(toolApp);
			}
		}

		[Theory]
		[InlineData("create")]
		[InlineData("create", "conn")]
		[InlineData("create", "conn", "dbase")]
		public void Create_NotEnoughParams(params string[] args)
		{
			// Arrange
			Program toolApp = SutSetup(out StringBuilder output, out StringBuilder error);
			try
			{
				// Act
				int ret_val = toolApp.Run(args);

				// Assert
				Assert.True(output.Length > 0);
				Assert.True(error.Length > 0);
				Assert.Equal("Invalid input" + Environment.NewLine, error.ToString());
			}
			finally
			{
				SutTearDown(toolApp);
			}
		}

		[IgnoreWhenNotEnabledCreateDropTableTestingFact]
		public void Create_BadConn()
		{
			// Arrange
			Program toolApp = SutSetup(out StringBuilder output, out StringBuilder error);
			try
			{
				string[] args = new[] { "create", "conn", "database", "table" };

				// Act
				int ret_val = toolApp.Run(args);

				// Assert
				Assert.NotEqual(0, ret_val);
				Assert.True(output.Length <= 0);
				Assert.True(error.Length > 0);
				Assert.Contains("Invalid MySql server connection string 'conn'", error.ToString());
			}
			finally
			{
				SutTearDown(toolApp);
			}
		}

		[IgnoreWhenNotEnabledCreateDropTableTestingFact]
		public async Task Create_Ok()
		{
			// Arrange
			Program toolApp = SutSetup(out StringBuilder output, out StringBuilder error);
			try
			{
				string temp_tab_name = $"{_fixture.Options.Value.TableName}_{Guid.NewGuid():N}";

				string[] args = new[] { "create",
					_fixture.Options.Value.WriteConnectionString,
					_fixture.Options.Value.SchemaName,
					temp_tab_name
				};

				// Act
				int ret_val = toolApp.Run(args);

				// Assert
				Assert.Equal(0, ret_val);
				Assert.True(output.Length > 0);
				Assert.True(error.Length <= 0);
				Assert.Equal("Table and index were created successfully." + Environment.NewLine, output.ToString());

				await DropCreatedTable(temp_tab_name);
			}
			finally
			{
				SutTearDown(toolApp);
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
