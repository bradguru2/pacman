// PacManRenderer.cs
using System;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Maths;

namespace PacMan
{
    /// <summary>
    /// Renders a procedural Pac-Man using a fragment shader and raises OnChomp events
    /// when the mouth animation crosses the "open" threshold. Supports scaling,
    /// position, and 4-way rotation (passed via uniforms).
    /// </summary>
    public sealed class PacManRenderer : IDisposable
    {
        private readonly GL _gl;
        private readonly IWindow _window;

        private uint _vao;
        private uint _vbo;
        private uint _ebo;
        private uint _program;
        private int _uResolutionLoc;
        private int _uTimeLoc;
        private int _uPosLoc;
        private int _uRotationLoc;
        private int _uScaleLoc;
        private int _width = 800; // Default windowed
        private int _height = 600; // Default windowed

        private bool _isDying = false;
        private double _deathStartTime;


        // Chomp event - subscribers will play sound
        public event Action? OnChomp;

        // Debounce / smoothing state
        private bool _prevMouthOpen = false;
        private double _lastChompTime = 0.0;
        private const double _chompCooldown = 0.250; // seconds minimum between chomps
        private const float _mouthOpenThreshold = 0.45f; // threshold on mouthAnim to consider "open"
        private const float DeathDuration = 2.0f; // seconds for death animation

        // External controls
        public float Scale { get; set; } = 0.5f; // default scale factor
        public Vector2D<float> PositionUV { get; set; } = new Vector2D<float>(0.5f, 0.5f);
        public int RotationIndex { get; set; } = 0; // 0=Right,1=Up,2=Left,3=Down

