using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class Latency
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
        var window = SDL.CreateWindow("Latency", 640, 480, 0);
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

        // Create the lag texture (8x32)
        var textureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Usage = SDL.GPUTextureUsageFlags.Sampler,
            Width = 8,
            Height = 32,
            LayerCountOrDepth = 1,
            NumLevels = 1
        };
        var lagTexture = SDL.CreateGPUTexture(device, in textureCreateInfo);

        // Load image data
        uint byteCount = 8 * 32 * 4;
        var transferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = byteCount
        };
        var textureTransferBuffer = SDL.CreateGPUTransferBuffer(device, in transferCreateInfo);

        var textureTransferData = SDL.MapGPUTransferBuffer(device, textureTransferBuffer, false);

        var imageData = Common.LoadImage(device, "latency.bmp", out _, out _);
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

        // Upload to GPU
        var cmdbuf = SDL.AcquireGPUCommandBuffer(device);
        var copyPass = SDL.BeginGPUCopyPass(cmdbuf);

        var textureTransferInfo = new SDL.GPUTextureTransferInfo
        {
            TransferBuffer = textureTransferBuffer,
            Offset = 0
        };
        var textureRegion = new SDL.GPUTextureRegion
        {
            Texture = lagTexture,
            W = 8,
            H = 32,
            D = 1
        };
        SDL.UploadToGPUTexture(copyPass, in textureTransferInfo, in textureRegion, false);

        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(cmdbuf);
        SDL.ReleaseGPUTransferBuffer(device, textureTransferBuffer);

        // Print instructions
        Console.WriteLine("Press Left/Right to toggle capturing the mouse cursor.");
        Console.WriteLine("Press Down to change the number of allowed frames in flight.");
        Console.WriteLine("Press Up to toggle fullscreen mode.");
        Console.WriteLine("When the mouse cursor is captured the color directly above the cursor's point is the result of the test.");
        Console.WriteLine("Negative lag can occur when the cursor is below the tear line when tearing is enabled as the cursor is only moved during V-blank so it lags the framebuffer update.");
        Console.WriteLine("  Gray:  -1 frames lag");
        Console.WriteLine("  White:  0 frames lag");
        Console.WriteLine("  Green:  1 frames lag");
        Console.WriteLine("  Yellow: 2 frames lag");
        Console.WriteLine("  Red:    3 frames lag");
        Console.WriteLine("  Cyan:   4 frames lag");
        Console.WriteLine("  Purple: 5 frames lag");
        Console.WriteLine("  Blue:   6 frames lag");

        var allowedFramesInFlight = 2;
        var fullscreen = false;
        var captureCursor = false;
        var lagX = 1;

        SDL.SetGPUAllowedFramesInFlight(device, (uint)allowedFramesInFlight);

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
                        if (scancode == SDL.Scancode.Left || scancode == SDL.Scancode.Right)
                        {
                            captureCursor = !captureCursor;
                        }
                        else if (scancode == SDL.Scancode.Down)
                        {
                            allowedFramesInFlight = Math.Clamp((allowedFramesInFlight + 1) % 4, 1, 3);
                            SDL.SetGPUAllowedFramesInFlight(device, (uint)allowedFramesInFlight);
                            Console.WriteLine($"Allowed frames in flight: {allowedFramesInFlight}");
                        }
                        else if (scancode == SDL.Scancode.Up)
                        {
                            fullscreen = !fullscreen;
                            SDL.SetWindowFullscreen(window, fullscreen);
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

            if (!SDL.WaitAndAcquireGPUSwapchainTexture(commandBuffer, window, out var swapchainTexture, out var w, out var h))
            {
                Console.WriteLine($"Failed to acquire swapchain texture: {SDL.GetError()}");
                continue;
            }

            if (swapchainTexture != IntPtr.Zero)
            {
                // Get the current mouse cursor position
                float cursorX, cursorY;
                SDL.GetGlobalMouseState(out cursorX, out cursorY);
                int winX, winY;
                SDL.GetWindowPosition(window, out winX, out winY);
                cursorX -= winX;
                cursorY -= winY;

                if (captureCursor)
                {
                    // Move the cursor to a known position
                    cursorX = lagX;
                    SDL.WarpMouseInWindow(window, cursorX, cursorY);
                    if (lagX >= (int)w - 8)
                    {
                        lagX = 1;
                    }
                    else
                    {
                        lagX++;
                    }
                }

                // Draw a sprite directly under the cursor if permitted by the blitting engine
                if (cursorX >= 1 && cursorX <= (int)w - 8 && cursorY >= 5 && cursorY <= (int)h - 27)
                {
                    var blitInfo = new SDL.GPUBlitInfo
                    {
                        Source = new SDL.GPUBlitRegion
                        {
                            Texture = lagTexture,
                            W = 8,
                            H = 32,
                            MipLevel = 0
                        },
                        Destination = new SDL.GPUBlitRegion
                        {
                            Texture = swapchainTexture,
                            X = (uint)(cursorX - 1),
                            Y = (uint)(cursorY - 5),
                            W = 8,
                            H = 32
                        },
                        LoadOp = SDL.GPULoadOp.Clear,
                        ClearColor = new SDL.FColor { R = 0.0f, G = 0.0f, B = 0.0f, A = 1.0f }
                    };
                    SDL.BlitGPUTexture(commandBuffer, in blitInfo);
                }
                else
                {
                    // Just clear the screen
                    var colorTargetInfo = new SDL.GPUColorTargetInfo
                    {
                        Texture = swapchainTexture,
                        ClearColor = new SDL.FColor { R = 0.0f, G = 0.0f, B = 0.0f, A = 1.0f },
                        LoadOp = SDL.GPULoadOp.Clear,
                        StoreOp = SDL.GPUStoreOp.Store
                    };
                    var colorTargetPtr = SDL.StructureToPointer<SDL.GPUColorTargetInfo>(colorTargetInfo);
                    var renderPass = SDL.BeginGPURenderPass(commandBuffer, colorTargetPtr, 1, IntPtr.Zero);
                    Marshal.FreeHGlobal(colorTargetPtr);
                    SDL.EndGPURenderPass(renderPass);
                }
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUTexture(device, lagTexture);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
