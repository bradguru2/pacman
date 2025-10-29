using Silk.NET.OpenGL;
using Silk.NET.Maths;

namespace PacMan
{
    public class PelletRenderer
    {
        private readonly GL _gl;
        private readonly Maze _maze;
        private uint _vao, _vbo;
        private uint _program;
        private uint _pelletCount;
        private uint _superVAO, _superVBO;
        private uint _superCount;
        private int _iColorLoc, _iTimeLoc;
        private readonly float _pelletSize;
        private readonly float _superPelletSize;
        private readonly Dictionary<(int, int), int> _pelletMap = new(); // Glue Maze row/col to pellet index
        private readonly List<float> _verts = [];
        private readonly List<float> _superVerts = [];
        private float[]? _cachedPelletArray;
        private float[]? _cachedSuperVerts;                          // full float array for all super pellets
        private Dictionary<(int r, int c), int> _superPelletMap = new();    // map tile -> pellet index


        public PelletRenderer(GL gl, Maze maze)
        {
            _gl = gl;
            _maze = maze;
            _pelletSize = maze.TileW * 0.10f;
            _superPelletSize = maze.TileW * 0.18f;
            _cachedPelletArray = null;
            _cachedSuperVerts = null;
        }

        unsafe public void Initialize()
        {
            var pelletIndex = -1;
            var superPelletIndex = -1;

            for (int r = 0; r < _maze.Rows; r++)
            {
                for (int c = 0; c < _maze.Columns; c++)
                {
                    float u = (c + 0.5f) / _maze.Columns;
                    float v = 1.0f - ((r + 0.5f) / _maze.Rows);

                    if (_maze.Pellets[r, c])
                    {
                        float s = _pelletSize;
                        _verts.AddRange(
                        [
                            u - s, v - s,
                            u + s, v - s,
                            u + s, v + s,
                            u - s, v - s,
                            u + s, v + s,
                            u - s, v + s
                        ]);
                        _pelletMap[(r, c)] = ++pelletIndex;
                    }
                    else if (_maze.SuperPellets[r, c])
                    {
                        float s = _superPelletSize;
                        _superVerts.AddRange(
                        [
                            u - s, v - s,
                            u + s, v - s,
                            u + s, v + s,
                            u - s, v - s,
                            u + s, v + s,
                            u - s, v + s
                        ]);
                        _superPelletMap[(r, c)] = ++superPelletIndex;
                        Console.WriteLine($"[DIAG] Adding super pellet at row={r}, col={c}, index={superPelletIndex}");
                    }
                }
            }

            _pelletCount = (uint)(_verts.Count / 2);
            _superCount = (uint)(_superVerts.Count / 2);

            // VAO/VBO for normal pellets
            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();
            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(GLEnum.ArrayBuffer, _vbo);
            _cachedPelletArray = [.. _verts];
            fixed(float* v = &_cachedPelletArray[0])
            {
                _gl.BufferData(GLEnum.ArrayBuffer, (nuint)(_verts.Count * sizeof(float)), v, GLEnum.StaticDraw);
            }
            _gl.VertexAttribPointer(0, 2, GLEnum.Float, false, 2 * sizeof(float), (void*)0);
            _gl.EnableVertexAttribArray(0);

            // VAO/VBO for super pellets
            _superVAO = _gl.GenVertexArray();
            _superVBO = _gl.GenBuffer();
            _gl.BindVertexArray(_superVAO);
            _gl.BindBuffer(GLEnum.ArrayBuffer, _superVBO);
            _cachedSuperVerts = [.. _superVerts];
            fixed (float* v = &_cachedSuperVerts[0])
            {
                _gl.BufferData(GLEnum.ArrayBuffer, (nuint)(_superVerts.Count * sizeof(float)), v, GLEnum.StaticDraw);
            }
            _gl.VertexAttribPointer(0, 2, GLEnum.Float, false, 2 * sizeof(float), (void*)0);
            _gl.EnableVertexAttribArray(0);

            // Shared simple shaders
            string vert = @"
                #version 330 core
                layout(location = 0) in vec2 aPos;
                void main() { gl_Position = vec4(aPos * 2.0 - 1.0, 0.0, 1.0); }
            ";

            string frag = @"
                #version 330 core
                uniform vec3 uColor;
                uniform float uTime;
                out vec4 FragColor;

                void main() {
                    // Base brightness oscillates between 0.6 and 1.0
                    float glow = 0.8 + 0.2 * sin(uTime * 4.0);
                    FragColor = vec4(uColor * glow, 1.0);
                }
            ";


            _program = ShaderUtils.CreateProgram(_gl, vert, frag);
            _iColorLoc = _gl.GetUniformLocation(_program, "uColor");
            _iTimeLoc = _gl.GetUniformLocation(_program, "uTime");
        }

        public void Render(float timeSeconds)
        {
            _gl.UseProgram(_program);
            _gl.Disable(GLEnum.DepthTest);
            _gl.Enable(GLEnum.Blend);
            _gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

            _gl.Uniform1(_iTimeLoc, timeSeconds);

            // Draw normal pellets (white, static)
            _gl.Uniform3(_iColorLoc, 1.0f, 1.0f, 1.0f);
            _gl.BindVertexArray(_vao);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, _pelletCount);

            // Draw super pellets (yellow, glowing)
            _gl.Uniform3(_iColorLoc, 1.0f, 1.0f, 0.3f);
            _gl.BindVertexArray(_superVAO);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, _superCount);

            _gl.BindVertexArray(0);
            _gl.UseProgram(0);
        }
        public void MovePelletOutOfMaze(Vector2D<float> posUV)
        {
            OnMovePelletOutOfMaze(posUV, _cachedPelletArray, _pelletMap, _vbo);
        }
        
        public void MoveSuperPelletOutOfMaze(Vector2D<float> posUV)
        {
            OnMovePelletOutOfMaze(posUV, _cachedSuperVerts, _superPelletMap, _superVBO);
        }

        private void OnMovePelletOutOfMaze(Vector2D<float> posUV, float []? cachedFloatArray, Dictionary<(int, int), int> positionMap, uint vbo)
        {
            ArgumentNullException.ThrowIfNull(cachedFloatArray);

            // Convert to maze tile coordinates
            int col = (int)(posUV.X * _maze.Columns);
            int row = (int)((1.0f - posUV.Y) * _maze.Rows); // Flip Y if maze rows count from top

            if (!positionMap.TryGetValue((row, col), out int pelletIndex))
                return;

            Console.WriteLine($"[DIAG] Removing pellet at row={row}, col={col}, index={pelletIndex}");

            // Compute float offset in the cached array
            int floatOffset = pelletIndex * 12;

            // Overwrite the cached vertices for this pellet with degenerate coords (offscreen)
            for (int i = 0; i < 12; i++)
                cachedFloatArray[floatOffset + i] = -10f; // well offscreen

            unsafe
            {
                _gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
                fixed (float* v = &cachedFloatArray[floatOffset])
                {
                    _gl.BufferSubData(
                        GLEnum.ArrayBuffer,
                        (nint)(floatOffset * sizeof(float)),
                        (nuint)(12 * sizeof(float)),
                        v
                    );
                }
                _gl.BindBuffer(GLEnum.ArrayBuffer, 0);
            }
        }

    }
}