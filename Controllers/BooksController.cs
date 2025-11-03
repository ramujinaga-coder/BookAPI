using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

[Route("api/books")]
[ApiController]
public class BooksController : ControllerBase
{
    private readonly IDbConnection _db;
    private readonly IDistributedCache _cache;
    private const string BooksCacheKey = "books:all";

    public BooksController(IDbConnection db, IDistributedCache cache)
    {
        _db = db;
        _cache = cache;
    }

    // Get all books (cached)
    [HttpGet]
    public IActionResult GetBooks()
    {
        // Try to get from cache
        var cached = _cache.GetString(BooksCacheKey);
        if (!string.IsNullOrEmpty(cached))
        {
            var cachedBooks = JsonSerializer.Deserialize<List<Book>>(cached);
            return Ok(cachedBooks);
        }

        // Load from DB
        var books = _db.Query<Book>("SELECT * FROM Books").ToList();

        // Store in cache (absolute expiration 60 seconds)
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
        };
        _cache.SetString(BooksCacheKey, JsonSerializer.Serialize(books), options);

        return Ok(books);
    }

    // Insert book (invalidate cache)
    [HttpPost]
    public IActionResult InsertBook([FromBody] Book book)
    {
        var sql = "INSERT INTO Books (Name) VALUES (@Name);";
        _db.Execute(sql, new { book.Name });

        // Invalidate cache
        _cache.Remove(BooksCacheKey);

        return Ok("Book Added");
    }

    // Seed sample data (invalidate cache)
    [HttpPost("seed")]
    public IActionResult SeedData()
    {
        var sql = "INSERT INTO Books (Name) VALUES (@Name);";
        _db.Execute(sql, new { Name = "C# Fundamentals" });
        _db.Execute(sql, new { Name = "ASP.NET Core Basics" });

        // Invalidate cache
        _cache.Remove(BooksCacheKey);

        return Ok("Data Seeded");
    }

    // Get book count
    [HttpGet("count")]
    public IActionResult GetBookCount()
    {
        int count = _db.ExecuteScalar<int>("SELECT COUNT(*) FROM Books");
        return Ok($"Total Books: {count}");
    }
}
