using Microsoft.Maui.Graphics;
using RadioE45.Visuals;

namespace RadioE45.Controls;

// Simulated VU meter (levels in RadioE45.Core/Visuals/VuMeterLevels, shared with the Uno
// SKCanvasElement version): this view only runs the timer and draws the cells.
public class VuMeterView : GraphicsView
{
    public static readonly BindableProperty IsActiveProperty = BindableProperty.Create(
        nameof(IsActive), typeof(bool), typeof(VuMeterView), false, propertyChanged: OnIsActiveChanged);

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private readonly VuMeterLevels _levels = new();
    private IDispatcherTimer? _timer;

    public VuMeterView()
    {
        Drawable = new VuMeterDrawable(_levels);
    }

    private static void OnIsActiveChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (VuMeterView)bindable;
        if ((bool)newValue)
            view.Start();
        else
            view.Stop();
    }

    private void Start()
    {
        if (_timer is not null)
            return;

        IDispatcher? dispatcher = Dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        _levels.Start();

        _timer = dispatcher.CreateTimer();
        _timer.Interval = VuMeterLevels.TickInterval;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void Stop()
    {
        if (_timer is null)
            return;

        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;

        _levels.Reset();
        Invalidate();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _levels.Tick();
        Invalidate();
    }

    private sealed class VuMeterDrawable(VuMeterLevels levels) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            const int columns = VuMeterLevels.BarCount;
            const int rows = VuMeterLevels.Rows;
            float cellWidth = (dirtyRect.Width - VuMeterLevels.HorizontalSpacing * (columns - 1)) / columns;
            float cellHeight = (dirtyRect.Height - VuMeterLevels.VerticalSpacing * (rows - 1)) / rows;
            if (cellWidth <= 0 || cellHeight <= 0)
                return;

            for (int col = 0; col < columns; col++)
            {
                int lit = levels.LitCells(col);
                float x = dirtyRect.X + col * (cellWidth + VuMeterLevels.HorizontalSpacing);

                for (int row = 0; row < rows; row++)
                {
                    float y = dirtyRect.Bottom - (row + 1) * cellHeight - row * VuMeterLevels.VerticalSpacing;
                    Color color = Color.FromUint(VuMeterLevels.RowArgb(row));
                    canvas.FillColor = row < lit ? color : color.WithAlpha(VuMeterLevels.UnlitAlpha);
                    canvas.FillRoundedRectangle(x, y, cellWidth, cellHeight, VuMeterLevels.CellCornerRadius);
                }
            }
        }
    }
}
