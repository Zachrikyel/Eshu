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

        private RecommendationEngine.Transition _recoTransition;
        private RecommendationEngine.Pillar _recoPillar;
        private RecommendationEngine.Commitment _recoCommitment;
        private string? _recoLastGenre;

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

        private async void SetStatus_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LibraryViewModel vm || vm.SelectedGame == null) return;
            if (sender is not FrameworkElement el || el.Tag is not GameStatus status) return;

            bool justCompleted = status == GameStatus.Completed && vm.SelectedGame.Status != GameStatus.Completed;
            await vm.SetSelectedGameStatusAsync(status);

            if (justCompleted)
            {
                StartRecommendationFlow(vm.SelectedGame.Genre);
            }
        }

        private void StartRecommendationFlow(string? lastCompletedGenre)
        {
            _recoLastGenre = lastCompletedGenre;
            RecommendationOverlay.Visibility = Visibility.Visible;
            ShowRecoQuestion1();
        }

        private void ShowRecoQuestion1()
        {
            RecoStepLabel.Text = "Pregunta 1 de 4";
            RecoQuestionText.Text = "¿Quieres mantener la misma energía del juego anterior o buscas un cambio?";
            RecoOptionsPanel.Children.Clear();
            AddRecoOption("Continuidad — dame algo similar", () => { _recoTransition = RecommendationEngine.Transition.Continuity; ShowRecoQuestion2(); });
            AddRecoOption("Cambio radical — algo totalmente distinto", () => { _recoTransition = RecommendationEngine.Transition.RadicalChange; ShowRecoQuestion2(); });
        }

        private void ShowRecoQuestion2()
        {
            RecoStepLabel.Text = "Pregunta 2 de 4";
            RecoQuestionText.Text = "¿Qué quieres que domine tu próxima partida?";
            RecoOptionsPanel.Children.Clear();
            AddRecoOption("Historia inmersiva", () => { _recoPillar = RecommendationEngine.Pillar.Story; ShowRecoQuestion3(); });
            AddRecoOption("Acción pura y reflejos", () => { _recoPillar = RecommendationEngine.Pillar.Action; ShowRecoQuestion3(); });
            AddRecoOption("Estrategia y optimización", () => { _recoPillar = RecommendationEngine.Pillar.Strategy; ShowRecoQuestion3(); });
            AddRecoOption("Desconexión casual", () => { _recoPillar = RecommendationEngine.Pillar.Casual; ShowRecoQuestion3(); });
        }

        private void ShowRecoQuestion3()
        {
            RecoStepLabel.Text = "Pregunta 3 de 4";
            RecoQuestionText.Text = "¿Cuánto tiempo quieres invertir antes de sentir que \"terminaste\"?";
            RecoOptionsPanel.Children.Clear();
            AddRecoOption("Corto y directo (1-12 h)", () => { _recoCommitment = RecommendationEngine.Commitment.Short; ShowRecoQuestion4(); });
            AddRecoOption("Aventura estándar (15-30 h)", () => { _recoCommitment = RecommendationEngine.Commitment.Standard; ShowRecoQuestion4(); });
            AddRecoOption("Pozo de horas (+40 h)", () => { _recoCommitment = RecommendationEngine.Commitment.LongHaul; ShowRecoQuestion4(); });
        }

        private void ShowRecoQuestion4()
        {
            RecoStepLabel.Text = "Pregunta 4 de 4";
            RecoQuestionText.Text = "¿Qué tanta exigencia quieres tolerar hoy?";
            RecoOptionsPanel.Children.Clear();
            AddRecoOption("Modo chill", () => ShowRecoResults(RecommendationEngine.Friction.Chill));
            AddRecoOption("Reto equilibrado", () => ShowRecoResults(RecommendationEngine.Friction.Balanced));
            AddRecoOption("Exigencia máxima", () => ShowRecoResults(RecommendationEngine.Friction.Maximum));
        }

        private void ShowRecoResults(RecommendationEngine.Friction friction)
        {
            if (DataContext is not LibraryViewModel vm) return;

            var engine = App.ServiceProvider.GetRequiredService<RecommendationEngine>();
            var results = engine.Recommend(vm.Games, _recoLastGenre, _recoTransition, _recoPillar, _recoCommitment, friction);

            RecoStepLabel.Text = "Recomendado para ti";
            RecoQuestionText.Text = results.Count > 0
                ? "Estos son los que mejor calzan:"
                : "Nada en tu biblioteca todavía — sincroniza o prueba otras respuestas.";
            RecoOptionsPanel.Children.Clear();

            foreach (var game in results)
            {
                AddRecoOption(game.Title, () =>
                {
                    vm.SelectedGame = game;
                    CloseRecommendationOverlay();
                });
            }
        }

        private void AddRecoOption(string label, Action onClick)
        {
            var button = new Button
            {
                Content = label,
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };
            button.Click += (_, _) => onClick();
            RecoOptionsPanel.Children.Add(button);
        }

        private void CloseRecommendationOverlay_Click(object sender, RoutedEventArgs e)
        {
            CloseRecommendationOverlay();
        }

        private void CloseRecommendationOverlay()
        {
            RecommendationOverlay.Visibility = Visibility.Collapsed;
        }

        private async void ToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LibraryViewModel vm)
            {
                await vm.ToggleSelectedGameFavoriteAsync();
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
