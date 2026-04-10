using TenantManagement.Contracts;

namespace TenantManagement.Services;

public static class OrgUnitTreeBuilder
{
    public static List<OrgUnitTreeNodeDto> BuildNested(IReadOnlyList<OrgUnitCteRow> rows)
    {
        if (rows.Count == 0)
        {
            return new List<OrgUnitTreeNodeDto>();
        }

        var nodes = new Dictionary<Guid, OrgUnitTreeNodeDto>();
        foreach (var r in rows)
        {
            nodes[r.Id] = new OrgUnitTreeNodeDto
            {
                Id = r.Id,
                Name = r.Name,
                UnitType = r.UnitType
            };
        }

        var roots = new List<OrgUnitTreeNodeDto>();
        foreach (var r in rows.OrderBy(x => x.Depth).ThenBy(x => x.Name, StringComparer.Ordinal))
        {
            var node = nodes[r.Id];
            if (r.ParentId is null)
            {
                roots.Add(node);
            }
            else if (nodes.TryGetValue(r.ParentId.Value, out var parent))
            {
                parent.Children.Add(node);
            }
        }

        roots.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        foreach (var n in nodes.Values)
        {
            n.Children.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        }

        return roots;
    }
}
