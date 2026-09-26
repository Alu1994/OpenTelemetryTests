using Microsoft.Extensions.Logging;

namespace ApiOTEL.Logging;

// O exporter OTLP usado (OpenTelemetry.Exporter.OpenTelemetryProtocol) não serializa
// LogRecord.EventId como atributo, então o EventId é replicado aqui como parâmetro do
// template da mensagem para ficar visível em backends de log (ex: Loki/Grafana).
public static partial class UsuarioLog
{
    private const int UsuarioCriadoId = 1001;
    private const int UsuarioEncontradoId = 1002;
    private const int UsuarioNaoEncontradoId = 1003;
    private const int UsuarioAtualizadoId = 1004;
    private const int UsuarioDeletadoId = 1005;

    [LoggerMessage(EventId = UsuarioCriadoId, Level = LogLevel.Information, Message = "Usuário {UsuarioId} criado com sucesso (EventId: {EventId})")]
    private static partial void UsuarioCriadoCore(this ILogger logger, Guid usuarioId, int eventId);

    public static void UsuarioCriado(this ILogger logger, Guid usuarioId) =>
        logger.UsuarioCriadoCore(usuarioId, UsuarioCriadoId);

    [LoggerMessage(EventId = UsuarioEncontradoId, Level = LogLevel.Information, Message = "Usuário {UsuarioId} encontrado (EventId: {EventId})")]
    private static partial void UsuarioEncontradoCore(this ILogger logger, Guid usuarioId, int eventId);

    public static void UsuarioEncontrado(this ILogger logger, Guid usuarioId) =>
        logger.UsuarioEncontradoCore(usuarioId, UsuarioEncontradoId);

    [LoggerMessage(EventId = UsuarioNaoEncontradoId, Level = LogLevel.Warning, Message = "Usuário {UsuarioId} não encontrado (EventId: {EventId})")]
    private static partial void UsuarioNaoEncontradoCore(this ILogger logger, Guid usuarioId, int eventId);

    public static void UsuarioNaoEncontrado(this ILogger logger, Guid usuarioId) =>
        logger.UsuarioNaoEncontradoCore(usuarioId, UsuarioNaoEncontradoId);

    [LoggerMessage(EventId = UsuarioAtualizadoId, Level = LogLevel.Information, Message = "Usuário {UsuarioId} atualizado com sucesso (EventId: {EventId})")]
    private static partial void UsuarioAtualizadoCore(this ILogger logger, Guid usuarioId, int eventId);

    public static void UsuarioAtualizado(this ILogger logger, Guid usuarioId) =>
        logger.UsuarioAtualizadoCore(usuarioId, UsuarioAtualizadoId);

    [LoggerMessage(EventId = UsuarioDeletadoId, Level = LogLevel.Information, Message = "Usuário {UsuarioId} deletado com sucesso (EventId: {EventId})")]
    private static partial void UsuarioDeletadoCore(this ILogger logger, Guid usuarioId, int eventId);

    public static void UsuarioDeletado(this ILogger logger, Guid usuarioId) =>
        logger.UsuarioDeletadoCore(usuarioId, UsuarioDeletadoId);
}
