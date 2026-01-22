using Microsoft.EntityFrameworkCore;
using ReadingPdf.Areas.Terceros.Models;
using ReadingPdf.Models;

namespace ReadingPdf.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Existing DbSets...
        public DbSet<Movimiento> Movimiento { get; set; } = null!;
        public DbSet<Bank> Bank { get; set; } = null!;
        public DbSet<BankAccount> BankAccounts { get; set; } = null!;

        // Recognized companies
        public DbSet<EmpresaReconocida> EmpresasReconocidas { get; set; } = null!;

        // New: Empresas table
        public DbSet<Empresa> Empresas { get; set; } = null!;
        public IQueryable<TTerceros> TTerceros { get; internal set; }

        //public IQueryable<TTerceros> TTerceros { get; internal set; }

        // New: MovimientoHistorico DbSet
        public DbSet<MovimientoHistorico> MovimientoHistorico { get; set; } = null!;

        public DbSet<MovimientoEntrenamiento> MovimientosEntrenamiento { get; set; }

        public DbSet<Proceso> Procesos { get; set; }
        public DbSet<MovimientoProceso> MovimientosProceso { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ignore the in-memory helper property without referencing the symbol (avoids CS0229 ambiguity)
            modelBuilder.Entity(typeof(Movimiento)).Ignore("EmpresaExtraida");

            // Bank -> BankAccounts (1 - many)
            modelBuilder.Entity<Bank>()
                .HasMany(b => b.BankAccounts)
                .WithOne(ba => ba.Bank)
                .HasForeignKey(ba => ba.BankId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BankAccount>()
                .HasKey(ba => ba.BankAccId);

            modelBuilder.Entity<BankAccount>()
                .Property(ba => ba.BankAccName)
                .HasMaxLength(200);

            // Map Empresa to explicit table name "empresas"
            modelBuilder.Entity<Empresa>(entity =>
            {
                entity.ToTable("empresas");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EmpresaNombre)
                      .IsRequired()
                      .HasMaxLength(250);
            });
            modelBuilder.Entity<Proceso>()
               .HasMany(p => p.Movimientos)
               .WithOne(m => m.Proceso)
               .HasForeignKey(m => m.ProcesoId)
               .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Proceso>()
                .HasIndex(p => p.FechaCreacion);

            modelBuilder.Entity<MovimientoProceso>()
                .HasIndex(m => m.ProcesoId);

        }
    }
}
