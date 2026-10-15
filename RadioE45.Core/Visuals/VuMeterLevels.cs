namespace RadioE45.Visuals;

/// <summary>
/// Simulated VU meter levels, shared by the MAUI (GraphicsView) and Uno (SKCanvasElement) views.
/// No platform exposes raw PCM levels for the streamed audio without native taps (Visualizer on
/// Android, MTAudioProcessingTap on iOS/Mac, AudioGraph on Windows, nothing on libVLC/web), so the
/// bars follow a smoothed random walk instead of the real signal. Same look-and-feel everywhere,
/// driven purely by whether the radio is playing. The views own the timer and the drawing.
/// </summary>
public sealed class VuMeterLevels
{
    public const int BarCount = 2;
    public const int Rows = 4;
    public static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(90);

    /// <summary>Spacing between bars and between cells, in device-independent units.</summary>
    public const float HorizontalSpacing = 4f;
    public const float VerticalSpacing = 3f;
    public const float CellCornerRadius = 2f;

    /// <summary>Opacity of the cells above the current level.</summary>
    public const float UnlitAlpha = 0.15f;

    public const uint GreenArgb = 0xFF30A46C;
    public const uint YellowArgb = 0xFFF5D90A;
    public const uint RedArgb = 0xFFE5484D;

    private const int GreenRows = 2;
    private const int YellowRows = 1;
    private const double NewTargetChance = 0.35;
    private const double AttackFactor = 0.6;
    private const double DecayFactor = 0.25;

    private readonly Random _random;
    private readonly double[] _levels = new double[BarCount];
    private readonly double[] _targets = new double[BarCount];

    public VuMeterLevels(Random? random = null) => _random = random ?? new Random();

    /// <summary>Picks new random targets: call when the animation starts.</summary>
    public void Start()
    {
        for (int i = 0; i < BarCount; i++)
            _targets[i] = _random.NextDouble();
    }

    /// <summary>Clears every bar: call when the animation stops.</summary>
    public void Reset() => Array.Clear(_levels);

    /// <summary>Advances the animation by one tick.</summary>
    public void Tick()
    {
        for (int i = 0; i < BarCount; i++)
        {
            if (_random.NextDouble() < NewTargetChance)
                _targets[i] = _random.NextDouble();

            double target = _targets[i];
            double current = _levels[i];
            _levels[i] = current < target
                ? current + (target - current) * AttackFactor
                : current + (target - current) * DecayFactor;
        }
    }

    /// <summary>Number of lit cells (0..<see cref="Rows"/>) of a bar, from the bottom.</summary>
    public int LitCells(int bar) => (int)Math.Round(Math.Clamp(_levels[bar], 0, 1) * Rows);

    /// <summary>ARGB color of a row, counted from the bottom: green, then yellow, then red.</summary>
    public static uint RowArgb(int row) =>
        row < GreenRows ? GreenArgb : row < GreenRows + YellowRows ? YellowArgb : RedArgb;
}
