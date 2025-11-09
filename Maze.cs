// Maze.cs
// Parses ASCII map (MazeData.Map) into wall rectangles (normalized UV coordinates).
// Provides simple collision queries used by PacManController.

using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.Maths;

namespace PacMan
{
    public sealed class Maze
    {
        // Each wall is an axis-aligned rectangle in UV space (0..1).
        public readonly List<(float x, float y, float w, float h)> Walls = new();

        public int Columns { get; private set; }
        public int Rows { get; private set; }

        // tile normalized sizes
        public float TileW { get; }
        public float TileH { get; }

        public bool[,] Pellets { get; private set; }
        public bool[,] SuperPellets { get; private set; }

        public Maze()
        {
            var map = MazeData.Layout ?? throw new InvalidOperationException("MazeData.Map is null.");
            Rows = map.Length;
            Columns = map.Length > 0 ? map[0].Length : 0;
            if (Columns == 0) throw new InvalidOperationException("MazeData empty.");

            TileW = 1f / Columns;
            TileH = 1f / Rows;

            ParseMap(map);
            Pellets = new bool[Rows, Columns];
            SuperPellets = new bool[Rows, Columns];
        }

        public void InitializePellets()
        {
            var layout = MazeData.Layout;
            Rows = layout.Length;
            Columns = layout[0].Length;           

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    char ch = layout[r][c];
                    if (ch == '.' && r < Rows - 2)
                        Pellets[r, c] = true;
                    else if (ch == 'o' && r < Rows - 2)
                        SuperPellets[r, c] = true;
                }
            }
        }

        private void ParseMap(string[] map)
        {
            Walls.Clear();

            for (int row = 0; row < Rows; row++)
            {
                string line = map[row];
                if (line.Length != Columns) throw new InvalidOperationException($"Maze row {row} length mismatch.");

                for (int col = 0; col < Columns; col++)
                {
                    char c = line[col];
                    if (c == '#')
                    {
                        // Convert row/col to normalized bottom-left corner in UV coordinates.
                        // map[0] is top row -> invert y when producing UV bottom-left.
                        float x = col * TileW;
                        float y = 1f - (row + 1) * TileH; // bottom-left
                        Walls.Add((x, y, TileW, TileH));
                    }
                }
            }
        }

        public bool IsWalkable(int row, int col)
        {
            var layout = MazeData.Layout;

            if (row < 0 || row >= layout.Length)
                return false;
            if (col < 0 || col >= layout[row].Length)
                return false;

            char ch = layout[row][col];
            // Only open paths (spaces, dots, or power pellets)
            return ch == ' ' || ch == '.' || ch == 'o';
        }

        public (int row, int col) GetCoordinates(Vector2D<float> uv)
        {
            float tX = uv.X * Columns;
            int col = (int)(tX);
            //col = Math.Clamp(col, 0, Columns - 1);

            float tY = (1.0f - uv.Y) * Rows;   // keep your Y convention
            int row = (int)(tY);
            //row = Math.Clamp(row, 0, Rows - 1);
            //col = Math.Clamp(col, 0, Columns - 1);

            return (row, col);
        }

        public Vector2D<float> MapToTileCenterUV(Vector2D<float> incoming)
        {
            var (row, col) = GetCoordinates(incoming);
            return GetTileCenterUV(Rows - row - 1, col); // Y is inverted on GetTileCenterUV in UV space
        }

        public Vector2D<float> GetTileCenterUV(int row, int col)
        {
            // Convert maze grid coords (row, col) to normalized UV center.
            float u = (col + 0.5f) / Columns;
            float v = (row + 0.5f) / Rows;
            return new Vector2D<float>(u, v);
        }


        /// <summary>
        /// Returns true if the given circle (position in UV 0..1, radius in UV units) intersects any wall.
        /// </summary>
        public bool HasCollision(Vector2D<float> posUV, float radius)
        {
            // Quick broad-phase: check all walls (map is small). If performance matters,
            // build a spatial grid or bounding-volume hierarchy.
            foreach (var w in Walls)
            {
                if (Collision.CircleIntersectsRect(posUV, radius, w.x, w.y, w.w, w.h))
                    return true;
            }

            int col = (int)(posUV.X / TileW);
            int row = (int)((1 - posUV.Y) / TileH); // Flip Y if maze rows count from top
            return !IsWalkable(row, col);
        }

        public Vector2D<float> WrapPositionUV(Vector2D<float> uv, float displacement = 0.0f)
        {
            // Horizontal wrap (left/right tunnel)
            if (uv.X < (0.00f + displacement))
                uv.X = 1.0f - displacement;
            else if (uv.X > (1.0f - displacement))
                uv.X = 0.0f + displacement;

            // Vertical wrap optional (usually not needed)
            return uv;
        }

    }
}
