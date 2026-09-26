using ApiOTEL.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiOTEL.Data;

public class UsuarioDbContext(DbContextOptions<UsuarioDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("usuarios");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Nome).IsRequired();
            entity.Property(u => u.Sobrenome).IsRequired();
            entity.Property(u => u.Cpf).IsRequired();
        });
    }
}
