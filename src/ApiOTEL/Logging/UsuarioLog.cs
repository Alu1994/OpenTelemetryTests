using Microsoft.Extensions.Logging;

namespace ApiOTEL.Logging;

public static partial class UsuarioLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Usuário {UsuarioId} criado com sucesso")]
    public static partial void UsuarioCriado(this ILogger logger, Guid usuarioId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Usuário {UsuarioId} encontrado")]
    public static partial void UsuarioEncontrado(this ILogger logger, Guid usuarioId);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Usuário {UsuarioId} não encontrado")]
    public static partial void UsuarioNaoEncontrado(this ILogger logger, Guid usuarioId);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Usuário {UsuarioId} atualizado com sucesso")]
    public static partial void UsuarioAtualizado(this ILogger logger, Guid usuarioId);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "Usuário {UsuarioId} deletado com sucesso")]
    public static partial void UsuarioDeletado(this ILogger logger, Guid usuarioId);
}
