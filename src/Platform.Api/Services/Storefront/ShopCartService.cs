using Microsoft.Extensions.Options;
using Platform.Api.Common;
using Platform.Api.Common.Exceptions;
using Platform.Api.Mapping.Storefront;
using Platform.Api.Repositories;
using Platform.Api.Services.Catalog;
using Platform.Api.Services.Inventory;
using Platform.Shared.Commerce;
using Platform.Shared.Common;
using Platform.Shared.Constants;
using Platform.Shared.Dtos.Storefront;
using Platform.Shared.Entities.Catalog;
using Platform.Shared.Entities.Common;
using Platform.Shared.Entities.Inventory;
using Platform.Shared.Entities.Sales;
using Platform.Shared.Units;

namespace Platform.Api.Services.Storefront;

/// <summary>
/// The signed-in customer's cart and checkout. The cart holds no stock and
/// no prices (P4); checkout re-prices everything, places the order and holds
/// the stock in one transaction, so two customers can never be promised the
/// same last bag.
/// </summary>
public interface IShopCartService
{
    /// <summary>
    /// Returns the cart, priced now.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The cart.</returns>
    Task<ShopCartDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the delivery PIN code availability is checked for.
    /// </summary>
    /// <param name="request">PIN code.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The cart.</returns>
    Task<ShopCartDto> SetPincodeAsync(SetPincodeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an amount of a SKU (to its line, if it has one).
    /// </summary>
    /// <param name="request">SKU, amount, unit.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The cart.</returns>
    Task<ShopCartDto> AddLineAsync(AddCartLineRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the amount or unit of a line.
    /// </summary>
    /// <param name="skuId">SKU of the line.</param>
    /// <param name="request">New amount and unit.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The cart.</returns>
    Task<ShopCartDto> UpdateLineAsync(Guid skuId, UpdateCartLineRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a line.
    /// </summary>
    /// <param name="skuId">SKU of the line.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The cart.</returns>
    Task<ShopCartDto> RemoveLineAsync(Guid skuId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Places the order for the cart and holds its stock.
    /// </summary>
    /// <param name="request">Address, phone and checkout key.</param>
    /// <param name="cancellationToken">Cancels the transaction.</param>
    /// <returns>The order (the same one again for a repeated key).</returns>
    Task<ShopOrderDto> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Firestore-backed <see cref="IShopCartService"/>.
/// </summary>
public sealed class ShopCartService : IShopCartService
{
    /// <summary>Prefix of order references.</summary>
    private const string OrderSeries = "ORD";

    private readonly ICustomerContext _customer;
    private readonly IRepository<Cart> _carts;
    private readonly IRepository<Sku> _skus;
    private readonly IRepository<Product> _products;
    private readonly IRepository<Order> _orders;
    private readonly IShopPricer _pricer;
    private readonly IAvailabilityService _availability;
    private readonly IUomConversionProvider _conversions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly StorefrontOptions _options;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="customer">Signed-in customer.</param>
    /// <param name="carts">Cart data access.</param>
    /// <param name="skus">SKU data access.</param>
    /// <param name="products">Product data access.</param>
    /// <param name="orders">Order data access.</param>
    /// <param name="pricer">Prices the cart.</param>
    /// <param name="availability">Free stock of serving warehouses.</param>
    /// <param name="conversions">Unit conversion (P7).</param>
    /// <param name="unitOfWork">Commits and runs the checkout transaction.</param>
    /// <param name="currentUser">Caller's organisation.</param>
    /// <param name="timeProvider">Clock.</param>
    /// <param name="options">Hold length.</param>
    public ShopCartService(
        ICustomerContext customer,
        IRepository<Cart> carts,
        IRepository<Sku> skus,
        IRepository<Product> products,
        IRepository<Order> orders,
        IShopPricer pricer,
        IAvailabilityService availability,
        IUomConversionProvider conversions,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        IOptions<StorefrontOptions> options)
    {
        _customer = customer;
        _carts = carts;
        _skus = skus;
        _products = products;
        _orders = orders;
        _pricer = pricer;
        _availability = availability;
        _conversions = conversions;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<ShopCartDto> GetAsync(CancellationToken cancellationToken = default)
    {
        Customer customer = await _customer.RequireAsync(cancellationToken);
        (Cart cart, _) = await LoadCartAsync(customer, cancellationToken);
        return await PriceAsync(cart, customer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ShopCartDto> SetPincodeAsync(SetPincodeRequest request, CancellationToken cancellationToken = default)
    {
        Customer customer = await _customer.RequireAsync(cancellationToken);
        (Cart cart, bool isNew) = await LoadCartAsync(customer, cancellationToken);
        cart.Pincode = request.Pincode.Trim();
        await SaveAsync(cart, isNew, cancellationToken);
        return await PriceAsync(cart, customer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ShopCartDto> AddLineAsync(AddCartLineRequest request, CancellationToken cancellationToken = default)
    {
        Customer customer = await _customer.RequireAsync(cancellationToken);
        (Sku sku, string uom) = await LoadSellableAsync(request.SkuId, request.Uom, cancellationToken);
        (Cart cart, bool isNew) = await LoadCartAsync(customer, cancellationToken);

        CartLine? line = cart.Lines.FirstOrDefault(l => l.SkuId == sku.Id);
        if (line is null)
        {
            if (cart.Lines.Count >= FieldLengths.CartLines)
            {
                throw new BusinessRuleException($"A cart holds at most {FieldLengths.CartLines} items. Place this order first.");
            }

            cart.Lines.Add(new CartLine { SkuId = sku.Id, Quantity = Quantity.Normalise(request.Quantity), Uom = uom });
        }
        else
        {
            // One line per SKU: an amount in another unit is converted to the line's unit (P7).
            IUomConversionService converter = await _conversions.GetAsync(cancellationToken);
            if (!converter.TryConvert(new Quantity(request.Quantity, uom), line.Uom, new SkuUnits(sku.BaseUom, sku.Conversions), out Quantity added))
            {
                throw new FieldValidationException(nameof(AddCartLineRequest.Uom), $"This item is already in your cart in {line.Uom}.");
            }

            line.Quantity = Quantity.Normalise(line.Quantity + added.Value);
        }

        await SaveAsync(cart, isNew, cancellationToken);
        return await PriceAsync(cart, customer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ShopCartDto> UpdateLineAsync(Guid skuId, UpdateCartLineRequest request, CancellationToken cancellationToken = default)
    {
        Customer customer = await _customer.RequireAsync(cancellationToken);
        (Cart cart, bool isNew) = await LoadCartAsync(customer, cancellationToken);
        CartLine line = cart.Lines.FirstOrDefault(l => l.SkuId == skuId) ?? throw new NotFoundException("Cart line");
        (_, string uom) = await LoadSellableAsync(skuId, request.Uom, cancellationToken);
        line.Quantity = Quantity.Normalise(request.Quantity);
        line.Uom = uom;
        await SaveAsync(cart, isNew, cancellationToken);
        return await PriceAsync(cart, customer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ShopCartDto> RemoveLineAsync(Guid skuId, CancellationToken cancellationToken = default)
    {
        Customer customer = await _customer.RequireAsync(cancellationToken);
        (Cart cart, bool isNew) = await LoadCartAsync(customer, cancellationToken);
        if (cart.Lines.RemoveAll(l => l.SkuId == skuId) == 0)
        {
            throw new NotFoundException("Cart line");
        }

        await SaveAsync(cart, isNew, cancellationToken);
        return await PriceAsync(cart, customer, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ShopOrderDto> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        Customer customer = await _customer.RequireAsync(cancellationToken);
        Guid orgId = _currentUser.OrgId ?? throw new AuthenticationFailedException();
        Guid orderId = Order.IdFor(customer.Id, request.ClientId);
        if (await _orders.GetByIdAsync(orderId, cancellationToken) is { } placed)
        {
            return ShopMapping.ToOrderDto(placed);
        }

        (Cart cart, bool isNew) = await LoadCartAsync(customer, cancellationToken);
        if (isNew || cart.Lines.Count == 0)
        {
            throw new BusinessRuleException("Your cart is empty.");
        }

        Address address = ShopMapping.ToAddress(request.Address);
        PricedCart priced = await _pricer.PriceAsync(cart.Lines, customer, address.Pincode, address.State, cancellationToken);
        if (!priced.CanCheckout)
        {
            throw new BusinessRuleException(string.Join(" ", priced.Problems.Concat(priced.Lines
                .Where(l => l.Problem is not null)
                .Select(l => $"{(l.Name.Length > 0 ? l.Name : "An item")}: {l.Problem}"))));
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        Order order = await _unitOfWork.RunInTransactionAsync(async session =>
        {
            // Reads first: the order (a retried checkout), the number, the balances, the cart, the customer.
            if (await session.GetAsync<Order>(orderId) is { } again)
            {
                return again;
            }

            string reference = await DocumentNumbers.NextAsync(session, orgId, $"{OrderSeries}-{FinancialYear.Code(now)}");
            List<Guid> skuIds = priced.Lines.Select(l => l.Sku!.Id).ToList();
            IReadOnlyDictionary<Guid, StockBalance> balances = await session.GetManyAsync<StockBalance>(
                priced.Serving.SelectMany(w => skuIds.Select(sku => StockBalance.IdFor(w.WarehouseId, sku))));
            Cart? storedCart = await session.GetAsync<Cart>(cart.Id);
            Customer? storedCustomer = await session.GetAsync<Customer>(customer.Id);

            // Plan again on the balances read inside the transaction: this is the promise.
            IReadOnlyDictionary<Guid, List<WarehouseStock>> stock = _availability.FromBalances(skuIds, priced.Serving, balances);
            // Decision #2: one warehouse supplies the whole order.
            OrderSource source = AllocationPlanner.PlanOrder(ShopPricer.NeedsOf(priced.Lines), stock)
                ?? throw new BusinessRuleException(
                    $"Someone has just ordered some of these items for PIN code {address.Pincode}. Your cart is kept — please update it and try again.");

            var holds = new List<StockHold>();
            DateTime holdExpiresAt = now.AddMinutes(_options.HoldMinutes);
            foreach (PricedLine line in priced.Lines)
            {
                Sku sku = line.Sku!;
                StockBalance balance = balances[StockBalance.IdFor(source.WarehouseId, sku.Id)];
                balance.Reserved = Quantity.Normalise(balance.Reserved + line.BaseQuantity);
                holds.Add(new StockHold
                {
                    Id = StockHold.IdFor(orderId, source.WarehouseId, sku.Id),
                    OrderId = orderId,
                    ReferenceNo = reference,
                    WarehouseId = source.WarehouseId,
                    SkuId = sku.Id,
                    SkuCode = sku.Code,
                    Quantity = line.BaseQuantity,
                    Uom = sku.BaseUom,
                    Status = StockHoldStatus.Active,
                    ExpiresAt = holdExpiresAt,
                });
            }

            var (earliest, latest) = AllocationPlanner.Window(FinancialYear.IstDate(now), source.LeadTimeDays);
            var created = new Order
            {
                Id = orderId,
                ReferenceNo = reference,
                ClientId = request.ClientId,
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                CustomerGstin = request.Gstin ?? customer.Gstin,
                Phone = request.Phone,
                Address = address,
                SellerState = priced.SellerState!,
                IsInterState = priced.IsInterState,
                Currency = Money.Inr,
                Lines = priced.Lines.Select(ToOrderLine).ToList(),
                Totals = priced.Totals,
                Status = OrderStatus.Placed,
                EarliestDeliveryOn = earliest,
                LatestDeliveryOn = latest,
                HoldExpiresAt = holdExpiresAt,
            };

            session.Add(created);
            holds.ForEach(session.Add);
            foreach (StockBalance balance in balances.Values.Where(b => holds.Any(h => StockBalance.IdFor(h.WarehouseId, h.SkuId) == b.Id)))
            {
                session.Update(balance);
            }

            if (storedCart is not null)
            {
                storedCart.Lines.Clear();
                storedCart.Pincode = address.Pincode;
                session.Update(storedCart);
            }

            if (storedCustomer is { Address: null })
            {
                storedCustomer.Address = address;
                storedCustomer.Phone ??= request.Phone;
                session.Update(storedCustomer);
            }

            return created;
        }, cancellationToken);

        return ShopMapping.ToOrderDto(order);
    }

    /// <summary>
    /// Copies a priced line onto an order line, with the price and tax true now (P8).
    /// </summary>
    /// <param name="line">Priced line.</param>
    /// <returns>The order line.</returns>
    private static OrderLine ToOrderLine(PricedLine line)
    {
        var orderLine = new OrderLine
        {
            SkuId = line.Sku!.Id,
            ProductId = line.Product!.Id,
            SkuCode = line.Sku.Code,
            Name = line.Name,
            HsnCode = line.Product.HsnCode,
            Quantity = line.Line.Quantity,
            Uom = line.Line.Uom,
            BaseQuantity = line.BaseQuantity,
            BaseUom = line.Sku.BaseUom,
            PriceListId = line.Price!.PriceListId,
            UnitPrice = line.UnitPrice!.Value,
            TaxRatePercent = line.Tax!.RatePercent,
            CessPercent = line.Tax.CessPercent,
        };
        GstCalculator.CopyTo(line.Amounts!, orderLine);
        return orderLine;
    }

    /// <summary>
    /// Prices the cart for its PIN code. GST is shown for the customer's saved
    /// state when their saved address has that PIN code, else within the seller's state.
    /// </summary>
    /// <param name="cart">Cart.</param>
    /// <param name="customer">Customer.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The customer's cart.</returns>
    private async Task<ShopCartDto> PriceAsync(Cart cart, Customer customer, CancellationToken cancellationToken)
    {
        string? state = customer.Address is { } saved && saved.Pincode == cart.Pincode ? saved.State : null;
        PricedCart priced = await _pricer.PriceAsync(cart.Lines, customer, cart.Pincode, state, cancellationToken);
        return ShopMapping.ToCartDto(priced, cart.Pincode);
    }

    /// <summary>
    /// Loads the customer's cart, or starts one (not yet saved). A new cart
    /// starts with the PIN code of the customer's saved address.
    /// </summary>
    /// <param name="customer">Customer.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The cart and whether it is new.</returns>
    private async Task<(Cart Cart, bool IsNew)> LoadCartAsync(Customer customer, CancellationToken cancellationToken)
    {
        Guid id = Cart.IdFor(customer.Id);
        return await _carts.GetByIdAsync(id, cancellationToken) is { } cart
            ? (cart, false)
            : (new Cart { Id = id, CustomerId = customer.Id, Pincode = customer.Address?.Pincode }, true);
    }

    /// <summary>
    /// Saves the cart.
    /// </summary>
    /// <param name="cart">Cart.</param>
    /// <param name="isNew">True to insert.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when saved.</returns>
    private async Task SaveAsync(Cart cart, bool isNew, CancellationToken cancellationToken)
    {
        if (isNew)
        {
            _carts.Add(cart);
        }
        else
        {
            _carts.Update(cart);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Loads a SKU that is on sale and checks it can be ordered in a unit.
    /// </summary>
    /// <param name="skuId">SKU.</param>
    /// <param name="uom">Unit asked for.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The SKU and the unit as the catalogue spells it.</returns>
    private async Task<(Sku Sku, string Uom)> LoadSellableAsync(Guid skuId, string uom, CancellationToken cancellationToken)
    {
        Sku sku = await _skus.GetByIdAsync(skuId, cancellationToken) is { IsActive: true } found ? found : throw new NotFoundException("Product");
        if (await _products.GetByIdAsync(sku.ProductId, cancellationToken) is not { IsActive: true })
        {
            throw new NotFoundException("Product");
        }

        IUomConversionService converter = await _conversions.GetAsync(cancellationToken);
        string? unit = converter.ReachableUnits(new SkuUnits(sku.BaseUom, sku.Conversions))
            .Select(u => u.Uom)
            .FirstOrDefault(u => string.Equals(u, uom.Trim(), StringComparison.OrdinalIgnoreCase));
        return unit is null
            ? throw new FieldValidationException(nameof(AddCartLineRequest.Uom), $"This item cannot be ordered in {uom}.")
            : (sku, unit);
    }
}
