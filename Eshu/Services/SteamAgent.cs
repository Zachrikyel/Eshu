using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using Eshu.Models;

namespace Eshu.Services
{
    public class SteamAgent : ILibraryAgent
    {
        public string PlatformName => "Steam";

        private readonly List<FileSystemWatcher> _watchers = new();
        private Action<Game>? _onGameInstalled;
        private Action<string>? _onGameUninstalled;

        public async Task<List<Game>> ScanLibraryAsync()
        {
            return await Task.Run(() =>
            {
                var discoveredGames = new List<Game>();
                var libraryPaths = GetSteamLibraryPaths();

                foreach (var path in libraryPaths)
                {
                    var steamappsFolder = Path.Combine(path, "steamapps");
                    if (!Directory.Exists(steamappsFolder)) continue;

                    // Los archivos .acf son la fuente de verdad de Steam sobre lo que está instalado.
                    var acfFiles = Directory.GetFiles(steamappsFolder, "appmanifest_*.acf");

                    foreach (var acf in acfFiles)
                    {
                        var game = ParseSteamManifest(acf, steamappsFolder);
                        if (game != null)
                        {
                            discoveredGames.Add(game);
                        }
                    }
                }

                return discoveredGames;
            });
        }

        public void StartWatching(Action<Game> onGameInstalled, Action<string> onGameUninstalled)
        {
            _onGameInstalled = onGameInstalled;
            _onGameUninstalled = onGameUninstalled;

            var libraryPaths = GetSteamLibraryPaths();

            foreach (var path in libraryPaths)
            {
                var steamappsFolder = Path.Combine(path, "steamapps");
                if (!Directory.Exists(steamappsFolder)) continue;

                var watcher = new FileSystemWatcher(steamappsFolder)
                {
                    Filter = "appmanifest_*.acf", // Solo vigilamos los manifiestos, no cada archivo del juego
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };

                watcher.Created += OnManifestChanged;
                watcher.Changed += OnManifestChanged;
                watcher.Deleted += OnManifestDeleted;

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

        private List<string> GetSteamLibraryPaths()
        {
            var paths = new List<string>();
            try
            {
                // 1. Encontrar la raíz de Steam desde el Registro de Windows
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key?.GetValue("SteamPath") is string steamPath)
                {
                    steamPath = steamPath.Replace("/", "\\");
                    paths.Add(steamPath); // Carpeta principal

                    // 2. Buscar unidades adicionales en libraryfolders.vdf (para bibliotecas repartidas en varios discos)
                    var vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdfPath))
                    {
                        var vdfContent = File.ReadAllText(vdfPath);
                        var matches = Regex.Matches(vdfContent, "\"path\"\\s+\"([^\"]+)\"");
                        foreach (Match match in matches)
                        {
                            var extraPath = match.Groups[1].Value.Replace("\\\\", "\\");
                            if (!paths.Contains(extraPath))
                            {
                                paths.Add(extraPath);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SteamAgent] No se pudo leer el registro: {ex.Message}");
            }
            return paths;
        }

        private Game? ParseSteamManifest(string acfFilePath, string steamappsFolder)
        {
            try
            {
                var content = ReadFileWithRetry(acfFilePath);
                if (string.IsNullOrWhiteSpace(content)) return null;

                var nameMatch = Regex.Match(content, "\"name\"\\s+\"([^\"]+)\"");
                var dirMatch = Regex.Match(content, "\"installdir\"\\s+\"([^\"]+)\"");
                var stateMatch = Regex.Match(content, "\"StateFlags\"\\s+\"([^\"]+)\""); // "4" = totalmente instalado

                if (nameMatch.Success && dirMatch.Success && stateMatch.Success)
                {
                    if (stateMatch.Groups[1].Value != "4") return null; // Ignorar juegos a medio descargar

                    var installPath = Path.Combine(steamappsFolder, "common", dirMatch.Groups[1].Value);

                    return new Game
                    {
                        Title = nameMatch.Groups[1].Value,
                        Platform = "Steam",
                        InstallPath = installPath,
                        IsInstalled = true,
                        Status = GameStatus.PendingValidation
                    };
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private string ReadFileWithRetry(string path)
        {
            // Steam bloquea el .acf brevemente mientras escribe — reintentamos sin colapsar.
            for (int i = 0; i < 3; i++)
            {
                try { return File.ReadAllText(path); }
                catch (IOException) { System.Threading.Thread.Sleep(100); }
            }
            return string.Empty;
        }

        private void OnManifestChanged(object sender, FileSystemEventArgs e)
        {
            Task.Run(() =>
            {
                var steamappsFolder = Path.GetDirectoryName(e.FullPath);
                if (steamappsFolder == null) return;

                var game = ParseSteamManifest(e.FullPath, steamappsFolder);
                if (game != null && _onGameInstalled != null)
                {
                    _onGameInstalled(game);
                }
            });
        }

        private void OnManifestDeleted(object sender, FileSystemEventArgs e)
        {
            _onGameUninstalled?.Invoke(e.Name ?? string.Empty);
        }
    }
}
