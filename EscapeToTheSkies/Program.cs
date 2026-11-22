using EscapeToTheSkies;

string? captureDir = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--export-web" && i + 1 < args.Length)
    {
        WebExporter.Export(args[i + 1]);
        return;
    }
    if (args[i] == "--capture-screenshots" && i + 1 < args.Length)
    {
        captureDir = args[i + 1];
    }
}

bool smokeTest = args.Contains("--smoke-test");
using var game = new EscapeToTheSkiesGame(smokeTest, captureDir);
game.Run();