        public PacManRenderer(GL gl, IWindow window)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        unsafe public void Initialize()
        {
            _gl.Enable(GLEnum.Blend);
            _gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

            float[] vertices = {
                -1f,  1f,        0f, 1f,
                -1f, -1f,        0f, 0f,
                 1f, -1f,        1f, 0f,
                 1f,  1f,        1f, 1f
            };

            uint[] indices = { 0, 1, 2, 2, 3, 0 };

            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();
            _ebo = _gl.GenBuffer();

            _gl.BindVertexArray(_vao);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            unsafe
            {
                fixed (float* v = vertices)
                {
                    _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);
                }
            }

            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            unsafe
            {
                fixed (uint* i = indices)
                {
                    _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), i, BufferUsageARB.StaticDraw);
                }
            }

            uint stride = (2 + 2) * (uint)sizeof(float);

            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);

            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));

            _gl.BindVertexArray(0);

            string vert = @"
                #version 330 core
                layout(location = 0) in vec2 aPos;
                layout(location = 1) in vec2 aUV;
                out vec2 vUV;
                void main()
                {
                    vUV = aUV;
                    gl_Position = vec4(aPos, 0.0, 1.0);
                }
            ";

            // --- replace the existing `frag` string with this exact text ---
            string frag = @"
                #version 330 core
                in vec2 vUV;
                out vec4 FragColor;

                uniform vec2 uResolution;
                uniform float uTime;
                uniform vec2 uPos;
                uniform float uRotation;
                uniform float uScale;

                mat2 rot(float a) {
                    float c = cos(a);
                    float s = sin(a);
                    return mat2(c, -s, s, c);
                }

                void main()
                {
                    // convert from 0..1 UVs to centered coordinates
                    vec2 uv = vUV;
                    vec2 centered = uv - uPos;

                    // correct for aspect ratio
                    float aspect = uResolution.x / uResolution.y;
                    centered.y *= aspect;

                    // rotate so 0 rotation = right
                    float angle = 0.0;
                    if (uRotation < 0.5)       angle = 0.0;
                    else if (uRotation < 1.5)  angle = -1.5708;
                    else if (uRotation < 2.5)  angle = 3.1416;
                    else                       angle = 1.5708;
                    vec2 rc = rot(-angle) * centered;

                    // Pac-Man body parameters
                    float baseRadius = 0.35;
                    float radius = baseRadius * uScale;

                    // mouth animation
                    float mouthAnim = abs(sin(uTime * 4.0));
                    float mouthHalf = 0.6 * mouthAnim + 0.15;

                    float r = length(rc);
                    float ang = atan(rc.y, rc.x);

                    vec3 bodyColor = vec3(1.0, 0.9, 0.0);

                    // default: transparent
                    float alpha = 0.0;
                    vec3 color = vec3(0.0);

                    // draw only if inside body and not in mouth
                    if (r <= radius && !(ang > -mouthHalf && ang < mouthHalf))
                    {
                        alpha = 1.0;
                        color = bodyColor;

                        // eye
                        vec2 eyePos = vec2(0.0, radius * 0.45);
                        float eyeRadius = 0.06 * uScale;
                        float eyeDist = length(rc - eyePos);
                        if (eyeDist < eyeRadius)
                            color = vec3(0.0);
                    }

                    FragColor = vec4(color, alpha); //FragColor = vec4(1.0, 1.0, 1.0, 1.0);                     
                }
            ";


            _program = CreateProgram(vert, frag);

            // get uniform locations
            _uResolutionLoc = _gl.GetUniformLocation(_program, "uResolution");
            _uTimeLoc = _gl.GetUniformLocation(_program, "uTime");
            _uPosLoc = _gl.GetUniformLocation(_program, "uPos");
            _uRotationLoc = _gl.GetUniformLocation(_program, "uRotation");
            _uScaleLoc = _gl.GetUniformLocation(_program, "uScale");

            // set initial viewport/resolution
            var size = _window.Size;
            _width = size.X;
            _height = size.Y;
            _gl.Viewport(new Vector2D<int>(_width, _height));
        }

        public void OnResize(int width, int height)
        {
            _width = Math.Max(1, width);
            _height = Math.Max(1, height);
        }

        unsafe public void Render(float timeSeconds)
        {
            // mouth detection logic (same as shader) for OnChomp event
            float mouthAnim = MathF.Abs(MathF.Sin(timeSeconds * 4.0f));
            bool mouthOpen = mouthAnim > _mouthOpenThreshold;
            
            _prevMouthOpen = mouthOpen;

            _gl.UseProgram(_program);

            if (_uResolutionLoc != -1)
                _gl.Uniform2(_uResolutionLoc, (float)_width, (float)_height);
            if (_uTimeLoc != -1)
                _gl.Uniform1(_uTimeLoc, timeSeconds);

            // pass position and rotation and scale
            if (_uPosLoc != -1)
                _gl.Uniform2(_uPosLoc, PositionUV.X, PositionUV.Y);
            if (_uRotationLoc != -1)
                _gl.Uniform1(_uRotationLoc, (float)RotationIndex);
            if (_uScaleLoc != -1)
            {
                if (_isDying)
                {
                    float t = (float)(_window!.Time - _deathStartTime);
                    float shrink = MathF.Max(0f, 1f - t / DeathDuration);
                    _gl.Uniform1(_uScaleLoc, Scale * shrink);

                    if (shrink <= 0f)
                        _isDying = false; // finished
                }
                else
                {
                    _gl.Uniform1(_uScaleLoc, Scale);
                }
            }

            _gl.BindVertexArray(_vao);
            // Force 2D overlay and blending every frame
            _gl.Disable(GLEnum.DepthTest);
            _gl.Enable(GLEnum.Blend);
            _gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);

            _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, null);
            _gl.BindVertexArray(0);

            _gl.UseProgram(0);
        }

        public void Chomp(double currentTime)
        {
            if (currentTime - _lastChompTime < _chompCooldown)
                return;

            _lastChompTime = currentTime;
            OnChomp?.Invoke();
        }

        public void BeginDeathAnimation()
        {
            _isDying = true;
            _deathStartTime = _window!.Time;
        }

        public bool IsDeathAnimationDone(double currentTime)
        {
            return !_isDying && (currentTime - _deathStartTime) > 2.0; // seconds
        }


        private uint CreateShader(ShaderType type, string src)
        {
            uint shader = _gl.CreateShader(type);
            _gl.ShaderSource(shader, src);
            _gl.CompileShader(shader);
            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status == 0)
            {
                string log = _gl.GetShaderInfoLog(shader);
                throw new Exception($"Shader compile error ({type}): {log}");
            }
            return shader;
        }

        private uint CreateProgram(string vertexSrc, string fragSrc)
        {
            uint vs = CreateShader(ShaderType.VertexShader, vertexSrc);
            uint fs = CreateShader(ShaderType.FragmentShader, fragSrc);
            uint prog = _gl.CreateProgram();
            _gl.AttachShader(prog, vs);
            _gl.AttachShader(prog, fs);
            _gl.LinkProgram(prog);
            _gl.GetProgram(prog, ProgramPropertyARB.LinkStatus, out int status);
            if (status == 0)
            {
                string log = _gl.GetProgramInfoLog(prog);
                throw new Exception($"Program link error: {log}");
            }

            _gl.DetachShader(prog, vs);
            _gl.DetachShader(prog, fs);
            _gl.DeleteShader(vs);
            _gl.DeleteShader(fs);

            return prog;
        }

        public void Dispose()
        {
            if (_gl != null)
            {
                if (_program != 0) _gl.DeleteProgram(_program);
                if (_vbo != 0) _gl.DeleteBuffer(_vbo);
                if (_ebo != 0) _gl.DeleteBuffer(_ebo);
                if (_vao != 0) _gl.DeleteVertexArray(_vao);
            }
        }
    }
}
