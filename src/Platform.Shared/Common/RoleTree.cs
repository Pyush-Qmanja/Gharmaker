namespace Platform.Shared.Common;

/// <summary>
/// One role as the hierarchy sees it: its id, the role it reports to, and
/// whether it is the built-in top role.
/// </summary>
/// <param name="Id">Role id.</param>
/// <param name="ParentId">Role it reports to; null means directly under the top role.</param>
/// <param name="IsSystem">True for the built-in Administrator role, the root of the tree.</param>
/// <param name="Name">Display name, for ordering.</param>
public sealed record RoleNode(Guid Id, Guid? ParentId, bool IsSystem, string Name);

/// <summary>
/// The role hierarchy: every role reports to a parent, and the built-in
/// Administrator role is the root. Someone may manage only what is strictly
/// below a role they hold. Pure logic, shared by the API (which enforces it)
/// and the web app (which draws it).
/// </summary>
public sealed class RoleTree
{
    private readonly Dictionary<Guid, RoleNode> _nodes;
    private readonly Guid? _rootId;

    /// <summary>
    /// Builds the tree.
    /// </summary>
    /// <param name="nodes">Every role of the organisation.</param>
    public RoleTree(IEnumerable<RoleNode> nodes)
    {
        _nodes = nodes.ToDictionary(n => n.Id);
        _rootId = _nodes.Values.FirstOrDefault(n => n.IsSystem)?.Id;
    }

    /// <summary>Id of the built-in top role, or null when the organisation has none.</summary>
    public Guid? RootId => _rootId;

    /// <summary>
    /// The role a role reports to. A role with no parent, or whose parent is
    /// unknown, reports to the top role; the top role reports to nobody.
    /// </summary>
    /// <param name="roleId">Role id.</param>
    /// <returns>The parent id, or null for the top role or an unknown role.</returns>
    public Guid? ParentOf(Guid roleId)
    {
        if (!_nodes.TryGetValue(roleId, out RoleNode? node) || node.IsSystem)
        {
            return null;
        }

        return node.ParentId is { } parent && _nodes.ContainsKey(parent) && parent != roleId ? parent : _rootId;
    }

    /// <summary>
    /// Every role above one, nearest first. Stops at a loop, so bad data can
    /// never hang a request.
    /// </summary>
    /// <param name="roleId">Role id.</param>
    /// <returns>Ancestor ids.</returns>
    public IReadOnlyList<Guid> AncestorsOf(Guid roleId)
    {
        var ancestors = new List<Guid>();
        var seen = new HashSet<Guid> { roleId };
        for (Guid? current = ParentOf(roleId); current is { } id && seen.Add(id); current = ParentOf(id))
        {
            ancestors.Add(id);
        }

        return ancestors;
    }

    /// <summary>
    /// Whether a role sits strictly below any of the given roles.
    /// </summary>
    /// <param name="roleId">Role being managed.</param>
    /// <param name="heldRoleIds">Roles the manager holds.</param>
    /// <returns>True when one of the held roles is an ancestor.</returns>
    public bool IsBelow(Guid roleId, IEnumerable<Guid> heldRoleIds)
    {
        var held = heldRoleIds.ToHashSet();
        return AncestorsOf(roleId).Any(held.Contains);
    }

    /// <summary>
    /// Whether a role is one of the given roles or below one of them — a place
    /// where the manager may attach a new role.
    /// </summary>
    /// <param name="roleId">Candidate parent.</param>
    /// <param name="heldRoleIds">Roles the manager holds.</param>
    /// <returns>True when held or below a held role.</returns>
    public bool IsAtOrBelow(Guid roleId, IEnumerable<Guid> heldRoleIds)
    {
        var held = heldRoleIds.ToHashSet();
        return held.Contains(roleId) || AncestorsOf(roleId).Any(held.Contains);
    }

    /// <summary>
    /// Whether moving a role under a new parent would put it below itself.
    /// </summary>
    /// <param name="roleId">Role being moved.</param>
    /// <param name="newParentId">Proposed parent.</param>
    /// <returns>True when the move would create a loop.</returns>
    public bool WouldLoop(Guid roleId, Guid newParentId) =>
        newParentId == roleId || AncestorsOf(newParentId).Contains(roleId);

    /// <summary>
    /// Every role in chart order: the top role first, then each role followed
    /// by the roles reporting to it, siblings by name, with its depth.
    /// </summary>
    /// <returns>Roles and their depth (0 for the top).</returns>
    public IReadOnlyList<(RoleNode Node, int Depth)> Ordered()
    {
        ILookup<Guid?, RoleNode> children = _nodes.Values
            .Where(n => !n.IsSystem)
            .ToLookup(n => ParentOf(n.Id));
        var ordered = new List<(RoleNode, int)>();
        var seen = new HashSet<Guid>();

        void Visit(RoleNode node, int depth)
        {
            if (!seen.Add(node.Id))
            {
                return;
            }

            ordered.Add((node, depth));
            foreach (RoleNode child in children[node.Id].OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                Visit(child, depth + 1);
            }
        }

        IEnumerable<RoleNode> tops = _rootId is { } root
            ? new[] { _nodes[root] }
            : children[null].OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase);
        foreach (RoleNode top in tops)
        {
            Visit(top, 0);
        }

        // Anything left over sits in a loop of bad data; still show it rather than hide it.
        foreach (RoleNode stray in _nodes.Values.Where(n => !seen.Contains(n.Id)).OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase))
        {
            Visit(stray, 1);
        }

        return ordered;
    }
}
