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

## Build e testes

```bash
dotnet build
```

A pasta `tests/` está reservada para os projetos de teste automatizado, que ainda serão adicionados.
