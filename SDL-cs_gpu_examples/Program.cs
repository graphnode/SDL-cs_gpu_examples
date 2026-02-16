using SDL3;

namespace SDL_cs_gpu_examples;

public static class Program
{
    private record struct ExampleInfo(string Name, Func<int> Run);

    private static readonly ExampleInfo[] AllExamples =
    [
        new("ClearScreen", ClearScreen.Main),
        new("ClearScreenMultiWindow", ClearScreenMultiWindow.Main),
        new("BasicTriangle", BasicTriangle.Main),
        new("BasicVertexBuffer", BasicVertexBuffer.Main),
    ];

    private static ExampleInfo GetExample(string name)
    {
        var example = Array.Find(AllExamples, x =>
            x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        );
        return example;
    }

    public static int Main(string[] args)
    {
        // Initialize ShaderCross for runtime HLSL compilation
        if (!ShaderCross.Init())
        {
            Console.WriteLine($"Failed to initialize ShaderCross: {SDL.GetError()}");
            return 1;
        }

        try
        {
            if (args.Length > 0)
            {
                var example = GetExample(args[0]);
                if (example == default)
                    return 1;
                example.Run.Invoke();
            }
            else
            {
                foreach (var example in AllExamples)
                {
                    example.Run.Invoke();
                }
            }

            return 0;
        }
        finally
        {
            ShaderCross.Quit();
        }
    }
}
