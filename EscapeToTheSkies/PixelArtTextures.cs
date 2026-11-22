using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EscapeToTheSkies;

public enum TileType : byte
{
    Empty = 0,
    RuinStone = 1,       // Solid dark chasm carved brick
    RuinMossStone = 2,   // Solid ruin block with bioluminescent moss top
    RuinPlatform = 3,    // One-way cracked ancient stone slab
    ForestLog = 4,       // Solid ancient woodland timber / earth
    ForestGrass = 5,     // Solid grassy forest block
    ForestBranch = 6,    // One-way leafy bough / wooden bridge
    SkyMarble = 7,       // Solid sunlit ivory-gold sky stone
    CloudPlatform = 8,   // One-way radiant cloudstone ledge
}

public sealed class PixelArtTextures : IDisposable
{
    public const int TileSize = 16;

    public Texture2D WhitePixel { get; }
    public Texture2D TileAtlas { get; }
    public Texture2D CoinAtlas { get; }
    public Texture2D PlayerAtlas { get; }
    public Texture2D SoftGlow { get; }
    public Texture2D GoalArch { get; }

    public PixelArtTextures(GraphicsDevice gd)
    {
        WhitePixel = new Texture2D(gd, 1, 1);
        WhitePixel.SetData(new[] { Color.White });

        TileAtlas = BuildTileAtlas(gd);
        CoinAtlas = BuildCoinAtlas(gd);
        PlayerAtlas = BuildPlayerAtlas(gd);
        SoftGlow = BuildSoftGlow(gd, 32);
        GoalArch = BuildGoalArch(gd);
    }

    public static Rectangle GetTileSource(TileType tile, int variant = 0)
    {
        int col = (int)tile;
        int row = Math.Abs(variant) % 2;
        return new Rectangle(col * TileSize, row * TileSize, TileSize, TileSize);
    }

    public static Rectangle GetCoinSource(int tierIndex, int frame)
    {
        // tierIndex: 0 (+1 bronze), 1 (+3 gold), 2 (+5 sky relic)
        // frame: 0..3
        return new Rectangle((frame % 4) * 12, tierIndex * 12, 12, 12);
    }

    public static Rectangle GetPlayerSource(int animFrame)
    {
        // 0: idle, 1..4: run, 5: jump up, 6: fall, 7: sustain glide
        return new Rectangle((animFrame % 8) * 16, 0, 16, 16);
    }

    private static Texture2D BuildTileAtlas(GraphicsDevice gd)
    {
        const int cols = 9;
        const int rows = 2;
        int w = cols * TileSize;
        int h = rows * TileSize;
        var px = new Color[w * h];

        for (int row = 0; row < rows; row++)
        {
            DrawRuinStone(px, w, 1 * TileSize, row * TileSize, row, withMoss: false);
            DrawRuinStone(px, w, 2 * TileSize, row * TileSize, row, withMoss: true);
            DrawOneWayStone(px, w, 3 * TileSize, row * TileSize, row);
            DrawForestTimber(px, w, 4 * TileSize, row * TileSize, row, withGrass: false);
            DrawForestTimber(px, w, 5 * TileSize, row * TileSize, row, withGrass: true);
            DrawForestBranch(px, w, 6 * TileSize, row * TileSize, row);
            DrawSkyMarble(px, w, 7 * TileSize, row * TileSize, row);
            DrawCloudPlatform(px, w, 8 * TileSize, row * TileSize, row);
        }

        var tex = new Texture2D(gd, w, h);
        tex.SetData(px);
        return tex;
    }

