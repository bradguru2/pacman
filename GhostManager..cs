using System.Collections.Generic;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;

namespace PacMan
{
    public class GhostManager
    {
        private readonly Maze _maze;
        private readonly List<Ghost> _ghosts = new();
        private readonly Random _rng = new();
        private const int GhostDoorRow = 12;  
        private const int GhostDoorCol = 13;  // roughly center

        private const float Speed = 0.10f;
        private bool _teleporting; // Mutex like

        public GhostManager(Maze maze)
        {
            _maze = maze;

            // spawn ghosts in box
            _ghosts.Add(new Ghost(_maze.GetTileCenterUV(16, 13), new(1, 0), new(1, 0, 0)));
            _ghosts.Add(new Ghost(_maze.GetTileCenterUV(16, 14), new(-1, 0), new(0, 1, 0)));
            _ghosts.Add(new Ghost(_maze.GetTileCenterUV(16, 12), new(1, 0), new(1, 0.5f, 0)));
            _ghosts.Add(new Ghost(_maze.GetTileCenterUV(16, 15), new(-1, 0), new(1, 0, 1)));
        }

        public void Update(double dt)
        {
            foreach (var ghost in _ghosts)
            {
                float r = 0.015f; // constant ghost radius in UV tile units
                bool blocked = false;
                
                Vector2D<float>[] probes =
                {
                    new(ghost.PosUV.X + r, ghost.PosUV.Y),  // right probe
                    new(ghost.PosUV.X - r, ghost.PosUV.Y),  // left probe
                    new(ghost.PosUV.X, ghost.PosUV.Y + r),  // up probe
                    new(ghost.PosUV.X, ghost.PosUV.Y - r)   // down probe
                };

                // Check surrounding positions for collisions
                foreach (var p in probes)
                {
                    var (row, col) = _maze.GetCoordinates(p);
                    if (!_maze.IsWalkable(row, col) && !ghost.Teleport)
                    {
                        ghost.PosUV -= ghost.Dir * Speed * (float)dt;  // Undo this component of the proposed move
                        ghost.PosUV = _maze.MapToTileCenterUV(ghost.PosUV); // Snap to center of tile
                        blocked = true;
                        break; // We can't move diagonally so either  X or Y
                    }
                }

                if (blocked && !ghost.Teleport)
                    ghost.Dir = PickNewDirection(ghost.PosUV);  // We are going to attempt a move                   )
                else
                {
                    if (ghost.InBox)
                    {
                        var (row, col) = _maze.GetCoordinates(ghost.PosUV);
                        
                        if (row <= GhostDoorRow - 1)
                        {
                            ghost.PosUV = _maze.GetTileCenterUV(19, col);
                            ghost.Teleport = false;
                            ghost.InBox = false;
                            _teleporting = false;
                        }
                        else if (_teleporting == false && (col == GhostDoorCol || col == GhostDoorCol + 1))
                        {
                            ghost.PosUV.X = _maze.GetTileCenterUV(row, col).X;
                            ghost.Dir = new Vector2D<float>(0, 0.9f);
                            ghost.PosUV += ghost.Dir * Speed * (float)dt;
                            ghost.Teleport = true;
                            _teleporting = true;
                        }
                        else
                            ghost.PosUV += ghost.Dir * Speed * (float)dt; // Make new move

                    }
                    else
                        ghost.PosUV += ghost.Dir * Speed * (float)dt; // Make new move
                }
            }
        }

        private Vector2D<float> PickNewDirection(Vector2D<float> current)
        {
            Vector2D<float>[] dirs = { new(0.9f, 0), new(-0.9f, 0), new(0, 0.9f), new(0, -0.9f) };
            return dirs[_rng.Next(dirs.Length)];
        }

        public IEnumerable<Ghost> GetGhosts() => _ghosts;
    }

    public class Ghost
    {
        public Vector2D<float> PosUV;
        public Vector2D<float> Dir;
        public Vector3D<float> Color;
        internal bool InBox = true;
        internal bool Teleport = false;

        public Ghost(Vector2D<float> pos, Vector2D<float> dir, Vector3D<float> color)
        {
            PosUV = pos;
            Dir = dir;
            Color = color;
        }
    }

}