using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GameTrainer.Core.Catalog;

namespace GameTrainer.Gui;

/// <summary>
/// Custom Windows 2000 oscilloscope / chart control rendering training progression curves
/// (<c>GoalRate %</c> and <c>MeanReward</c>) from <c>metrics.jsonl</c>, with parent curve overlay.
/// </summary>
public sealed class ImprovementChartControl : Control
{
    private static readonly IBrush PlotBackgroundBrush = new SolidColorBrush(Color.Parse("#001400"));
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.Parse("#004000")), 1);
    private static readonly Pen GoalRatePen = new(new SolidColorBrush(Color.Parse("#00FF66")), 2.2);
    private static readonly Pen ParentGoalRatePen = new(
        new SolidColorBrush(Color.Parse("#66CCFF")),
        1.8,
        new DashStyle([4, 3], 0));
    private static readonly Pen MeanRewardPen = new(new SolidColorBrush(Color.Parse("#FFCC00")), 1.8);

    private IReadOnlyList<EpisodeMetricPoint> _metrics = [];
    private IReadOnlyList<EpisodeMetricPoint> _parentMetrics = [];
    private string _versionLabel = string.Empty;
    private string? _parentLabel;

    public void SetSeries(
        string versionLabel,
        IReadOnlyList<EpisodeMetricPoint> metrics,
        string? parentLabel,
        IReadOnlyList<EpisodeMetricPoint> parentMetrics)
    {
        _versionLabel = versionLabel;
        _metrics = metrics;
        _parentLabel = parentLabel;
        _parentMetrics = parentMetrics;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var rect = new Rect(Bounds.Size);
        if (rect.Width < 40 || rect.Height < 40)
        {
            return;
        }

        context.FillRectangle(PlotBackgroundBrush, rect);

        const double leftPad = 48;
        const double rightPad = 18;
        const double topPad = 28;
        const double bottomPad = 28;

        var plotRect = new Rect(
            leftPad,
            topPad,
            Math.Max(10, rect.Width - leftPad - rightPad),
            Math.Max(10, rect.Height - topPad - bottomPad));

        // Draw horizontal and vertical grid lines (Win2000 Task Manager style)
        for (var i = 0; i <= 4; i++)
        {
            var y = plotRect.Top + plotRect.Height * (i / 4.0);
            context.DrawLine(GridPen, new Point(plotRect.Left, y), new Point(plotRect.Right, y));

            var pctLabel = $"{100 - i * 25}%";
            DrawText(context, pctLabel, new Point(6, y - 7), Brushes.LightGreen, 10);
        }

        for (var j = 0; j <= 5; j++)
        {
            var x = plotRect.Left + plotRect.Width * (j / 5.0);
            context.DrawLine(GridPen, new Point(x, plotRect.Top), new Point(x, plotRect.Bottom));
        }

        // Legend bar at top
        var legend = $"Selected ({_versionLabel}): Green=Goal%  Yellow=Reward(scaled)";
        if (!string.IsNullOrWhiteSpace(_parentLabel) && _parentMetrics.Count > 0)
        {
            legend += $"   |   Parent ({_parentLabel}): Cyan Dashed=Goal%";
        }
        DrawText(context, legend, new Point(leftPad, 6), Brushes.White, 11);

        if (_metrics.Count == 0 && _parentMetrics.Count == 0)
        {
            DrawText(
                context,
                "No metrics.jsonl data recorded for this model version.",
                new Point(plotRect.Left + 16, plotRect.Center.Y - 8),
                Brushes.Silver,
                12);
            return;
        }

        var maxEpisode = Math.Max(
            _metrics.Count > 0 ? _metrics.Max(m => m.Episode) : 1,
            _parentMetrics.Count > 0 ? _parentMetrics.Max(m => m.Episode) : 1);
        maxEpisode = Math.Max(1, maxEpisode);

        // Draw X-axis labels
        DrawText(context, "Ep 0", new Point(plotRect.Left, plotRect.Bottom + 6), Brushes.LightGreen, 10);
        DrawText(context, $"Ep {maxEpisode}", new Point(plotRect.Right - 42, plotRect.Bottom + 6), Brushes.LightGreen, 10);

        // Plot parent goal rate curve if present
        if (_parentMetrics.Count > 0)
        {
            DrawCurve(context, plotRect, _parentMetrics, maxEpisode, p => Math.Clamp(p.GoalRate, 0f, 1f), ParentGoalRatePen);
        }

        // Plot selected model reward curve (mapped from -0.5..1.0 into 0..1)
        if (_metrics.Count > 0)
        {
            DrawCurve(
                context,
                plotRect,
                _metrics,
                maxEpisode,
                p => Math.Clamp((p.MeanReward + 0.5f) / 1.5f, 0f, 1f),
                MeanRewardPen);

            DrawCurve(
                context,
                plotRect,
                _metrics,
                maxEpisode,
                p => Math.Clamp(p.GoalRate, 0f, 1f),
                GoalRatePen);
        }
    }

    private static void DrawCurve(
        DrawingContext context,
        Rect plotRect,
        IReadOnlyList<EpisodeMetricPoint> points,
        int maxEpisode,
        Func<EpisodeMetricPoint, float> normalizedSelector,
        Pen pen)
    {
        Point? previous = null;
        foreach (var pt in points)
        {
            var xRatio = Math.Clamp((double)pt.Episode / maxEpisode, 0.0, 1.0);
            var yRatio = Math.Clamp(normalizedSelector(pt), 0f, 1f);

            var current = new Point(
                plotRect.Left + xRatio * plotRect.Width,
                plotRect.Bottom - yRatio * plotRect.Height);

            if (previous is { } prev)
            {
                context.DrawLine(pen, prev, current);
            }

            context.DrawEllipse(pen.Brush, pen, current, 2.5, 2.5);
            previous = current;
        }
    }

    private static void DrawText(
        DrawingContext context,
        string text,
        Point origin,
        IBrush foreground,
        double fontSize)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(Win2000Theme.MonospaceFont),
            fontSize,
            foreground);

        context.DrawText(formatted, origin);
    }
}
