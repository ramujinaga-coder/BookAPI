using System.Data;
using System.Data.SQLite;
using System.IO;
using Dapper;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using BookApi.Data;

var builder = WebApplication.CreateBuilder(args);

// Resolve a cross-platform path for the SQLite file. If the connection string
// is provided in configuration, we honor it. If it contains a relative path
// we make it relative to the application's content root so it works on
// Windows and in Docker containers alike. We also ensure the directory exists.
string? configured = builder.Configuration.GetConnectionString("DefaultConnection");
string connectionString;
if (string.IsNullOrWhiteSpace(configured))
{
    // default relative path under content root
    var dbFile = Path.Combine(builder.Environment.ContentRootPath, "data", "books.db");
    Directory.CreateDirectory(Path.GetDirectoryName(dbFile) ?? builder.Environment.ContentRootPath);
    connectionString = $"Data Source={dbFile}";
}
else
{
    // Expect value like: "Data Source=/app/data/books.db" or "Data Source=relative/path/books.db"
    const string prefix = "Data Source=";
    if (configured.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        var filePart = configured.Substring(prefix.Length).Trim();

        // If user supplied an absolute path (e.g. /app/data/books.db on Linux or C:\path on Windows), use it
        if (Path.IsPathRooted(filePart))
        {
            var dir = Path.GetDirectoryName(filePart);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            connectionString = configured;
        }
        else
        {
            // Make it relative to content root
            var dbFile = Path.Combine(builder.Environment.ContentRootPath, filePart.TrimStart('/', '\\'));
            Directory.CreateDirectory(Path.GetDirectoryName(dbFile) ?? builder.Environment.ContentRootPath);
            connectionString = $"Data Source={dbFile}";
        }
    }
    else
    {
        // Not in expected format, fall back to using it as-is
        connectionString = configured;
    }
}
// Add SQLite Database Context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddSingleton<IDbConnection>(sp => new SQLiteConnection(connectionString));
builder.Services.AddControllers();

// Configure Redis distributed cache
var redisConfig = builder.Configuration.GetSection("Redis").GetValue<string>("Configuration");
if (!string.IsNullOrEmpty(redisConfig))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConfig;
        options.InstanceName = "BookApi_";
    });
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Books API", Version = "v1" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Books API v1");
        c.RoutePrefix = string.Empty;
    });
}

// Disable HTTPS redirection in Docker
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthorization();
app.MapControllers();

// Ensure Database & Table are Created
using (var connection = new SQLiteConnection(connectionString))
{
    connection.Open();
    connection.Execute(@"
        CREATE TABLE IF NOT EXISTS Books (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL
        );
    ");
}


app.Run();
