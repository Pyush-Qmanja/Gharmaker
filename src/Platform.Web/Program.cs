using Platform.Web.Areas.Shop;
using Platform.Web.Extensions;
using Platform.Web.Security;

var builder = WebApplication.CreateBuilder(args);

// Do not announce the server software.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

builder.Services
    .AddPlatformMvc()
    .AddPlatformAuthentication()
    .AddPlatformApiClient(builder.Configuration)
    .AddPlatformScreens();

var app = builder.Build();

// The visitor's real IP and scheme when a trusted load balancer passes them on.
app.UseForwardedHeaders();
app.UseWebSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseShopIdentity();
app.UseStaffSession();
app.UseAuthorization();

app.MapAreaControllerRoute(
    name: "shop",
    areaName: ShopAuth.Area,
    pattern: "shop/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
