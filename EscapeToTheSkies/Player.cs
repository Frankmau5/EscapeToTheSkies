using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace EscapeToTheSkies;

public struct Particle
{
    public Vector2 Pos;
    public Vector2 Vel;
    public Color Color;
    public float Life;
    public float MaxLife;
    public float Size;
}

public struct FloatingPopup
{
    public Vector2 Pos;
    public string Text;
    public Color Color;
    public float Life;
    public float MaxLife;
}

public sealed class Player
{
    public const float HalfW = 5f;
    public const float HeightPx = 14f;

    public Vector2 Position; // Bottom-center (feet)
    public Vector2 Velocity;

    public bool OnGround { get; private set; }
    public bool FacingRight { get; private set; } = true;
    public bool IsSustainingJump { get; private set; }

    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private float _sustainRemaining;
    private float _dropThroughTimer;
    private float _animTimer;
    private float _stepDustTimer;
    private Vector2 _squash = Vector2.One;

    public Rectangle Hitbox => new(
        (int)MathF.Floor(Position.X - HalfW),
        (int)MathF.Floor(Position.Y - HeightPx),
        (int)(HalfW * 2f),
        (int)HeightPx
    );

    public void Spawn(Vector2 feetPos)
    {
        Position = feetPos;
        Velocity = Vector2.Zero;
        OnGround = false;
        FacingRight = true;
        IsSustainingJump = false;
        _coyoteTimer = 0f;
        _jumpBufferTimer = 0f;
        _sustainRemaining = 0f;
        _dropThroughTimer = 0f;
        _animTimer = 0f;
        _stepDustTimer = 0f;
        _squash = Vector2.One;
    }

