# error-handling-patterns — detailed patterns and worked examples

## Language-Specific Patterns

### C# Error Handling

**Custom Exception Hierarchy:**

```csharp
public class AppException : Exception
{
    public string? Code { get; }
    public IReadOnlyDictionary<string, object> Details { get; }
    public DateTime Timestamp { get; }

    public AppException(string message, string? code = null, Dictionary<string, object>? details = null)
        : base(message)
    {
        Code = code;
        Details = details ?? new Dictionary<string, object>();
        Timestamp = DateTime.UtcNow;
    }
}

public class ValidationException : AppException
{
    public ValidationException(string message, string? code = null, Dictionary<string, object>? details = null)
        : base(message, code, details) { }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message, string? code = null, Dictionary<string, object>? details = null)
        : base(message, code, details) { }
}

public class ExternalServiceException : AppException
{
    public string Service { get; }

    public ExternalServiceException(string message, string service, string? code = null, Dictionary<string, object>? details = null)
        : base(message, code, details)
    {
        Service = service;
    }
}

// Usage
public User GetUser(string userId)
{
    var user = db.Users.FirstOrDefault(u => u.Id == userId);
    if (user is null)
    {
        throw new NotFoundException(
            "User not found",
            code: "USER_NOT_FOUND",
            details: new Dictionary<string, object> { ["user_id"] = userId });
    }
    return user;
}
```

**Scoped Cleanup (using / try-finally):**

```csharp
public static class DatabaseTransactionExtensions
{
    // Ensures the transaction is committed or rolled back.
    public static void RunInTransaction(this IDbSession session, Action<IDbSession> action)
    {
        try
        {
            action(session);
            session.Commit();
        }
        catch
        {
            session.Rollback();
            throw;
        }
        finally
        {
            session.Close();
        }
    }
}

// Usage
db.Session.RunInTransaction(session =>
{
    var user = new User { Name = "Alice" };
    session.Add(user);
    // Automatic commit or rollback
});
```

**Retry with Exponential Backoff:**

```csharp
public static class RetryPolicy
{
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> action,
        int maxAttempts = 3,
        double backoffFactor = 2.0,
        params Type[] retryableExceptions)
    {
        Exception? lastException = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception e) when (retryableExceptions.Length == 0
                || Array.Exists(retryableExceptions, t => t.IsInstanceOfType(e)))
            {
                lastException = e;
                if (attempt < maxAttempts - 1)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(backoffFactor, attempt));
                    await Task.Delay(delay);
                    continue;
                }
                throw;
            }
        }
        throw lastException!;
    }
}

// Usage
var data = await RetryPolicy.RetryAsync(
    async () =>
    {
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
    },
    maxAttempts: 3,
    retryableExceptions: typeof(HttpRequestException));
```

### TypeScript/JavaScript Error Handling

**Custom Error Classes:**

```typescript
// Custom error classes
class ApplicationError extends Error {
  constructor(
    message: string,
    public code: string,
    public statusCode: number = 500,
    public details?: Record<string, any>,
  ) {
    super(message);
    this.name = this.constructor.name;
    Error.captureStackTrace(this, this.constructor);
  }
}

class ValidationError extends ApplicationError {
  constructor(message: string, details?: Record<string, any>) {
    super(message, "VALIDATION_ERROR", 400, details);
  }
}

class NotFoundError extends ApplicationError {
  constructor(resource: string, id: string) {
    super(`${resource} not found`, "NOT_FOUND", 404, { resource, id });
  }
}

// Usage
function getUser(id: string): User {
  const user = users.find((u) => u.id === id);
  if (!user) {
    throw new NotFoundError("User", id);
  }
  return user;
}
```

**Result Type Pattern:**

```typescript
// Result type for explicit error handling
type Result<T, E = Error> = { ok: true; value: T } | { ok: false; error: E };

// Helper functions
function Ok<T>(value: T): Result<T, never> {
  return { ok: true, value };
}

function Err<E>(error: E): Result<never, E> {
  return { ok: false, error };
}

// Usage
function parseJSON<T>(json: string): Result<T, SyntaxError> {
  try {
    const value = JSON.parse(json) as T;
    return Ok(value);
  } catch (error) {
    return Err(error as SyntaxError);
  }
}

// Consuming Result
const result = parseJSON<User>(userJson);
if (result.ok) {
  console.log(result.value.name);
} else {
  console.error("Parse failed:", result.error.message);
}

// Chaining Results
function chain<T, U, E>(
  result: Result<T, E>,
  fn: (value: T) => Result<U, E>,
): Result<U, E> {
  return result.ok ? fn(result.value) : result;
}
```

**Async Error Handling:**

