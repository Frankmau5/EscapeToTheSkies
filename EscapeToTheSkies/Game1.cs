using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace EscapeToTheSkies;

public enum GameScreenState
{
    MainMenu,
    Playing,
    Shop,
    HowToPlay,
    Paused,
    Victory,
}

public sealed class EscapeToTheSkiesGame : Game
{
    public const int VirtualWorldW = 640;
    public const int VirtualWorldH = 400;
    public const int UiW = 1280;
    public const int UiH = 800;

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private RenderTarget2D _worldTarget = null!;
    private RenderTarget2D _uiTarget = null!;

    private PixelArtTextures _textures = null!;
    private BitmapFont _font = null!;
    private AudioSynthesizer _audio = null!;
    private BackgroundRenderer _background = null!;
    private LevelData _level = null!;
    private Player _player = null!;
    private PlayerStats _stats = null!;
    private InputState _input = null!;
    private ShopOverlay _shop = null!;
    private HowToPlayOverlay _howToPlay = null!;
    private SaveData _save = null!;

    private readonly List<Particle> _particles = new();
    private readonly List<FloatingPopup> _popups = new();

    private GameScreenState _screen = GameScreenState.MainMenu;
    private GameScreenState _howToPlayReturnTo = GameScreenState.MainMenu;
    private Vector2 _cameraPos;
    private float _elapsedVisual;
    private double _runTimer;
    private float _physicsAccum;
    private float _shakeTimer;
    private float _shakeIntensity;
    private bool _newRecordThisRun;
    private int _mainMenuIndex;
    private int _pauseIndex;
    private int _victoryIndex;
    private bool _isFullscreen;

    private readonly bool _smokeTest;
    private readonly string? _captureDir;
    private RenderTarget2D? _captureTarget;
    private int _smokeFrames;
    private int _captureStep;
    private const float FixedDt = 1f / 60f;

    public EscapeToTheSkiesGame(bool smokeTest = false, string? captureDir = null)
    {
        _smokeTest = smokeTest;
        _captureDir = captureDir;
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = UiW;
        _graphics.PreferredBackBufferHeight = UiH;
        _graphics.SynchronizeWithVerticalRetrace = true;
        Window.Title = "Escape to the Skies";
        Window.AllowUserResizing = true;
        IsMouseVisible = false;
        IsFixedTimeStep = false;
    }

    protected override void Initialize()
    {
        base.Initialize();

        _input = new InputState();
        _stats = new PlayerStats();
        _level = new LevelData();
        _player = new Player();
        _shop = new ShopOverlay();
        _howToPlay = new HowToPlayOverlay();
        _background = new BackgroundRenderer();
        _save = SaveManager.Load();

        ResetRun(startPlaying: false);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _worldTarget = new RenderTarget2D(GraphicsDevice, VirtualWorldW, VirtualWorldH);
        _uiTarget = new RenderTarget2D(GraphicsDevice, UiW, UiH);
        if (_captureDir != null)
        {
            _captureTarget = new RenderTarget2D(GraphicsDevice, UiW, UiH);
        }
        _textures = new PixelArtTextures(GraphicsDevice);
        _font = new BitmapFont(GraphicsDevice);
        _audio = new AudioSynthesizer();
    }

    protected override void UnloadContent()
    {
        _captureTarget?.Dispose();
        _worldTarget.Dispose();
        _uiTarget.Dispose();
        _textures.Dispose();
        _font.Dispose();
        _audio.Dispose();
        base.UnloadContent();
    }

    private void ResetRun(bool startPlaying)
    {
        _stats.Reset();
        _level.ResetCoins();
        _player.Spawn(_level.PlayerSpawnPx);
        _particles.Clear();
        _popups.Clear();
        _runTimer = 0.0;
        _physicsAccum = 0f;
        _newRecordThisRun = false;
        _pauseIndex = 0;
        _victoryIndex = 0;

        _cameraPos = ComputeTargetCamera(_player.Position);
        _screen = startPlaying ? GameScreenState.Playing : GameScreenState.MainMenu;
    }

