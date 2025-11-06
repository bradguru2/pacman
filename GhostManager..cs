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
        private bool _paused = false; // Pauses ghost animations
        private float _level = 1.0f;


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
            // Don't update while paused
            if (_paused) return;

            foreach (var ghost in _ghosts)
            {
                if (ghost.IsDead)
                    continue;
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
                        ghost.PosUV -= ghost.Dir * Speed * _level * (float)dt;  // Undo this component of the proposed move
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
                            ghost.PosUV.X = _maze.GetTileCenterUV(row, col).X; // Y is inverted but does not matter here
                            ghost.Dir = new Vector2D<float>(0, 0.9f);
                            ghost.PosUV += ghost.Dir * Speed * _level * (float)dt;
                            ghost.Teleport = true;
                            _teleporting = true;
                        }
                        else
                            ghost.PosUV += ghost.Dir * Speed * _level * (float)dt;
                    }
                    else
                        ghost.PosUV += ghost.Dir * Speed * _level * (float)dt;

                }
            }
        }

        private Vector2D<float> PickNewDirection(Vector2D<float> current)
        {
            Vector2D<float>[] dirs = { new(0.9f, 0), new(-0.9f, 0), new(0, 0.9f), new(0, -0.9f) };
            return dirs[_rng.Next(dirs.Length)];
        }

        public IEnumerable<Ghost> Ghosts { get { return _ghosts; } }

        public void Pause() => _paused = true;

        public void Resume() => _paused = false;

        public bool TryCatchPacMan(Vector2D<float> pacManPosition)
        {
            foreach (var ghost in _ghosts)
            {
                float dx = pacManPosition.X - ghost.PosUV.X;
                float dy = pacManPosition.Y - ghost.PosUV.Y;
                float distSq = dx * dx + dy * dy;
                float hitRadius = 0.018f; // tweak as needed

                if (distSq < hitRadius * hitRadius)
                {
                    return !ghost.Frightened;
                }
            }
            return false;
        }

        public bool TryAvoidPacMan(Vector2D<float> pacManPosition)
        {
            foreach (var ghost in _ghosts)
            {
                float dx = pacManPosition.X - ghost.PosUV.X;
                float dy = pacManPosition.Y - ghost.PosUV.Y;
                float distSq = dx * dx + dy * dy;
                float hitRadius = 0.018f; // tweak as needed

                if (distSq < hitRadius * hitRadius)
                {
                    if (ghost.Frightened == true)
                    {
                        ghost.Dir = new Vector2D<float>(0, 0.9f);
                        ghost.PosUV = ghost.StartingPosUV;
                        ghost.InBox = true;
                        ghost.IsDead = true;
                        Console.WriteLine("[GHOST] Ghost eaten and returning to box");
                    }
                    
                    return !ghost.Frightened;
                }
            }
            return true;
        }

        public void SetLevel(float level)
        {
            _level += level * 0.125f; // scale level effect
        }

        public void SetFrightened(bool value)
        {
            foreach (var ghost in _ghosts)
            {
                ghost.Frightened = value;
                if (value == false)
                    ghost.IsDead = false;
            }

            Console.WriteLine($"[GHOST] All ghosts frightened = {value}");
        }
    }

    public class Ghost(Vector2D<float> pos, Vector2D<float> dir, Vector3D<float> color)
    {
        public Vector2D<float> PosUV = pos;
        public Vector2D<float> Dir = dir;
        public Vector3D<float> Color = color;
        public readonly Vector2D<float> StartingPosUV = pos;
        public bool InBox = true;
        public bool Teleport = false;
        public bool Frightened = false;
        public bool IsDead = false;
    }

}