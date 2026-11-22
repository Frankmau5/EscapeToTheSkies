using System.Globalization;

namespace EscapeToTheSkies;

public sealed class SaveData
{
    public double? BestTimeSeconds { get; set; }
    public int BestCoinsCollected { get; set; }
    public int RunsCompleted { get; set; }
}

public static class SaveManager
{
    private static string GetSaveFilePath()
    {
        string baseDir = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? "";
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            baseDir = Path.Combine(home, ".local", "share");
        }

        return Path.Combine(baseDir, "EscapeToTheSkies", "records.txt");
    }

    public static SaveData Load()
    {
        var data = new SaveData();
        try
        {
            string path = GetSaveFilePath();
            if (!File.Exists(path))
            {
                return data;
            }

            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = line[..eq];
                string val = line[(eq + 1)..];
                switch (key)
                {
                    case "best_time" when double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double t) && t > 0:
                        data.BestTimeSeconds = t;
                        break;
                    case "best_coins" when int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int c):
                        data.BestCoinsCollected = c;
                        break;
                    case "runs" when int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r):
                        data.RunsCompleted = r;
                        break;
                }
            }
        }
        catch
        {
            // Ignore read issues on restricted filesystems
        }

        return data;
    }

    public static bool RecordRun(SaveData data, double runSeconds, int coinsCollected)
    {
        bool newRecord = !data.BestTimeSeconds.HasValue || runSeconds < data.BestTimeSeconds.Value;
        if (newRecord)
        {
            data.BestTimeSeconds = runSeconds;
        }

        if (coinsCollected > data.BestCoinsCollected)
        {
            data.BestCoinsCollected = coinsCollected;
        }

        data.RunsCompleted++;

        try
        {
            string path = GetSaveFilePath();
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string bestTimeStr = data.BestTimeSeconds?.ToString("F3", CultureInfo.InvariantCulture) ?? "";
            File.WriteAllLines(path, new[]
            {
                $"best_time={bestTimeStr}",
                $"best_coins={data.BestCoinsCollected.ToString(CultureInfo.InvariantCulture)}",
                $"runs={data.RunsCompleted.ToString(CultureInfo.InvariantCulture)}",
            });
        }
        catch
        {
            // Ignore write issues on read-only environments
        }

        return newRecord;
    }

    public static string FormatTime(double seconds)
    {
        if (seconds < 0)
        {
            seconds = 0;
        }

        int mins = (int)(seconds / 60.0);
        double rem = seconds - mins * 60.0;
        return $"{mins:00}:{rem:00.00}";
    }
}
