using JobManagement.Infrastructure.Interfaces.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace JobManagement.Infrastructure.Middleware
{
    /// <summary>
    /// Middleware to track user activity and update the last activity timestamp in JWT tokens
    /// </summary>
    public class ActivityTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ActivityTrackingMiddleware> _logger;

        public ActivityTrackingMiddleware(RequestDelegate next, ILogger<ActivityTrackingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IJwtService jwtService)
        {
            // Skip activity tracking for authentication endpoints
            if (IsAuthenticationPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            try
            {
                // Extract the token from the Authorization header
                string token = ExtractToken(context);

                if (!string.IsNullOrEmpty(token) && jwtService.ValidateToken(token))
                {
                    // Update the last activity timestamp in the token
                    string updatedToken = jwtService.UpdateLastActivity(token);

                    // Replace the Authorization header with the updated token
                    if (!string.IsNullOrEmpty(updatedToken) && updatedToken != token)
                    {
                        context.Response.OnStarting(() =>
                        {
                            context.Response.Headers["X-Updated-Token"] = updatedToken;
                            return Task.CompletedTask;
                        });
                    }
                }

                // Continue processing the request
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in activity tracking middleware");
                // Don't block the request if activity tracking fails
                await _next(context);
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

            return null;
        }

        private bool IsAuthenticationPath(PathString path)
        {
            // Paths that don't need activity tracking
            var authPaths = new[]
            {
                "/api/auth/login",
                "/api/auth/register",
                "/api/auth/refresh-token",
                "/api/auth/revoke-token",
                "/api/auth/external-login",
                "/swagger",
                "/health"
            };

            return authPaths.Any(p => path.StartsWithSegments(p));
        }
    }

    /// <summary>
    /// Extensions for registering activity tracking middleware
    /// </summary>
    public static class ActivityTrackingMiddlewareExtensions
    {
        /// <summary>
        /// Adds activity tracking middleware to the application pipeline
        /// </summary>
        public static IApplicationBuilder UseActivityTracking(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ActivityTrackingMiddleware>();
        }
    }
}