```typescript
// Async/await with proper error handling
async function fetchUserOrders(userId: string): Promise<Order[]> {
  try {
    const user = await getUser(userId);
    const orders = await getOrders(user.id);
    return orders;
  } catch (error) {
    if (error instanceof NotFoundError) {
      return []; // Return empty array for not found
    }
    if (error instanceof NetworkError) {
      // Retry logic
      return retryFetchOrders(userId);
    }
    // Re-throw unexpected errors
    throw error;
  }
}

// Promise error handling
function fetchData(url: string): Promise<Data> {
  return fetch(url)
    .then((response) => {
      if (!response.ok) {
        throw new NetworkError(`HTTP ${response.status}`);
      }
      return response.json();
    })
    .catch((error) => {
      console.error("Fetch failed:", error);
      throw error;
    });
}
```

## Universal Patterns

### Pattern 1: Circuit Breaker

Prevent cascading failures in distributed systems.

```csharp
public enum CircuitState
{
    Closed,   // Normal operation
    Open,     // Failing, reject requests
    HalfOpen  // Testing if recovered
}

public class CircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _timeout;
    private readonly int _successThreshold;

    private int _failureCount;
    private int _successCount;
    private CircuitState _state = CircuitState.Closed;
    private DateTime? _lastFailureTime;

    public CircuitBreaker(int failureThreshold = 5, TimeSpan? timeout = null, int successThreshold = 2)
    {
        _failureThreshold = failureThreshold;
        _timeout = timeout ?? TimeSpan.FromSeconds(60);
        _successThreshold = successThreshold;
    }

    public T Call<T>(Func<T> func)
    {
        if (_state == CircuitState.Open)
        {
            if (_lastFailureTime is not null && DateTime.UtcNow - _lastFailureTime > _timeout)
            {
                _state = CircuitState.HalfOpen;
                _successCount = 0;
            }
            else
            {
                throw new InvalidOperationException("Circuit breaker is OPEN");
            }
        }

        try
        {
            var result = func();
            OnSuccess();
            return result;
        }
        catch
        {
            OnFailure();
            throw;
        }
    }

    private void OnSuccess()
    {
        _failureCount = 0;
        if (_state == CircuitState.HalfOpen)
        {
            _successCount++;
            if (_successCount >= _successThreshold)
            {
                _state = CircuitState.Closed;
                _successCount = 0;
            }
        }
    }

    private void OnFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;
        if (_failureCount >= _failureThreshold)
        {
            _state = CircuitState.Open;
        }
    }
}

// Usage
var circuitBreaker = new CircuitBreaker();

public Data FetchData() => circuitBreaker.Call(() => externalApi.GetData());
```

### Pattern 2: Error Aggregation

Collect multiple errors instead of failing on first error.

```typescript
class ErrorCollector {
  private errors: Error[] = [];

  add(error: Error): void {
    this.errors.push(error);
  }

  hasErrors(): boolean {
    return this.errors.length > 0;
  }

  getErrors(): Error[] {
    return [...this.errors];
  }

  throw(): never {
    if (this.errors.length === 1) {
      throw this.errors[0];
    }
    throw new AggregateError(
      this.errors,
      `${this.errors.length} errors occurred`,
    );
  }
}

// Usage: Validate multiple fields
function validateUser(data: any): User {
  const errors = new ErrorCollector();

  if (!data.email) {
    errors.add(new ValidationError("Email is required"));
  } else if (!isValidEmail(data.email)) {
    errors.add(new ValidationError("Email is invalid"));
  }

  if (!data.name || data.name.length < 2) {
    errors.add(new ValidationError("Name must be at least 2 characters"));
  }

  if (!data.age || data.age < 18) {
    errors.add(new ValidationError("Age must be 18 or older"));
  }

  if (errors.hasErrors()) {
    errors.throw();
  }

  return data as User;
}
```

### Pattern 3: Graceful Degradation

Provide fallback functionality when errors occur.

```csharp
public static class Fallback
{
    // Try the primary function, fall back to the fallback on error.
    public static T WithFallback<T>(Func<T> primary, Func<T> fallback, bool logError = true)
    {
        try
        {
            return primary();
        }
        catch (Exception e)
        {
            if (logError)
            {
                logger.LogError(e, "Primary function failed");
            }
            return fallback();
        }
    }

    public static T? TryFunction<T>(Func<T?> func)
    {
        try
        {
            return func();
        }
        catch
        {
            return default;
        }
    }
}

// Usage
public UserProfile GetUserProfile(string userId) =>
    Fallback.WithFallback(
        primary: () => FetchFromCache(userId),
        fallback: () => FetchFromDatabase(userId));

// Multiple fallbacks
public double GetExchangeRate(string currency) =>
    Fallback.TryFunction(() => apiProvider1.GetRate(currency))
    ?? Fallback.TryFunction(() => apiProvider2.GetRate(currency))
    ?? Fallback.TryFunction(() => cache.GetRate(currency))
    ?? DefaultRate;
```
