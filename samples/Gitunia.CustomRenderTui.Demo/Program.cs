namespace Gitunia.CustomRenderTui.Demo;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: gitunia-custom-demo <repo-path>");
            return 2;
        }

        if (!RawTerminal.IsSupported || Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            Console.Error.WriteLine("gitunia-custom-demo needs an interactive terminal on Linux, macOS or Windows.");
            return 2;
        }

        using var app = new App(Path.GetFullPath(args[0]));
        return app.Run();
    }
}
