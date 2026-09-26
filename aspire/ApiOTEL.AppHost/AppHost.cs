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

builder.AddProject<Projects.ApiOTEL>("apiotel-api")
    .WithReference(apiotelDb)
    .WaitFor(apiotelDb)
    .WaitFor(lgtm)
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", lgtm.GetEndpoint("otlp-grpc"));

builder.Build().Run();
