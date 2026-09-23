using Platform.Shared.Dtos.Common;
using Platform.Shared.Entities.Catalog;

namespace Platform.Shared.Dtos.Catalog;

/// <summary>
/// Read model of a unit of measure.
/// </summary>
public class UomDto : EntityDto
{
    /// <summary>Unit code, e.g. <c>BAG</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>What the unit measures.</summary>
    public UomDimension Dimension { get; set; }

    /// <summary>Size in the dimension's reference unit; null for packaging units.</summary>
    public decimal? BaseFactor { get; set; }

    /// <summary>False once deactivated.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Editable unit fields shared by create and update.
/// </summary>
public interface IUomFields
{
    /// <summary>Unit code.</summary>
    string Code { get; }

    /// <summary>Display name.</summary>
    string Name { get; }

    /// <summary>What the unit measures.</summary>
    UomDimension Dimension { get; }

    /// <summary>Size in the dimension's reference unit; empty for packaging units.</summary>
    decimal? BaseFactor { get; }
}

/// <summary>
/// Body of <c>POST /api/uoms</c>.
/// </summary>
public class CreateUomRequest : IUomFields
{
    /// <inheritdoc />
    public string Code { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public UomDimension Dimension { get; set; }

    /// <inheritdoc />
    public decimal? BaseFactor { get; set; }
}

/// <summary>
/// Body of <c>PUT /api/uoms/{id}</c>.
/// </summary>
public class UpdateUomRequest : IUomFields, IActivatableRequest
{
    /// <inheritdoc />
    public string Code { get; set; } = string.Empty;

    /// <inheritdoc />
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc />
    public UomDimension Dimension { get; set; }

    /// <inheritdoc />
    public decimal? BaseFactor { get; set; }

    /// <summary>False to deactivate, true to restore.</summary>
    public bool IsActive { get; set; } = true;
}
