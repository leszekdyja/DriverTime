using DriverTime.Api.Authentication;
using DriverTime.Application;
using DriverTime.Application.Interfaces;
using DriverTime.Infrastructure;
using DriverTime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment()
    && string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Secret"]))
{
    throw new InvalidOperationException(
        "Jwt:Secret must be configured outside the Development environment.");
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IJwtSettings, JwtRuntimeSettings>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services
    .AddAuthentication(JwtAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, JwtAuthenticationHandler>(
        JwtAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
});

app.Map("/health", healthApp =>
{
    healthApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsJsonAsync(new
        {
            status = "Healthy",
            application = "DriverTime"
        });
    });
});

app.UseCors(policy =>
{
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
        ?? Array.Empty<string>();

    if (app.Environment.IsDevelopment())
    {
        policy.AllowAnyOrigin();
    }
    else if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins);
    }

    policy
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("Content-Disposition");
});

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DriverTimeDbContext>();
    await dbContext.Database.MigrateAsync();
    var seedDemoData = app.Environment.IsDevelopment()
        || builder.Configuration.GetValue<bool>("DemoData:Enabled");
    await scope.ServiceProvider
        .GetRequiredService<DatabaseSeeder>()
        .SeedAsync(seedDemoData);
}

app.Run();
