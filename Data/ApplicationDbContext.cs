// VEA.API/Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using System.Linq;
using VEA.API.Models;

namespace VEA.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Company> Companies { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;
        public DbSet<Client> Clients { get; set; } = null!;
        public DbSet<Service> Services { get; set; } = null!;
        public DbSet<Appointment> Appointments { get; set; } = null!;
        public DbSet<Address> Addresses { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<EmployeeBlock> EmployeeBlocks { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // MATA TODAS AS COLUNAS FANTASMAS DO APPOINTMENT DE UMA VEZ POR TODAS
            modelBuilder.Entity<Appointment>(entity =>
            {
                // Ignora explicitamente as que já apareceram
                entity.Ignore("ServiceId");
                entity.Ignore("CompanyId1");
                entity.Ignore("CompanyId1");
                entity.Ignore("CompanyId2");
                entity.Ignore("CompanyId3");
                entity.Ignore("CompanyId4");
                entity.Ignore("CompanyId5");

                // Mata automaticamente qualquer coluna que tenha "CompanyId" ou "ServiceId" no nome (exceto a CompanyId original)
                foreach (var property in entity.Metadata.GetProperties().ToList())
                {
                    if (property.Name.Contains("ServiceId") ||
                        (property.Name.Contains("CompanyId") && property.Name != "CompanyId"))
                    {
                        entity.Ignore(property.Name);
                    }
                }
            });

            // RELACIONAMENTOS E CONFIGURAÇÕES ORIGINAIS (tudo que você já tinha)
            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Company)
                .WithMany(c => c.Employees)
                .HasForeignKey(e => e.CompanyId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Role>()
                .HasOne(r => r.Company)
                .WithMany()
                .HasForeignKey(r => r.CompanyId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Role)
                .WithMany(r => r.Employees)
                .HasForeignKey(e => e.RoleId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Service>()
                .HasOne(s => s.Company)
                .WithMany(c => c.Services)
                .HasForeignKey(s => s.CompanyId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<EmployeeBlock>()
                .HasOne(b => b.Employee)
                .WithMany(e => e.Blocks)
                .HasForeignKey(b => b.EmployeeId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Client>()
                .HasOne(c => c.Company)
                .WithMany()
                .HasForeignKey(c => c.CompanyId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Client)
                .WithMany()
                .HasForeignKey(a => a.ClientId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Employee)
                .WithMany()
                .HasForeignKey(a => a.EmployeeId)
                .IsRequired()
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Company)
                .WithMany()
                .HasForeignKey(a => a.CompanyId)
                .IsRequired()
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Service>()
                .HasMany(s => s.Employees)
                .WithMany(e => e.Services)
                .UsingEntity(j => j.ToTable("ServiceEmployee"));

            modelBuilder.Entity<Company>()
                .HasOne(c => c.Address)
                .WithOne(a => a.Company)
                .HasForeignKey<Address>(a => a.CompanyId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            // ÍNDICES
            modelBuilder.Entity<Company>().HasIndex(c => c.Email).IsUnique();
            modelBuilder.Entity<Client>().HasIndex(c => c.Email).IsUnique();
            modelBuilder.Entity<Employee>().HasIndex(e => new { e.Email, e.CompanyId }).IsUnique();
            modelBuilder.Entity<Role>().HasIndex(r => r.Name);
            modelBuilder.Entity<Role>().HasIndex(r => r.CompanyId);
            modelBuilder.Entity<Appointment>().HasIndex(a => a.StartDateTime);
            modelBuilder.Entity<Appointment>().HasIndex(a => new { a.CompanyId, a.EmployeeId });
            modelBuilder.Entity<Appointment>().HasIndex(a => a.Status);
            modelBuilder.Entity<EmployeeBlock>().HasIndex(b => new { b.EmployeeId, b.BlockDate });
            modelBuilder.Entity<EmployeeBlock>().HasIndex(b => b.StartTime);
            modelBuilder.Entity<Company>().HasIndex(c => c.BusinessType);
            modelBuilder.Entity<Client>().HasIndex(c => c.CompanyId);
            modelBuilder.Entity<Address>().HasIndex(a => a.Cep);
            modelBuilder.Entity<Address>().HasIndex(a => a.Cidade);

            // OperatingHours padrão
            modelBuilder.Entity<Company>()
                .Property(c => c.OperatingHours)
                .IsRequired()
                .HasDefaultValue("09:00-18:00");
        }
    }
}