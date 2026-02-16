using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class Clear3DSlice
{
    public static int Main()
    {
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            Console.WriteLine($"Failed to initialize SDL: {SDL.GetError()}");
            return -1;
        }

        var device = SDL.CreateGPUDevice(
            SDL.GPUShaderFormat.SPIRV | SDL.GPUShaderFormat.DXIL | SDL.GPUShaderFormat.MSL,
            true,
            null);
        if (device == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create GPU device: {SDL.GetError()}");
            return -1;
        }

        var window = SDL.CreateWindow("Clear3DSlice", 640, 480, SDL.WindowFlags.Resizable);
        if (window == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create window: {SDL.GetError()}");
            return -1;
        }

        if (!SDL.ClaimWindowForGPUDevice(device, window))
        {
            Console.WriteLine($"Failed to claim window: {SDL.GetError()}");
            return -1;
        }

        // Create 3D texture
        var swapchainFormat = SDL.GetGPUSwapchainTextureFormat(device, window);
        var texture3DCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType3D,
            Format = swapchainFormat,
            Width = 64,
            Height = 64,
            LayerCountOrDepth = 4,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.ColorTarget | SDL.GPUTextureUsageFlags.Sampler
        };
        var texture3D = SDL.CreateGPUTexture(device, in texture3DCreateInfo);

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
                // Clear each slice of the 3D texture with a different color
                var sliceColors = new SDL.FColor[]
                {
                    new() { R = 1.0f, G = 0.0f, B = 0.0f, A = 1.0f }, // Red
                    new() { R = 0.0f, G = 1.0f, B = 0.0f, A = 1.0f }, // Green
                    new() { R = 0.0f, G = 0.0f, B = 1.0f, A = 1.0f }, // Blue
                    new() { R = 1.0f, G = 0.0f, B = 1.0f, A = 1.0f }  // Magenta
                };

                for (int i = 0; i < 4; i++)
                {
                    var colorTargetInfo = new SDL.GPUColorTargetInfo
                    {
                        Texture = texture3D,
                        Cycle = (i == 0),
                        LoadOp = SDL.GPULoadOp.Clear,
                        StoreOp = SDL.GPUStoreOp.Store,
                        ClearColor = sliceColors[i],
                        LayerOrDepthPlane = (uint)i
                    };

                    var colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                    var renderPass = SDL.BeginGPURenderPass(commandBuffer, colorTargetPtr, 1, IntPtr.Zero);
                    Marshal.FreeHGlobal(colorTargetPtr);

                    SDL.EndGPURenderPass(renderPass);
                }

                // Blit each slice to a quadrant of the swapchain
                for (int i = 0; i < 4; i++)
                {
                    uint destX = (uint)((i % 2) * (w / 2));
                    uint destY = (i > 1) ? (h / 2) : 0;

                    var blitInfo = new SDL.GPUBlitInfo
                    {
                        Source = new SDL.GPUBlitRegion
                        {
                            Texture = texture3D,
                            LayerOrDepthPlane = (uint)i,
                            W = 64,
                            H = 64
                        },
                        Destination = new SDL.GPUBlitRegion
                        {
                            Texture = swapchainTexture,
                            X = destX,
                            Y = destY,
                            W = w / 2,
                            H = h / 2
                        },
                        LoadOp = SDL.GPULoadOp.Load,
                        Filter = SDL.GPUFilter.Nearest
                    };

                    SDL.BlitGPUTexture(commandBuffer, in blitInfo);
                }
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUTexture(device, texture3D);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