    public void Update(
        float dt,
        InputState input,
        PlayerStats stats,
        LevelData level,
        AudioSynthesizer audio,
        List<Particle> particles)
    {
        bool wasOnGround = OnGround;
        float prevFeetY = Position.Y;

        if (_dropThroughTimer > 0f)
        {
            _dropThroughTimer = MathF.Max(0f, _dropThroughTimer - dt);
        }

        // Smooth squash & stretch return to (1, 1)
        _squash.X = MathHelper.Lerp(_squash.X, 1f, Math.Clamp(dt * 14f, 0f, 1f));
        _squash.Y = MathHelper.Lerp(_squash.Y, 1f, Math.Clamp(dt * 14f, 0f, 1f));

        // Coyote & Jump Buffer timers
        if (OnGround)
        {
            _coyoteTimer = 0.10f;
        }
        else
        {
            _coyoteTimer = MathF.Max(0f, _coyoteTimer - dt);
        }

        if (input.JumpPressed)
        {
            // Check Down + Jump to drop through one-way platforms
            if (OnGround && input.MoveY > 0.65f && IsStandingOnlyOnOneWay(level))
            {
                _dropThroughTimer = 0.18f;
                OnGround = false;
                _coyoteTimer = 0f;
                _jumpBufferTimer = 0f;
                Position.Y += 2f;
                Velocity.Y = 35f;
            }
            else
            {
                _jumpBufferTimer = 0.12f;
            }
        }
        else
        {
            _jumpBufferTimer = MathF.Max(0f, _jumpBufferTimer - dt);
        }

        // Horizontal Acceleration
        float targetVx = input.MoveX * stats.RunSpeed;
        float accel = OnGround ? stats.GroundAccel : stats.AirAccel;
        if (MathF.Abs(input.MoveX) > 0.05f)
        {
            FacingRight = input.MoveX > 0f;
        }

        if (Velocity.X < targetVx)
        {
            Velocity.X = MathF.Min(targetVx, Velocity.X + accel * dt);
        }
        else if (Velocity.X > targetVx)
        {
            Velocity.X = MathF.Max(targetVx, Velocity.X - accel * dt);
        }

        // Trigger Jump if buffered + coyote/grounded
        if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
        {
            Velocity.Y = -stats.JumpVelocity;
            _sustainRemaining = stats.JumpSustainTime;
            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;
            OnGround = false;
            _squash = new Vector2(0.76f, 1.28f);
            audio.PlayJump(stats.JumpHeightLevel);
            SpawnDust(particles, Position, 6, new Color(210, 232, 255));
        }

        // Early jump release variable cut
        if (input.JumpReleased && Velocity.Y < -55f)
        {
            Velocity.Y *= PlayerStats.EarlyReleaseCut;
            _sustainRemaining = 0f;
        }

        // Vertical Gravity & Jump Duration Sustain!
        IsSustainingJump = false;
        float gScale = 1f;
        if (Velocity.Y < 0f && input.JumpHeld && _sustainRemaining > 0f)
        {
            gScale = PlayerStats.SustainGravityScale;
            _sustainRemaining -= dt;
            IsSustainingJump = true;

            if (particles.Count < 220)
            {
                particles.Add(new Particle
                {
                    Pos = Position + new Vector2((Random.Shared.NextSingle() - 0.5f) * 8f, -2f),
                    Vel = new Vector2((Random.Shared.NextSingle() - 0.5f) * 26f, 35f + Random.Shared.NextSingle() * 35f),
                    Color = new Color(128, 244, 255),
                    Life = 0.20f,
                    MaxLife = 0.20f,
                    Size = 2f,
                });
            }
        }
        else if (MathF.Abs(Velocity.Y) < PlayerStats.ApexThresholdVy)
        {
            gScale = PlayerStats.ApexGravityScale;
        }
        else if (Velocity.Y > 0f)
        {
            gScale = PlayerStats.FallGravityScale;
        }

        Velocity.Y = MathF.Min(PlayerStats.MaxFallSpeed, Velocity.Y + PlayerStats.BaseGravity * gScale * dt);

        // Move & Collide X (stepped to avoid tunneling at max Speed)
        MoveX(Velocity.X * dt, level);

        // Move & Collide Y
        MoveY(Velocity.Y * dt, prevFeetY, level);

        // Landing polish
        if (!wasOnGround && OnGround)
        {
            _squash = new Vector2(1.26f, 0.78f);
            audio.PlayLand();
            SpawnDust(particles, Position, 5, new Color(195, 206, 224));
        }

        // Running dust
        if (OnGround && MathF.Abs(Velocity.X) > 45f)
        {
            _animTimer += dt * (6f + MathF.Abs(Velocity.X) * 0.045f);
            _stepDustTimer -= dt;
            if (_stepDustTimer <= 0f)
            {
                _stepDustTimer = MathF.Max(0.08f, 0.22f - stats.SpeedLevel * 0.025f);
                SpawnDust(particles, Position, 2, new Color(176, 186, 206));
            }
        }
        else
        {
            _animTimer += dt * 3f;
        }
    }

    private void MoveX(float dx, LevelData level)
    {
        Position.X += dx;
        Position.X = Math.Clamp(Position.X, HalfW, LevelData.WorldWidthPx - HalfW);

        int topTile = (int)MathF.Floor((Position.Y - HeightPx + 1f) / LevelData.TileSize);
        int botTile = (int)MathF.Floor((Position.Y - 1f) / LevelData.TileSize);
        int leftTile = (int)MathF.Floor((Position.X - HalfW) / LevelData.TileSize);
        int rightTile = (int)MathF.Floor((Position.X + HalfW - 0.01f) / LevelData.TileSize);

        for (int ty = topTile; ty <= botTile; ty++)
        {
            for (int tx = leftTile; tx <= rightTile; tx++)
            {
                if (!LevelData.IsSolid(level.GetTile(tx, ty)))
                {
                    continue;
                }

                if (dx > 0f)
                {
                    Position.X = tx * LevelData.TileSize - HalfW;
                    Velocity.X = 0f;
                }
                else if (dx < 0f)
                {
                    Position.X = (tx + 1) * LevelData.TileSize + HalfW;
                    Velocity.X = 0f;
                }
            }
        }
    }

