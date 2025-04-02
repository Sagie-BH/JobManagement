namespace JobManagement.Infrastructure.Models.Authentication
{
    /// <summary>
    /// Response model for authentication
    /// </summary>
    public class AuthResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public List<string> Roles { get; set; }
        public List<string> Permissions { get; set; }
    }
}