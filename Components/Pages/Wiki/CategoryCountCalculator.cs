using Wiki_Blaze.Data.Entities;

namespace Wiki_Blaze.Components.Pages.Wiki;

public sealed record CategoryCount(int DirectCount, int TotalCount);

public static class CategoryCountCalculator
{
    public static IReadOnlyDictionary<int, CategoryCount> Calculate(
        IEnumerable<WikiCategory>? categories,
        IEnumerable<WikiPage>? pages)
    {
        var directCounts = (pages ?? Enumerable.Empty<WikiPage>())
            .GroupBy(page => page.CategoryId)
            .ToDictionary(group => group.Key, group => group.Count());

        if (categories == null)
        {
            return directCounts.ToDictionary(
                entry => entry.Key,
                entry => new CategoryCount(entry.Value, entry.Value));
        }

        var categoryList = categories.ToList();
        if (categoryList.Count == 0)
        {
            return directCounts.ToDictionary(
                entry => entry.Key,
                entry => new CategoryCount(entry.Value, entry.Value));
        }

        var categoryIds = categoryList.Select(category => category.Id).ToHashSet();
        var childrenByParent = categoryList
            .Where(category => category.ParentId.HasValue && categoryIds.Contains(category.ParentId.Value))
            .GroupBy(category => category.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(category => category.Id).ToList());

        var totalCache = new Dictionary<int, int>();
        var counts = new Dictionary<int, CategoryCount>();

        foreach (var category in categoryList)
        {
            var totalCount = CalculateTotalCount(category.Id, directCounts, childrenByParent, totalCache, new HashSet<int>());
            counts[category.Id] = new CategoryCount(
                directCounts.GetValueOrDefault(category.Id),
                totalCount);
        }

        foreach (var directCount in directCounts)
        {
            if (!counts.ContainsKey(directCount.Key))
            {
                counts[directCount.Key] = new CategoryCount(directCount.Value, directCount.Value);
            }
        }

        return counts;
    }

    private static int CalculateTotalCount(
        int categoryId,
        IReadOnlyDictionary<int, int> directCounts,
        IReadOnlyDictionary<int, List<int>> childrenByParent,
        IDictionary<int, int> totalCache,
        ISet<int> recursionGuard)
    {
        if (totalCache.TryGetValue(categoryId, out var cachedCount))
        {
            return cachedCount;
        }

        if (!recursionGuard.Add(categoryId))
        {
            return directCounts.GetValueOrDefault(categoryId);
        }

        var total = directCounts.GetValueOrDefault(categoryId);
        if (childrenByParent.TryGetValue(categoryId, out var children))
        {
            foreach (var childId in children)
            {
                total += CalculateTotalCount(childId, directCounts, childrenByParent, totalCache, recursionGuard);
            }
        }

        recursionGuard.Remove(categoryId);
        totalCache[categoryId] = total;
        return total;
    }
}
