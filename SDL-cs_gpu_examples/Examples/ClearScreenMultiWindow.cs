using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class ClearScreenMultiWindow
{
    public static int Main()
    {
        // Initialize SDL with video subsystem
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            Console.WriteLine($"Failed to initialize SDL: {SDL.GetError()}");
            return -1;
        }

        // Create GPU device
        var device = SDL.CreateGPUDevice(SDL.GPUShaderFormat.SPIRV, false, null);
        if (device == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create GPU device: {SDL.GetError()}");
            return -1;
        }

        // Create first window
        var window1 = SDL.CreateWindow("ClearScreenMultiWindow (1)", 640, 480, 0);
        if (window1 == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create window 1: {SDL.GetError()}");
            return -1;
        }

        // Claim first window for GPU device
        if (!SDL.ClaimWindowForGPUDevice(device, window1))
        {
            Console.WriteLine($"Failed to claim window 1 for GPU device: {SDL.GetError()}");
            return -1;
        }

        // Create second window
        var window2 = SDL.CreateWindow("ClearScreenMultiWindow (2)", 640, 480, 0);
        if (window2 == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create window 2: {SDL.GetError()}");
            return -1;
        }

        // Claim second window for GPU device
        if (!SDL.ClaimWindowForGPUDevice(device, window2))
        {
            Console.WriteLine($"Failed to claim window 2 for GPU device: {SDL.GetError()}");
            return -1;
        }

        // Main loop
        var running = true;
        while (running)
        {
            // Process events
            while (SDL.PollEvent(out var evt))
            {
                switch ((SDL.EventType)evt.Type)
                {
                    case SDL.EventType.Quit:
                        running = false;
                        break;
                    case SDL.EventType.WindowCloseRequested:
                        running = false;
                        break;
                }
            }

            // Draw - both windows in a single command buffer
            var commandBuffer = SDL.AcquireGPUCommandBuffer(device);
            if (commandBuffer == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to acquire command buffer: {SDL.GetError()}");
                continue;
            }

            // Render to first window (bluish gray)
            if (!SDL.WaitAndAcquireGPUSwapchainTexture(commandBuffer, window1, out var swapchainTexture1, out _, out _))
            {
                Console.WriteLine($"Failed to acquire swapchain texture for window 1: {SDL.GetError()}");
                continue;
            }

            if (swapchainTexture1 != IntPtr.Zero)
            {
                var colorTargetInfo = new SDL.GPUColorTargetInfo
                {
                    Texture = swapchainTexture1,
                    ClearColor = new SDL.FColor { R = 0.3f, G = 0.4f, B = 0.5f, A = 1.0f },
                    LoadOp = SDL.GPULoadOp.Clear,
                    StoreOp = SDL.GPUStoreOp.Store
                };

                var ptr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                var renderPass = SDL.BeginGPURenderPass(commandBuffer, ptr, 1, IntPtr.Zero);
                Marshal.FreeHGlobal(ptr);

                SDL.EndGPURenderPass(renderPass);
            }

            // Render to second window (pinkish)
            if (!SDL.WaitAndAcquireGPUSwapchainTexture(commandBuffer, window2, out var swapchainTexture2, out _, out _))
            {
                Console.WriteLine($"Failed to acquire swapchain texture for window 2: {SDL.GetError()}");
                continue;
            }

            if (swapchainTexture2 != IntPtr.Zero)
            {
                var colorTargetInfo = new SDL.GPUColorTargetInfo
                {
                    Texture = swapchainTexture2,
                    ClearColor = new SDL.FColor { R = 1.0f, G = 0.5f, B = 0.6f, A = 1.0f },
                    LoadOp = SDL.GPULoadOp.Clear,
                    StoreOp = SDL.GPUStoreOp.Store
                };

                var ptr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                var renderPass = SDL.BeginGPURenderPass(commandBuffer, ptr, 1, IntPtr.Zero);
                Marshal.FreeHGlobal(ptr);

                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseWindowFromGPUDevice(device, window2);
        SDL.DestroyWindow(window2);
        SDL.ReleaseWindowFromGPUDevice(device, window1);
        SDL.DestroyWindow(window1);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
