namespace GitExtensions.Xplat.Core.CommitHistory;

/// <summary>
///  Where a commit sits in the revision graph: its lane, the lanes open above it, the lanes its parents continue in, and how
///  many lanes the row needs. Each lane keeps one color index for its whole life, so a line does not change color when other
///  lanes open or close; the color lists run parallel to the column lists.
/// </summary>
public sealed record GraphRow(
    int Column,
    IReadOnlyList<int> ActiveColumns,
    IReadOnlyList<int> ParentColumns,
    int Width,
    int Color = 0,
    IReadOnlyList<int>? ActiveColors = null,
    IReadOnlyList<int>? ParentColors = null)
{
    public int ActiveColorAt(int index) =>
        ActiveColors is { } colors && index < colors.Count ? colors[index] : ActiveColumns[index];

    public int ParentColorAt(int index) => ParentColors is { } colors && index < colors.Count ? colors[index] : Column;
}

/// <summary>
///  Assigns lanes to commits listed newest first, the order git log uses. Holds no UI types, so a view can draw the lanes
///  without knowing how they were chosen.
/// </summary>
public static class GraphLayout
{
    public static IReadOnlyList<GraphRow> Compute(IReadOnlyList<CommitRow> rows)
    {
        Lanes lanes = new();
        List<GraphRow> graph = new(rows.Count);

        foreach (CommitRow row in rows)
        {
            List<int> active = [];
            List<int> activeColors = [];
            for (int lane = 0; lane < lanes.Count; lane++)
            {
                if (lanes.HashAt(lane) is not null)
                {
                    active.Add(lane);
                    activeColors.Add(lanes.ColorAt(lane));
                }
            }

            int column = lanes.IndexOf(row.Hash);
            if (column < 0)
            {
                column = lanes.Place(row.Hash);
            }

            int color = lanes.ColorAt(column);
            List<int> parentColumns = [];
            List<int> parentColors = [];
            IReadOnlyList<string> parents = row.ParentHashes ?? [];
            if (parents.Count == 0)
            {
                lanes.Close(column);
            }
            else
            {
                int firstParentLane = lanes.IndexOf(parents[0]);
                if (firstParentLane >= 0 && firstParentLane != column)
                {
                    // The branch ends here by joining the lane its parent already has.
                    lanes.Close(column);
                    parentColumns.Add(firstParentLane);
                }
                else
                {
                    lanes.Continue(column, parents[0]);
                    parentColumns.Add(column);
                }

                // The line to the first parent keeps this branch's color, also where it joins another lane.
                parentColors.Add(color);

                foreach (string parent in parents.Skip(1))
                {
                    int existing = lanes.IndexOf(parent);
                    int parentLane = existing >= 0 ? existing : lanes.Place(parent);
                    parentColumns.Add(parentLane);
                    parentColors.Add(lanes.ColorAt(parentLane));
                }
            }

            graph.Add(new GraphRow(column, active, parentColumns, lanes.Count, color, activeColors, parentColors));
            lanes.TrimEnd();
        }

        return graph;
    }

    /// <summary>
    ///  The open lanes: which commit each one waits for, and its color.
    /// </summary>
    private sealed class Lanes
    {
        private readonly List<string?> _hashes = [];
        private readonly List<int> _colors = [];
        private readonly Dictionary<string, int> _laneOf = new(StringComparer.Ordinal);
        private int _nextColor;

        public int Count => _hashes.Count;

        public string? HashAt(int lane) => _hashes[lane];

        public int ColorAt(int lane) => _colors[lane];

        public int IndexOf(string hash) => _laneOf.TryGetValue(hash, out int lane) ? lane : -1;

        /// <summary>
        ///  Opens a lane for <paramref name="hash"/> with a new color, reusing the leftmost free lane.
        /// </summary>
        public int Place(string hash)
        {
            int free = _hashes.IndexOf(null);
            if (free < 0)
            {
                _hashes.Add(null);
                _colors.Add(0);
                free = _hashes.Count - 1;
            }

            _hashes[free] = hash;
            _colors[free] = _nextColor++;
            _laneOf[hash] = free;
            return free;
        }

        /// <summary>
        ///  The lane now waits for <paramref name="hash"/>, keeping its color.
        /// </summary>
        public void Continue(int lane, string hash)
        {
            Forget(lane);
            _hashes[lane] = hash;
            _laneOf[hash] = lane;
        }

        public void Close(int lane)
        {
            Forget(lane);
            _hashes[lane] = null;
        }

        public void TrimEnd()
        {
            while (_hashes.Count > 0 && _hashes[^1] is null)
            {
                _hashes.RemoveAt(_hashes.Count - 1);
                _colors.RemoveAt(_colors.Count - 1);
            }
        }

        private void Forget(int lane)
        {
            if (_hashes[lane] is { } hash)
            {
                _laneOf.Remove(hash);
            }
        }
    }
}
