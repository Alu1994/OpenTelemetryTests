# Plano de Observabilidade

Este documento descreve o plano usado para validar localmente, de forma gratuita, o setup de OpenTelemetry configurado no projeto (`aspire/ApiOTEL.ServiceDefaults`), e como evoluir para o Datadog (free tier) quando necessário.

O projeto já instrumenta automaticamente ASP.NET Core, HttpClient, EF Core/Npgsql e o runtime do .NET via OpenTelemetry, além de logs estruturados de alta performance (`LoggerMessage`) nos endpoints de usuário. Toda essa telemetria (traces, logs e métricas) é enviada via **OTLP** para o coletor apontado pela variável de ambiente `OTEL_EXPORTER_OTLP_ENDPOINT`.

O plano tem duas etapas, ambas já implementadas no `AppHost`:

1. **Opção 1 — Grafana LGTM**: stack local, gratuita, sem cadastro. Sempre ligada.
2. **Opção 2 — Datadog (free tier)**: opcional, liga automaticamente assim que você configura uma API key — roda em paralelo com a Opção 1, sem substituí-la.

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

O Datadog tem um free tier real (com limite de hosts/retenção), mas exige criar uma conta e gerar uma API key — não é "zero cadastro" como a Opção 1. **Essa opção já está implementada no `AppHost` e é opcional/aditiva**: quando não há API key configurada, tudo continua funcionando exatamente como na Opção 1 (API → `apiotel-lgtm` direto). Quando a API key é configurada, o `AppHost` liga automaticamente um terceiro container, um **OpenTelemetry Collector** (`apiotel-otelcol`), que passa a receber o OTLP da API e distribuir em paralelo tanto para o `apiotel-lgtm` quanto para o Datadog — os dois lados funcionam ao mesmo tempo, sem precisar escolher um.

### Passo 1 — Criar a conta e a API key

1. Crie uma conta grátis em https://www.datadoghq.com/free-datadog-trial/ (não pede cartão de crédito para o free tier).
2. Depois de logado, vá em *Organization Settings → API Keys* (ou acesse diretamente https://app.datadoghq.com/organization-settings/api-keys) e copie uma API key existente ou crie uma nova.
3. Anote também o **site** da sua conta (aparece na URL do Datadog, ex: `datadoghq.com`, `datadoghq.eu`, `us5.datadoghq.com`, etc.) — o `AppHost` já assume `datadoghq.com` por padrão; se a sua conta for de outro site, ajuste o valor de `DD_SITE` em `AppHost.cs`.

### Passo 2 — Configurar a API key localmente (sem commitar a key)

A API key **nunca** deve ir para o `AppHost.cs`/git. Ela é lida via um parâmetro do Aspire (`datadog-api-key`), configurado via *user secrets* do projeto `ApiOTEL.AppHost`:

```bash
cd aspire/ApiOTEL.AppHost
dotnet user-secrets set "Parameters:datadog-api-key" "<sua-api-key>"
```

### Passo 3 — Rodar

```bash
dotnet run --project aspire/ApiOTEL.AppHost
```

Como a API key agora está presente na configuração, o `AppHost` detecta isso automaticamente e sobe o container `apiotel-otelcol` (imagem `otel/opentelemetry-collector-contrib`, config em `aspire/ApiOTEL.AppHost/otelcol-config.yaml`), redirecionando o `OTEL_EXPORTER_OTLP_ENDPOINT` da API para ele em vez de para o `apiotel-lgtm` diretamente.

### Passo 4 — Validar

1. Gere tráfego contra a API (`src/ApiOTEL/ApiOTEL.http` ou `curl`).
2. No **Grafana** (`apiotel-lgtm`, como na Opção 1): continue vendo traces/logs/métricas normalmente — nada muda aqui.
3. No **Datadog**:
   - **APM → Traces**: o serviço `apiotel-api` com os spans de ASP.NET Core e Npgsql.
   - **Logs → Live Tail**: os logs estruturados da API correlacionados com `trace_id`.
   - **Metrics Explorer**: métricas de runtime do .NET e do Npgsql exportadas via OTLP.

> **Nota:** ao criar a conta, o Datadog pode te levar para um wizard de onboarding "Install the Datadog Agent on Docker" (que pede pra rodar `docker run ... registry.datadoghq.com/agent:7` com auto-instrumentação). **Não é necessário seguir esse wizard** — ele instala o Datadog Agent clássico, uma abordagem diferente da usada aqui. Este projeto já envia telemetria via OTLP/OpenTelemetry Collector diretamente, sem precisar do Agent. Pode fechar/pular essa tela e ir direto em APM/Logs/Metrics no menu lateral.

### Removendo a integração

Para voltar a rodar só com a Opção 1 (sem Datadog), basta remover o secret:

```bash
cd aspire/ApiOTEL.AppHost
dotnet user-secrets remove "Parameters:datadog-api-key"
```

Na próxima execução, o `AppHost` volta automaticamente a apontar a API direto para o `apiotel-lgtm`, sem subir o collector.

### Validação já realizada

Testado de ponta a ponta em duas etapas:

1. **Com uma API key inválida** (para validar o pipeline sem gastar uma key real): o `apiotel-otelcol` sobe corretamente, valida (e rejeita, como esperado) a key fake nos logs do container, e o tráfego continua chegando normalmente no `apiotel-lgtm` através dele — confirmando que o fan-out (um destino falhando não derruba o outro) funciona como esperado.
2. **Com uma API key real**: o `apiotel-otelcol` validou a key com sucesso (`"API key validation successful"` nos logs, confirmado também de forma independente via `GET https://api.datadoghq.com/api/v1/validate`), gerei tráfego real na API (criação/consulta/atualização/remoção de usuários), e **os dados apareceram no Datadog** (APM/Traces e Logs) — confirmado visualmente no painel pelo usuário. O mesmo tráfego continuou chegando no Grafana/Loki normalmente, confirmando que os dois destinos recebem os dados em paralelo sem conflito.
