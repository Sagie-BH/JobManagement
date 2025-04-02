using JobManagement.Infrastructure.Interfaces.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace JobManagement.Application.Middelware
{
    /// <summary>
    /// Middleware to handle authentication and token validation for each request
    /// </summary>
    public class AuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthenticationMiddleware> _logger;

        public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IJwtService jwtService)
        {
            // Skip authentication for certain paths
            if (IsExcludedPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            try
            {
                // Extract the token from the Authorization header or query string
                string token = ExtractToken(context);

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Authentication failed: No token provided");
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Unauthorized: No authentication token provided");
                    return;
                }

                // Validate the token
                bool isValid = jwtService.ValidateToken(token);
                if (!isValid)
                {
                    _logger.LogWarning("Authentication failed: Invalid token");
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Unauthorized: Invalid authentication token");
                    return;
                }

                // Continue with the request
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in authentication middleware");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("An error occurred while processing your request");
            }
        }

        private string ExtractToken(HttpContext context)
        {
            // Try to get token from the Authorization header
            string authHeader = context.Request.Headers["Authorization"];
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }

            // Try to get token from query string (useful for WebSocket connections)
            if (context.Request.Query.TryGetValue("access_token", out StringValues accessToken))
            {
                return accessToken.FirstOrDefault();
            }

            return null;
        }

        private bool IsExcludedPath(PathString path)
        {
            // Paths that don't require authentication
            var excludedPaths = new[]
            {
                "/api/auth/login",
                "/api/auth/register",
                "/api/auth/refresh-token",
                "/api/auth/external-login",
                "/swagger",
                "/health"
            };

            return excludedPaths.Any(p => path.StartsWithSegments(p));
        }
    }

    public static class AuthenticationMiddlewareExtensions
    {
        /// <summary>
        /// Adds custom authentication middleware to the application pipeline
        /// </summary>
        public static IApplicationBuilder UseAuthenticationMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthenticationMiddleware>();
        }
    }
}
