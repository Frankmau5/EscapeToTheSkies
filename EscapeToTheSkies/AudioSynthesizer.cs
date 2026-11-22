using Microsoft.Xna.Framework.Audio;

namespace EscapeToTheSkies;

public sealed class AudioSynthesizer : IDisposable
{
    private const int SampleRate = 22050;

    private readonly bool _audioReady;
    private readonly SoundEffect? _jump;
    private readonly SoundEffect? _land;
    private readonly SoundEffect? _coinBronze;
    private readonly SoundEffect? _coinGold;
    private readonly SoundEffect? _coinRelic;
    private readonly SoundEffect? _menuMove;
    private readonly SoundEffect? _menuError;
    private readonly SoundEffect? _shopOpen;
    private readonly SoundEffect? _shopClose;
    private readonly SoundEffect? _upgrade;
    private readonly SoundEffect? _victory;

    public bool Muted { get; set; }

    public AudioSynthesizer()
    {
        try
        {
            _jump = BuildSweep(0.11f, 240f, 520f, 0.24f, WaveKind.Triangle);
            _land = BuildNoiseThump(0.07f, 0.16f);
            _coinBronze = BuildTwoTone(0.055f, 784f, 0.085f, 1175f, 0.22f);
            _coinGold = BuildArpeggio(new[] { 659f, 880f, 1318f }, 0.055f, 0.22f);
            _coinRelic = BuildArpeggio(new[] { 587f, 880f, 1175f, 1760f }, 0.06f, 0.24f);
            _menuMove = BuildTone(0.035f, 540f, 0.14f, WaveKind.Sine);
            _menuError = BuildTwoTone(0.06f, 190f, 0.09f, 145f, 0.20f);
            _shopOpen = BuildArpeggio(new[] { 440f, 554f, 659f }, 0.045f, 0.18f);
            _shopClose = BuildArpeggio(new[] { 659f, 554f, 440f }, 0.04f, 0.15f);
            _upgrade = BuildArpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.065f, 0.25f);
            _victory = BuildArpeggio(new[] { 523.25f, 659.25f, 783.99f, 987.77f, 1174.66f, 1567.98f }, 0.095f, 0.28f);
            _audioReady = true;
        }
        catch
        {
            _audioReady = false;
        }
    }

    public void PlayJump(int heightLevel)
    {
        float pitch = Math.Clamp(heightLevel * 0.07f, 0f, 0.35f);
        Play(_jump, 0.72f, pitch);
    }

    public void PlayLand() => Play(_land, 0.55f, 0f);

    public void PlayCoin(int value)
    {
        if (value >= 5)
        {
            Play(_coinRelic, 0.78f, 0f);
        }
        else if (value >= 3)
        {
            Play(_coinGold, 0.75f, 0f);
        }
        else
        {
            Play(_coinBronze, 0.72f, 0f);
        }
    }

    public void PlayMenuMove() => Play(_menuMove, 0.6f, 0f);
    public void PlayMenuError() => Play(_menuError, 0.7f, 0f);
    public void PlayShopOpen() => Play(_shopOpen, 0.65f, 0f);
    public void PlayShopClose() => Play(_shopClose, 0.60f, 0f);
    public void PlayUpgrade(int newLevel) => Play(_upgrade, 0.85f, Math.Clamp((newLevel - 1) * 0.05f, 0f, 0.25f));
    public void PlayVictory() => Play(_victory, 0.90f, 0f);

    private void Play(SoundEffect? sfx, float volume, float pitch)
    {
        if (!_audioReady || Muted || sfx == null)
        {
            return;
        }

        try
        {
            sfx.Play(volume, pitch, 0f);
        }
        catch
        {
            // Ignore transient audio errors
        }
    }

    private enum WaveKind { Sine, Triangle }

    private static SoundEffect BuildTone(float duration, float freq, float amp, WaveKind kind)
    {
        int samples = (int)(SampleRate * duration);
        byte[] pcm = new byte[samples * 2];
        double phase = 0.0;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float env = MathF.Min(1f, t * 14f) * MathF.Pow(1f - t, 1.4f);
            phase += 2.0 * Math.PI * freq / SampleRate;
            float sample = SampleWave(phase, kind) * env * amp;
            WriteSample(pcm, i, sample);
        }

        return new SoundEffect(pcm, SampleRate, AudioChannels.Mono);
    }

    private static SoundEffect BuildSweep(float duration, float startFreq, float endFreq, float amp, WaveKind kind)
    {
        int samples = (int)(SampleRate * duration);
        byte[] pcm = new byte[samples * 2];
        double phase = 0.0;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float freq = startFreq + (endFreq - startFreq) * MathF.Sqrt(t);
            float env = MathF.Min(1f, t * 18f) * (1f - t);
            phase += 2.0 * Math.PI * freq / SampleRate;
            float sample = SampleWave(phase, kind) * env * amp;
            WriteSample(pcm, i, sample);
        }

        return new SoundEffect(pcm, SampleRate, AudioChannels.Mono);
    }

    private static SoundEffect BuildTwoTone(float d1, float f1, float d2, float f2, float amp)
    {
        return BuildArpeggio(new[] { f1, f2 }, (d1 + d2) * 0.5f, amp);
    }

    private static SoundEffect BuildArpeggio(float[] notes, float noteDuration, float amp)
    {
        int noteSamples = (int)(SampleRate * noteDuration);
        int tailSamples = (int)(SampleRate * 0.08f);
        int totalSamples = noteSamples * notes.Length + tailSamples;
        byte[] pcm = new byte[totalSamples * 2];
        double phase = 0.0;

        for (int n = 0; n < notes.Length; n++)
        {
            int start = n * noteSamples;
            int len = n == notes.Length - 1 ? noteSamples + tailSamples : noteSamples;
            float freq = notes[n];

            for (int i = 0; i < len && start + i < totalSamples; i++)
            {
                float t = (float)i / len;
                float env = MathF.Min(1f, t * 22f) * MathF.Pow(1f - t, 1.5f);
                phase += 2.0 * Math.PI * freq / SampleRate;
                float fundamental = MathF.Sin((float)phase);
                float harmonic = 0.25f * MathF.Sin((float)(phase * 2.0));
                WriteSample(pcm, start + i, (fundamental + harmonic) * env * amp);
            }
        }

        return new SoundEffect(pcm, SampleRate, AudioChannels.Mono);
    }

    private static SoundEffect BuildNoiseThump(float duration, float amp)
    {
        int samples = (int)(SampleRate * duration);
        byte[] pcm = new byte[samples * 2];
        uint rng = 0x13579BDFu;
        float lp = 0f;

        for (int i = 0; i < samples; i++)
        {
            rng = rng * 1664525u + 1013904223u;
            float white = ((rng >> 16) / 32768f) - 1f;
            lp = lp * 0.85f + white * 0.15f;
            float t = (float)i / samples;
            float env = MathF.Pow(1f - t, 2.2f);
            WriteSample(pcm, i, lp * env * amp * 2.2f);
        }

        return new SoundEffect(pcm, SampleRate, AudioChannels.Mono);
    }

    private static float SampleWave(double phase, WaveKind kind)
    {
        if (kind == WaveKind.Sine)
        {
            return MathF.Sin((float)phase);
        }

        double norm = (phase / (2.0 * Math.PI)) % 1.0;
        if (norm < 0)
        {
            norm += 1.0;
        }
        return (float)(norm < 0.5 ? 4.0 * norm - 1.0 : 3.0 - 4.0 * norm);
    }

    private static void WriteSample(byte[] pcm, int index, float sample)
    {
        short s = (short)Math.Clamp(sample * 32767f, -32767f, 32767f);
        int offset = index * 2;
        pcm[offset] = (byte)(s & 0xFF);
        pcm[offset + 1] = (byte)((s >> 8) & 0xFF);
    }

    public void Dispose()
    {
        _jump?.Dispose();
        _land?.Dispose();
        _coinBronze?.Dispose();
        _coinGold?.Dispose();
        _coinRelic?.Dispose();
        _menuMove?.Dispose();
        _menuError?.Dispose();
        _shopOpen?.Dispose();
        _shopClose?.Dispose();
        _upgrade?.Dispose();
        _victory?.Dispose();
    }
}
