using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

// Vertex formats matching the C examples
[StructLayout(LayoutKind.Sequential)]
public struct PositionVertex(float x, float y, float z)
{
    public float X = x, Y = y, Z = z;
}

[StructLayout(LayoutKind.Sequential)]
public struct PositionColorVertex(float x, float y, float z, byte r, byte g, byte b, byte a)
{
    public float X = x, Y = y, Z = z;
    public byte R = r, G = g, B = b, A = a;
}

[StructLayout(LayoutKind.Sequential)]
public struct PositionTextureVertex(float x, float y, float z, float u, float v)
{
    public float X = x, Y = y, Z = z;
    public float U = u, V = v;
}

public static class Common
{
    public static string BasePath
    {
        get
        {
            field ??= SDL.GetBasePath() ?? AppContext.BaseDirectory;
            return field;
        }
    }

    private static SDL.GPUShaderFormat ToSdlFormat(ShaderFormat format)
    {
        return format switch
        {
            ShaderFormat.SPIRV => SDL.GPUShaderFormat.SPIRV,
            ShaderFormat.DXIL => SDL.GPUShaderFormat.DXIL,
            ShaderFormat.MSL => SDL.GPUShaderFormat.MSL,
            _ => SDL.GPUShaderFormat.SPIRV
        };
    }

    public static unsafe IntPtr LoadShader(
        IntPtr device,
        string shaderFilename,
        uint samplerCount = 0,
        uint uniformBufferCount = 0,
        uint storageBufferCount = 0,
        uint storageTextureCount = 0)
    {
        // Ensure SlangCompiler is initialized
        if (!SlangCompiler.Init())
        {
            return IntPtr.Zero;
        }

        // Auto-detect the shader stage from the file name
        ShaderStage stage;
        SDL.GPUShaderStage gpuStage;
        if (shaderFilename.Contains(".vert"))
        {
            stage = ShaderStage.Vertex;
            gpuStage = SDL.GPUShaderStage.Vertex;
        }
        else if (shaderFilename.Contains(".frag"))
        {
            stage = ShaderStage.Fragment;
            gpuStage = SDL.GPUShaderStage.Fragment;
        }
        else
        {
            Console.WriteLine("Invalid shader stage!");
            return IntPtr.Zero;
        }

        // Try .slang first, then .hlsl
        string fullPath = Path.Combine(BasePath, "Content", "Shaders", "Source", $"{shaderFilename}.slang");
        if (!File.Exists(fullPath))
        {
            fullPath = Path.Combine(BasePath, "Content", "Shaders", "Source", $"{shaderFilename}.hlsl");
        }

        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"Shader source file not found: {fullPath}");
            return IntPtr.Zero;
        }

        var shaderSource = File.ReadAllText(fullPath);

        // Get the preferred shader format for this platform
        var format = SlangCompiler.GetPreferredFormat();

        // Compile to the appropriate format using Slang
        var bytecode = SlangCompiler.Compile(shaderSource, "main", stage, format);
        if (bytecode == null)
        {
            Console.WriteLine($"Failed to compile shader: {shaderFilename}");
            return IntPtr.Zero;
        }

        // Create GPU shader
        fixed (byte* bytecodePtr = bytecode)
        {
            var createInfo = new SDL.GPUShaderCreateInfo
            {
                CodeSize = (nuint)bytecode.Length,
                Code = (nint)bytecodePtr,
                Format = ToSdlFormat(format),
                Stage = gpuStage,
                Entrypoint = "main",
                NumSamplers = samplerCount,
                NumUniformBuffers = uniformBufferCount,
                NumStorageBuffers = storageBufferCount,
                NumStorageTextures = storageTextureCount
            };

            var shader = SDL.CreateGPUShader(device, in createInfo);
            if (shader == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to create GPU shader: {SDL.GetError()}");
            }
            return shader;
        }
    }
}
