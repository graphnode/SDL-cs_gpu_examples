using SDL3;

namespace SDL_cs_gpu_examples;

public static class ClearScreen
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

        // Create window                                                                            
        var window = SDL.CreateWindow("SDL GPU ClearScreen", 640, 480, SDL.WindowFlags.Resizable);
        if (window == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to create window: {SDL.GetError()}");
            return -1;
        }

        // Claim window for GPU device                                                              
        if (!SDL.ClaimWindowForGPUDevice(device, window))
        {
            Console.WriteLine($"Failed to claim window for GPU device: {SDL.GetError()}");
            return -1;
        }

        // Main loop                                                                                
        var running = true;
        while (running)
        {
            // Process events                                                                       
            while (SDL.PollEvent(out var evt))
            {
                if (evt.Type == (uint)SDL.EventType.Quit)
                {
                    running = false;
                }
            }

            // Draw                                                                                 
            var commandBuffer = SDL.AcquireGPUCommandBuffer(device);
            if (commandBuffer == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to acquire command buffer: {SDL.GetError()}");
                continue;
            }

            if (!SDL.WaitAndAcquireGPUSwapchainTexture(commandBuffer, window, out var
                    swapchainTexture, out _, out _))
            {
                Console.WriteLine($"Failed to acquire swapchain texture: {SDL.GetError()}");
                continue;
            }

            if (swapchainTexture != IntPtr.Zero)
            {
                var colorTargetInfo = new SDL.GPUColorTargetInfo
                {
                    Texture = swapchainTexture,
                    ClearColor = new SDL.FColor { R = 0.3f, G = 0.4f, B = 0.5f, A = 1.0f },
                    LoadOp = SDL.GPULoadOp.Clear,
                    StoreOp = SDL.GPUStoreOp.Store
                };

                var ptr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                var renderPass = SDL.BeginGPURenderPass(commandBuffer, ptr, 1, IntPtr.Zero);
                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup                                                                                  
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}