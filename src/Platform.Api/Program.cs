using Platform.Api.Firestore;
using Platform.Api.Extensions;
using Platform.Api.Filters;
using Platform.Shared.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPlatformData(builder.Configuration)
    .AddPlatformAuthentication(builder.Configuration)
    .AddPlatformModules()
    .AddPlatformErrorHandling()
    .AddPlatformSwagger();

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options => JsonDefaults.Apply(options.JsonSerializerOptions));

var app = builder.Build();

app.UseExceptionHandler();

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

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
