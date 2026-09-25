using FluentValidation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Identity;
using Platform.Shared.Dtos.Inventory;
using Platform.Web.Common;
using Platform.Web.Services.Api;

namespace Platform.Web.Controllers;

/// <summary>
/// Warehouse screens. Everything is inherited from <see cref="CrudController{TDto,TCreate,TUpdate}"/>;
/// the API limits the list to the user's scope. This class only loads the
/// users offered as "responsible person" and which warehouses' stock the user may open.
/// </summary>
public sealed class WarehousesController : CrudController<WarehouseDto, CreateWarehouseRequest, UpdateWarehouseRequest>
{
    private readonly ICrudApiClient<UserDto, CreateUserRequest, UpdateUserRequest> _users;
    private readonly IStockApiClient _stock;

    /// <summary>
    /// Creates the controller.
    /// </summary>
    /// <param name="api">Warehouse API client.</param>
    /// <param name="createValidator">Shared create validator.</param>
    /// <param name="updateValidator">Shared update validator.</param>
    /// <param name="users">User API client, for the responsible-person drop-down.</param>
    /// <param name="stock">Stock API client, for the "View stock" link on each row.</param>
    public WarehousesController(
        ICrudApiClient<WarehouseDto, CreateWarehouseRequest, UpdateWarehouseRequest> api,
        IValidator<CreateWarehouseRequest> createValidator,
        IValidator<UpdateWarehouseRequest> updateValidator,
        ICrudApiClient<UserDto, CreateUserRequest, UpdateUserRequest> users,
        IStockApiClient stock)
        : base(api, createValidator, updateValidator)
    {
        _users = users;
        _stock = stock;
    }

    /// <inheritdoc />
    protected override string SingularName => "Warehouse";

    /// <inheritdoc />
    protected override string PluralName => "Warehouses";

    /// <inheritdoc />
    protected override UpdateWarehouseRequest ToUpdateRequest(WarehouseDto dto) => new()
    {
        Code = dto.Code,
        Name = dto.Name,
        Type = dto.Type,
        Address = new AddressDto
        {
            Line1 = dto.Address.Line1,
            Line2 = dto.Address.Line2,
            City = dto.Address.City,
            State = dto.Address.State,
            Pincode = dto.Address.Pincode,
        },
        Lat = dto.Lat,
        Lng = dto.Lng,
        OwnerUserId = dto.OwnerUserId,
        IsActive = dto.IsActive,
    };

    /// <summary>
    /// Loads active users for the responsible-person drop-down, a name map for the table,
    /// and the warehouses whose stock the user may open.
    /// </summary>
    /// <param name="cancellationToken">Aborted when the browser disconnects.</param>
    /// <returns>A task that completes when ViewData is ready.</returns>
    protected override async Task PrepareViewAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<UserDto> users = await _users.ListForLookupAsync(cancellationToken);
        ViewData[ViewDataKeys.UserNames] = users.ToDictionary(u => u.Id, u => u.Name);
        ViewData[ViewDataKeys.UserOptions] = users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new SelectListItem(u.Name, u.Id.ToString()))
            .ToList();

        var stockWarehouses = await _stock.GetWarehousesAsync(cancellationToken);
        ViewData[ViewDataKeys.StockWarehouseIds] = (stockWarehouses.Value ?? new List<StockWarehouseDto>())
            .Where(w => w.CanView)
            .Select(w => w.Id)
            .ToHashSet();
    }
}
