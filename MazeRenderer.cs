// MazeRenderer.cs
// Renders the Maze Walls as blue rectangles. Uses one VBO built once from the wall rectangles.
// Drawing uses triangles (two per rect). All coordinates are converted from UV (0..1) to NDC (-1..1).

using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using Silk.NET.Maths;

namespace PacMan
{
    public sealed class MazeRenderer : IDisposable
    {
        private readonly GL _gl;
        private readonly Maze _maze;
        private uint _vao;
        private uint _vbo;
        private int _vertexCount;
        private uint _program;

        // simple color for walls (classic blue)
        private readonly (float r, float g, float b) _wallColor = (0.0f, 0.19f, 1.0f); // #0031FF-ish

        public MazeRenderer(GL gl, Maze maze)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
            _maze = maze ?? throw new ArgumentNullException(nameof(maze));
        }

        public void Initialize()
        {
            BuildMesh();
            CreateShader();
            var err = _gl.GetError();
            Console.WriteLine($"[DIAG] GL.GetError() after Maze.Initialize() = {err}");
        }

        unsafe private void BuildMesh()
        {
            // Build a list of vec2 positions for all rectangles (two triangles each).
            var verts = new List<float>();

            foreach (var w in _maze.Walls)
            {
                // w.x/w.y are bottom-left in UV (0..1). Convert to NDC: ndc = uv*2 - 1
                float x0 = w.x * 2f - 1f;
                float y0 = w.y * 2f - 1f;
                float x1 = (w.x + w.w) * 2f - 1f;
                float y1 = (w.y + w.h) * 2f - 1f;

                // Triangle 1
                verts.Add(x0); verts.Add(y0);
                verts.Add(x1); verts.Add(y0);
                verts.Add(x1); verts.Add(y1);

                // Triangle 2
                verts.Add(x1); verts.Add(y1);
                verts.Add(x0); verts.Add(y1);
                verts.Add(x0); verts.Add(y0);
            }

            _vertexCount = verts.Count / 2;

            Console.WriteLine($"[DIAG] Built maze mesh verts={verts.Count} vertexCount={_vertexCount} wallCount={_maze.Walls.Count}");


            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();

            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            unsafe
            {
                fixed (float* p = verts.ToArray())
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(verts.Count * sizeof(float)), p, BufferUsageARB.StaticDraw);
                }
            }

            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);

            _gl.BindVertexArray(0);
        }

        private void CreateShader()
        {
            string vert = @"
                #version 330 core
                layout(location = 0) in vec2 aPos;
                void main()
                {
                    gl_Position = vec4(aPos, 0.0, 1.0);
                }
            ";

            string frag = @"
                #version 330 core
                out vec4 FragColor;
                uniform vec3 uColor;
                void main()
                {
                    FragColor = vec4(uColor, 1.0);
                }
            ";

            uint vs = _gl.CreateShader(ShaderType.VertexShader);
            _gl.ShaderSource(vs, vert);
            _gl.CompileShader(vs);
            _gl.GetShader(vs, ShaderParameterName.CompileStatus, out int vsok);
            if (vsok == 0) throw new Exception("Maze vertex shader failed: " + _gl.GetShaderInfoLog(vs));

            uint fs = _gl.CreateShader(ShaderType.FragmentShader);
            _gl.ShaderSource(fs, frag);
            _gl.CompileShader(fs);
            _gl.GetShader(fs, ShaderParameterName.CompileStatus, out int fsok);
            if (fsok == 0) throw new Exception("Maze fragment shader failed: " + _gl.GetShaderInfoLog(fs));

            _program = _gl.CreateProgram();
            _gl.AttachShader(_program, vs);
            _gl.AttachShader(_program, fs);
            _gl.LinkProgram(_program);
            _gl.GetProgram(_program, ProgramPropertyARB.LinkStatus, out int linkOk);
            if (linkOk == 0) throw new Exception("Maze shader link failed: " + _gl.GetProgramInfoLog(_program));

            // After compiling vertex shader (right after gl.CompileShader(vs);)
            _gl.GetShader(vs, ShaderParameterName.CompileStatus, out int vstat);
            Console.WriteLine($"[DIAG] Maze VS compile status={vstat}; log={_gl.GetShaderInfoLog(vs)}");

            // After compiling fragment shader
            _gl.GetShader(fs, ShaderParameterName.CompileStatus, out int fstat);
            Console.WriteLine($"[DIAG] Maze FS compile status={fstat}; log={_gl.GetShaderInfoLog(fs)}");

            // After linking
            _gl.GetProgram(_program, ProgramPropertyARB.LinkStatus, out int linkOk2);
            Console.WriteLine($"[DIAG] Maze program link status={linkOk2}; log={_gl.GetProgramInfoLog(_program)}");
    
            _gl.DetachShader(_program, vs);
            _gl.DetachShader(_program, fs);
            _gl.DeleteShader(vs);
            _gl.DeleteShader(fs);
        }

        public void Render()
        {
            if (_vertexCount == 0) return;

            // Ensure 2D draw state (no depth test for UI/2D)
            _gl.Disable(GLEnum.DepthTest);

            _gl.UseProgram(_program);
            int loc = _gl.GetUniformLocation(_program, "uColor");
            if (loc != -1)
                _gl.Uniform3(loc, _wallColor.r, _wallColor.g, _wallColor.b);

            _gl.BindVertexArray(_vao);

            // CORRECT: cast PrimitiveType to GLEnum and pass int count
            _gl.DrawArrays(GLEnum.Triangles, 0, (uint)_vertexCount);

            _gl.BindVertexArray(0);
            _gl.UseProgram(0);
        }       

        public void Dispose()
        {
            if (_program != 0) _gl.DeleteProgram(_program);
            if (_vbo != 0) _gl.DeleteBuffer(_vbo);
            if (_vao != 0) _gl.DeleteVertexArray(_vao);
        }
    }
}
