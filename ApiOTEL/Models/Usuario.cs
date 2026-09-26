namespace ApiOTEL.Models;

public class Usuario
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public required string Sobrenome { get; set; }
    public DateOnly DataNascimento { get; set; }
    public required string Cpf { get; set; }
    public DateTime DataCadastro { get; set; }
}

public record UsuarioCreateRequest(string Nome, string Sobrenome, DateOnly DataNascimento, string Cpf);

public record UsuarioUpdateRequest(string Nome, string Sobrenome, DateOnly DataNascimento, string Cpf);
