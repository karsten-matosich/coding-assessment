using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using KarstenAssessmentApi;
using Xunit;

namespace KarstenAssessmentApi.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
  public string TestDbPath { get; }

  public CustomWebApplicationFactory()
  {
    var tempDir = Path.GetTempPath();
    TestDbPath = Path.Combine(tempDir, $"test_db_{Guid.NewGuid()}.db");
    Directory.CreateDirectory(Path.GetDirectoryName(TestDbPath)!);
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    base.ConfigureWebHost(builder);

    builder.UseEnvironment("Testing");

    // Inject per-test configuration so tests can run in parallel safely.
    builder.ConfigureAppConfiguration((context, configBuilder) =>
    {
      var settings = new Dictionary<string, string?>
      {
        ["SQLITE_PATH"] = TestDbPath
      };
      configBuilder.AddInMemoryCollection(settings);
    });
  }

  protected override void Dispose(bool disposing)
  {
    if (disposing && File.Exists(TestDbPath))
    {
      try { File.Delete(TestDbPath); } catch { }
    }
    base.Dispose(disposing);
  }
}

public abstract class TestBase : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
  protected readonly HttpClient Client;
  protected readonly string ConnectionString;

  protected TestBase(CustomWebApplicationFactory factory)
  {
    ConnectionString = $"Data Source={factory.TestDbPath}";
    Client = factory.CreateClient();
  }

  protected void ExecuteDbCommand(Action<SqliteCommand> action)
  {
    using var connection = new SqliteConnection(ConnectionString);
    connection.Open();
    using var cmd = connection.CreateCommand();
    action(cmd);
  }

  protected void CreateAccountsTable()
  {
    ExecuteDbCommand(cmd =>
    {
      cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS accounts (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          name TEXT NOT NULL,
          account_number TEXT NOT NULL UNIQUE,
          balance DECIMAL(10,2) NOT NULL
        );";
      cmd.ExecuteNonQuery();
    });
  }

  protected void CreateTransactionsTable()
  {
    ExecuteDbCommand(cmd =>
    {
      cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS transactions (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          account_id INTEGER NOT NULL,
          transaction_upload_id INTEGER,
          amount DECIMAL(10,2) NOT NULL,
          transaction_date TEXT NOT NULL,
          direction TEXT NOT NULL,
          external_transaction_id TEXT,
          FOREIGN KEY (account_id) REFERENCES accounts(id)
        );";
      cmd.ExecuteNonQuery();
    });
  }

  protected void CreateTransactionUploadsTable()
  {
    ExecuteDbCommand(cmd =>
    {
      cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS transaction_uploads (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          upload_date TEXT NOT NULL,
          file_name TEXT NOT NULL,
          file_size INTEGER NOT NULL,
          incoming_transaction_count INTEGER NOT NULL,
          outgoing_transaction_count INTEGER NOT NULL,
          status TEXT NOT NULL,
          error_message TEXT
        );";
      cmd.ExecuteNonQuery();
    });
  }

  protected void CreateFailedTransactionImportsTable()
  {
    ExecuteDbCommand(cmd =>
    {
      cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS failed_transaction_imports (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          transaction_upload_id INTEGER NOT NULL,
          external_transaction_id TEXT NOT NULL,
          error_message TEXT NOT NULL,
          csv_row_value TEXT NOT NULL,
          FOREIGN KEY (transaction_upload_id) REFERENCES transaction_uploads(id)
        );";
      cmd.ExecuteNonQuery();
    });
  }

  protected void ClearAllTables()
  {
    ExecuteDbCommand(cmd =>
    {
      // Delete from tables that exist (ignore errors if table doesn't exist)
      try { cmd.CommandText = "DELETE FROM failed_transaction_imports;"; cmd.ExecuteNonQuery(); } catch { }
      try { cmd.CommandText = "DELETE FROM transactions;"; cmd.ExecuteNonQuery(); } catch { }
      try { cmd.CommandText = "DELETE FROM transaction_uploads;"; cmd.ExecuteNonQuery(); } catch { }
      try { cmd.CommandText = "DELETE FROM accounts;"; cmd.ExecuteNonQuery(); } catch { }
    });
  }

  protected void InsertTestAccount(int id, string name, string accountNumber, decimal balance)
  {
    ExecuteDbCommand(cmd =>
    {
      cmd.CommandText = $"INSERT INTO accounts (id, name, account_number, balance) VALUES ({id}, '{name}', '{accountNumber}', {balance});";
      cmd.ExecuteNonQuery();
    });
  }

  protected MultipartFormDataContent CreateCsvMultipartContent(string csvContent, string fileName = "test.csv")
  {
    var content = new MultipartFormDataContent();
    var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
    content.Add(fileContent, "file", fileName);
    return content;
  }

  protected async Task<JsonElement> AssertSuccessResponse(HttpResponseMessage response, HttpStatusCode expectedStatus = HttpStatusCode.OK)
  {
    response.StatusCode.Should().Be(expectedStatus);
    var jsonElement = await response.Content.ReadFromJsonAsync<JsonElement>();
    if (jsonElement.ValueKind == JsonValueKind.Undefined || jsonElement.ValueKind == JsonValueKind.Null)
    {
      throw new InvalidOperationException("Response body is null");
    }
    return jsonElement;
  }

  protected async Task<JsonElement> AssertErrorResponse(HttpResponseMessage response, HttpStatusCode expectedStatus)
  {
    response.StatusCode.Should().Be(expectedStatus);
    var jsonElement = await response.Content.ReadFromJsonAsync<JsonElement>();
    if (jsonElement.ValueKind == JsonValueKind.Undefined || jsonElement.ValueKind == JsonValueKind.Null)
    {
      throw new InvalidOperationException("Response body is null");
    }
    return jsonElement;
  }

  protected int GetTableCount(string tableName)
  {
    int count = 0;
    ExecuteDbCommand(cmd =>
    {
      cmd.CommandText = $"SELECT COUNT(*) FROM {tableName};";
      count = Convert.ToInt32(cmd.ExecuteScalar());
    });
    return count;
  }

  public void Dispose() => Client.Dispose();
}

