namespace EscapeToTheSkies;

public enum StatType
{
    Speed = 0,
    JumpHeight = 1,
    JumpDuration = 2,
}

public readonly record struct JumpEnvelope(float MaxHeightPx, float MaxDistancePx, float AirTimeSec);

public sealed class PlayerStats
{
    public const int MaxLevel = 5;

    // Cost to upgrade from level i -> i + 1
    public static readonly int[] UpgradeCosts = { 4, 8, 14, 22, 32 };

    // 1. Speed stat (px/s in 640x400 virtual world)
    public static readonly float[] RunSpeeds = { 108f, 135f, 162f, 190f, 220f, 255f };
    public static readonly float[] GroundAccels = { 860f, 1060f, 1280f, 1520f, 1800f, 2120f };
    public static readonly float[] AirAccels = { 620f, 780f, 950f, 1140f, 1360f, 1620f };

    // 2. Jump Height stat (initial launch speed px/s)
    public static readonly float[] JumpVelocities = { 230f, 270f, 310f, 350f, 392f, 435f };

    // 3. Jump Duration stat (held low-gravity sustain duration in seconds)
    public static readonly float[] JumpSustainTimes = { 0.06f, 0.15f, 0.24f, 0.33f, 0.42f, 0.52f };

    // Shared gravity constants used by both live Player physics and jump reach simulation
    public const float BaseGravity = 960f;
    public const float SustainGravityScale = 0.26f;
    public const float ApexGravityScale = 0.72f;
    public const float FallGravityScale = 1.12f;
    public const float ApexThresholdVy = 46f;
    public const float MaxFallSpeed = 420f;
    public const float EarlyReleaseCut = 0.52f;

    public int SpeedLevel { get; private set; }
    public int JumpHeightLevel { get; private set; }
    public int JumpDurationLevel { get; private set; }

    public int Coins { get; set; }
    public int TotalCoinsCollected { get; set; }
    public int UpgradesPurchased { get; private set; }

    public float RunSpeed => RunSpeeds[SpeedLevel];
    public float GroundAccel => GroundAccels[SpeedLevel];
    public float AirAccel => AirAccels[SpeedLevel];
    public float JumpVelocity => JumpVelocities[JumpHeightLevel];
    public float JumpSustainTime => JumpSustainTimes[JumpDurationLevel];

    public void Reset()
    {
        SpeedLevel = 0;
        JumpHeightLevel = 0;
        JumpDurationLevel = 0;
        Coins = 0;
        TotalCoinsCollected = 0;
        UpgradesPurchased = 0;
    }

    public int GetLevel(StatType stat) => stat switch
    {
        StatType.Speed => SpeedLevel,
        StatType.JumpHeight => JumpHeightLevel,
        StatType.JumpDuration => JumpDurationLevel,
        _ => 0,
    };

    public bool IsMaxed(StatType stat) => GetLevel(stat) >= MaxLevel;

    public int GetNextCost(StatType stat)
    {
        int lv = GetLevel(stat);
        return lv >= MaxLevel ? 0 : UpgradeCosts[lv];
    }

    public bool CanAfford(StatType stat) => !IsMaxed(stat) && Coins >= GetNextCost(stat);

    public bool TryUpgrade(StatType stat)
    {
        if (!CanAfford(stat))
        {
            return false;
        }

        Coins -= GetNextCost(stat);
        UpgradesPurchased++;
        switch (stat)
        {
            case StatType.Speed:
                SpeedLevel++;
                break;
            case StatType.JumpHeight:
                JumpHeightLevel++;
                break;
            case StatType.JumpDuration:
                JumpDurationLevel++;
                break;
        }

        return true;
    }

    public static string GetDisplayName(StatType stat) => stat switch
    {
        StatType.Speed => "MOVE SPEED",
        StatType.JumpHeight => "JUMP HEIGHT",
        StatType.JumpDuration => "JUMP DURATION",
        _ => "",
    };

    public static string GetDescription(StatType stat) => stat switch
    {
        StatType.Speed => "Run faster & leap farther across wide gaps",
        StatType.JumpHeight => "Stronger upward spring to reach high ledges",
        StatType.JumpDuration => "Hold Jump longer for extended skyward lift",
        _ => "",
    };

    public static string GetValueSummary(StatType stat, int level) => stat switch
    {
        StatType.Speed => $"{RunSpeeds[level]:0} px/s",
        StatType.JumpHeight => $"{JumpVelocities[level]:0} impulse",
        StatType.JumpDuration => $"{JumpSustainTimes[level]:0.00}s lift",
        _ => "",
    };

    public static JumpEnvelope SimulateMaxJump(int speedLevel, int jumpHeightLevel, int jumpDurationLevel)
    {
        const float dt = 1f / 60f;
        float vx = RunSpeeds[speedLevel];
        float vy = -JumpVelocities[jumpHeightLevel];
        float sustainLeft = JumpSustainTimes[jumpDurationLevel];
        float x = 0f;
        float y = 0f;
        float minY = 0f;
        float t = 0f;

        for (int step = 0; step < 300; step++)
        {
            float gScale = 1f;
            if (vy < 0f && sustainLeft > 0f)
            {
                gScale = SustainGravityScale;
                sustainLeft -= dt;
            }
            else if (MathF.Abs(vy) < ApexThresholdVy)
            {
                gScale = ApexGravityScale;
            }
            else if (vy > 0f)
            {
                gScale = FallGravityScale;
            }

            vy = MathF.Min(MaxFallSpeed, vy + BaseGravity * gScale * dt);
            x += vx * dt;
            y += vy * dt;
            t += dt;

            if (y < minY)
            {
                minY = y;
            }

            if (y >= 0f && vy > 0f)
            {
                break;
            }
        }

        return new JumpEnvelope(-minY, x, t);
    }
}
