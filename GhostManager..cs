using System.Numerics;
using Silk.NET.Maths;

namespace PacMan
{
    public class GhostManager
    {
        private readonly Maze _maze;
        private readonly List<Ghost> _ghosts = new();
        private readonly Random _rng = new();
        private const int GhostDoorRow = 12;
        private const int GhostDoorCol = 13;  // roughly center

        private const float Speed = 0.085f;
        private bool _teleporting; // Mutex like
        private bool _paused = false; // Pauses ghost animations
        private float _level = 1.0f;
        private double _modeTimer = 0;
        private const double ModeDuration = 7.0; // seconds per phase
        private bool _isChasePhase = false;
        private const float r = 0.015f; // constant ghost radius in UV tile units
        private readonly Dictionary<int, Vector2D<float>> _pacDirections = new()
        {
            { 0, new Vector2D<float>(0, 1) },  // Up
            { 1, new Vector2D<float>(-1, 0) }, // Left
            { 2, new Vector2D<float>(0, -1) }, // Down
            { 3, new Vector2D<float>(1, 0) },  // Right
        };

        public GhostManager(Maze maze)
        {
            _maze = maze;

            // spawn ghosts in box
            _ghosts.Add(new Ghost(_maze.GetTileCenterUV(16, 13), new(1, 0), new(1, 0, 0), "Blinky"));
            _ghosts.Add(new Ghost(_maze.GetTileCenterUV(16, 14), new(-1, 0), new(1, 0.753f, 0.769f), "Pinky"));
            _ghosts.Add(new Ghost(_maze.GetTileCenterUV(16, 12), new(1, 0), new(0, 1f, 1f), "Inky"));
            _ghosts.Add(new Ghost(_maze.GetTileCenterUV(16, 15), new(-1, 0), new(1, 0.647f, 0), "Clyde"));

        }

        public void Update(double dt, Vector2D<float> pacPos, int rotationIndex)
        {
            // Don't update while paused
            if (_paused) return;

            _modeTimer += dt;
            if (_modeTimer > ModeDuration)
            {
                _isChasePhase = !_isChasePhase;
                _modeTimer = 0;
            }

            foreach (var ghost in _ghosts)
            {
                if (ghost.Mode == GhostMode.Dead)
                    continue;

                if (ghost.Mode != GhostMode.Frightened)
                    ghost.Mode = _isChasePhase ? GhostMode.Chase : GhostMode.Scatter;

                var blocked = false;

                // Determine target tile based on mode and personality before checking collisions                
                Vector2D<float> target = GetTargetTile(ghost, pacPos, rotationIndex);

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
                        blocked = true;
                        break; // We can't move diagonally so either  X or Y
                    }
                }

                if ((!ghost.Teleport && !ghost.InBox) || (ghost.InBox && blocked && !_teleporting))
                {
                    ghost.PreviousDir = ghost.Dir; // Save previous direction
                    ghost.Dir = PickNewDirection((float)dt, ghost, target, blocked);  // We are going to attempt a move
                    ghost.PosUV += ghost.Dir * Speed * _level * (float)dt;

                    var (newRow, newCol) = _maze.GetCoordinates(ghost.PosUV);
                    newRow = _maze.Rows - 1 - newRow; // invert Y for use in GetTileCenterUV

                    if (ghost.Dir != ghost.PreviousDir)
                    {
                        ghost.PosUV = _maze.GetTileCenterUV(newRow, newCol); // Snap to center of tile
                    }
                    
                    ghost.PosUV = _maze.WrapPositionUV(ghost.PosUV); // Handle tunnel wrapping
                }
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

        private Vector2D<float> PickNewDirection(float dt, Ghost ghost, Vector2D<float> target, bool blocked)
        {
            float bestDist = float.MaxValue;
            Vector2D<float> bestDir = ghost.Dir;

            // Compute tile position of ghost
            var (row, col) = _maze.GetCoordinates(ghost.PosUV);

            // Special case: if frightened and not blocked, keep going same direction
            if (ghost.Mode == GhostMode.Frightened && !blocked)
                return ghost.Dir; // keep going
            else if (row == 14 && (col == 0 || col == _maze.Columns - 1))
                return -ghost.Dir; // reverse direction if blocked or at tunnel               

            // Only make a new decision if tile changed or blocked
            if (ghost.PreviousTile == (row, col) && !blocked)
            {
                return ghost.Dir; // No tile change, keep current direction
            }
            else
            {
                ghost.PreviousTile = (row, col); // Cache current tile maze position
            }

            foreach (var dir in _pacDirections.Values)
            {
                // Skip immediate reverse direction
                if (dir == -ghost.Dir)
                    continue;

                var nextUV = new Vector2D<float>(
                    ghost.PosUV.X + dir.X * _maze.TileW,
                    ghost.PosUV.Y + dir.Y * _maze.TileH
                );

                var (nextRow, nextCol) = _maze.GetCoordinates(nextUV);
                if (!_maze.IsWalkable(nextRow, nextCol))
                    continue;
                
                float dist = Vector2D.Distance(nextUV, target); // Euclidean distance to target
                
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestDir = dir;
                }
                // If equal distance, bias order ensures first priority wins
            }
            return bestDir;
        }

