using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Eshu.Models;

namespace Eshu.Converters
{
    // Convierte "#6C5CE7" en un Brush usable en Background/Fill.
    public class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    return new SolidColorBrush(color);
                }
                catch { }
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // GameStatus.PendingValidation -> "Pendiente de verificar"
    public class StatusToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not GameStatus status) return string.Empty;
            return status switch
            {
                GameStatus.Unplayed => "No jugado",
                GameStatus.Playing => "Jugando",
                GameStatus.Completed => "Terminado",
                GameStatus.Abandoned => "Abandonado",
                GameStatus.PendingValidation => "Pendiente de verificar",
                _ => status.ToString()
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Cada estado tiene su color — los mismos de Themes/Colors.xaml, nada de
    // colores nuevos sueltos (Brushes.Cyan de sistema no es nuestro #2DE3C8).
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not GameStatus status) return Brushes.Gray;
            string resourceKey = status switch
            {
                GameStatus.Playing => "CyanBrush",
                GameStatus.Completed => "GreenBrush",
                GameStatus.PendingValidation => "AmberBrush",
                GameStatus.Abandoned => "WineBrush",
                _ => "TextSecondaryBrush"
            };
            return Application.Current.Resources[resourceKey] as Brush ?? Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
