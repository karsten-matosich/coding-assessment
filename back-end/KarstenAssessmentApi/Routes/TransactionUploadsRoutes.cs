using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Builder;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KarstenAssessmentApi.Routes;

public static class TransactionUploadsRoutes
{
  public static void MapTransactionUploadsRoutes(this WebApplication app, string connectionString)
  {
    app.MapGet("/transaction_uploads/get_all", () =>
    {
      return DatabaseHelper.HandleDatabaseOperation(() =>
      {
        var uploads = new List<object>();
        DatabaseHelper.ExecuteWithConnection(connectionString, conn =>
        {
          using var cmd = conn.CreateCommand();
          cmd.CommandText = @"SELECT id, upload_date, file_name, file_size, incoming_transaction_count, outgoing_transaction_count, status, error_message FROM transaction_uploads;";
          using var rdr = cmd.ExecuteReader();
          while (rdr.Read())
          {
            uploads.Add(new
            {
              id = rdr.GetInt32(0),
              upload_date = rdr.GetDateTime(1),
              file_name = rdr.GetString(2),
              file_size = rdr.GetInt64(3),
              incoming_transaction_count = rdr.GetInt32(4),
              outgoing_transaction_count = rdr.GetInt32(5),
              status = rdr.GetString(6),
              error_message = rdr.IsDBNull(7) ? (string?)null : rdr.GetString(7)
            });
          }
        });
        return Results.Ok(uploads);
      }, "Error querying transaction uploads");
    });

    app.MapPost("/transaction_uploads/upload_csv", async (HttpContext context) =>
    {
      try
      {
        if (!context.Request.HasFormContentType)
        {
          return Results.BadRequest(new { message = "Request must be multipart/form-data" });
        }

        IFormCollection form;
        try
        {
          form = await context.Request.ReadFormAsync();
        }
        catch
        {
          return Results.BadRequest(new { message = "No file uploaded" });
        }

        var file = form.Files["file"];

        if (file == null || file.Length == 0)
        {
          return Results.BadRequest(new { message = "No file uploaded" });
        }

        // Validate file type
        var fileName = file.FileName;
        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
          return Results.BadRequest(new { message = "File must be a CSV file" });
        }

        // Read file content
        using var reader = new StreamReader(file.OpenReadStream());
        var csvContent = await reader.ReadToEndAsync();

        // Parse CSV first to get header information
        var parseResult = ParseCsv(csvContent, connectionString);
        if (!parseResult.Success)
        {
          return Results.BadRequest(new { message = parseResult.ErrorMessage });
        }

        // Create a mapping of external_transaction_id to CSV row for duplicate lookups
        var csvLines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var externalIdToCsvRow = new Dictionary<string, string>();
        if (csvLines.Length > 1)
        {
          // Parse header to find id column index
          var headerLine = csvLines[0].Trim();
          var headers = ParseCsvLine(headerLine);
          var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
          for (int i = 0; i < headers.Count; i++)
          {
            headerMap[headers[i].Trim()] = i;
          }
          
          var idIndex = headerMap.ContainsKey("id") ? headerMap["id"] : 0;
          
          // Skip header row and map external IDs to CSV rows
          for (int i = 1; i < csvLines.Length; i++)
          {
            var line = csvLines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            var values = ParseCsvLine(line);
            if (values.Count > idIndex)
            {
              var externalId = values[idIndex]?.Trim();
              if (!string.IsNullOrWhiteSpace(externalId) && !externalIdToCsvRow.ContainsKey(externalId))
              {
                externalIdToCsvRow[externalId] = line;
              }
            }
          }
        }

        // Insert transactions
        var transactionsToInsert = new List<CreateTransactionRequest>();
        
        var transactionResult = DatabaseHelper.ExecuteWithTransaction(connectionString, (conn, transaction) =>
        {
          int transactionUploadId = 0;
          string status = "completed";
          string? errorMessage = null;
          
          if (parseResult.ValidTransactions.Count > 0)
          {
            // Check for duplicates before inserting
            var existingDuplicates = new HashSet<(int AccountId, decimal Amount, string? ExternalId)>();
            using var checkDupCmd = conn.CreateCommand();
            checkDupCmd.Transaction = transaction;
            checkDupCmd.CommandText = @"SELECT account_id, amount, external_transaction_id FROM transactions 
                                        WHERE external_transaction_id IS NOT NULL;";
            using var dupRdr = checkDupCmd.ExecuteReader();
            while (dupRdr.Read())
            {
              var accountId = dupRdr.GetInt32(0);
              var amount = dupRdr.GetDecimal(1);
              var externalId = dupRdr.IsDBNull(2) ? null : dupRdr.GetString(2);
              existingDuplicates.Add((accountId, amount, externalId));
            }
            dupRdr.Close();

            // Filter out duplicates and track them
            var duplicateSkipped = new List<(string Id, string Reason, string CsvRow)>();
            
            foreach (var trans in parseResult.ValidTransactions)
            {
              var key = (trans.AccountId, trans.Amount, trans.ExternalTransactionId);
              if (existingDuplicates.Contains(key))
              {
                // Look up the CSV row for this transaction
                var csvRow = trans.ExternalTransactionId != null && externalIdToCsvRow.ContainsKey(trans.ExternalTransactionId)
                  ? externalIdToCsvRow[trans.ExternalTransactionId]
                  : "";
                duplicateSkipped.Add((trans.ExternalTransactionId ?? "unknown", "flagged as duplicate", csvRow));
                continue;
              }
              transactionsToInsert.Add(trans);
            }

            // Update parseResult with duplicate errors
            if (duplicateSkipped.Any())
            {
              parseResult.SkippedTransactions.AddRange(duplicateSkipped);
              // Recalculate counts
              parseResult.IncomingCount = transactionsToInsert.Count(t => t.Direction == "Incoming");
              parseResult.OutgoingCount = transactionsToInsert.Count(t => t.Direction == "Outgoing");
            }

            // Insert non-duplicate transactions
            foreach (var trans in transactionsToInsert)
            {
              using var cmd = conn.CreateCommand();
              cmd.Transaction = transaction;
              cmd.CommandText = @"INSERT INTO transactions (account_id, transaction_upload_id, amount, transaction_date, direction, external_transaction_id) 
                                VALUES ($accountId, NULL, $amount, $transactionDate, $direction, $externalTransactionId);";
              cmd.Parameters.AddWithValue("$accountId", trans.AccountId);
              cmd.Parameters.AddWithValue("$amount", trans.Amount);
              cmd.Parameters.AddWithValue("$transactionDate", trans.TransactionDate);
              cmd.Parameters.AddWithValue("$direction", trans.Direction);
              cmd.Parameters.AddWithValue("$externalTransactionId", trans.ExternalTransactionId ?? (object)DBNull.Value);
              cmd.ExecuteNonQuery();
            }

            // Calculate balance changes per account and update balances (only for successfully inserted transactions)
            // Always use absolute value of amount - direction exclusively determines add/subtract
            var accountBalanceChanges = new Dictionary<int, decimal>();
            foreach (var trans in transactionsToInsert)
            {
              if (!accountBalanceChanges.ContainsKey(trans.AccountId))
              {
                accountBalanceChanges[trans.AccountId] = 0;
              }
              
              // Ensure amount is always positive - direction determines add/subtract
              var absoluteAmount = Math.Abs(trans.Amount);
              
              if (trans.Direction == "Incoming")
              {
                accountBalanceChanges[trans.AccountId] += absoluteAmount;
              }
              else if (trans.Direction == "Outgoing")
              {
                accountBalanceChanges[trans.AccountId] -= absoluteAmount;
              }
            }

            // Update account balances
            foreach (var balanceChange in accountBalanceChanges)
            {
              using var updateBalanceCmd = conn.CreateCommand();
              updateBalanceCmd.Transaction = transaction;
              updateBalanceCmd.CommandText = @"UPDATE accounts SET balance = balance + $change WHERE id = $accountId;";
              updateBalanceCmd.Parameters.AddWithValue("$change", balanceChange.Value);
              updateBalanceCmd.Parameters.AddWithValue("$accountId", balanceChange.Key);
              updateBalanceCmd.ExecuteNonQuery();
            }
          }

          // Insert transaction_upload record
          var uploadDate = DateTime.Now.ToString("yyyy-MM-dd");
          status = parseResult.Status;
          // No longer storing error details in error_message since we have a separate table
          errorMessage = parseResult.SkippedTransactions.Any() 
            ? $"{parseResult.SkippedTransactions.Count} transaction(s) failed validation or were duplicates"
            : null;

          using var uploadCmd = conn.CreateCommand();
          uploadCmd.Transaction = transaction;
          uploadCmd.CommandText = @"INSERT INTO transaction_uploads (upload_date, file_name, file_size, incoming_transaction_count, outgoing_transaction_count, status, error_message) 
                                    VALUES ($uploadDate, $fileName, $fileSize, $incomingCount, $outgoingCount, $status, $errorMessage);";
          uploadCmd.Parameters.AddWithValue("$uploadDate", uploadDate);
          uploadCmd.Parameters.AddWithValue("$fileName", fileName);
          uploadCmd.Parameters.AddWithValue("$fileSize", file.Length);
          uploadCmd.Parameters.AddWithValue("$incomingCount", parseResult.IncomingCount);
          uploadCmd.Parameters.AddWithValue("$outgoingCount", parseResult.OutgoingCount);
          uploadCmd.Parameters.AddWithValue("$status", status);
          uploadCmd.Parameters.AddWithValue("$errorMessage", errorMessage ?? (object)DBNull.Value);
          uploadCmd.ExecuteNonQuery();
          
          // Get the inserted ID
          using var getIdCmd = conn.CreateCommand();
          getIdCmd.Transaction = transaction;
          getIdCmd.CommandText = "SELECT last_insert_rowid();";
          transactionUploadId = Convert.ToInt32(getIdCmd.ExecuteScalar());

          // Insert failed transactions into failed_transaction_imports table
          if (parseResult.SkippedTransactions.Any() && transactionUploadId > 0)
          {
            foreach (var skipped in parseResult.SkippedTransactions)
            {
              using var failedCmd = conn.CreateCommand();
              failedCmd.Transaction = transaction;
              failedCmd.CommandText = @"INSERT INTO failed_transaction_imports (transaction_upload_id, external_transaction_id, error_message, csv_row_value) 
                                        VALUES ($uploadId, $externalId, $errorMessage, $csvRow);";
              failedCmd.Parameters.AddWithValue("$uploadId", transactionUploadId);
              failedCmd.Parameters.AddWithValue("$externalId", skipped.Id);
              failedCmd.Parameters.AddWithValue("$errorMessage", skipped.Reason);
              failedCmd.Parameters.AddWithValue("$csvRow", skipped.CsvRow);
              failedCmd.ExecuteNonQuery();
            }
          }

          // Update transactions with transaction_upload_id (only for successfully inserted transactions)
          if (transactionsToInsert.Count > 0 && transactionUploadId > 0)
          {
            var externalIds = transactionsToInsert
              .Where(t => !string.IsNullOrEmpty(t.ExternalTransactionId))
              .Select(t => t.ExternalTransactionId)
              .ToList();
            
            if (externalIds.Any())
            {
              var placeholders = string.Join(",", externalIds.Select((_, i) => $"$extId{i}"));
              using var updateCmd = conn.CreateCommand();
              updateCmd.Transaction = transaction;
              updateCmd.CommandText = $@"UPDATE transactions SET transaction_upload_id = $uploadId 
                                        WHERE transaction_upload_id IS NULL 
                                        AND external_transaction_id IN ({placeholders});";
              updateCmd.Parameters.AddWithValue("$uploadId", transactionUploadId);
              for (int i = 0; i < externalIds.Count; i++)
              {
                updateCmd.Parameters.AddWithValue($"$extId{i}", externalIds[i] ?? (object)DBNull.Value);
              }
              updateCmd.ExecuteNonQuery();
            }
          }

          return (transactionUploadId, status, errorMessage, insertedCount: transactionsToInsert.Count);
        });

        var insertedCount = parseResult.ValidTransactions.Count > 0 ? transactionResult.insertedCount : 0;
        return Results.Ok(new
        {
          message = $"Upload completed. {insertedCount} transactions inserted.",
          transactionUploadId = transactionResult.transactionUploadId,
          status = transactionResult.status,
          errorMessage = transactionResult.errorMessage
        });
      }
      catch (Exception ex)
      {
        return Results.Problem($"Error processing file upload: {ex.Message}");
      }
    }).Accepts<IFormFile>("multipart/form-data");
  }

  private static CsvParseResult ParseCsv(string csvContent, string connectionString)
  {
    var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    if (lines.Length < 2)
    {
      return new CsvParseResult
      {
        Success = false,
        ErrorMessage = "CSV file must contain at least a header row and one data row"
      };
    }

    // Parse header
    var headerLine = lines[0].Trim();
    var headers = ParseCsvLine(headerLine);
    var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < headers.Count; i++)
    {
      headerMap[headers[i].Trim()] = i;
    }

    // Validate required columns
    var requiredColumns = new[] { "id", "account_number", "direction", "amount", "transaction_date" };
    var missingColumns = requiredColumns.Where(col => !headerMap.ContainsKey(col)).ToList();
    if (missingColumns.Any())
    {
      return new CsvParseResult
      {
        Success = false,
        ErrorMessage = $"Missing required columns: {string.Join(", ", missingColumns)}"
      };
    }

    var idIndex = headerMap["id"];
    var accountNumberIndex = headerMap["account_number"];
    var directionIndex = headerMap["direction"];
    var amountIndex = headerMap["amount"];
    var transactionDateIndex = headerMap["transaction_date"];

    // Get account mappings
    var accountMap = new Dictionary<string, int>();
    using var conn = new SqliteConnection(connectionString);
    conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"SELECT id, account_number FROM accounts;";
    using var rdr = cmd.ExecuteReader();
    while (rdr.Read())
    {
      accountMap[rdr.GetString(1)] = rdr.GetInt32(0);
    }

    var validTransactions = new List<CreateTransactionRequest>();
    var skippedTransactions = new List<(string Id, string Reason, string CsvRow)>();
    int incomingCount = 0;
    int outgoingCount = 0;

    // Parse data rows
    for (int i = 1; i < lines.Length; i++)
    {
      var line = lines[i].Trim();
      if (string.IsNullOrWhiteSpace(line)) continue;

      var originalLine = line; // Store original CSV row for failed transactions
      var values = ParseCsvLine(line);
      var maxRequiredIndex = Math.Max(idIndex, Math.Max(accountNumberIndex, Math.Max(directionIndex, Math.Max(amountIndex, transactionDateIndex))));
      
      // Try to get external ID for error tracking, use row number if not available
      var externalId = values.Count > idIndex ? values[idIndex]?.Trim() : null;
      var transactionId = string.IsNullOrWhiteSpace(externalId) ? $"row_{i + 1}" : externalId;
      
      if (values.Count <= maxRequiredIndex)
      {
        skippedTransactions.Add((transactionId, "insufficient columns", originalLine));
        continue;
      }

      var accountNumber = values[accountNumberIndex]?.Trim();
      var direction = values[directionIndex]?.Trim();
      var amountStr = values[amountIndex]?.Trim();
      var dateStr = values[transactionDateIndex]?.Trim();

      // Validate required fields are not null/empty
      if (string.IsNullOrWhiteSpace(externalId))
      {
        skippedTransactions.Add((transactionId, "missing id", originalLine));
        continue;
      }
      if (string.IsNullOrWhiteSpace(accountNumber))
      {
        skippedTransactions.Add((transactionId, "missing account number", originalLine));
        continue;
      }
      if (string.IsNullOrWhiteSpace(direction))
      {
        skippedTransactions.Add((transactionId, "missing direction", originalLine));
        continue;
      }
      if (string.IsNullOrWhiteSpace(amountStr))
      {
        skippedTransactions.Add((transactionId, "missing amount", originalLine));
        continue;
      }
      if (string.IsNullOrWhiteSpace(dateStr))
      {
        skippedTransactions.Add((transactionId, "missing transaction date", originalLine));
        continue;
      }

      // Validate account number exists (check early)
      if (!accountMap.ContainsKey(accountNumber))
      {
        skippedTransactions.Add((transactionId, "no matching account number", originalLine));
        continue;
      }

      // Validate and normalize direction
      var normalizedDirection = NormalizeDirection(direction);
      if (normalizedDirection == null)
      {
        skippedTransactions.Add((transactionId, "invalid direction", originalLine));
        continue;
      }

      // Validate amount
      if (!decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) ||
          !IsValidDecimal(amountStr))
      {
        skippedTransactions.Add((transactionId, "invalid amount", originalLine));
        continue;
      }

      // Take absolute value of amount (direction determines add/subtract)
      amount = Math.Abs(amount);

      // Validate and convert date
      if (!TryParseDate(dateStr, out var transactionDateStr))
      {
        skippedTransactions.Add((transactionId, "invalid transaction date", originalLine));
        continue;
      }

      // Parse the formatted date string to DateTime
      if (!DateTime.TryParseExact(transactionDateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var transactionDate))
      {
        skippedTransactions.Add((transactionId, "invalid transaction date", originalLine));
        continue;
      }

      try
      {
        var transaction = new CreateTransactionRequest(
          AccountId: accountMap[accountNumber],
          TransactionUploadId: null,
          Amount: amount,
          TransactionDate: transactionDate,
          Direction: normalizedDirection,
          ExternalTransactionId: externalId
        );

        validTransactions.Add(transaction);
        if (normalizedDirection == "Incoming")
        {
          incomingCount++;
        }
        else
        {
          outgoingCount++;
        }
      }
      catch
      {
        skippedTransactions.Add((transactionId, "unknown error", originalLine));
      }
    }

    var status = "completed";

    return new CsvParseResult
    {
      Success = true,
      ValidTransactions = validTransactions,
      SkippedTransactions = skippedTransactions,
      IncomingCount = incomingCount,
      OutgoingCount = outgoingCount,
      Status = status,
      ErrorMessage = null // Will be set after duplicate check
    };
  }

  private static List<string> ParseCsvLine(string line)
  {
    var values = new List<string>();
    var current = "";
    var inQuotes = false;

    foreach (var c in line)
    {
      if (c == '"')
      {
        inQuotes = !inQuotes;
      }
      else if (c == ',' && !inQuotes)
      {
        values.Add(current);
        current = "";
      }
      else
      {
        current += c;
      }
    }
    values.Add(current);
    return values;
  }

  private static string? NormalizeDirection(string direction)
  {
    var dir = direction.Trim();
    if (dir.Equals("I", StringComparison.OrdinalIgnoreCase) ||
        dir.Equals("Incoming", StringComparison.OrdinalIgnoreCase))
    {
      return "Incoming";
    }
    if (dir.Equals("O", StringComparison.OrdinalIgnoreCase) ||
        dir.Equals("Outgoing", StringComparison.OrdinalIgnoreCase))
    {
      return "Outgoing";
    }
    return null;
  }

  private static bool IsValidDecimal(string value)
  {
    if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
    {
      return false;
    }
    var parts = value.Trim().Split('.');
    if (parts.Length == 2 && parts[1].Length > 2)
    {
      return false; // More than 2 decimal places
    }
    return true;
  }

  private static bool TryParseDate(string dateStr, out string formattedDate)
  {
    formattedDate = "";
    dateStr = dateStr.Trim();

    // Try yyyy-mm-dd format
    if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date1))
    {
      formattedDate = date1.ToString("yyyy-MM-dd");
      return true;
    }

    // Try mm/dd/yyyy format
    if (DateTime.TryParseExact(dateStr, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date2))
    {
      formattedDate = date2.ToString("yyyy-MM-dd");
      return true;
    }

    return false;
  }

  private class CsvParseResult
  {
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<CreateTransactionRequest> ValidTransactions { get; set; } = new();
    public List<(string Id, string Reason, string CsvRow)> SkippedTransactions { get; set; } = new();
    public int IncomingCount { get; set; }
    public int OutgoingCount { get; set; }
    public string Status { get; set; } = "completed";
  }
}