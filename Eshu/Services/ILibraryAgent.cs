using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eshu.Models;

namespace Eshu.Services
{
    // Contrato único para cualquier fuente de juegos: Steam, Epic, GOG, Xbox,
    // Amazon, EA, Ubisoft, Battle.net o carpetas locales. Cada plataforma nueva
    // es una clase más que implementa esto — el resto del sistema no cambia.
    public interface ILibraryAgent
    {
        string PlatformName { get; }

        Task<List<Game>> ScanLibraryAsync();

        void StartWatching(Action<Game> onGameInstalled, Action<string> onGameUninstalled);

        void StopWatching();
    }
}
