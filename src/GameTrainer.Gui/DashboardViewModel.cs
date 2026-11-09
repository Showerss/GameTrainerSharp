using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GameTrainer.Core;
using GameTrainer.Core.Catalog;
using GameTrainer.Games.GridWorld;

namespace GameTrainer.Gui;

public sealed class GameListItemViewModel(GameEntry entry)
{
    public GameEntry Entry => entry;
    public string GameId => entry.GameId;
    public string DisplayName => entry.DisplayName;
    public string Description => entry.Description;

    public string LatestVersionBadge =>
        entry.LatestVersion is { } latest
            ? $"{latest.Version} [{latest.Modality}]"
            : "[Untrained]";

    public string LastUpdatedDisplay =>
        entry.LastUpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public override string ToString() => $"{DisplayName}  ({LatestVersionBadge})";
}

public sealed class ModelVersionRowViewModel(ModelVersionEntry entry)
{
    public ModelVersionEntry Entry => entry;
    public string Version => entry.Version;
    public string StageDisplay => entry.Modality.ToString();
    public string ModeDisplay => entry.Mode.ToString();
    public string TaughtByDisplay => string.IsNullOrWhiteSpace(entry.ParentVersion) ? "(None - Scratch)" : entry.ParentVersion!;
    public string GoalRateDisplay => $"{entry.FinalGoalRate * 100f:0.0}%";
    public string MeanRewardDisplay => $"{entry.FinalMeanReward:+0.00;-0.00}";
    public string EpisodesDisplay => entry.TotalEpisodes.ToString();
    public string UpdatedAtDisplay => entry.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}

