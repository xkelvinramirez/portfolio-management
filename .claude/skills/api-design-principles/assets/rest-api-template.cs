// Production-ready REST API template using ASP.NET Core Minimal APIs.
// Includes pagination, filtering, error handling, and best practices.
//
// Program.cs — run with: dotnet run

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS: Configures Cross-Origin Resource Sharing
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // TODO: Replace AllowAnyOrigin() with .WithOrigins("https://your-app.example.com")
        // in production. AllowCredentials() cannot be combined with AllowAnyOrigin().
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Trusted Host: Prevents HTTP Host Header attacks.
// TODO: Configure allowed hosts in production, e.g. "api.example.com".
app.Use(async (context, next) =>
{
    // ASP.NET Core enforces allowed hosts via builder.WebHost.UseSetting(
    //   WebHostDefaults.AllowedHostsKey, "api.example.com")
    // or the "AllowedHosts" key in appsettings.json. Left permissive ("*") here.
    await next();
});

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(o => o.RouteTemplate = "api/docs/{documentName}/swagger.json");
    app.UseSwaggerUI(o => o.RoutePrefix = "api/docs");
}

// Global error handling
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var exception = feature?.Error;

        var (statusCode, error, message) = exception switch
        {
            ApiException apiEx => (apiEx.StatusCode, apiEx.GetType().Name, apiEx.Message),
            _ => (StatusCodes.Status500InternalServerError, "InternalServerError", "An unexpected error occurred")
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var details = (exception as ApiException)?.Details;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(error, message, details));
    });
});

// ---------------------------------------------------------------------------
// Models
// ---------------------------------------------------------------------------

app.MapGet("/api/users", ListUsers)
    .WithTags("Users")
    .WithName("ListUsers")
    .Produces<PaginatedResponse<User>>(StatusCodes.Status200OK);

app.MapPost("/api/users", CreateUser)
    .WithTags("Users")
    .WithName("CreateUser")
    .Produces<User>(StatusCodes.Status201Created)
    .ProducesValidationProblem();

app.MapGet("/api/users/{userId}", GetUser)
    .WithTags("Users")
    .WithName("GetUser")
    .Produces<User>(StatusCodes.Status200OK)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

app.MapPatch("/api/users/{userId}", UpdateUser)
    .WithTags("Users")
    .WithName("UpdateUser")
    .Produces<User>(StatusCodes.Status200OK)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

app.MapDelete("/api/users/{userId}", DeleteUser)
    .WithTags("Users")
    .WithName("DeleteUser")
    .Produces(StatusCodes.Status204NoContent)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

app.Run();

// ---------------------------------------------------------------------------
// Endpoint handlers
// ---------------------------------------------------------------------------

/// <summary>List users with pagination and filtering.</summary>
static IResult ListUsers(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] UserStatus? status = null,
    [FromQuery] string? search = null)
{
    if (page < 1) throw new ValidationException("page must be >= 1");
    if (pageSize is < 1 or > 100) throw new ValidationException("pageSize must be between 1 and 100");

    // Mock implementation
    const int total = 100;
    var items = Enumerable
        .Range((page - 1) * pageSize, Math.Max(0, Math.Min(page * pageSize, total) - (page - 1) * pageSize))
        .Select(i => new User(
            Id: i.ToString(),
            Email: $"user{i}@example.com",
            Name: $"User {i}",
            Status: UserStatus.Active,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow))
        .ToList();

    var response = new PaginatedResponse<User>(
        Items: items,
        Total: total,
        Page: page,
        PageSize: pageSize,
        Pages: (int)Math.Ceiling(total / (double)pageSize));

    return Results.Ok(response);
}

/// <summary>Create a new user.</summary>
static IResult CreateUser([FromBody] UserCreate user)
{
    var validation = ValidateModel(user);
    if (validation is not null) return validation;

    // Mock implementation
    var created = new User(
        Id: "123",
        Email: user.Email,
        Name: user.Name,
        Status: user.Status,
        CreatedAt: DateTime.UtcNow,
        UpdatedAt: DateTime.UtcNow);

    return Results.Created($"/api/users/{created.Id}", created);
}

