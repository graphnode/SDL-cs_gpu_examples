using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class ComputeSampler
{
    private static readonly string[] SamplerNames =
    [
        "PointClamp",
        "PointWrap",
        "LinearClamp",
        "LinearWrap",
        "AnisotropicClamp",
        "AnisotropicWrap"
    ];

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
        var window = SDL.CreateWindow("ComputeSampler", 640, 480, 0);
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

        // Load the image
        var surface = Common.LoadImage(device, "ravioli.bmp", out int imgW, out int imgH);
        if (surface == IntPtr.Zero)
        {
            Console.WriteLine("Could not load image data!");
            return -1;
        }

        // Create source texture (sampler)
        var textureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Width = (uint)imgW,
            Height = (uint)imgH,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.Sampler
        };
        var texture = SDL.CreateGPUTexture(device, in textureCreateInfo);

        // Create write texture for compute output
        var writeTextureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Width = 640,
            Height = 480,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.Sampler | SDL.GPUTextureUsageFlags.ComputeStorageWrite
        };
        var writeTexture = SDL.CreateGPUTexture(device, in writeTextureCreateInfo);

        // Create compute pipeline
        var computeMetadata = new ShaderCross.ComputePipelineMetadata
        {
            NumSamplers = 1,
            NumReadOnlyStorageTextures = 0,
            NumReadOnlyStorageBuffers = 0,
            NumReadWriteStorageTextures = 1,
            NumUniformBuffers = 1,
            ThreadCountX = 8,
            ThreadCountY = 8,
            ThreadCountZ = 1,
        };
        var pipeline = Common.CreateComputePipelineFromShader(device, "TexturedQuad.comp", computeMetadata);
        if (pipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create compute pipeline!");
            return -1;
        }

        // Create samplers
        var samplers = new IntPtr[6];

        // PointClamp
        var samplerInfo0 = new SDL.GPUSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Nearest,
            MagFilter = SDL.GPUFilter.Nearest,
            MipmapMode = SDL.GPUSamplerMipmapMode.Nearest,
            AddressModeU = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeV = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeW = SDL.GPUSamplerAddressMode.ClampToEdge,
        };
        samplers[0] = SDL.CreateGPUSampler(device, in samplerInfo0);

        // PointWrap
        var samplerInfo1 = new SDL.GPUSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Nearest,
            MagFilter = SDL.GPUFilter.Nearest,
            MipmapMode = SDL.GPUSamplerMipmapMode.Nearest,
            AddressModeU = SDL.GPUSamplerAddressMode.Repeat,
            AddressModeV = SDL.GPUSamplerAddressMode.Repeat,
            AddressModeW = SDL.GPUSamplerAddressMode.Repeat,
        };
        samplers[1] = SDL.CreateGPUSampler(device, in samplerInfo1);

        // LinearClamp
        var samplerInfo2 = new SDL.GPUSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Linear,
            MagFilter = SDL.GPUFilter.Linear,
            MipmapMode = SDL.GPUSamplerMipmapMode.Linear,
            AddressModeU = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeV = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeW = SDL.GPUSamplerAddressMode.ClampToEdge,
        };
        samplers[2] = SDL.CreateGPUSampler(device, in samplerInfo2);

        // LinearWrap
        var samplerInfo3 = new SDL.GPUSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Linear,
            MagFilter = SDL.GPUFilter.Linear,
            MipmapMode = SDL.GPUSamplerMipmapMode.Linear,
            AddressModeU = SDL.GPUSamplerAddressMode.Repeat,
            AddressModeV = SDL.GPUSamplerAddressMode.Repeat,
            AddressModeW = SDL.GPUSamplerAddressMode.Repeat,
        };
        samplers[3] = SDL.CreateGPUSampler(device, in samplerInfo3);

        // AnisotropicClamp
        var samplerInfo4 = new SDL.GPUSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Linear,
            MagFilter = SDL.GPUFilter.Linear,
            MipmapMode = SDL.GPUSamplerMipmapMode.Linear,
            AddressModeU = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeV = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeW = SDL.GPUSamplerAddressMode.ClampToEdge,
            EnableAnisotropy = true,
            MaxAnisotropy = 4
        };
        samplers[4] = SDL.CreateGPUSampler(device, in samplerInfo4);

        // AnisotropicWrap
        var samplerInfo5 = new SDL.GPUSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Linear,
            MagFilter = SDL.GPUFilter.Linear,
            MipmapMode = SDL.GPUSamplerMipmapMode.Linear,
            AddressModeU = SDL.GPUSamplerAddressMode.Repeat,
            AddressModeV = SDL.GPUSamplerAddressMode.Repeat,
            AddressModeW = SDL.GPUSamplerAddressMode.Repeat,
            EnableAnisotropy = true,
            MaxAnisotropy = 4
        };
        samplers[5] = SDL.CreateGPUSampler(device, in samplerInfo5);

        // Upload image data to texture
        Common.UploadTextureFromSurface(device, texture, surface, imgW, imgH);
        SDL.DestroySurface(surface);

        int currentSamplerIndex = 0;

        Console.WriteLine("Press Left/Right to switch between sampler states");
        Console.WriteLine($"Setting sampler state to: {SamplerNames[0]}");

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
                        if (evt.Key.Scancode == SDL.Scancode.Left)
                        {
                            currentSamplerIndex--;
                            if (currentSamplerIndex < 0)
                                currentSamplerIndex = samplers.Length - 1;
                            Console.WriteLine($"Setting sampler state to: {SamplerNames[currentSamplerIndex]}");
                        }
                        else if (evt.Key.Scancode == SDL.Scancode.Right)
                        {
                            currentSamplerIndex = (currentSamplerIndex + 1) % samplers.Length;
                            Console.WriteLine($"Setting sampler state to: {SamplerNames[currentSamplerIndex]}");
                        }
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
                    new() { Texture = writeTexture, MipLevel = 0, Layer = 0, Cycle = 1 }
                ];
                var computePass = SDL.BeginGPUComputePass(commandBuffer, storageTexBindings, 1, [], 0);

                SDL.BindGPUComputePipeline(computePass, pipeline);

                // Bind sampler
                var samplerBinding = new SDL.GPUTextureSamplerBinding
                {
                    Texture = texture,
                    Sampler = samplers[currentSamplerIndex]
                };
                var samplerBindingPtr = SDL.StructureToPointer<SDL.GPUTextureSamplerBinding>(samplerBinding);
                SDL.BindGPUComputeSamplers(computePass, 0, samplerBindingPtr, 1);
                Marshal.FreeHGlobal(samplerBindingPtr);

                // Push uniform data
                float texcoordMultiplier = 0.25f;
                unsafe
                {
                    SDL.PushGPUComputeUniformData(commandBuffer, 0, (IntPtr)(&texcoordMultiplier), sizeof(float));
                }

                SDL.DispatchGPUCompute(computePass, swW / 8, swH / 8, 1);
                SDL.EndGPUComputePass(computePass);

                // Blit compute result to swapchain
                var blitInfo = new SDL.GPUBlitInfo
                {
                    Source = new SDL.GPUBlitRegion
                    {
                        Texture = writeTexture,
                        W = 640,
                        H = 480
                    },
                    Destination = new SDL.GPUBlitRegion
                    {
                        Texture = swapchainTexture,
                        W = swW,
                        H = swH
                    },
                    LoadOp = SDL.GPULoadOp.DontCare,
                    Filter = SDL.GPUFilter.Nearest
                };
                SDL.BlitGPUTexture(commandBuffer, in blitInfo);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUComputePipeline(device, pipeline);
        SDL.ReleaseGPUTexture(device, texture);
        SDL.ReleaseGPUTexture(device, writeTexture);

        for (int i = 0; i < samplers.Length; i++)
        {
            SDL.ReleaseGPUSampler(device, samplers[i]);
        }

        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
