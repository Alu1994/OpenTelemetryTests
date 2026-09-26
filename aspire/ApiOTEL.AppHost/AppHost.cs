var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithContainerName("apiotel-postgres")
    .WithDataVolume()
    .WithPgAdmin(containerName: "apiotel-pgadmin");

var apiotelDb = postgres.AddDatabase("apiotel");

builder.AddProject<Projects.ApiOTEL>("apiotel-api")
    .WithReference(apiotelDb)
    .WaitFor(apiotelDb);

builder.Build().Run();
