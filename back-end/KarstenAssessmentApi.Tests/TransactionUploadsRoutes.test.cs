using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace KarstenAssessmentApi.Tests;

public class TransactionUploadsRoutesTests : TestBase
{
  public TransactionUploadsRoutesTests(CustomWebApplicationFactory factory) : base(factory)
  {
    InitializeDatabase();
  }

  private void InitializeDatabase()
  {
    CreateAccountsTable();
    CreateTransactionsTable();
    CreateTransactionUploadsTable();
    CreateFailedTransactionImportsTable();
    ClearAllTables();
    InsertTestAccount(1, "Test Account 1", "ACC001", 1000.00m);
    InsertTestAccount(2, "Test Account 2", "ACC002", 500.00m);
  }

  [Fact]
  public async Task UploadCsv_Success()
  {
    var csvContent = @"id,account_number,direction,amount,transaction_date
TXN001,ACC001,Incoming,100.50,2024-01-15
TXN002,ACC002,Outgoing,50.25,2024-01-16";

    var content = CreateCsvMultipartContent(csvContent);
    var response = await Client.PostAsync("/transaction_uploads/upload_csv", content);

    var body = await AssertSuccessResponse(response);
    body.GetProperty("message").GetString().Should().Contain("Upload completed");
    body.GetProperty("status").GetString().Should().Be("completed");

    GetTableCount("transactions").Should().Be(2);
    GetTableCount("transaction_uploads").Should().Be(1);
  }

  [Fact]
  public async Task UploadCsv_NoFile()
  {
    var content = new MultipartFormDataContent();
    var response = await Client.PostAsync("/transaction_uploads/upload_csv", content);

    var body = await AssertErrorResponse(response, HttpStatusCode.BadRequest);
    body.GetProperty("message").GetString().Should().Contain("No file uploaded");
  }

  [Fact]
  public async Task UploadCsv_InvalidFileType()
  {
    var content = new MultipartFormDataContent();
    var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("test content"));
    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
    content.Add(fileContent, "file", "test.txt");

    var response = await Client.PostAsync("/transaction_uploads/upload_csv", content);

    var body = await AssertErrorResponse(response, HttpStatusCode.BadRequest);
    body.GetProperty("message").GetString().Should().Contain("File must be a CSV file");
  }

  [Fact]
  public async Task UploadCsv_MissingRequiredColumns()
  {
    var csvContent = @"id,account_number
TXN001,ACC001";

    var content = CreateCsvMultipartContent(csvContent);
    var response = await Client.PostAsync("/transaction_uploads/upload_csv", content);

    var body = await AssertErrorResponse(response, HttpStatusCode.BadRequest);
    body.GetProperty("message").GetString().Should().Contain("Missing required columns");
  }

  [Fact]
  public async Task UploadCsv_InvalidAccountNumber()
  {
    var csvContent = @"id,account_number,direction,amount,transaction_date
TXN001,INVALID_ACC,Incoming,100.50,2024-01-15";

    var content = CreateCsvMultipartContent(csvContent);
    var response = await Client.PostAsync("/transaction_uploads/upload_csv", content);

    var body = await AssertSuccessResponse(response);
    body.GetProperty("status").GetString().Should().Be("completed");

    GetTableCount("failed_transaction_imports").Should().BeGreaterThan(0);
  }

  [Fact]
  public async Task UploadCsv_UpdatesAccountBalances()
  {
    var csvContent = @"id,account_number,direction,amount,transaction_date
TXN001,ACC001,Incoming,100.50,2024-01-15
TXN002,ACC001,Outgoing,25.00,2024-01-16";

    var content = CreateCsvMultipartContent(csvContent);
    var response = await Client.PostAsync("/transaction_uploads/upload_csv", content);

    response.StatusCode.Should().Be(HttpStatusCode.OK);

    ExecuteDbCommand(cmd =>
    {
      cmd.CommandText = "SELECT balance FROM accounts WHERE account_number = 'ACC001';";
      var balance = Convert.ToDecimal(cmd.ExecuteScalar());
      balance.Should().Be(1075.50m);
    });
  }

  [Fact]
  public async Task UploadCsv_HandlesDuplicateTransactions()
  {
    var csvContent1 = @"id,account_number,direction,amount,transaction_date
TXN001,ACC001,Incoming,100.50,2024-01-15";

    var content1 = CreateCsvMultipartContent(csvContent1, "test1.csv");
    await Client.PostAsync("/transaction_uploads/upload_csv", content1);

    var csvContent2 = @"id,account_number,direction,amount,transaction_date
TXN001,ACC001,Incoming,100.50,2024-01-15
TXN002,ACC002,Outgoing,50.25,2024-01-16";

    var content2 = CreateCsvMultipartContent(csvContent2, "test2.csv");
    var response = await Client.PostAsync("/transaction_uploads/upload_csv", content2);

    var body = await AssertSuccessResponse(response);
    body.GetProperty("status").GetString().Should().Be("completed");

    GetTableCount("transactions").Should().Be(2);
  }

  [Fact]
  public async Task UploadCsv_HandlesDifferentDateFormats()
  {
    var csvContent = @"id,account_number,direction,amount,transaction_date
TXN001,ACC001,Incoming,100.50,2024-01-15
TXN002,ACC002,Outgoing,50.25,01/16/2024";

    var content = CreateCsvMultipartContent(csvContent);
    var response = await Client.PostAsync("/transaction_uploads/upload_csv", content);

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    GetTableCount("transactions").Should().Be(2);
  }

  [Fact]
  public async Task UploadCsv_HandlesDirectionAbbreviations()
  {
    var csvContent = @"id,account_number,direction,amount,transaction_date
TXN001,ACC001,I,100.50,2024-01-15
TXN002,ACC002,O,50.25,2024-01-16";

    var content = CreateCsvMultipartContent(csvContent);
    var response = await Client.PostAsync("/transaction_uploads/upload_csv", content);

    response.StatusCode.Should().Be(HttpStatusCode.OK);

    ExecuteDbCommand(cmd =>
    {
      cmd.CommandText = "SELECT direction FROM transactions WHERE external_transaction_id = 'TXN001';";
      var direction = cmd.ExecuteScalar()?.ToString();
      direction.Should().Be("Incoming");
    });
  }
}
