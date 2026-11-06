using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fireline.Shared.Horde
{
    /// <summary>What <see cref="FlowField.Sample"/> found for a position.</summary>
    public enum FlowSample
    {
        /// <summary>Outside the grid, no targets, or walled off from every target. Fall back to a straight seek.</summary>
        NoPath,
        /// <summary>Clear line of sight to the target: seek its live position directly (smoothest movement).</summary>
        Direct,
        /// <summary>Walls in the way: follow the returned direction around them.</summary>
        Follow
    }

    public sealed class FlowField
    {
        private const float Diagonal = 1.41421356f;
        private const float Epsilon = 0.000001f;

        // 8 neighbors: 4 straight, then 4 diagonal.
        private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        private readonly bool[] _blocked;
        private readonly float[] _cost;
        private readonly int[] _source;
        private readonly bool[] _sight;
        private readonly Vector2[] _direction;
        private readonly MinHeap _open;
        private Vector2[] _targets = new Vector2[4];
        private int _targetCount;

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }
        /// <summary>World position of the grid's min corner (bottom-left).</summary>
        public Vector2 Origin { get; }

        /// <summary>
        /// Line-of-sight checks only run for cells within this path distance (world units) of their
        /// target. Further out, enemies follow the field, which is just as good when far away and
        /// keeps rebuilds cheap.
        /// </summary>
        public float SightRange { get; set; } = 12f;

        /// <summary>Number of targets used in the last <see cref="Build"/>.</summary>
        public int TargetCount => _targetCount;

        public FlowField(int width, int height, float cellSize, Vector2 origin)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));

            Width = width;
            Height = height;
            CellSize = cellSize;
            Origin = origin;

            int n = width * height;
            _blocked = new bool[n];
            _cost = new float[n];
            _source = new int[n];
            _sight = new bool[n];
            _direction = new Vector2[n];
            _open = new MinHeap(Math.Max(16, n / 4));
            Array.Fill(_cost, float.PositiveInfinity);
            Array.Fill(_source, -1);
        }

        // ---- Grid helpers -------------------------------------------------------------------

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        private int Index(int x, int y) => y * Width + x;

        /// <summary>Cell containing a world position. Returns false if it's outside the grid.</summary>
        public bool TryWorldToCell(Vector2 position, out int x, out int y)
        {
            x = (int)MathF.Floor((position.X - Origin.X) / CellSize);
            y = (int)MathF.Floor((position.Y - Origin.Y) / CellSize);
            return InBounds(x, y);
        }

        public Vector2 CellCenter(int x, int y) =>
            new Vector2(Origin.X + (x + 0.5f) * CellSize, Origin.Y + (y + 0.5f) * CellSize);

        public void SetBlocked(int x, int y, bool blocked)
        {
            if (!InBounds(x, y)) throw new ArgumentOutOfRangeException(nameof(x), $"Cell ({x},{y}) is outside the grid.");
            _blocked[Index(x, y)] = blocked;
        }

        public bool IsBlocked(int x, int y) => InBounds(x, y) && _blocked[Index(x, y)];

        /// <summary>True if the world position is in a blocked cell. Outside the grid counts as open.</summary>
        public bool IsBlockedAt(Vector2 position) =>
            TryWorldToCell(position, out int x, out int y) && _blocked[Index(x, y)];

        /// <summary>Path distance (in cells) from this cell to its nearest target, or infinity if unreachable.</summary>
        public float GetCost(int x, int y) => InBounds(x, y) ? _cost[Index(x, y)] : float.PositiveInfinity;

        /// <summary>Which target this cell leads to (index into the Build list), or -1.</summary>
        public int GetTarget(int x, int y) => InBounds(x, y) ? _source[Index(x, y)] : -1;

        public bool HasLineOfSight(int x, int y) => InBounds(x, y) && _sight[Index(x, y)];

        /// <summary>The stored (unsmoothed) direction for a cell. Zero for targets, walls and unreachable cells.</summary>
        public Vector2 GetCellDirection(int x, int y) => InBounds(x, y) ? _direction[Index(x, y)] : Vector2.Zero;

        /// <summary>
        /// Recomputes the whole field toward the given targets (e.g. living players).
        /// Call it after changing blocked cells, and every so often as targets move.
        /// </summary>
        public void Build(IReadOnlyList<Vector2> targets)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));

            Array.Fill(_cost, float.PositiveInfinity);
            Array.Fill(_source, -1);
            Array.Clear(_sight, 0, _sight.Length);
            Array.Clear(_direction, 0, _direction.Length);
            _open.Clear();

            if (_targets.Length < targets.Count) _targets = new Vector2[targets.Count];
            _targetCount = targets.Count;
            for (int t = 0; t < targets.Count; t++)
            {
                _targets[t] = targets[t];
                Seed(t, targets[t]);
            }

            // Multi-source Dijkstra: every cell ends up with the distance to its nearest reachable target.
            while (_open.TryPop(out int current, out float currentCost))
            {
                if (currentCost > _cost[current] + Epsilon) continue; // stale heap entry

                int cx = current % Width, cy = current / Width;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + Dx[k], ny = cy + Dy[k];
                    if (!CanStep(cx, cy, nx, ny, k)) continue;

                    int next = Index(nx, ny);
                    float nextCost = currentCost + (k < 4 ? 1f : Diagonal);
                    if (nextCost + Epsilon >= _cost[next]) continue;

                    _cost[next] = nextCost;
                    _source[next] = _source[current];
                    _open.Push(next, nextCost);
                }
            }

            // Each reachable cell points at its cheapest neighbor; nearby cells also get line of sight.
            float sightCells = SightRange / CellSize;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = Index(x, y);
                    if (_blocked[i] || float.IsPositiveInfinity(_cost[i])) continue;

                    _direction[i] = CheapestNeighborDirection(x, y);
                    if (_cost[i] <= sightCells)
                        _sight[i] = IsLineClear(CellCenter(x, y), _targets[_source[i]]);
                }
            }
        }

        // A target standing in a blocked cell (e.g. hugging a wall, since blocked cells are inflated)
        // still needs to be reachable, so seed the nearest open cells around it instead.
        private void Seed(int target, Vector2 position)
        {
            if (!TryWorldToCell(position, out int tx, out int ty)) return;

            if (!_blocked[Index(tx, ty)])
            {
                SeedCell(target, tx, ty, 0f);
                return;
            }

            for (int radius = 1; radius <= 2; radius++)
            {
                bool any = false;
                for (int y = ty - radius; y <= ty + radius; y++)
                for (int x = tx - radius; x <= tx + radius; x++)
                {
                    if (Math.Max(Math.Abs(x - tx), Math.Abs(y - ty)) != radius) continue; // ring only
                    if (!InBounds(x, y) || _blocked[Index(x, y)]) continue;
                    float dx = x - tx, dy = y - ty;
                    SeedCell(target, x, y, MathF.Sqrt(dx * dx + dy * dy));
                    any = true;
                }
                if (any) return;
            }
        }

        private void SeedCell(int target, int x, int y, float cost)
        {
            int i = Index(x, y);
            if (cost >= _cost[i]) return;
            _cost[i] = cost;
            _source[i] = target;
            _open.Push(i, cost);
        }

        // Diagonal moves can't cut a corner: both cells beside the diagonal must be open.
        private bool CanStep(int fromX, int fromY, int toX, int toY, int k)
        {
            if (!InBounds(toX, toY) || _blocked[Index(toX, toY)]) return false;
            if (k < 4) return true;
            return !IsBlocked(toX, fromY) && !IsBlocked(fromX, toY);
        }

        private Vector2 CheapestNeighborDirection(int x, int y)
        {
            int i = Index(x, y);
            float best = _cost[i];
            int bestK = -1;

            for (int k = 0; k < 8; k++)
            {
                int nx = x + Dx[k], ny = y + Dy[k];
                if (!CanStep(x, y, nx, ny, k)) continue;
                float c = _cost[Index(nx, ny)];
                if (c < best - Epsilon)
                {
                    best = c;
                    bestK = k;
                }
            }

            if (bestK < 0) return Vector2.Zero; // a target cell (or a local minimum)
            return Vector2.Normalize(new Vector2(Dx[bestK], Dy[bestK]));
        }

        /// <summary>
        /// True if the straight segment from <paramref name="from"/> to <paramref name="to"/> crosses no
        /// blocked cell. The cell containing <paramref name="to"/> is ignored, so a target hugging a wall
        /// can still be seen. Walks the grid cell by cell (Amanatides–Woo).
        /// </summary>
        public bool IsLineClear(Vector2 from, Vector2 to)
        {
            float gx = (from.X - Origin.X) / CellSize, gy = (from.Y - Origin.Y) / CellSize;
            float ex = (to.X - Origin.X) / CellSize, ey = (to.Y - Origin.Y) / CellSize;
            int x = (int)MathF.Floor(gx), y = (int)MathF.Floor(gy);
            int endX = (int)MathF.Floor(ex), endY = (int)MathF.Floor(ey);

            float dx = ex - gx, dy = ey - gy;
            int stepX = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
            int stepY = dy > 0 ? 1 : (dy < 0 ? -1 : 0);
            float tDeltaX = stepX != 0 ? MathF.Abs(1f / dx) : float.PositiveInfinity;
            float tDeltaY = stepY != 0 ? MathF.Abs(1f / dy) : float.PositiveInfinity;
            float tMaxX = stepX > 0 ? (x + 1 - gx) * tDeltaX : (stepX < 0 ? (gx - x) * tDeltaX : float.PositiveInfinity);
            float tMaxY = stepY > 0 ? (y + 1 - gy) * tDeltaY : (stepY < 0 ? (gy - y) * tDeltaY : float.PositiveInfinity);

            int guard = Math.Abs(endX - x) + Math.Abs(endY - y) + 2;
            while (guard-- > 0)
            {
                if (x == endX && y == endY) return true;
                if (IsBlocked(x, y)) return false;

                // Passing exactly through a corner touches both side cells; treat it like a diagonal step.
                if (MathF.Abs(tMaxX - tMaxY) < Epsilon)
                {
                    if (IsBlocked(x + stepX, y) || IsBlocked(x, y + stepY)) return false;
                    x += stepX; y += stepY;
                    tMaxX += tDeltaX; tMaxY += tDeltaY;
                }
                else if (tMaxX < tMaxY)
                {
                    x += stepX; tMaxX += tDeltaX;
                }
                else
                {
                    y += stepY; tMaxY += tDeltaY;
                }
            }
            return true;
        }

        public FlowSample Sample(Vector2 position, out Vector2 direction, out int target)
        {
            direction = Vector2.Zero;
            target = -1;
            if (_targetCount == 0 || !TryWorldToCell(position, out int cx, out int cy)) return FlowSample.NoPath;

            int i = Index(cx, cy);
            if (_blocked[i] || float.IsPositiveInfinity(_cost[i]))
                return EscapeTowardReachableNeighbor(position, cx, cy, out direction, out target);

            target = _source[i];
            if (_sight[i] || _cost[i] <= Epsilon) return FlowSample.Direct;

            // Blend the four nearest cell directions so movement doesn't snap to 45° steps.
            float fx = (position.X - Origin.X) / CellSize - 0.5f;
            float fy = (position.Y - Origin.Y) / CellSize - 0.5f;
            int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
            float tx = fx - x0, ty = fy - y0;

            Vector2 sum = Vector2.Zero;
            AddWeighted(ref sum, x0, y0, (1 - tx) * (1 - ty), position);
            AddWeighted(ref sum, x0 + 1, y0, tx * (1 - ty), position);
            AddWeighted(ref sum, x0, y0 + 1, (1 - tx) * ty, position);
            AddWeighted(ref sum, x0 + 1, y0 + 1, tx * ty, position);

            if (sum.LengthSquared() < Epsilon) sum = _direction[i];
            if (sum.LengthSquared() < Epsilon) return FlowSample.Direct;

            direction = Vector2.Normalize(sum);
            return FlowSample.Follow;
        }

        private void AddWeighted(ref Vector2 sum, int x, int y, float weight, Vector2 position)
        {
            if (weight <= 0f || !InBounds(x, y)) return;
            int i = Index(x, y);
            if (_blocked[i] || float.IsPositiveInfinity(_cost[i])) return;

            Vector2 d = _direction[i];
            if (_sight[i]) // this neighbor sees its target, so aim straight at it
            {
                Vector2 toTarget = _targets[_source[i]] - position;
                if (toTarget.LengthSquared() > Epsilon) d = Vector2.Normalize(toTarget);
            }
            sum += d * weight;
        }

        // Knocked into a wall cell, or stuck in a pocket: head for the cheapest reachable neighbor.
        private FlowSample EscapeTowardReachableNeighbor(Vector2 position, int cx, int cy, out Vector2 direction, out int target)
        {
            direction = Vector2.Zero;
            target = -1;
            float best = float.PositiveInfinity;
            int bx = 0, by = 0;

            for (int k = 0; k < 8; k++)
            {
                int nx = cx + Dx[k], ny = cy + Dy[k];
                if (!InBounds(nx, ny)) continue;
                int n = Index(nx, ny);
                if (_blocked[n] || _cost[n] >= best) continue;
                best = _cost[n];
                bx = nx; by = ny;
            }

            if (float.IsPositiveInfinity(best)) return FlowSample.NoPath;

            target = _source[Index(bx, by)];
            Vector2 toCell = CellCenter(bx, by) - position;
            if (toCell.LengthSquared() < Epsilon) return FlowSample.Direct;
            direction = Vector2.Normalize(toCell);
            return FlowSample.Follow;
        }

        public Vector2 Slide(Vector2 from, Vector2 to)
        {
            if (IsBlockedAt(from)) return to;

            Vector2 delta = to - from;
            float length = delta.Length();
            if (length < Epsilon) return to;

            int steps = Math.Min(16, Math.Max(1, (int)MathF.Ceiling(length / (CellSize * 0.5f))));
            Vector2 step = delta / steps;
            Vector2 p = from;

            for (int s = 0; s < steps; s++)
            {
                Vector2 next = p + step;
                if (!IsBlockedAt(next)) { p = next; continue; }

                var alongX = new Vector2(next.X, p.Y);
                if (!IsBlockedAt(alongX)) { p = alongX; continue; }

                var alongY = new Vector2(p.X, next.Y);
                if (!IsBlockedAt(alongY)) { p = alongY; continue; }

                break; // wedged in a corner
            }
            return p;
        }

        // ---- Binary min-heap (netstandard2.1 has no PriorityQueue) ---------------------------

        private sealed class MinHeap
        {
            private int[] _items;
            private float[] _keys;
            private int _count;

            public MinHeap(int capacity)
            {
                _items = new int[capacity];
                _keys = new float[capacity];
            }

            public void Clear() => _count = 0;

            public void Push(int item, float key)
            {
                if (_count == _items.Length)
                {
                    Array.Resize(ref _items, _count * 2);
                    Array.Resize(ref _keys, _count * 2);
                }

                int i = _count++;
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (_keys[parent] <= key) break;
                    _items[i] = _items[parent];
                    _keys[i] = _keys[parent];
                    i = parent;
                }
                _items[i] = item;
                _keys[i] = key;
            }

            public bool TryPop(out int item, out float key)
            {
                if (_count == 0)
                {
                    item = -1;
                    key = 0f;
                    return false;
                }

                item = _items[0];
                key = _keys[0];
                int lastItem = _items[--_count];
                float lastKey = _keys[_count];

                int i = 0;
                while (true)
                {
                    int child = i * 2 + 1;
                    if (child >= _count) break;
                    if (child + 1 < _count && _keys[child + 1] < _keys[child]) child++;
                    if (_keys[child] >= lastKey) break;
                    _items[i] = _items[child];
                    _keys[i] = _keys[child];
                    i = child;
                }
                if (_count > 0)
                {
                    _items[i] = lastItem;
                    _keys[i] = lastKey;
                }
                return true;
            }
        }
    }
}
