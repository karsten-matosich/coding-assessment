using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace KarstenAssessmentApi.Tests;

public class AccountsRoutesTests : TestBase
{
  public AccountsRoutesTests(CustomWebApplicationFactory factory) : base(factory)
  {
    InitializeDatabase();
  }

  private void InitializeDatabase()
  {
    CreateAccountsTable();
    CreateTransactionsTable();
    ClearAllTables();
    InsertTestAccount(1, "Test Account", "1", 1000.00m);
  }

  [Fact]
  public async Task Create_Success()
  {
    var request = new { Name = "New Account", AccountNumber = "2" };
    var response = await Client.PostAsJsonAsync("/accounts/create", request);

    var body = await AssertSuccessResponse(response);
    body.GetProperty("message").GetString().Should().Be("Account created successfully");
  }

  [Fact]
  public async Task Create_FailDuplicateAccountNumber()
  {
    var request = new { Name = "Dupe Account", AccountNumber = "1" };
    var response = await Client.PostAsJsonAsync("/accounts/create", request);

    var body = await AssertErrorResponse(response, HttpStatusCode.BadRequest);
    body.GetProperty("message").GetString().Should().Contain("already exists");
  }

  [Fact]
  public async Task Update_Success()
  {
    var request = new { Name = "Updated Account", AccountNumber = "3" };
    var response = await Client.PutAsJsonAsync("/accounts/1/update", request);

    var body = await AssertSuccessResponse(response);
    body.GetProperty("message").GetString().Should().Be("Account updated successfully");

    ExecuteDbCommand(cmd =>
    {
      cmd.CommandText = "SELECT name, account_number, balance FROM accounts WHERE account_number = '3';";
      using var reader = cmd.ExecuteReader();
      reader.Read().Should().BeTrue();
      reader.GetString(0).Should().Be("Updated Account");
      reader.GetString(1).Should().Be("3");
      reader.GetDecimal(2).Should().Be(1000.00m);
    });
  }

  [Fact]
  public async Task Update_FailDuplicateAccountNumber()
  {
    await Client.PostAsJsonAsync("/accounts/create", new { Name = "Second Account", AccountNumber = "2" });

    var updateRequest = new { Name = "Updated Account", AccountNumber = "2" };
    var response = await Client.PutAsJsonAsync("/accounts/1/update", updateRequest);

    var body = await AssertErrorResponse(response, HttpStatusCode.BadRequest);
    body.GetProperty("message").GetString().Should().Contain("already exists");
  }

  [Fact]
  public async Task Update_FailAccountNumberChangeWithTransactions()
  {
    // Create transactions table and insert a transaction for account 1
    CreateTransactionsTable();
    ExecuteDbCommand(cmd =>
    {
      cmd.CommandText = @"
        INSERT INTO transactions (account_id, amount, transaction_date, direction, external_transaction_id)
        VALUES (1, 100.00, '2024-01-01', 'Incoming', 'test_id_1');";
      cmd.ExecuteNonQuery();
    });

    // Try to update account number for account with transactions
    var updateRequest = new { Name = "Updated Account", AccountNumber = "999" };
    var response = await Client.PutAsJsonAsync("/accounts/1/update", updateRequest);

    var body = await AssertErrorResponse(response, HttpStatusCode.BadRequest);
    body.GetProperty("message").GetString().Should().Be("You may not update an account number for accounts with existing transactions.");

    // Verify account number was not changed
    ExecuteDbCommand(cmd =>
    {
      cmd.CommandText = "SELECT account_number FROM accounts WHERE id = 1;";
      using var reader = cmd.ExecuteReader();
      reader.Read().Should().BeTrue();
      reader.GetString(0).Should().Be("1"); // Should still be "1", not "999"
    });
  }

  [Fact]
  public async Task Update_SuccessAccountNumberChangeWithoutTransactions()
  {
    // Account 1 has no transactions, so we should be able to change the account number
    var updateRequest = new { Name = "Updated Account", AccountNumber = "999" };
    var response = await Client.PutAsJsonAsync("/accounts/1/update", updateRequest);

    var body = await AssertSuccessResponse(response);
    body.GetProperty("message").GetString().Should().Be("Account updated successfully");

    // Verify account number was changed
    ExecuteDbCommand(cmd =>
    {
      cmd.CommandText = "SELECT account_number FROM accounts WHERE id = 1;";
      using var reader = cmd.ExecuteReader();
      reader.Read().Should().BeTrue();
      reader.GetString(0).Should().Be("999");
    });
  }

  [Fact]
  public async Task Update_SuccessWhenAccountNumberUnchanged()
  {
    // Create transactions table and insert a transaction for account 1
    CreateTransactionsTable();
    ExecuteDbCommand(cmd =>
    {
      cmd.CommandText = @"
        INSERT INTO transactions (account_id, amount, transaction_date, direction, external_transaction_id)
        VALUES (1, 100.00, '2024-01-01', 'Incoming', 'test_id_1');";
      cmd.ExecuteNonQuery();
    });

    // Update account name but keep account number the same - should succeed
    var updateRequest = new { Name = "Updated Account Name", AccountNumber = "1" };
    var response = await Client.PutAsJsonAsync("/accounts/1/update", updateRequest);

    var body = await AssertSuccessResponse(response);
    body.GetProperty("message").GetString().Should().Be("Account updated successfully");

    // Verify account name was updated but account number stayed the same
    ExecuteDbCommand(cmd =>
    {
      cmd.CommandText = "SELECT name, account_number FROM accounts WHERE id = 1;";
      using var reader = cmd.ExecuteReader();
      reader.Read().Should().BeTrue();
      reader.GetString(0).Should().Be("Updated Account Name");
      reader.GetString(1).Should().Be("1");
    });
  }
}