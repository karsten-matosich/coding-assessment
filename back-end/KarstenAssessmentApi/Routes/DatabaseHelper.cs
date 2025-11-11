using Microsoft.Data.Sqlite;

namespace KarstenAssessmentApi.Routes;

public static class DatabaseHelper
{
  public static T ExecuteWithConnection<T>(string connectionString, Func<SqliteConnection, T> operation)
  {
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    return operation(conn);
  }

  public static void ExecuteWithConnection(string connectionString, Action<SqliteConnection> operation)
  {
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    operation(conn);
  }

  public static T ExecuteWithTransaction<T>(string connectionString, Func<SqliteConnection, SqliteTransaction, T> operation)
  {
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    using var transaction = conn.BeginTransaction();
    try
    {
      var result = operation(conn, transaction);
      transaction.Commit();
      return result;
    }
    catch
    {
      transaction.Rollback();
      throw;
    }
  }

  public static void ExecuteWithTransaction(string connectionString, Action<SqliteConnection, SqliteTransaction> operation)
  {
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    using var transaction = conn.BeginTransaction();
    try
    {
      operation(conn, transaction);
      transaction.Commit();
    }
    catch
    {
      transaction.Rollback();
      throw;
    }
  }

  public static IResult HandleDatabaseOperation(Func<IResult> operation, string errorMessagePrefix)
  {
    try
    {
      return operation();
    }
    catch (SqliteException sqlEx) when (sqlEx.SqliteErrorCode == 19 && sqlEx.Message.Contains("account_number"))
    {
      return Results.BadRequest(new { message = "An account with this account number already exists. Account numbers must be unique." });
    }
    catch (Exception ex)
    {
      return Results.Problem($"{errorMessagePrefix}: {ex.Message}");
    }
  }

  public static IResult HandleDatabaseOperationWithUniqueConstraint(
    Func<IResult> operation,
    string errorMessagePrefix,
    string? uniqueConstraintMessage = null)
  {
    try
    {
      return operation();
    }
    catch (SqliteException sqlEx) when (sqlEx.SqliteErrorCode == 19)
    {
      var message = uniqueConstraintMessage ?? "A record with this value already exists. The value must be unique.";
      return Results.BadRequest(new { message });
    }
    catch (Exception ex)
    {
      return Results.Problem($"{errorMessagePrefix}: {ex.Message}");
    }
  }
}

