using JobManagement.Infrastructure.Interfaces.Authentication;
using JobManagement.Infrastructure.Models.Authentication;
using JobManagement.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace JobManagement.Infrastructure.Services.Authentication
{
    /// <summary>
    /// Implementation of Google authentication service
    /// </summary>
    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly GoogleAuthSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GoogleAuthService> _logger;

        public GoogleAuthService(
            IOptions<GoogleAuthSettings> settings,
            IHttpClientFactory httpClientFactory,
            ILogger<GoogleAuthService> logger)
        {
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<GoogleUserInfo> ValidateGoogleTokenAsync(string idToken)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();

                // Validate token with Google's tokeninfo endpoint
                var response = await httpClient.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={idToken}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Google token validation failed: {response.StatusCode}");
                }

                var payload = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>();

                // Verify the token is issued for our app
                if (payload.Aud != _settings.ClientId)
                {
                    throw new Exception("Token was not issued for this application");
                }

                // Return user info
                return new GoogleUserInfo
                {
                    Id = payload.Sub,
                    Email = payload.Email,
                    EmailVerified = payload.EmailVerified,
                    FirstName = payload.GivenName,
                    LastName = payload.FamilyName,
                    PictureUrl = payload.Picture
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating Google token");
                throw;
            }
        }

        /// <summary>
        /// Internal model for Google token validation response
        /// </summary>
        private class GoogleTokenResponse
        {
            public string Iss { get; set; }
            public string Azp { get; set; }
            public string Aud { get; set; }
            public string Sub { get; set; }
            public string Email { get; set; }
            public bool EmailVerified { get; set; }
            public string Name { get; set; }
            public string Picture { get; set; }
            public string GivenName { get; set; }
            public string FamilyName { get; set; }
            public string Locale { get; set; }
            public long Iat { get; set; }
            public long Exp { get; set; }
        }
    }
}