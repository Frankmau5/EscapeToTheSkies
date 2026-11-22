using EscapeToTheSkies;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

int failures = 0;

void Assert(bool condition, string message)
{
    if (!condition)
    {
        Console.Error.WriteLine($"FAIL: {message}");
        failures++;
    }
}

// 1. Verify stat progression tables and upgrade economy
{
    var stats = new PlayerStats();
    Assert(stats.SpeedLevel == 0 && stats.JumpHeightLevel == 0 && stats.JumpDurationLevel == 0,
        "Initial stat levels must start at 0");
    Assert(!stats.TryUpgrade(StatType.Speed), "Cannot upgrade without coins");

    for (int lv = 0; lv < PlayerStats.MaxLevel; lv++)
    {
        int cost = stats.GetNextCost(StatType.Speed);
        Assert(cost == PlayerStats.UpgradeCosts[lv], $"Unexpected cost at level {lv}");
        stats.Coins = cost;
        Assert(stats.TryUpgrade(StatType.Speed), $"Upgrade should succeed at level {lv}");
    }
    Assert(stats.IsMaxed(StatType.Speed), "Speed should be maxed at level 5");
    stats.Coins = 999;
    Assert(!stats.TryUpgrade(StatType.Speed), "Cannot upgrade past max level");

    // Monotonicity across Speed, JumpHeight, JumpDuration
    for (int i = 0; i < PlayerStats.MaxLevel; i++)
    {
        Assert(PlayerStats.RunSpeeds[i + 1] > PlayerStats.RunSpeeds[i], "RunSpeeds must strictly increase");
        Assert(PlayerStats.JumpVelocities[i + 1] > PlayerStats.JumpVelocities[i], "JumpVelocities must strictly increase");
        Assert(PlayerStats.JumpSustainTimes[i + 1] > PlayerStats.JumpSustainTimes[i], "JumpSustainTimes must strictly increase");
    }

    JumpEnvelope lv0 = PlayerStats.SimulateMaxJump(0, 0, 0);
    JumpEnvelope lv1H = PlayerStats.SimulateMaxJump(0, 1, 0);
    JumpEnvelope lv1D = PlayerStats.SimulateMaxJump(0, 0, 1);
    JumpEnvelope lv1S = PlayerStats.SimulateMaxJump(1, 0, 0);
    JumpEnvelope lv5 = PlayerStats.SimulateMaxJump(5, 5, 5);

    Assert(lv1H.MaxHeightPx > lv0.MaxHeightPx + 10f, "Jump Height upgrade must increase max jump height significantly");
    Assert(lv1D.AirTimeSec > lv0.AirTimeSec + 0.08f, "Jump Duration upgrade must increase air time significantly");
    Assert(lv1S.MaxDistancePx > lv0.MaxDistancePx + 10f, "Speed upgrade must increase horizontal jump span significantly");
    Assert(lv5.MaxHeightPx > lv0.MaxHeightPx * 4f, "Maxed stats should dramatically expand vertical reach");

    Console.WriteLine($"Jump envelopes (tiles): Lv0=({lv0.MaxHeightPx / 16f:0.00}h, {lv0.MaxDistancePx / 16f:0.00}w), " +
                      $"Lv1=(H={lv1H.MaxHeightPx / 16f:0.00}h, D={lv1D.AirTimeSec:0.00}s), " +
                      $"Lv5=({lv5.MaxHeightPx / 16f:0.00}h, {lv5.MaxDistancePx / 16f:0.00}w)");
}

// 2. Verify LevelData geometry, coins, spawn, and goal trigger
var level = new LevelData();
{
    Assert(level.Coins.Count >= 40, $"Expected rich coin layout, got {level.Coins.Count}");
    Assert(level.TotalPossibleCoins >= 180, $"Expected >= 180 total coin value, got {level.TotalPossibleCoins}");

    foreach (CoinSpawn coin in level.Coins)
    {
        int tx = (int)MathF.Floor(coin.Position.X / LevelData.TileSize);
        int ty = (int)MathF.Floor(coin.Position.Y / LevelData.TileSize);
        TileType tileAtCoin = level.GetTile(tx, ty);
        Assert(!LevelData.IsSolid(tileAtCoin), $"Coin #{coin.Id} at ({tx},{ty}) overlaps solid tile {tileAtCoin}");
    }

    // Spawn check: player feet rest directly on standable tile, hitbox is empty air
    int spawnTx = (int)MathF.Floor(level.PlayerSpawnPx.X / LevelData.TileSize);
    int spawnFloorTy = (int)MathF.Floor(level.PlayerSpawnPx.Y / LevelData.TileSize);
    Assert(LevelData.IsStandable(level.GetTile(spawnTx, spawnFloorTy)), "Player spawn must rest on standable tile");
    Assert(!LevelData.IsSolid(level.GetTile(spawnTx, spawnFloorTy - 1)), "Player spawn body tile must be clear");

    // Goal check: goal trigger must be unobstructed
    int goalCenterTx = level.GoalTriggerPx.Center.X / LevelData.TileSize;
    int goalFloorTy = level.GoalTriggerPx.Bottom / LevelData.TileSize;
    Assert(LevelData.IsStandable(level.GetTile(goalCenterTx, goalFloorTy)), "Goal arch must rest on summit tile");
    Assert(!LevelData.IsSolid(level.GetTile(goalCenterTx, goalFloorTy - 1)), "Goal arch interior must be clear");
}

