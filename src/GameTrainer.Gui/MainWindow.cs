using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GameTrainer.Core;

namespace GameTrainer.Gui;

/// <summary>
/// Classic Windows 2000 Observatory main window displaying registered games,
/// the "Add Game" registration box, model version lineage, improvement curves, and training logs.
/// </summary>
public sealed class MainWindow : Window
{
    private readonly DashboardViewModel _vm;
    private readonly ListBox _gamesListBox;
    private readonly ComboBox _addGameComboBox;
    private readonly Button _addGameButton;
    private readonly TextBlock _gameTitleText;
    private readonly TextBlock _gameSummaryText;
    private readonly ListBox _versionsListBox;
    private readonly ImprovementChartControl _chartControl;
    private readonly TextBlock _chartSummaryText;
    private readonly TextBox _logTextBox;
    private readonly TextBlock _statusText;

    public MainWindow()
        : this(new DashboardViewModel())
    {
    }

    public MainWindow(DashboardViewModel viewModel)
    {
        _vm = viewModel;

        Title = "GameTrainer 2000 - Generational RL Observatory";
        Width = 980;
        Height = 640;
        MinWidth = 760;
        MinHeight = 500;
        Background = Win2000Theme.FaceBrush;
        FontFamily = Win2000Theme.ClassicFont;
        FontSize = 12;

        _gamesListBox = new ListBox
        {
            Background = Win2000Theme.WindowBrush,
            Foreground = Win2000Theme.TextBrush,
            FontFamily = Win2000Theme.ClassicFont,
            FontSize = 12
        };
        _gamesListBox.SelectionChanged += (_, _) =>
        {
            if (_gamesListBox.SelectedItem is GameListItemViewModel item)
            {
                _vm.SelectedGame = item;
            }
        };

        _addGameComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontFamily = Win2000Theme.ClassicFont,
            FontSize = 12
        };
        _addGameComboBox.SelectionChanged += (_, _) =>
        {
            _vm.SelectedDescriptorToAdd = _addGameComboBox.SelectedItem as IGameDescriptor;
        };

        _addGameButton = Win2000Theme.CreateButton("+ Add Game to Catalog", () =>
        {
            _vm.AddSelectedGame();
            SyncFromViewModel();
        });

        _gameTitleText = new TextBlock
        {
            FontFamily = Win2000Theme.ClassicFont,
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = Win2000Theme.TextBrush
        };

        _gameSummaryText = new TextBlock
        {
            FontFamily = Win2000Theme.ClassicFont,
            FontSize = 12,
            Foreground = Win2000Theme.MutedTextBrush,
            Margin = new Thickness(0, 3, 0, 0)
        };

        _versionsListBox = new ListBox
        {
            Background = Win2000Theme.WindowBrush,
            Foreground = Win2000Theme.TextBrush,
            FontFamily = Win2000Theme.MonospaceFont,
            FontSize = 12
        };
        _versionsListBox.SelectionChanged += (_, _) =>
        {
            if (_versionsListBox.SelectedItem is ModelVersionRowViewModel row)
            {
                _vm.SelectedModelVersion = row;
                SyncSelectedVersionDetails();
            }
        };

