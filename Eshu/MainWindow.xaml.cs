using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Eshu.Models;
using Eshu.Services;
using Eshu.ViewModels;

namespace Eshu
{
    public partial class MainWindow : Window
    {
        private readonly LibrarySyncEngine _syncEngine;

        public MainWindow()
        {
            InitializeComponent();

            var viewModel = App.ServiceProvider.GetRequiredService<LibraryViewModel>();
            _syncEngine = App.ServiceProvider.GetRequiredService<LibrarySyncEngine>();
            DataContext = viewModel;

            // Cargar la biblioteca y empezar a vigilar los agentes solo cuando la
            // ventana ya existe — no antes, no bloqueando el arranque de la app.
            Loaded += async (_, _) => await viewModel.InitializeAsync();

            // Si no apagamos los watchers al cerrar, quedan hilos y timers sueltos
            // (el del registro de GOG, los FileSystemWatcher) después de cerrar Eshu.
            Closing += (_, _) => _syncEngine.StopWatchingAll();
        }

        private async void SyncButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LibraryViewModel vm)
            {
                await vm.RunSyncAsync();
            }
        }

        private void FullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowStyle == WindowStyle.None)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ShutdownButton_Click(object sender, RoutedEventArgs e)
        {
            // Confirmación obligatoria: esto apaga el PC completo, no solo Eshu.
            var result = MessageBox.Show(
                "Esto apaga todo el computador, no solo Eshu. ¿Continuar?",
                "Apagar equipo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo("shutdown", "/s /t 0")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LibraryViewModel vm) return;
            var path = vm.SelectedGame?.InstallPath;
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo iniciar el juego: {ex.Message}", "Eshu",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GameTile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Game game
                && DataContext is LibraryViewModel vm)
            {
                vm.SelectedGame = game;
            }
        }

        private void FavoritesChip_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LibraryViewModel vm)
            {
                vm.ToggleFavoritesFilter();
            }
        }

        private void StatusFilterChip_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LibraryViewModel vm && sender is ToggleButton tb && tb.Tag is GameStatus status)
            {
                vm.ToggleStatusFilter(status);
            }
        }

        private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is LibraryViewModel vm && sender is ComboBox cb && cb.SelectedIndex >= 0)
            {
                vm.ApplySort((SortMode)cb.SelectedIndex);
            }
        }
    }
}
