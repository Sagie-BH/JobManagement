namespace JobManagement.Infrastructure.Models.Authentication
{
    /// <summary>
    /// Response model for validation errors
    /// </summary>
    public class ValidationErrorResponse
    {
        public string Type { get; set; }
        public string Title { get; set; }
        public int Status { get; set; }
        public string TraceId { get; set; }
        public Dictionary<string, string[]> Errors { get; set; }
    }
}