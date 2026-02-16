using System.Numerics;
using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class PullSpriteBatch
{
    private const uint SpriteCount = 8192;

    private static readonly float[] UCoords = [0.0f, 0.5f, 0.0f, 0.5f];
    private static readonly float[] VCoords = [0.0f, 0.0f, 0.5f, 0.5f];

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
        var window = SDL.CreateWindow("PullSpriteBatch", 640, 480, 0);
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

        // Set present mode
        var presentMode = SDL.GPUPresentMode.VSync;
        if (SDL.WindowSupportsGPUPresentMode(device, window, SDL.GPUPresentMode.Immediate))
        {
            presentMode = SDL.GPUPresentMode.Immediate;
        }
        else if (SDL.WindowSupportsGPUPresentMode(device, window, SDL.GPUPresentMode.Mailbox))
        {
            presentMode = SDL.GPUPresentMode.Mailbox;
        }

        SDL.SetGPUSwapchainParameters(device, window, SDL.GPUSwapchainComposition.SDR, presentMode);

        SDL.SRand(0);

        // Create the shaders
        var vertShader = Common.LoadShader(device, "PullSpriteBatch.vert", 0, 1, 1);
        if (vertShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create vertex shader!");
            return -1;
        }

        var fragShader = Common.LoadShader(device, "TexturedQuadColor.frag", 1);
        if (fragShader == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create fragment shader!");
            return -1;
        }

        // Create the sprite render pipeline (no vertex input - pulls from storage buffer)
        var renderPipeline = CreateSpritePipeline(device, window, vertShader, fragShader);
        if (renderPipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create render pipeline!");
            return -1;
        }

        SDL.ReleaseGPUShader(device, vertShader);
        SDL.ReleaseGPUShader(device, fragShader);

        // Load the image data
        var imageData = Common.LoadImage(device, "ravioli_atlas.bmp", out var imgWidth, out var imgHeight);
        if (imageData == IntPtr.Zero)
        {
            Console.WriteLine("Could not load image data!");
            return -1;
        }

        // Create texture transfer buffer and upload
        var textureTransferBuffer = SDL.CreateGPUTransferBuffer(device, new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = (uint)(imgWidth * imgHeight * 4)
        });

        var textureTransferPtr = SDL.MapGPUTransferBuffer(device, textureTransferBuffer, false);
        unsafe
        {
            var surfacePtr = (SDL.Surface*)imageData;
            uint dataSize = (uint)(imgWidth * imgHeight * 4);
            Buffer.MemoryCopy((void*)surfacePtr->Pixels, (void*)textureTransferPtr, dataSize, dataSize);
        }
        SDL.UnmapGPUTransferBuffer(device, textureTransferBuffer);

        // Create the GPU resources
        var texture = SDL.CreateGPUTexture(device, new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Width = (uint)imgWidth,
            Height = (uint)imgHeight,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.Sampler
        });

        var sampler = SDL.CreateGPUSampler(device, new SDL.GPUSamplerCreateInfo
        {
            MinFilter = SDL.GPUFilter.Nearest,
            MagFilter = SDL.GPUFilter.Nearest,
            MipmapMode = SDL.GPUSamplerMipmapMode.Nearest,
            AddressModeU = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeV = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeW = SDL.GPUSamplerAddressMode.ClampToEdge
        });

        var spriteDataTransferBuffer = SDL.CreateGPUTransferBuffer(device, new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = SpriteCount * (uint)Marshal.SizeOf<ComputeSpriteInstance>()
        });

        var spriteDataBuffer = SDL.CreateGPUBuffer(device, new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.GraphicsStorageRead,
            Size = SpriteCount * (uint)Marshal.SizeOf<ComputeSpriteInstance>()
        });

        // Transfer the up-front data (texture)
        var uploadCmdBuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(uploadCmdBuf);

        SDL.UploadToGPUTexture(copyPass,
            new SDL.GPUTextureTransferInfo { TransferBuffer = textureTransferBuffer, Offset = 0 },
            new SDL.GPUTextureRegion { Texture = texture, W = (uint)imgWidth, H = (uint)imgHeight, D = 1 },
            false);

        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(uploadCmdBuf);

        SDL.DestroySurface(imageData);
        SDL.ReleaseGPUTransferBuffer(device, textureTransferBuffer);

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

            var cameraMatrix = Matrix4x4.CreateOrthographicOffCenter(0, 640, 480, 0, 0, -1);

            var cmdBuf = SDL.AcquireGPUCommandBuffer(device);
            if (cmdBuf == IntPtr.Zero)
            {
                Console.WriteLine($"Failed to acquire command buffer: {SDL.GetError()}");
                continue;
            }

            if (!SDL.WaitAndAcquireGPUSwapchainTexture(cmdBuf, window, out var swapchainTexture, out _, out _))
            {
                Console.WriteLine($"Failed to acquire swapchain texture: {SDL.GetError()}");
                continue;
            }

            if (swapchainTexture != IntPtr.Zero)
            {
                // Build sprite instance transfer
                var dataPtr = SDL.MapGPUTransferBuffer(device, spriteDataTransferBuffer, true);
                unsafe
                {
                    var sprites = (ComputeSpriteInstance*)dataPtr;
                    for (uint i = 0; i < SpriteCount; i++)
                    {
                        int ravioli = SDL.Rand(4);
                        sprites[i].X = SDL.Rand(640);
                        sprites[i].Y = SDL.Rand(480);
                        sprites[i].Z = 0;
                        sprites[i].Rotation = SDL.RandF() * MathF.PI * 2;
                        sprites[i].W = 32;
                        sprites[i].H = 32;
                        sprites[i].TexU = UCoords[ravioli];
                        sprites[i].TexV = VCoords[ravioli];
                        sprites[i].TexW = 0.5f;
                        sprites[i].TexH = 0.5f;
                        sprites[i].R = 1.0f;
                        sprites[i].G = 1.0f;
                        sprites[i].B = 1.0f;
                        sprites[i].A = 1.0f;
                    }
                }
                SDL.UnmapGPUTransferBuffer(device, spriteDataTransferBuffer);

                // Upload instance data
                copyPass = SDL.BeginGPUCopyPass(cmdBuf);
                SDL.UploadToGPUBuffer(copyPass,
                    new SDL.GPUTransferBufferLocation { TransferBuffer = spriteDataTransferBuffer, Offset = 0 },
                    new SDL.GPUBufferRegion { Buffer = spriteDataBuffer, Offset = 0, Size = SpriteCount * (uint)Marshal.SizeOf<ComputeSpriteInstance>() },
                    true);
                SDL.EndGPUCopyPass(copyPass);

                // Render sprites
                var colorTargetInfo = new SDL.GPUColorTargetInfo
                {
                    Texture = swapchainTexture,
                    Cycle = false,
                    LoadOp = SDL.GPULoadOp.Clear,
                    StoreOp = SDL.GPUStoreOp.Store,
                    ClearColor = new SDL.FColor { R = 0, G = 0, B = 0, A = 1 }
                };

                var colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                var renderPass = SDL.BeginGPURenderPass(cmdBuf, colorTargetPtr, 1, IntPtr.Zero);
                Marshal.FreeHGlobal(colorTargetPtr);

                SDL.BindGPUGraphicsPipeline(renderPass, renderPipeline);

                // Bind storage buffer
                SDL.BindGPUVertexStorageBuffers(renderPass, 0, [spriteDataBuffer], 1);

                var texSamplerBinding = new SDL.GPUTextureSamplerBinding { Texture = texture, Sampler = sampler };
                SDL.BindGPUFragmentSamplers(renderPass, 0, [texSamplerBinding], 1);

                unsafe
                {
                    SDL.PushGPUVertexUniformData(cmdBuf, 0, (nint)(&cameraMatrix), (uint)sizeof(Matrix4x4));
                }

                SDL.DrawGPUPrimitives(renderPass, SpriteCount * 6, 1, 0, 0);

                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(cmdBuf);
        }

        // Cleanup
        SDL.ReleaseGPUGraphicsPipeline(device, renderPipeline);
        SDL.ReleaseGPUSampler(device, sampler);
        SDL.ReleaseGPUTexture(device, texture);
        SDL.ReleaseGPUTransferBuffer(device, spriteDataTransferBuffer);
        SDL.ReleaseGPUBuffer(device, spriteDataBuffer);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }

    private static IntPtr CreateSpritePipeline(IntPtr device, IntPtr window, IntPtr vertexShader, IntPtr fragmentShader)
    {
        var swapchainFormat = SDL.GetGPUSwapchainTextureFormat(device, window);
        var colorTargetDesc = new SDL.GPUColorTargetDescription
        {
            Format = swapchainFormat,
            BlendState = new SDL.GPUColorTargetBlendState
            {
                EnableBlend = true,
                ColorBlendOp = SDL.GPUBlendOp.Add,
                AlphaBlendOp = SDL.GPUBlendOp.Add,
                SrcColorBlendFactor = SDL.GPUBlendFactor.SrcAlpha,
                DstColorBlendFactor = SDL.GPUBlendFactor.OneMinusSrcAlpha,
                SrcAlphaBlendFactor = SDL.GPUBlendFactor.SrcAlpha,
                DstAlphaBlendFactor = SDL.GPUBlendFactor.OneMinusSrcAlpha
            }
        };
        var colorTargetDescPtr = SDL.StructureToPointer<SDL.GPUColorTargetDescription>(colorTargetDesc);

        var pipelineCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            PrimitiveType = SDL.GPUPrimitiveType.TriangleList,
            TargetInfo = new SDL.GPUGraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = colorTargetDescPtr,
                NumColorTargets = 1
            }
        };

        var pipelineResult = SDL.CreateGPUGraphicsPipeline(device, in pipelineCreateInfo);
        Marshal.FreeHGlobal(colorTargetDescPtr);

        return pipelineResult;
    }
}
