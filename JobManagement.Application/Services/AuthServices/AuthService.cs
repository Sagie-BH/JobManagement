using JobManagement.Domain.Constants;
using JobManagement.Domain.Entities;
using JobManagement.Infrastructure.Interfaces;
using JobManagement.Infrastructure.Interfaces.Authentication;
using JobManagement.Infrastructure.Models.Authentication;
using Microsoft.Extensions.Logging;
using System.Security.Authentication;
using System.Security.Cryptography;

namespace JobManagement.Infrastructure.Services.Authentication
{
    /// <summary>
    /// Implementation of authentication service for user registration, login, and token management
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IJwtService _jwtService;
        private readonly IGoogleAuthService _googleAuthService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUnitOfWork unitOfWork,
            IJwtService jwtService,
            IGoogleAuthService googleAuthService,
            ILogger<AuthService> logger)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _googleAuthService = googleAuthService;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress)
        {
            // Check if user with the same email already exists
            var existingUsers = await _unitOfWork.Repository<User>().GetAsync(u => u.Email == request.Email);
            if (existingUsers.Any())
            {
                throw new InvalidOperationException("Email is already registered");
            }

            // Check if username is already taken
            existingUsers = await _unitOfWork.Repository<User>().GetAsync(u => u.Username == request.Username);
            if (existingUsers.Any())
            {
                throw new InvalidOperationException("Username is already taken");
            }

            // Create new user
            var user = new User
            {
                Email = request.Email,
                Username = request.Username,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PasswordHash = HashPassword(request.Password),
                Provider = AuthConstants.Providers.Local,
                LastLogin = DateTime.UtcNow,
                IsActive = true
            };

            await _unitOfWork.Repository<User>().AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            // Assign default role (User)
            var userRole = await AssignDefaultRoleAsync(user.Id);

            // Get role and permission details
            var role = await _unitOfWork.Repository<Role>().GetByIdAsync(userRole.RoleId);
            var rolePermissions = await _unitOfWork.Repository<RolePermission>().GetAsync(rp => rp.RoleId == role.Id);

            var permissions = new List<string>();
            foreach (var rp in rolePermissions)
            {
                var permission = await _unitOfWork.Repository<Permission>().GetByIdAsync(rp.PermissionId);
                if (permission != null)
                {
                    permissions.Add(permission.Name);
                }
            }

            // Generate JWT tokens
            var accessToken = _jwtService.GenerateAccessToken(user, new[] { role.Name }, permissions);
            var refreshToken = await _jwtService.GenerateRefreshTokenAsync(user, ipAddress);

            _logger.LogInformation($"User {user.Username} registered successfully");

            return new AuthResponse
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                ExpiresAt = refreshToken.ExpiresAt,
                Roles = new List<string> { role.Name },
                Permissions = permissions
            };
        }

        /// <inheritdoc/>
        public async Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress)
        {
            // Get user by username
            var users = await _unitOfWork.Repository<User>().GetAsync(u => u.Username == request.Username);
            var user = users.FirstOrDefault();

            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            {
                throw new AuthenticationException("Invalid username or password");
            }

            // Check if user is active
            if (!user.IsActive)
            {
                throw new AuthenticationException("This account has been deactivated");
            }

            // Update last login timestamp
            user.LastLogin = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            // Get user roles and permissions
            var userRoles = await _unitOfWork.Repository<UserRole>().GetAsync(ur => ur.UserId == user.Id);
            var roles = new List<string>();
            var permissions = new List<string>();

            foreach (var userRole in userRoles)
            {
                var role = await _unitOfWork.Repository<Role>().GetByIdAsync(userRole.RoleId);
                if (role != null)
                {
                    roles.Add(role.Name);

                    // Get permissions for this role
                    var rolePermissions = await _unitOfWork.Repository<RolePermission>().GetAsync(rp => rp.RoleId == role.Id);
                    foreach (var rp in rolePermissions)
                    {
                        var permission = await _unitOfWork.Repository<Permission>().GetByIdAsync(rp.PermissionId);
                        if (permission != null)
                        {
                            permissions.Add(permission.Name);
                        }
                    }
                }
            }

            // Generate JWT tokens
            var accessToken = _jwtService.GenerateAccessToken(user, roles, permissions);
            var refreshToken = await _jwtService.GenerateRefreshTokenAsync(user, ipAddress);

            _logger.LogInformation($"User {user.Username} logged in successfully");

            return new AuthResponse
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                ExpiresAt = refreshToken.ExpiresAt,
                Roles = roles,
                Permissions = permissions
            };
        }

        /// <inheritdoc/>
        public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress)
        {
            return await _jwtService.RefreshTokenAsync(request.RefreshToken, ipAddress);
        }

        /// <inheritdoc/>
        public async Task<bool> RevokeTokenAsync(string token, string ipAddress)
        {
            return await _jwtService.RevokeTokenAsync(token, ipAddress, "Manually revoked");
        }

        /// <inheritdoc/>
        public async Task<AuthResponse> ExternalLoginAsync(ExternalAuthRequest request, string ipAddress)
        {
            if (request.Provider != AuthConstants.Providers.Google)
            {
                throw new NotSupportedException($"Provider {request.Provider} is not supported");
            }

            // Validate the Google token
            var googleUserInfo = await _googleAuthService.ValidateGoogleTokenAsync(request.IdToken);

            // Check if user exists
            var users = await _unitOfWork.Repository<User>().GetAsync(u => u.Email == googleUserInfo.Email);
            var user = users.FirstOrDefault();

            if (user == null)
            {
                // Create new user
                user = new User
                {
                    Email = googleUserInfo.Email,
                    Username = googleUserInfo.Email.Split('@')[0], // Use first part of email as username
                    FirstName = googleUserInfo.FirstName,
                    LastName = googleUserInfo.LastName,
                    ExternalId = googleUserInfo.Id,
                    Provider = AuthConstants.Providers.Google,
                    LastLogin = DateTime.UtcNow,
                    IsActive = true
                };

                await _unitOfWork.Repository<User>().AddAsync(user);
                await _unitOfWork.SaveChangesAsync();

                // Assign default role
                await AssignDefaultRoleAsync(user.Id);
            }
            else
            {
                // Update existing user information from Google
                user.FirstName = googleUserInfo.FirstName;
                user.LastName = googleUserInfo.LastName;
                user.ExternalId = googleUserInfo.Id;
                user.Provider = AuthConstants.Providers.Google;
                user.LastLogin = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync();
            }

            // Get user roles and permissions
            var userRoles = await _unitOfWork.Repository<UserRole>().GetAsync(ur => ur.UserId == user.Id);
            var roles = new List<string>();
            var permissions = new List<string>();

            foreach (var userRole in userRoles)
            {
                var role = await _unitOfWork.Repository<Role>().GetByIdAsync(userRole.RoleId);
                if (role != null)
                {
                    roles.Add(role.Name);

                    // Get permissions for this role
                    var rolePermissions = await _unitOfWork.Repository<RolePermission>().GetAsync(rp => rp.RoleId == role.Id);
                    foreach (var rp in rolePermissions)
                    {
                        var permission = await _unitOfWork.Repository<Permission>().GetByIdAsync(rp.PermissionId);
                        if (permission != null)
                        {
                            permissions.Add(permission.Name);
                        }
                    }
                }
            }

            // Generate JWT tokens
            var accessToken = _jwtService.GenerateAccessToken(user, roles, permissions);
            var refreshToken = await _jwtService.GenerateRefreshTokenAsync(user, ipAddress);

            _logger.LogInformation($"User {user.Username} logged in with Google successfully");

            return new AuthResponse
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                ExpiresAt = refreshToken.ExpiresAt,
                Roles = roles,
                Permissions = permissions
            };
        }

        /// <summary>
        /// Assigns the default role (User) to a new user
        /// </summary>
        private async Task<UserRole> AssignDefaultRoleAsync(Guid userId)
        {
            // Get User role
            var roles = await _unitOfWork.Repository<Role>().GetAsync(r => r.Name == AuthConstants.Roles.User);
            var userRole = roles.FirstOrDefault();

            // If role doesn't exist, create it
            if (userRole == null)
            {
                userRole = new Role
                {
                    Name = AuthConstants.Roles.User,
                    Description = "Regular user with basic permissions"
                };

                await _unitOfWork.Repository<Role>().AddAsync(userRole);
                await _unitOfWork.SaveChangesAsync();

                // Create basic permissions if they don't exist
                await EnsureBasicPermissionsExistAsync(userRole.Id);
            }

            // Assign role to user
            var newUserRole = new UserRole
            {
                UserId = userId,
                RoleId = userRole.Id
            };

            await _unitOfWork.Repository<UserRole>().AddAsync(newUserRole);
            await _unitOfWork.SaveChangesAsync();

            return newUserRole;
        }

        /// <summary>
        /// Ensures basic permissions exist and are assigned to the User role
        /// </summary>
        private async Task EnsureBasicPermissionsExistAsync(Guid roleId)
        {
            // Define basic permissions for regular users
            var basicPermissions = new[]
            {
                AuthConstants.Permissions.ViewJobs,
                AuthConstants.Permissions.CreateJobs,
                AuthConstants.Permissions.ViewWorkers
            };

            foreach (var permissionName in basicPermissions)
            {
                // Check if permission exists
                var permissions = await _unitOfWork.Repository<Permission>().GetAsync(p => p.Name == permissionName);
                var permission = permissions.FirstOrDefault();

                // Create permission if it doesn't exist
                if (permission == null)
                {
                    permission = new Permission
                    {
                        Name = permissionName,
                        Description = $"Allows {permissionName}"
                    };

                    await _unitOfWork.Repository<Permission>().AddAsync(permission);
                    await _unitOfWork.SaveChangesAsync();
                }

                // Assign permission to role
                var rolePermission = new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permission.Id
                };

                await _unitOfWork.Repository<RolePermission>().AddAsync(rolePermission);
            }

            await _unitOfWork.SaveChangesAsync();
        }

        /// <summary>
        /// Hashes a password using PBKDF2 with HMAC-SHA256
        /// </summary>
        private string HashPassword(string password)
        {
            using var rng = RandomNumberGenerator.Create();
            var salt = new byte[16];
            rng.GetBytes(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(32);

            var hashBytes = new byte[48]; // 16 bytes salt + 32 bytes hash
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 32);

            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Verifies a password against a hash
        /// </summary>
        private bool VerifyPassword(string password, string storedHash)
        {
            var hashBytes = Convert.FromBase64String(storedHash);

            var salt = new byte[16];
            Array.Copy(hashBytes, 0, salt, 0, 16);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(32);

            for (var i = 0; i < 32; i++)
            {
                if (hashBytes[i + 16] != hash[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}