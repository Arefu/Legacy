using DrGero.Types;

namespace DrGero.UI
{
    public sealed class MapTreeNode
    {
        public string Text { get; init; } = string.Empty;
        public MapEntry? Entry { get; init; } // non-null only for actual map leaves
        public List<MapTreeNode> Children { get; } = [];
    }

    public static class MapTreeBuilder
    {
        public static List<MapTreeNode> Build(IEnumerable<MapEntry> entries)
        {
            var zones = new List<MapTreeNode>();

            foreach (var zoneGroup in entries.GroupBy(e => e.Zone).OrderBy(g => g.Key))
            {
                var zoneNode = new MapTreeNode { Text = $"Zone {zoneGroup.Key}" };

                foreach (var areaGroup in zoneGroup.GroupBy(e => e.Area).OrderBy(g => g.Key))
                {
                    var variations = areaGroup.OrderBy(e => e.Variation).ToList();

                    if (variations.Count == 1)
                    {
                        // Single variation: area node is itself the leaf
                        var entry = variations[0];
                        zoneNode.Children.Add(new MapTreeNode
                        {
                            Text = AreaLabel(areaGroup.Key, entry),
                            Entry = entry
                        });
                    }
                    else
                    {
                        // Multiple variations: area is a group, variations are leaves
                        var areaNode = new MapTreeNode { Text = $"Area {areaGroup.Key}" };

                        foreach (var entry in variations)
                        {
                            areaNode.Children.Add(new MapTreeNode
                            {
                                Text = VariationLabel(entry),
                                Entry = entry
                            });
                        }

                        zoneNode.Children.Add(areaNode);
                    }
                }

                zones.Add(zoneNode);
            }

            return zones;
        }

        private static string AreaLabel(byte area, MapEntry entry) =>
            HasRealName(entry.Name) ? $"Area {area} ({entry.Name})" : $"Area {area}";

        private static string VariationLabel(MapEntry entry) =>
            HasRealName(entry.Name)
                ? $"Variation {entry.Variation} ({entry.Name})"
                : $"Variation {entry.Variation}";

        private static bool HasRealName(string? name) =>
            !string.IsNullOrWhiteSpace(name) && name != "***";
    }
}