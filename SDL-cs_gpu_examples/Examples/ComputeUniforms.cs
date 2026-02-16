using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class ComputeUniforms
{
    [StructLayout(LayoutKind.Sequential)]
    private struct GradientUniforms
    {
        public float Time;
    }

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
        var window = SDL.CreateWindow("ComputeUniforms", 640, 480, 0);
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

        // Create the compute pipeline
        var computeMetadata = new ShaderCross.ComputePipelineMetadata
        {
            NumSamplers = 0,
            NumReadOnlyStorageTextures = 0,
            NumReadOnlyStorageBuffers = 0,
            NumReadWriteStorageTextures = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 8,
            ThreadCountY = 8,
            ThreadCountZ = 1,
        };
        var gradientPipeline = Common.CreateComputePipelineFromShader(device, "GradientTexture.comp", computeMetadata);
        if (gradientPipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create compute pipeline!");
            return -1;
        }

        // Get window size
        SDL.GetWindowSizeInPixels(window, out int w, out int h);

        // Create texture for compute output
        var textureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Width = (uint)w,
            Height = (uint)h,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.Sampler | SDL.GPUTextureUsageFlags.ComputeStorageWrite
        };
        var gradientRenderTexture = SDL.CreateGPUTexture(device, in textureCreateInfo);

        var uniformValues = new GradientUniforms { Time = 0 };

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

            // Update
            uniformValues.Time += 0.01f;

            // Draw
            var commandBuffer = SDL.AcquireGPUCommandBuffer(device);
            if (commandBuffer == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to acquire command buffer: {SDL.GetError()}");
                continue;
            }

            if (!SDL.WaitAndAcquireGPUSwapchainTexture(commandBuffer, window, out var swapchainTexture, out var swW, out var swH))
            {
                Console.WriteLine($"Failed to acquire swapchain texture: {SDL.GetError()}");
                continue;
            }

            if (swapchainTexture != IntPtr.Zero)
            {
                // Compute pass
                SDL.GPUStorageTextureReadWriteBinding[] storageTexBindings =
                [
                    new() { Texture = gradientRenderTexture, Cycle = 1 }
                ];
                var computePass = SDL.BeginGPUComputePass(commandBuffer, storageTexBindings, 1, [], 0);

                SDL.BindGPUComputePipeline(computePass, gradientPipeline);

                unsafe
                {
                    SDL.PushGPUComputeUniformData(commandBuffer, 0, (IntPtr)(&uniformValues), (uint)Marshal.SizeOf<GradientUniforms>());
                }

                SDL.DispatchGPUCompute(computePass, swW / 8, swH / 8, 1);
                SDL.EndGPUComputePass(computePass);

                // Blit compute result to swapchain
                var blitInfo = new SDL.GPUBlitInfo
                {
                    Source = new SDL.GPUBlitRegion
                    {
                        Texture = gradientRenderTexture,
                        W = swW,
                        H = swH
                    },
                    Destination = new SDL.GPUBlitRegion
                    {
                        Texture = swapchainTexture,
                        W = swW,
                        H = swH
                    },
                    LoadOp = SDL.GPULoadOp.DontCare,
                    Filter = SDL.GPUFilter.Linear
                };
                SDL.BlitGPUTexture(commandBuffer, in blitInfo);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUComputePipeline(device, gradientPipeline);
        SDL.ReleaseGPUTexture(device, gradientRenderTexture);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
