# OpenTelemetryTests

API de usuários (CRUD) construída com ASP.NET Core Minimal APIs, persistência em PostgreSQL via EF Core, e orquestração/instrumentação local com .NET Aspire.

## Estrutura do repositório

```
.
├── src/
│   └── ApiOTEL/              # API (endpoints, models, EF Core, logging)
├── aspire/
│   ├── ApiOTEL.AppHost/      # Orquestrador Aspire (sobe Postgres + API)
│   └── ApiOTEL.ServiceDefaults/  # Instrumentação padrão (OpenTelemetry, health checks, resiliência)
├── tests/                    # Reservado para testes automatizados (ainda vazio)
└── OpenTelemetryTests.sln
```

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) (a versão é fixada em `global.json`)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (para o container do PostgreSQL/PgAdmin)

## Como rodar (via Aspire — recomendado)

O `AppHost` sobe automaticamente um container PostgreSQL (`apiotel-postgres`) e um PgAdmin (`apiotel-pgadmin-*`) via Docker, e inicia a API já conectada ao banco.

```bash
dotnet run --project aspire/ApiOTEL.AppHost
```

Ao iniciar, o terminal mostra a URL do **dashboard do Aspire** (ex: `https://localhost:17237`), onde é possível ver logs, traces, métricas e o endereço em que a API subiu.

Além disso, o AppHost sobe um container **Grafana LGTM** (`apiotel-lgtm`) e já configura a API para exportar toda a telemetria (traces, logs e métricas via OTLP) para ele — veja a seção [Observabilidade](#observabilidade) abaixo.

## Como rodar a API sozinha (sem Aspire)

Suba um PostgreSQL manualmente:

```bash
docker run -d --name apiotel-postgres -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=apiotel -p 5432:5432 postgres:16-alpine
```

E rode a API apontando a connection string para esse banco:

```bash
# bash
ConnectionStrings__apiotel="Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=apiotel" \
dotnet run --project src/ApiOTEL

# PowerShell
$env:ConnectionStrings__apiotel = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=apiotel"
dotnet run --project src/ApiOTEL
```

A tabela `usuarios` é criada automaticamente no primeiro start (via `EnsureCreated`).

## Endpoints

O arquivo [`src/ApiOTEL/ApiOTEL.http`](src/ApiOTEL/ApiOTEL.http) já contém requisições prontas para todos os endpoints abaixo.

| Método | Rota              | Descrição                     |
|--------|-------------------|--------------------------------|
| GET    | `/health`         | Health check                   |
| GET    | `/usuarios/{id}`  | Busca usuário por id (UUIDv7)  |
| POST   | `/usuarios`       | Cria um usuário                |
| PUT    | `/usuarios/{id}`  | Atualiza um usuário            |
| DELETE | `/usuarios/{id}`  | Remove um usuário              |

Exemplo de payload para `POST`/`PUT`:

```json
{
  "nome": "Matheus",
  "sobrenome": "Cavalcante",
  "dataNascimento": "1994-05-10",
  "cpf": "12345678900"
}
```

## Observabilidade

O projeto usa OpenTelemetry (via `ApiOTEL.ServiceDefaults`) para instrumentar automaticamente ASP.NET Core, HttpClient, EF Core/Npgsql e runtime do .NET, além dos logs estruturados de alta performance (`LoggerMessage`) da API. A telemetria (traces, logs e métricas) é enviada via **OTLP** para o coletor configurado em `OTEL_EXPORTER_OTLP_ENDPOINT`.

### Opção 1 — Grafana LGTM (local, grátis, já configurado)

O `AppHost` (`aspire/ApiOTEL.AppHost/AppHost.cs`) sobe automaticamente o container **[grafana/otel-lgtm](https://github.com/grafana/docker-otel-lgtm)** — uma stack "tudo em um" com Grafana + Tempo (traces) + Loki (logs) + Prometheus (métricas), já com os data sources pré-configurados e correlacionados (logs ↔ traces via `trace_id`). Basta rodar:

```bash
dotnet run --project aspire/ApiOTEL.AppHost
```

Depois de subir:

1. Gere algum tráfego contra a API (ex: usando o `src/ApiOTEL/ApiOTEL.http` ou `curl`).
2. Abra a **UI do Grafana** — a porta é exibida no dashboard do Aspire (resource `apiotel-lgtm`, endpoint `grafana`), por padrão `http://localhost:3000`.
3. Login: usuário `admin`, senha `admin` (padrão da imagem, sem custo/sem cadastro).
4. Vá em **Explore**:
   - Data source **Tempo** → busque traces (por serviço `apiotel-api`, ou TraceQL `{}`).
   - Data source **Loki** → query `{service_name="apiotel-api"}` para ver os logs, incluindo os campos `trace_id`/`span_id` que permitem pular direto do log para o trace correspondente.
   - Data source **Prometheus** → métricas como `http_server_request_duration_seconds`, `db_client_operation_duration_seconds`, `dotnet_gc_*`, etc.

Essa opção não exige nenhuma conta, API key ou cartão de crédito — é só o container rodando localmente.

### Opção 2 — Datadog (free tier)

O Datadog tem um free tier real (com limite de hosts/retenção), mas exige criar uma conta e gerar uma API key. Para plugar o mesmo setup de OpenTelemetry nele:

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

## Build e testes

```bash
dotnet build
```

A pasta `tests/` está reservada para os projetos de teste automatizado, que ainda serão adicionados.
