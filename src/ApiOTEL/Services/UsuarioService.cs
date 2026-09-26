using ApiOTEL.Data;
using ApiOTEL.Logging;
using ApiOTEL.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiOTEL.Services;

public class UsuarioService(UsuarioDbContext dbContext, ILogger<UsuarioService> logger)
{
    public async Task<Usuario> CriarAsync(UsuarioCreateRequest request)
    {
        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            Nome = request.Nome,
            Sobrenome = request.Sobrenome,
            DataNascimento = request.DataNascimento,
            Cpf = request.Cpf,
            DataCadastro = DateTime.UtcNow
        };

        dbContext.Usuarios.Add(usuario);
        await dbContext.SaveChangesAsync();

        logger.UsuarioCriado(usuario.Id);

        return usuario;
    }

    public async Task<Usuario?> ObterPorIdAsync(Guid id)
    {
        var usuario = await dbContext.Usuarios.FirstOrDefaultAsync(u => u.Id == id);

        if (usuario is null)
        {
            logger.UsuarioNaoEncontrado(id);
        }
        else
        {
            logger.UsuarioEncontrado(id);
        }

        return usuario;
    }

    public async Task<Usuario?> AtualizarAsync(Guid id, UsuarioUpdateRequest request)
    {
        var usuario = await dbContext.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null)
        {
            logger.UsuarioNaoEncontrado(id);
            return null;
        }

        usuario.Nome = request.Nome;
        usuario.Sobrenome = request.Sobrenome;
        usuario.DataNascimento = request.DataNascimento;
        usuario.Cpf = request.Cpf;

        await dbContext.SaveChangesAsync();

        logger.UsuarioAtualizado(id);

        return usuario;
    }

    public async Task<bool> DeletarAsync(Guid id)
    {
        var usuario = await dbContext.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
        if (usuario is null)
        {
            logger.UsuarioNaoEncontrado(id);
            return false;
        }

        dbContext.Usuarios.Remove(usuario);
        await dbContext.SaveChangesAsync();

        logger.UsuarioDeletado(id);

        return true;
    }
}
