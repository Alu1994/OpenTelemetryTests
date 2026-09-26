# Plano de Observabilidade

Este documento descreve o plano usado para validar localmente, de forma gratuita, o setup de OpenTelemetry configurado no projeto (`aspire/ApiOTEL.ServiceDefaults`), e como evoluir para o Datadog (free tier) quando necessário.

O projeto já instrumenta automaticamente ASP.NET Core, HttpClient, EF Core/Npgsql e o runtime do .NET via OpenTelemetry, além de logs estruturados de alta performance (`LoggerMessage`) nos endpoints de usuário. Toda essa telemetria (traces, logs e métricas) é enviada via **OTLP** para o coletor apontado pela variável de ambiente `OTEL_EXPORTER_OTLP_ENDPOINT`.

O plano tem duas etapas:

1. **Opção 1 — Grafana LGTM**: stack local, gratuita, sem cadastro. Já implementada no `AppHost`.
2. **Opção 2 — Datadog (free tier)**: passos documentados para quando quiser plugar num serviço de observabilidade "real"/hospedado, que exige conta e API key.

## Opção 1 — Grafana LGTM (local, grátis, já configurado)

O `AppHost` (`aspire/ApiOTEL.AppHost/AppHost.cs`) sobe automaticamente o container **[grafana/otel-lgtm](https://github.com/grafana/docker-otel-lgtm)** — uma stack "tudo em um" com Grafana + Tempo (traces) + Loki (logs) + Prometheus (métricas), já com os data sources pré-configurados e correlacionados (logs ↔ traces via `trace_id`). A API recebe automaticamente a variável `OTEL_EXPORTER_OTLP_ENDPOINT` apontando para esse container.

### Como rodar

```bash
dotnet run --project aspire/ApiOTEL.AppHost
```

### Como validar

1. Gere algum tráfego contra a API (ex: usando o `src/ApiOTEL/ApiOTEL.http` ou `curl`).
2. Abra a **UI do Grafana** — a porta é exibida no dashboard do Aspire (resource `apiotel-lgtm`, endpoint `grafana`), por padrão `http://localhost:3000`.
3. Login: usuário `admin`, senha `admin` (padrão da imagem, sem custo/sem cadastro).
4. Vá em **Explore**:
   - Data source **Tempo** → busque traces (por serviço `apiotel-api`, ou TraceQL `{}`).
   - Data source **Loki** → query `{service_name="apiotel-api"}` para ver os logs, incluindo os campos `trace_id`/`span_id` que permitem pular direto do log para o trace correspondente.
   - Data source **Prometheus** → métricas como `http_server_request_duration_seconds`, `db_client_operation_duration_seconds`, `dotnet_gc_*`, etc.

Essa opção não exige nenhuma conta, API key ou cartão de crédito — é só o container rodando localmente.

### Validação já realizada

Esse setup foi testado de ponta a ponta (AppHost real, tráfego real contra a API, consultas diretas à API do Grafana) e confirmou:

- **Traces** no Tempo — span do ASP.NET Core aninhado com o span do Postgres/Npgsql.
- **Logs** no Loki — incluindo os `LoggerMessage` customizados (ex: "Usuário X criado com sucesso"), já correlacionados com `trace_id`/`span_id`.
- **Métricas** no Prometheus — runtime do .NET, GC, EF Core/Npgsql, etc.

### Limitação conhecida: `EventId` não aparece nos logs

Investigando os logs no Loki, percebemos que o `EventId` numérico definido em `[LoggerMessage(EventId = ...)]` não chegava como atributo exportado (só o texto da mensagem e os parâmetros do template, como `UsuarioId`, apareciam).

**Causa raiz:** o suporte a exportar `LogRecord.EventId` via OTLP (como os atributos `logrecord.event.id` e `EventName`) é **experimental** no OpenTelemetry .NET desde a versão `1.7.0-alpha.1` do `OpenTelemetry.Exporter.OpenTelemetryProtocol`, e continua experimental até a versão mais recente disponível (`1.19.1`, a mesma família de versões que a `1.15.3` usada no projeto) — não existe uma versão "mais nova" onde isso já vem habilitado por padrão. A partir da `1.13.0`, o `EventName` (quando definido) passou a ser exportado por padrão, mas o `logrecord.event.id` numérico continua atrás de uma feature flag.

**Correção aplicada** — em vez de duplicar o número manualmente no template da mensagem, habilitamos a feature flag oficial do SDK via variável de ambiente no `AppHost` (`aspire/ApiOTEL.AppHost/AppHost.cs`):

```csharp
.WithEnvironment("OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES", "true")
```

Com isso, `src/ApiOTEL/Logging/UsuarioLog.cs` ficou no formato original e simples (sem nenhum parâmetro extra), e o próprio SDK passa a exportar o `EventId` nativamente.

Validado em `apiotel-lgtm`: após habilitar a flag, uma nova entrada de log no Loki traz o campo estruturado `logrecord_event_id="1001"` (o Loki normaliza pontos para underscore em nomes de label), sem nenhuma mudança no código de logging da API.

## Opção 2 — Datadog (free tier)

O Datadog tem um free tier real (com limite de hosts/retenção), mas exige criar uma conta e gerar uma API key — não é "zero cadastro" como a Opção 1. Passos para plugar o mesmo setup de OpenTelemetry nele:

1. **Crie uma conta grátis** em https://www.datadoghq.com/free-datadog-trial/ e gere uma **API key** em *Organization Settings → API Keys*.
2. **Suba o Datadog Agent localmente**, já com o OTLP ingest habilitado, apontando para sua API key:
   ```bash
   docker run -d --name apiotel-datadog-agent \
     -e DD_API_KEY=<sua-api-key> \
     -e DD_SITE="datadoghq.com" \
     -e DD_APM_ENABLED=true \
     -e DD_OTLP_CONFIG_RECEIVER_PROTOCOLS_GRPC_ENDPOINT=0.0.0.0:4317 \
     -e DD_OTLP_CONFIG_RECEIVER_PROTOCOLS_HTTP_ENDPOINT=0.0.0.0:4318 \
     -e DD_OTLP_CONFIG_LOGS_ENABLED=true \
     -p 4317:4317 -p 4318:4318 -p 8126:8126 \
     -v /var/run/docker.sock:/var/run/docker.sock:ro \
     -v /proc/:/host/proc/:ro \
     -v /sys/fs/cgroup/:/host/sys/fs/cgroup:ro \
     gcr.io/datadoghq/agent:7
   ```
   *(No Windows/Docker Desktop os volumes de `/proc` e `/sys/fs/cgroup` podem ser omitidos — servem para coletar métricas de host em Linux.)*
3. **Troque o destino do OTLP** no `AppHost` (`aspire/ApiOTEL.AppHost/AppHost.cs`): em vez de apontar para o container `apiotel-lgtm`, aponte a variável de ambiente `OTEL_EXPORTER_OTLP_ENDPOINT` da API para o Agent, por exemplo trocando o container resource pelo endpoint do Agent (`http://localhost:4317`) — ou, se preferir manter os dois lado a lado para comparar, adicione o Agent como um segundo `AddContainer` e ajuste qual endpoint a API deve usar.
4. Rode o projeto normalmente (`dotnet run --project aspire/ApiOTEL.AppHost`) e gere tráfego na API.
5. **Valide no Datadog**:
   - **APM → Traces**: deve aparecer o serviço `apiotel-api` com os spans de ASP.NET Core, EF Core/Npgsql.
   - **Logs → Live Tail**: os logs estruturados da API (incluindo os `LoggerMessage` de `usuarios`) correlacionados com `trace_id`.
   - **Infrastructure/Metrics**: métricas de runtime do .NET e do Npgsql exportadas via OTLP.
6. Remova a API key/o Agent quando terminar de validar, para não deixar credenciais em texto plano no `AppHost` — prefira variáveis de ambiente (`builder.AddParameter` + *user secrets*, por exemplo) em vez de hardcodar a key no código caso decida manter essa integração no repositório.
