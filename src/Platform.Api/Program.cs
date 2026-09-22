using Platform.Api.Data;
using Platform.Api.Extensions;
using Platform.Api.Filters;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPlatformData(builder.Configuration)
    .AddPlatformAuthentication(builder.Configuration)
    .AddPlatformModules()
    .AddPlatformErrorHandling()
    .AddPlatformSwagger();

builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>());

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    await DbSeeder.SeedDevelopmentAsync(app.Services, app.Configuration);
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
