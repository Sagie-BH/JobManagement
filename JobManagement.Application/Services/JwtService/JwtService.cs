using JobManagement.Domain.Entities;
using JobManagement.Infrastructure.Interfaces;
using JobManagement.Infrastructure.Interfaces.Authentication;
using JobManagement.Infrastructure.Models.Authentication;
using JobManagement.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace JobManagement.Infrastructure.Services.Authentication
{
    /// <summary>
    /// Implementation of JWT service for token management with inactivity tracking
    /// </summary>
    public class JwtService : IJwtService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<JwtService> _logger;

        public JwtService(
            IOptions<JwtSettings> jwtSettings,
            IUnitOfWork unitOfWork,
            ILogger<JwtService> logger)
        {
            _jwtSettings = jwtSettings.Value;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <inheritdoc/>
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("fullName", user.FullName),
                // Add last activity timestamp to track inactivity
                new Claim("lastActivity", DateTime.UtcNow.ToString("o"))
            };

            // Add role claims
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Add permission claims
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <inheritdoc/>
        public async Task<RefreshToken> GenerateRefreshTokenAsync(User user, string ipAddress)
        {
            // Generate a cryptographically strong random token
            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[64];
            rng.GetBytes(randomBytes);

            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = Convert.ToBase64String(randomBytes),
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };

            // Save refresh token to database
            await _unitOfWork.Repository<RefreshToken>().AddAsync(refreshToken);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation($"Generated refresh token for user {user.Id} (IP: {ipAddress})");
            return refreshToken;
        }

        /// <inheritdoc/>
        public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, string ipAddress)
        {
            var refreshTokens = await _unitOfWork.Repository<RefreshToken>().GetAsync(r => r.Token == refreshToken);

            // Validate refresh token
            var storedToken = refreshTokens.FirstOrDefault();
            if (storedToken == null)
            {
                _logger.LogWarning($"Refresh token not found (IP: {ipAddress})");
                throw new SecurityTokenException("Invalid refresh token");
            }

            if (!storedToken.IsActive)
            {
                _logger.LogWarning($"Inactive refresh token used for user {storedToken.UserId} (IP: {ipAddress})");
                throw new SecurityTokenException("Inactive refresh token");
            }

            // Get user details
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(storedToken.UserId);
            if (user == null)
            {
                _logger.LogWarning($"User {storedToken.UserId} not found for valid refresh token (IP: {ipAddress})");
                throw new SecurityTokenException("Unknown user");
            }

            // Update user's last login timestamp
            user.LastLogin = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            // Get user roles and permissions
            var userRoles = await _unitOfWork.Repository<UserRole>().GetAsync(ur => ur.UserId == user.Id);
            var roleIds = userRoles.Select(ur => ur.RoleId).ToList();

            var roles = new List<string>();
            var permissions = new List<string>();

            foreach (var roleId in roleIds)
            {
                var role = await _unitOfWork.Repository<Role>().GetByIdAsync(roleId);
                if (role != null)
                {
                    roles.Add(role.Name);

                    // Get permissions for this role
                    var rolePermissions = await _unitOfWork.Repository<RolePermission>().GetAsync(rp => rp.RoleId == roleId);
                    foreach (var rp in rolePermissions)
                    {
                        var permission = await _unitOfWork.Repository<Permission>().GetByIdAsync(rp.PermissionId);
                        if (permission != null && !permissions.Contains(permission.Name))
                        {
                            permissions.Add(permission.Name);
                        }
                    }
                }
            }

            // Generate new tokens
            var newAccessToken = GenerateAccessToken(user, roles, permissions);
            var newRefreshToken = await GenerateRefreshTokenAsync(user, ipAddress);

            // Revoke the current refresh token
            await RevokeTokenAsync(storedToken.Token, ipAddress, "Replaced by new token", newRefreshToken.Token);

            _logger.LogInformation($"Refreshed token for user {user.Id} (IP: {ipAddress})");

            // Return the new tokens
            return new AuthResponse
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken.Token,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                Roles = roles,
                Permissions = permissions
            };
        }

        /// <inheritdoc/>
        public async Task<bool> RevokeTokenAsync(string token, string ipAddress, string reason = null, string replacementToken = null)
        {
            var refreshTokens = await _unitOfWork.Repository<RefreshToken>().GetAsync(r => r.Token == token);
            var storedToken = refreshTokens.FirstOrDefault();

            if (storedToken == null)
            {
                _logger.LogWarning($"Attempt to revoke non-existent token (IP: {ipAddress})");
                return false;
            }

            // Revoke token
            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.RevokedByIp = ipAddress;
            storedToken.ReasonRevoked = reason;
            storedToken.ReplacedByToken = replacementToken;

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation($"Refresh token for user {storedToken.UserId} has been revoked (IP: {ipAddress})");
            return true;
        }

        /// <inheritdoc/>
        public bool ValidateToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);

            try
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out var validatedToken);

                // Check inactivity timeout
                if (validatedToken is JwtSecurityToken jwtToken)
                {
                    var lastActivityClaim = jwtToken.Claims.FirstOrDefault(x => x.Type == "lastActivity");
                    if (lastActivityClaim != null && DateTime.TryParse(lastActivityClaim.Value, out DateTime lastActivity))
                    {
                        var inactivityPeriod = DateTime.UtcNow - lastActivity;
                        if (inactivityPeriod.TotalMinutes > _jwtSettings.InactivityTimeoutMinutes)
                        {
                            _logger.LogInformation("Token rejected due to inactivity timeout");
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Token validation failed: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public int? GetUserIdFromToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);

            try
            {
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    // Don't validate lifetime here to allow extracting ID from expired tokens
                    ValidateLifetime = false,
                    ClockSkew = TimeSpan.Zero
                }, out var validatedToken);

                var jwtToken = (JwtSecurityToken)validatedToken;
                var userId = int.Parse(jwtToken.Claims.First(x => x.Type == JwtRegisteredClaimNames.Sub).Value);

                return userId;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Error extracting user ID from token: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Updates the last activity timestamp in the token
        /// </summary>
        /// <param name="token">The JWT token</param>
        /// <returns>A new token with updated last activity timestamp</returns>
        public string UpdateLastActivity(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();

            try
            {
                // Parse the token
                var jwtToken = tokenHandler.ReadJwtToken(token);

                // Get existing claims
                var claims = new List<Claim>(jwtToken.Claims);

                // Remove old lastActivity claim if it exists
                var oldActivityClaim = claims.FirstOrDefault(c => c.Type == "lastActivity");
                if (oldActivityClaim != null)
                {
                    claims.Remove(oldActivityClaim);
                }

                // Add new lastActivity claim
                claims.Add(new Claim("lastActivity", DateTime.UtcNow.ToString("o")));

                // Create new JWT token with updated claims
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var newToken = new JwtSecurityToken(
                    issuer: jwtToken.Issuer,
                    audience: jwtToken.Audiences.FirstOrDefault(),
                    claims: claims,
                    expires: jwtToken.ValidTo,
                    signingCredentials: creds
                );

                return tokenHandler.WriteToken(newToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating last activity in token");
                return token; // Return original token on error
            }
        }
    }
}