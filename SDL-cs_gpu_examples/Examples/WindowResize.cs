using System.Runtime.InteropServices;
using SDL3;

namespace SDL_cs_gpu_examples;

public static class WindowResize
{
    private struct Resolution
    {
        public uint X, Y;
        public Resolution(uint x, uint y) { X = x; Y = y; }
    }

    private static readonly Resolution[] Resolutions =
    [
        new(640, 480),
        new(1280, 720),
        new(1024, 1024),
        new(1600, 900),
        new(1920, 1080),
        new(3200, 1800),
        new(3840, 2160)
    ];

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

        var window = SDL.CreateWindow("WindowResize", 640, 480, SDL.WindowFlags.Resizable);
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

        // Create pipeline
        var swapchainFormat = SDL.GetGPUSwapchainTextureFormat(device, window);
        var colorTargetDesc = new SDL.GPUColorTargetDescription { Format = swapchainFormat };
        var colorTargetDescPtr = SDL.StructureToPointer<SDL.GPUColorTargetDescription>(colorTargetDesc);

        var pipelineCreateInfo = new SDL.GPUGraphicsPipelineCreateInfo
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            PrimitiveType = SDL.GPUPrimitiveType.TriangleList,
            RasterizerState = new SDL.GPURasterizerState
            {
                FillMode = SDL.GPUFillMode.Fill
            },
            TargetInfo = new SDL.GPUGraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = colorTargetDescPtr,
                NumColorTargets = 1
            }
        };

        var pipeline = SDL.CreateGPUGraphicsPipeline(device, in pipelineCreateInfo);
        Marshal.FreeHGlobal(colorTargetDescPtr);

        if (pipeline == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create pipeline!");
            return -1;
        }

        SDL.ReleaseGPUShader(device, vertexShader);
        SDL.ReleaseGPUShader(device, fragmentShader);

        Console.WriteLine("Press Left and Right to resize the window!");

        int resolutionIndex = 0;

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
                        bool changeResolution = false;

                        if (scancode == SDL.Scancode.Right)
                        {
                            resolutionIndex = (resolutionIndex + 1) % Resolutions.Length;
                            changeResolution = true;
                        }
                        else if (scancode == SDL.Scancode.Left)
                        {
                            resolutionIndex -= 1;
                            if (resolutionIndex < 0)
                                resolutionIndex = Resolutions.Length - 1;
                            changeResolution = true;
                        }

                        if (changeResolution)
                        {
                            var res = Resolutions[resolutionIndex];
                            Console.WriteLine($"Setting resolution to: {res.X}, {res.Y}");
                            SDL.SetWindowSize(window, (int)res.X, (int)res.Y);
                            SDL.SetWindowPosition(window, (int)SDL.WindowPosCentered(), (int)SDL.WindowPosCentered());
                            SDL.SyncWindow(window);
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

            if (!SDL.WaitAndAcquireGPUSwapchainTexture(commandBuffer, window, out var swapchainTexture, out _, out _))
            {
                Console.WriteLine($"Failed to acquire swapchain texture: {SDL.GetError()}");
                continue;
            }

            if (swapchainTexture != IntPtr.Zero)
            {
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

                SDL.BindGPUGraphicsPipeline(renderPass, pipeline);
                SDL.DrawGPUPrimitives(renderPass, 3, 1, 0, 0);
                SDL.EndGPURenderPass(renderPass);
            }

            SDL.SubmitGPUCommandBuffer(commandBuffer);
        }

        // Cleanup
        SDL.ReleaseGPUGraphicsPipeline(device, pipeline);
        SDL.ReleaseWindowFromGPUDevice(device, window);
        SDL.DestroyWindow(window);
        SDL.DestroyGPUDevice(device);
        SDL.Quit();

        return 0;
    }
}
