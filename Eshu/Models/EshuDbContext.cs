using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace Eshu.Models
{
    public class EshuDbContext : DbContext
    {
        public DbSet<Game> Games => Set<Game>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "eshu.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Game>().HasIndex(g => g.Status);
            modelBuilder.Entity<Game>().HasIndex(g => g.IsInstalled);

            // Esto es lo que evita duplicar el mismo juego dos veces al re-escanear.
            modelBuilder.Entity<Game>().HasIndex(g => new { g.Platform, g.Title }).IsUnique();
        }
    }
}
