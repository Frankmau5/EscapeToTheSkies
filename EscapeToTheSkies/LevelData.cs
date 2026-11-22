using Microsoft.Xna.Framework;

namespace EscapeToTheSkies;

public sealed class CoinSpawn
{
    public int Id { get; }
    public Vector2 Position { get; }
    public int Value { get; }
    public bool Collected { get; set; }

    public CoinSpawn(int id, Vector2 position, int value)
    {
        Id = id;
        Position = position;
        Value = value;
    }

    public int TierIndex => Value >= 5 ? 2 : (Value >= 3 ? 1 : 0);
}

public readonly record struct DecorationProp(PropKind Kind, Vector2 Position, int LengthTiles);

public enum PropKind
{
    Crystal,
    Lantern,
    Vine,
}

public sealed class LevelData
{
    public const int TileSize = PixelArtTextures.TileSize; // 16
    public const int Width = 56;   // 896 px
    public const int Height = 198; // 3168 px

    public const int WorldWidthPx = Width * TileSize;
    public const int WorldHeightPx = Height * TileSize;

    private readonly TileType[,] _tiles = new TileType[Width, Height];
    public List<CoinSpawn> Coins { get; } = new();
    public List<DecorationProp> Props { get; } = new();

    public Vector2 PlayerSpawnPx { get; private set; }
    public Rectangle GoalTriggerPx { get; private set; }
    public Vector2 GoalArchBasePx { get; private set; }

    public LevelData()
    {
        BuildLevel();
    }

    public TileType GetTile(int tx, int ty)
    {
        if (tx < 0 || tx >= Width || ty < 0 || ty >= Height)
        {
            return TileType.Empty;
        }
        return _tiles[tx, ty];
    }

    public static bool IsSolid(TileType t) => t is
        TileType.RuinStone or
        TileType.RuinMossStone or
        TileType.ForestLog or
        TileType.ForestGrass or
        TileType.SkyMarble;

    public static bool IsOneWay(TileType t) => t is
        TileType.RuinPlatform or
        TileType.ForestBranch or
        TileType.CloudPlatform;

    public static bool IsStandable(TileType t) => IsSolid(t) || IsOneWay(t);

    public int TotalPossibleCoins
    {
        get
        {
            int sum = 0;
            foreach (CoinSpawn c in Coins) sum += c.Value;
            return sum;
        }
    }

    public void ResetCoins()
    {
        foreach (CoinSpawn c in Coins)
        {
            c.Collected = false;
        }
    }

    private void FillRect(int x1, int y1, int x2, int y2, TileType type)
    {
        for (int y = y1; y <= y2; y++)
        {
            for (int x = x1; x <= x2; x++)
            {
                if (x >= 0 && x < Width && y >= 0 && y < Height)
                {
                    _tiles[x, y] = type;
                }
            }
        }
    }

    private void Platform(int x1, int x2, int y, TileType type) => FillRect(x1, y, x2, y, type);

    private void Coin(int tx, int ty, int value)
    {
        int id = Coins.Count;
        Coins.Add(new CoinSpawn(id, new Vector2(tx * TileSize + TileSize * 0.5f, ty * TileSize + TileSize * 0.5f), value));
    }

    private void CoinRow(int xStart, int xEnd, int step, int ty, int value)
    {
        for (int x = xStart; x <= xEnd; x += step)
        {
            Coin(x, ty, value);
        }
    }

    private void Prop(PropKind kind, int tx, int ty, int len = 2)
    {
        Props.Add(new DecorationProp(kind, new Vector2(tx * TileSize, ty * TileSize), len));
    }