/// <summary>
/// Observable view-model driving the Windows 2000 Observatory Dashboard.
/// Bound to <see cref="ICatalogStore"/> and discovered <see cref="IGameDescriptor"/> implementations.
/// </summary>
public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private readonly ICatalogStore _store;
    private readonly IReadOnlyList<IGameDescriptor> _descriptors;

    private GameListItemViewModel? _selectedGame;
    private ModelVersionRowViewModel? _selectedModelVersion;
    private IGameDescriptor? _selectedDescriptorToAdd;
    private IReadOnlyList<EpisodeMetricPoint> _selectedVersionMetrics = [];
    private IReadOnlyList<EpisodeMetricPoint> _parentVersionMetrics = [];
    private string _selectedVersionTrainingLog = string.Empty;
    private string _statusMessage = "Ready.";

    public DashboardViewModel(
        ICatalogStore? store = null,
        IReadOnlyList<IGameDescriptor>? descriptors = null)
    {
        _store = store ?? new CatalogStore();
        _descriptors = descriptors ?? BuiltInGames.All;

        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CatalogRootPath => _store.RootDirectory;

    public ObservableCollection<GameListItemViewModel> Games { get; } = [];

    public ObservableCollection<IGameDescriptor> UnregisteredDescriptors { get; } = [];

    public ObservableCollection<ModelVersionRowViewModel> ModelVersions { get; } = [];

    public GameListItemViewModel? SelectedGame
    {
        get => _selectedGame;
        set
        {
            if (ReferenceEquals(_selectedGame, value)) return;
            _selectedGame = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedGameTitle));
            OnPropertyChanged(nameof(SelectedGameMetaSummary));
            LoadVersionsForSelectedGame();
        }
    }

    public ModelVersionRowViewModel? SelectedModelVersion
    {
        get => _selectedModelVersion;
        set
        {
            if (ReferenceEquals(_selectedModelVersion, value)) return;
            _selectedModelVersion = value;
            OnPropertyChanged();
            LoadArtifactsForSelectedVersion();
        }
    }

    public IGameDescriptor? SelectedDescriptorToAdd
    {
        get => _selectedDescriptorToAdd;
        set
        {
            if (ReferenceEquals(_selectedDescriptorToAdd, value)) return;
            _selectedDescriptorToAdd = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanAddGame));
        }
    }

    public bool CanAddGame => SelectedDescriptorToAdd is not null;

    public string SelectedGameTitle =>
        SelectedGame is null
            ? "No Game Selected"
            : $"{SelectedGame.DisplayName} ({SelectedGame.GameId})";

    public string SelectedGameMetaSummary =>
        SelectedGame is null
            ? "Select or add a game from the left panel."
            : $"Latest Model: {SelectedGame.LatestVersionBadge}   |   Last Updated: {SelectedGame.LastUpdatedDisplay}   |   Total Versions: {SelectedGame.Entry.Versions.Count}";

    public IReadOnlyList<EpisodeMetricPoint> SelectedVersionMetrics
    {
        get => _selectedVersionMetrics;
        private set
        {
            _selectedVersionMetrics = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<EpisodeMetricPoint> ParentVersionMetrics
    {
        get => _parentVersionMetrics;
        private set
        {
            _parentVersionMetrics = value;
            OnPropertyChanged();
        }
    }

    public string SelectedVersionTrainingLog
    {
        get => _selectedVersionTrainingLog;
        private set
        {
            _selectedVersionTrainingLog = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public GameEntry? AddSelectedGame()
    {
        if (SelectedDescriptorToAdd is null)
        {
            return null;
        }

        var descriptor = SelectedDescriptorToAdd;
        var entry = _store.RegisterGame(descriptor);
        Refresh(selectGameId: entry.GameId);
        StatusMessage = $"Registered game '{descriptor.DisplayName}' ({descriptor.Id}) in catalog.";
        return entry;
    }

    public void Refresh(string? selectGameId = null)
    {
        var previousGameId = selectGameId ?? SelectedGame?.GameId;
        var previousVersion = SelectedModelVersion?.Version;

        var catalogGames = _store.GetGames();
        Games.Clear();
        foreach (var g in catalogGames)
        {
            Games.Add(new GameListItemViewModel(g));
        }

        var registeredIds = catalogGames
            .Select(g => g.GameId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        UnregisteredDescriptors.Clear();
        foreach (var d in _descriptors)
        {
            if (!registeredIds.Contains(d.Id))
            {
                UnregisteredDescriptors.Add(d);
            }
        }

        SelectedDescriptorToAdd = UnregisteredDescriptors.FirstOrDefault();

        SelectedGame = Games.FirstOrDefault(g =>
                           string.Equals(g.GameId, previousGameId, StringComparison.OrdinalIgnoreCase))
                       ?? Games.FirstOrDefault();

        if (previousVersion is not null)
        {
            var matchingVer = ModelVersions.FirstOrDefault(v =>
                string.Equals(v.Version, previousVersion, StringComparison.OrdinalIgnoreCase));
            if (matchingVer is not null)
            {
                SelectedModelVersion = matchingVer;
            }
        }

        StatusMessage = $"Catalog loaded ({Games.Count} game(s), {UnregisteredDescriptors.Count} available to add).";
    }

    private void LoadVersionsForSelectedGame()
    {
        ModelVersions.Clear();
        if (SelectedGame is null)
        {
            SelectedModelVersion = null;
            return;
        }

        foreach (var ver in SelectedGame.Entry.Versions.OrderByDescending(v => v.UpdatedAt))
        {
            ModelVersions.Add(new ModelVersionRowViewModel(ver));
        }

        SelectedModelVersion = ModelVersions.FirstOrDefault();
    }

    private void LoadArtifactsForSelectedVersion()
    {
        if (SelectedGame is null || SelectedModelVersion is null)
        {
            SelectedVersionMetrics = [];
            ParentVersionMetrics = [];
            SelectedVersionTrainingLog = "No model version selected.";
            return;
        }

        var gameId = SelectedGame.GameId;
        var version = SelectedModelVersion.Version;

        SelectedVersionMetrics = _store.ReadMetrics(gameId, version);
        var parentVer = SelectedModelVersion.Entry.ParentVersion;
        ParentVersionMetrics = !string.IsNullOrWhiteSpace(parentVer)
            ? _store.ReadMetrics(gameId, parentVer!)
            : [];

        var log = _store.ReadTrainingLog(gameId, version);
        SelectedVersionTrainingLog = string.IsNullOrWhiteSpace(log)
            ? $"(No log entries found for {gameId} {version})"
            : log;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
