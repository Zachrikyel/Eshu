using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eshu.Models;

namespace Eshu.Services
{
    // A diferencia de Steam/Epic/GOG, "Local" no tiene una tienda que le diga qué
    // hay instalado — por eso recibe la lista de carpetas a vigilar por fuera,
    // en vez de tenerla fija en el código. Así da igual si son 2 carpetas o 5 discos.
    public class LocalAgent : ILibraryAgent
    {
        public string PlatformName => "Local";

        private readonly List<string> _watchedFolders;
        private readonly List<FileSystemWatcher> _watchers = new();
        private Action<Game>? _onGameInstalled;
        private Action<string>? _onGameUninstalled;

        public LocalAgent(IEnumerable<string> watchedFolders)
        {
            _watchedFolders = watchedFolders.ToList();
        }

        public async Task<List<Game>> ScanLibraryAsync()
        {
            return await Task.Run(() =>
            {
                var discoveredGames = new List<Game>();

                foreach (var baseDir in _watchedFolders)
                {
                    if (!Directory.Exists(baseDir)) continue;

                    try
                    {
                        // Cada subcarpeta directa se trata como un juego independiente.
                        var gameFolders = Directory.GetDirectories(baseDir);

                        foreach (var folder in gameFolders)
                        {
                            var game = AnalyzeGameFolder(folder);
                            if (game != null)
                            {
                                discoveredGames.Add(game);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Un disco desconectado o sin permisos no debe tumbar el escaneo completo.
                        System.Diagnostics.Debug.WriteLine($"[LocalAgent] Error escaneando {baseDir}: {ex.Message}");
                    }
                }

                return discoveredGames;
            });
        }

        public void StartWatching(Action<Game> onGameInstalled, Action<string> onGameUninstalled)
        {
            _onGameInstalled = onGameInstalled;
            _onGameUninstalled = onGameUninstalled;

            foreach (var baseDir in _watchedFolders)
            {
                if (!Directory.Exists(baseDir)) continue;

                var watcher = new FileSystemWatcher(baseDir)
                {
                    NotifyFilter = NotifyFilters.DirectoryName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                watcher.Created += OnDirectoryCreated;
                watcher.Deleted += OnDirectoryDeleted;

                _watchers.Add(watcher);
            }
        }

        public void StopWatching()
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
        }

        private Game? AnalyzeGameFolder(string folderPath)
        {
            try
            {
                var folderInfo = new DirectoryInfo(folderPath);

                // Buscamos todos los ejecutables dentro de la carpeta del juego.
                var exeFiles = folderInfo.GetFiles("*.exe", SearchOption.AllDirectories);
                if (!exeFiles.Any()) return null;

                // El ejecutable real suele ser el más pesado (evita instaladores/desinstaladores).
                var mainExe = exeFiles.OrderByDescending(f => f.Length).First();

                return new Game
                {
                    Title = folderInfo.Name, // El nombre de la carpeta define el título
                    Platform = "Local",
                    InstallPath = mainExe.FullName,
                    IsInstalled = true,
                    Status = GameStatus.PendingValidation
                };
            }
            catch
            {
                return null;
            }
        }

        private void OnDirectoryCreated(object sender, FileSystemEventArgs e)
        {
            // Esperamos un instante a que el sistema termine de copiar la carpeta.
            Task.Delay(1000).ContinueWith(_ =>
            {
                var game = AnalyzeGameFolder(e.FullPath);
                if (game != null && _onGameInstalled != null)
                {
                    _onGameInstalled(game);
                }
            });
        }

        private void OnDirectoryDeleted(object sender, FileSystemEventArgs e)
        {
            _onGameUninstalled?.Invoke(Path.GetFileName(e.FullPath));
        }
    }
}
