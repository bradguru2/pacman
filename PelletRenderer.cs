using Silk.NET.OpenGL;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;

namespace PacMan
{
    public class PelletRenderer
    {
        private readonly GL _gl;
        private readonly Maze _maze;
        private uint _vao, _vbo;
        private uint _program;
        private uint _pelletCount;
        private float PelletSize;


        public PelletRenderer(GL gl, Maze maze)
        {
            _gl = gl;
            _maze = maze;
        }

        unsafe public void Initialize()
        {
            var verts = new List<float>();

            for (int r = 0; r < _maze.Rows; r++)
            {
                for (int c = 0; c < _maze.Columns; c++)
                {
                    if (!_maze.Pellets[r, c])
                        continue;

                    // Map (r,c) to UV space
                    float u = (c + 0.5f) / _maze.Columns;
                    float v = 1.0f - ((r + 0.5f) / _maze.Rows);


                    float s = _maze.TileW * 0.10f; // small pellet size ~10% of tile width

                    verts.AddRange(new float[]
                    {
                        u - s, v - s,
                        u + s, v - s,
                        u + s, v + s,
                        u - s, v - s,
                        u + s, v + s,
                        u - s, v + s
                    });
                }
            }

            _pelletCount = (uint)(verts.Count / 2);

            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();
            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(GLEnum.ArrayBuffer, _vbo);

            fixed (float* v = verts.ToArray())
            {
                _gl.BufferData(GLEnum.ArrayBuffer, (nuint)(verts.Count * sizeof(float)), v, GLEnum.StaticDraw);
            }

            _gl.VertexAttribPointer(0, 2, GLEnum.Float, false, 2 * sizeof(float), (void*)0);
            _gl.EnableVertexAttribArray(0);

            string vert = @"
                #version 330 core
                layout(location = 0) in vec2 aPos;
                void main() { gl_Position = vec4(aPos * 2.0 - 1.0, 0.0, 1.0); }
            ";

            string frag = @"
                #version 330 core
                out vec4 FragColor;
                void main() { FragColor = vec4(1.0, 1.0, 1.0, 1.0); } // white pellets
            ";

            _program = ShaderUtils.CreateProgram(_gl, vert, frag);
        }

        public void Render()
        {
            if (_pelletCount == 0) return;
            _gl.UseProgram(_program);
            _gl.BindVertexArray(_vao);
            _gl.Disable(GLEnum.DepthTest);
            _gl.DrawArrays(GLEnum.Triangles, 0, _pelletCount);
            _gl.BindVertexArray(0);
            _gl.UseProgram(0);
        }
    }
}