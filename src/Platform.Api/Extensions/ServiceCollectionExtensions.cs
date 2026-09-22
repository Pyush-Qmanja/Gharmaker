using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Platform.Api.Common;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Mapping.Catalog;
using Platform.Api.Middleware;
using Platform.Api.Repositories;
using Platform.Api.Security;
using Platform.Api.Services;
using Platform.Api.Services.Auth;
using Platform.Api.Services.Catalog;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Catalog;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Entities.Identity;
using Platform.Shared.Validation.Common;

namespace Platform.Api.Extensions;

/// <summary>
/// Groups service registration by concern so <c>Program.cs</c> stays a short
/// list of calls. A new module adds one line to <see cref="AddPlatformModules"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Firestore client (one per process), the generic
    /// repositories and the unit of work.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Reads the <c>Firestore</c> section.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPlatformData(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FirestoreOptions>()
            .Bind(configuration.GetSection(FirestoreOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IFirestoreContext>(sp =>
            new FirestoreContext(BuildFirestoreDb(sp.GetRequiredService<IOptions<FirestoreOptions>>().Value)));

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
    /// Builds the Firestore client: the emulator when <see cref="FirestoreOptions.EmulatorHost"/>
    /// is set, otherwise the real project using the service-account key file.
    /// </summary>
    /// <param name="options">Firestore settings.</param>
    /// <returns>A ready client.</returns>
    /// <exception cref="InvalidOperationException">Neither an emulator nor a key file is configured.</exception>
    private static FirestoreDb BuildFirestoreDb(FirestoreOptions options)
    {
        var builder = new FirestoreDbBuilder { ProjectId = options.ProjectId };

        if (!string.IsNullOrWhiteSpace(options.EmulatorHost))
        {
            Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", options.EmulatorHost);
            builder.EmulatorDetection = EmulatorDetection.EmulatorOnly;
            return builder.Build();
        }

        if (string.IsNullOrWhiteSpace(options.CredentialsPath) || !File.Exists(options.CredentialsPath))
        {
            throw new InvalidOperationException(
                "Firestore:CredentialsPath must point to the service-account JSON file (or set Firestore:EmulatorHost).");
        }

        builder.GoogleCredential = CredentialFactory
            .FromFile<ServiceAccountCredential>(options.CredentialsPath)
            .ToGoogleCredential();
        return builder.Build();
    }

    /// <summary>
    /// Registers JWT bearer authentication, the token service and password hashing.
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

        services.AddAuthorization();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }

    /// <summary>
    /// Registers every business module: its mapper and its service. Validators
    /// are picked up automatically from <c>Platform.Shared</c>.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPlatformModules(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<PagedRequestValidator>();

        services.AddCrudModule<Brand, BrandDto, CreateBrandRequest, UpdateBrandRequest, BrandMapper, BrandService>();

        return services;
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
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddPlatformSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Platform API", Version = "v1" });

            string xmlPath = Path.Combine(AppContext.BaseDirectory, "Platform.Api.xml");
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

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
