using System.ComponentModel.DataAnnotations;

namespace Eshu.Models
{
    public class Game
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        // De qué tienda viene: "Steam", "Epic", "GOG", "Xbox", "Local"...
        // Junto con Title arma la "firma" que identifica un juego sin duplicarlo.
        public string Platform { get; set; } = string.Empty;

        public string Genre { get; set; } = string.Empty;
        public string? InstallPath { get; set; }
        public bool IsInstalled { get; set; }
        public bool IsFavorite { get; set; }

        public int HoursPlayed { get; set; }
        public int EstimatedHoursToBeat { get; set; } // dato tipo HowLongToBeat, para el motor de recomendación

        public GameStatus Status { get; set; } = GameStatus.PendingValidation;

        public string CoverColorHex { get; set; } = "#6C5CE7"; // temporal, hasta tener carátulas reales
    }
}
