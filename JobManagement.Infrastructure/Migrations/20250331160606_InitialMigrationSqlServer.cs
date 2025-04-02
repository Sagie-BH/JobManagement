using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigrationSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalJobs = table.Column<int>(type: "int", nullable: false),
                    PendingJobs = table.Column<int>(type: "int", nullable: false),
                    RunningJobs = table.Column<int>(type: "int", nullable: false),
                    CompletedJobs = table.Column<int>(type: "int", nullable: false),
                    FailedJobs = table.Column<int>(type: "int", nullable: false),
                    StoppedJobs = table.Column<int>(type: "int", nullable: false),
                    AverageExecutionTimeMs = table.Column<double>(type: "float", nullable: false),
                    SuccessRate = table.Column<double>(type: "float", nullable: false),
                    TotalRetries = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobQueueStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    QueueData = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobQueueStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetricSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SnapshotData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotType = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QueueMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalQueueLength = table.Column<int>(type: "int", nullable: false),
                    HighPriorityJobs = table.Column<int>(type: "int", nullable: false),
                    RegularPriorityJobs = table.Column<int>(type: "int", nullable: false),
                    LowPriorityJobs = table.Column<int>(type: "int", nullable: false),
                    AverageWaitTimeMs = table.Column<double>(type: "float", nullable: false),
                    JobsProcessedLastMinute = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkerMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalWorkers = table.Column<int>(type: "int", nullable: false),
                    ActiveWorkers = table.Column<int>(type: "int", nullable: false),
                    IdleWorkers = table.Column<int>(type: "int", nullable: false),
                    OfflineWorkers = table.Column<int>(type: "int", nullable: false),
                    AverageWorkerUtilization = table.Column<double>(type: "float", nullable: false),
                    TotalCapacity = table.Column<int>(type: "int", nullable: false),
                    CurrentLoad = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkerNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LastHeartbeat = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    CurrentLoad = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BaseJobEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Progress = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Discriminator = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    ExecutionPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxRetryAttempts = table.Column<int>(type: "int", nullable: true),
                    CurrentRetryCount = table.Column<int>(type: "int", nullable: true),
                    WorkerNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScheduledStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaseJobEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BaseJobEntity_WorkerNodes_WorkerNodeId",
                        column: x => x.WorkerNodeId,
                        principalTable: "WorkerNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "JobLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LogType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobLogs_BaseJobEntity_JobId",
                        column: x => x.JobId,
                        principalTable: "BaseJobEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BaseJobEntity_EndTime",
                table: "BaseJobEntity",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_BaseJobEntity_Priority",
                table: "BaseJobEntity",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_BaseJobEntity_ScheduledStartTime",
                table: "BaseJobEntity",
                column: "ScheduledStartTime");

            migrationBuilder.CreateIndex(
                name: "IX_BaseJobEntity_StartTime",
                table: "BaseJobEntity",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_BaseJobEntity_Status",
                table: "BaseJobEntity",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BaseJobEntity_WorkerNodeId",
                table: "BaseJobEntity",
                column: "WorkerNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobLogs_JobId",
                table: "JobLogs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobLogs_LogType",
                table: "JobLogs",
                column: "LogType");

            migrationBuilder.CreateIndex(
                name: "IX_JobLogs_Timestamp",
                table: "JobLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_JobMetrics_Timestamp",
                table: "JobMetrics",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_MetricSnapshots_SnapshotType",
                table: "MetricSnapshots",
                column: "SnapshotType");

            migrationBuilder.CreateIndex(
                name: "IX_MetricSnapshots_Timestamp",
                table: "MetricSnapshots",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_QueueMetrics_Timestamp",
                table: "QueueMetrics",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerMetrics_Timestamp",
                table: "WorkerMetrics",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerNodes_LastHeartbeat",
                table: "WorkerNodes",
                column: "LastHeartbeat");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerNodes_Name",
                table: "WorkerNodes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkerNodes_Status",
                table: "WorkerNodes",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobLogs");

            migrationBuilder.DropTable(
                name: "JobMetrics");

            migrationBuilder.DropTable(
                name: "JobQueueStates");

            migrationBuilder.DropTable(
                name: "MetricSnapshots");

            migrationBuilder.DropTable(
                name: "QueueMetrics");

            migrationBuilder.DropTable(
                name: "WorkerMetrics");

            migrationBuilder.DropTable(
                name: "BaseJobEntity");

            migrationBuilder.DropTable(
                name: "WorkerNodes");
        }
    }
}
