using FluentValidation;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Inventory;
using Platform.Shared.Entities.Inventory;
using Platform.Shared.Validation.Common;

namespace Platform.Shared.Validation.Inventory;

/// <summary>
/// Rules for one stock line: a SKU code, a unit and a non-zero amount.
/// Whether the amount may be negative is decided by the document's validator.
/// </summary>
internal sealed class StockLineRequestValidator : AbstractValidator<StockLineRequest>
{
    /// <summary>
    /// Defines the line rules.
    /// </summary>
    /// <param name="allowNegative">True on an adjustment, where a negative amount takes stock away.</param>
    public StockLineRequestValidator(bool allowNegative)
    {
        RuleFor(x => x.SkuCode).NotEmpty().MaximumLength(FieldLengths.Code).WithName("SKU code");
        RuleFor(x => x.Uom).NotEmpty().MaximumLength(FieldLengths.Code).WithName("Unit");
        RuleFor(x => x.Quantity).ValidQuantity();
        if (allowNegative)
        {
            RuleFor(x => x.Quantity).NotEqual(0).WithMessage("Enter an amount other than zero.");
        }
        else
        {
            RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Enter an amount greater than zero.");
        }
    }
}

/// <summary>
/// Rules for the lines of any stock document: at least one, not too many,
/// and each SKU at most once.
/// </summary>
internal static class StockLinesRules
{
    /// <summary>
    /// Adds the line-list rules to a validator.
    /// </summary>
    /// <typeparam name="T">Request type.</typeparam>
    /// <param name="validator">Validator to extend.</param>
    /// <param name="allowNegative">Whether a line may take stock away.</param>
    public static void AddLineRules<T>(this AbstractValidator<T> validator, bool allowNegative)
        where T : IStockDocumentRequest
    {
        validator.RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Add at least one line.")
            .Must(lines => lines.Count <= FieldLengths.StockLines)
            .WithMessage($"A document can have at most {FieldLengths.StockLines} lines; split it into several.")
            .Must(lines => lines.Select(l => l.SkuCode.Trim().ToUpperInvariant()).Distinct().Count() == lines.Count)
            .WithMessage("A SKU is listed twice; put it on one line.");
        validator.RuleForEach(x => x.Lines).SetValidator(new StockLineRequestValidator(allowNegative));
        validator.RuleFor(x => x.Remarks).ValidOptionalRemarks();
    }
}

/// <summary>
/// Validates <see cref="CreateReceiptRequest"/>.
/// </summary>
public class CreateReceiptRequestValidator : AbstractValidator<CreateReceiptRequest>
{
    /// <summary>
    /// Defines the receipt rules.
    /// </summary>
    public CreateReceiptRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("Choose the warehouse receiving the goods.");
        RuleFor(x => x.SupplierName).ValidName().WithName("Supplier");
        RuleFor(x => x.SupplierReferenceNo).ValidOptionalReferenceNo().WithName("Supplier invoice / challan no.");
        this.AddLineRules(allowNegative: false);
    }
}

/// <summary>
/// Validates <see cref="CreateTransferRequest"/>.
/// </summary>
public class CreateTransferRequestValidator : AbstractValidator<CreateTransferRequest>
{
    /// <summary>
    /// Defines the transfer rules.
    /// </summary>
    public CreateTransferRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("Choose the warehouse sending the stock.");
        RuleFor(x => x.ToWarehouseId)
            .NotEmpty().WithMessage("Choose the warehouse receiving the stock.")
            .NotEqual(x => x.WarehouseId).WithMessage("Send to a different warehouse.");
        this.AddLineRules(allowNegative: false);
    }
}

/// <summary>
/// Validates <see cref="CreateAdjustmentRequest"/>: damage and expiry only take
/// stock away (enter positive amounts); a count correction may go either way.
/// </summary>
public class CreateAdjustmentRequestValidator : AbstractValidator<CreateAdjustmentRequest>
{
    /// <summary>Reasons an adjustment may use.</summary>
    public static readonly IReadOnlyList<StockReason> Reasons = new[]
    {
        StockReason.AuditCorrection, StockReason.Damage, StockReason.ExpiryWriteOff,
    };

    /// <summary>
    /// Defines the adjustment rules.
    /// </summary>
    public CreateAdjustmentRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("Choose the warehouse to adjust.");
        RuleFor(x => x.Reason)
            .Must(r => Reasons.Contains(r))
            .WithMessage("Choose count correction, damage or expiry.");
        RuleFor(x => x.Remarks).ValidRequiredRemarks();
        this.AddLineRules(allowNegative: true);
        RuleForEach(x => x.Lines)
            .Must(l => l.Quantity > 0)
            .When(x => x.Reason is StockReason.Damage or StockReason.ExpiryWriteOff)
            .WithMessage("For damage and expiry enter the amount lost as a positive number; it is taken out of stock.");
    }
}

/// <summary>
/// Validates <see cref="ReverseStockDocumentRequest"/>.
/// </summary>
public class ReverseStockDocumentRequestValidator : AbstractValidator<ReverseStockDocumentRequest>
{
    /// <summary>
    /// A reversal must say why.
    /// </summary>
    public ReverseStockDocumentRequestValidator()
    {
        RuleFor(x => x.Remarks).ValidRequiredRemarks();
    }
}
