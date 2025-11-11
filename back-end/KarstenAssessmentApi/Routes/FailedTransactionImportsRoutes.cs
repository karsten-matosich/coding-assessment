using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Builder;
using System.Collections.Generic;

namespace KarstenAssessmentApi.Routes;

public static class FailedTransactionImportsRoutes
{
  public static void MapFailedTransactionImportsRoutes(this WebApplication app, string connectionString)
  {
    app.MapGet("/failed_transaction_imports/get_all", () =>
    {
      return DatabaseHelper.HandleDatabaseOperation(() =>
      {
        var failedImports = new List<object>();
        DatabaseHelper.ExecuteWithConnection(connectionString, conn =>
        {
          using var cmd = conn.CreateCommand();
          cmd.CommandText = @"SELECT id, transaction_upload_id, external_transaction_id, error_message, csv_row_value 
                            FROM failed_transaction_imports;";
          using var rdr = cmd.ExecuteReader();
          while (rdr.Read())
          {
            failedImports.Add(new
            {
              id = rdr.GetInt32(0),
              transaction_upload_id = rdr.GetInt32(1),
              external_transaction_id = rdr.GetString(2),
              error_message = rdr.GetString(3),
              csv_row_value = rdr.GetString(4)
            });
          }
        });
        return Results.Ok(failedImports);
      }, "Error querying failed transaction imports");
    });
  }
}