    private static Vector2 ComputeTargetCamera(Vector2 playerFeet)
    {
        float targetX = playerFeet.X - VirtualWorldW * 0.5f;
        float targetY = playerFeet.Y - VirtualWorldH * 0.56f;
        targetX = Math.Clamp(targetX, 0f, LevelData.WorldWidthPx - VirtualWorldW);
        targetY = Math.Clamp(targetY, 0f, LevelData.WorldHeightPx - VirtualWorldH);
        return new Vector2(targetX, targetY);
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = MathF.Min(0.05f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        _elapsedVisual += dt;

        Rectangle backRect = ComputeLetterboxRect(
            GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferHeight
        );
        _input.Update(dt, backRect, UiW, UiH);
        IsMouseVisible = !_smokeTest && _screen != GameScreenState.Playing;

        if (_captureDir != null)
        {
            StageCaptureFrame();
            base.Update(gameTime);
            return;
        }

        if (_smokeTest)
        {
            _smokeFrames++;
            _screen = _smokeFrames switch
            {
                <= 2 => GameScreenState.MainMenu,
                <= 4 => GameScreenState.Playing,
                <= 6 => GameScreenState.Shop,
                <= 8 => GameScreenState.HowToPlay,
                <= 10 => GameScreenState.Paused,
                <= 12 => GameScreenState.Victory,
                _ => GameScreenState.Victory,
            };
            // Exercise Chasm, Forest, and Sky background camera heights
            float sampleY = LevelData.WorldHeightPx * (1f - (_smokeFrames % 12) * 0.08f);
            _cameraPos = ComputeTargetCamera(new Vector2(LevelData.WorldWidthPx * 0.5f, sampleY));
            if (_smokeFrames > 12)
            {
                Exit();
                return;
            }
            base.Update(gameTime);
            return;
        }

        if (_input.FullscreenTogglePressed)
        {
            ToggleFullscreen();
        }

        if (_shakeTimer > 0f)
        {
            _shakeTimer = MathF.Max(0f, _shakeTimer - dt);
        }

        switch (_screen)
        {
            case GameScreenState.MainMenu:
                UpdateMainMenu(dt);
                break;
            case GameScreenState.Playing:
                UpdatePlaying(dt);
                break;
            case GameScreenState.Shop:
                // Speedrun timer is paused while the shop overlay is open
                if (_shop.Update(dt, _input, _stats, _audio, UiW, UiH))
                {
                    _screen = GameScreenState.Playing;
                }
                break;
            case GameScreenState.HowToPlay:
                // Speedrun timer is paused while How To Play is open
                if (_howToPlay.Update(_input, _audio))
                {
                    _screen = _howToPlayReturnTo;
                }
                break;
            case GameScreenState.Paused:
                UpdatePaused();
                break;
            case GameScreenState.Victory:
                UpdateVictory(dt);
                break;
        }

        base.Update(gameTime);
    }

    private void UpdateMainMenu(float dt)
    {
        // Gently pan around starter area
        Vector2 target = ComputeTargetCamera(_player.Position + new Vector2(MathF.Sin(_elapsedVisual * 0.4f) * 40f, 0f));
        _cameraPos = Vector2.Lerp(_cameraPos, target, Math.Clamp(dt * 3f, 0f, 1f));

        const int optionCount = 5;
        if (_input.MenuMoveY != 0)
        {
            _mainMenuIndex = (_mainMenuIndex + _input.MenuMoveY + optionCount) % optionCount;
            _audio.PlayMenuMove();
        }

        if (_input.HowToPlayTogglePressed)
        {
            _howToPlayReturnTo = GameScreenState.MainMenu;
            _audio.PlayShopOpen();
            _screen = GameScreenState.HowToPlay;
            return;
        }

        bool clickedItem = false;
        for (int i = 0; i < optionCount; i++)
        {
            if (GetMainMenuItemRect(i).Contains(_input.MouseUiPos))
            {
                if (_input.MouseMoved && _mainMenuIndex != i)
                {
                    _mainMenuIndex = i;
                    _audio.PlayMenuMove();
                }
                if (_input.MouseLeftClicked)
                {
                    _mainMenuIndex = i;
                    clickedItem = true;
                }
            }
        }

        if (_input.ConfirmPressed || clickedItem)
        {
            switch (_mainMenuIndex)
            {
                case 0: // Start Climb
                    _audio.PlayShopOpen();
                    ResetRun(startPlaying: true);
                    break;
                case 1: // How To Play
                    _howToPlayReturnTo = GameScreenState.MainMenu;
                    _audio.PlayShopOpen();
                    _screen = GameScreenState.HowToPlay;
                    break;
                case 2: // Fullscreen
                    ToggleFullscreen();
                    _audio.PlayMenuMove();
                    break;
                case 3: // Sound
                    _audio.Muted = !_audio.Muted;
                    _audio.PlayMenuMove();
                    break;
                case 4: // Quit
                    Exit();
                    break;
            }
        }
        else if (_input.IsKeyJustPressed(Keys.Escape))
        {
            Exit();
        }
    }

    private void UpdatePlaying(float dt)
    {
        if (_input.PausePressed)
        {
            _pauseIndex = 0;
            _audio.PlayMenuMove();
            _screen = GameScreenState.Paused;
            return;
        }

        if (_input.ShopTogglePressed)
        {
            _shop.ResetSelection();
            _audio.PlayShopOpen();
            _screen = GameScreenState.Shop;
            return;
        }

        if (_input.HowToPlayTogglePressed)
        {
            _howToPlayReturnTo = GameScreenState.Playing;
            _audio.PlayShopOpen();
            _screen = GameScreenState.HowToPlay;
            return;
        }

        if (_input.RestartPressed)
        {
            _audio.PlayMenuMove();
            ResetRun(startPlaying: true);
            return;
        }

        _runTimer += dt;
        _physicsAccum += dt;

        int steps = 0;
        while (_physicsAccum >= FixedDt && steps < 5)
        {
            _physicsAccum -= FixedDt;
            steps++;

            _player.Update(FixedDt, _input, _stats, _level, _audio, _particles);
            CheckCoins();

            if (_player.Hitbox.Intersects(_level.GoalTriggerPx))
            {
                TriggerVictory();
                return;
            }
        }

        UpdateParticlesAndPopups(dt);

        Vector2 targetCam = ComputeTargetCamera(_player.Position);
        _cameraPos = Vector2.Lerp(_cameraPos, targetCam, Math.Clamp(dt * 9.5f, 0f, 1f));
    }

    private void CheckCoins()
    {
        Rectangle pBox = _player.Hitbox;
        pBox.Inflate(3, 3);

        foreach (CoinSpawn coin in _level.Coins)
        {
            if (coin.Collected) continue;

            var cBox = new Rectangle((int)(coin.Position.X - 6), (int)(coin.Position.Y - 6), 12, 12);
            if (!pBox.Intersects(cBox)) continue;

            coin.Collected = true;
            _stats.Coins += coin.Value;
            _stats.TotalCoinsCollected += coin.Value;
            _audio.PlayCoin(coin.Value);

            Color burst = coin.Value >= 5
                ? new Color(118, 240, 255)
                : (coin.Value >= 3 ? new Color(255, 224, 84) : new Color(250, 178, 92));

            for (int i = 0; i < 10 && _particles.Count < 240; i++)
            {
                float ang = i * (MathF.PI * 2f / 10f) + Random.Shared.NextSingle() * 0.4f;
                float spd = 35f + Random.Shared.NextSingle() * 55f;
                _particles.Add(new Particle
                {
                    Pos = coin.Position,
                    Vel = new Vector2(MathF.Cos(ang) * spd, MathF.Sin(ang) * spd - 15f),
                    Color = burst,
                    Life = 0.36f,
                    MaxLife = 0.36f,
                    Size = 2f,
                });
            }

            _popups.Add(new FloatingPopup
            {
                Pos = coin.Position + new Vector2(0, -8f),
                Text = $"+{coin.Value}",
                Color = burst,
                Life = 0.70f,
                MaxLife = 0.70f,
            });
        }
    }

    private void TriggerVictory()
    {
        _audio.PlayVictory();
        _newRecordThisRun = SaveManager.RecordRun(_save, _runTimer, _stats.TotalCoinsCollected);
        _shakeTimer = 0.28f;
        _shakeIntensity = 3f;
        _victoryIndex = 0;
        _screen = GameScreenState.Victory;

        for (int i = 0; i < 48; i++)
        {
            float ang = Random.Shared.NextSingle() * MathF.PI * 2f;
            float spd = 40f + Random.Shared.NextSingle() * 140f;
            _particles.Add(new Particle
            {
                Pos = _level.GoalArchBasePx + new Vector2(0, -24f),
                Vel = new Vector2(MathF.Cos(ang) * spd, MathF.Sin(ang) * spd - 35f),
                Color = i % 2 == 0 ? new Color(255, 232, 108) : new Color(128, 242, 255),
                Life = 1.2f,
                MaxLife = 1.2f,
                Size = 2f,
            });
        }
    }

    private void UpdateParticlesAndPopups(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            Particle p = _particles[i];
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                _particles.RemoveAt(i);
                continue;
            }
            p.Pos += p.Vel * dt;
            p.Vel.Y += 90f * dt;
            _particles[i] = p;
        }

        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            FloatingPopup pop = _popups[i];
            pop.Life -= dt;
            if (pop.Life <= 0f)
            {
                _popups.RemoveAt(i);
                continue;
            }
            pop.Pos.Y -= 20f * dt;
            _popups[i] = pop;
        }
    }

    private void UpdatePaused()
    {
        if (_input.PausePressed || _input.BackPressed)
        {
            _audio.PlayShopClose();
            _screen = GameScreenState.Playing;
            return;
        }

        const int optionCount = 7;
        if (_input.MenuMoveY != 0)
        {
            _pauseIndex = (_pauseIndex + _input.MenuMoveY + optionCount) % optionCount;
            _audio.PlayMenuMove();
        }

        bool clickedItem = false;
        for (int i = 0; i < optionCount; i++)
        {
            if (GetPauseItemRect(i).Contains(_input.MouseUiPos))
            {
                if (_input.MouseMoved && _pauseIndex != i)
                {
                    _pauseIndex = i;
                    _audio.PlayMenuMove();
                }
                if (_input.MouseLeftClicked)
                {
                    _pauseIndex = i;
                    clickedItem = true;
                }
            }
        }

        if (_input.ConfirmPressed || clickedItem)
        {
            switch (_pauseIndex)
            {
                case 0: // Resume
                    _audio.PlayShopClose();
                    _screen = GameScreenState.Playing;
                    break;
                case 1: // How To Play
                    _howToPlayReturnTo = GameScreenState.Paused;
                    _audio.PlayShopOpen();
                    _screen = GameScreenState.HowToPlay;
                    break;
                case 2: // Restart Run
                    _audio.PlayShopOpen();
                    ResetRun(startPlaying: true);
                    break;
                case 3: // Fullscreen
                    ToggleFullscreen();
                    _audio.PlayMenuMove();
                    break;
                case 4: // Sound
                    _audio.Muted = !_audio.Muted;
                    _audio.PlayMenuMove();
                    break;
                case 5: // Main Menu
                    _audio.PlayMenuMove();
                    ResetRun(startPlaying: false);
                    break;
                case 6: // Quit
                    Exit();
                    break;
            }
        }
    }

    private void UpdateVictory(float dt)
    {
        UpdateParticlesAndPopups(dt);

        if (_input.MenuMoveX < 0 || _input.MenuMoveY < 0)
        {
            if (_victoryIndex != 0)
            {
                _victoryIndex = 0;
                _audio.PlayMenuMove();
            }
        }
        else if (_input.MenuMoveX > 0 || _input.MenuMoveY > 0)
        {
            if (_victoryIndex != 1)
            {
                _victoryIndex = 1;
                _audio.PlayMenuMove();
            }
        }

        bool clickedBtn = false;
        for (int i = 0; i < 2; i++)
        {
            if (GetVictoryButtonRect(i).Contains(_input.MouseUiPos))
            {
                if (_input.MouseMoved && _victoryIndex != i)
                {
                    _victoryIndex = i;
                    _audio.PlayMenuMove();
                }
                if (_input.MouseLeftClicked)
                {
                    _victoryIndex = i;
                    clickedBtn = true;
                }
            }
        }

        if (_input.BackPressed || _input.PausePressed || _input.IsKeyJustPressed(Keys.M) || ((_input.ConfirmPressed || clickedBtn) && _victoryIndex == 1))
        {
            _audio.PlayMenuMove();
            ResetRun(startPlaying: false);
        }
        else if (_input.RestartPressed || ((_input.ConfirmPressed || clickedBtn) && _victoryIndex == 0))
        {
            _audio.PlayShopOpen();
            ResetRun(startPlaying: true);
        }
    }

    private void ToggleFullscreen()
    {
        _isFullscreen = !_isFullscreen;
        _graphics.IsFullScreen = _isFullscreen;
        if (_isFullscreen)
        {
            var dm = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = dm.Width;
            _graphics.PreferredBackBufferHeight = dm.Height;
        }
        else
        {
            _graphics.PreferredBackBufferWidth = UiW;
            _graphics.PreferredBackBufferHeight = UiH;
        }
        _graphics.ApplyChanges();
    }

    protected override void Draw(GameTime gameTime)
    {
        // Pass 1: Render 640x400 crisp pixel-art world
        GraphicsDevice.SetRenderTarget(_worldTarget);
        GraphicsDevice.Clear(new Color(13, 11, 28));
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
        DrawWorld();
        _spriteBatch.End();

        // Pass 2: Render 1280x800 crisp UI/HUD/Overlays
        GraphicsDevice.SetRenderTarget(_uiTarget);
        GraphicsDevice.Clear(Color.Transparent);
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
        DrawUI();
        _spriteBatch.End();

        // Pass 3: Present both targets cleanly aspect-fitted to the backbuffer (exact 1:1 on Steam Deck 1280x800!)
        if (_captureTarget != null && _captureDir != null && _captureStep >= 1 && _captureStep <= 5)
        {
            GraphicsDevice.SetRenderTarget(_captureTarget);
            GraphicsDevice.Clear(Color.Black);
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
            _spriteBatch.Draw(_worldTarget, new Rectangle(0, 0, UiW, UiH), Color.White);
            _spriteBatch.Draw(_uiTarget, new Rectangle(0, 0, UiW, UiH), Color.White);
            _spriteBatch.End();

            GraphicsDevice.SetRenderTarget(null);
            Directory.CreateDirectory(_captureDir);
            string[] names = { "", "main-menu.png", "deep-chasm.png", "forest-layer.png", "upgrade-shop.png", "bright-skies.png" };
            using var fs = File.Create(Path.Combine(_captureDir, names[_captureStep]));
            _captureTarget.SaveAsPng(fs, UiW, UiH);
            if (_captureStep >= 5)
            {
                Exit();
            }
        }

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);

        Rectangle dst = ComputeLetterboxRect(
            GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferHeight
        );

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
        _spriteBatch.Draw(_worldTarget, dst, Color.White);
        _spriteBatch.Draw(_uiTarget, dst, Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void StageCaptureFrame()
    {
        _captureStep++;
        _elapsedVisual = 1.25f + _captureStep * 0.35f;
        _particles.Clear();
        _popups.Clear();

        switch (_captureStep)
        {
            case 1: // Main Menu
                _save.BestTimeSeconds = 42.18;
                _save.BestCoinsCollected = 96;
                _screen = GameScreenState.MainMenu;
                _mainMenuIndex = 0;
                _player.Spawn(_level.PlayerSpawnPx);
                _cameraPos = ComputeTargetCamera(_player.Position);
                break;

            case 2: // Deep Chasm Ruins
                _screen = GameScreenState.Playing;
                _stats.Reset();
                _stats.Coins = 12;
                _runTimer = 11.42;
                _player.Spawn(new Vector2(27.5f * LevelData.TileSize, 193f * LevelData.TileSize));
                _cameraPos = ComputeTargetCamera(_player.Position);
                break;

            case 3: // Overgrown Forest Layer
                _screen = GameScreenState.Playing;
                _stats.Reset();
                _stats.Coins = 18;
                _stats.TryUpgrade(StatType.Speed);
                _stats.TryUpgrade(StatType.JumpHeight);
                _stats.TryUpgrade(StatType.JumpDuration);
                _stats.Coins = 18;
                _runTimer = 28.65;
                _player.Spawn(new Vector2(27.5f * LevelData.TileSize, 106f * LevelData.TileSize));
                _player.Velocity = new Vector2(130f, -120f);
                _cameraPos = ComputeTargetCamera(_player.Position);
                break;

            case 4: // Upgrade Shop Overlay
                _screen = GameScreenState.Shop;
                _shop.ResetSelection();
                _runTimer = 28.65;
                _player.Spawn(new Vector2(27.5f * LevelData.TileSize, 109f * LevelData.TileSize));
                _cameraPos = ComputeTargetCamera(_player.Position);
                break;

            case 5: // Bright Skies & Summit Gate
                _screen = GameScreenState.Playing;
                _stats.Reset();
                _stats.Coins = 90;
                for (int i = 0; i < 3; i++)
                {
                    _stats.TryUpgrade(StatType.Speed);
                    _stats.TryUpgrade(StatType.JumpHeight);
                    _stats.TryUpgrade(StatType.JumpDuration);
                }
                _stats.Coins = 14;
                _runTimer = 41.80;
                _player.Spawn(new Vector2(23.5f * LevelData.TileSize, 12f * LevelData.TileSize));
                _cameraPos = ComputeTargetCamera(_player.Position + new Vector2(32f, -20f));
                break;
        }
    }

    private static Rectangle ComputeLetterboxRect(int backW, int backH)
    {
        const float targetAspect = (float)UiW / UiH; // 1.6 (16:10 Steam Deck native)
        float actualAspect = backW / (float)Math.Max(1, backH);

        if (actualAspect > targetAspect)
        {
            int w = (int)MathF.Round(backH * targetAspect);
            return new Rectangle((backW - w) / 2, 0, w, backH);
        }
        else
        {
            int h = (int)MathF.Round(backW / targetAspect);
            return new Rectangle(0, (backH - h) / 2, backW, h);
        }
    }

    private void DrawWorld()
    {
        Vector2 shake = Vector2.Zero;
        if (_shakeTimer > 0f)
        {
            shake = new Vector2(
                (Random.Shared.NextSingle() - 0.5f) * 2f * _shakeIntensity,
                (Random.Shared.NextSingle() - 0.5f) * 2f * _shakeIntensity
            );
        }

        Vector2 cam = new(MathF.Round(_cameraPos.X + shake.X), MathF.Round(_cameraPos.Y + shake.Y));

        _background.Draw(_spriteBatch, _textures, cam.X, cam.Y, LevelData.WorldWidthPx, LevelData.WorldHeightPx, _elapsedVisual);

        // Decorations behind tiles
        foreach (DecorationProp prop in _level.Props)
        {
            float sx = prop.Position.X - cam.X;
            float sy = prop.Position.Y - cam.Y;
            if (sx < -48 || sx > VirtualWorldW + 48 || sy < -80 || sy > VirtualWorldH + 80) continue;

            switch (prop.Kind)
            {
                case PropKind.Crystal:
                {
                    float pulse = 0.65f + 0.35f * MathF.Sin(_elapsedVisual * 3f + prop.Position.X);
                    _spriteBatch.Draw(_textures.SoftGlow, new Rectangle((int)sx - 8, (int)sy - 14, 28, 28),
                        new Color(92, 224, 255, 110) * pulse);
                    _spriteBatch.Draw(_textures.WhitePixel, new Rectangle((int)sx + 4, (int)sy - 8, 4, 8), new Color(110, 232, 255));
                    _spriteBatch.Draw(_textures.WhitePixel, new Rectangle((int)sx + 8, (int)sy - 5, 3, 5), new Color(168, 248, 255));
                    break;
                }
                case PropKind.Lantern:
                {
                    float flicker = 0.80f + 0.20f * MathF.Sin(_elapsedVisual * 7f + prop.Position.Y);
                    _spriteBatch.Draw(_textures.SoftGlow, new Rectangle((int)sx - 10, (int)sy - 18, 32, 32),
                        new Color(255, 198, 92, 95) * flicker);
                    _spriteBatch.Draw(_textures.WhitePixel, new Rectangle((int)sx + 6, (int)sy - 10, 2, 10), new Color(64, 52, 48));
                    _spriteBatch.Draw(_textures.WhitePixel, new Rectangle((int)sx + 5, (int)sy - 12, 4, 4), new Color(255, 222, 118));
                    break;
                }
                case PropKind.Vine:
                {
                    float sway = MathF.Sin(_elapsedVisual * 1.8f + prop.Position.X * 0.1f) * 1.5f;
                    int h = prop.LengthTiles * LevelData.TileSize;
                    for (int vy = 0; vy < h; vy += 4)
                    {
                        float offset = sway * (vy / (float)h);
                        _spriteBatch.Draw(_textures.WhitePixel, new Rectangle((int)(sx + 7 + offset), (int)(sy + vy), 2, 4),
                            new Color(44, 132, 68));
                    }
                    break;
                }
            }
        }

        // Visible Tile Grid
        int minTx = Math.Max(0, (int)MathF.Floor(cam.X / LevelData.TileSize) - 1);
        int maxTx = Math.Min(LevelData.Width - 1, (int)MathF.Ceiling((cam.X + VirtualWorldW) / LevelData.TileSize) + 1);
        int minTy = Math.Max(0, (int)MathF.Floor(cam.Y / LevelData.TileSize) - 1);
        int maxTy = Math.Min(LevelData.Height - 1, (int)MathF.Ceiling((cam.Y + VirtualWorldH) / LevelData.TileSize) + 1);

        for (int ty = minTy; ty <= maxTy; ty++)
        {
            for (int tx = minTx; tx <= maxTx; tx++)
            {
                TileType t = _level.GetTile(tx, ty);
                if (t == TileType.Empty) continue;

                Rectangle src = PixelArtTextures.GetTileSource(t, tx + ty);
                var dst = new Rectangle(
                    (int)(tx * LevelData.TileSize - cam.X),
                    (int)(ty * LevelData.TileSize - cam.Y),
                    LevelData.TileSize,
                    LevelData.TileSize
                );
                _spriteBatch.Draw(_textures.TileAtlas, dst, src, Color.White);
            }
        }

        // Sky Escape Gate at top of map
        Vector2 archScreen = _level.GoalArchBasePx - cam;
        if (archScreen.Y > -64 && archScreen.Y < VirtualWorldH + 80)
        {
            float pulse = 0.78f + 0.22f * MathF.Sin(_elapsedVisual * 4f);
            _spriteBatch.Draw(
                _textures.SoftGlow,
                new Rectangle((int)archScreen.X - 36, (int)archScreen.Y - 62, 72, 72),
                new Color(148, 240, 255, 145) * pulse
            );
            _spriteBatch.Draw(
                _textures.GoalArch,
                new Vector2((int)archScreen.X - 24, (int)archScreen.Y - 56),
                Color.White
            );
        }

        // Coins
        int coinFrame = (int)(_elapsedVisual * 7f) % 4;
        foreach (CoinSpawn coin in _level.Coins)
        {
            if (coin.Collected) continue;

            float bob = MathF.Sin(_elapsedVisual * 3.8f + coin.Position.X * 0.08f) * 2.0f;
            float cx = coin.Position.X - cam.X;
            float cy = coin.Position.Y + bob - cam.Y;
            if (cx < -16 || cx > VirtualWorldW + 16 || cy < -16 || cy > VirtualWorldH + 16) continue;

            if (coin.Value >= 3)
            {
                Color glow = coin.Value >= 5 ? new Color(112, 242, 255, 95) : new Color(255, 224, 96, 85);
                _spriteBatch.Draw(_textures.SoftGlow, new Rectangle((int)cx - 10, (int)cy - 10, 20, 20), glow);
            }

            Rectangle src = PixelArtTextures.GetCoinSource(coin.TierIndex, (coinFrame + coin.Id) % 4);
            _spriteBatch.Draw(_textures.CoinAtlas, new Vector2((int)cx - 6, (int)cy - 6), src, Color.White);
        }

        // Particles
        foreach (Particle p in _particles)
        {
            float alpha = Math.Clamp(p.Life / p.MaxLife, 0f, 1f);
            int s = (int)p.Size;
            _spriteBatch.Draw(_textures.WhitePixel, new Rectangle((int)(p.Pos.X - cam.X), (int)(p.Pos.Y - cam.Y), s, s), p.Color * alpha);
        }

        // Player (drawn in world offset)
        Vector2 savedPos = _player.Position;
        _player.Position -= cam;
        _player.Draw(_spriteBatch, _textures);
        _player.Position = savedPos;

        // Floating coin popups
        foreach (FloatingPopup pop in _popups)
        {
            float alpha = Math.Clamp(pop.Life / pop.MaxLife, 0f, 1f);
            _font.DrawCentered(_spriteBatch, pop.Text, pop.Pos.X - cam.X, pop.Pos.Y - cam.Y, pop.Color * alpha, scale: 1);
        }
    }

    private void DrawUI()
    {
        switch (_screen)
        {
            case GameScreenState.MainMenu:
                DrawMainMenuUI();
                break;
            case GameScreenState.Playing:
                DrawGameplayHud();
                break;
            case GameScreenState.Shop:
                DrawGameplayHud();
                _shop.Draw(_spriteBatch, _textures.WhitePixel, _font, _stats, UiW, UiH);
                break;
            case GameScreenState.HowToPlay:
                if (_howToPlayReturnTo != GameScreenState.MainMenu)
                {
                    DrawGameplayHud();
                }
                _howToPlay.Draw(_spriteBatch, _textures.WhitePixel, _font, UiW, UiH, inActiveRun: _howToPlayReturnTo != GameScreenState.MainMenu);
                break;
            case GameScreenState.Paused:
                DrawGameplayHud();
                DrawPauseUI();
                break;
            case GameScreenState.Victory:
                DrawVictoryUI();
                break;
        }
    }

    private void DrawMainMenuUI()
    {
        _spriteBatch.Draw(_textures.WhitePixel, new Rectangle(0, 0, UiW, UiH), new Color(8, 10, 22, 165));

        int boxW = 720;
        int boxH = 568;
        int bx = (UiW - boxW) / 2;
        int by = (UiH - boxH) / 2;
        ShopOverlay.DrawBox(_spriteBatch, _textures.WhitePixel, bx, by, boxW, boxH,
            new Color(16, 18, 34, 242), new Color(110, 198, 250), 3);

        _font.DrawCentered(_spriteBatch, "ESCAPE TO THE SKIES", UiW * 0.5f, by + 38, new Color(138, 228, 255), scale: 5);
        _font.DrawCentered(_spriteBatch, "CLIMB FROM THE DEEP CHASM TO THE SUNLIT SUMMIT", UiW * 0.5f, by + 96, new Color(196, 214, 236), scale: 2);

        if (_save.BestTimeSeconds.HasValue)
        {
            string bestLine = $"PERSONAL BEST: {SaveManager.FormatTime(_save.BestTimeSeconds.Value)}   |   BEST COINS: {_save.BestCoinsCollected}";
            _font.DrawCentered(_spriteBatch, bestLine, UiW * 0.5f, by + 136, new Color(255, 224, 96), scale: 2);
        }
        else
        {
            _font.DrawCentered(_spriteBatch, "COLLECT COINS. UPGRADE 3 STATS. BEAT YOUR BEST TIME.", UiW * 0.5f, by + 136, new Color(255, 224, 96), scale: 2);
        }

        string[] menuItems =
        {
            "START CLIMB",
            "HOW TO PLAY",
            _isFullscreen ? "FULLSCREEN: ON" : "FULLSCREEN: OFF",
            _audio.Muted ? "SOUND: MUTED" : "SOUND: ON",
            "QUIT GAME",
        };

        for (int i = 0; i < menuItems.Length; i++)
        {
            bool sel = i == _mainMenuIndex;
            Rectangle r = GetMainMenuItemRect(i);
            Color bg = sel ? new Color(34, 60, 82) : new Color(22, 26, 46, 230);
            Color border = sel ? new Color(118, 236, 182) : new Color(72, 92, 134);
            ShopOverlay.DrawBox(_spriteBatch, _textures.WhitePixel, r.X, r.Y, r.Width, r.Height, bg, border, sel ? 3 : 2);
            if (sel)
            {
                float pulse = 0.6f + 0.4f * MathF.Sin(_elapsedVisual * 6f);
                _font.DrawString(_spriteBatch, ">", new Vector2(r.X + 18, r.Y + 13), new Color(138, 248, 196) * pulse, 2);
            }
            _font.DrawCentered(_spriteBatch, menuItems[i], UiW * 0.5f, r.Y + 14, sel ? Color.White : new Color(204, 218, 238), 2);
        }

        int footY = by + boxH - 52;
        int fx = bx + 90;
        _font.DrawDeckBadge(_spriteBatch, "D-PAD", new Vector2(fx, footY), new Color(172, 206, 244), 2);
        fx += _font.MeasureBadgeWidth("D-PAD", 2) + 8;
        _font.DrawString(_spriteBatch, "Select", new Vector2(fx, footY + 4), Color.White, 2);
        fx += 116;

        _font.DrawDeckBadge(_spriteBatch, "A", new Vector2(fx, footY), new Color(96, 224, 128), 2);
        fx += _font.MeasureBadgeWidth("A", 2) + 8;
        _font.DrawString(_spriteBatch, "Confirm", new Vector2(fx, footY + 4), Color.White, 2);
        fx += 130;

        _font.DrawDeckBadge(_spriteBatch, "X", new Vector2(fx, footY), new Color(108, 198, 255), 2);
        fx += _font.MeasureBadgeWidth("X", 2) + 8;
        _font.DrawString(_spriteBatch, "How To Play", new Vector2(fx, footY + 4), Color.White, 2);
    }

    private void DrawGameplayHud()
    {
        float heightProgress = BackgroundRenderer.ComputeHeightProgress(_player.Position.Y, LevelData.WorldHeightPx);
        string zoneName = BackgroundRenderer.GetZoneName(heightProgress);
        int pct = (int)MathF.Round(heightProgress * 100f);

        // Top-Left Timer + Zone Box
        ShopOverlay.DrawBox(_spriteBatch, _textures.WhitePixel, 20, 18, 386, 96,
            new Color(14, 16, 28, 200), new Color(84, 118, 168), 2);

        string pausedTag = _screen != GameScreenState.Playing ? "PAUSED" : "";
        _font.DrawString(_spriteBatch, $"TIME {SaveManager.FormatTime(_runTimer)}", new Vector2(36, 30), Color.White, 3);
        if (!string.IsNullOrEmpty(pausedTag))
        {
            _font.DrawString(_spriteBatch, pausedTag, new Vector2(284, 34), new Color(138, 232, 255), 2);
        }
        string bestStr = _save.BestTimeSeconds.HasValue ? SaveManager.FormatTime(_save.BestTimeSeconds.Value) : "--:--.--";
        _font.DrawString(_spriteBatch, $"BEST {bestStr}", new Vector2(36, 62), new Color(255, 222, 96), 2);
        _font.DrawString(_spriteBatch, $"{zoneName} ({pct}%)", new Vector2(36, 88), new Color(142, 224, 250), 2);

        // Top-Right Coins + Shop Trigger Prompt
        bool anyAffordable = _stats.CanAfford(StatType.Speed)
                          || _stats.CanAfford(StatType.JumpHeight)
                          || _stats.CanAfford(StatType.JumpDuration);

        Color shopBorder = anyAffordable
            ? Color.Lerp(new Color(248, 196, 68), Color.White, 0.5f + 0.5f * MathF.Sin(_elapsedVisual * 7f))
            : new Color(92, 118, 158);

        ShopOverlay.DrawBox(_spriteBatch, _textures.WhitePixel, UiW - 332, 18, 312, 82,
            new Color(14, 16, 28, 200), shopBorder, 2);

        _font.DrawString(_spriteBatch, $"COINS: {_stats.Coins}", new Vector2(UiW - 314, 30), new Color(255, 226, 98), 3);
        _font.DrawDeckBadge(_spriteBatch, "Y", new Vector2(UiW - 314, 62), new Color(252, 206, 72), 2);
        _font.DrawString(
            _spriteBatch,
            anyAffordable ? "SHOP UPGRADE READY!" : "OPEN SHOP (TAB)",
            new Vector2(UiW - 274, 66),
            anyAffordable ? new Color(146, 250, 162) : new Color(196, 212, 232),
            2
        );

        // Bottom-Left Mini Stat Readout + How To Play Badge
        ShopOverlay.DrawBox(_spriteBatch, _textures.WhitePixel, 20, UiH - 54, 412, 36,
            new Color(14, 16, 28, 190), new Color(70, 88, 124), 2);
        _font.DrawString(_spriteBatch, $"SPD {_stats.SpeedLevel}/5", new Vector2(34, UiH - 43), new Color(255, 186, 74), 2);
        _font.DrawString(_spriteBatch, $"HGT {_stats.JumpHeightLevel}/5", new Vector2(168, UiH - 43), new Color(108, 232, 138), 2);
        _font.DrawString(_spriteBatch, $"DUR {_stats.JumpDurationLevel}/5", new Vector2(302, UiH - 43), new Color(98, 224, 255), 2);

        ShopOverlay.DrawBox(_spriteBatch, _textures.WhitePixel, 444, UiH - 54, 198, 36,
            new Color(14, 16, 28, 190), new Color(70, 88, 124), 2);
        _font.DrawDeckBadge(_spriteBatch, "X", new Vector2(454, UiH - 47), new Color(108, 198, 255), 2);
        _font.DrawString(_spriteBatch, "HOW TO PLAY", new Vector2(494, UiH - 43), new Color(206, 224, 246), 2);

        // Right-Edge Vertical Altitude Gauge (Deep Chasm -> Forest -> Sky)
        int barX = UiW - 30;
        int barY = 128;
        int barW = 12;
        int barH = UiH - 180;
        ShopOverlay.DrawBox(_spriteBatch, _textures.WhitePixel, barX - 2, barY - 2, barW + 4, barH + 4,
            new Color(10, 12, 22, 210), new Color(110, 146, 190), 2);

        int third = barH / 3;
        _spriteBatch.Draw(_textures.WhitePixel, new Rectangle(barX, barY, barW, third), new Color(98, 198, 250));
        _spriteBatch.Draw(_textures.WhitePixel, new Rectangle(barX, barY + third, barW, third), new Color(64, 158, 78));
        _spriteBatch.Draw(_textures.WhitePixel, new Rectangle(barX, barY + third * 2, barW, barH - third * 2), new Color(68, 52, 124));

        int markerY = barY + (int)((1f - heightProgress) * (barH - 6));
        _spriteBatch.Draw(_textures.WhitePixel, new Rectangle(barX - 5, markerY, barW + 10, 6), Color.White);
    }

    private void DrawPauseUI()
    {
        _spriteBatch.Draw(_textures.WhitePixel, new Rectangle(0, 0, UiW, UiH), new Color(6, 8, 16, 190));

        int boxW = 480;
        int boxH = 476;
        int bx = (UiW - boxW) / 2;
        int by = (UiH - boxH) / 2;
        ShopOverlay.DrawBox(_spriteBatch, _textures.WhitePixel, bx, by, boxW, boxH,
            new Color(18, 22, 38, 245), new Color(120, 182, 240), 3);

        _font.DrawCentered(_spriteBatch, "PAUSED", UiW * 0.5f, by + 28, Color.White, 4);

        string[] items =
        {
            "RESUME CLIMB",
            "HOW TO PLAY",
            "RESTART RUN",
            _isFullscreen ? "FULLSCREEN: ON" : "FULLSCREEN: OFF",
            _audio.Muted ? "SOUND: MUTED" : "SOUND: ON",
            "MAIN MENU",
            "QUIT GAME",
        };

        for (int i = 0; i < items.Length; i++)
        {
            bool sel = i == _pauseIndex;
            Rectangle r = GetPauseItemRect(i);
            if (sel)
            {
                ShopOverlay.DrawBox(_spriteBatch, _textures.WhitePixel, r.X, r.Y, r.Width, r.Height,
                    new Color(38, 54, 92), new Color(122, 220, 255), 2);
            }
            _font.DrawCentered(_spriteBatch, items[i], UiW * 0.5f, r.Y + 10, sel ? new Color(148, 238, 255) : Color.White, 2);
        }

        _font.DrawCentered(_spriteBatch, "[A] SELECT   |   [B / ESC] RESUME", UiW * 0.5f, by + boxH - 34, new Color(168, 188, 214), 2);
    }

    private void DrawVictoryUI()
    {
        _spriteBatch.Draw(_textures.WhitePixel, new Rectangle(0, 0, UiW, UiH), new Color(24, 62, 108, 175));

        int boxW = 680;
        int boxH = 500;
        int bx = (UiW - boxW) / 2;
        int by = (UiH - boxH) / 2;
        ShopOverlay.DrawBox(_spriteBatch, _textures.WhitePixel, bx, by, boxW, boxH,
            new Color(16, 26, 46, 242), new Color(255, 222, 98), 3);

        _font.DrawCentered(_spriteBatch, "ESCAPED TO THE SKIES!", UiW * 0.5f, by + 36, new Color(138, 234, 255), 4);

        if (_newRecordThisRun)
        {
            _font.DrawCentered(_spriteBatch, "NEW PERSONAL BEST TIME!", UiW * 0.5f, by + 84, new Color(255, 226, 94), 3);
        }

        string timeStr = SaveManager.FormatTime(_runTimer);
        _font.DrawCentered(_spriteBatch, timeStr, UiW * 0.5f, by + 132, Color.White, 6);

        string bestStr = _save.BestTimeSeconds.HasValue ? SaveManager.FormatTime(_save.BestTimeSeconds.Value) : timeStr;
        _font.DrawCentered(_spriteBatch, $"BEST RECORD: {bestStr}", UiW * 0.5f, by + 196, new Color(255, 220, 102), 2);

        ShopOverlay.DrawBox(_spriteBatch, _textures.WhitePixel, bx + 64, by + 236, boxW - 128, 118,
            new Color(22, 34, 58, 230), new Color(96, 152, 208), 2);

        _font.DrawCentered(_spriteBatch, $"COINS COLLECTED: {_stats.TotalCoinsCollected} / {_level.TotalPossibleCoins}", UiW * 0.5f, by + 256, Color.White, 2);
        _font.DrawCentered(_spriteBatch, $"UPGRADES PURCHASED: {_stats.UpgradesPurchased}", UiW * 0.5f, by + 286, new Color(184, 224, 248), 2);
        _font.DrawCentered(_spriteBatch,
            $"SPEED {_stats.SpeedLevel}/5   |   HEIGHT {_stats.JumpHeightLevel}/5   |   DURATION {_stats.JumpDurationLevel}/5",
            UiW * 0.5f, by + 318, new Color(142, 240, 180), 2);

        string[] btns = { "RUN AGAIN [A]", "MAIN MENU [B]" };
        for (int i = 0; i < btns.Length; i++)
        {
            bool sel = i == _victoryIndex;
            Rectangle r = GetVictoryButtonRect(i);
            ShopOverlay.DrawBox(_spriteBatch, _textures.WhitePixel, r.X, r.Y, r.Width, r.Height,
                sel ? new Color(36, 72, 62) : new Color(24, 34, 54),
                sel ? new Color(118, 246, 156) : new Color(92, 122, 160), sel ? 3 : 2);
            _font.DrawCentered(_spriteBatch, btns[i], r.X + r.Width * 0.5f, r.Y + 20, Color.White, 2);
        }

        _font.DrawCentered(_spriteBatch, "[D-PAD / CLICK] CHOOSE  |  [A] CONFIRM  |  [B] MAIN MENU", UiW * 0.5f, by + boxH - 34, new Color(168, 198, 224), 2);
    }

    private static Rectangle GetMainMenuItemRect(int index)
    {
        const int boxW = 720;
        const int boxH = 568;
        int bx = (UiW - boxW) / 2;
        int by = (UiH - boxH) / 2;
        return new Rectangle(bx + 110, by + 186 + index * 58, boxW - 220, 46);
    }

    private static Rectangle GetPauseItemRect(int index)
    {
        const int boxW = 480;
        const int boxH = 476;
        int bx = (UiW - boxW) / 2;
        int by = (UiH - boxH) / 2;
        return new Rectangle(bx + 48, by + 94 + index * 46 - 5, boxW - 96, 36);
    }

    private static Rectangle GetVictoryButtonRect(int index)
    {
        const int boxW = 680;
        const int boxH = 500;
        int bx = (UiW - boxW) / 2;
        int by = (UiH - boxH) / 2;
        return new Rectangle(bx + 78 + index * 284, by + 382, 240, 56);
    }
}
