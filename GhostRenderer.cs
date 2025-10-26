using Silk.NET.OpenGL;
using Silk.NET.Maths;
using System;

namespace PacMan
{
    public class GhostRenderer
    {
        private readonly GL _gl;
        private readonly Vector2D<float> _positionUV;
        private readonly Vector3D<float> _color;
        private readonly float _scale;
        private uint _vao, _vbo, _program;
        private int _uColorLoc, _uPosLoc, _uScaleLoc;

        public GhostRenderer(GL gl, Vector2D<float> positionUV, Vector3D<float> color, float scale = 0.0325f)
        {
            _gl = gl;
            _positionUV = positionUV;
            _color = color;
            _scale = scale;
        }

        unsafe public void Initialize()
        {
            // Simple quad (full screen unit)
            float[] vertices = new float[]
            {
            0, 0,
            1, 0,
            1, 1,
            0, 0,
            1, 1,
            0, 1
            };

            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();
            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(GLEnum.ArrayBuffer, _vbo);
            fixed (float* v = vertices)
            {
                _gl.BufferData(GLEnum.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), v, GLEnum.StaticDraw);
            }
            _gl.VertexAttribPointer(0, 2, GLEnum.Float, false, 2 * sizeof(float), (void*)0);
            _gl.EnableVertexAttribArray(0);

            // Simple shader that draws a ghost shape (circle with eyes)
            string vert = @"
                #version 330 core
                layout(location = 0) in vec2 aPos;
                uniform vec2 uPos;
                uniform float uScale;
                out vec2 vLocal;

            void main()
            {
                // vLocal goes from -1..1 across ghost quad
                vLocal = (aPos - 0.5) * 2.0;
                vec2 pos = (aPos - 0.5) * uScale + uPos;
                gl_Position = vec4(pos * 2.0 - 1.0, 0.0, 1.0);
            }

            ";

            string frag = @"
                #version 330 core
                uniform vec3 uColor;
                in vec2 vLocal;
                out vec4 FragColor;

                void main()
                {
                    vec2 p = vLocal;
                    vec3 color = uColor; // Starting Color ghost background

                    // if p is outside circle, discard
                    if (length(p) > 1.0 && p.y > -0.3)
                        discard;

                    // Eyes                    
                    vec2 leftEye = p - vec2(-0.35, 0.25);
                    vec2 rightEye = p - vec2(0.35, 0.25);

                    if (length(leftEye - vec2(0.05, 0.0)) < 0.1 || length(rightEye - vec2(0.05, 0.0)) < 0.1)
                        color = vec3(0.2, 0.4, 1.0);
                    else if (length(leftEye) < 0.25 || length(rightEye) < 0.25)
                        color = vec3(1.0);    

                    FragColor = vec4(color, 1.0);
                }
            ";

            _program = ShaderUtils.CreateProgram(_gl, vert, frag);

            _uColorLoc = _gl.GetUniformLocation(_program, "uColor");
            _uPosLoc = _gl.GetUniformLocation(_program, "uPos");
            _uScaleLoc = _gl.GetUniformLocation(_program, "uScale");
        }

        public void Render()
        {
            _gl.UseProgram(_program);

            _gl.Uniform3(_uColorLoc, _color.X, _color.Y, _color.Z);
            _gl.Uniform2(_uPosLoc, _positionUV.X, _positionUV.Y);
            _gl.Uniform1(_uScaleLoc, _scale);

            _gl.BindVertexArray(_vao);
            _gl.Disable(GLEnum.DepthTest);
            _gl.Enable(GLEnum.Blend);
            _gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);

            _gl.BindVertexArray(0);
            _gl.UseProgram(0);
        }
    }
}