        private Vector2D<float> GetTargetTile(Ghost ghost, Vector2D<float> pacPos, int rotationIndex)
        {
            // Convert a few corner tiles to UV space for scatter mode.
            Vector2D<float> topLeft = _maze.GetTileCenterUV(_maze.Rows - 1, 0);
            Vector2D<float> topRight = _maze.GetTileCenterUV(_maze.Rows - 1, _maze.Columns - 1);
            Vector2D<float> bottomLeft = _maze.GetTileCenterUV(0, 0);
            Vector2D<float> bottomRight = _maze.GetTileCenterUV(0, _maze.Columns - 1);

            // Default target = Pac-Man’s position
            Vector2D<float> target = pacPos;

            switch (ghost.Mode)
            {
                case GhostMode.Scatter:
                    // Each ghost has a home corner.
                    if (ghost.Name == "Blinky")         // Red (Blinky)
                        target = topRight;
                    else if (ghost.Name == "Pinky")     // Pink (Pinky)
                        target = topLeft;
                    else if (ghost.Name == "Inky")      // Cyan (Inky)
                        target = bottomRight;
                    else if (ghost.Name == "Clyde")     // Orange (Clyde)
                        target = bottomLeft;
                    break;

                case GhostMode.Chase:
                    // Personality-based targeting
                    if (ghost.Name == "Blinky")         // Red (Blinky)
                    {
                        // 🔴 Blinky — targets Pac-Man directly
                        target = pacPos;
                    }
                    else if (ghost.Name == "Pinky")     // Pink (Pinky)
                    {
                        // 💗 Pinky — aims 4 tiles ahead of Pac-Man
                        target = pacPos + _pacDirections[rotationIndex] * (_maze.TileW * 4);
                    }
                    else if (ghost.Name == "Inky")      // Cyan (Inky)
                    {
                        // 💙 Inky — aims 2 tiles ahead of Pac-Man,
                        // mirrored around Blinky’s position
                        var blinky = _ghosts.FirstOrDefault(g => g.Name == "Blinky");
                        if (blinky != null)
                        {
                            Vector2D<float> ahead = pacPos + _pacDirections[rotationIndex] * (_maze.TileW * 2);
                            Vector2D<float> diff = ahead - blinky.PosUV;
                            target = blinky.PosUV + diff * 2f;
                        }
                    }
                    else if (ghost.Name == "Clyde")     // Orange (Clyde)
                    {
                        // 🟠 Clyde — chases Pac-Man unless close, then retreats
                        float dist = Vector2D.Distance(ghost.PosUV, pacPos);
                        if (dist > 0.25f)
                            target = pacPos;
                        else
                            target = bottomLeft;
                    }
                    break;

                case GhostMode.Frightened:
                    // Scatter randomly
                    target = new Vector2D<float>(
                        (float)_rng.NextDouble(),
                        (float)_rng.NextDouble()
                    );
                    break;

                case GhostMode.Dead:
                    // Return to ghost box center to respawn
                    target = _maze.GetTileCenterUV(16, 13);
                    break;
            }

            return target;
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
                    if (ghost.Mode != GhostMode.Frightened)
                    {                        
                        return true;
                    }
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
                    if (ghost.Mode == GhostMode.Frightened)
                    {
                        ghost.Dir = new Vector2D<float>(0, 0.9f);
                        ghost.PosUV = ghost.StartingPosUV;
                        ghost.InBox = true;
                        ghost.Mode = GhostMode.Dead;
                        Console.WriteLine("[GHOST] Ghost eaten and returning to box");
                    }

                    return ghost.Mode != GhostMode.Frightened;
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
                ghost.Mode = value ? GhostMode.Frightened :  GhostMode.Scatter;
            }

            Console.WriteLine($"[GHOST] All ghosts frightened = {value}");
        }
    }   

}