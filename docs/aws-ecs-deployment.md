# Rodando a API fora do Aspire (ex: AWS ECS)

Este documento descreve como a API se comporta quando **não** está rodando sob o `AppHost` do .NET Aspire — por exemplo, containerizada e publicada como uma ECS Service Task na AWS (`qa`, `sbox`, `prod`).

## O que é responsabilidade de quem

| Responsabilidade | Onde vive | Por quê |
|---|---|---|
| Instrumentação OpenTelemetry (ASP.NET Core, HttpClient, EF Core/Npgsql, runtime), health checks, resiliência HTTP | `aspire/ApiOTEL.ServiceDefaults` (referenciado por `src/ApiOTEL`) | É código puro do SDK do OpenTelemetry/ASP.NET Core — **não depende do Aspire em tempo de execução**, só foi criado pelo template do Aspire. Funciona igual dentro de um container standalone. |
| Nome do serviço (`service.name` = `apiotel-api`) e a flag experimental que habilita `EventId` nos logs | `src/ApiOTEL/Program.cs` + `ApiOTEL.ServiceDefaults` | Comportamento fixo da aplicação, igual em qualquer ambiente — não muda entre `qa`/`sbox`/`prod`, então fica embutido no código, não na orquestração. |
| Endpoint do coletor OTLP (`OTEL_EXPORTER_OTLP_ENDPOINT`), connection string do Postgres, tags de ambiente (`deployment.environment`) | Variáveis de ambiente da **task definition do ECS** (uma por ambiente) — localmente, o `AppHost` faz esse papel | Isso varia por ambiente: cada task definition (`qa`, `sbox`, `prod`) aponta pro Datadog Agent e pro banco daquele ambiente. |

Ou seja: **o binário publicado (`ApiOTEL.dll`/imagem Docker) é idêntico em todos os lugares.** O que muda entre rodar localmente via Aspire e rodar no ECS são só variáveis de ambiente — nenhum código precisa saber se está rodando sob o Aspire ou não.

## Variáveis de ambiente esperadas na task definition (ECS)

Configure estas variáveis na task definition de cada ambiente (`qa`, `sbox`, `prod`), apontando pro Datadog Agent do respectivo cluster:

```
ConnectionStrings__apiotel=Host=<host-do-postgres>;Port=5432;Username=<user>;Password=<senha>;Database=<db>
OTEL_EXPORTER_OTLP_ENDPOINT=http://<endereco-do-datadog-agent>:4317
OTEL_RESOURCE_ATTRIBUTES=deployment.environment=<qa|sbox|prod>
```

Notas:

- **`ConnectionStrings__apiotel`**: mesmo padrão usado ao rodar a API manualmente fora do Aspire (documentado no [README](../README.md)). Em produção, prefira ler a senha de um secret (AWS Secrets Manager/SSM Parameter Store) e injetar via `secrets` na task definition, não como env var em texto puro.
- **`OTEL_EXPORTER_OTLP_ENDPOINT`**: como o cluster ECS já tem o Datadog Agent rodando, aponte para o endereço/porta onde o Agent expõe o intake OTLP (por padrão `4317` gRPC / `4318` HTTP). O endereço exato depende de como o Agent está deployado no cluster:
  - **Agent como sidecar na mesma task** (comum em Fargate): use `http://localhost:4317`, já que containers da mesma task ECS (modo `awsvpc`) compartilham o namespace de rede.
  - **Agent como daemon service por instância** (comum em ECS em EC2, modo `bridge`): normalmente resolvido via variável de ambiente/metadata da instância, ou via service discovery (Cloud Map) apontando pro Agent. Ajuste conforme como o cluster já está configurado.
- **`OTEL_RESOURCE_ATTRIBUTES=deployment.environment=<qa|sbox|prod>`**: variável padrão do OpenTelemetry, lida automaticamente pelo SDK — é isso que faz o Datadog segmentar os dados por ambiente (tag `env`) sem precisar de nenhum código específico por ambiente.
- **`OTEL_SERVICE_NAME`**: opcional. Se não for definida, a API já usa `apiotel-api` como padrão (embutido em `ApiOTEL.ServiceDefaults`). Só defina isso se quiser um nome diferente por algum motivo.
- Se `OTEL_EXPORTER_OTLP_ENDPOINT` não estiver definida, a API simplesmente não exporta telemetria via OTLP (não trava, não dá erro) — então é seguro fazer o rollout dessa variável gradualmente por ambiente.

## O que já é fixo no código (não precisa configurar)

- **`service.name = apiotel-api`**: definido em `ApiOTEL.ServiceDefaults/Extensions.cs`, com `OTEL_SERVICE_NAME` como override opcional.
- **`OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES=true`**: setado por `Extensions.SetOpenTelemetryEnvironmentDefaults()`, chamado no início de `Program.cs`, **antes** de `WebApplication.CreateBuilder(args)`. Isso é importante: o SDK do OpenTelemetry lê essa flag a partir do `IConfiguration` que é montado durante o `CreateBuilder`, então setá-la depois desse ponto (como fizemos inicialmente, só no `ConfigureOpenTelemetry`) é tarde demais e o valor é ignorado silenciosamente. Foi validado localmente rodando a API "nua" (sem Aspire, só `dotnet run` + env vars) para confirmar que o `EventId` continua aparecendo nos logs exportados.
- Instrumentação automática de ASP.NET Core, HttpClient, EF Core/Npgsql e runtime .NET.

## Como validar localmente que a API funciona "fora do Aspire"

Você pode simular o cenário do ECS localmente, sem o `AppHost`:

```bash
# Suba um Postgres e um coletor OTLP quaisquer (ex: os mesmos containers usados pelo AppHost)
docker run -d --name pg-test -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=apiotel -p 55432:5432 postgres:16-alpine
docker run -d --name otel-test -p 3000:3000 -p 4317:4317 -p 4318:4318 grafana/otel-lgtm

# Rode a API só com variáveis de ambiente, sem o Aspire
cd src/ApiOTEL
ConnectionStrings__apiotel="Host=localhost;Port=55432;Username=postgres;Password=postgres;Database=apiotel" \
OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317" \
dotnet run
```

Essa é exatamente a validação que foi feita: a API respondeu normalmente, e os logs/traces exportados mostraram `service_name=apiotel-api` e `logrecord_event_id` corretamente preenchidos, sem nenhuma dependência do `AppHost`.

## Resumo

- `aspire/` (AppHost + o que ele injeta via `.WithEnvironment(...)`) é **só para orquestração local** — sobe Postgres/Grafana/Datadog Collector via Docker e aponta a API pra eles.
- `src/ApiOTEL` (incluindo a referência a `ApiOTEL.ServiceDefaults`) é **auto-suficiente**: builda numa imagem Docker normal, e funciona em qualquer lugar que forneça as variáveis de ambiente padrão do OpenTelemetry (`OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_RESOURCE_ATTRIBUTES`) e a connection string do banco — sem precisar do Aspire rodando.
