namespace Eshu.Models
{
    // Nombres en inglés por convención de código; el texto en español que ve
    // el usuario vive en Converters/StatusToStringConverter, no aquí — así no
    // mantenemos la misma traducción en dos lugares distintos.
    public enum GameStatus
    {
        Unplayed = 0,
        Playing = 1,
        Completed = 2,
        Abandoned = 3,
        PendingValidation = 4
    }
}
