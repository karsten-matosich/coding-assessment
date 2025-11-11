using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Builder;
using System.Collections.Generic;

namespace KarstenAssessmentApi.Routes;

public static class AccountsRoutes
{
  public static void MapAccountsRoutes(this WebApplication app, string connectionString)
  {
    app.MapGet("/accounts/get_all", () =>
    {
      return DatabaseHelper.HandleDatabaseOperation(() =>
      {
        var accounts = new List<object>();
        DatabaseHelper.ExecuteWithConnection(connectionString, conn =>
        {
          using var cmd = conn.CreateCommand();
          cmd.CommandText = @"SELECT id, name, account_number, balance FROM accounts;";
          using var rdr = cmd.ExecuteReader();
          while (rdr.Read())
          {
            accounts.Add(new
            {
              id = rdr.GetInt32(0),
              name = rdr.GetString(1),
              account_number = rdr.GetString(2),
              balance = rdr.GetDecimal(3)
            });
          }
        });
        return Results.Ok(accounts);
      }, "Error querying accounts");
    });

    app.MapPost("/accounts/create", (CreateAccountRequest request) =>
    {
      return DatabaseHelper.HandleDatabaseOperationWithUniqueConstraint(() =>
      {
        DatabaseHelper.ExecuteWithConnection(connectionString, conn =>
        {
          using var cmd = conn.CreateCommand();
          cmd.CommandText = @"INSERT INTO accounts (name, account_number, balance) VALUES ($name, $accountNumber, 0.00);";
          cmd.Parameters.AddWithValue("$name", request.Name);
          cmd.Parameters.AddWithValue("$accountNumber", request.AccountNumber);
          cmd.ExecuteNonQuery();
        });
        return Results.Ok(new { message = "Account created successfully" });
      }, "Error creating account", $"An account with account number '{request.AccountNumber}' already exists. Account numbers must be unique.");
    });

    app.MapPut("/accounts/{id}/update", (int id, UpdateAccountRequest request) =>
    {
      return DatabaseHelper.HandleDatabaseOperationWithUniqueConstraint(() =>
      {
        var currentAccountNumber = DatabaseHelper.ExecuteWithConnection(connectionString, conn =>
        {
          using var cmd = conn.CreateCommand();
          cmd.CommandText = @"SELECT account_number FROM accounts WHERE id = $id;";
          cmd.Parameters.AddWithValue("$id", id);
          var result = cmd.ExecuteScalar();
          return result == null || result == DBNull.Value ? null : result.ToString();
        });

        if (currentAccountNumber != null && currentAccountNumber != request.AccountNumber)
        {
          var hasTransactions = DatabaseHelper.ExecuteWithConnection(connectionString, conn =>
          {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT COUNT(*) FROM transactions WHERE account_id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            var count = cmd.ExecuteScalar();
            return count != null && Convert.ToInt32(count) > 0;
          });

          if (hasTransactions)
          {
            return Results.BadRequest(new { message = "You may not update an account number for accounts with existing transactions." });
          }
        }

        var currentBalance = DatabaseHelper.ExecuteWithConnection(connectionString, conn =>
        {
          using var cmd = conn.CreateCommand();
          cmd.CommandText = @"SELECT balance FROM accounts WHERE id = $id;";
          cmd.Parameters.AddWithValue("$id", id);
          var result = cmd.ExecuteScalar();
          return result == null || result == DBNull.Value ? 0m : Convert.ToDecimal(result);
        });

        DatabaseHelper.ExecuteWithConnection(connectionString, conn =>
        {
          using var cmd = conn.CreateCommand();
          cmd.CommandText = @"UPDATE accounts SET name = $name, account_number = $accountNumber, balance = $balance WHERE id = $id;";
          cmd.Parameters.AddWithValue("$name", request.Name);
          cmd.Parameters.AddWithValue("$accountNumber", request.AccountNumber);
          cmd.Parameters.AddWithValue("$balance", currentBalance);
          cmd.Parameters.AddWithValue("$id", id);
          cmd.ExecuteNonQuery();
        });

        return Results.Ok(new { message = "Account updated successfully" });
      }, "Error updating account", $"An account with account number '{request.AccountNumber}' already exists. Account numbers must be unique.");
    });
  }
}

public record CreateAccountRequest(string Name, string AccountNumber);
public record UpdateAccountRequest(string Name, string AccountNumber);