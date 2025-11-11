using KarstenAssessmentApi.Routes;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
  options.AddDefaultPolicy(policy =>
  {
    policy.WithOrigins("http://localhost:4200")
      .AllowAnyHeader()
      .AllowAnyMethod();
  });
});

var app = builder.Build();
app.UseCors();

var dbPath = builder.Configuration["SQLITE_PATH"] ?? "/data/KarstenAssessmentDb.db";
var connString = $"Data Source={dbPath}";

app.MapGet("/", () => Results.Ok(new { message = "KarstenAssessmentApi is running" }));

// Register all route groups
app.MapAccountsRoutes(connString);
app.MapTransactionsRoutes(connString);
app.MapTransactionUploadsRoutes(connString);
app.MapFailedTransactionImportsRoutes(connString);

app.Run();

// Make Program class accessible for testing
public partial class Program { }