    private void MoveY(float dy, float prevFeetY, LevelData level)
    {
        OnGround = false;
        Position.Y += dy;

        if (Position.Y > LevelData.WorldHeightPx)
        {
            Position.Y = LevelData.WorldHeightPx;
            Velocity.Y = 0f;
            OnGround = true;
            return;
        }

        int leftTile = (int)MathF.Floor((Position.X - HalfW + 0.5f) / LevelData.TileSize);
        int rightTile = (int)MathF.Floor((Position.X + HalfW - 0.5f) / LevelData.TileSize);

        if (dy >= 0f)
        {
            int feetTile = (int)MathF.Floor((Position.Y - 0.01f) / LevelData.TileSize);
            int startTile = (int)MathF.Floor((prevFeetY - 0.01f) / LevelData.TileSize);

            for (int ty = Math.Max(0, startTile); ty <= feetTile; ty++)
            {
                float tileTop = ty * LevelData.TileSize;
                for (int tx = leftTile; tx <= rightTile; tx++)
                {
                    TileType t = level.GetTile(tx, ty);
                    bool solidHit = LevelData.IsSolid(t);
                    bool oneWayHit = LevelData.IsOneWay(t) && _dropThroughTimer <= 0f && prevFeetY <= tileTop + 2.5f;

                    if (solidHit || oneWayHit)
                    {
                        Position.Y = tileTop;
                        Velocity.Y = 0f;
                        OnGround = true;
                        return;
                    }
                }
            }
        }
        else
        {
            int headTile = (int)MathF.Floor((Position.Y - HeightPx) / LevelData.TileSize);
            for (int tx = leftTile; tx <= rightTile; tx++)
            {
                if (LevelData.IsSolid(level.GetTile(tx, headTile)))
                {
                    Position.Y = (headTile + 1) * LevelData.TileSize + HeightPx;
                    Velocity.Y = 0f;
                    return;
                }
            }
        }
    }

    private bool IsStandingOnlyOnOneWay(LevelData level)
    {
        int feetTile = (int)MathF.Floor((Position.Y + 1f) / LevelData.TileSize);
        int leftTile = (int)MathF.Floor((Position.X - HalfW + 0.5f) / LevelData.TileSize);
        int rightTile = (int)MathF.Floor((Position.X + HalfW - 0.5f) / LevelData.TileSize);

        bool sawOneWay = false;
        for (int tx = leftTile; tx <= rightTile; tx++)
        {
            TileType t = level.GetTile(tx, feetTile);
            if (LevelData.IsSolid(t)) return false;
            if (LevelData.IsOneWay(t)) sawOneWay = true;
        }
        return sawOneWay;
    }

    private static void SpawnDust(List<Particle> particles, Vector2 pos, int count, Color color)
    {
        for (int i = 0; i < count && particles.Count < 240; i++)
        {
            particles.Add(new Particle
            {
                Pos = pos + new Vector2((Random.Shared.NextSingle() - 0.5f) * 8f, -1f),
                Vel = new Vector2((Random.Shared.NextSingle() - 0.5f) * 48f, -10f - Random.Shared.NextSingle() * 26f),
                Color = color,
                Life = 0.24f,
                MaxLife = 0.24f,
                Size = Random.Shared.NextSingle() > 0.5f ? 2f : 1f,
            });
        }
    }

    public void Draw(SpriteBatch sb, PixelArtTextures tex)
    {
        int frame;
        if (!OnGround)
        {
            if (IsSustainingJump) frame = 7;
            else if (Velocity.Y < 0f) frame = 5;
            else frame = 6;
        }
        else if (MathF.Abs(Velocity.X) > 22f)
        {
            frame = 1 + ((int)_animTimer % 4);
        }
        else
        {
            frame = 0;
        }

        if (IsSustainingJump)
        {
            sb.Draw(
                tex.SoftGlow,
                new Rectangle((int)Position.X - 14, (int)Position.Y - 18, 28, 24),
                new Color(102, 238, 255, 115)
            );
        }

        Rectangle src = PixelArtTextures.GetPlayerSource(frame);
        SpriteEffects flip = FacingRight ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

        Vector2 origin = new(8f, 15f);
        Vector2 drawPos = new(MathF.Round(Position.X), MathF.Round(Position.Y));
        sb.Draw(tex.PlayerAtlas, drawPos, src, Color.White, 0f, origin, _squash, flip, 0f);
    }
}
