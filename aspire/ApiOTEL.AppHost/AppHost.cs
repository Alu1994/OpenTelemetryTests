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

var apiBuilder = builder.AddProject<Projects.ApiOTEL>("apiotel-api")
    .WithReference(apiotelDb)
    .WaitFor(apiotelDb)
    .WaitFor(lgtm)
    // Habilita a exportação de LogRecord.EventId como atributo OTLP (logrecord.event.id /
    // EventName). Ainda é experimental no SDK do OpenTelemetry .NET (mesmo na versão mais
    // recente), por isso precisa dessa flag em vez de vir habilitado por padrão.
    .WithEnvironment("OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES", "true");

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
