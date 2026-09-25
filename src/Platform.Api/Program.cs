using Platform.Api.Firestore;
using Platform.Api.Extensions;
using Platform.Api.Filters;
using Platform.Api.Middleware;
using Platform.Api.Security;
using Platform.Shared.Common;

var builder = WebApplication.CreateBuilder(args);

// Do not announce the server software.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

builder.Services
    .AddPlatformData(builder.Configuration)
    .AddPlatformAuthentication(builder.Configuration)
    .AddPlatformRateLimiting(builder.Configuration)
    .AddPlatformModules(builder.Configuration)
    .AddPlatformErrorHandling()
    .AddPlatformSwagger();

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options => JsonDefaults.Apply(options.JsonSerializerOptions));

var app = builder.Build();

// The caller's real IP when a trusted proxy (the web app, a load balancer) passes it on;
// used for rate limits and the audit log.
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseApiSecurityHeaders();

// Swagger is always on in Development; elsewhere only when Swagger:Enabled is true.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocumentTitle = "Platform API");
}

if (app.Environment.IsDevelopment())
{
    await DevelopmentSeeder.SeedAsync(app.Services, app.Configuration);
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseSessionGuard();

// After authentication, so signed-in callers are limited per user rather than per IP.
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();
