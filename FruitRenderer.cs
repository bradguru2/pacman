using Silk.NET.OpenGL;
using Silk.NET.Maths;

namespace PacMan
{
    public class FruitRenderer
    {
        private readonly GL _gl;
        private Fruit _fruit;
        private uint _vao, _vbo, _program;
        private int _uPosLoc, _uScaleLoc, _uColorLoc;

        public FruitRenderer(GL gl, Fruit fruit)
        {
            _gl = gl;
            _fruit = fruit;
        }

        public unsafe void Initialize()
        {
            float[] vertices =
            {
                0,0, 1,0, 1,1,
                0,0, 1,1, 0,1
            };

            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();
            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(GLEnum.ArrayBuffer, _vbo);
            fixed (float* v = vertices)
                _gl.BufferData(GLEnum.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), v, GLEnum.StaticDraw);

            _gl.VertexAttribPointer(0, 2, GLEnum.Float, false, 2 * sizeof(float), (void*)0);
            _gl.EnableVertexAttribArray(0);

            string vert = @"
                #version 330 core
                layout(location = 0) in vec2 aPos;
                uniform vec2 uPos;
                uniform float uScale;
                out vec2 vLocal;
                void main() {
                    vLocal = (aPos - 0.5) * 2.0;
                    vec2 pos = (aPos - 0.5) * uScale + uPos;
                    gl_Position = vec4(pos * 2.0 - 1.0, 0.0, 1.0);
                }";

            string frag = @"
                #version 330 core
                uniform vec3 uColor;
                in vec2 vLocal;
                out vec4 FragColor;
                void main() {
                    float r = length(vLocal);
                    if (r > 1.0) discard; // round fruit
                    FragColor = vec4(uColor, 1.0);
                }";

            _program = ShaderUtils.CreateProgram(_gl, vert, frag);
            _uPosLoc = _gl.GetUniformLocation(_program, "uPos");
            _uScaleLoc = _gl.GetUniformLocation(_program, "uScale");
            _uColorLoc = _gl.GetUniformLocation(_program, "uColor");
        }

        public void Render(float scale = 0.03f)
        {
            if (!_fruit.Active) return;

            _gl.UseProgram(_program);
            _gl.Uniform2(_uPosLoc, _fruit.PosUV.X, _fruit.PosUV.Y);
            _gl.Uniform1(_uScaleLoc, scale);
            _gl.Uniform3(_uColorLoc, 1.0f, 0.0f, 0.2f); // reddish fruit

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