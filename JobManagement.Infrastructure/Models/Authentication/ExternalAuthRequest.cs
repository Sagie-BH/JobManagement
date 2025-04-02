using System.ComponentModel.DataAnnotations;

namespace JobManagement.Infrastructure.Models.Authentication
{
    /// <summary>
    /// Request model for external authentication login
    /// </summary>
    public class ExternalAuthRequest
    {
        [Required]
        public string Provider { get; set; }

        [Required]
        public string IdToken { get; set; }
    }
}