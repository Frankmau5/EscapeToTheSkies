using System.Globalization;
using System.Text;

namespace EscapeToTheSkies;

public static class WebExporter
{
    public static void Export(string outPath)
    {
        var level = new LevelData();
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        sb.AppendLine("// Auto-generated from LevelData.cs & StatsAndUpgrades.cs");
        sb.AppendLine("window.GAME_LEVEL_DATA = {");
        sb.AppendLine($"  width: {LevelData.Width},");
        sb.AppendLine($"  height: {LevelData.Height},");
        sb.AppendLine($"  tileSize: {LevelData.TileSize},");
        sb.AppendLine(string.Format(inv, "  playerSpawn: {{ x: {0}, y: {1} }},", level.PlayerSpawnPx.X, level.PlayerSpawnPx.Y));
        sb.AppendLine(string.Format(inv, "  goalArchBase: {{ x: {0}, y: {1} }},", level.GoalArchBasePx.X, level.GoalArchBasePx.Y));
        sb.AppendLine(string.Format(inv, "  goalTrigger: {{ x: {0}, y: {1}, w: {2}, h: {3} }},",
            level.GoalTriggerPx.X, level.GoalTriggerPx.Y, level.GoalTriggerPx.Width, level.GoalTriggerPx.Height));

        // Flat row-major tiles array
        sb.Append("  tiles: [");
        for (int ty = 0; ty < LevelData.Height; ty++)
        {
            for (int tx = 0; tx < LevelData.Width; tx++)
            {
                if (ty > 0 || tx > 0) sb.Append(',');
                sb.Append((int)level.GetTile(tx, ty));
            }
        }
        sb.AppendLine("],");

        // Coins
        sb.Append("  coins: [");
        for (int i = 0; i < level.Coins.Count; i++)
        {
            CoinSpawn c = level.Coins[i];
            if (i > 0) sb.Append(',');
            sb.Append(string.Format(inv, "{{id:{0},x:{1},y:{2},v:{3},t:{4}}}",
                c.Id, c.Position.X, c.Position.Y, c.Value, c.TierIndex));
        }
        sb.AppendLine("],");

        // Props
        sb.Append("  props: [");
        for (int i = 0; i < level.Props.Count; i++)
        {
            DecorationProp p = level.Props[i];
            if (i > 0) sb.Append(',');
            sb.Append(string.Format(inv, "{{k:{0},x:{1},y:{2},l:{3}}}",
                (int)p.Kind, p.Position.X, p.Position.Y, p.LengthTiles));
        }
        sb.AppendLine("],");

        // Physics & Stat tables
        sb.AppendLine("  stats: {");
        sb.AppendLine($"    maxLevel: {PlayerStats.MaxLevel},");
        sb.AppendLine($"    upgradeCosts: [{string.Join(",", PlayerStats.UpgradeCosts)}],");
        sb.AppendLine($"    runSpeeds: [{string.Join(",", PlayerStats.RunSpeeds.Select(v => v.ToString(inv)))}],");
        sb.AppendLine($"    groundAccels: [{string.Join(",", PlayerStats.GroundAccels.Select(v => v.ToString(inv)))}],");
        sb.AppendLine($"    airAccels: [{string.Join(",", PlayerStats.AirAccels.Select(v => v.ToString(inv)))}],");
        sb.AppendLine($"    jumpVelocities: [{string.Join(",", PlayerStats.JumpVelocities.Select(v => v.ToString(inv)))}],");
        sb.AppendLine($"    jumpSustainTimes: [{string.Join(",", PlayerStats.JumpSustainTimes.Select(v => v.ToString(inv)))}],");
        sb.AppendLine(string.Format(inv, "    baseGravity: {0},", PlayerStats.BaseGravity));
        sb.AppendLine(string.Format(inv, "    sustainGravityScale: {0},", PlayerStats.SustainGravityScale));
        sb.AppendLine(string.Format(inv, "    apexGravityScale: {0},", PlayerStats.ApexGravityScale));
        sb.AppendLine(string.Format(inv, "    fallGravityScale: {0},", PlayerStats.FallGravityScale));
        sb.AppendLine(string.Format(inv, "    apexThresholdVy: {0},", PlayerStats.ApexThresholdVy));
        sb.AppendLine(string.Format(inv, "    maxFallSpeed: {0},", PlayerStats.MaxFallSpeed));
        sb.AppendLine(string.Format(inv, "    earlyReleaseCut: {0}", PlayerStats.EarlyReleaseCut));
        sb.AppendLine("  }");
        sb.AppendLine("};");

        string? dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(outPath, sb.ToString());
        Console.WriteLine($"Exported web level data to {outPath}");
    }
}
