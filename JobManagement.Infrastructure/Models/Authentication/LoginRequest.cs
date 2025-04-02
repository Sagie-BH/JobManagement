using System.ComponentModel.DataAnnotations;

namespace JobManagement.Infrastructure.Models.Authentication
{
    /// <summary>
    /// Request model for user login
    /// </summary>
    public class LoginRequest
    {
        [Required]
        public string Username { get; set; }

        [Required]
        public string Password { get; set; }
    }
}