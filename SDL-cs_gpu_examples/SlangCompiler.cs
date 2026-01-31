using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SDL_cs_gpu_examples;

public enum ShaderStage
{
    Vertex,
    Fragment,
    Compute
}

public enum ShaderFormat
{
    SPIRV,
    DXIL,
    MSL
}

public static class SlangCompiler
{
    private static bool _initialized = false;
    private static string? _tempDir;
    private static string? _slangcPath;
    private static string? _dxcPath;

    public static bool Init()
    {
        if (_initialized) return true;

        try
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "SDL_GPU_Shaders_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempDir);

            // Find slangc.exe in the NuGet packages
            var nugetPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

            var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-ARM64" : "win-x64";
            _slangcPath = Path.Combine(nugetPath, "slang.sdk", "0.5.1", "runtimes", arch, "native", "slangc.exe");

            if (!File.Exists(_slangcPath))
            {
                Console.WriteLine($"slangc.exe not found at: {_slangcPath}");
                return false;
            }

            // Find DXC DLLs
            _dxcPath = Path.Combine(nugetPath, "microsoft.direct3d.dxc", "1.8.2505.32", "build", "native", "bin", "x64");
            if (!Directory.Exists(_dxcPath))
            {
                Console.WriteLine($"DXC not found at: {_dxcPath}");
                // Not fatal - SPIRV will still work
                _dxcPath = null;
            }

            _initialized = true;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize SlangCompiler: {ex.Message}");
            return false;
        }
    }

    public static void Quit()
    {
        if (_initialized && _tempDir != null)
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
            _initialized = false;
            _tempDir = null;
        }
    }

    public static ShaderFormat GetPreferredFormat()
    {
        // On Windows with DXC available, use DXIL for D3D12
        // Otherwise fall back to SPIRV for Vulkan
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _dxcPath != null)
        {
            return ShaderFormat.DXIL;
        }
        else
        {
            return ShaderFormat.SPIRV;
        }
    }

    public static byte[]? Compile(string source, string entryPoint, ShaderStage stage, ShaderFormat format)
    {
        if (!_initialized || _tempDir == null || _slangcPath == null)
        {
            Console.WriteLine("SlangCompiler not initialized");
            return null;
        }

        try
        {
            // Create unique filenames for this compilation
            var uniqueId = Guid.NewGuid().ToString("N")[..8];
            var inputPath = Path.Combine(_tempDir, $"shader_{uniqueId}.slang");

            // Determine output extension based on format
            var outputExt = format switch
            {
                ShaderFormat.SPIRV => ".spv",
                ShaderFormat.DXIL => ".dxil",
                ShaderFormat.MSL => ".metal",
                _ => ".bin"
            };
            var outputPath = Path.Combine(_tempDir, $"shader_{uniqueId}{outputExt}");

            // Write shader source to temp file
            File.WriteAllText(inputPath, source);

            // Determine stage string for slangc
            var stageStr = stage switch
            {
                ShaderStage.Vertex => "vertex",
                ShaderStage.Fragment => "fragment",
                ShaderStage.Compute => "compute",
                _ => throw new ArgumentException($"Unknown shader stage: {stage}")
            };

            // Determine target and profile for slangc
            var (target, profile) = format switch
            {
                ShaderFormat.SPIRV => ("spirv", "glsl_450"),
                ShaderFormat.DXIL => ("dxil", "sm_6_0"),
                ShaderFormat.MSL => ("metal", "metal"),
                _ => throw new ArgumentException($"Unknown shader format: {format}")
            };

            // Build slangc arguments
            var args = $"\"{inputPath}\" -target {target} -profile {profile} -stage {stageStr} -entry {entryPoint} -o \"{outputPath}\"";

            // Run slangc.exe with proper PATH for DXC
            var startInfo = new ProcessStartInfo
            {
                FileName = _slangcPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_slangcPath)
            };

            // Add DXC to PATH if available
            if (_dxcPath != null)
            {
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                startInfo.Environment["PATH"] = _dxcPath + ";" + currentPath;
            }

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Console.WriteLine("Failed to start slangc process");
                return null;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // Check for errors
            if (!string.IsNullOrWhiteSpace(stderr) && stderr.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Slang compilation errors:\n{stderr}");
            }

            // Read compiled bytecode
            if (!File.Exists(outputPath))
            {
                Console.WriteLine($"Slang compilation failed - no output file generated.");
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    Console.WriteLine(stderr);
                }
                return null;
            }

            var bytecode = File.ReadAllBytes(outputPath);

            // Clean up temp files
            try
            {
                File.Delete(inputPath);
                File.Delete(outputPath);
            }
            catch
            {
                // Ignore cleanup errors
            }

            return bytecode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Slang compilation exception: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return null;
        }
    }

    public static byte[]? CompileToSpirv(string source, string entryPoint, ShaderStage stage)
    {
        return Compile(source, entryPoint, stage, ShaderFormat.SPIRV);
    }

    public static byte[]? CompileToDxil(string source, string entryPoint, ShaderStage stage)
    {
        return Compile(source, entryPoint, stage, ShaderFormat.DXIL);
    }

    public static byte[]? CompileToMsl(string source, string entryPoint, ShaderStage stage)
    {
        return Compile(source, entryPoint, stage, ShaderFormat.MSL);
    }
}
