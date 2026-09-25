using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Platform.Api.Controllers.Storefront;
using Platform.Shared.Dtos.Common;
using Platform.Shared.Dtos.Storefront;
using Xunit;

namespace Platform.Tests.Opacity;

/// <summary>
/// P1 — warehouse opacity, enforced at the API. These tests read the
/// storefront contract by reflection: every customer-facing endpoint, every
/// type it can return, and every property of those types, all the way down.
/// A live scan of real responses runs in the API test script as well.
/// </summary>
public sealed partial class StorefrontOpacityTests
{
    /// <summary>
    /// Words that must never name a field a customer receives (P1 and
    /// <c>.claude/rules/storefront.md</c>): where stock is, who supplied it,
    /// what it cost, and how it is held or allocated.
    /// </summary>
    private static readonly HashSet<string> ForbiddenWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Warehouse", "Bin", "Batch", "Lot", "Shade", "Lat", "Lng", "Latitude", "Longitude",
        "Supplier", "Cost", "Margin", "Allocation", "Allocated", "Hold", "Held", "Reserved", "Location",
    };

    /// <summary>
    /// Every storefront endpoint, as "VERB route". Adding an endpoint means adding it
    /// here in the same change (the rule), after checking what it returns.
    /// </summary>
    private static readonly string[] KnownEndpoints =
    {
        "GET api/storefront/categories",
        "GET api/storefront/brands",
        "GET api/storefront/products",
        "GET api/storefront/products/{id:guid}",
        "POST api/storefront/auth/register",
        "POST api/storefront/auth/login",
        "GET api/storefront/me",
        "PUT api/storefront/me",
        "GET api/storefront/cart",
        "PUT api/storefront/cart/pincode",
        "POST api/storefront/cart/lines",
        "PUT api/storefront/cart/lines/{skuId:guid}",
        "DELETE api/storefront/cart/lines/{skuId:guid}",
        "POST api/storefront/checkout",
        "GET api/storefront/orders",
        "GET api/storefront/orders/{id:guid}",
        "POST api/storefront/orders/{id:guid}/cancel",
    };

    /// <summary>
    /// No property of any storefront DTO, at any depth, is named with a forbidden word.
    /// </summary>
    [Fact]
    public void StorefrontDtosCarryNoWarehouseDetail()
    {
        var violations = new List<string>();
        foreach (Type type in StorefrontDtoTypes())
        {
            Walk(type, type.Name, violations, new HashSet<Type>());
        }

        Assert.True(violations.Count == 0, "Forbidden fields in storefront DTOs:\n" + string.Join("\n", violations));
    }

    /// <summary>
    /// Every storefront action returns storefront DTOs only (or a page of them) —
    /// never an entity or an internal DTO that could grow a warehouse field.
    /// </summary>
    [Fact]
    public void StorefrontEndpointsReturnOnlyStorefrontTypes()
    {
        var violations = new List<string>();
        foreach (MethodInfo action in StorefrontActions())
        {
            Type returned = Unwrap(action.ReturnType);
            foreach (Type type in ComplexTypesIn(returned))
            {
                if (type.Namespace != typeof(ShopProductDto).Namespace)
                {
                    violations.Add($"{action.DeclaringType!.Name}.{action.Name} returns {type.FullName}");
                }
            }

            Walk(returned, $"{action.DeclaringType!.Name}.{action.Name}", violations, new HashSet<Type>());
        }

        Assert.True(violations.Count == 0, "Storefront endpoints returning non-storefront types:\n" + string.Join("\n", violations));
    }

    /// <summary>
    /// The storefront has exactly the endpoints listed in this suite, so a new
    /// customer-facing endpoint cannot ship without being reviewed here.
    /// </summary>
    [Fact]
    public void EveryStorefrontEndpointIsListedInThisSuite()
    {
        var actual = StorefrontActions().SelectMany(Describe).Order(StringComparer.Ordinal).ToList();
        var expected = KnownEndpoints.Order(StringComparer.Ordinal).ToList();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Storefront controllers live in their own namespace, the one the storefront
    /// rules (.claude/rules/storefront.md) and this suite cover.
    /// </summary>
    [Fact]
    public void StorefrontControllersLiveInTheirOwnNamespace()
    {
        Assert.All(
            StorefrontActions().Select(a => a.DeclaringType!).Distinct(),
            controller => Assert.StartsWith("Platform.Api.Controllers.Storefront", controller.Namespace, StringComparison.Ordinal));
    }

    /// <summary>
    /// The checker itself works: the staff order DTO, which does show where stock is
    /// held, is flagged. Without this a broken checker would pass everything.
    /// </summary>
    [Fact]
    public void CheckerFlagsWarehouseFieldsInStaffTypes()
    {
        var violations = new List<string>();
        Walk(typeof(Platform.Shared.Dtos.Sales.OrderDto), "OrderDto", violations, new HashSet<Type>());

        Assert.Contains(violations, v => v.Contains("WarehouseCode", StringComparison.Ordinal));
        Assert.Contains(violations, v => v.Contains("Holds", StringComparison.Ordinal));
    }

    /// <summary>
    /// Lists the public types of the storefront DTO namespace.
    /// </summary>
    /// <returns>The types.</returns>
    private static IEnumerable<Type> StorefrontDtoTypes() =>
        typeof(ShopProductDto).Assembly.GetTypes()
            .Where(t => t.IsPublic && t.Namespace == typeof(ShopProductDto).Namespace);

    /// <summary>
    /// Lists the actions of every storefront controller.
    /// </summary>
    /// <returns>The action methods.</returns>
    private static IEnumerable<MethodInfo> StorefrontActions() =>
        typeof(StorefrontControllerBase).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(StorefrontControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    /// <summary>
    /// Describes an action as "VERB route" for each HTTP method it answers.
    /// </summary>
    /// <param name="action">Action method.</param>
    /// <returns>The descriptions.</returns>
    private static IEnumerable<string> Describe(MethodInfo action)
    {
        string prefix = action.DeclaringType!.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
        foreach (HttpMethodAttribute http in action.GetCustomAttributes<HttpMethodAttribute>())
        {
            string template = string.IsNullOrEmpty(http.Template) ? prefix : $"{prefix}/{http.Template}";
            foreach (string verb in http.HttpMethods)
            {
                yield return $"{verb} {template}";
            }
        }
    }

    /// <summary>
    /// Unwraps <c>Task&lt;ActionResult&lt;T&gt;&gt;</c> and similar to <c>T</c>.
    /// </summary>
    /// <param name="type">Declared return type.</param>
    /// <returns>The payload type.</returns>
    private static Type Unwrap(Type type)
    {
        while (type.IsGenericType
            && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ActionResult<>)))
        {
            type = type.GetGenericArguments()[0];
        }

        return type;
    }

    /// <summary>
    /// Finds the complex (non-primitive, non-collection) types inside a type,
    /// looking through collections and <see cref="PagedResult{T}"/>.
    /// </summary>
    /// <param name="type">Type to look into.</param>
    /// <returns>The complex types found at the top level.</returns>
    private static IEnumerable<Type> ComplexTypesIn(Type type)
    {
        if (IsSimple(type))
        {
            yield break;
        }

        if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(PagedResult<>) || IsCollection(type)))
        {
            foreach (Type argument in type.GetGenericArguments().SelectMany(ComplexTypesIn))
            {
                yield return argument;
            }

            yield break;
        }

        yield return type;
    }

    /// <summary>
    /// Checks every property of a type, recursively, for forbidden words.
    /// </summary>
    /// <param name="type">Type to check.</param>
    /// <param name="path">Where it was reached from, for the message.</param>
    /// <param name="violations">Collected problems.</param>
    /// <param name="seen">Types already checked (stops cycles).</param>
    private static void Walk(Type type, string path, List<string> violations, HashSet<Type> seen)
    {
        foreach (Type complex in ComplexTypesIn(type))
        {
            if (!seen.Add(complex))
            {
                continue;
            }

            foreach (PropertyInfo property in complex.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                string where = $"{path} → {complex.Name}.{property.Name}";
                if (Words(property.Name).FirstOrDefault(ForbiddenWords.Contains) is { } word)
                {
                    violations.Add($"{where} (\"{word}\")");
                }

                Walk(property.PropertyType, where, violations, seen);
            }
        }
    }

    /// <summary>
    /// Splits a PascalCase name into its words: "WarehouseCode" → Warehouse, Code.
    /// </summary>
    /// <param name="name">Property name.</param>
    /// <returns>The words.</returns>
    private static IEnumerable<string> Words(string name) => WordPattern().Matches(name).Select(m => m.Value);

    /// <summary>
    /// Checks for types that carry no fields of their own.
    /// </summary>
    /// <param name="type">Type.</param>
    /// <returns>True for primitives, strings, money, dates, ids and enums.</returns>
    private static bool IsSimple(Type type)
    {
        Type inner = Nullable.GetUnderlyingType(type) ?? type;
        return inner.IsPrimitive || inner.IsEnum || inner == typeof(string) || inner == typeof(decimal)
            || inner == typeof(Guid) || inner == typeof(DateTime) || inner == typeof(DateOnly) || inner == typeof(DateTimeOffset);
    }

    /// <summary>
    /// Checks for list-like generic types.
    /// </summary>
    /// <param name="type">Type.</param>
    /// <returns>True for lists and other enumerables.</returns>
    private static bool IsCollection(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

    /// <summary>A capital followed by lower-case letters or digits, or a run of capitals.</summary>
    /// <returns>The compiled regex.</returns>
    [GeneratedRegex("[A-Z][a-z0-9]*|[a-z0-9]+")]
    private static partial Regex WordPattern();
}
