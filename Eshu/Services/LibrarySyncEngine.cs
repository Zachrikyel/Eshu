using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Eshu.Models;

namespace Eshu.Services
{
    // El único lugar que decide qué se guarda en la base de datos. Los agentes
    // solo saben escanear su plataforma; este motor los junta a todos y nunca
    // pisa lo que el usuario ya marcó (estado, favorito, horas jugadas).
    public class LibrarySyncEngine
    {
        private readonly IEnumerable<ILibraryAgent> _agents;
        private readonly IDbContextFactory<EshuDbContext> _dbContextFactory;
        private readonly IgdbMetadataService _metadataService;

        public event Action<Game>? GameInstalled;
        public event Action<string, string>? GameUninstalled; // (Platform, Title)
        public event Action<Game>? GameMetadataUpdated;

        public LibrarySyncEngine(IEnumerable<ILibraryAgent> agents, IDbContextFactory<EshuDbContext> dbContextFactory, IgdbMetadataService metadataService)
        {
            _agents = agents;
            _dbContextFactory = dbContextFactory;
            _metadataService = metadataService;
        }

        // Escaneo completo — se llama al abrir la app y cuando el usuario pulsa "Sincronizar".
        public async Task RunFullSyncAsync()
        {
            var scanTasks = _agents.Select(ScanAgentSafely);
            var results = await Task.WhenAll(scanTasks);

            using var db = await _dbContextFactory.CreateDbContextAsync();

            foreach (var scannedGames in results)
            {
                foreach (var scanned in scannedGames)
                {
                    await UpsertGame(db, scanned);
                }
            }

            await db.SaveChangesAsync();
        }

        // Se llama por separado del sync normal — 600 juegos a este ritmo tardan
        // minutos, y no tiene sentido bloquear el botón "Sincronizar" por eso.
        // Solo consulta los que todavía no tienen género (no repite trabajo).
        public async Task EnrichMissingGenresAsync()
        {
            if (!_metadataService.IsConfigured) return;

            List<Game> pending;
            using (var db = await _dbContextFactory.CreateDbContextAsync())
            {
                pending = await db.Games.Where(g => g.Genre == "").ToListAsync();
            }

            foreach (var game in pending)
            {
                var genre = await _metadataService.FetchGenreAsync(game.Title);
                if (!string.IsNullOrWhiteSpace(genre))
                {
                    using var db = await _dbContextFactory.CreateDbContextAsync();
                    var tracked = await db.Games.FirstOrDefaultAsync(g => g.Id == game.Id);
                    if (tracked != null)
                    {
                        tracked.Genre = genre;
                        await db.SaveChangesAsync();
                        GameMetadataUpdated?.Invoke(tracked);
                    }
                }

                // El límite real de IGDB es 4/seg — con esto nos quedamos cómodos por debajo.
                await Task.Delay(300);
            }
        }

        // Un agente que falla (disco desconectado, plataforma no instalada) no debe
        // tumbar el sync de los demás.
        private async Task<List<Game>> ScanAgentSafely(ILibraryAgent agent)
        {
            try
            {
                return await agent.ScanLibraryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LibrarySyncEngine] {agent.PlatformName} falló: {ex.Message}");
                return new List<Game>();
            }
        }

        // Inserta lo nuevo; de lo que ya existía solo actualiza los campos "objetivos"
        // (ruta, si está instalado) — nunca Status, IsFavorite ni HoursPlayed.
        private async Task UpsertGame(EshuDbContext db, Game scanned)
        {
            var existing = await db.Games.FirstOrDefaultAsync(g =>
                g.Platform == scanned.Platform && g.Title == scanned.Title);

            if (existing == null)
            {
                scanned.InstalledAt = DateTime.Now;
                db.Games.Add(scanned);
                GameInstalled?.Invoke(scanned);
            }
            else
            {
                existing.InstallPath = scanned.InstallPath;
                existing.IsInstalled = scanned.IsInstalled;
            }
        }

        // Activa el "watch" de cada agente para detectar instalaciones/desinstalaciones
        // mientras la app está abierta, sin esperar al siguiente sync manual.
        public void StartWatchingAll()
        {
            foreach (var agent in _agents)
            {
                var platformName = agent.PlatformName; // capturado para los callbacks de abajo

                agent.StartWatching(
                    onGameInstalled: game => { _ = HandleLiveInstall(game); },
                    onGameUninstalled: title => { _ = HandleLiveUninstall(platformName, title); }
                );
            }
        }

        public void StopWatchingAll()
        {
            foreach (var agent in _agents)
            {
                agent.StopWatching();
            }
        }

        private async Task HandleLiveInstall(Game game)
        {
            try
            {
                using var db = await _dbContextFactory.CreateDbContextAsync();
                await UpsertGame(db, game);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LibrarySyncEngine] Error procesando instalación: {ex.Message}");
            }
        }

        private async Task HandleLiveUninstall(string platform, string title)
        {
            try
            {
                using var db = await _dbContextFactory.CreateDbContextAsync();
                var existing = await db.Games.FirstOrDefaultAsync(g => g.Platform == platform && g.Title == title);
                if (existing != null)
                {
                    existing.IsInstalled = false; // no se borra: conserva estado, horas y favorito
                    await db.SaveChangesAsync();
                    GameUninstalled?.Invoke(existing.Platform, existing.Title);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LibrarySyncEngine] Error procesando desinstalación: {ex.Message}");
            }
        }
    }
}
