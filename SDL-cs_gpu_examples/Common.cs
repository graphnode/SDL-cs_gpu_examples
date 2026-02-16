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
    /// <summary>
    /// Gets the base path of the application or project.
    /// In release mode, this returns the directory of the executable,
    /// and in debug mode, it traverses upwards from the executable's directory
    /// to locate the project's root directory containing the .csproj file.
    /// </summary>
    public static string BasePath
    {
        get
        {
            string basePath = SDL.GetBasePath() ?? AppContext.BaseDirectory;
#if DEBUG
            // Start from the executable directory
            var currentDir = new DirectoryInfo(basePath);

            // Walk up to find the project root (contains .csproj file)
            while (currentDir != null)
            {
                if (currentDir.GetFiles("*.csproj").Length > 0)
                {
                    field = currentDir.FullName;
                    return field;
                }
                currentDir = currentDir.Parent;
            }

            // Fallback to BasePath if we can't find the project root
#endif
            field = basePath;
            return field;
        }
    }

    public static IntPtr LoadShader(
        IntPtr device,
        string shaderFilename,
        uint samplerCount = 0,
        uint uniformBufferCount = 0,
        uint storageBufferCount = 0,
        uint storageTextureCount = 0)
    {
        // Auto-detect the shader stage from the file name
        SDL.GPUShaderStage stage;
        if (shaderFilename.Contains(".vert"))
        {
            stage = SDL.GPUShaderStage.Vertex;
        }
        else if (shaderFilename.Contains(".frag"))
        {
            stage = SDL.GPUShaderStage.Fragment;
        }
        else
        {
            Console.WriteLine("Invalid shader stage!");
            return IntPtr.Zero;
        }

        // Load HLSL source
        string sourcePath = Path.Combine(BasePath, "Content", "Shaders", $"{shaderFilename}.hlsl");
        if (!File.Exists(sourcePath))
        {
            Console.WriteLine($"Shader source not found: {sourcePath}");
            return IntPtr.Zero;
        }

        var hlslSource = File.ReadAllText(sourcePath);

        // Compile HLSL to SPIRV using ShaderCross
        var hlslInfo = new ShaderCross.HLSLInfo
        {
            ManagedSource = hlslSource,
            ManagedEntrypoint = "main",
            ShaderStage = (ShaderCross.ShaderStage)stage,
            Props = 0
        };

        var spirvBuffer = ShaderCross.CompileSPIRVFromHLSL(ref hlslInfo, out var spirvSize);
        if (spirvBuffer == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to compile HLSL to SPIRV: {SDL.GetError()}");
            return IntPtr.Zero;
        }

        // Create GPU shader from SPIRV using ShaderCross
        var spirvInfo = new ShaderCross.SPIRVInfo
        {
            ByteCode = spirvBuffer,
            ByteCodeSize = spirvSize,
            ShaderStage = (ShaderCross.ShaderStage)stage,
            ManagedEntrypoint = "main"
        };

        var resourceInfo = new ShaderCross.GraphicsShaderResourceInfo
        {
            NumSamplers = samplerCount,
            NumStorageTextures = storageTextureCount,
            NumStorageBuffers = storageBufferCount,
            NumUniformBuffers = uniformBufferCount
        };

        var shader = ShaderCross.CompileGraphicsShaderFromSPIRV(device, ref spirvInfo, ref resourceInfo, 0);
        SDL.Free(spirvBuffer);

        if (shader == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create GPU shader: {SDL.GetError()}");
        }

        return shader;
    }
}