// 3. Progressive Reachability Simulation:
// Verify that starting at Lv(0,0,0) with 0 coins, collecting reachable coins and buying upgrades
// allows reaching the Sky Escape Gate without softlocks!
{
    var standableTiles = new List<(int tx, int ty)>();
    for (int ty = 0; ty < LevelData.Height; ty++)
    {
        for (int tx = 0; tx < LevelData.Width; tx++)
        {
            if (LevelData.IsStandable(level.GetTile(tx, ty)) && !LevelData.IsSolid(level.GetTile(tx, ty - 1)))
            {
                standableTiles.Add((tx, ty));
            }
        }
    }

    int startTx = (int)MathF.Floor(level.PlayerSpawnPx.X / LevelData.TileSize);
    int startTy = (int)MathF.Floor(level.PlayerSpawnPx.Y / LevelData.TileSize);

    var visited = new HashSet<(int tx, int ty)>();
    var stats = new PlayerStats();

    HashSet<(int tx, int ty)> ComputeReachable(int sLv, int hLv, int dLv)
    {
        JumpEnvelope env = PlayerStats.SimulateMaxJump(sLv, hLv, dLv);
        // Conservative playable clearance margins
        float maxUpPx = env.MaxHeightPx - 4f;
        float maxHorizPx = env.MaxDistancePx * 0.90f;

        var reach = new HashSet<(int tx, int ty)> { (startTx, startTy) };
        var queue = new Queue<(int tx, int ty)>();
        queue.Enqueue((startTx, startTy));

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();

            // Walk left/right on contiguous surface or step down
            foreach (int dx in new[] { -1, 1 })
            {
                int nx = cur.tx + dx;
                if (nx >= 0 && nx < LevelData.Width &&
                    LevelData.IsStandable(level.GetTile(nx, cur.ty)) &&
                    !LevelData.IsSolid(level.GetTile(nx, cur.ty - 1)) &&
                    reach.Add((nx, cur.ty)))
                {
                    queue.Enqueue((nx, cur.ty));
                }
            }

            // Jump to any standable tile within conservative jump parabola envelope
            foreach (var cand in standableTiles)
            {
                if (reach.Contains(cand)) continue;

                float dyPx = (cur.ty - cand.ty) * LevelData.TileSize; // positive = cand is higher
                float dxPx = MathF.Abs(cand.tx - cur.tx) * LevelData.TileSize;

                if (dyPx > maxUpPx) continue;

                if (dyPx >= 0f)
                {
                    // Ascending jump: tighter horizontal span the higher the target is
                    float heightRatio = Math.Clamp(dyPx / MathF.Max(1f, maxUpPx), 0f, 1f);
                    float allowedDx = maxHorizPx * (1.0f - 0.35f * heightRatio) + 12f;
                    if (dxPx <= allowedDx && reach.Add(cand))
                    {
                        queue.Enqueue(cand);
                    }
                }
                else
                {
                    // Dropping down to lower platform
                    float dropFactor = 1f + MathF.Min(2f, (-dyPx) / 64f) * 0.45f;
                    if (dxPx <= maxHorizPx * dropFactor && reach.Add(cand))
                    {
                        queue.Enqueue(cand);
                    }
                }
            }
        }

        return reach;
    }

    int CollectCoinsIn(HashSet<(int tx, int ty)> reach)
    {
        int sum = 0;
        JumpEnvelope env = PlayerStats.SimulateMaxJump(stats.SpeedLevel, stats.JumpHeightLevel, stats.JumpDurationLevel);
        foreach (CoinSpawn coin in level.Coins)
        {
            int cx = (int)MathF.Floor(coin.Position.X / LevelData.TileSize);
            int cy = (int)MathF.Floor(coin.Position.Y / LevelData.TileSize);

            // Reachable if any reached tile is directly below or within jump height of coin
            bool canGrab = false;
            foreach (var t in reach)
            {
                float dx = MathF.Abs(t.tx - cx) * LevelData.TileSize;
                float dy = (t.ty - cy) * LevelData.TileSize;
                if (dy >= 0 && dy <= env.MaxHeightPx + 10f && dx <= env.MaxDistancePx * 0.85f + 12f)
                {
                    canGrab = true;
                    break;
                }
            }

            if (canGrab) sum += coin.Value;
        }
        return sum;
    }

    // Check Lv0 coins
    var reachLv0 = ComputeReachable(0, 0, 0);
    int lv0Coins = CollectCoinsIn(reachLv0);
    Assert(lv0Coins >= 16, $"At Lv(0,0,0), expected >= 16 reachable coins, found {lv0Coins}");

    // Simulate balanced upgrading as coins unlock higher areas
    int spent = 0;
    bool goalReached = false;
    for (int iter = 0; iter < 15; iter++)
    {
        visited = ComputeReachable(stats.SpeedLevel, stats.JumpHeightLevel, stats.JumpDurationLevel);
        int availTotal = CollectCoinsIn(visited);
        stats.Coins = Math.Max(0, availTotal - spent);

        // Check if goal tile is reached
        int goalTx = level.GoalTriggerPx.Center.X / LevelData.TileSize;
        int goalFloorTy = level.GoalTriggerPx.Bottom / LevelData.TileSize;
        if (visited.Contains((goalTx, goalFloorTy)))
        {
            goalReached = true;
            break;
        }

        // Buy cheapest upgrade among Speed, JumpHeight, JumpDuration
        StatType? bestBuy = null;
        int bestCost = int.MaxValue;
        foreach (StatType st in new[] { StatType.JumpHeight, StatType.JumpDuration, StatType.Speed })
        {
            if (!stats.IsMaxed(st) && stats.CanAfford(st) && stats.GetNextCost(st) < bestCost)
            {
                bestCost = stats.GetNextCost(st);
                bestBuy = st;
            }
        }

        if (bestBuy is null) break;
        stats.TryUpgrade(bestBuy.Value);
        spent += bestCost;
    }

    Assert(goalReached, $"Summit Goal must be reachable via natural coin progression! Stopped at SPD={stats.SpeedLevel}, HGT={stats.JumpHeightLevel}, DUR={stats.JumpDurationLevel}, visited={visited.Count} tiles.");
    Console.WriteLine($"Reachability verified: Lv0 coins={lv0Coins}, reached summit at SPD={stats.SpeedLevel}, HGT={stats.JumpHeightLevel}, DUR={stats.JumpDurationLevel} (spent {spent} coins).");
}

