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

        public static class SystemConstants
        {
            public const string SystemUser = "System";
            public const int DefaultRetryCount = 3;
            public const int DefaultPollingInterval = 5;
        }
    }

    /// <summary>
    /// Constants for authentication and authorization
    /// </summary>
    public static class AuthConstants
    {
        /// <summary>
        /// Default roles in the system
        /// </summary>
        public static class Roles
        {
            public const string Admin = "Admin";
            public const string Operator = "Operator";
            public const string User = "User";
        }

        /// <summary>
        /// Default permissions in the system
        /// </summary>
        public static class Permissions
        {
            // Job permissions
            public const string ViewJobs = "Jobs.View";
            public const string CreateJobs = "Jobs.Create";
            public const string EditJobs = "Jobs.Edit";
            public const string DeleteJobs = "Jobs.Delete";
            public const string StopJobs = "Jobs.Stop";
            public const string RestartJobs = "Jobs.Restart";

            // Worker permissions
            public const string ViewWorkers = "Workers.View";
            public const string ManageWorkers = "Workers.Manage";
            public const string DeleteWorkers = "Workers.Delete";

            // Metrics permissions
            public const string ViewMetrics = "Metrics.View";
            public const string ExportMetrics = "Metrics.Export";

            // User management permissions
            public const string ViewUsers = "Users.View";
            public const string CreateUsers = "Users.Create";
            public const string EditUsers = "Users.Edit";
            public const string DeleteUsers = "Users.Delete";
            public const string AssignRoles = "Users.AssignRoles";
        }

        /// <summary>
        /// Authentication providers
        /// </summary>
        public static class Providers
        {
            public const string Local = "Local";
            public const string Google = "Google";
        }

        /// <summary>
        /// Policy names
        /// </summary>
        public static class Policies
        {
            public const string RequireAdminRole = "RequireAdminRole";
            public const string RequireOperatorRole = "RequireOperatorRole";
            public const string CanManageJobs = "CanManageJobs";
            public const string CanManageWorkers = "CanManageWorkers";
            public const string CanManageUsers = "CanManageUsers";
            public const string CanViewMetrics = "CanViewMetrics";
        }
    }
}
