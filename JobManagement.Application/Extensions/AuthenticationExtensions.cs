using JobManagement.Infrastructure.Authentication;
using JobManagement.Infrastructure.Interfaces.Authentication;
using JobManagement.Infrastructure.Services.Authentication;
using JobManagement.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;

namespace JobManagement.Infrastructure.Extensions
{
    /// <summary>
    /// Extension methods for configuring authentication and authorization services
    /// </summary>
    public static class AuthenticationExtensions
    {
        /// <summary>
        /// Adds authentication and authorization services to the service collection
        /// </summary>
        public static IServiceCollection AddAuthServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure JWT settings
            var jwtSettingsSection = configuration.GetSection("JwtSettings");
            services.Configure<JwtSettings>(jwtSettingsSection);
            var jwtSettings = jwtSettingsSection.Get<JwtSettings>();

            // Configure Google Auth settings
            var googleAuthSettingsSection = configuration.GetSection("GoogleAuthSettings");
            services.Configure<GoogleAuthSettings>(googleAuthSettingsSection);

            // Register authentication services
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IGoogleAuthService, GoogleAuthService>();
            services.AddScoped<IAuthService, AuthService>();

            // HttpClient for external API calls (needed for Google Auth)
            services.AddHttpClient();

            // Configure JWT authentication
            var key = Encoding.UTF8.GetBytes(jwtSettings.Secret);
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // Set to true in production
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // Configure SignalR JWT authentication
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Extract the token from the query string for SignalR connections
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/hubs/job") || path.StartsWithSegments("/hubs/worker")))
                        {
                            context.Token = accessToken;
                        }

                        return System.Threading.Tasks.Task.CompletedTask;
                    }
                };
            });

            // Configure custom authorization policies
            services.AddAuthorization(options =>
            {
                ConfigureAuthorizationPolicies(options);
            });

            // Register custom authorization policy provider
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();

            return services;
        }

        /// <summary>
        /// Configures authorization policies
        /// </summary>
        private static void ConfigureAuthorizationPolicies(AuthorizationOptions options)
        {
            // Role-based policies
            options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
            options.AddPolicy("RequireOperatorRole", policy => policy.RequireRole("Admin", "Operator"));

            // Permission-based policies
            options.AddPolicy("CanManageJobs", policy => policy.RequireAssertion(context =>
                context.User.HasClaim(c => c.Type == "permission" &&
                    (c.Value == "Jobs.Create" || c.Value == "Jobs.Edit" || c.Value == "Jobs.Delete"))));

            options.AddPolicy("CanViewJobs", policy => policy.RequireAssertion(context =>
                context.User.HasClaim(c => c.Type == "permission" && c.Value == "Jobs.View")));

            options.AddPolicy("CanManageWorkers", policy => policy.RequireAssertion(context =>
                context.User.HasClaim(c => c.Type == "permission" && c.Value == "Workers.Manage")));

            options.AddPolicy("CanViewMetrics", policy => policy.RequireAssertion(context =>
                context.User.HasClaim(c => c.Type == "permission" && c.Value == "Metrics.View")));

            options.AddPolicy("CanExportMetrics", policy => policy.RequireAssertion(context =>
                context.User.HasClaim(c => c.Type == "permission" && c.Value == "Metrics.Export")));
        }
    }
}