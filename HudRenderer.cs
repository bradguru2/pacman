using Silk.NET.OpenGL;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using System;

namespace PacMan
{
    public class HudRenderer
    {
        private readonly GL _gl;
        private int _score;
        private int _lives;
        private uint _texture;
        private uint _vao;
        private uint _vbo;
        private uint _program;

        private Font _font;
        private int _uTexLoc;
        private int _uPosLoc;

        private int _width, _height;

        public HudRenderer(GL gl, int windowWidth, int windowHeight)
        {
            _gl = gl;
            _width = windowWidth;
            _height = windowHeight;
            _score = 0;
            _lives = 3;
            // Load a fallback font
            var collection = new FontCollection();
            try
            {
                _font = SystemFonts.Families.First(f => f.Name == "Ubuntu Mono").CreateFont(64); // use your working one
                Console.WriteLine($"[DIAG] 'font={_font.Name}' loaded for HUD");
            }
            catch
            {
                _font = SystemFonts.CreateFont("Arial", 24); // fallback
            }
        }

        public void Initialize()
        {
            float[] vertices = {
                // pos      // uv
                0.0f, 0.0f,  0.0f, 0.0f,
                0.25f, 0.0f,  1.0f, 0.0f,
                0.25f, 0.05f,  1.0f, 1.0f,
                0.0f, 0.0f,   0.0f, 0.0f,
                0.25f, 0.05f, 1.0f, 1.0f,
                0.0f, 0.05f,  0.0f, 1.0f
            };

            unsafe
            {
                _vao = _gl.GenVertexArray();
                _vbo = _gl.GenBuffer();

                _gl.BindVertexArray(_vao);
                _gl.BindBuffer(GLEnum.ArrayBuffer, _vbo);
                fixed (float* v = vertices)
                {
                    _gl.BufferData(GLEnum.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), v, GLEnum.StaticDraw);
                }

                int stride = 4 * sizeof(float);
                _gl.VertexAttribPointer(0, 2, GLEnum.Float, false, (uint)stride, (void*)0);
                _gl.EnableVertexAttribArray(0);

                _gl.VertexAttribPointer(1, 2, GLEnum.Float, false, (uint)stride, (void*)(2 * sizeof(float)));
                _gl.EnableVertexAttribArray(1);
            }

            // Create initial texture
            UpdateTexture();

            // Vertex shader (2D quad)
            string vert = @"
               #version 330 core
               layout(location = 0) in vec2 aPos;
               layout(location = 1) in vec2 aUV;
               uniform vec2 uPos;
               out vec2 vUV;

               void main() {
                    // For example:
                    float offsetX = 0.001f;
                    float offsetY = 0.012f;

                    // Then add these offsets in your vertex shader:
                    gl_Position = vec4((aPos.x + offsetX) * 2.0 - 1.0, (aPos.y + offsetY) * 2.0 - 1.0, 0.0, 1.0);

                    //vec2 pos = aPos + uPos; // translate HUD quad
                    //gl_Position = vec4(pos * 2.0 - 1.0, 0.0, 1.0);
                    vUV = aUV;
                }
            ";

            // Fragment shader (text texture)
            string frag = @"
                #version 330 core
                in vec2 vUV;
                out vec4 FragColor;
                uniform sampler2D uTex;

                void main() {
                    FragColor = texture(uTex, vUV); // vec4(1.0, 1.0, 1.0, 1.0); // 
                }
            ";
            
            _uTexLoc = _gl.GetUniformLocation(_program, "uTex");
            _uPosLoc = _gl.GetUniformLocation(_program, "uPos");

            _program = ShaderUtils.CreateProgram(_gl, vert, frag);            
        }

        public void AddScore(int amount)
        {
            _score += amount;
            UpdateTexture();
        }

        public void LoseLife()
        {
            _lives = Math.Max(0, _lives - 1);
            UpdateTexture();
        }

        private void UpdateTexture()
        {
            string text = $"SCORE: {_score}    LIVES: {_lives}";
            using var img = new Image<Rgba32>(_width, 64);
            img.Mutate(ctx =>
            {
                ctx.Fill(Color.Transparent);
                ctx.DrawText(text, _font, Color.Yellow, new PointF(10, 10));
            });

            img.Mutate(ctx => ctx.Flip(FlipMode.Vertical));

            // Convert to pixel data
            var pixels = new byte[_width * 64 * 4];
            img.CopyPixelDataTo(pixels);

            if (_texture == 0)
                _texture = _gl.GenTexture();

            _gl.BindTexture(GLEnum.Texture2D, _texture);
            unsafe
            {
                fixed (byte* p = pixels)
                {
                    _gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgba, (uint)_width, 64, 0, GLEnum.Rgba, GLEnum.UnsignedByte, p);
                }
            }
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            _gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            
            Console.WriteLine($"[HUD] Texture updated (Score={_score}, Lives={_lives})");
        }

        public void Render()
        {
            _gl.UseProgram(_program);
            _gl.ActiveTexture(GLEnum.Texture0);
            _gl.BindTexture(GLEnum.Texture2D, _texture);
            _gl.Uniform1(_uTexLoc, 0);

            // HUD top-left corner in NDC space (y=0.9 is near bottom)
            _gl.Uniform2(_uPosLoc, -0.98f, -0.95f);

            _gl.BindVertexArray(_vao);
            _gl.Disable(GLEnum.DepthTest);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
            _gl.BindVertexArray(0);
            _gl.UseProgram(0);
        }
    }
}
