using SDL3;

namespace SDL_cs_gpu_examples;

#if DEBUG
/// <summary>
/// Watches shader source files and automatically reloads them when they change.
/// Only active in DEBUG builds for the development workflow.
/// </summary>
public static class ShaderHotReloader
{
    private class TrackedShader
    {
        public required string SourcePath { get; init; }
        public required IntPtr Device { get; init; }
        public required Action<IntPtr> OnReloaded { get; init; }
        public IntPtr CurrentHandle { get; set; }
        public DateTime LastModified { get; set; }
    }

    private static readonly Dictionary<string, TrackedShader> _trackedShaders = new();
    private static FileSystemWatcher? _watcher;
    private static readonly HashSet<string> _pendingReloads = [];
    private static bool _initialized;

    /// <summary>
    /// Initialize the hot reloader. Call this once at application startup.
    /// </summary>
    public static void Init()
    {
        if (_initialized) return;

        var shaderSourceDir = Path.Combine(Common.BasePath, "Content", "Shaders", "Source");
        if (!Directory.Exists(shaderSourceDir))
        {
            Console.WriteLine($"Shader source directory not found at {shaderSourceDir}, hot-reload disabled");
            return;
        }

        Console.WriteLine($"Watching for shader changes in: {shaderSourceDir}");

        _watcher = new FileSystemWatcher(shaderSourceDir)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            Filter = "*.hlsl",
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnShaderFileChanged;
        _watcher.Created += OnShaderFileChanged;
        _watcher.Renamed += OnShaderFileChanged;

        _initialized = true;
        Console.WriteLine("Shader hot-reload enabled");
    }

    /// <summary>
    /// Track a shader for hot-reloading. Call this after loading a shader.
    /// </summary>
    /// <param name="shaderHandle">Current shader handle</param>
    /// <param name="shaderFilename">Shader filename (e.g., "Triangle.vert")</param>
    /// <param name="device">GPU device</param>
    /// <param name="onReloaded">Callback invoked with new shader handle when reloaded</param>
    public static void Track(IntPtr shaderHandle, string shaderFilename, IntPtr device, Action<IntPtr> onReloaded)
    {
        if (!_initialized)
            return;

        var sourcePath = Path.Combine(Common.BasePath, "Content", "Shaders", "Source", $"{shaderFilename}.slang");
        if (!File.Exists(sourcePath))
            sourcePath = Path.Combine(Common.BasePath, "Content", "Shaders", "Source", $"{shaderFilename}.hlsl");

        if (!File.Exists(sourcePath))
        {
            Console.WriteLine($"Cannot track shader {shaderFilename}: source file not found");
            return;
        }

        var tracked = new TrackedShader
        {
            SourcePath = sourcePath,
            Device = device,
            OnReloaded = onReloaded,
            CurrentHandle = shaderHandle,
            LastModified = File.GetLastWriteTimeUtc(sourcePath)
        };

        _trackedShaders[shaderFilename] = tracked;
    }

    /// <summary>
    /// Check for pending shader reloads and apply them. Call this once per frame.
    /// </summary>
    public static void CheckAndReload()
    {
        lock (_pendingReloads)
        {
            if (!_initialized || _pendingReloads.Count == 0)
                return;

            foreach (string filename in _pendingReloads)
            {
                ReloadShader(filename);
            }
            
            _pendingReloads.Clear();
        }
    }

    private static void OnShaderFileChanged(object sender, FileSystemEventArgs e)
    {
        if (e.Name == null)
            return;

        // Extract shader filename without extension
        var filename = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(e.Name));
        if (string.IsNullOrEmpty(filename)) return;

        // Find all tracked shaders that might match this source file
        foreach (var (key, shader) in _trackedShaders)
        {
            if (shader.SourcePath.Contains(filename))
            {
                lock (_pendingReloads)
                {
                    _pendingReloads.Add(key);
                }
            }
        }
    }

    private static void ReloadShader(string shaderFilename)
    {
        if (!_trackedShaders.TryGetValue(shaderFilename, out var shader))
            return;

        // Check if file actually changed
        var currentModified = File.GetLastWriteTimeUtc(shader.SourcePath);
        if (currentModified <= shader.LastModified)
            return;

        Console.WriteLine($"Hot-reloading shader: {shaderFilename}");

        // Load the new shader
        var newHandle = Common.LoadShader(shader.Device, shaderFilename);
        if (newHandle == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to reload shader: {shaderFilename}");
            return;
        }

        // Release old shader
        if (shader.CurrentHandle != IntPtr.Zero)
        {
            SDL.ReleaseGPUShader(shader.Device, shader.CurrentHandle);
        }

        // Update tracking info
        shader.CurrentHandle = newHandle;
        shader.LastModified = currentModified;

        // Notify caller of new handle
        shader.OnReloaded(newHandle);

        Console.WriteLine($"Shader reloaded successfully: {shaderFilename}");
    }

    /// <summary>
    /// Cleanup resources. Call this at application shutdown.
    /// </summary>
    public static void Quit()
    {
        _watcher?.Dispose();
        _watcher = null;
        _trackedShaders.Clear();
        lock (_pendingReloads)
        {
            _pendingReloads.Clear();
        }
        _initialized = false;
    }
}
#else
// No-op implementation for Release builds
public static class ShaderHotReloader
{
    public static void Init() { }
    public static void Track(IntPtr shaderHandle, string shaderFilename, IntPtr device, Action<IntPtr> onReloaded) { }
    public static void CheckAndReload() { }
    public static void Quit() { }
}
#endif