    private static void DrawRuinStone(Color[] px, int stride, int ox, int oy, int variant, bool withMoss)
    {
        Color dark = new(24, 22, 42);
        Color baseStone = new(44, 42, 74);
        Color midStone = new(62, 60, 98);
        Color hiStone = new(86, 86, 130);
        Color runeGlow = new(82, 210, 232);
        Color mossDark = new(26, 98, 92);
        Color mossLight = new(62, 178, 146);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                bool border = x == 0 || y == 0 || x == 15 || y == 15;
                bool brickSeam = y == 7 || (y < 7 && x == 8) || (y > 7 && x == (variant == 0 ? 4 : 12));
                Color c = baseStone;

                if (border || brickSeam)
                {
                    c = dark;
                }
                else if (y == 1 || y == 8)
                {
                    c = hiStone;
                }
                else if (((x * 7 + y * 13 + variant * 5) % 11) == 0)
                {
                    c = midStone;
                }

                // Subtle carved rune dot on variant 1
                if (variant == 1 && !withMoss && x >= 10 && x <= 12 && y >= 10 && y <= 12 && (x == 11 || y == 11))
                {
                    c = runeGlow * 0.65f;
                    c.A = 255;
                }

                if (withMoss)
                {
                    int mossDepth = ((x + variant * 3) % 5 == 0) ? 4 : (((x * 3) % 4 == 0) ? 2 : 3);
                    if (y == 0) c = mossLight;
                    else if (y < mossDepth) c = (x % 3 == 0) ? mossLight : mossDark;
                    else if (y == mossDepth && x % 2 == 0) c = dark;
                }

                px[(oy + y) * stride + (ox + x)] = c;
            }
        }
    }

    private static void DrawOneWayStone(Color[] px, int stride, int ox, int oy, int variant)
    {
        Color outline = new(22, 20, 38);
        Color topHi = new(118, 120, 166);
        Color body = new(74, 72, 114);
        Color underside = new(44, 42, 72);
        Color crystal = new(110, 224, 245);

        for (int y = 0; y < 7; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Color c = Color.Transparent;
                if (y == 0)
                {
                    c = (x == 0 || x == 15) ? outline : topHi;
                }
                else if (y == 1)
                {
                    c = (x == 0 || x == 15) ? outline : ((x % 6 == 0) ? topHi : body);
                }
                else if (y <= 4)
                {
                    c = (x == 0 || x == 15 || x == 7 + variant) ? outline : body;
                    if (y == 3 && (x == 3 || x == 12)) c = crystal * 0.7f;
                    if (c != Color.Transparent) c.A = 255;
                }
                else if (y == 5)
                {
                    if (x >= 1 && x <= 14) c = underside;
                }
                else if (x >= 2 && x <= 13 && x % 3 != 0)
                {
                    c = outline;
                }

                px[(oy + y) * stride + (ox + x)] = c;
            }
        }
    }

    private static void DrawForestTimber(Color[] px, int stride, int ox, int oy, int variant, bool withGrass)
    {
        Color barkDark = new(42, 28, 22);
        Color barkMid = new(82, 54, 38);
        Color barkLight = new(114, 78, 52);
        Color grassDark = new(38, 116, 56);
        Color grassMid = new(76, 174, 74);
        Color grassHi = new(134, 218, 96);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Color c = barkMid;
                if (x == 0 || y == 0 || x == 15 || y == 15)
                {
                    c = barkDark;
                }
                else if (((x + variant * 2) % 4 == 0 && y % 5 != 0) || y == 1)
                {
                    c = barkLight;
                }
                else if ((x * 5 + y * 3) % 9 == 0)
                {
                    c = barkDark;
                }

                if (withGrass)
                {
                    int depth = 3 + ((x * 3 + variant) % 3);
                    if (y == 0) c = grassHi;
                    else if (y == 1) c = (x % 3 == 0) ? grassHi : grassMid;
                    else if (y < depth) c = (x % 2 == 0) ? grassMid : grassDark;
                    else if (y == depth) c = barkDark;
                }

                px[(oy + y) * stride + (ox + x)] = c;
            }
        }
    }

    private static void DrawForestBranch(Color[] px, int stride, int ox, int oy, int variant)
    {
        Color outline = new(34, 24, 18);
        Color woodHi = new(144, 100, 62);
        Color wood = new(104, 68, 42);
        Color leaf = new(72, 172, 74);
        Color leafHi = new(138, 220, 96);

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Color c = Color.Transparent;
                if (y == 0)
                {
                    c = (x % 4 == (variant == 0 ? 1 : 2)) ? leafHi : ((x % 2 == 0) ? leaf : woodHi);
                }
                else if (y == 1)
                {
                    c = (x == 0 || x == 15) ? outline : woodHi;
                }
                else if (y <= 4)
                {
                    c = (x == 0 || x == 15) ? outline : ((y == 3 && x % 5 == 0) ? woodHi : wood);
                }
                else if (y == 5 && x >= 1 && x <= 14)
                {
                    c = outline;
                }
                else if (y >= 6 && ((x >= 2 && x <= 5) || (x >= 10 && x <= 13)))
                {
                    c = (y == 6) ? leaf : outline;
                }

                px[(oy + y) * stride + (ox + x)] = c;
            }
        }
    }

    private static void DrawSkyMarble(Color[] px, int stride, int ox, int oy, int variant)
    {
        Color border = new(96, 134, 174);
        Color marble = new(218, 234, 248);
        Color marbleShade = new(176, 204, 232);
        Color marbleHi = new(246, 252, 255);
        Color goldTrim = new(238, 198, 92);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Color c = marble;
                if (x == 0 || y == 0 || x == 15 || y == 15)
                {
                    c = border;
                }
                else if (y == 1 || x == 1)
                {
                    c = marbleHi;
                }
                else if (y == 2 && (x % 2 == 0 || variant == 1))
                {
                    c = goldTrim;
                }
                else if (y >= 12 || (y == 8 && x % 4 != 0))
                {
                    c = marbleShade;
                }

                px[(oy + y) * stride + (ox + x)] = c;
            }
        }
    }

    private static void DrawCloudPlatform(Color[] px, int stride, int ox, int oy, int variant)
    {
        Color outline = new(118, 178, 224);
        Color cloudHi = new(252, 254, 255);
        Color cloudMid = new(226, 244, 255);
        Color cloudLow = new(174, 214, 246);

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float puff = MathF.Sin((x + variant * 3) * 0.48f);
                int topY = puff > 0.35f ? 0 : 1;
                int botY = puff < -0.2f ? 6 : 7;

                Color c = Color.Transparent;
                if (y == topY && x >= 1 && x <= 14) c = cloudHi;
                else if (y > topY && y < botY - 1) c = (y <= 2) ? cloudHi : cloudMid;
                else if (y == botY - 1 && x >= 1 && x <= 14) c = cloudLow;
                else if (y == botY && x >= 2 && x <= 13) c = outline;

                if ((x == 0 || x == 15) && y >= 2 && y <= 4) c = outline;

                px[(oy + y) * stride + (ox + x)] = c;
            }
        }
    }

    private static Texture2D BuildCoinAtlas(GraphicsDevice gd)
    {
        // 3 tiers (rows) x 4 spin frames (cols), each 12x12
        int w = 4 * 12;
        int h = 3 * 12;
        var px = new Color[w * h];

        var palettes = new (Color Edge, Color Main, Color Hi, Color Core)[]
        {
            (new Color(120, 62, 24), new Color(214, 130, 56), new Color(252, 198, 120), new Color(255, 228, 178)),   // +1 Bronze
            (new Color(152, 92, 14), new Color(246, 194, 42), new Color(255, 238, 118), new Color(255, 252, 200)),   // +3 Gold
            (new Color(44, 122, 166), new Color(110, 226, 252), new Color(224, 252, 255), new Color(255, 240, 120)), // +5 Sky Relic
        };

        float[] widthScales = { 1.0f, 0.72f, 0.34f, 0.72f };

        for (int tier = 0; tier < 3; tier++)
        {
            var pal = palettes[tier];
            for (int frame = 0; frame < 4; frame++)
            {
                int ox = frame * 12;
                int oy = tier * 12;
                float rx = 4.6f * widthScales[frame];
                float ry = tier == 2 ? 5.1f : 4.6f;

                for (int y = 0; y < 12; y++)
                {
                    for (int x = 0; x < 12; x++)
                    {
                        float dx = (x - 5.5f) / MathF.Max(1.1f, rx);
                        float dy = (y - 5.5f) / ry;
                        float dist = tier == 2
                            ? MathF.Abs(dx) * 0.78f + MathF.Abs(dy) * 0.78f // Diamond-like relic coin
                            : MathF.Sqrt(dx * dx + dy * dy);

                        Color c = Color.Transparent;
                        if (dist <= 1.08f)
                        {
                            if (dist >= 0.80f) c = pal.Edge;
                            else if (dx + dy < -0.35f) c = pal.Hi;
                            else if (MathF.Abs(dx) < 0.25f && MathF.Abs(dy) < 0.45f) c = pal.Core;
                            else c = pal.Main;
                        }

                        px[(oy + y) * w + (ox + x)] = c;
                    }
                }
            }
        }

        var tex = new Texture2D(gd, w, h);
        tex.SetData(px);
        return tex;
    }

    private static Texture2D BuildPlayerAtlas(GraphicsDevice gd)
    {
        // 8 frames of 16x16 sprites facing right
        int w = 8 * 16;
        int h = 16;
        var px = new Color[w * h];

        for (int f = 0; f < 8; f++)
        {
            DrawPlayerFrame(px, w, f * 16, f);
        }

        var tex = new Texture2D(gd, w, h);
        tex.SetData(px);
        return tex;
    }

    private static void DrawPlayerFrame(Color[] px, int stride, int ox, int frame)
    {
        Color outline = new(18, 16, 28);
        Color cloak = new(56, 132, 206);
        Color cloakHi = new(102, 186, 246);
        Color scarf = frame == 7 ? new Color(138, 250, 255) : new Color(242, 114, 72);
        Color scarfHi = frame == 7 ? new Color(230, 255, 255) : new Color(255, 184, 112);
        Color skin = new(246, 208, 174);
        Color eye = new(28, 24, 42);
        Color boot = frame == 7 ? new Color(110, 244, 255) : new Color(76, 54, 44);

        void Plot(int x, int y, Color c)
        {
            if (x >= 0 && x < 16 && y >= 0 && y < 16)
            {
                px[y * stride + (ox + x)] = c;
            }
        }

        int bobY = (frame == 1 || frame == 3) ? -1 : 0;
        if (frame == 5 || frame == 7) bobY = -1;

        // Scarf tail trailing left
        int scarfWave = (frame == 2 || frame == 4 || frame == 7) ? -1 : 0;
        for (int sx = 2; sx <= 5; sx++)
        {
            Plot(sx, 7 + bobY + (sx < 4 ? scarfWave : 0), sx == 2 ? scarfHi : scarf);
            Plot(sx, 8 + bobY + (sx < 4 ? scarfWave : 0), outline);
        }

        // Hood & Head
        for (int y = 2 + bobY; y <= 7 + bobY; y++)
        {
            for (int x = 5; x <= 11; x++)
            {
                if ((y == 2 + bobY || y == 7 + bobY) && (x == 5 || x == 11)) continue;
                bool edge = x == 5 || x == 11 || y == 2 + bobY || y == 7 + bobY;
                Color c = edge ? outline : (y == 3 + bobY ? cloakHi : cloak);

                // Face opening on right side
                if (x >= 8 && x <= 10 && y >= 4 + bobY && y <= 6 + bobY)
                {
                    c = skin;
                    if (x == 10 && y == 5 + bobY) c = eye;
                }
                Plot(x, y, c);
            }
        }

        // Scarf collar
        for (int x = 5; x <= 10; x++)
        {
            Plot(x, 7 + bobY, scarf);
        }

        // Torso cloak
        for (int y = 8 + bobY; y <= 11 + bobY; y++)
        {
            for (int x = 5; x <= 10; x++)
            {
                bool edge = x == 5 || x == 10;
                Plot(x, y, edge ? outline : (x <= 7 ? cloakHi : cloak));
            }
        }

        // Legs / Boots per animation frame
        int leftFootX = 6;
        int rightFootX = 9;
        int leftFootY = 14;
        int rightFootY = 14;

        switch (frame)
        {
            case 1: leftFootX = 4; rightFootX = 10; leftFootY = 13; rightFootY = 14; break;
            case 2: leftFootX = 5; rightFootX = 9; break;
            case 3: leftFootX = 10; rightFootX = 5; leftFootY = 14; rightFootY = 13; break;
            case 4: leftFootX = 6; rightFootX = 9; break;
            case 5: leftFootX = 5; rightFootX = 10; leftFootY = 12; rightFootY = 13; break;
            case 6: leftFootX = 5; rightFootX = 10; leftFootY = 14; rightFootY = 13; break;
            case 7: leftFootX = 5; rightFootX = 10; leftFootY = 13; rightFootY = 13; break;
        }

        for (int fy = 12 + bobY; fy <= leftFootY; fy++)
        {
            Plot(leftFootX, fy, fy >= leftFootY - 1 ? boot : outline);
            Plot(leftFootX + 1, fy, fy >= leftFootY - 1 ? boot : cloak);
        }
        for (int fy = 12 + bobY; fy <= rightFootY; fy++)
        {
            Plot(rightFootX, fy, fy >= rightFootY - 1 ? boot : cloak);
            Plot(rightFootX + 1, fy, fy >= rightFootY - 1 ? boot : outline);
        }
    }

    private static Texture2D BuildSoftGlow(GraphicsDevice gd, int size)
    {
        var px = new Color[size * size];
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                float a = Math.Clamp(1f - d, 0f, 1f);
                a = a * a;
                px[y * size + x] = Color.White * a;
            }
        }

        var tex = new Texture2D(gd, size, size);
        tex.SetData(px);
        return tex;
    }

    private static Texture2D BuildGoalArch(GraphicsDevice gd)
    {
        // 48x56 pixel radiant Sky Escape Gate
        const int w = 48;
        const int h = 56;
        var px = new Color[w * h];

        Color border = new(82, 118, 162);
        Color marble = new(232, 244, 255);
        Color marbleShade = new(176, 206, 236);
        Color gold = new(248, 208, 84);
        Color portalOuter = new(102, 224, 255, 190);
        Color portalInner = new(238, 254, 255, 220);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = Color.Transparent;
                float cx = x - 23.5f;
                float cy = y - 22f;
                bool inArchOpening = MathF.Abs(cx) <= 12f && y >= 14 && (y >= 22 || (cx * cx + cy * cy <= 12f * 12f));
                bool inArchRim = MathF.Abs(cx) <= 18f && y >= 6 && (y >= 22 || (cx * cx + cy * cy <= 18.5f * 18.5f));

                if (y >= 50 && x >= 2 && x <= 45)
                {
                    c = (y == 50 || y == 55 || x == 2 || x == 45) ? border : (y == 51 ? gold : marble);
                }
                else if (inArchOpening)
                {
                    float core = 1f - MathF.Abs(cx) / 12f;
                    c = core > 0.45f ? portalInner : portalOuter;
                }
                else if (inArchRim)
                {
                    bool outerEdge = y == 6 || x == 6 || x == 41 || (y < 22 && cx * cx + cy * cy >= 16.5f * 16.5f);
                    bool innerGold = (y < 22 && cx * cx + cy * cy <= 14.5f * 14.5f) || MathF.Abs(cx) >= 12f && MathF.Abs(cx) <= 13.5f;
                    c = outerEdge ? border : (innerGold ? gold : (cx < 0 ? marble : marbleShade));
                }

                px[y * w + x] = c;
            }
        }

        var tex = new Texture2D(gd, w, h);
        tex.SetData(px);
        return tex;
    }

    public void Dispose()
    {
        WhitePixel.Dispose();
        TileAtlas.Dispose();
        CoinAtlas.Dispose();
        PlayerAtlas.Dispose();
        SoftGlow.Dispose();
        GoalArch.Dispose();
    }
}
