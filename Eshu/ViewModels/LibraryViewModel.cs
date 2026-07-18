using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Eshu.Models;
using Eshu.Services;

namespace Eshu.ViewModels
{
    public class LibraryViewModel : INotifyPropertyChanged
    {
        private readonly LibrarySyncEngine _syncEngine;
        private readonly IDbContextFactory<EshuDbContext> _dbContextFactory;
        private readonly Dispatcher _dispatcher;

        public ObservableCollection<Game> Games { get; } = new();
        public ICollectionView GamesView { get; }

        private readonly HashSet<GameStatus> _activeStatusFilters = new();
        private bool _favoritesOnly;

        private Game? _selectedGame;
        public Game? SelectedGame
        {
            get => _selectedGame;
            set { _selectedGame = value; OnPropertyChanged(); }
        }

        private bool _isSyncing;
        public bool IsSyncing
        {
            get => _isSyncing;
            set { _isSyncing = value; OnPropertyChanged(); }
        }

        public LibraryViewModel(LibrarySyncEngine syncEngine, IDbContextFactory<EshuDbContext> dbContextFactory)
        {
            _syncEngine = syncEngine;
            _dbContextFactory = dbContextFactory;
            _dispatcher = Application.Current.Dispatcher;

            GamesView = CollectionViewSource.GetDefaultView(Games);
            GamesView.Filter = FilterGames;
            ApplySort(SortMode.Alphabetical);

            // Los agentes avisan desde hilos de fondo (FileSystemWatcher, el timer de GOG);
            // todo lo que toque Games tiene que volver al hilo de la interfaz primero.
            _syncEngine.GameInstalled += game => _dispatcher.Invoke(() => UpsertInCollection(game));
            _syncEngine.GameUninstalled += (platform, title) => _dispatcher.Invoke(() => MarkUninstalledInCollection(platform, title));
        }

        // Se llama una vez al abrir la ventana: carga lo que ya está guardado y
        // dispara un sync completo por detrás, sin bloquear la pantalla.
        public async Task InitializeAsync()
        {
            await LoadFromDatabaseAsync();
            _syncEngine.StartWatchingAll();
            _ = RunSyncAsync();
        }

        public async Task RunSyncAsync()
        {
            IsSyncing = true;
            try
            {
                await _syncEngine.RunFullSyncAsync();
                await LoadFromDatabaseAsync();
            }
            finally
            {
                IsSyncing = false;
            }
        }

        private async Task LoadFromDatabaseAsync()
        {
            using var db = await _dbContextFactory.CreateDbContextAsync();
            var games = await db.Games.AsNoTracking().ToListAsync();

            Games.Clear();
            foreach (var game in games)
            {
                Games.Add(game);
            }
            SelectedGame ??= Games.FirstOrDefault();
        }

        private void UpsertInCollection(Game game)
        {
            var existing = Games.FirstOrDefault(g => g.Platform == game.Platform && g.Title == game.Title);
            if (existing == null)
            {
                Games.Add(game);
            }
            else
            {
                existing.IsInstalled = game.IsInstalled;
                existing.InstallPath = game.InstallPath;
            }
        }

        private void MarkUninstalledInCollection(string platform, string title)
        {
            var existing = Games.FirstOrDefault(g => g.Platform == platform && g.Title == title);
            if (existing != null)
            {
                existing.IsInstalled = false;
            }
        }

        // Los 6 chips llaman esto — Favoritos es su propio interruptor porque es
        // una dimensión distinta (no se combina "o" con los estados, se combina "y").
        public void ToggleFavoritesFilter()
        {
            _favoritesOnly = !_favoritesOnly;
            GamesView.Refresh();
        }

        // Los 5 chips de estado se combinan entre sí con "o": si marcas "No jugado"
        // y "Pendiente" a la vez, ves los que son cualquiera de los dos.
        public void ToggleStatusFilter(GameStatus status)
        {
            if (!_activeStatusFilters.Remove(status))
            {
                _activeStatusFilters.Add(status);
            }
            GamesView.Refresh();
        }

        public void ApplySort(SortMode mode)
        {
            string propertyName = mode switch
            {
                SortMode.Alphabetical => nameof(Game.Title),
                SortMode.InstallDate => nameof(Game.InstalledAt),
                SortMode.AcquisitionDate => nameof(Game.AcquiredAt),
                SortMode.HoursPlayed => nameof(Game.HoursPlayed),
                SortMode.LastPlayed => nameof(Game.LastPlayedAt),
                _ => nameof(Game.Title)
            };
            var direction = mode == SortMode.Alphabetical ? ListSortDirection.Ascending : ListSortDirection.Descending;

            GamesView.SortDescriptions.Clear();
            GamesView.SortDescriptions.Add(new SortDescription(propertyName, direction));
        }

        // Sin chips marcados, se ve todo — marcar filtros nunca deja la vitrina
        // vacía por accidente, solo la acota.
        private bool FilterGames(object obj)
        {
            if (obj is not Game game) return false;
            if (_favoritesOnly && !game.IsFavorite) return false;
            if (_activeStatusFilters.Count > 0 && !_activeStatusFilters.Contains(game.Status)) return false;
            return true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
