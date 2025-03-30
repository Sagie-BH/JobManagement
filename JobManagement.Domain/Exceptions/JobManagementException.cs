namespace JobManagement.Domain.Exceptions
{
    public class JobManagementException : Exception
    {
        public JobManagementException(string message) : base(message) { }
        public JobManagementException(string message, Exception innerException) : base(message, innerException) { }
    }
}
