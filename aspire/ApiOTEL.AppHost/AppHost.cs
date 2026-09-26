var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithContainerName("apiotel-postgres")
    .WithDataVolume()
    .WithPgAdmin(containerName: "apiotel-pgadmin");

var apiotelDb = postgres.AddDatabase("apiotel");

// Grafana LGTM: stack all-in-one (Grafana + Loki + Tempo + Prometheus) que recebe
// telemetria via OTLP e oferece uma UI (Grafana) para visualizar traces, logs e métricas.
var lgtm = builder.AddContainer("apiotel-lgtm", "grafana/otel-lgtm")
    .WithContainerName("apiotel-lgtm")
    .WithHttpEndpoint(port: 3000, targetPort: 3000, name: "grafana")
    .WithHttpEndpoint(targetPort: 4317, name: "otlp-grpc")
    .WithHttpEndpoint(targetPort: 4318, name: "otlp-http");

// Só o endereço do coletor OTLP é responsabilidade do Aspire (é específico da orquestração
// local). O resto da configuração de OpenTelemetry (nome do serviço, flags experimentais,
// etc.) já vem embutido em ApiOTEL.ServiceDefaults, então a API funciona igual quando rodar
// standalone fora do Aspire (ex: container no ECS) — só muda quem define
// OTEL_EXPORTER_OTLP_ENDPOINT (aqui vs. a task definition de cada ambiente).
var apiBuilder = builder.AddProject<Projects.ApiOTEL>("apiotel-api")
    .WithReference(apiotelDb)
    .WaitFor(apiotelDb)
    .WaitFor(lgtm);

// Datadog (free tier) é opcional: só é ligado quando a API key é configurada via
// `dotnet user-secrets set "Parameters:datadog-api-key" "<key>"` neste projeto (AppHost).
// Quando presente, sobe um OpenTelemetry Collector (apiotel-otelcol) que recebe o OTLP da
// API e distribui tanto pro Grafana LGTM quanto pro Datadog, em paralelo — veja
// docs/observability-plan.md para o passo a passo completo.
var datadogApiKeyValue = builder.Configuration["Parameters:datadog-api-key"];
if (!string.IsNullOrWhiteSpace(datadogApiKeyValue))
{
    var datadogApiKey = builder.AddParameter("datadog-api-key", secret: true);

    var otelCollector = builder.AddContainer("apiotel-otelcol", "otel/opentelemetry-collector-contrib")
        .WithContainerName("apiotel-otelcol")
        .WithBindMount("otelcol-config.yaml", "/etc/otelcol-contrib/config.yaml", isReadOnly: true)
        .WithEnvironment("DD_API_KEY", datadogApiKey)
        .WithEnvironment("DD_SITE", "datadoghq.com")
        .WithHttpEndpoint(targetPort: 4317, name: "otlp-grpc")
        .WithHttpEndpoint(targetPort: 4318, name: "otlp-http")
        .WaitFor(lgtm);

    apiBuilder
        .WaitFor(otelCollector)
        .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", otelCollector.GetEndpoint("otlp-grpc"));
}
else
{
    apiBuilder.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", lgtm.GetEndpoint("otlp-grpc"));
}

builder.Build().Run();