    private void BuildLevel()
    {
        // =========================================================================
        // BOUNDARY WALLS & CHASM FLOOR
        // =========================================================================
        // Deep Chasm bedrock floor (rows 195..197)
        FillRect(0, 195, Width - 1, Height - 1, TileType.RuinStone);
        FillRect(2, 195, Width - 3, 195, TileType.RuinMossStone);

        // Chasm underground side walls (rows 132..197)
        FillRect(0, 132, 1, 197, TileType.RuinStone);
        FillRect(Width - 2, 132, Width - 1, 197, TileType.RuinStone);

        // Forest trunk side boundaries (rows 66..131)
        FillRect(0, 66, 1, 131, TileType.ForestLog);
        FillRect(Width - 2, 66, Width - 1, 131, TileType.ForestLog);

        // Sky zone (rows 0..65) opens cleanly to open air!

        // Player starts at bottom center of the Deep Chasm
        PlayerSpawnPx = new Vector2(27.5f * TileSize, 195f * TileSize);

        // =========================================================================
        // ZONE 1: THE DEEP CHASM / ANCIENT STONE RUINS (rows 195 -> 132)
        // =========================================================================
        // --- 1A: Starter Atrium (Lv 0,0,0 reachable, dy <= 2, ~21 coins!) ---
        Platform(24, 31, 193, TileType.RuinMossStone);
        Platform(15, 21, 193, TileType.RuinPlatform);
        Platform(34, 40, 193, TileType.RuinPlatform);
        CoinRow(16, 20, 2, 192, 1); // +3
        CoinRow(35, 39, 2, 192, 1); // +3

        Platform(7, 13, 191, TileType.RuinPlatform);
        Platform(25, 30, 191, TileType.RuinPlatform);
        Platform(42, 48, 191, TileType.RuinPlatform);
        CoinRow(8, 12, 2, 190, 1);  // +3
        Coin(27, 190, 1);           // +1
        CoinRow(43, 47, 2, 190, 1); // +3

        Platform(2, 6, 189, TileType.RuinMossStone);
        Platform(26, 29, 189, TileType.RuinPlatform);
        Platform(49, 53, 189, TileType.RuinMossStone);
        Coin(4, 188, 3);  // +3 Gold
        Coin(27, 188, 3); // +3 Gold
        Coin(51, 188, 3); // +3 Gold
        // Total in Starter Atrium (Lv 0): 22 coins! (Enough for Lv1 in all 3 stats + spare!)

        Prop(PropKind.Crystal, 4, 194);
        Prop(PropKind.Crystal, 51, 194);
        Prop(PropKind.Lantern, 23, 194);
        Prop(PropKind.Lantern, 32, 194);
        Prop(PropKind.Vine, 10, 187, 3);
        Prop(PropKind.Vine, 45, 187, 3);

        // --- 1B: Lower Ruin Balconies (dy = 3, needs Lv 1 Height or Duration) ---
        Platform(11, 19, 186, TileType.RuinPlatform);
        Platform(23, 32, 186, TileType.RuinPlatform);
        Platform(36, 44, 186, TileType.RuinPlatform);
        Coin(14, 185, 1);
        Coin(17, 185, 1);
        Coin(26, 185, 1);
        Coin(29, 185, 1);
        Coin(38, 185, 1);
        Coin(41, 185, 1);

        // Left Speed/Duration Ruin Alcove (5-tile horizontal jump from x=11 to x=6)
        Platform(2, 6, 185, TileType.RuinMossStone);
        Platform(2, 8, 181, TileType.RuinMossStone);
        Platform(11, 16, 178, TileType.RuinPlatform);
        Coin(4, 184, 1);
        Coin(5, 180, 3);
        Coin(13, 177, 3);
        Prop(PropKind.Crystal, 3, 180);

        // Central Ruin Climb (dy = 3..4)
        Platform(18, 23, 183, TileType.RuinPlatform);
        Platform(32, 37, 183, TileType.RuinPlatform);
        Coin(20, 182, 1);
        Coin(35, 182, 1);

        FillRect(25, 180, 30, 181, TileType.RuinStone);
        Platform(25, 30, 180, TileType.RuinMossStone);
        Coin(27, 179, 3);
        Prop(PropKind.Lantern, 25, 179);
        Prop(PropKind.Lantern, 30, 179);

        Platform(15, 21, 177, TileType.RuinPlatform);
        Platform(34, 40, 177, TileType.RuinPlatform);
        Coin(17, 176, 1);
        Coin(38, 176, 1);

        // Side step stones for smooth Lv1 ascent + Right Crystal Vault
        Platform(8, 13, 174, TileType.RuinPlatform);
        Platform(42, 47, 174, TileType.RuinPlatform);
        Platform(48, 53, 171, TileType.RuinMossStone);
        Coin(10, 173, 1);
        Coin(44, 173, 3);
        Coin(51, 170, 5); // +5 Ancient Relic!
        Prop(PropKind.Crystal, 49, 170);

        // --- 1C: Mid-Ruin Pillars & Vaults (rows 173 -> 150) ---
        Platform(23, 32, 173, TileType.RuinPlatform);
        Coin(26, 172, 1);
        Coin(29, 172, 1);

        Platform(14, 20, 170, TileType.RuinPlatform);
        Platform(35, 41, 170, TileType.RuinPlatform);
        Coin(17, 169, 1);
        Coin(38, 169, 1);

        FillRect(22, 166, 33, 167, TileType.RuinStone);
        Platform(22, 33, 166, TileType.RuinMossStone);
        Coin(27, 165, 3);

        // Left Ancient Treasury (wide gap from 14..20 at y=162 -> rewards Speed/Duration)
        Platform(14, 20, 162, TileType.RuinPlatform);
        Platform(35, 41, 162, TileType.RuinPlatform);
        Platform(2, 10, 162, TileType.RuinMossStone);
        Platform(2, 7, 158, TileType.RuinMossStone);
        Coin(6, 161, 3);
        Coin(8, 161, 3);
        Coin(4, 157, 5); // +5 Relic!
        Prop(PropKind.Crystal, 2, 161);
        Prop(PropKind.Vine, 12, 163, 4);

        Platform(24, 31, 158, TileType.RuinPlatform);
        Coin(26, 157, 1);
        Coin(29, 157, 1);

        Platform(13, 20, 154, TileType.RuinMossStone);
        Platform(35, 42, 154, TileType.RuinMossStone);
        Coin(16, 153, 3);
        Coin(39, 153, 3);

        Platform(23, 32, 150, TileType.RuinPlatform);
        Coin(25, 149, 1);
        Coin(29, 149, 1);

        // Right Hanging Vine Vault
        Platform(39, 45, 148, TileType.RuinPlatform);
        Platform(47, 53, 144, TileType.RuinMossStone);
        Coin(42, 147, 3);
        Coin(51, 143, 5); // +5 Relic!
        Prop(PropKind.Crystal, 48, 143);
        Prop(PropKind.Vine, 44, 149, 4);

        // --- 1D: Upper Ruin Exit -> Forest Roots (rows 146 -> 132) ---
        Platform(11, 18, 146, TileType.RuinPlatform);
        Platform(37, 44, 146, TileType.RuinPlatform);
        Coin(14, 145, 1);
        Coin(40, 145, 1);

        FillRect(21, 142, 34, 143, TileType.RuinStone);
        Platform(21, 34, 142, TileType.RuinMossStone);
        Coin(23, 141, 1);
        Coin(27, 141, 3);
        Coin(31, 141, 1);
        Prop(PropKind.Lantern, 21, 141);
        Prop(PropKind.Lantern, 34, 141);

        Platform(14, 20, 138, TileType.ForestBranch);
        Platform(35, 41, 138, TileType.ForestBranch);
        Coin(17, 137, 1);
        Coin(38, 137, 1);

        FillRect(23, 134, 32, 135, TileType.ForestLog);
        Platform(23, 32, 134, TileType.ForestGrass);
        Coin(27, 133, 3);

        // =========================================================================
        // ZONE 2: THE OVERGROWN FOREST LAYER (rows 132 -> 66)
        // =========================================================================
        Platform(12, 19, 129, TileType.ForestBranch);
        Platform(36, 43, 129, TileType.ForestBranch);
        Coin(15, 128, 3);
        Coin(40, 128, 3);

        FillRect(22, 124, 33, 125, TileType.ForestLog);
        Platform(22, 33, 124, TileType.ForestGrass);
        Coin(24, 123, 1);
        Coin(27, 123, 3);
        Coin(30, 123, 1);
        Prop(PropKind.Lantern, 22, 123);
        Prop(PropKind.Lantern, 33, 123);

        // Left Forest Speed Run Track (rows 120 -> 111)
        Platform(8, 16, 120, TileType.ForestBranch);
        Platform(2, 7, 116, TileType.ForestGrass);
        Platform(10, 17, 111, TileType.ForestBranch);
        Coin(12, 119, 3);
        Coin(4, 115, 5); // +5 Relic!
        Coin(12, 110, 1);
        Coin(15, 110, 3);
        Prop(PropKind.Vine, 11, 121, 4);

        // Right Hollow Tree Shaft (rows 120 -> 110)
        Platform(39, 46, 120, TileType.ForestBranch);
        FillRect(47, 115, 53, 117, TileType.ForestLog);
        Platform(47, 53, 115, TileType.ForestGrass);
        Platform(39, 45, 110, TileType.ForestBranch);
        Coin(42, 119, 3);
        Coin(51, 114, 5); // +5 Relic!
        Coin(42, 109, 3);

        // Central Canopy Express & Main Branches
        Platform(24, 31, 119, TileType.ForestBranch);
        Coin(27, 118, 1);

        Platform(18, 25, 114, TileType.ForestBranch);
        Platform(30, 37, 114, TileType.ForestBranch);
        Coin(21, 113, 3);
        Coin(34, 113, 3);

        FillRect(23, 109, 32, 110, TileType.ForestLog);
        Platform(23, 32, 109, TileType.ForestGrass);
        Coin(27, 108, 3);

        Platform(13, 20, 104, TileType.ForestBranch);
        Platform(35, 42, 104, TileType.ForestBranch);
        Coin(16, 103, 3);
        Coin(39, 103, 3);

        // Canopy Side Caches (rows 100 -> 99)
        Platform(3, 9, 100, TileType.ForestGrass);
        Platform(46, 52, 99, TileType.ForestGrass);
        Coin(5, 99, 5);
        Coin(50, 98, 5);
        Prop(PropKind.Vine, 5, 101, 4);
        Prop(PropKind.Vine, 50, 100, 4);

        Platform(24, 31, 100, TileType.ForestBranch);
        Coin(27, 99, 3);

        Platform(14, 21, 95, TileType.ForestBranch);
        Platform(34, 41, 95, TileType.ForestBranch);
        Coin(17, 94, 3);
        Coin(38, 94, 3);

        FillRect(22, 90, 33, 91, TileType.ForestLog);
        Platform(22, 33, 90, TileType.ForestGrass);
        Coin(24, 89, 1);
        Coin(27, 89, 3);
        Coin(30, 89, 1);

        Platform(11, 18, 85, TileType.ForestBranch);
        Platform(37, 44, 85, TileType.ForestBranch);
        Coin(14, 84, 3);
        Coin(41, 84, 3);

        // Upper Canopy Treasures
        Platform(2, 8, 80, TileType.ForestGrass);
        Platform(47, 53, 80, TileType.ForestGrass);
        Coin(5, 79, 5);
        Coin(50, 79, 5);

        Platform(24, 31, 80, TileType.ForestBranch);
        Coin(27, 79, 3);

        Platform(15, 22, 75, TileType.ForestBranch);
        Platform(33, 40, 75, TileType.ForestBranch);
        Coin(18, 74, 3);
        Coin(37, 74, 3);

        // Grand Treetop Terrace (threshold into the Bright Sky!)
        FillRect(21, 70, 34, 71, TileType.ForestLog);
        Platform(21, 34, 70, TileType.ForestGrass);
        Coin(24, 69, 3);
        Coin(27, 69, 5);
        Coin(31, 69, 3);
        Prop(PropKind.Lantern, 21, 69);
        Prop(PropKind.Lantern, 34, 69);

        // =========================================================================
        // ZONE 3: THE BRIGHT SKIES & SKY MARBLE SUMMIT (rows 66 -> 8)
        // =========================================================================
        Platform(13, 20, 64, TileType.CloudPlatform);
        Platform(35, 42, 64, TileType.CloudPlatform);
        Coin(16, 63, 3);
        Coin(39, 63, 3);

        FillRect(23, 58, 32, 59, TileType.SkyMarble);
        Coin(27, 57, 3);

        // Left & Right Cloud Wings (wide sky jumps!)
        Platform(4, 11, 53, TileType.CloudPlatform);
        Platform(44, 51, 53, TileType.CloudPlatform);
        Coin(7, 52, 3);
        Coin(48, 52, 3);

        Platform(14, 20, 52, TileType.CloudPlatform);
        Platform(35, 41, 52, TileType.CloudPlatform);
        Coin(17, 51, 3);
        Coin(38, 51, 3);

        FillRect(3, 46, 9, 47, TileType.SkyMarble);
        FillRect(46, 46, 52, 47, TileType.SkyMarble);
        Coin(6, 45, 5);
        Coin(49, 45, 5);

        FillRect(24, 46, 31, 47, TileType.SkyMarble);
        Coin(27, 45, 5);

        Platform(12, 19, 40, TileType.CloudPlatform);
        Platform(36, 43, 40, TileType.CloudPlatform);
        Coin(15, 39, 3);
        Coin(40, 39, 3);

        // Outer Cloud Shrines
        Platform(2, 8, 34, TileType.CloudPlatform);
        Platform(47, 53, 34, TileType.CloudPlatform);
        Coin(5, 33, 5);
        Coin(50, 33, 5);

        FillRect(22, 33, 33, 34, TileType.SkyMarble);
        Coin(25, 32, 3);
        Coin(29, 32, 3);

        Platform(13, 20, 26, TileType.CloudPlatform);
        Platform(35, 42, 26, TileType.CloudPlatform);
        Coin(16, 25, 5);
        Coin(39, 25, 5);

        Platform(23, 32, 19, TileType.CloudPlatform);
        Coin(27, 18, 5);

        // --- GRAND SUMMIT DAIS & SKY ESCAPE GATE (rows 12..13) ---
        FillRect(21, 12, 34, 13, TileType.SkyMarble);
        Platform(22, 33, 12, TileType.SkyMarble);

        // Sky Escape Gate centered at tile x = 28 (448 px)
        float archCenterX = 28f * TileSize;
        float archBaseY = 12f * TileSize;
        GoalArchBasePx = new Vector2(archCenterX, archBaseY);
        GoalTriggerPx = new Rectangle((int)(archCenterX - 14), (int)(archBaseY - 44), 28, 44);
    }
}
