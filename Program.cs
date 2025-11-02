// Program.cs
using System;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Maths;

namespace PacMan
{
    class Program
    {
        private static IWindow? _window;
        private static PacManRenderer? _renderer;
        private static GameAudio? _audio;
        private static PacManController? _controller;
        private static MazeRenderer? _mazeRenderer;
        private static PelletRenderer? _pelletRenderer;
        private static List<GhostRenderer>? _ghosts;
        private static Maze _maze = new();
        private static HudRenderer? _hud;
        private static GhostManager? _ghostManager;




        static void Main(string[] args)
        {
            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(1024, 768);
            options.Title = "Pac-Man Demo (movement)";

            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Render += OnRender;
            _window.Update += OnUpdate;
            _window.Closing += OnClosing;
            _window.FramebufferResize += OnFramebufferResize;

            _window.Run();            
        }

        private static void OnLoad()
        {
            var gl = GL.GetApi(_window!);

            // ✅ GL setup
            gl.Enable(GLEnum.Blend);
            gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
            gl.Disable(GLEnum.DepthTest); // Not needed for 2D layers

            // ✅ Create maze
            var maze = _maze;

            // ✅ Create and init maze renderer first (background)
            _mazeRenderer = new MazeRenderer(gl, maze);
            _mazeRenderer.Initialize();

            Console.WriteLine($"[DIAG] Maze rows={maze.Rows} cols={maze.Columns} tiles={maze.Rows * maze.Columns} walls={maze.Walls.Count}");

            // ✅ Create Pac-Man
            _renderer = new PacManRenderer(gl, _window!);
            _renderer.Initialize();

            float baseRadius = 0.35f; // must match shader baseRadius
            _renderer.Scale = (maze.TileW * 0.40f) / baseRadius;

            // ✅ Choose a walkable starting tile near center
            Vector2D<float> startUV = maze.GetTileCenterUV(14, 13);
            startUV.X += maze.TileW / 2f; // nudges him perfectly center

            _renderer.PositionUV = startUV;
            _renderer.RotationIndex = 0;

            // ✅ Initialize pellets and remove unreachable ones
            maze.InitializePellets();
          
            // ✅ Create pellet renderer AFTER pellets finalized
            _pelletRenderer = new PelletRenderer(gl, maze);
            _pelletRenderer.Initialize();

            // ✅ Create ghost manager
            _ghostManager = new GhostManager(maze);
            
            // Ghosts setup
            _ghosts = [.. _ghostManager.GetGhosts().Select(ghost => new GhostRenderer(gl, ghost, _renderer.Scale * baseRadius))];            

            foreach (var ghost in _ghosts)
                ghost.Initialize();

            // ✅ Create controller and connect it
            _controller?.Dispose();
            _controller = new PacManController(_window!, startUV)
            {
                Speed = 0.10f, // UV units per second
                EntityRadius = _renderer.Scale * baseRadius,
                Margin = _renderer.Scale * baseRadius + 0.01f
            };
            _controller.SetMaze(maze);            
            
            // ✅ Hook up chomp audio
            try
            {
                _audio = new GameAudio("Assets/Audio/waka.wav");
                _audio.Initialize();
                _renderer.OnChomp += () => {
                    _audio?.PlayChomp();
                    _hud?.AddScore(10);
                };
                _controller.OnSuperPelletEaten += () =>
                {
                    _audio?.PlayChomp();
                    _hud?.AddScore(50); // example score bonus
                    //_ghostManager?.EnterFrightenedMode(); // later step when AI exists
                };

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Audio init failed: {ex.Message}");
            }

            // ✅ Create HUD
            _hud = new HudRenderer(gl, _window!.Size.X, _window.Size.Y);
            _hud.Initialize();
        }

        private static void OnUpdate(double dt)
        {
            if (_controller != null)
            {
                _controller.Update(dt);
                _ghostManager?.Update(dt);

                // Feed controller state into renderer
                if (_renderer != null && _pelletRenderer != null && _hud != null)
                {
                    var doRender = false;
                    _renderer.PositionUV = _controller.Position;
                    _renderer.RotationIndex = _controller.RotationIndex;

                    // returns true if pellet was eaten
                    if (_controller.TryEatPellet(_renderer.PositionUV))
                    {
                        _pelletRenderer.MovePelletOutOfMaze(_renderer.PositionUV);
                        doRender = true;
                        _renderer.Chomp(_window!.Time);
                    }
                    else if (_controller.TryEatSuperPellet(_renderer.PositionUV))
                    {
                        _pelletRenderer.MoveSuperPelletOutOfMaze(_renderer.PositionUV);
                        _controller.RaiseSuperPelletEaten();
                        doRender = true;
                    }
                    if (doRender)
                        _pelletRenderer.Render((float)_window!.Time);
                }
            }
        }

        private static void OnRender(double dt)
        {
            var gl = GL.GetApi(_window!);

            // ✅ Clear screen each frame
            gl.ClearColor(0f, 0f, 0f, 1f); // black background
            gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            // ✅ Draw maze first
            _mazeRenderer?.Render();

            // ✅ Then pellets
            _pelletRenderer?.Render((float)_window!.Time);

            // ✅ Then Pac-Man
            _renderer?.Render((float)_window!.Time);

            // ✅ Then ghosts
            foreach (var ghost in _ghosts!)
                ghost.Render();

            // ✅ Then HUD
            _hud?.Render();            
        }


        private static void OnFramebufferResize(Vector2D<int> size)
        {
            var gl = GL.GetApi(_window!);
            gl.Viewport(size);
            _renderer?.OnResize(size.X, size.Y);
        }

        private static void OnClosing()
        {
            _controller?.Dispose();
            _audio?.Dispose();
            _renderer?.Dispose();
        }
    }
}
