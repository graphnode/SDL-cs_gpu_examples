using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class CopyAndReadback
{
    // P/Invoke workaround: SDL3-CS binding incorrectly types the second parameter
    // as GPUTextureRegion instead of GPUBufferRegion for DownloadFromGPUBuffer
    [DllImport("SDL3", CallingConvention = CallingConvention.Cdecl,
        EntryPoint = "SDL_DownloadFromGPUBuffer")]
    private static extern void DownloadFromGPUBuffer(
        IntPtr copyPass,
        in SDL.GPUBufferRegion source,
        in SDL.GPUTransferBufferLocation destination);

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
        var window = SDL.CreateWindow("CopyAndReadback", 640, 480, 0);
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

        uint textureWidth = (uint)imgW;
        uint textureHeight = (uint)imgH;

        // Create texture resources
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
        var originalTexture = SDL.CreateGPUTexture(device, in textureCreateInfo);
        var textureCopy = SDL.CreateGPUTexture(device, in textureCreateInfo);

        var smallTextureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Width = textureWidth / 2,
            Height = textureHeight / 2,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.Sampler | SDL.GPUTextureUsageFlags.ColorTarget
        };
        var textureSmall = SDL.CreateGPUTexture(device, in smallTextureCreateInfo);

        // Buffer data
        uint[] bufferData = [2, 4, 8, 16, 32, 64, 128, 0];
        uint bufferDataSize = (uint)(bufferData.Length * sizeof(uint));

        var originalBufferCreateInfo = new SDL.GPUBufferCreateInfo
        {
            Usage = SDL.GPUBufferUsageFlags.GraphicsStorageRead,
            Size = bufferDataSize
        };
        var originalBuffer = SDL.CreateGPUBuffer(device, in originalBufferCreateInfo);
        var bufferCopy = SDL.CreateGPUBuffer(device, in originalBufferCreateInfo);

        uint imageDataSize = textureWidth * textureHeight * 4;

        // Create download transfer buffer
        var downloadTransferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Download,
            Size = imageDataSize + bufferDataSize
        };
        var downloadTransferBuffer = SDL.CreateGPUTransferBuffer(device, in downloadTransferCreateInfo);

        // Create upload transfer buffer
        var uploadTransferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = imageDataSize + bufferDataSize
        };
        var uploadTransferBuffer = SDL.CreateGPUTransferBuffer(device, in uploadTransferCreateInfo);

        // Map and copy data to upload transfer buffer
        var uploadTransferPtr = SDL.MapGPUTransferBuffer(device, uploadTransferBuffer, false);
        unsafe
        {
            var surfacePtr = (SDL.Surface*)surface;
            Buffer.MemoryCopy((void*)surfacePtr->Pixels, (void*)uploadTransferPtr, imageDataSize, imageDataSize);
            fixed (uint* bufDataPtr = bufferData)
            {
                Buffer.MemoryCopy(bufDataPtr, (void*)(uploadTransferPtr + (int)imageDataSize), bufferDataSize, bufferDataSize);
            }
        }
        SDL.UnmapGPUTransferBuffer(device, uploadTransferBuffer);

        var cmdbuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(cmdbuf);

        // Upload original texture
        var textureTransferInfo = new SDL.GPUTextureTransferInfo
        {
            TransferBuffer = uploadTransferBuffer,
            Offset = 0
        };
        var textureRegion = new SDL.GPUTextureRegion
        {
            Texture = originalTexture,
            W = textureWidth,
            H = textureHeight,
            D = 1
        };
        SDL.UploadToGPUTexture(copyPass, in textureTransferInfo, in textureRegion, false);

        // Copy original texture to copy
        var srcLocation = new SDL.GPUTextureLocation
        {
            Texture = originalTexture,
            X = 0, Y = 0, Z = 0
        };
        var dstLocation = new SDL.GPUTextureLocation
        {
            Texture = textureCopy,
            X = 0, Y = 0, Z = 0
        };
        SDL.CopyGPUTextureToTexture(copyPass, in srcLocation, in dstLocation, textureWidth, textureHeight, 1, false);

        // Upload original buffer
        var bufferTransferLocation = new SDL.GPUTransferBufferLocation
        {
            TransferBuffer = uploadTransferBuffer,
            Offset = imageDataSize
        };
        var bufferRegion = new SDL.GPUBufferRegion
        {
            Buffer = originalBuffer,
            Offset = 0,
            Size = bufferDataSize
        };
        SDL.UploadToGPUBuffer(copyPass, in bufferTransferLocation, in bufferRegion, false);

        // Copy original buffer to copy
        var srcBufferLocation = new SDL.GPUBufferLocation
        {
            Buffer = originalBuffer,
            Offset = 0
        };
        var dstBufferLocation = new SDL.GPUBufferLocation
        {
            Buffer = bufferCopy,
            Offset = 0
        };
        SDL.CopyGPUBufferToBuffer(copyPass, in srcBufferLocation, in dstBufferLocation, bufferDataSize, false);

        SDL.EndGPUCopyPass(copyPass);

        // Blit to create the half-size version
        var blitInfo = new SDL.GPUBlitInfo
        {
            Source = new SDL.GPUBlitRegion
            {
                Texture = originalTexture,
                W = textureWidth,
                H = textureHeight
            },
            Destination = new SDL.GPUBlitRegion
            {
                Texture = textureSmall,
                W = textureWidth / 2,
                H = textureHeight / 2
            },
            LoadOp = SDL.GPULoadOp.DontCare,
            Filter = SDL.GPUFilter.Linear
        };
        SDL.BlitGPUTexture(cmdbuf, in blitInfo);

        // Download the copied data
        copyPass = SDL.BeginGPUCopyPass(cmdbuf);

        var downloadTextureRegion = new SDL.GPUTextureRegion
        {
            Texture = textureCopy,
            W = textureWidth,
            H = textureHeight,
            D = 1
        };
        var downloadTextureTransferInfo = new SDL.GPUTextureTransferInfo
        {
            TransferBuffer = downloadTransferBuffer,
            Offset = 0
        };
        SDL.DownloadFromGPUTexture(copyPass, in downloadTextureRegion, in downloadTextureTransferInfo);

        var downloadBufferRegion = new SDL.GPUBufferRegion
        {
            Buffer = bufferCopy,
            Offset = 0,
            Size = bufferDataSize
        };
        var downloadBufferTransferLocation = new SDL.GPUTransferBufferLocation
        {
            TransferBuffer = downloadTransferBuffer,
            Offset = imageDataSize
        };
        DownloadFromGPUBuffer(copyPass, in downloadBufferRegion, in downloadBufferTransferLocation);

        SDL.EndGPUCopyPass(copyPass);

        var fence = SDL.SubmitGPUCommandBufferAndAcquireFence(cmdbuf);
        SDL.WaitForGPUFences(device, true, [fence], 1);
        SDL.ReleaseGPUFence(device, fence);

        // Compare original data to downloaded data
        var downloadedData = SDL.MapGPUTransferBuffer(device, downloadTransferBuffer, false);
        unsafe
        {
            var surfacePtr = (SDL.Surface*)surface;
            bool textureMatch = true;
            byte* originalPixels = (byte*)surfacePtr->Pixels;
            byte* downloadedPixels = (byte*)downloadedData;
            for (uint i = 0; i < imageDataSize; i++)
            {
                if (originalPixels[i] != downloadedPixels[i])
                {
                    textureMatch = false;
                    break;
                }
            }

            if (textureMatch)
                Console.WriteLine("SUCCESS! Original texture bytes and the downloaded bytes match!");
            else
                Console.WriteLine("FAILURE! Original texture bytes do not match downloaded bytes!");

            bool bufferMatch = true;
            uint* downloadedBufferData = (uint*)(downloadedData + (int)imageDataSize);
            fixed (uint* origBufData = bufferData)
            {
                for (int i = 0; i < bufferData.Length; i++)
                {
                    if (origBufData[i] != downloadedBufferData[i])
                    {
                        bufferMatch = false;
                        break;
                    }
                }
            }

            if (bufferMatch)
                Console.WriteLine("SUCCESS! Original buffer bytes and the downloaded bytes match!");
            else
                Console.WriteLine("FAILURE! Original buffer bytes do not match downloaded bytes!");
        }

        SDL.UnmapGPUTransferBuffer(device, downloadTransferBuffer);

        // Cleanup transfer buffers
        SDL.ReleaseGPUTransferBuffer(device, downloadTransferBuffer);
        SDL.ReleaseGPUTransferBuffer(device, uploadTransferBuffer);
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

            if (!SDL.WaitAndAcquireGPUSwapchainTexture(commandBuffer, window, out var swapchainTexture, out var swW, out var swH))
            {
                Console.WriteLine($"Failed to acquire swapchain texture: {SDL.GetError()}");
                continue;
            }

            if (swapchainTexture != IntPtr.Zero)
            {
                // Clear pass
                var colorTargetInfo = new SDL.GPUColorTargetInfo
                {
                    Texture = swapchainTexture,
                    ClearColor = new SDL.FColor { R = 0, G = 0, B = 0, A = 1 },
                    LoadOp = SDL.GPULoadOp.Clear,
                    StoreOp = SDL.GPUStoreOp.Store
                };
                var colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                var clearPass = SDL.BeginGPURenderPass(commandBuffer, colorTargetPtr, 1, IntPtr.Zero);
                Marshal.FreeHGlobal(colorTargetPtr);
                SDL.EndGPURenderPass(clearPass);

                // Blit original to top-left
                var blit1 = new SDL.GPUBlitInfo
                {
                    Source = new SDL.GPUBlitRegion
                    {
                        Texture = originalTexture,
                        W = textureWidth,
                        H = textureHeight
                    },
                    Destination = new SDL.GPUBlitRegion
                    {
                        Texture = swapchainTexture,
                        W = swW / 2,
                        H = swH / 2
                    },
                    LoadOp = SDL.GPULoadOp.Load,
                    Filter = SDL.GPUFilter.Nearest
                };
                SDL.BlitGPUTexture(commandBuffer, in blit1);

                // Blit copy to top-right
                var blit2 = new SDL.GPUBlitInfo
                {
                    Source = new SDL.GPUBlitRegion
                    {
                        Texture = textureCopy,
                        W = textureWidth,
                        H = textureHeight
                    },
                    Destination = new SDL.GPUBlitRegion
                    {
                        Texture = swapchainTexture,
                        X = swW / 2,
                        W = swW / 2,
                        H = swH / 2
                    },
                    LoadOp = SDL.GPULoadOp.Load,
                    Filter = SDL.GPUFilter.Nearest
                };
                SDL.BlitGPUTexture(commandBuffer, in blit2);

                // Blit small to bottom-center
                var blit3 = new SDL.GPUBlitInfo
                {
                    Source = new SDL.GPUBlitRegion
                    {
                        Texture = textureSmall,
                        W = textureWidth / 2,
                        H = textureHeight / 2
                    },
                    Destination = new SDL.GPUBlitRegion
                    {
                        Texture = swapchainTexture,
                        X = swW / 4,
                        Y = swH / 2,
                        W = swW / 2,
                        H = swH / 2
                    },
                    LoadOp = SDL.GPULoadOp.Load,
                    Filter = SDL.GPUFilter.Nearest
                };
                SDL.BlitGPUTexture(commandBuffer, in blit3);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUTexture(device, originalTexture);
        SDL.ReleaseGPUTexture(device, textureCopy);
        SDL.ReleaseGPUTexture(device, textureSmall);
        SDL.ReleaseGPUBuffer(device, originalBuffer);
        SDL.ReleaseGPUBuffer(device, bufferCopy);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
