namespace Platform.Shared.Dtos.Storefront;

/// <summary>
/// Body of <c>POST /api/storefront/auth/register</c>: a customer opens an account.
/// </summary>
public class ShopRegisterRequest
{
    /// <summary>Full name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Email to sign in with.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Password (kept only by Firebase Authentication).</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Mobile number, e.g. +919876543210.</summary>
    public string? Phone { get; set; }

    /// <summary>Business name, for contractors and builders.</summary>
    public string? CompanyName { get; set; }
}

/// <summary>
/// A signed-in customer session.
/// </summary>
public class ShopSessionDto
{
    /// <summary>JWT for the storefront API.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>UTC instant the token stops working.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Customer id.</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Email.</summary>
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// The customer's own account details.
/// </summary>
public class ShopProfileDto
{
    /// <summary>Customer id.</summary>
    public Guid Id { get; set; }

    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Mobile number.</summary>
    public string? Phone { get; set; }

    /// <summary>Business name.</summary>
    public string? CompanyName { get; set; }

    /// <summary>GSTIN for B2B invoices.</summary>
    public string? Gstin { get; set; }

    /// <summary>Default delivery address.</summary>
    public ShopAddressDto? Address { get; set; }
}

/// <summary>
/// Body of <c>PUT /api/storefront/me</c>.
/// </summary>
public class UpdateShopProfileRequest
{
    /// <summary>Name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mobile number.</summary>
    public string? Phone { get; set; }

    /// <summary>Business name.</summary>
    public string? CompanyName { get; set; }

    /// <summary>GSTIN for B2B invoices.</summary>
    public string? Gstin { get; set; }

    /// <summary>Default delivery address; null to keep none.</summary>
    public ShopAddressDto? Address { get; set; }
}
