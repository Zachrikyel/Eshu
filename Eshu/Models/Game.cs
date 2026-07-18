using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace Eshu.Models
{
    // Implementa INotifyPropertyChanged porque el motor de sincronización va a
    // actualizar juegos que ya están en pantalla (por ejemplo, cuando detecta una
    // instalación en vivo) — sin esto, la vitrina no se refrescaría sola.
    public class Game : INotifyPropertyChanged
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        // De qué tienda viene: "Steam", "Epic Games", "GOG", "Local"...
        public string Platform { get; set; } = string.Empty;

        public string Genre { get; set; } = string.Empty;

        private string? _installPath;
        public string? InstallPath
        {
            get => _installPath;
            set { _installPath = value; OnPropertyChanged(); }
        }

        private bool _isInstalled;
        public bool IsInstalled
        {
            get => _isInstalled;
            set { _isInstalled = value; OnPropertyChanged(); }
        }

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set { _isFavorite = value; OnPropertyChanged(); }
        }

        public int HoursPlayed { get; set; }
        public int EstimatedHoursToBeat { get; set; } // dato tipo HowLongToBeat, para el motor de recomendación

        // 1 (relajado) a 5 (exigencia máxima), para la pregunta de "Fricción" del
        // motor de recomendación. Null mientras no tengamos cómo poblarlo — un
        // juego sin dato simplemente no suma ni resta en esa pregunta.
        public int? Difficulty { get; set; }

        // La fecha en que Eshu vio el juego por primera vez — se llena sola.
        public DateTime? InstalledAt { get; set; }

        // Estas dos necesitan leer datos de cuenta de cada tienda (compras, sesiones
        // de juego), no solo escanear archivos locales — todavía no las llenamos.
        // El orden por estas opciones ya funciona, solo que hoy no diferencia nada.
        public DateTime? AcquiredAt { get; set; }
        public DateTime? LastPlayedAt { get; set; }

        private GameStatus _status = GameStatus.PendingValidation;
        public GameStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string CoverColorHex { get; set; } = "#6C5CE7"; // temporal, hasta tener carátulas reales

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
