using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Eshu.Models;

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
            using (var scope = ServiceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<EshuDbContext>();
                db.Database.EnsureCreated();
            }

            base.OnStartup(e);
            // A partir de aquí, StartupUri="MainWindow.xaml" (en App.xaml) abre la ventana.
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<EshuDbContext>();

            // Parte 2: aquí registramos SteamAgent/EpicAgent/LocalAgent y el motor de sincronización.
            // Por ahora el contenedor solo conoce la base de datos.
        }
    }
}
