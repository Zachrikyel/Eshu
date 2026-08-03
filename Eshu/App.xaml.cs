using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Eshu.Models;
using Eshu.Services;
using Eshu.ViewModels;

namespace Eshu
{
    public partial class App : Application
    {
        // Punto único de acceso al contenedor de dependencias. MainWindow (y lo
        // que sigamos agregando en las próximas partes) lo usa para pedir lo que necesita.
        public static ServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // Crea el archivo eshu.db si todavía no existe (primer arranque).
            var dbFactory = ServiceProvider.GetRequiredService<IDbContextFactory<EshuDbContext>>();
            using (var db = dbFactory.CreateDbContext())
            {
                db.Database.EnsureCreated();
            }

            base.OnStartup(e);
            // A partir de aquí, StartupUri="MainWindow.xaml" (en App.xaml) abre la ventana.
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Fábrica en vez de un contexto único: el motor de sync crea uno nuevo
            // por operación, porque DbContext no es seguro entre hilos y los agentes
            // avisan desde hilos distintos (FileSystemWatcher, el timer de GOG).
            services.AddDbContextFactory<EshuDbContext>();

            services.AddSingleton<ILibraryAgent, SteamAgent>();
            services.AddSingleton<ILibraryAgent, EpicAgent>();
            services.AddSingleton<ILibraryAgent, GogAgent>();
            services.AddSingleton<ILibraryAgent>(_ => new LocalAgent(LoadWatchedFolders()));

            services.AddSingleton<LibrarySyncEngine>();
            services.AddSingleton<LibraryViewModel>();
            services.AddSingleton<IgdbMetadataService>();
            services.AddSingleton<RecommendationEngine>();

            // Parte 5: aquí entra MainWindow, pidiendo LibraryViewModel del contenedor.
        }

        // "Local" no tiene una tienda que le diga qué carpetas mirar, así que las
        // leemos de un archivo de texto junto al ejecutable — cualquier cantidad
        // de discos o rutas, sin tocar código para agregar una nueva.
        private static List<string> LoadWatchedFolders()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "watched-folders.json");

            if (!File.Exists(configPath))
            {
                // Primer arranque: dejamos un ejemplo editable en vez de una lista vacía.
                var example = new List<string> { @"C:\Juegos", @"D:\Games" };
                File.WriteAllText(configPath, JsonSerializer.Serialize(example, new JsonSerializerOptions { WriteIndented = true }));
                return example;
            }

            try
            {
                var json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
