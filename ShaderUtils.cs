using Silk.NET.OpenGL;
using System;

namespace PacMan
{
    public static class ShaderUtils
    {
        public static uint CreateProgram(GL gl, string vertexSource, string fragmentSource)
        {
            uint vertex = CompileShader(gl, ShaderType.VertexShader, vertexSource);
            uint fragment = CompileShader(gl, ShaderType.FragmentShader, fragmentSource);

            uint program = gl.CreateProgram();
            gl.AttachShader(program, vertex);
            gl.AttachShader(program, fragment);
            gl.LinkProgram(program);

            // Check link status
            gl.GetProgram(program, GLEnum.LinkStatus, out int linked);
            if (linked == 0)
            {
                string log = gl.GetProgramInfoLog(program);
                throw new Exception($"Program link error: {log}");
            }

            // cleanup
            gl.DetachShader(program, vertex);
            gl.DetachShader(program, fragment);
            gl.DeleteShader(vertex);
            gl.DeleteShader(fragment);

            return program;
        }

        private static uint CompileShader(GL gl, ShaderType type, string src)
        {
            uint shader = gl.CreateShader(type);
            gl.ShaderSource(shader, src);
            gl.CompileShader(shader);

            // Check compile status
            gl.GetShader(shader, GLEnum.CompileStatus, out int compiled);
            if (compiled == 0)
            {
                string log = gl.GetShaderInfoLog(shader);
                throw new Exception($"{type} compile error: {log}");
            }

            return shader;
        }
    }
}