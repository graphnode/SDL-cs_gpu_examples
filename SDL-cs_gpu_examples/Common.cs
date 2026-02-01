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

    private static string GetCompiledShaderSubdir(ShaderFormat format)
    {
        return format switch
        {
            ShaderFormat.SPIRV => "SPIRV",
            ShaderFormat.DXIL => "DXIL",
            ShaderFormat.MSL => "MSL",
            _ => "SPIRV"
        };
    }

    private static string GetCompiledShaderExtension(ShaderFormat format)
    {
        return format switch
        {
            ShaderFormat.SPIRV => ".spv",
            ShaderFormat.DXIL => ".dxil",
            ShaderFormat.MSL => ".metal",
            _ => ".spv"
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

        // Get the preferred shader format for this platform
        var format = SlangCompiler.GetPreferredFormat();

        // Try to load precompiled shader first
        string compiledDir = GetCompiledShaderSubdir(format);
        string compiledExt = GetCompiledShaderExtension(format);
        string compiledPath = Path.Combine(BasePath, "Content", "Shaders", "Compiled", compiledDir, $"{shaderFilename}{compiledExt}");

        bool usePrecompiled = File.Exists(compiledPath);
        
        byte[]? bytecode;
        
        // Find source file path in output directory and fallback to project directory
        string sourcePath = Path.Combine(BasePath, "Content", "Shaders", "Source", $"{shaderFilename}.slang");
        if (!File.Exists(sourcePath))
            sourcePath = Path.Combine(BasePath, "Content", "Shaders", "Source", $"{shaderFilename}.hlsl");    
        
#if DEBUG
        // In debug mode, recompile if source is newer than compiled
        if (usePrecompiled && File.Exists(sourcePath))
        {
            var sourceTime = File.GetLastWriteTimeUtc(sourcePath);
            var compiledTime = File.GetLastWriteTimeUtc(compiledPath);
            if (sourceTime > compiledTime)
            {
                Console.WriteLine($"Shader source modified, recompiling: {shaderFilename}");
                usePrecompiled = false;
            }
        }
#endif

        if (usePrecompiled)
        {
            // Use precompiled shader
            bytecode = File.ReadAllBytes(compiledPath);
        }
        else
        {
            // Fall back to runtime compilation
            if (!SlangCompiler.Init())
            {
                return IntPtr.Zero;
            }

            if (!File.Exists(sourcePath))
            {
                Console.WriteLine($"Shader not found (precompiled: {compiledPath}, source: {sourcePath})");
                return IntPtr.Zero;
            }

            var shaderSource = File.ReadAllText(sourcePath);
            bytecode = SlangCompiler.Compile(shaderSource, "main", stage, format);

#if DEBUG
            // Save recompiled shader for next run
            if (bytecode != null)
            {
                try
                {
                    var outputDir = Path.GetDirectoryName(compiledPath);
                    if (outputDir != null && !Directory.Exists(outputDir))
                    {
                        Directory.CreateDirectory(outputDir);
                    }
                    File.WriteAllBytes(compiledPath, bytecode);
                }
                catch
                {
                    // Ignore save errors
                }
            }
#endif
        }

        if (bytecode == null)
        {
            Console.WriteLine($"Failed to load shader: {shaderFilename}");
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
