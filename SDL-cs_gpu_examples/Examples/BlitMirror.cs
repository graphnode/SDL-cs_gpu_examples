using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class BlitMirror
{
    public static int Main()
    {
        // Initialize SDL
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            Console.WriteLine($"Failed to initialize SDL: {SDL.GetError()}");
            return -1;
        }

        // Create GPU device
        var device = SDL.CreateGPUDevice(
            SDL.GPUShaderFormat.SPIRV | SDL.GPUShaderFormat.DXIL | SDL.GPUShaderFormat.MSL,
            true,
            null);
        if (device == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create GPU device: {SDL.GetError()}");
            return -1;
        }

        // Create window
        var window = SDL.CreateWindow("BlitMirror", 640, 480, 0);
        if (window == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create window: {SDL.GetError()}");
            return -1;
        }

        // Claim window for GPU device
        if (!SDL.ClaimWindowForGPUDevice(device, window))
        {
            Console.WriteLine($"Failed to claim window: {SDL.GetError()}");
            return -1;
        }

        // Load the image and create texture
        var surface = Common.LoadImage(device, "ravioli.bmp", out var imgWidth, out var imgHeight);
        if (surface == IntPtr.Zero)
        {
            Console.WriteLine("Could not load image data!");
            return -1;
        }

        var textureWidth = (uint)imgWidth;
        var textureHeight = (uint)imgHeight;

        // Create texture
        var textureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Width = textureWidth,
            Height = textureHeight,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.Sampler
        };
        var texture = SDL.CreateGPUTexture(device, in textureCreateInfo);

        // Upload texture data
        Common.UploadTextureFromSurface(device, texture, surface, imgWidth, imgHeight);
        SDL.DestroySurface(surface);

        // Main loop
        var running = true;
        while (running)
        {
            while (SDL.PollEvent(out var evt))
            {
                switch ((SDL.EventType)evt.Type)
                {
                    case SDL.EventType.Quit:
                    case SDL.EventType.WindowCloseRequested:
                        running = false;
                        break;
                }
            }

            // Draw
            var commandBuffer = SDL.AcquireGPUCommandBuffer(device);
            if (commandBuffer == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to acquire command buffer: {SDL.GetError()}");
                continue;
            }

            if (!SDL.WaitAndAcquireGPUSwapchainTexture(commandBuffer, window, out var swapchainTexture, out var w, out var h))
            {
                Console.WriteLine($"Failed to acquire swapchain texture: {SDL.GetError()}");
                continue;
            }

            if (swapchainTexture != IntPtr.Zero)
            {
                // Clear the screen first with a render pass
                var colorTargetInfo = new SDL.GPUColorTargetInfo
                {
                    Texture = swapchainTexture,
                    ClearColor = new SDL.FColor { R = 0.0f, G = 0.0f, B = 0.0f, A = 1.0f },
                    LoadOp = SDL.GPULoadOp.Clear,
                    StoreOp = SDL.GPUStoreOp.Store,
                    Cycle = false
                };
                var colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                var clearPass = SDL.BeginGPURenderPass(commandBuffer, colorTargetPtr, 1, IntPtr.Zero);
                Marshal.FreeHGlobal(colorTargetPtr);
                SDL.EndGPURenderPass(clearPass);

                // Normal (top-left)
                var blitInfo = new SDL.GPUBlitInfo
                {
                    Source = new SDL.GPUBlitRegion
                    {
                        Texture = texture,
                        W = textureWidth,
                        H = textureHeight
                    },
                    Destination = new SDL.GPUBlitRegion
                    {
                        Texture = swapchainTexture,
                        W = w / 2,
                        H = h / 2
                    },
                    LoadOp = SDL.GPULoadOp.DontCare,
                    Filter = SDL.GPUFilter.Nearest
                };
                SDL.BlitGPUTexture(commandBuffer, in blitInfo);

                // Flipped Horizontally (top-right)
                blitInfo = new SDL.GPUBlitInfo
                {
                    Source = new SDL.GPUBlitRegion
                    {
                        Texture = texture,
                        W = textureWidth,
                        H = textureHeight
                    },
                    Destination = new SDL.GPUBlitRegion
                    {
                        Texture = swapchainTexture,
                        X = w / 2,
                        W = w / 2,
                        H = h / 2
                    },
                    LoadOp = SDL.GPULoadOp.Load,
                    FlipMode = SDL.FlipMode.Horizontal,
                    Filter = SDL.GPUFilter.Nearest
                };
                SDL.BlitGPUTexture(commandBuffer, in blitInfo);

                // Flipped Vertically (bottom-left)
                blitInfo = new SDL.GPUBlitInfo
                {
                    Source = new SDL.GPUBlitRegion
                    {
                        Texture = texture,
                        W = textureWidth,
                        H = textureHeight
                    },
                    Destination = new SDL.GPUBlitRegion
                    {
                        Texture = swapchainTexture,
                        Y = h / 2,
                        W = w / 2,
                        H = h / 2
                    },
                    LoadOp = SDL.GPULoadOp.Load,
                    FlipMode = SDL.FlipMode.Vertical,
                    Filter = SDL.GPUFilter.Nearest
                };
                SDL.BlitGPUTexture(commandBuffer, in blitInfo);

                // Flipped Horizontally and Vertically (bottom-right)
                blitInfo = new SDL.GPUBlitInfo
                {
                    Source = new SDL.GPUBlitRegion
                    {
                        Texture = texture,
                        W = textureWidth,
                        H = textureHeight
                    },
                    Destination = new SDL.GPUBlitRegion
                    {
                        Texture = swapchainTexture,
                        X = w / 2,
                        Y = h / 2,
                        W = w / 2,
                        H = h / 2
                    },
                    LoadOp = SDL.GPULoadOp.Load,
                    FlipMode = SDL.FlipMode.Horizontal | SDL.FlipMode.Vertical,
                    Filter = SDL.GPUFilter.Nearest
                };
                SDL.BlitGPUTexture(commandBuffer, in blitInfo);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUTexture(device, texture);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
