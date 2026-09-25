using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Platform.Api.Services.Inventory.Stock;
using Microsoft.AspNetCore.Authorization;
using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Platform.Api.Common;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Controllers;
using Platform.Api.Mapping.Catalog;
using Platform.Api.Mapping.Identity;
using Platform.Api.Mapping.Inventory;
using Platform.Api.Middleware;
using Platform.Api.Repositories;
using Platform.Api.Security;
using Platform.Api.Security.Authorization;
using Platform.Api.Services;
using Platform.Api.Services.Auth;
using Platform.Api.Services.Catalog;
using Platform.Api.Services.Catalog.Import;
using Platform.Api.Services.Identity;
using Platform.Api.Services.Inventory;
using Platform.Api.Services.Pricing;
using Platform.Api.Services.Sales;
using Platform.Api.Services.Storefront;
using Platform.Api.Mapping.Pricing;
using Platform.Shared.Dtos.Pricing;
using Platform.Shared.Entities.Pricing;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Entities.Identity;
using Platform.Shared.Entities.Inventory;
using Platform.Shared.Validation.Common;

namespace Platform.Api.Extensions;

/// <summary>
/// Groups service registration by concern so <c>Program.cs</c> stays a short
/// list of calls. A new module adds one line to <see cref="AddPlatformModules"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Name of the Firebase Admin app instance owned by this API.</summary>
    private const string FirebaseAppName = "platform-api";

    /// <summary>
    /// Registers Firebase: one shared Google credential, the Firestore client
    /// and the Firebase Authentication admin client (one of each per process),
    /// plus the generic repositories and the unit of work.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Reads the <c>Firebase</c> section.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPlatformData(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FirebaseOptions>()
            .Bind(configuration.GetSection(FirebaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(sp => LoadCredential(sp.GetRequiredService<IOptions<FirebaseOptions>>().Value));
        services.AddSingleton<IFirestoreContext>(sp => new FirestoreContext(
            BuildFirestoreDb(sp.GetRequiredService<IOptions<FirebaseOptions>>().Value, sp.GetRequiredService<GoogleCredential>())));
        services.AddSingleton(sp => BuildFirebaseAuth(
            sp.GetRequiredService<IOptions<FirebaseOptions>>().Value, sp.GetRequiredService<GoogleCredential>()));

        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // One instance per request serves as both the commit side and the staging side.
        services.AddScoped<UnitOfWork>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<IChangeTracker>(sp => sp.GetRequiredService<UnitOfWork>());
        return services;
    }

    /// <summary>
    /// Loads the service-account credential shared by Firestore and Authentication.
    /// When both emulators are used, a placeholder token is returned instead.
    /// </summary>
    /// <param name="options">Firebase settings.</param>
    /// <returns>The credential.</returns>
    /// <exception cref="InvalidOperationException">A real project is used but no key file is configured.</exception>
    private static GoogleCredential LoadCredential(FirebaseOptions options)
    {
        if (options.UseFirestoreEmulator && options.UseAuthEmulator)
        {
            // Emulators ignore credentials but the SDKs still require one.
            return GoogleCredential.FromAccessToken("emulator");
        }

        if (string.IsNullOrWhiteSpace(options.CredentialsPath) || !File.Exists(options.CredentialsPath))
        {
            throw new InvalidOperationException(
                "Firebase:CredentialsPath must point to the service-account JSON file (or configure both emulator hosts).");
        }

        return CredentialFactory.FromFile<ServiceAccountCredential>(options.CredentialsPath).ToGoogleCredential();
    }

    /// <summary>
    /// Builds the Firestore client for the emulator or the real project.
    /// </summary>
    /// <param name="options">Firebase settings.</param>
    /// <param name="credential">Shared credential.</param>
    /// <returns>A ready client.</returns>
    private static FirestoreDb BuildFirestoreDb(FirebaseOptions options, GoogleCredential credential)
    {
        var builder = new FirestoreDbBuilder { ProjectId = options.ProjectId };

        if (options.UseFirestoreEmulator)
        {
            Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", options.FirestoreEmulatorHost);
            builder.EmulatorDetection = EmulatorDetection.EmulatorOnly;
            return builder.Build();
        }

        builder.GoogleCredential = credential;
        return builder.Build();
    }

    /// <summary>
    /// Builds the Firebase Authentication admin client for the emulator or the real project.
    /// </summary>
    /// <param name="options">Firebase settings.</param>
    /// <param name="credential">Shared credential.</param>
    /// <returns>The admin client.</returns>
    private static FirebaseAuth BuildFirebaseAuth(FirebaseOptions options, GoogleCredential credential)
    {
        if (options.UseAuthEmulator)
        {
            // The Admin SDK switches to the emulator when this variable is set.
            Environment.SetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST", options.AuthEmulatorHost);
        }

        FirebaseApp app = FirebaseApp.GetInstance(FirebaseAppName)
            ?? FirebaseApp.Create(new AppOptions { Credential = credential, ProjectId = options.ProjectId }, FirebaseAppName);
        return FirebaseAuth.GetAuth(app);
    }


    /// <summary>
    /// Registers JWT bearer authentication, the token service and the Firebase identity provider.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Reads the <c>Jwt</c> section.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPlatformAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep claim names exactly as issued (ClaimNames), no remapping.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtTokenService.CreateSigningKey(jwt),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimNames.Name,
                };
            });

        services.AddAuthorization(AuthPolicies.Configure);
        services.AddScoped<ICallerAccount, CallerAccount>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAccessHierarchy, AccessHierarchy>();
        services.AddScoped<IAuthorizationHandler, CapabilityAuthorizationHandler>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddHttpClient<IIdentityProvider, FirebaseIdentityProvider>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }

    /// <summary>
    /// Registers every business module: its mapper and its service. Validators
    /// are picked up automatically from <c>Platform.Shared</c>.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Reads the <c>Storefront</c> section.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPlatformModules(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssemblyContaining<PagedRequestValidator>();

        services.AddCrudModule<Brand, BrandDto, CreateBrandRequest, UpdateBrandRequest, BrandMapper, BrandService>();
        services.AddCrudModule<Uom, UomDto, CreateUomRequest, UpdateUomRequest, UomMapper, UomService>();
        services.AddMemoryCache();
        services.AddScoped<IUomConversionProvider, UomConversionProvider>();
        services.AddScoped<ICatalogBrowseService, CatalogBrowseService>();
        services.AddScoped<ICatalogImportService, CatalogImportService>();
        services.AddCrudModule<Warehouse, WarehouseDto, CreateWarehouseRequest, UpdateWarehouseRequest, WarehouseMapper, WarehouseService>();
        services.AddCrudModule<Role, RoleDto, CreateRoleRequest, UpdateRoleRequest, RoleMapper, RoleService>();
        services.AddCrudModule<User, UserDto, CreateUserRequest, UpdateUserRequest, UserMapper, UserService>();
        services.AddScoped<IUserSessionService>(sp => (UserService)sp.GetRequiredService<ICrudService<UserDto, CreateUserRequest, UpdateUserRequest>>());
        services.AddScoped<IStockLineResolver, StockLineResolver>();
        services.AddScoped<IStockPoster, StockPoster>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IStockQueryService, StockQueryService>();
        services.AddScoped<IStockReconcileService, StockReconcileService>();
        services.AddScoped<IOpeningStockImportService, OpeningStockImportService>();
        services.AddScoped<IAuditService, AuditService>();

        // Phase 3: pricing and GST, delivery areas, customers and orders, the storefront.
        services.AddOptions<StorefrontOptions>()
            .Bind(configuration.GetSection(StorefrontOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddCrudModule<PriceList, PriceListDto, CreatePriceListRequest, UpdatePriceListRequest, PriceListMapper, PriceListService>();
        services.AddScoped<ITaxRateProvider, TaxRateProvider>();
        services.AddScoped<ITaxRateService, TaxRateService>();
        services.AddScoped<IPriceResolver, PriceResolver>();
        services.AddScoped<IPriceService, PriceService>();
        services.AddScoped<IDeliveryAreaService, DeliveryAreaService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IBusinessSettingsService, BusinessSettingsService>();
        services.AddScoped<IOrderCloser, OrderCloser>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddSingleton<IStorefrontOrganisation, StorefrontOrganisation>();
        services.AddScoped<ICustomerContext, CustomerContext>();
        services.AddScoped<IShopPricer, ShopPricer>();
        services.AddScoped<IShopCatalogService, ShopCatalogService>();
        services.AddScoped<IShopCartService, ShopCartService>();
        services.AddScoped<IShopOrderService, ShopOrderService>();
        services.AddScoped<IShopAccountService, ShopAccountService>();
        services.AddHostedService<HoldExpiryWorker>();

        EnsureCrudControllersDeclareCapabilities();

        return services;
    }

    /// <summary>
    /// Fails startup if any CRUD controller lacks <see cref="CrudCapabilitiesAttribute"/>,
    /// so no resource can ship guarded only by "signed in" (P6).
    /// </summary>
    /// <exception cref="InvalidOperationException">An unguarded CRUD controller exists.</exception>
    private static void EnsureCrudControllersDeclareCapabilities()
    {
        string[] unguarded = typeof(CrudControllerBase<,,>).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && IsCrudController(t))
            .Where(t => t.GetCustomAttributes(typeof(CrudCapabilitiesAttribute), inherit: true).Length == 0)
            .Select(t => t.Name)
            .ToArray();

        if (unguarded.Length > 0)
        {
            throw new InvalidOperationException(
                $"CRUD controllers missing [CrudCapabilities]: {string.Join(", ", unguarded)}.");
        }
    }

    /// <summary>
    /// Checks whether a type derives from <see cref="CrudControllerBase{TDto,TCreate,TUpdate}"/>.
    /// </summary>
    /// <param name="type">Type to test.</param>
    /// <returns>True for CRUD controllers.</returns>
    private static bool IsCrudController(Type type)
    {
        for (Type? current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(CrudControllerBase<,,>))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Registers the mapper and service for one CRUD resource in a single call.
    /// </summary>
    /// <typeparam name="TEntity">Persisted entity.</typeparam>
    /// <typeparam name="TDto">Read model.</typeparam>
    /// <typeparam name="TCreate">Create request body.</typeparam>
    /// <typeparam name="TUpdate">Update request body.</typeparam>
    /// <typeparam name="TMapper">Mapper implementation.</typeparam>
    /// <typeparam name="TService">Service implementation.</typeparam>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddCrudModule<TEntity, TDto, TCreate, TUpdate, TMapper, TService>(this IServiceCollection services)
        where TEntity : Platform.Shared.Entities.Common.BaseEntity, new()
        where TMapper : class, IEntityMapper<TEntity, TDto, TCreate, TUpdate>
        where TService : class, ICrudService<TDto, TCreate, TUpdate>
    {
        services.AddSingleton<IEntityMapper<TEntity, TDto, TCreate, TUpdate>, TMapper>();
        services.AddScoped<ICrudService<TDto, TCreate, TUpdate>, TService>();
        return services;
    }

    /// <summary>
    /// Registers ProblemDetails and the global exception handler.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPlatformErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    /// <summary>
    /// Registers Swagger with a Bearer-token button for trying secured endpoints.
    /// Served in Development, or anywhere when <c>Swagger:Enabled</c> is true
    /// (see <c>Program.cs</c>).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPlatformSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Platform API",
                Version = "v1",
                Description = "To try secured endpoints: call POST /api/auth/login, copy accessToken "
                    + "from the response, click Authorize and paste it (without the word Bearer).",
            });

            // The XML doc comments required on every member become the Swagger text:
            // Platform.Api.xml for endpoints, Platform.Shared.xml for DTO fields.
            foreach (string xmlPath in Directory.GetFiles(AppContext.BaseDirectory, "Platform.*.xml"))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }

            options.SupportNonNullableReferenceTypes();

            var bearer = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            };
            options.AddSecurityDefinition("Bearer", bearer);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = Array.Empty<string>() });
        });
        return services;
    }
}
