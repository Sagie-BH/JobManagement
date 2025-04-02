using JobManagement.Application.Extensions;
using JobManagement.Application.Hubs;
using JobManagement.Infrastructure.Extensions;
using Microsoft.AspNetCore.Authorization;
using JobManagement.Infrastructure.Authentication;
using JobManagement.Infrastructure.Services;
using JobManagement.Application.Middelware;
using JobManagement.Infrastructure.Middleware;  // Add this using statement

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add infrastructure and application services
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddSignalR();
builder.Services.AddSignalRServices();

builder.Services.AddHostedService<DbInitializerService>();

// Add HttpContextAccessor for auditing
builder.Services.AddHttpContextAccessor();

// Add authentication and authorization services
builder.Services.AddAuthServices(builder.Configuration);

// Register permission handler
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Essential for SignalR
    });
});

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

var app = builder.Build();

app.UseCors("AllowReactApp");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthenticationMiddleware();

app.UseActivityTracking();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Configure SignalR hubs routing
app.MapHub<JobHub>("/hubs/job");
app.MapHub<WorkerHub>("/hubs/worker");

app.Run();