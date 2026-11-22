using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EscapeToTheSkies;

public sealed class BackgroundRenderer
{
    private readonly struct AmbientMote
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Speed;
        public readonly float Phase;
        public readonly float Size;
        public AmbientMote(float x, float y, float speed, float phase, float size)
        {
            X = x; Y = y; Speed = speed; Phase = phase; Size = size;
        }
    }

    private readonly AmbientMote[] _motes;

    public BackgroundRenderer()
    {
        var rng = new Random(20261121);
        _motes = new AmbientMote[90];
        for (int i = 0; i < _motes.Length; i++)
        {
            _motes[i] = new AmbientMote(
                (float)rng.NextDouble() * 640f,
                (float)rng.NextDouble() * 400f,
                8f + (float)rng.NextDouble() * 20f,
                (float)(rng.NextDouble() * Math.PI * 2.0),
                rng.NextDouble() > 0.72 ? 2f : 1f
            );
        }
    }

    /// <summary>
    /// Normalized climb height from 0.0 (deep underground chasm) through ~0.5 (forest layer) to 1.0 (bright sunlit sky).
    /// </summary>
    public static float ComputeHeightProgress(float cameraCenterY, float worldHeightPx)
    {
        float p = 1f - (cameraCenterY / MathF.Max(1f, worldHeightPx));
        return Math.Clamp(p, 0f, 1f);
    }

    public static string GetZoneName(float progress)
    {
        if (progress < 0.34f) return "THE DEEP CHASM";
        if (progress < 0.67f) return "OVERGROWN FOREST";
        return "THE BRIGHT SKIES";
    }

    public void Draw(SpriteBatch sb, PixelArtTextures tex, float cameraX, float cameraY, float worldWidthPx, float worldHeightPx, float elapsed)
    {
        float progress = ComputeHeightProgress(cameraY + 200f, worldHeightPx);

        // Smooth vertical sky/cavern color gradient sampled across 25 horizontal bands
        Color topColor = EvaluateTopGradient(progress);
        Color botColor = EvaluateBottomGradient(progress);

        const int bands = 25;
        int bandH = 400 / bands;
        for (int i = 0; i < bands; i++)
        {
            float t = i / (float)(bands - 1);
            Color c = Color.Lerp(topColor, botColor, t);
            sb.Draw(tex.WhitePixel, new Rectangle(0, i * bandH, 640, bandH + 1), c);
        }

        // Zone-specific parallax silhouettes
        float ruinFade = Math.Clamp(1f - Math.Max(0f, progress - 0.30f) / 0.22f, 0f, 1f);
        float forestFade = Math.Clamp(1f - MathF.Abs(progress - 0.50f) / 0.28f, 0f, 1f);
        float skyFade = Math.Clamp((progress - 0.52f) / 0.24f, 0f, 1f);

        if (ruinFade > 0.01f)
        {
            DrawChasmRuinsParallax(sb, tex, cameraX, cameraY, ruinFade, elapsed);
        }

        if (forestFade > 0.01f)
        {
            DrawForestCanopyParallax(sb, tex, cameraX, cameraY, forestFade, elapsed);
        }

        if (skyFade > 0.01f)
        {
            DrawBrightSkyParallax(sb, tex, cameraX, cameraY, skyFade, elapsed);
        }

        // Ambient particles (crystal motes in ruins -> fireflies/leaves in forest -> wind motes in sky)
        DrawAmbientMotes(sb, tex, cameraX, cameraY, progress, elapsed);
    }

    private static Color EvaluateTopGradient(float p)
    {
        // Deep indigo -> purple twilight -> forest canopy green -> bright sky cerulean
        Color chasmTop = new(13, 11, 28);
        Color midRuinTop = new(28, 22, 56);
        Color forestTop = new(26, 62, 46);
        Color upperForestTop = new(46, 108, 88);
        Color skyTop = new(78, 178, 240);
        Color summitTop = new(130, 218, 255);

        if (p < 0.28f) return Color.Lerp(chasmTop, midRuinTop, p / 0.28f);
        if (p < 0.48f) return Color.Lerp(midRuinTop, forestTop, (p - 0.28f) / 0.20f);
        if (p < 0.68f) return Color.Lerp(forestTop, upperForestTop, (p - 0.48f) / 0.20f);
        if (p < 0.86f) return Color.Lerp(upperForestTop, skyTop, (p - 0.68f) / 0.18f);
        return Color.Lerp(skyTop, summitTop, (p - 0.86f) / 0.14f);
    }

    private static Color EvaluateBottomGradient(float p)
    {
        Color chasmBot = new(24, 18, 46);
        Color midRuinBot = new(44, 30, 74);
        Color forestBot = new(42, 92, 58);
        Color upperForestBot = new(74, 148, 108);
        Color skyBot = new(156, 224, 255);
        Color summitBot = new(218, 246, 255);

        if (p < 0.28f) return Color.Lerp(chasmBot, midRuinBot, p / 0.28f);
        if (p < 0.48f) return Color.Lerp(midRuinBot, forestBot, (p - 0.28f) / 0.20f);
        if (p < 0.68f) return Color.Lerp(forestBot, upperForestBot, (p - 0.48f) / 0.20f);
        if (p < 0.86f) return Color.Lerp(upperForestBot, skyBot, (p - 0.68f) / 0.18f);
        return Color.Lerp(skyBot, summitBot, (p - 0.86f) / 0.14f);
    }

    private static void DrawChasmRuinsParallax(SpriteBatch sb, PixelArtTextures tex, float camX, float camY, float alpha, float elapsed)
    {
        Color pillarFar = new Color(26, 22, 48) * (0.85f * alpha);
        Color pillarNear = new Color(36, 30, 64) * (0.90f * alpha);
        Color crystal = new Color(90, 210, 240) * (0.45f * alpha);

        // Distant towering underground ruin columns
        for (int i = -1; i < 8; i++)
        {
            int px = (int)(i * 112 - ((camX * 0.22f) % 112f));
            sb.Draw(tex.WhitePixel, new Rectangle(px, 0, 28, 400), pillarFar);
            sb.Draw(tex.WhitePixel, new Rectangle(px - 6, 40, 40, 12), pillarFar);
            sb.Draw(tex.WhitePixel, new Rectangle(px - 6, 240, 40, 12), pillarFar);
        }

        // Midground cracked arches + glowing crystal veins
        for (int i = -1; i < 6; i++)
        {
            int px = (int)(i * 160 - ((camX * 0.40f) % 160f));
            int py = (int)(120 - ((camY * 0.35f) % 200f));
            sb.Draw(tex.WhitePixel, new Rectangle(px + 20, 0, 20, 400), pillarNear);
            float pulse = 0.7f + 0.3f * MathF.Sin(elapsed * 2.2f + i * 1.4f);
            sb.Draw(tex.SoftGlow, new Rectangle(px + 12, (py + 220) % 400, 36, 36), crystal * pulse);
        }
    }

    private static void DrawForestCanopyParallax(SpriteBatch sb, PixelArtTextures tex, float camX, float camY, float alpha, float elapsed)
    {
        Color trunkFar = new Color(24, 52, 38) * (0.80f * alpha);
        Color canopyFar = new Color(34, 82, 52) * (0.85f * alpha);
        Color sunShaft = new Color(172, 236, 162) * (0.12f * alpha);

        // Giant ancient forest trunks in the mist
        for (int i = -1; i < 7; i++)
        {
            int px = (int)(i * 130 - ((camX * 0.26f) % 130f));
            sb.Draw(tex.WhitePixel, new Rectangle(px + 18, 0, 34, 400), trunkFar);
            int leafY = (int)(40 + ((i * 53 - camY * 0.18f) % 140f));
            if (leafY < 0) leafY += 140;
            sb.Draw(tex.WhitePixel, new Rectangle(px - 16, leafY, 102, 28), canopyFar);
        }

        // Dappled sunlit shafts filtering through the trees
        for (int i = 0; i < 4; i++)
        {
            int sx = (int)(60 + i * 170 - ((camX * 0.15f) % 170f) + MathF.Sin(elapsed * 0.6f + i) * 8f);
            sb.Draw(tex.WhitePixel, new Rectangle(sx, 0, 44, 400), sunShaft);
        }
    }

    private static void DrawBrightSkyParallax(SpriteBatch sb, PixelArtTextures tex, float camX, float camY, float alpha, float elapsed)
    {
        Color sunGlow = new Color(255, 246, 196) * (0.45f * alpha);
        Color cloudFar = new Color(224, 244, 255) * (0.65f * alpha);
        Color cloudNear = new Color(250, 253, 255) * (0.85f * alpha);

        // Warm upper sun halo
        sb.Draw(tex.SoftGlow, new Rectangle(460, 18, 128, 128), sunGlow);

        // Drifting layered clouds
        for (int i = 0; i < 7; i++)
        {
            float drift = (elapsed * (6f + (i % 3) * 3f)) % 760f;
            float cx = ((i * 135f + drift - camX * 0.18f) % 760f) - 100f;
            float cy = ((i * 68f - camY * 0.22f) % 420f);
            if (cy < 0) cy += 420f;

            int cw = 72 + (i % 3) * 24;
            int ch = 18 + (i % 2) * 6;
            sb.Draw(tex.WhitePixel, new Rectangle((int)cx, (int)cy, cw, ch), cloudFar);
            sb.Draw(tex.WhitePixel, new Rectangle((int)cx + 12, (int)cy - 6, cw - 24, 10), cloudNear);
        }
    }

    private void DrawAmbientMotes(SpriteBatch sb, PixelArtTextures tex, float camX, float camY, float progress, float elapsed)
    {
        Color moteColor;
        if (progress < 0.36f)
        {
            moteColor = new Color(128, 224, 255, 150); // Chasm spirit/crystal sparks
        }
        else if (progress < 0.68f)
        {
            moteColor = new Color(196, 252, 130, 175); // Forest fireflies & sunlit pollen
        }
        else
        {
            moteColor = new Color(255, 252, 232, 185); // Sky wind & sun motes
        }

        foreach (AmbientMote m in _motes)
        {
            float driftX = MathF.Sin(elapsed * 1.1f + m.Phase) * 14f;
            float riseY = (elapsed * m.Speed) % 400f;
            float sx = (m.X + driftX - camX * 0.55f) % 640f;
            float sy = (m.Y - riseY - camY * 0.55f) % 400f;
            if (sx < 0) sx += 640f;
            if (sy < 0) sy += 400f;

            float twinkle = 0.55f + 0.45f * MathF.Sin(elapsed * 3.0f + m.Phase * 2f);
            int s = (int)m.Size;
            sb.Draw(tex.WhitePixel, new Rectangle((int)sx, (int)sy, s, s), moteColor * twinkle);
        }
    }
}
