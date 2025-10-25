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
        private uint _superVAO, _superVBO;
        private uint _superCount;
        private int _iColorLoc, _iTimeLoc;




        public PelletRenderer(GL gl, Maze maze)
        {
            _gl = gl;
            _maze = maze;
        }

        unsafe public void Initialize()
        {
            var verts = new List<float>();
            var superVerts = new List<float>();

            for (int r = 0; r < _maze.Rows; r++)
            {
                for (int c = 0; c < _maze.Columns; c++)
                {
                    float u = (c + 0.5f) / _maze.Columns;
                    float v = 1.0f - ((r + 0.5f) / _maze.Rows);

                    if (_maze.Pellets[r, c])
                    {
                        float s = _maze.TileW * 0.10f; // regular pellet
                        verts.AddRange(new float[]
                        {
                    u - s, v - s,  u + s, v - s,  u + s, v + s,
                    u - s, v - s,  u + s, v + s,  u - s, v + s
                        });
                    }
                    else if (_maze.SuperPellets[r, c])
                    {
                        float s = _maze.TileW * 0.18f; // slightly larger
                        superVerts.AddRange(new float[]
                        {
                    u - s, v - s,  u + s, v - s,  u + s, v + s,
                    u - s, v - s,  u + s, v + s,  u - s, v + s
                        });
                    }
                }
            }

            _pelletCount = (uint)(verts.Count / 2);
            _superCount = (uint)(superVerts.Count / 2);

            // VAO/VBO for normal pellets
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

            // VAO/VBO for super pellets
            _superVAO = _gl.GenVertexArray();
            _superVBO = _gl.GenBuffer();
            _gl.BindVertexArray(_superVAO);
            _gl.BindBuffer(GLEnum.ArrayBuffer, _superVBO);
            fixed (float* v = superVerts.ToArray())
            {
                _gl.BufferData(GLEnum.ArrayBuffer, (nuint)(superVerts.Count * sizeof(float)), v, GLEnum.StaticDraw);
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


    }
}