using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace GameTrainer.Gui;

/// <summary>
/// Classic Windows 2000 ("Win2k") color palette, 3D beveled border helpers,
/// and control builders.
/// </summary>
public static class Win2000Theme
{
    // Classic Win2000 3D Objects warm gray (#D4D0C8)
    public static readonly Color FaceColor = Color.Parse("#D4D0C8");
    public static readonly Color LightHighlightColor = Color.Parse("#FFFFFF");
    public static readonly Color ThreeDShadowColor = Color.Parse("#808080");
    public static readonly Color DarkShadowColor = Color.Parse("#404040");
    public static readonly Color WindowBackgroundColor = Color.Parse("#FFFFFF");
    public static readonly Color ActiveTitleStartColor = Color.Parse("#0A246A");
    public static readonly Color ActiveTitleEndColor = Color.Parse("#A6CAF0");
    public static readonly Color SelectionNavyColor = Color.Parse("#0A246A");
    public static readonly Color TextColor = Color.Parse("#000000");
    public static readonly Color MutedTextColor = Color.Parse("#404040");

    public static readonly IBrush FaceBrush = new SolidColorBrush(FaceColor);
    public static readonly IBrush HighlightBrush = new SolidColorBrush(LightHighlightColor);
    public static readonly IBrush ShadowBrush = new SolidColorBrush(ThreeDShadowColor);
    public static readonly IBrush DarkShadowBrush = new SolidColorBrush(DarkShadowColor);
    public static readonly IBrush WindowBrush = new SolidColorBrush(WindowBackgroundColor);
    public static readonly IBrush SelectionBrush = new SolidColorBrush(SelectionNavyColor);
    public static readonly IBrush TextBrush = new SolidColorBrush(TextColor);
    public static readonly IBrush MutedTextBrush = new SolidColorBrush(MutedTextColor);

    public static readonly FontFamily ClassicFont = new("Tahoma, Microsoft Sans Serif, Segoe UI, sans-serif");
    public static readonly FontFamily MonospaceFont = new("Lucida Console, Consolas, monospace");

    /// <summary>
    /// Creates a classic 2px 3D raised Windows 2000 panel (white top-left, dark bottom-right).
    /// </summary>
    public static Control CreateRaisedPanel(Control child, Thickness? padding = null)
    {
        return new Border
        {
            Background = FaceBrush,
            BorderBrush = DarkShadowBrush,
            BorderThickness = new Thickness(1, 1, 2, 2),
            Child = new Border
            {
                BorderBrush = HighlightBrush,
                BorderThickness = new Thickness(1, 1, 0, 0),
                Padding = padding ?? new Thickness(6),
                Child = child
            }
        };
    }

    /// <summary>
    /// Creates a classic 2px 3D sunken Windows 2000 well (gray top-left, white bottom-right).
    /// </summary>
    public static Control CreateSunkenPanel(Control child, IBrush? background = null, Thickness? padding = null)
    {
        return new Border
        {
            Background = background ?? WindowBrush,
            BorderBrush = HighlightBrush,
            BorderThickness = new Thickness(1, 1, 2, 2),
            Child = new Border
            {
                BorderBrush = ShadowBrush,
                BorderThickness = new Thickness(1, 1, 0, 0),
                Padding = padding ?? new Thickness(4),
                Child = child
            }
        };
    }

    /// <summary>
    /// Creates a classic beveled Windows 2000 pushbutton.
    /// </summary>
    public static Button CreateButton(string text, Action onClick)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = text,
                FontFamily = ClassicFont,
                FontSize = 12,
                Foreground = TextBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            Background = FaceBrush,
            BorderBrush = DarkShadowBrush,
            BorderThickness = new Thickness(1, 1, 2, 2),
            Padding = new Thickness(10, 4),
            CornerRadius = new CornerRadius(0)
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>
    /// Creates the signature Windows 2000 active title bar gradient header.
    /// </summary>
    public static Control CreateTitleBanner(string title, string subtitle)
    {
        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(ActiveTitleStartColor, 0.0),
                new GradientStop(ActiveTitleEndColor, 1.0)
            ]
        };

        return new Border
        {
            Background = gradient,
            Padding = new Thickness(8, 5),
            Child = new DockPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontFamily = ClassicFont,
                        FontSize = 13,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = subtitle,
                        FontFamily = ClassicFont,
                        FontSize = 11,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        };
    }
}
