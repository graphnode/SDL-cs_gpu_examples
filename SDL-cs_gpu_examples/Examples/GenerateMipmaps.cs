using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class GenerateMipmaps
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
        var window = SDL.CreateWindow("GenerateMipmaps", 640, 480, 0);
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

        // Create the mipmap texture with 3 levels (32x32, 16x16, 8x8)
        var textureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Usage = SDL.GPUTextureUsageFlags.Sampler | SDL.GPUTextureUsageFlags.ColorTarget,
            Width = 32,
            Height = 32,
            LayerCountOrDepth = 1,
            NumLevels = 3
        };
        var mipmapTexture = SDL.CreateGPUTexture(device, in textureCreateInfo);

        // Load image data
        uint byteCount = 32 * 32 * 4;
        var transferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = byteCount
        };
        var textureTransferBuffer = SDL.CreateGPUTransferBuffer(device, in transferCreateInfo);

        var textureTransferData = SDL.MapGPUTransferBuffer(device, textureTransferBuffer, false);

        var imageData = Common.LoadImage(device, "cube0.bmp", out int _, out int _);
        if (imageData == IntPtr.Zero)
        {
            Console.WriteLine("Could not load image data!");
            return -1;
        }

        unsafe
        {
            var surfacePtr = (SDL.Surface*)imageData;
            Buffer.MemoryCopy((void*)surfacePtr->Pixels, (void*)textureTransferData, byteCount, byteCount);
        }
        SDL.DestroySurface(imageData);

        SDL.UnmapGPUTransferBuffer(device, textureTransferBuffer);

        // Upload to GPU and generate mipmaps
        var cmdbuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(cmdbuf);

        var textureTransferInfo = new SDL.GPUTextureTransferInfo
        {
            TransferBuffer = textureTransferBuffer,
            Offset = 0
        };
        var textureRegion = new SDL.GPUTextureRegion
        {
            Texture = mipmapTexture,
            W = 32,
            H = 32,
            D = 1
        };
        SDL.UploadToGPUTexture(copyPass, in textureTransferInfo, in textureRegion, false);

        SDL.EndGPUCopyPass(copyPass);
        SDL.GenerateMipmapsForGPUTexture(cmdbuf, mipmapTexture);

        SDL.SubmitGPUCommandBuffer(cmdbuf);
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
                // Blit the smallest mip level (level 2 = 8x8) stretched to fill the screen
                var blitInfo = new SDL.GPUBlitInfo
                {
                    Source = new SDL.GPUBlitRegion
                    {
                        Texture = mipmapTexture,
                        W = 8,
                        H = 8,
                        MipLevel = 2
                    },
                    Destination = new SDL.GPUBlitRegion
                    {
                        Texture = swapchainTexture,
                        W = w,
                        H = h
                    },
                    LoadOp = SDL.GPULoadOp.DontCare
                };
                SDL.BlitGPUTexture(commandBuffer, in blitInfo);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUTexture(device, mipmapTexture);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