        _chartControl = new ImprovementChartControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            MinHeight = 300
        };

        _chartSummaryText = new TextBlock
        {
            FontFamily = Win2000Theme.MonospaceFont,
            FontSize = 12,
            Foreground = Win2000Theme.TextBrush,
            Margin = new Thickness(0, 0, 0, 6)
        };

        _logTextBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = Win2000Theme.MonospaceFont,
            FontSize = 12,
            Background = Win2000Theme.WindowBrush,
            Foreground = Win2000Theme.TextBrush,
            BorderThickness = new Thickness(0)
        };

        _statusText = new TextBlock
        {
            FontFamily = Win2000Theme.ClassicFont,
            FontSize = 11,
            Foreground = Win2000Theme.TextBrush
        };

        Content = BuildRootLayout();
        SyncFromViewModel();
    }

    private Control BuildRootLayout()
    {
        var root = new DockPanel
        {
            Margin = new Thickness(6),
            LastChildFill = true
        };

        // 1. Top Win2000 Banner + Toolbar
        var topStack = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                Win2000Theme.CreateTitleBanner(
                    "GameTrainer 2000 - Generational RL Observatory",
                    "Vector Teacher -> Visual Student Pipeline"),
                BuildToolbar()
            }
        };
        DockPanel.SetDock(topStack, Dock.Top);
        root.Children.Add(topStack);

        // 2. Bottom Win2000 Sunken Status Bar
        var statusBar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(0, 6, 0, 0),
            Children =
            {
                Win2000Theme.CreateSunkenPanel(_statusText, Win2000Theme.FaceBrush, new Thickness(6, 2))
            }
        };
        var pathBlock = new TextBlock
        {
            Text = $"Store: {_vm.CatalogRootPath}",
            FontFamily = Win2000Theme.ClassicFont,
            FontSize = 11,
            Foreground = Win2000Theme.MutedTextBrush
        };
        var pathPanel = Win2000Theme.CreateSunkenPanel(pathBlock, Win2000Theme.FaceBrush, new Thickness(6, 2));
        Grid.SetColumn(pathPanel, 1);
        statusBar.Children.Add(pathPanel);

        DockPanel.SetDock(statusBar, Dock.Bottom);
        root.Children.Add(statusBar);

        // 3. Center Split: Left Game Catalog Sidebar + Right Observatory Workspace
        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("270,8,*"),
            Margin = new Thickness(0, 6, 0, 0)
        };

        var sidebar = BuildSidebar();
        Grid.SetColumn(sidebar, 0);
        mainGrid.Children.Add(sidebar);

        var workspace = BuildWorkspace();
        Grid.SetColumn(workspace, 2);
        mainGrid.Children.Add(workspace);

        root.Children.Add(mainGrid);
        return root;
    }

    private Control BuildToolbar()
    {
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        bar.Children.Add(Win2000Theme.CreateButton("Refresh Catalog & Logs", () =>
        {
            _vm.Refresh();
            SyncFromViewModel();
        }));

        return Win2000Theme.CreateRaisedPanel(bar, new Thickness(4));
    }

    private Control BuildSidebar()
    {
        var dock = new DockPanel
        {
            LastChildFill = true
        };

        var header = new TextBlock
        {
            Text = "Registered Games",
            FontFamily = Win2000Theme.ClassicFont,
            FontWeight = FontWeight.Bold,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 4)
        };
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);

        // Add Game Registration Box at bottom of sidebar
        var addBoxStack = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                new TextBlock
                {
                    Text = "Add Discovered Game:",
                    FontFamily = Win2000Theme.ClassicFont,
                    FontWeight = FontWeight.Bold,
                    FontSize = 11
                },
                _addGameComboBox,
                _addGameButton
            }
        };
        var addBox = Win2000Theme.CreateRaisedPanel(addBoxStack, new Thickness(6));
        DockPanel.SetDock(addBox, Dock.Bottom);
        dock.Children.Add(addBox);

        dock.Children.Add(Win2000Theme.CreateSunkenPanel(_gamesListBox));
        return Win2000Theme.CreateRaisedPanel(dock, new Thickness(6));
    }

    private Control BuildWorkspace()
    {
        var dock = new DockPanel
        {
            LastChildFill = true
        };

        var summaryHeader = Win2000Theme.CreateRaisedPanel(
            new StackPanel
            {
                Children =
                {
                    _gameTitleText,
                    _gameSummaryText
                }
            },
            new Thickness(8, 6));
        summaryHeader.Margin = new Thickness(0, 0, 0, 6);
        DockPanel.SetDock(summaryHeader, Dock.Top);
        dock.Children.Add(summaryHeader);

        var tabs = new TabControl
        {
            Background = Win2000Theme.FaceBrush,
            FontFamily = Win2000Theme.ClassicFont,
            Items =
            {
                new TabItem
                {
                    Header = "Model Versions & Lineage",
                    Content = BuildVersionsTab()
                },
                new TabItem
                {
                    Header = "Improvement Graph",
                    Content = BuildGraphTab()
                },
                new TabItem
                {
                    Header = "Training Logs",
                    Content = BuildLogsTab()
                }
            }
        };

        dock.Children.Add(Win2000Theme.CreateRaisedPanel(tabs, new Thickness(6)));
        return dock;
    }

    private Control BuildVersionsTab()
    {
        var dock = new DockPanel
        {
            Margin = new Thickness(4),
            LastChildFill = true
        };

        var columnsHeader = new TextBlock
        {
            Text = "VERSION   | STAGE    | MODE              | TAUGHT BY        | GOAL %  | MEAN RWD | LAST UPDATED",
            FontFamily = Win2000Theme.MonospaceFont,
            FontWeight = FontWeight.Bold,
            FontSize = 12,
            Margin = new Thickness(4, 2, 4, 4)
        };
        var headerBox = Win2000Theme.CreateRaisedPanel(columnsHeader, new Thickness(4, 2));
        DockPanel.SetDock(headerBox, Dock.Top);
        dock.Children.Add(headerBox);

        dock.Children.Add(Win2000Theme.CreateSunkenPanel(_versionsListBox));
        return dock;
    }

    private Control BuildGraphTab()
    {
        var dock = new DockPanel
        {
            Margin = new Thickness(4),
            LastChildFill = true
        };

        DockPanel.SetDock(_chartSummaryText, Dock.Top);
        dock.Children.Add(_chartSummaryText);

        dock.Children.Add(Win2000Theme.CreateSunkenPanel(_chartControl, Brushes.Black, new Thickness(2)));
        return dock;
    }

    private Control BuildLogsTab()
    {
        var dock = new DockPanel
        {
            Margin = new Thickness(4),
            LastChildFill = true
        };

        var topBar = new DockPanel
        {
            Margin = new Thickness(0, 0, 0, 4),
            Children =
            {
                Win2000Theme.CreateButton("Refresh Log", () =>
                {
                    _vm.Refresh();
                    SyncFromViewModel();
                })
            }
        };
        DockPanel.SetDock(topBar, Dock.Top);
        dock.Children.Add(topBar);

        dock.Children.Add(Win2000Theme.CreateSunkenPanel(_logTextBox));
        return dock;
    }

    private void SyncFromViewModel()
    {
        _gamesListBox.ItemsSource = _vm.Games.ToList();
        _gamesListBox.SelectedItem = _vm.SelectedGame;

        _addGameComboBox.ItemsSource = _vm.UnregisteredDescriptors.ToList();
        _addGameComboBox.SelectedItem = _vm.SelectedDescriptorToAdd;
        _addGameButton.IsEnabled = _vm.CanAddGame;

        _gameTitleText.Text = _vm.SelectedGameTitle;
        _gameSummaryText.Text = _vm.SelectedGameMetaSummary;

        _versionsListBox.ItemsSource = _vm.ModelVersions.ToList();
        _versionsListBox.SelectedItem = _vm.SelectedModelVersion;
        _versionsListBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ModelVersionRowViewModel>(
            (row, _) => new TextBlock
            {
                FontFamily = Win2000Theme.MonospaceFont,
                FontSize = 12,
                Text = row is null
                    ? string.Empty
                    : $"{row.Version,-9} | {row.StageDisplay,-8} | {row.ModeDisplay,-17} | {row.TaughtByDisplay,-16} | {row.GoalRateDisplay,7} | {row.MeanRewardDisplay,8} | {row.UpdatedAtDisplay}"
            });

        _addGameComboBox.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<IGameDescriptor>(
            (desc, _) => new TextBlock
            {
                FontFamily = Win2000Theme.ClassicFont,
                FontSize = 12,
                Text = desc is null ? "(All discovered games registered)" : $"{desc.DisplayName} [{desc.Id}]"
            });

        _statusText.Text = _vm.StatusMessage;
        SyncSelectedVersionDetails();
    }

    private void SyncSelectedVersionDetails()
    {
        if (_vm.SelectedModelVersion is { } ver)
        {
            _chartSummaryText.Text =
                $"Version: {ver.Version} ({ver.StageDisplay}, {ver.ModeDisplay})  |  Taught By: {ver.TaughtByDisplay}  |  Final Goal Rate: {ver.GoalRateDisplay}  |  Final Mean Reward: {ver.MeanRewardDisplay}";

            _chartControl.SetSeries(
                ver.Version,
                _vm.SelectedVersionMetrics,
                ver.Entry.ParentVersion,
                _vm.ParentVersionMetrics);
        }
        else
        {
            _chartSummaryText.Text = "Select a trained model version to view its improvement curve.";
            _chartControl.SetSeries("None", [], null, []);
        }

        _logTextBox.Text = _vm.SelectedVersionTrainingLog;
    }
}
