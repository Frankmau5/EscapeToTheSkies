using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EscapeToTheSkies;

public sealed class ShopOverlay
{
    private static readonly StatType[] Order =
    {
        StatType.Speed,
        StatType.JumpHeight,
        StatType.JumpDuration,
    };

    private static readonly Color[] AccentColors =
    {
        new Color(255, 186, 74),  // Speed: Warm Amber
        new Color(108, 232, 138), // Jump Height: Forest Emerald
        new Color(98, 224, 255),  // Jump Duration: Sky Cyan
    };

    private int _selectedIndex;
    private float _flashTimer;
    private bool _flashError;
    private float _pulseTimer;

    public void ResetSelection()
    {
        _selectedIndex = 0;
        _flashTimer = 0f;
    }

    /// <summary>
    /// Returns true if the shop should close.
    /// </summary>
    public bool Update(float dt, InputState input, PlayerStats stats, AudioSynthesizer audio, int uiW, int uiH)
    {
        _pulseTimer += dt;
        if (_flashTimer > 0f)
        {
            _flashTimer = MathF.Max(0f, _flashTimer - dt);
        }

        if (input.ShopTogglePressed || input.BackPressed)
        {
            audio.PlayShopClose();
            return true;
        }

        if (input.MenuMoveY != 0)
        {
            _selectedIndex = (_selectedIndex + input.MenuMoveY + Order.Length) % Order.Length;
            audio.PlayMenuMove();
        }

        const int panelW = 900;
        const int panelH = 620;
        int px = (uiW - panelW) / 2;
        int py = (uiH - panelH) / 2;
        int cardX = px + 34;
        int cardW = panelW - 68;
        int cardH = 122;
        int cardYStart = py + 140;
        int cardGap = 16;

        bool clickedCard = false;
        for (int i = 0; i < Order.Length; i++)
        {
            var cardRect = new Rectangle(cardX, cardYStart + i * (cardH + cardGap), cardW, cardH);
            if (cardRect.Contains(input.MouseUiPos))
            {
                if (input.MouseMoved && _selectedIndex != i)
                {
                    _selectedIndex = i;
                    audio.PlayMenuMove();
                }
                if (input.MouseLeftClicked)
                {
                    _selectedIndex = i;
                    clickedCard = true;
                }
            }
        }

        if (input.MouseLeftClicked && !clickedCard)
        {
            var panelRect = new Rectangle(px, py, panelW, panelH);
            var closeRect = new Rectangle(px + 460, py + panelH - 64, 410, 48);
            if (!panelRect.Contains(input.MouseUiPos) || closeRect.Contains(input.MouseUiPos))
            {
                audio.PlayShopClose();
                return true;
            }
        }

        if (input.ConfirmPressed || clickedCard)
        {
            StatType stat = Order[_selectedIndex];
            if (stats.TryUpgrade(stat))
            {
                audio.PlayUpgrade(stats.GetLevel(stat));
                _flashTimer = 0.25f;
                _flashError = false;
            }
            else
            {
                audio.PlayMenuError();
                _flashTimer = 0.25f;
                _flashError = true;
            }
        }

        return false;
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, BitmapFont font, PlayerStats stats, int uiW, int uiH)
    {
        // Backdrop dim
        sb.Draw(pixel, new Rectangle(0, 0, uiW, uiH), new Color(8, 10, 20, 195));

        int panelW = 900;
        int panelH = 620;
        int px = (uiW - panelW) / 2;
        int py = (uiH - panelH) / 2;

        // Outer glass frame
        DrawBox(sb, pixel, px, py, panelW, panelH, new Color(18, 22, 38, 245), new Color(116, 178, 232));

        // Header
        font.DrawString(sb, "STAT UPGRADE SHOP", new Vector2(px + 34, py + 28), new Color(235, 246, 255), scale: 4);

        // Timer Paused badge
        DrawBox(sb, pixel, px + 454, py + 28, 172, 34,
            new Color(28, 42, 62), new Color(128, 224, 255), 2);
        font.DrawCentered(sb, "TIMER PAUSED", px + 540, py + 37, new Color(168, 242, 255), scale: 2);

        string coinLabel = $"COINS: {stats.Coins}";
        Vector2 coinSize = font.MeasureString(coinLabel, 3);
        DrawBox(sb, pixel, px + panelW - (int)coinSize.X - 66, py + 24, (int)coinSize.X + 34, 42,
            new Color(34, 28, 16, 240), new Color(248, 204, 68));
        font.DrawString(sb, coinLabel, new Vector2(px + panelW - (int)coinSize.X - 49, py + 34), new Color(255, 224, 96), scale: 3);

        font.DrawString(
            sb,
            "Boost your climb. Reach higher ledges & speedrun to the sky!",
            new Vector2(px + 34, py + 72),
            new Color(172, 194, 218),
            scale: 2
        );

        // Live simulated jump summary strip
        JumpEnvelope env = PlayerStats.SimulateMaxJump(stats.SpeedLevel, stats.JumpHeightLevel, stats.JumpDurationLevel);
        string reachText = $"CURRENT PEAK: {env.MaxHeightPx / LevelData.TileSize:0.0} TILES HIGH   |   MAX LEAP: {env.MaxDistancePx / LevelData.TileSize:0.0} TILES WIDE";
        font.DrawCentered(sb, reachText, px + panelW * 0.5f, py + 104, new Color(142, 226, 250), scale: 2);

        // 3 Stat Cards
        int cardX = px + 34;
        int cardW = panelW - 68;
        int cardH = 122;
        int cardYStart = py + 140;
        int cardGap = 16;

        for (int i = 0; i < Order.Length; i++)
        {
            StatType stat = Order[i];
            int cy = cardYStart + i * (cardH + cardGap);
            bool selected = i == _selectedIndex;
            int level = stats.GetLevel(stat);
            bool maxed = stats.IsMaxed(stat);
            int cost = stats.GetNextCost(stat);
            bool canAfford = stats.CanAfford(stat);
            Color accent = AccentColors[i];

            Color fill = selected ? new Color(30, 38, 64, 250) : new Color(22, 26, 44, 235);
            Color border = selected ? accent : new Color(66, 78, 112);

            if (selected && _flashTimer > 0f)
            {
                border = _flashError ? new Color(255, 92, 92) : Color.White;
            }

            DrawBox(sb, pixel, cardX, cy, cardW, cardH, fill, border, selected ? 3 : 2);

            if (selected)
            {
                float pulse = 0.55f + 0.45f * MathF.Sin(_pulseTimer * 6f);
                font.DrawString(sb, ">", new Vector2(cardX + 14, cy + 20), accent * pulse, scale: 3);
            }

            // Title + Level
            font.DrawString(sb, PlayerStats.GetDisplayName(stat), new Vector2(cardX + 42, cy + 16), accent, scale: 3);
            font.DrawString(sb, PlayerStats.GetDescription(stat), new Vector2(cardX + 42, cy + 48), new Color(192, 206, 228), scale: 2);

            // Level Pips (5 boxes)
            int pipStartX = cardX + 42;
            int pipY = cy + 78;
            for (int p = 0; p < PlayerStats.MaxLevel; p++)
            {
                bool filled = p < level;
                bool preview = p == level && selected && !maxed;
                Color pipColor = filled ? accent : (preview ? accent * 0.35f : new Color(46, 54, 80));
                DrawBox(sb, pixel, pipStartX + p * 34, pipY, 26, 20, pipColor, filled ? Color.White * 0.7f : new Color(76, 88, 120), 2);
            }

            // Current -> Next Stat readout
            string valNow = PlayerStats.GetValueSummary(stat, level);
            string valSummary = maxed
                ? $"{valNow} (MAX)"
                : $"{valNow} -> {PlayerStats.GetValueSummary(stat, level + 1)}";
            font.DrawString(sb, valSummary, new Vector2(cardX + 234, cy + 81), Color.White, scale: 2);

            // Cost Box on right side of card
            int costBoxW = 186;
            int costBoxH = 66;
            int costBoxX = cardX + cardW - costBoxW - 20;
            int costBoxY = cy + (cardH - costBoxH) / 2;

            if (maxed)
            {
                DrawBox(sb, pixel, costBoxX, costBoxY, costBoxW, costBoxH, new Color(28, 48, 40), new Color(110, 224, 148), 2);
                font.DrawCentered(sb, "MAXED OUT", costBoxX + costBoxW * 0.5f, costBoxY + 26, new Color(142, 246, 174), scale: 2);
            }
            else
            {
                Color costBg = canAfford ? new Color(46, 40, 18) : new Color(36, 24, 30);
                Color costBorder = canAfford ? new Color(250, 208, 72) : new Color(128, 74, 84);
                Color costText = canAfford ? new Color(255, 228, 102) : new Color(220, 126, 134);

                DrawBox(sb, pixel, costBoxX, costBoxY, costBoxW, costBoxH, costBg, costBorder, 2);
                font.DrawCentered(sb, $"{cost} COINS", costBoxX + costBoxW * 0.5f, costBoxY + 14, costText, scale: 2);
                font.DrawCentered(sb, canAfford ? "UPGRADE" : "NEED COINS", costBoxX + costBoxW * 0.5f, costBoxY + 38,
                    canAfford ? new Color(190, 248, 174) : new Color(168, 118, 126), scale: 2);
            }
        }

        // Bottom Steam Deck Controls Footer
        int footY = py + panelH - 54;
        int curX = px + 36;

        font.DrawDeckBadge(sb, "D-PAD", new Vector2(curX, footY), new Color(180, 202, 232), 2);
        curX += font.MeasureBadgeWidth("D-PAD", 2) + 10;
        font.DrawString(sb, "Select", new Vector2(curX, footY + 4), Color.White, 2);
        curX += 116;

        font.DrawDeckBadge(sb, "A", new Vector2(curX, footY), new Color(96, 224, 128), 2);
        curX += font.MeasureBadgeWidth("A", 2) + 10;
        font.DrawString(sb, "Buy Upgrade", new Vector2(curX, footY + 4), Color.White, 2);
        curX += 180;

        font.DrawDeckBadge(sb, "Y", new Vector2(curX, footY), new Color(252, 206, 72), 2);
        curX += font.MeasureBadgeWidth("Y", 2) + 8;
        font.DrawDeckBadge(sb, "B", new Vector2(curX, footY), new Color(242, 102, 102), 2);
        curX += font.MeasureBadgeWidth("B", 2) + 10;
        font.DrawString(sb, "Resume (Tab/Esc)", new Vector2(curX, footY + 4), Color.White, 2);
    }

    public static void DrawBox(SpriteBatch sb, Texture2D pixel, int x, int y, int w, int h, Color fill, Color border, int thickness = 2)
    {
        sb.Draw(pixel, new Rectangle(x, y, w, h), fill);
        sb.Draw(pixel, new Rectangle(x, y, w, thickness), border);
        sb.Draw(pixel, new Rectangle(x, y + h - thickness, w, thickness), border);
        sb.Draw(pixel, new Rectangle(x, y, thickness, h), border);
        sb.Draw(pixel, new Rectangle(x + w - thickness, y, thickness, h), border);
    }
}
