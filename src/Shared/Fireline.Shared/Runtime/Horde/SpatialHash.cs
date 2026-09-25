using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fireline.Shared.Horde
{
    public class SpatialHash
    {
        private readonly float _invCellSize;
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        private readonly List<long> _usedKeys = new List<long>();
        private readonly Stack<List<int>> _listPool = new Stack<List<int>>();

        public SpatialHash(float cellSize)
        {
            if (!(cellSize > 0f))
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Cell size must be positive.");

            CellSize = cellSize;
            _invCellSize = 1f / cellSize;
        }

        public float CellSize { get; }

        public int Count { get; private set; }

        private static long Key(int x, int y) => ((long)x << 32) | (uint)y;

        private int CellCoord(float v) => (int)MathF.Floor(v * _invCellSize);

        public void Clear()
        {
            for (int i = 0; i < _usedKeys.Count; i++)
            {
                List<int> list = _cells[_usedKeys[i]];
                list.Clear();
                _listPool.Push(list);
            }
            _cells.Clear();
            _usedKeys.Clear();
            Count = 0;
        }

        public void Insert(int id, Vector2 position)
        {
            long key = Key(CellCoord(position.X), CellCoord(position.Y));

            if (!_cells.TryGetValue(key, out List<int> list))
            {
                list = _listPool.Count > 0 ? _listPool.Pop() : new List<int>(8);
                _cells.Add(key, list);
                _usedKeys.Add(key);
            }

            list.Add(id);
            Count++;
        }

        public void QueryNeighbors(Vector2 position, List<int> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            results.Clear();
            int cx = CellCoord(position.X);
            int cy = CellCoord(position.Y);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (_cells.TryGetValue(Key(cx + dx, cy + dy), out List<int> list))
                        results.AddRange(list);
                }
            }
        }
    }
}
