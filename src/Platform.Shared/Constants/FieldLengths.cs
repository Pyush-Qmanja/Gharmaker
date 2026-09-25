namespace Platform.Shared.Constants;

/// <summary>
/// Maximum lengths for string fields. Firestore enforces no lengths, so the
/// shared validators (API and UI) are the only guard; change a length here once.
/// </summary>
public static class FieldLengths
{
    /// <summary>Display name of any entity (brand, user, organisation...).</summary>
    public const int Name = 200;

    /// <summary>URL-safe identifier used in storefront links.</summary>
    public const int Slug = 200;

    /// <summary>Short business code such as a warehouse or SKU code.</summary>
    public const int Code = 50;

    /// <summary>One address line.</summary>
    public const int AddressLine = 200;

    /// <summary>City or state name.</summary>
    public const int Place = 100;

    /// <summary>Email address (RFC 5321 practical limit).</summary>
    public const int Email = 254;

    /// <summary>Phone number in E.164 format including the leading plus.</summary>
    public const int Phone = 16;

    /// <summary>Absolute URL to a file or image.</summary>
    public const int Url = 2048;

    /// <summary>Firebase Authentication user id.</summary>
    public const int AuthUid = 128;

    /// <summary>GST HSN code: 4, 6 or 8 digits.</summary>
    public const int HsnCode = 8;

    /// <summary>SKU variant label, e.g. "600x600 Glossy Ivory".</summary>
    public const int VariantLabel = 100;

    /// <summary>Minimum length of a plain-text password.</summary>
    public const int PasswordMin = 8;

    /// <summary>Maximum length of a plain-text password.</summary>
    public const int PasswordMax = 128;

    /// <summary>Human reference number, ours or a supplier's.</summary>
    public const int ReferenceNo = 50;

    /// <summary>Free-text notes.</summary>
    public const int Remarks = 500;

    /// <summary>Most lines on one stock document (keeps a posting inside one Firestore transaction).</summary>
    public const int StockLines = 100;

    /// <summary>GSTIN: 15 characters.</summary>
    public const int Gstin = 15;

    /// <summary>Most quantity slabs on one price.</summary>
    public const int PriceSlabs = 10;

    /// <summary>Most lines in one cart or order.</summary>
    public const int CartLines = 50;

    /// <summary>Most PIN codes added in one request.</summary>
    public const int PincodesPerRequest = 500;
}
