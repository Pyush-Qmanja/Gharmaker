using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Options;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Dtos.Pricing;
using Platform.Shared.Validation.Common;
using Platform.Web.Areas.Shop;
using Platform.Web.Options;
using Platform.Web.Security;
using Platform.Web.Services.Api;
using Platform.Web.Services.Auth;
using Platform.Web.TagHelpers;

namespace Platform.Web.Extensions;

/// <summary>
/// Groups UI service registration so <c>Program.cs</c> stays a short list of
/// calls. A new screen adds one line to <see cref="AddPlatformScreens"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers MVC with a global sign-in requirement and anti-forgery check.
    /// Pages opt out with <c>[AllowAnonymous]</c>.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPlatformMvc(this IServiceCollection services)
    {
        services.AddControllersWithViews(options =>
        {
            // Staff screens need a staff sign-in; the store (area Shop) is open to visitors.
            options.Conventions.Add(new StaffAuthorizationConvention());
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());

            // Phone fields post a calling code and a number; this joins them first.
            options.ValueProviderFactories.Insert(0, new PhoneValueProviderFactory());
        });
        return services;
    }

    /// <summary>
    /// Registers cookie authentication. The cookie carries the API's JWT.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPlatformAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.SlidingExpiration = false;
            })
            .AddCookie(ShopAuth.Scheme, options =>
            {
                // Store customers have their own cookie, sent only to /shop pages.
                options.Cookie.Name = ".Platform.Shop";
                options.Cookie.Path = ShopAuth.PathPrefix;
                options.LoginPath = ShopAuth.PathPrefix + "/account/login";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.SlidingExpiration = false;
            });

        services.AddScoped<IAccountService, AccountService>();
        return services;
    }

    /// <summary>
    /// Registers the typed API client with the bearer-token handler.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Reads the <c>Api</c> section.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPlatformApiClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiOptions>()
            .Bind(configuration.GetSection(ApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddTransient<BearerTokenHandler>();
        services.AddTransient<ForwardedForHandler>();
        services.AddPlatformForwardedHeaders(configuration);
        services
            .AddHttpClient<IApiClient, ApiClient>((sp, client) =>
                client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<ApiOptions>>().Value.BaseUrl))
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<ForwardedForHandler>();
        return services;
    }

    /// <summary>
    /// Registers the shared validators and one API client per CRUD screen.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPlatformScreens(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<PagedRequestValidator>();

        services.AddCrudApiClient<BrandDto, CreateBrandRequest, UpdateBrandRequest>(ApiRoutes.Brands);
        services.AddCrudApiClient<UomDto, CreateUomRequest, UpdateUomRequest>(ApiRoutes.Uoms);
        services.AddScoped<ICatalogApiClient, CatalogApiClient>();
        services.AddCrudApiClient<WarehouseDto, CreateWarehouseRequest, UpdateWarehouseRequest>(ApiRoutes.Warehouses);
        services.AddCrudApiClient<RoleDto, CreateRoleRequest, UpdateRoleRequest>(ApiRoutes.Roles);
        services.AddCrudApiClient<UserDto, CreateUserRequest, UpdateUserRequest>(ApiRoutes.Users);
        services.AddScoped<IStockApiClient, StockApiClient>();
        services.AddScoped<IAuditApiClient, AuditApiClient>();
        services.AddScoped<IUserAccess, UserAccess>();
        services.AddCrudApiClient<PriceListDto, CreatePriceListRequest, UpdatePriceListRequest>(ApiRoutes.PriceLists);
        services.AddScoped<IPricingApiClient, PricingApiClient>();
        services.AddScoped<IDeliveryAreaApiClient, DeliveryAreaApiClient>();
        services.AddScoped<ISalesApiClient, SalesApiClient>();
        services.AddScoped<IShopApiClient, ShopApiClient>();
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Registers the API client for one CRUD resource.
    /// </summary>
    /// <typeparam name="TDto">Read model.</typeparam>
    /// <typeparam name="TCreate">Create request.</typeparam>
    /// <typeparam name="TUpdate">Update request.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <param name="route">Resource route from <see cref="ApiRoutes"/>.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddCrudApiClient<TDto, TCreate, TUpdate>(this IServiceCollection services, string route)
    {
        services.AddScoped<ICrudApiClient<TDto, TCreate, TUpdate>>(sp =>
            new CrudApiClient<TDto, TCreate, TUpdate>(sp.GetRequiredService<IApiClient>(), route));
        return services;
    }
}
