using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace KarstenAssessmentApi.Tests;

public class TransactionsRoutesTests : TestBase
{
  public TransactionsRoutesTests(CustomWebApplicationFactory factory) : base(factory)
  {
    InitializeDatabase();
  }

  private void InitializeDatabase()
  {
    CreateAccountsTable();
    CreateTransactionsTable();
    ClearAllTables();
    InsertTestAccount(1, "Test Account 1", "1", 1000.00m);
    InsertTestAccount(2, "Test Account 2", "2", 500.00m);
  }

  [Fact]
  public async Task BatchCreate_Success()
  {
    var request = new
    {
      Transactions = new[]
      {
        new
        {
          AccountId = 1,
          TransactionUploadId = (int?)null,
          Amount = 100.50m,
          TransactionDate = new DateTime(2024, 1, 15),
          Direction = "Incoming",
          ExternalTransactionId = (string?)"TXN-001"
        },
        new
        {
          AccountId = 2,
          TransactionUploadId = (int?)null,
          Amount = 50.25m,
          TransactionDate = new DateTime(2024, 1, 16),
          Direction = "Outgoing",
          ExternalTransactionId = (string?)"TXN-002"
        }
      }
    };

    var response = await Client.PostAsJsonAsync("/transactions/batch_create", request);

    var body = await AssertSuccessResponse(response);
    body.GetProperty("message").GetString().Should().Contain("Successfully created 2 transactions");

    GetTableCount("transactions").Should().Be(2);
  }

  [Fact]
  public async Task BatchCreate_EmptyArray()
  {
    var request = new
    {
      Transactions = Array.Empty<object>()
    };

    var response = await Client.PostAsJsonAsync("/transactions/batch_create", request);

    var body = await AssertSuccessResponse(response);
    body.GetProperty("message").GetString().Should().Contain("Successfully created 0 transactions");

    GetTableCount("transactions").Should().Be(0);
  }

  [Fact]
  public async Task BatchCreate_Failure_InvalidAccountId()
  {
    var request = new
    {
      Transactions = new[]
      {
        new
        {
          AccountId = 1,
          TransactionUploadId = (int?)null,
          Amount = 100.50m,
          TransactionDate = new DateTime(2025, 1, 1),
          Direction = "Incoming",
          ExternalTransactionId = (string?)"ext-id-1"
        },
        new
        {
          AccountId = 999,
          TransactionUploadId = (int?)null,
          Amount = 50.25m,
          TransactionDate = new DateTime(2025, 1, 1),
          Direction = "Outgoing",
          ExternalTransactionId = (string?)"ext-id-2"
        }
      }
    };

    var response = await Client.PostAsJsonAsync("/transactions/batch_create", request);

    response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    GetTableCount("transactions").Should().Be(0);
  }
}