// 4. Verify live Player physics step moves cleanly & interacts with floor and jump sustain
{
    var player = new Player();
    player.Spawn(level.PlayerSpawnPx);
    var stats = new PlayerStats();
    var input = new InputState();
    using var audio = new AudioSynthesizer();
    var particles = new List<Particle>();

    // Settle onto ground
    for (int i = 0; i < 10; i++)
    {
        player.Update(1f / 60f, input, stats, level, audio, particles);
    }

    Assert(player.OnGround, "Player must settle onto spawn floor");
    Assert(MathF.Abs(player.Position.Y - level.PlayerSpawnPx.Y) < 0.01f, "Player feet must align cleanly with floor top");
}

// 5. Verify Up direction keys do NOT trigger Jump and MainMenu / HowToPlay states exist
{
    Assert(!InputState.IsJumpKey(Keys.Up), "Up arrow key must NOT trigger jump");
    Assert(!InputState.IsJumpKey(Keys.W), "W key must NOT trigger jump");
    Assert(InputState.IsJumpKey(Keys.Space), "Space key must trigger jump");
    Assert(InputState.IsJumpKey(Keys.Z), "Z key must trigger jump");
    Assert(Enum.IsDefined(typeof(GameScreenState), GameScreenState.MainMenu), "MainMenu screen state must exist");
    Assert(Enum.IsDefined(typeof(GameScreenState), GameScreenState.HowToPlay), "HowToPlay screen state must exist");
}

if (failures > 0)
{
    Console.Error.WriteLine($"Tests FAILED with {failures} error(s).");
    return 1;
}

Console.WriteLine("ALL TESTS PASSED!");
return 0;