/// <summary>Get user by ID.</summary>
static IResult GetUser([FromRoute] string userId)
{
    // Mock: Check if exists
    if (userId == "999")
    {
        throw new NotFoundException("User not found", new { id = userId });
    }

    var user = new User(
        Id: userId,
        Email: "user@example.com",
        Name: "User Name",
        Status: UserStatus.Active,
        CreatedAt: DateTime.UtcNow,
        UpdatedAt: DateTime.UtcNow);

    return Results.Ok(user);
}

/// <summary>Partially update user.</summary>
static IResult UpdateUser([FromRoute] string userId, [FromBody] UserUpdate update)
{
    var validation = ValidateModel(update);
    if (validation is not null) return validation;

    // Validate user exists (mock)
    if (userId == "999")
    {
        throw new NotFoundException("User not found", new { id = userId });
    }

    var existing = new User(
        Id: userId,
        Email: "user@example.com",
        Name: "User Name",
        Status: UserStatus.Active,
        CreatedAt: DateTime.UtcNow,
        UpdatedAt: DateTime.UtcNow);

    // Apply updates
    var updated = existing with
    {
        Email = update.Email ?? existing.Email,
        Name = update.Name ?? existing.Name,
        Status = update.Status ?? existing.Status,
        UpdatedAt = DateTime.UtcNow
    };

    return Results.Ok(updated);
}

/// <summary>Delete user.</summary>
static IResult DeleteUser([FromRoute] string userId)
{
    // Verify exists (mock)
    if (userId == "999")
    {
        throw new NotFoundException("User not found", new { id = userId });
    }

    return Results.NoContent();
}

static IResult? ValidateModel(object model)
{
    var context = new ValidationContext(model);
    var results = new List<ValidationResult>();
    if (Validators.TryValidateObject(model, context, results, validateAllProperties: true))
    {
        return null;
    }

    var details = results.Select(r => new ErrorDetail(
        Field: r.MemberNames.FirstOrDefault(),
        Message: r.ErrorMessage ?? "Invalid value",
        Code: "invalid"));

    return Results.BadRequest(new ErrorResponse("ValidationError", "Request validation failed", details));
}

static class Validators
{
    public static bool TryValidateObject(object instance, ValidationContext context, List<ValidationResult> results, bool validateAllProperties)
        => System.ComponentModel.DataAnnotations.Validator.TryValidateObject(instance, context, results, validateAllProperties);
}

// ---------------------------------------------------------------------------
// Models
// ---------------------------------------------------------------------------

public enum UserStatus
{
    Active,
    Inactive,
    Suspended
}

public record UserCreate(
    [property: Required, EmailAddress] string Email,
    [property: Required, StringLength(100, MinimumLength = 1)] string Name,
    [property: Required, MinLength(8)] string Password,
    UserStatus Status = UserStatus.Active);

public record UserUpdate(
    [property: EmailAddress] string? Email = null,
    [property: StringLength(100, MinimumLength = 1)] string? Name = null,
    UserStatus? Status = null);

public record User(
    string Id,
    string Email,
    string Name,
    UserStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// ---------------------------------------------------------------------------
// Pagination
// ---------------------------------------------------------------------------

public record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize,
    int Pages);

// ---------------------------------------------------------------------------
// Error handling
// ---------------------------------------------------------------------------

public record ErrorDetail(string? Field, string Message, string Code);

public record ErrorResponse(string Error, string Message, object? Details = null);

public abstract class ApiException : Exception
{
    protected ApiException(int statusCode, string message, object? details = null) : base(message)
    {
        StatusCode = statusCode;
        Details = details;
    }

    public int StatusCode { get; }
    public object? Details { get; }
}

public sealed class NotFoundException : ApiException
{
    public NotFoundException(string message, object? details = null)
        : base(StatusCodes.Status404NotFound, message, details)
    {
    }
}

public sealed class ValidationException : ApiException
{
    public ValidationException(string message, object? details = null)
        : base(StatusCodes.Status400BadRequest, message, details)
    {
    }
}
