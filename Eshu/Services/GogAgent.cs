using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Eshu.Models;

namespace Eshu.Services
{
    public class GogAgent : ILibraryAgent
    {
        public string PlatformName => "GOG";

        // GOG (a diferencia de Steam/Epic) no deja archivos de manifiesto — todo
        // vive en el registro, bajo una subclave por juego. Confirmado con
        // ejemplos reales: cada subclave trae gameName, path y exe.
        private const string GogGamesKey = @"SOFTWARE\GOG.com\Games";

        private Timer? _pollTimer;
        private HashSet<string> _lastKnownTitles = new();
        private Action<Game>? _onGameInstalled;
        private Action<string>? _onGameUninstalled;

        public Task<List<Game>> ScanLibraryAsync()
        {
            return Task.FromResult(ScanRegistry());
        }

        public void StartWatching(Action<Game> onGameInstalled, Action<string> onGameUninstalled)
        {
            _onGameInstalled = onGameInstalled;
            _onGameUninstalled = onGameUninstalled;
            _lastKnownTitles = ScanRegistry().Select(g => g.Title).ToHashSet();

            // El registro no tiene un FileSystemWatcher equivalente sencillo en .NET,
            // así que en vez de eventos instantáneos revisamos cada minuto. Para GOG
            // (instalaciones que no cambian cada segundo) es un compromiso razonable.
            _pollTimer = new Timer(_ => CheckForChanges(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public void StopWatching()
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
        }

        private void CheckForChanges()
        {
            var current = ScanRegistry();
            var currentTitles = current.Select(g => g.Title).ToHashSet();

            foreach (var game in current)
            {
                if (!_lastKnownTitles.Contains(game.Title))
                {
                    _onGameInstalled?.Invoke(game);
                }
            }

            foreach (var oldTitle in _lastKnownTitles)
            {
                if (!currentTitles.Contains(oldTitle))
                {
                    _onGameUninstalled?.Invoke(oldTitle);
                }
            }

            _lastKnownTitles = currentTitles;
        }

        private List<Game> ScanRegistry()
        {
            var discoveredGames = new List<Game>();
            try
            {
                // Usamos la vista de 64 bits directamente en vez de escribir WOW6432Node
                // a mano — así Windows resuelve la ruta correcta por nosotros.
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var gamesKey = baseKey.OpenSubKey(GogGamesKey);
                if (gamesKey == null) return discoveredGames;

                foreach (var gameIdKey in gamesKey.GetSubKeyNames())
                {
                    using var subKey = gamesKey.OpenSubKey(gameIdKey);
                    if (subKey == null) continue;

                    var title = subKey.GetValue("gameName") as string;
                    var path = subKey.GetValue("path") as string;
                    var exe = subKey.GetValue("exe") as string;

                    if (string.IsNullOrWhiteSpace(title)) continue;

                    discoveredGames.Add(new Game
                    {
                        Title = title,
                        Platform = "GOG",
                        InstallPath = !string.IsNullOrWhiteSpace(exe) ? exe : path,
                        IsInstalled = true,
                        Status = GameStatus.PendingValidation
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GogAgent] No se pudo leer el registro: {ex.Message}");
            }
            return discoveredGames;
        }
    }
}
