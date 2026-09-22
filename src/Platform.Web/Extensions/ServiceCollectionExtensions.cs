using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Options;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Validation.Common;
using Platform.Web.Options;
using Platform.Web.Services.Api;
using Platform.Web.Services.Auth;

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
            options.Filters.Add(new AuthorizeFilter());
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
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
        services
            .AddHttpClient<IApiClient, ApiClient>((sp, client) =>
                client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<ApiOptions>>().Value.BaseUrl))
            .AddHttpMessageHandler<BearerTokenHandler>();
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
        services.AddCrudApiClient<RoleDto, CreateRoleRequest, UpdateRoleRequest>(ApiRoutes.Roles);
        services.AddCrudApiClient<UserDto, CreateUserRequest, UpdateUserRequest>(ApiRoutes.Users);
        services.AddScoped<IUserAccess, UserAccess>();

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
