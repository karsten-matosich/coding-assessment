using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Builder;
using System.Collections.Generic;

namespace KarstenAssessmentApi.Routes;

public static class TransactionsRoutes
{
  public static void MapTransactionsRoutes(this WebApplication app, string connectionString)
  {
    app.MapGet("/transactions/get_all", () =>
    {
      return DatabaseHelper.HandleDatabaseOperation(() =>
      {
        var transactions = new List<object>();
        DatabaseHelper.ExecuteWithConnection(connectionString, conn =>
        {
          using var cmd = conn.CreateCommand();
          cmd.CommandText = @"SELECT id, account_id, transaction_upload_id, amount, transaction_date, direction, external_transaction_id FROM transactions;";
          using var rdr = cmd.ExecuteReader();
          while (rdr.Read())
          {
            transactions.Add(MapTransactionFromReader(rdr));
          }
        });
        return Results.Ok(transactions);
      }, "Error querying transactions");
    });

    app.MapPost("/transactions/batch_create", (BatchCreateTransactionsRequest request) =>
    {
      return DatabaseHelper.HandleDatabaseOperation(() =>
      {
        DatabaseHelper.ExecuteWithTransaction(connectionString, (conn, transaction) =>
        {
          foreach (var trans in request.Transactions)
          {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"INSERT INTO transactions (account_id, transaction_upload_id, amount, transaction_date, direction, external_transaction_id) 
                              VALUES ($accountId, $transactionUploadId, $amount, $transactionDate, $direction, $externalTransactionId);";
            cmd.Parameters.AddWithValue("$accountId", trans.AccountId);
            cmd.Parameters.AddWithValue("$transactionUploadId", trans.TransactionUploadId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$amount", trans.Amount);
            cmd.Parameters.AddWithValue("$transactionDate", trans.TransactionDate);
            cmd.Parameters.AddWithValue("$direction", trans.Direction);
            cmd.Parameters.AddWithValue("$externalTransactionId", trans.ExternalTransactionId ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();
          }
        });
        return Results.Ok(new { message = $"Successfully created {request.Transactions.Count} transactions" });
      }, "Error creating transactions");
    });
  }

  private static object MapTransactionFromReader(SqliteDataReader rdr)
  {
    return new
    {
      id = rdr.GetInt32(0),
      account_id = rdr.GetInt32(1),
      transaction_upload_id = rdr.IsDBNull(2) ? (int?)null : rdr.GetInt32(2),
      amount = rdr.GetDecimal(3),
      transaction_date = rdr.GetDateTime(4),
      direction = rdr.GetString(5),
      external_transaction_id = rdr.IsDBNull(6) ? (string?)null : rdr.GetString(6)
    };
  }
}

public record CreateTransactionRequest(int AccountId, int? TransactionUploadId, decimal Amount, DateTime TransactionDate, string Direction, string? ExternalTransactionId);
public record BatchCreateTransactionsRequest(List<CreateTransactionRequest> Transactions);