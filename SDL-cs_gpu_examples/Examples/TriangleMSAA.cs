using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class TriangleMSAA
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

        var window = SDL.CreateWindow("TriangleMSAA", 640, 480, 0);
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

        // Load shaders
        var vertexShader = Common.LoadShader(device, "RawTriangle.vert");
        if (vertexShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to load vertex shader!");
            return -1;
        }

        var fragmentShader = Common.LoadShader(device, "SolidColor.frag");
        if (fragmentShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to load fragment shader!");
            return -1;
        }

        var rtFormat = SDL.GetGPUSwapchainTextureFormat(device, window);
        var colorTargetDesc = new SDL.GPUColorTargetDescription { Format = rtFormat };
        var colorTargetDescPtr = SDL.StructureToPointer<SDL.GPUColorTargetDescription>(colorTargetDesc);

        // Check which sample counts are supported and create pipelines/textures for each
        var sampleCountValues = new SDL.GPUSampleCount[]
        {
            SDL.GPUSampleCount.SampleCount1,
            SDL.GPUSampleCount.SampleCount2,
            SDL.GPUSampleCount.SampleCount4,
            SDL.GPUSampleCount.SampleCount8
        };

        var pipelines = new IntPtr[4];
        var msaaRenderTextures = new IntPtr[4];
        int supportedCount = 0;

        for (int i = 0; i < sampleCountValues.Length; i++)
        {
            var sampleCount = sampleCountValues[i];

            if (!SDL.GPUTextureSupportsSampleCount(device, rtFormat, sampleCount))
            {
                Console.WriteLine($"Sample count {(1 << i)} not supported");
                continue;
            }

            var pipelineCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
            {
                VertexShader = vertexShader,
                FragmentShader = fragmentShader,
                PrimitiveType = SDL.GPUPrimitiveType.TriangleList,
                MultisampleState = new SDL.GPUMultisampleState
                {
                    SampleCount = sampleCount
                },
                TargetInfo = new SDL.GPUGraphicsPipelineTargetInfo
                {
                    ColorTargetDescriptions = colorTargetDescPtr,
                    NumColorTargets = 1
                }
            };

            pipelines[supportedCount] = SDL.CreateGPUGraphicsPipeline(device, in pipelineCreateInfo);
            if (pipelines[supportedCount] == IntPtr.Zero)
            {
                Console.WriteLine("Failed to create pipeline!");
                return -1;
            }

            // Create MSAA render target texture
            var textureUsage = SDL.GPUTextureUsageFlags.ColorTarget;
            if (sampleCount == SDL.GPUSampleCount.SampleCount1)
                textureUsage |= SDL.GPUTextureUsageFlags.Sampler;

            var textureCreateInfo = new SDL.GPUTextureCreateInfo
            {
                Type = SDL.GPUTextureType.TextureType2D,
                Width = 640,
                Height = 480,
                LayerCountOrDepth = 1,
                NumLevels = 1,
                Format = rtFormat,
                Usage = textureUsage,
                SampleCount = sampleCount
            };

            msaaRenderTextures[supportedCount] = SDL.CreateGPUTexture(device, in textureCreateInfo);
            if (msaaRenderTextures[supportedCount] == IntPtr.Zero)
            {
                Console.WriteLine("Failed to create MSAA render target texture!");
                SDL.ReleaseGPUGraphicsPipeline(device, pipelines[supportedCount]);
                continue;
            }

            supportedCount++;
        }

        Marshal.FreeHGlobal(colorTargetDescPtr);

        // Create resolve texture
        var resolveTextureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Width = 640,
            Height = 480,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            Format = rtFormat,
            Usage = SDL.GPUTextureUsageFlags.ColorTarget | SDL.GPUTextureUsageFlags.Sampler
        };
        var resolveTexture = SDL.CreateGPUTexture(device, in resolveTextureCreateInfo);

        SDL.ReleaseGPUShader(device, vertexShader);
        SDL.ReleaseGPUShader(device, fragmentShader);

        Console.WriteLine("Press Left/Right to cycle between sample counts");
        Console.WriteLine($"Current sample count: {(1 << 0)}");

        int currentSampleCount = 0;

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
                    case SDL.EventType.KeyDown:
                        var scancode = evt.Key.Scancode;
                        bool changed = false;

                        if (scancode == SDL.Scancode.Left)
                        {
                            currentSampleCount -= 1;
                            if (currentSampleCount < 0)
                                currentSampleCount = supportedCount - 1;
                            changed = true;
                        }
                        else if (scancode == SDL.Scancode.Right)
                        {
                            currentSampleCount = (currentSampleCount + 1) % supportedCount;
                            changed = true;
                        }

                        if (changed)
                        {
                            Console.WriteLine($"Current sample count: {(1 << currentSampleCount)}");
                        }
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
                var colorTargetInfo = new SDL.GPUColorTargetInfo
                {
                    Texture = msaaRenderTextures[currentSampleCount],
                    ClearColor = new SDL.FColor { R = 1.0f, G = 1.0f, B = 1.0f, A = 1.0f },
                    LoadOp = SDL.GPULoadOp.Clear,
                };

                if (currentSampleCount == 0) // SDL_GPU_SAMPLECOUNT_1
                {
                    colorTargetInfo.StoreOp = SDL.GPUStoreOp.Store;
                }
                else
                {
                    colorTargetInfo.StoreOp = SDL.GPUStoreOp.Resolve;
                    colorTargetInfo.ResolveTexture = resolveTexture;
                }

                var colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                var renderPass = SDL.BeginGPURenderPass(commandBuffer, colorTargetPtr, 1, IntPtr.Zero);
                Marshal.FreeHGlobal(colorTargetPtr);

                SDL.BindGPUGraphicsPipeline(renderPass, pipelines[currentSampleCount]);
                SDL.DrawGPUPrimitives(renderPass, 3, 1, 0, 0);
                SDL.EndGPURenderPass(renderPass);

                // Blit the result to the swapchain
                var blitSourceTexture = (colorTargetInfo.ResolveTexture != IntPtr.Zero)
                    ? colorTargetInfo.ResolveTexture
                    : colorTargetInfo.Texture;

                var blitInfo = new SDL.GPUBlitInfo
                {
                    Source = new SDL.GPUBlitRegion
                    {
                        Texture = blitSourceTexture,
                        X = 160,
                        W = 320,
                        H = 240
                    },
                    Destination = new SDL.GPUBlitRegion
                    {
                        Texture = swapchainTexture,
                        W = w,
                        H = h
                    },
                    LoadOp = SDL.GPULoadOp.DontCare,
                    Filter = SDL.GPUFilter.Linear
                };

                SDL.BlitGPUTexture(commandBuffer, in blitInfo);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        for (int i = 0; i < supportedCount; i++)
        {
            SDL.ReleaseGPUGraphicsPipeline(device, pipelines[i]);
            SDL.ReleaseGPUTexture(device, msaaRenderTextures[i]);
        }
        SDL.ReleaseGPUTexture(device, resolveTexture);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
