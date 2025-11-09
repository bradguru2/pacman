// PacManController.cs
//
// Handles smooth arrow-key movement, 4-way facing, clamping inside window,
// and exposes current normalized position + rotation to the renderer.
//
// Controls: Arrow keys (Left, Right, Up, Down).
// Motion style: Smooth (hold to move).
// Facing: 4-way (Right, Up, Left, Down).
//
// NOTE: This uses Silk.NET input (InputContext/IKeyboard). If your Silk.NET
// version exposes slightly different event delegate signatures, adjust the
// KeyDown/KeyUp handlers accordingly (comments inline).

using System;
using System.Configuration.Assemblies;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace PacMan
{
    public sealed class PacManController : IDisposable
    {
        // public normalized position in UV coordinates (0..1)
        // start centered
        public Vector2D<float> Position { get; private set; } = new Vector2D<float>(0.5f, 0.5f);

        // rotation index: 0=Right, 1=Up, 2=Left, 3=Down
        public int RotationIndex { get; private set; } = 0;

        // normalized movement speed in UV units per second (tweakable)
        // Note: This value feels reasonable on a 800x600 window at "normal"
        public float Speed { get; set; } = 0.45f; // UV units per second (approx)

        public float EntityRadius { get; set; } = 0.05f; // default radius in UV units

        public event Action? OnSuperPelletEaten;
        public event Action? OnPacManCaught;
        public event Action? OnPacManRespawn;

        private readonly IWindow _window;
        private readonly IInputContext _input;
        private IKeyboard? _keyboard;

        // current input state
        private bool _leftDown, _rightDown, _upDown, _downDown;
        
        private bool _disposed = false;

        private Maze? _maze;

        private bool _paused = false;

        private readonly Vector2D<float> _respawnPosition;

        private int _level = 1;

        private bool _isSuperMode = false;
        private double _superStartTime = 0;
        private const double SuperDuration = 8.0; // seconds of super mode


        public PacManController(IWindow window, Vector2D<float> startPosition)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            Position = startPosition;
            _respawnPosition = startPosition;

            // Create input context from the window (Silk.NET.Windowing extension)
            // This is the common pattern in Silk.NET 2.x
            _input = _window.CreateInput();

            // Attempt to grab the first keyboard if present
            if (_input.Keyboards.Count > 0)
            {
                _keyboard = _input.Keyboards[0];

                // Subscribe to keyboard events.
                // Delegate signature in Silk.NET usually is: (IKeyboard k, Key key, int arg)
                // But some versions use different shapes; if compile fails, inspect the delegate type.
                _keyboard.KeyDown += OnKeyDown;
                _keyboard.KeyUp += OnKeyUp;
            }
            else
            {
                // No physical keyboard found — nothing to subscribe to.
                _keyboard = null;
            }
        }
        
        public void NextLevel()
        {
            _level++;
            Speed = MathF.Min(Speed + 0.025f, 0.75f); // increase speed slightly each level
        }

        /// <summary>
        /// Call from Program.Update(dt) to advance movement.
        /// dt is seconds since last update.
        /// </summary>
        public void Update(double dt)
        {
            if (_disposed || _paused) return;

            // build a direction vector from the current key state
            float dx = 0f, dy = 0f;
            if (_leftDown) dx -= 1f;
            if (_rightDown) dx += 1f;
            if (_upDown) dy += 1f;
            if (_downDown) dy -= 1f;

            // Normalize diagonal movement so speed stays consistent.
            if (dx != 0f && dy != 0f)
            {
                var inv = 1f / MathF.Sqrt(dx * dx + dy * dy);
                dx *= inv;
                dy *= inv;
            }            

            // Check for maze collisions if maze is set
            if (_maze != null) {    
                // Apply movement in normalized UV coords (0..1)
                float moveX = dx * Speed * (float)dt;
                float moveY = dy * Speed * (float)dt;
                var newPos = new Vector2D<float>(Position.X + moveX, Position.Y + moveY);
                newPos = _maze.WrapPositionUV(newPos); // Handle tunnel wrapping
                if (!_maze.HasCollision(newPos, EntityRadius))
                {
                    // No collision, proceed with movement
                    Position = newPos;
                }                
            }

            
            // Update facing direction: 4-way priority:
            // If horizontal input exists, prefer horizontal. Otherwise vertical.
            if (dx < 0f) RotationIndex = 2;        // Left
            else if (dx > 0f) RotationIndex = 0;   // Right
            else if (dy > 0f) RotationIndex = 1;   // Up
            else if (dy < 0f) RotationIndex = 3;   // Down

            // When no input, RotationIndex remains last direction (classic Pac-Man)            
        }

        public void SetMaze(Maze maze)
        {
            _maze = maze;
        }

        public void RaiseSuperPelletEaten()
        {
            Console.WriteLine("[POWER] Pac-Man entered Super Mode!");
            _isSuperMode = true;
            _superStartTime = _window.Time;
            OnSuperPelletEaten?.Invoke();
        }

        public void RaisePacManCaught()
        {
            OnPacManCaught?.Invoke();
        }

        public void RaisePacmanRespawn()
        {
            Position = _respawnPosition;
            RotationIndex = 0;
            OnPacManRespawn?.Invoke();
        }

        // Key down handler – sets flag
        private void OnKeyDown(IKeyboard keyboard, Key key, int arg)
        {
            // Arrow keys
            switch (key)
            {
                case Key.Left: _leftDown = true; break;
                case Key.Right: _rightDown = true; break;
                case Key.Up: _upDown = true; break;
                case Key.Down: _downDown = true; break;
            }
        }

        // Key up handler – clears flag
        private void OnKeyUp(IKeyboard keyboard, Key key, int arg)
        {
            switch (key)
            {
                case Key.Left: _leftDown = false; break;
                case Key.Right: _rightDown = false; break;
                case Key.Up: _upDown = false; break;
                case Key.Down: _downDown = false; break;
            }
        }

        public bool TryEatPellet(Vector2D<float> posUV)
        {
            // Clamp UV to [0,1] range just in case
            float u = Math.Clamp(posUV.X, 0f, 1f);
            float v = Math.Clamp(posUV.Y, 0f, 1f);

            // Convert to maze tile coordinates
            int col = (int)(u * _maze!.Columns);
            int row = (int)((1.0f - v) * _maze.Rows); // Flip Y if maze rows count from top

            // Safety guard
            if (row < 0 || row >= _maze.Rows || col < 0 || col >= _maze.Columns)
                return false;

            // Check if pellet exists
            if (_maze.Pellets[row, col])
            {
                _maze.Pellets[row, col] = false; // consume pellet                
                return true;
            }

            return false;
        }

        public bool TryEatSuperPellet(Vector2D<float> posUV)
        {
            int col = (int)(posUV.X * _maze!.Columns);
            int row = (int)((1.0f - posUV.Y) * _maze.Rows);

            if (row < 0 || row >= _maze.Rows || col < 0 || col >= _maze.Columns)
                return false;

            if (_maze.SuperPellets[row, col])
            {
                _maze.SuperPellets[row, col] = false;
                return true;
            }
            return false;
        }

        public bool TryEndSuperMode()
        {
            double elapsed = _window.Time - _superStartTime;
            if (elapsed > SuperDuration)
            {
                _isSuperMode = false;
                Console.WriteLine("[POWER] Super Mode ended");
                return true;
            }
            return false;
        }

        public void Pause() => _paused = true;

        public void Resume() => _paused = false;

        public bool IsSuperMode => _isSuperMode;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_keyboard != null)
                {
                    _keyboard.KeyDown -= OnKeyDown;
                    _keyboard.KeyUp -= OnKeyUp;
                }
                _input.Dispose();
            }
            catch
            {
                // swallow any dispose errors
            }
        }
    }
}
