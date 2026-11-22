using EscapeToTheSkies;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--export-web" && i + 1 < args.Length)
    {
        WebExporter.Export(args[i + 1]);
        return;
    }
}

bool smokeTest = args.Contains("--smoke-test");
using var game = new EscapeToTheSkiesGame(smokeTest);
game.Run();
