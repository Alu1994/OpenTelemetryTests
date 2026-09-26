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
├── docs/                     # Documentação adicional (ex: plano de observabilidade)
└── OpenTelemetryTests.sln
```

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) (a versão é fixada em `global.json`)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (para o container do PostgreSQL/PgAdmin)

## Como rodar

Existem três formas de rodar o projeto. Nas três, o OpenTelemetry (traces, logs e métricas) pode ser enviado tanto para o **Grafana LGTM** (local, grátis) quanto para o **Datadog** (free tier, opcional) — veja [Observabilidade](#observabilidade) para o detalhe de cada backend.

### Opção 1 — Com Aspire (recomendado)

O `AppHost` sobe automaticamente um container PostgreSQL (`apiotel-postgres`), um PgAdmin (`apiotel-pgadmin-*`) e o Grafana LGTM (`apiotel-lgtm`) via Docker, e inicia a API já conectada ao banco e configurada para exportar telemetria via OTLP.

```bash
dotnet run --project aspire/ApiOTEL.AppHost
```

Ao iniciar, o terminal mostra a URL do **dashboard do Aspire** (ex: `https://localhost:17237`), onde é possível ver logs, traces, métricas e o endereço em que a API subiu.

Para também ligar o Datadog em paralelo, configure a API key antes de rodar (veja [Opção 2 — Datadog](docs/observability-plan.md#opção-2--datadog-free-tier)):

```bash
cd aspire/ApiOTEL.AppHost
dotnet user-secrets set "Parameters:datadog-api-key" "<sua-api-key>"
```

### Opção 2 — Sem Aspire

Sem o Aspire, você mesmo sobe o Postgres e o backend de observabilidade via Docker, e roda a API apontando pra eles.

**1. Suba o Postgres:**

```bash
docker run -d --name apiotel-postgres -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=apiotel -p 5432:5432 postgres:16-alpine
```

**2. Suba o backend de observabilidade** — escolha um (ou os dois):

- **Grafana LGTM** (local, grátis, sem cadastro):

  ```bash
  docker run -d --name apiotel-lgtm -p 3000:3000 -p 4317:4317 -p 4318:4318 grafana/otel-lgtm
  ```

  UI em `http://localhost:3000` (usuário/senha `admin`/`admin`).

- **Datadog** (free tier — exige conta e API key, veja [`docs/observability-plan.md`](docs/observability-plan.md)): suba um OpenTelemetry Collector apontando pra ele, usando o config em [`docs/otelcol-standalone-datadog.yaml`](docs/otelcol-standalone-datadog.yaml):

  ```bash
  # bash — se estiver no Git Bash no Windows, prefixe com MSYS_NO_PATHCONV=1 para o caminho do -v não ser reescrito
  docker run -d --name apiotel-otelcol \
    -e DD_API_KEY="<sua-api-key>" \
    -e DD_SITE="datadoghq.com" \
    -p 4317:4317 -p 4318:4318 \
    -v "$(pwd)/docs/otelcol-standalone-datadog.yaml:/etc/otelcol-contrib/config.yaml:ro" \
    otel/opentelemetry-collector-contrib
  ```

  Se quiser Datadog **e** Grafana LGTM ao mesmo tempo, suba o LGTM sem publicar as portas `4317`/`4318` (elas ficariam ocupadas pelo collector) e use o config `aspire/ApiOTEL.AppHost/otelcol-config.yaml` (que já faz fan-out pros dois), colocando os dois containers na mesma rede Docker:

  ```bash
  docker network create apiotel-net
  docker run -d --name apiotel-lgtm --network apiotel-net -p 3000:3000 grafana/otel-lgtm
  docker run -d --name apiotel-otelcol --network apiotel-net \
    -e DD_API_KEY="<sua-api-key>" -e DD_SITE="datadoghq.com" \
    -p 4317:4317 -p 4318:4318 \
    -v "$(pwd)/aspire/ApiOTEL.AppHost/otelcol-config.yaml:/etc/otelcol-contrib/config.yaml:ro" \
    otel/opentelemetry-collector-contrib
  ```

**3. Rode a API**, apontando para o Postgres e para a porta `4317` do backend escolhido (por isso ambos os cenários acima publicam a mesma porta — não precisa mudar nada na API dependendo do backend):

```bash
# bash
ConnectionStrings__apiotel="Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=apiotel" \
OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317" \
dotnet run --project src/ApiOTEL --urls "http://localhost:5072"

# PowerShell
$env:ConnectionStrings__apiotel = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=apiotel"
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
dotnet run --project src/ApiOTEL --urls "http://localhost:5072"
```

Alternativamente, essas mesmas variáveis já estão pré-configuradas em [`src/ApiOTEL/Properties/launchSettings.json`](src/ApiOTEL/Properties/launchSettings.json), então basta escolher o profile certo pro backend que você subiu:

```bash
# Grafana LGTM direto na porta 4317
dotnet run --project src/ApiOTEL --launch-profile standalone

# Coletor com fan-out pro Datadog na porta 4319 (ex: docker-compose --profile datadog)
dotnet run --project src/ApiOTEL --launch-profile standalone-datadog
```

A tabela `usuarios` é criada automaticamente no primeiro start (via `EnsureCreated`).

### Opção 3 — Sem Aspire, com a API em modo debug no Rider

Mesma ideia da Opção 2 (Postgres + backend de observabilidade via Docker), mas a infra sobe com um único `docker compose` e a API roda com breakpoints pelo Rider em vez de `dotnet run`.

1. Suba a infra (Postgres + Grafana LGTM) com o [`docker-compose.yml`](docker-compose.yml) na raiz do repo:

   ```bash
   docker compose up -d
   ```

   Isso sobe `apiotel-postgres` (porta `5432`) e `apiotel-lgtm` (Grafana em `http://localhost:3000`, OTLP em `4317`/`4318`) — validado subindo sem erros.

   Para ligar o Datadog também, exporte `DD_API_KEY` e suba com o profile `datadog` (adiciona o container `apiotel-otelcol`, que faz fan-out para o LGTM e o Datadog):

   ```bash
   DD_API_KEY="<sua-api-key>" docker compose --profile datadog up -d
   ```

   Nesse caso o coletor fica em portas de host **diferentes** (`4319`/`4318` vira `4320`), pra não conflitar com as do `apiotel-lgtm` (`4317`/`4318`) — por isso existe um launch profile próprio pra esse cenário (passo 3).

2. Abra a solution (`OpenTelemetryTests.sln`) no Rider.
3. No dropdown de Run/Debug Configurations (ao lado do botão de play/debug), selecione o profile correspondente ao que você subiu no passo 1:
   - **ApiOTEL: standalone** — se subiu só `docker compose up -d` (Grafana LGTM, sem Datadog). Aponta `OTEL_EXPORTER_OTLP_ENDPOINT` para `http://localhost:4317`.
   - **ApiOTEL: standalone-datadog** — se subiu com `--profile datadog`. Aponta `OTEL_EXPORTER_OTLP_ENDPOINT` para `http://localhost:4319` (a porta do `apiotel-otelcol`, não a do `apiotel-lgtm`). **Usar o profile `standalone` aqui manda tudo direto pro LGTM e a telemetria nunca chega no Datadog.**
4. Coloque seus breakpoints e clique em **Debug** (ícone de inseto).

Se o Rider não listar os profiles automaticamente, edite a configuração de run do projeto `ApiOTEL` e selecione o launch profile correspondente manualmente.

Para derrubar a infra depois: `docker compose down` (ou `docker compose --profile datadog down` se tiver usado o profile).

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

O projeto usa OpenTelemetry (via `ApiOTEL.ServiceDefaults`) para instrumentar automaticamente ASP.NET Core, HttpClient, EF Core/Npgsql e runtime do .NET, além dos logs estruturados de alta performance (`LoggerMessage`) da API. A telemetria (traces, logs e métricas) é enviada via **OTLP** para o coletor configurado em `OTEL_EXPORTER_OTLP_ENDPOINT` — essa variável é a única coisa que muda entre rodar via Aspire ou standalone (Opções 2 e 3 acima); o resto da instrumentação é o mesmo em qualquer cenário.

Dois backends possíveis, que podem rodar sozinhos ou em paralelo:

- **Grafana LGTM** — local, grátis, sem cadastro. Via Aspire já vem ligado por padrão; sem Aspire, é o container `docker run grafana/otel-lgtm` da Opção 2.
- **Datadog (free tier)** — opcional, exige conta e API key. Via Aspire liga sozinho quando você configura a API key via *user secrets*; sem Aspire, é o container `otel/opentelemetry-collector-contrib` apontando pra ele.

O plano completo de observabilidade — incluindo os passos para criar a conta/API key do Datadog e como validar cada setup — está documentado em **[`docs/observability-plan.md`](docs/observability-plan.md)**.

**Fora do Aspire (ex: AWS ECS):** a instrumentação de OpenTelemetry vive em `ApiOTEL.ServiceDefaults` e é referenciada pela própria API — não depende do Aspire em tempo de execução. Rodando a imagem Docker da API em qualquer outro lugar (ECS, por exemplo), basta configurar as variáveis de ambiente padrão do OpenTelemetry (`OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_RESOURCE_ATTRIBUTES`) apontando pro coletor/Datadog Agent daquele ambiente. Veja **[`docs/aws-ecs-deployment.md`](docs/aws-ecs-deployment.md)** para o guia completo (o que é fixo no código vs. o que cada ambiente configura).

## Build e testes

```bash
dotnet build
```

A pasta `tests/` está reservada para os projetos de teste automatizado, que ainda serão adicionados.
