using System.Security.Claims;
using Platform.Api.Middleware;
using Platform.Shared.Common;
using Platform.Shared.Entities.Identity;
using Xunit;

namespace Platform.Tests.Unit;

/// <summary>
/// The role hierarchy: who is below whom, where a role may be attached, and
/// that bad data (loops, missing parents) can never hang or widen access.
/// </summary>
public sealed class RoleTreeTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid BranchManager = Guid.NewGuid();
    private static readonly Guid StoreKeeper = Guid.NewGuid();
    private static readonly Guid Helper = Guid.NewGuid();
    private static readonly Guid SalesManager = Guid.NewGuid();

    /// <summary>
    /// Administrator → Branch manager → Store keeper → Helper, and Administrator → Sales manager.
    /// </summary>
    private static RoleTree Chart() => new(new[]
    {
        new RoleNode(Admin, null, IsSystem: true, "Administrator"),
        new RoleNode(BranchManager, null, IsSystem: false, "Branch manager"),
        new RoleNode(StoreKeeper, BranchManager, IsSystem: false, "Store keeper"),
        new RoleNode(Helper, StoreKeeper, IsSystem: false, "Helper"),
        new RoleNode(SalesManager, Admin, IsSystem: false, "Sales manager"),
    });

    /// <summary>A manager is above everything in their branch, at any depth.</summary>
    [Fact]
    public void RolesBelowAreManaged()
    {
        RoleTree tree = Chart();

        Assert.True(tree.IsBelow(StoreKeeper, new[] { BranchManager }));
        Assert.True(tree.IsBelow(Helper, new[] { BranchManager }));
        Assert.True(tree.IsBelow(BranchManager, new[] { Admin }));
    }

    /// <summary>Nobody is below themselves, a peer, or someone in another branch.</summary>
    [Fact]
    public void OwnPeerAndOtherBranchesAreNotManaged()
    {
        RoleTree tree = Chart();

        Assert.False(tree.IsBelow(BranchManager, new[] { BranchManager }));
        Assert.False(tree.IsBelow(SalesManager, new[] { BranchManager }));
        Assert.False(tree.IsBelow(Admin, new[] { BranchManager }));
        Assert.False(tree.IsBelow(BranchManager, new[] { StoreKeeper }));
    }

    /// <summary>A role without a parent sits directly under the top role.</summary>
    [Fact]
    public void MissingParentMeansUnderTheTop()
    {
        RoleTree tree = Chart();

        Assert.Equal(Admin, tree.ParentOf(BranchManager));
        Assert.Null(tree.ParentOf(Admin));
    }

    /// <summary>A new role can be attached to one's own role or below it, never above.</summary>
    [Fact]
    public void NewRolesAttachAtOrBelowOwnRole()
    {
        RoleTree tree = Chart();

        Assert.True(tree.IsAtOrBelow(BranchManager, new[] { BranchManager }));
        Assert.True(tree.IsAtOrBelow(Helper, new[] { BranchManager }));
        Assert.False(tree.IsAtOrBelow(Admin, new[] { BranchManager }));
    }

    /// <summary>Moving a role under itself or under one of its own juniors is refused.</summary>
    [Fact]
    public void LoopsAreDetected()
    {
        RoleTree tree = Chart();

        Assert.True(tree.WouldLoop(BranchManager, BranchManager));
        Assert.True(tree.WouldLoop(BranchManager, Helper));
        Assert.False(tree.WouldLoop(Helper, SalesManager));
    }

    /// <summary>Stored data that loops (which the API never writes) still answers at once and is still drawn.</summary>
    [Fact]
    public void StoredLoopsNeverHang()
    {
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();
        var tree = new RoleTree(new[]
        {
            new RoleNode(Admin, null, true, "Administrator"),
            new RoleNode(a, b, false, "A"),
            new RoleNode(b, a, false, "B"),
        });

        Assert.Equal(new[] { b }, tree.AncestorsOf(a));
        Assert.Equal(3, tree.Ordered().Count);
    }

    /// <summary>The chart lists the top first, then each role followed by its juniors.</summary>
    [Fact]
    public void ChartIsInOrder()
    {
        var order = Chart().Ordered().Select(o => (o.Node.Id, o.Depth)).ToList();

        Assert.Equal((Admin, 0), order[0]);
        Assert.Equal((BranchManager, 1), order[1]);
        Assert.Equal((StoreKeeper, 2), order[2]);
        Assert.Equal((Helper, 3), order[3]);
        Assert.Equal((SalesManager, 1), order[4]);
    }
}

/// <summary>
/// A signed-in staff token is accepted only while the user is active and
/// their sessions have not been ended since it was issued.
/// </summary>
public sealed class SessionGuardTests
{
    private static readonly DateTime EndedAt = new(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>Builds a token's claims issued at an instant.</summary>
    /// <param name="issuedAt">Issue time.</param>
    /// <returns>The claims.</returns>
    private static ClaimsPrincipal TokenIssuedAt(DateTime issuedAt) => new(new ClaimsIdentity(
        new[] { new Claim("iat", new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString()) }, "test"));

    /// <summary>An active user who never had sessions ended is live.</summary>
    [Fact]
    public void ActiveUserIsLive() =>
        Assert.True(SessionGuard.IsLive(new User { IsActive = true }, TokenIssuedAt(EndedAt)));

    /// <summary>A deactivated or unknown user is refused at once, whatever the token.</summary>
    [Fact]
    public void InactiveOrUnknownUserIsRefused()
    {
        Assert.False(SessionGuard.IsLive(new User { IsActive = false }, TokenIssuedAt(EndedAt.AddHours(1))));
        Assert.False(SessionGuard.IsLive(null, TokenIssuedAt(EndedAt)));
    }

    /// <summary>A token from before "sign out everywhere" is refused; a later sign-in is fine.</summary>
    [Fact]
    public void TokensBeforeTheEndAreRefused()
    {
        var user = new User { IsActive = true, SessionsEndedAt = EndedAt };

        Assert.False(SessionGuard.IsLive(user, TokenIssuedAt(EndedAt.AddMinutes(-5))));
        Assert.False(SessionGuard.IsLive(user, TokenIssuedAt(EndedAt)));
        Assert.True(SessionGuard.IsLive(user, TokenIssuedAt(EndedAt.AddSeconds(1))));
    }

    /// <summary>A token without an issue time cannot prove it came after the end.</summary>
    [Fact]
    public void TokenWithoutIssueTimeIsRefusedAfterAnEnd() =>
        Assert.False(SessionGuard.IsLive(
            new User { IsActive = true, SessionsEndedAt = EndedAt },
            new ClaimsPrincipal(new ClaimsIdentity(Array.Empty<Claim>(), "test"))));
}
