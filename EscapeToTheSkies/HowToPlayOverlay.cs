using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EscapeToTheSkies;

public sealed class HowToPlayOverlay
{
    public bool Update(InputState input, AudioSynthesizer audio)
    {
        if (input.HowToPlayTogglePressed || input.BackPressed || input.ConfirmPressed || input.MouseLeftClicked)
        {
            audio.PlayShopClose();
            return true;
        }

        return false;
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, BitmapFont font, int uiW, int uiH, bool inActiveRun)
    {
        sb.Draw(pixel, new Rectangle(0, 0, uiW, uiH), new Color(8, 10, 20, 205));

        int panelW = 980;
        int panelH = 650;
        int px = (uiW - panelW) / 2;
        int py = (uiH - panelH) / 2;

        ShopOverlay.DrawBox(sb, pixel, px, py, panelW, panelH, new Color(16, 20, 36, 246), new Color(116, 198, 250), 3);

        // Header + Timer Paused indicator
        font.DrawString(sb, "HOW TO PLAY", new Vector2(px + 34, py + 26), new Color(138, 230, 255), scale: 4);

        if (inActiveRun)
        {
            ShopOverlay.DrawBox(sb, pixel, px + panelW - 252, py + 26, 218, 36,
                new Color(28, 42, 62), new Color(128, 224, 255), 2);
            font.DrawCentered(sb, "TIMER PAUSED", px + panelW - 143, py + 36, new Color(168, 242, 255), scale: 2);
        }

        // Goal section
        ShopOverlay.DrawBox(sb, pixel, px + 34, py + 82, panelW - 68, 92,
            new Color(22, 28, 50, 235), new Color(76, 102, 148), 2);
        font.DrawString(sb, "GOAL: ESCAPE TO THE SKIES", new Vector2(px + 48, py + 96), new Color(255, 224, 96), scale: 2);
        font.DrawString(
            sb,
            "Start deep underground in dark stone ruins, climb through the forest canopy,\nand reach the Sky Escape Gate at the top in your fastest time!",
            new Vector2(px + 48, py + 124),
            new Color(208, 224, 244),
            scale: 2
        );

        // Controls section
        ShopOverlay.DrawBox(sb, pixel, px + 34, py + 188, panelW - 68, 224,
            new Color(22, 28, 50, 235), new Color(76, 102, 148), 2);
        font.DrawString(sb, "STEAM DECK & KEYBOARD CONTROLS", new Vector2(px + 48, py + 202), new Color(142, 234, 180), scale: 2);

        int cx = px + 48;
        int cy = py + 234;
        DrawControlLine(sb, font, cx, cy, "L-STICK / D-PAD", "Move Left & Right (Up does not jump)", "A / D / ARROWS", new Color(172, 206, 244));
        DrawControlLine(sb, font, cx, cy + 34, "A BUTTON", "Jump (Hold for lift; Down+A drops)", "SPACE / Z", new Color(96, 224, 128));
        DrawControlLine(sb, font, cx, cy + 68, "Y BUTTON", "Open/Close Shop (Timer Paused)", "TAB / E", new Color(252, 206, 72));
        DrawControlLine(sb, font, cx, cy + 102, "X BUTTON", "How To Play (Timer Paused)", "H / F1", new Color(108, 198, 255));
        DrawControlLine(sb, font, cx, cy + 136, "MENU / VIEW", "Pause Menu  /  Quick Restart Run", "ESC / R", new Color(196, 176, 248));

        // 3 Upgrade Stats section
        ShopOverlay.DrawBox(sb, pixel, px + 34, py + 426, panelW - 68, 144,
            new Color(22, 28, 50, 235), new Color(76, 102, 148), 2);
        font.DrawString(sb, "COLLECT COINS & UPGRADE 3 STATS", new Vector2(px + 48, py + 440), new Color(255, 202, 92), scale: 2);
        font.DrawString(sb, "1. MOVE SPEED:     Run faster & leap across wider horizontal gaps", new Vector2(px + 48, py + 474), new Color(255, 186, 74), scale: 2);
        font.DrawString(sb, "2. JUMP HEIGHT:    Higher initial jump spring to reach taller ledges", new Vector2(px + 48, py + 502), new Color(108, 232, 138), scale: 2);
        font.DrawString(sb, "3. JUMP DURATION:  Hold Jump longer for extended skyward boost", new Vector2(px + 48, py + 530), new Color(98, 224, 255), scale: 2);

        // Footer
        int footY = py + panelH - 54;
        int fx = px + 34;
        font.DrawDeckBadge(sb, "A", new Vector2(fx, footY), new Color(96, 224, 128), 2);
        fx += font.MeasureBadgeWidth("A", 2) + 8;
        font.DrawDeckBadge(sb, "B", new Vector2(fx, footY), new Color(242, 102, 102), 2);
        fx += font.MeasureBadgeWidth("B", 2) + 8;
        font.DrawDeckBadge(sb, "X", new Vector2(fx, footY), new Color(108, 198, 255), 2);
        fx += font.MeasureBadgeWidth("X", 2) + 12;
        font.DrawString(sb, "Close & Return (H / Esc / Enter)", new Vector2(fx, footY + 4), Color.White, 2);
    }

    private static void DrawControlLine(SpriteBatch sb, BitmapFont font, int x, int y, string deckBtn, string desc, string kbKey, Color accent)
    {
        font.DrawDeckBadge(sb, deckBtn, new Vector2(x, y), accent, 2);
        font.DrawString(sb, desc, new Vector2(x + 208, y + 4), Color.White, 2);
        font.DrawString(sb, $"({kbKey})", new Vector2(x + 688, y + 4), new Color(148, 168, 194), 2);
    }
}
