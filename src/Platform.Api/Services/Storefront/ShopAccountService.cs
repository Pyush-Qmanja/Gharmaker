using Platform.Api.Common.Exceptions;
using Platform.Api.Firestore;
using Platform.Api.Mapping;
using Platform.Api.Mapping.Storefront;
using Platform.Api.Repositories;
using Platform.Api.Security;
using Platform.Shared.Dtos.Auth;
using Platform.Shared.Dtos.Storefront;
using Platform.Shared.Entities.Sales;

namespace Platform.Api.Services.Storefront;

/// <summary>
/// Customer accounts of the store: open one, sign in, and keep the profile.
/// Firebase Authentication keeps the password; the API issues a customer
/// token that staff endpoints refuse. Staff and customers never share a sign-in.
/// </summary>
public interface IShopAccountService
{
    /// <summary>
    /// Opens an account and signs the customer in.
    /// </summary>
    /// <param name="request">Name, email, password.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The session.</returns>
    Task<ShopSessionDto> RegisterAsync(ShopRegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs a customer in.
    /// </summary>
    /// <param name="request">Email and password.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The session.</returns>
    Task<ShopSessionDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the signed-in customer's profile.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The profile.</returns>
    Task<ShopProfileDto> GetProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the signed-in customer's profile.
    /// </summary>
    /// <param name="request">New values.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The profile.</returns>
    Task<ShopProfileDto> UpdateProfileAsync(UpdateShopProfileRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IShopAccountService"/>.
/// </summary>
public sealed class ShopAccountService : IShopAccountService
{
    private static readonly string AuthUidField = FirestoreNaming.Field(nameof(Customer.AuthUid));

    /// <summary>
    /// The one answer to a registration that cannot go ahead because the email is
    /// taken — by a customer or by staff. It never says which, so the form cannot
    /// be used to find out who has an account (with the registration rate limit,
    /// it cannot be used to test many emails either).
    /// </summary>
    private const string CannotRegisterMessage =
        "We could not create an account with these details. If you already have an account, sign in instead.";

    private readonly IIdentityProvider _identityProvider;
    private readonly ITokenService _tokens;
    private readonly IRepository<Customer> _customers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICustomerContext _customer;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="identityProvider">Firebase Authentication.</param>
    /// <param name="tokens">Issues customer tokens.</param>
    /// <param name="customers">Customer data access.</param>
    /// <param name="unitOfWork">Commits changes.</param>
    /// <param name="customer">Signed-in customer.</param>
    public ShopAccountService(
        IIdentityProvider identityProvider,
        ITokenService tokens,
        IRepository<Customer> customers,
        IUnitOfWork unitOfWork,
        ICustomerContext customer)
    {
        _identityProvider = identityProvider;
        _tokens = tokens;
        _customers = customers;
        _unitOfWork = unitOfWork;
        _customer = customer;
    }

    /// <inheritdoc />
    public async Task<ShopSessionDto> RegisterAsync(ShopRegisterRequest request, CancellationToken cancellationToken = default)
    {
        string email = request.Email.Trim().ToLowerInvariant();
        if (await _customers.ExistsAsync(nameof(Customer.Email), email, cancellationToken: cancellationToken))
        {
            throw new BusinessRuleException(CannotRegisterMessage);
        }

        string name = DtoMapping.Clean(request.Name);
        string authUid;
        try
        {
            authUid = await _identityProvider.CreateAccountAsync(email, request.Password, name, cancellationToken);
        }
        catch (ConflictException)
        {
            // Also a staff email: the answer must not tell anyone which emails are in use.
            throw new BusinessRuleException(CannotRegisterMessage);
        }

        var customer = new Customer
        {
            Name = name,
            Email = email,
            Phone = DtoMapping.CleanOptional(request.Phone),
            CompanyName = DtoMapping.CleanOptional(request.CompanyName),
            AuthUid = authUid,
        };
        _customers.Add(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Session(customer);
    }

    /// <inheritdoc />
    public async Task<ShopSessionDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        string email = request.Email.Trim().ToLowerInvariant();
        string authUid = await _identityProvider.VerifyPasswordAsync(email, request.Password, cancellationToken)
            ?? throw new AuthenticationFailedException();
        Customer customer = (await _customers.ListAsync(_customers.Query().WhereEqualTo(AuthUidField, authUid).Limit(1), cancellationToken))
            .FirstOrDefault(c => c.IsActive)
            ?? throw new AuthenticationFailedException();
        return Session(customer);
    }

    /// <inheritdoc />
    public async Task<ShopProfileDto> GetProfileAsync(CancellationToken cancellationToken = default) =>
        ToProfile(await _customer.RequireAsync(cancellationToken));

    /// <inheritdoc />
    public async Task<ShopProfileDto> UpdateProfileAsync(UpdateShopProfileRequest request, CancellationToken cancellationToken = default)
    {
        Customer customer = await _customer.RequireAsync(cancellationToken);
        customer.Name = DtoMapping.Clean(request.Name);
        customer.Phone = DtoMapping.CleanOptional(request.Phone);
        customer.CompanyName = DtoMapping.CleanOptional(request.CompanyName);
        customer.Gstin = DtoMapping.CleanOptional(request.Gstin)?.ToUpperInvariant();
        customer.Address = request.Address is null ? null : ShopMapping.ToAddress(request.Address);
        _customers.Update(customer);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToProfile(customer);
    }

    /// <summary>
    /// Issues a session for a customer.
    /// </summary>
    /// <param name="customer">Customer.</param>
    /// <returns>The session.</returns>
    private ShopSessionDto Session(Customer customer)
    {
        var (token, expiresAt) = _tokens.CreateCustomerToken(customer);
        return new ShopSessionDto
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            CustomerId = customer.Id,
            Name = customer.Name,
            Email = customer.Email,
        };
    }

    /// <summary>
    /// Maps the customer's own profile.
    /// </summary>
    /// <param name="customer">Customer.</param>
    /// <returns>The profile.</returns>
    private static ShopProfileDto ToProfile(Customer customer) => new()
    {
        Id = customer.Id,
        Name = customer.Name,
        Email = customer.Email,
        Phone = customer.Phone,
        CompanyName = customer.CompanyName,
        Gstin = customer.Gstin,
        Address = customer.Address is null ? null : ShopMapping.ToAddressDto(customer.Address),
    };
}
