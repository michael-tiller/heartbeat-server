namespace Heartbeat.Server.Middleware ;

  /// <summary>
  ///   Adds security headers to all responses.
  /// </summary>
  public sealed class SecurityHeadersMiddleware
  {
    private readonly RequestDelegate next;

    /// <summary>
    ///   Initializes a new instance of the <see cref="SecurityHeadersMiddleware" /> class.
    /// </summary>
    /// <param name="next">The next request delegate.</param>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
      this.next = next;
    }

    /// <summary>
    ///   Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
      // Prevent MIME type sniffing
      context.Response.Headers["X-Content-Type-Options"] = "nosniff";

      // Prevent clickjacking
      context.Response.Headers["X-Frame-Options"] = "DENY";

      // Enable XSS filter (legacy browsers)
      context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

      // Control referrer information
      context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

      // Content Security Policy (API-focused)
      context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

      // Permissions Policy - disable all features for API
      context.Response.Headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

      await next(context);
    }
  }