using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Eshu.Models;

namespace Eshu.Services
{
    public class EpicAgent : ILibraryAgent
    {
        public string PlatformName => "Epic Games";

        // Confirmado: Epic guarda un archivo .item (JSON) por cada juego instalado en esta carpeta.
        private static readonly string ManifestsFolder =
            @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";

        private FileSystemWatcher? _watcher;
        private Action<Game>? _onGameInstalled;
        private Action<string>? _onGameUninstalled;

        public async Task<List<Game>> ScanLibraryAsync()
        {
            return await Task.Run(() =>
            {
                var discoveredGames = new List<Game>();
                if (!Directory.Exists(ManifestsFolder)) return discoveredGames;

                foreach (var itemFile in Directory.GetFiles(ManifestsFolder, "*.item"))
                {
                    var game = ParseManifest(itemFile);
                    if (game != null)
                    {
                        discoveredGames.Add(game);
                    }
                }
                return discoveredGames;
            });
        }

        public void StartWatching(Action<Game> onGameInstalled, Action<string> onGameUninstalled)
        {
            _onGameInstalled = onGameInstalled;
            _onGameUninstalled = onGameUninstalled;

            if (!Directory.Exists(ManifestsFolder)) return;

            _watcher = new FileSystemWatcher(ManifestsFolder)
            {
                Filter = "*.item",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnManifestChanged;
            _watcher.Deleted += OnManifestDeleted;
        }

        public void StopWatching()
        {
            if (_watcher == null) return;
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        private Game? ParseManifest(string itemFilePath)
        {
            try
            {
                var json = File.ReadAllText(itemFilePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var manifest = JsonSerializer.Deserialize<EpicManifest>(json, options);

                if (manifest == null || string.IsNullOrWhiteSpace(manifest.DisplayName)) return null;

                return new Game
                {
                    Title = manifest.DisplayName,
                    Platform = "Epic Games",
                    InstallPath = manifest.InstallLocation,
                    IsInstalled = true,
                    Status = GameStatus.PendingValidation
                };
            }
            catch
            {
                // Un .item corrupto o de un formato futuro no debe tumbar el resto del escaneo.
                return null;
            }
        }

        private void OnManifestChanged(object sender, FileSystemEventArgs e)
        {
            Task.Run(() =>
            {
                var game = ParseManifest(e.FullPath);
                if (game != null && _onGameInstalled != null)
                {
                    _onGameInstalled(game);
                }
            });
        }

        private void OnManifestDeleted(object sender, FileSystemEventArgs e)
        {
            _onGameUninstalled?.Invoke(Path.GetFileNameWithoutExtension(e.Name ?? string.Empty));
        }

        // Solo mapeamos los campos que realmente usamos — el .item real trae muchos más.
        private class EpicManifest
        {
            [JsonPropertyName("DisplayName")]
            public string DisplayName { get; set; } = string.Empty;

            [JsonPropertyName("InstallLocation")]
            public string InstallLocation { get; set; } = string.Empty;

            [JsonPropertyName("AppName")]
            public string AppName { get; set; } = string.Empty;
        }
    }
}
