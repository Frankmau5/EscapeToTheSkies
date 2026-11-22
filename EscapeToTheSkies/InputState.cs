using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace EscapeToTheSkies;

public sealed class InputState
{
    private KeyboardState _prevKeys;
    private KeyboardState _currKeys;
    private GamePadState _prevPad;
    private GamePadState _currPad;
    private MouseState _prevMouse;
    private MouseState _currMouse;

    private float _navRepeatTimer;
    private int _lastNavDirY;

    public bool ActiveGamePadConnected => _currPad.IsConnected;

    public Point MouseUiPos { get; private set; }
    public bool MouseMoved { get; private set; }
    public bool MouseLeftClicked { get; private set; }

    public float MoveX { get; private set; }
    public float MoveY { get; private set; }

    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool JumpReleased { get; private set; }

    public bool ShopTogglePressed { get; private set; }
    public bool HowToPlayTogglePressed { get; private set; }
    public bool PausePressed { get; private set; }
    public bool ConfirmPressed { get; private set; }
    public bool BackPressed { get; private set; }
    public bool RestartPressed { get; private set; }
    public bool FullscreenTogglePressed { get; private set; }

    public int MenuMoveY { get; private set; }
    public int MenuMoveX { get; private set; }

    public void Update(float dt, Rectangle letterboxRect, int uiW, int uiH)
    {
        _prevKeys = _currKeys;
        _prevPad = _currPad;
        _prevMouse = _currMouse;

        _currKeys = Keyboard.GetState();
        _currPad = GetBestGamePad();
        _currMouse = Mouse.GetState();

        MouseMoved = _currMouse.X != _prevMouse.X || _currMouse.Y != _prevMouse.Y;
        MouseLeftClicked =
            _currMouse.LeftButton == ButtonState.Pressed &&
            _prevMouse.LeftButton == ButtonState.Released;

        if (letterboxRect.Width > 0 && letterboxRect.Height > 0)
        {
            int ux = (int)MathF.Round((_currMouse.X - letterboxRect.X) * (uiW / (float)letterboxRect.Width));
            int uy = (int)MathF.Round((_currMouse.Y - letterboxRect.Y) * (uiH / (float)letterboxRect.Height));
            MouseUiPos = new Point(ux, uy);
        }

        // Horizontal movement (Stick + D-Pad + Keyboard)
        float kx = 0f;
        if (_currKeys.IsKeyDown(Keys.Left) || _currKeys.IsKeyDown(Keys.A)) kx -= 1f;
        if (_currKeys.IsKeyDown(Keys.Right) || _currKeys.IsKeyDown(Keys.D)) kx += 1f;

        float px = _currPad.ThumbSticks.Left.X;
        if (MathF.Abs(px) < 0.25f) px = 0f;
        if (_currPad.DPad.Left == ButtonState.Pressed) px = -1f;
        if (_currPad.DPad.Right == ButtonState.Pressed) px = 1f;

        MoveX = Math.Clamp(MathF.Abs(px) >= MathF.Abs(kx) ? px : kx, -1f, 1f);

        // Vertical direction (-1 = Up, +1 = Down in screen coordinates; used for menu nav & drop-through, NOT jump)
        float ky = 0f;
        if (_currKeys.IsKeyDown(Keys.Up) || _currKeys.IsKeyDown(Keys.W)) ky -= 1f;
        if (_currKeys.IsKeyDown(Keys.Down) || _currKeys.IsKeyDown(Keys.S)) ky += 1f;

        float py = -_currPad.ThumbSticks.Left.Y; // Note: ThumbStick Y>0 is Up
        if (MathF.Abs(py) < 0.35f) py = 0f;
        if (_currPad.DPad.Up == ButtonState.Pressed) py = -1f;
        if (_currPad.DPad.Down == ButtonState.Pressed) py = 1f;

        MoveY = Math.Clamp(MathF.Abs(py) >= MathF.Abs(ky) ? py : ky, -1f, 1f);

        // Jump: GamePad A, Keyboard Space / Z / K (Up / W do NOT jump)
        bool currJump = IsJumpPressed(_currPad, _currKeys);
        bool prevJump = IsJumpPressed(_prevPad, _prevKeys);

        JumpHeld = currJump;
        JumpPressed = currJump && !prevJump;
        JumpReleased = !currJump && prevJump;

        // Shop Toggle: GamePad Y, Keyboard Tab / E / I
        ShopTogglePressed =
            JustPressed(Buttons.Y) ||
            JustPressed(Keys.Tab) ||
            JustPressed(Keys.E) ||
            JustPressed(Keys.I);

        // How To Play Toggle: GamePad X, Keyboard H / F1
        HowToPlayTogglePressed =
            JustPressed(Buttons.X) ||
            JustPressed(Keys.H) ||
            JustPressed(Keys.F1);

        // Pause / Menu: GamePad Start, Keyboard Escape / P
        PausePressed =
            JustPressed(Buttons.Start) ||
            JustPressed(Keys.Escape) ||
            JustPressed(Keys.P);

        // Confirm: GamePad A, Keyboard Enter / Space
        ConfirmPressed =
            JustPressed(Buttons.A) ||
            JustPressed(Keys.Enter) ||
            JustPressed(Keys.Space);

        // Back: GamePad B, Keyboard Escape / Back / B
        BackPressed =
            JustPressed(Buttons.B) ||
            JustPressed(Keys.Escape) ||
            JustPressed(Keys.Back) ||
            JustPressed(Keys.B);

        // Quick Restart: GamePad Back (View), Keyboard R
        RestartPressed =
            JustPressed(Buttons.Back) ||
            JustPressed(Keys.R);

        // Fullscreen toggle: F11 or Alt+Enter
        FullscreenTogglePressed =
            JustPressed(Keys.F11) ||
            ((_currKeys.IsKeyDown(Keys.LeftAlt) || _currKeys.IsKeyDown(Keys.RightAlt)) && JustPressed(Keys.Enter));

        // Discrete menu Up/Down with smooth hold repeat
        int rawDirY = MoveY < -0.5f ? -1 : (MoveY > 0.5f ? 1 : 0);
        if (rawDirY == 0)
        {
            MenuMoveY = 0;
            _navRepeatTimer = 0f;
            _lastNavDirY = 0;
        }
        else if (rawDirY != _lastNavDirY)
        {
            MenuMoveY = rawDirY;
            _lastNavDirY = rawDirY;
            _navRepeatTimer = 0.30f;
        }
        else
        {
            _navRepeatTimer -= dt;
            if (_navRepeatTimer <= 0f)
            {
                MenuMoveY = rawDirY;
                _navRepeatTimer = 0.11f;
            }
            else
            {
                MenuMoveY = 0;
            }
        }

        // Discrete menu Left/Right
        bool leftNow = MoveX < -0.55f;
        bool rightNow = MoveX > 0.55f;
        bool leftPrev =
            _prevPad.DPad.Left == ButtonState.Pressed ||
            _prevPad.ThumbSticks.Left.X < -0.55f ||
            _prevKeys.IsKeyDown(Keys.Left) ||
            _prevKeys.IsKeyDown(Keys.A);
        bool rightPrev =
            _prevPad.DPad.Right == ButtonState.Pressed ||
            _prevPad.ThumbSticks.Left.X > 0.55f ||
            _prevKeys.IsKeyDown(Keys.Right) ||
            _prevKeys.IsKeyDown(Keys.D);

        MenuMoveX = (rightNow && !rightPrev ? 1 : 0) - (leftNow && !leftPrev ? 1 : 0);
    }

    public static bool IsJumpKey(Keys key) => key is Keys.Space or Keys.Z or Keys.K;

    private static bool IsJumpPressed(GamePadState pad, KeyboardState keys) =>
        pad.Buttons.A == ButtonState.Pressed ||
        keys.IsKeyDown(Keys.Space) ||
        keys.IsKeyDown(Keys.Z) ||
        keys.IsKeyDown(Keys.K);

    public bool IsButtonJustPressed(Buttons btn) => JustPressed(btn);
    public bool IsKeyJustPressed(Keys key) => JustPressed(key);

    private bool JustPressed(Keys key) => _currKeys.IsKeyDown(key) && _prevKeys.IsKeyUp(key);

    private bool JustPressed(Buttons button) =>
        _currPad.IsButtonDown(button) && !_prevPad.IsButtonDown(button);

    private static GamePadState GetBestGamePad()
    {
        for (int i = 0; i < 4; i++)
        {
            GamePadState s = GamePad.GetState((PlayerIndex)i);
            if (s.IsConnected)
            {
                return s;
            }
        }

        return GamePad.GetState(PlayerIndex.One);
    }
}
