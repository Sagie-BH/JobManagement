namespace JobManagement.Domain.Constants
{
    public static class WorkerConstants
    {
        public static class Status
        {
            public const string Active = "Active";
            public const string Idle = "Idle";
            public const string Offline = "Offline";
        }

        public static class LogType
        {
            public const string Info = "Info";
            public const string Warning = "Warning";
            public const string Error = "Error";
            public const string Debug = "Debug";
        }

        public static class SystemConstants
        {
            public const string SystemUser = "System";
            public const int DefaultRetryCount = 3;
            public const int DefaultPollingInterval = 5;
        }
    }
}
