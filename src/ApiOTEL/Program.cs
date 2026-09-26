using ApiOTEL.Data;
using ApiOTEL.Models;
using ApiOTEL.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddNpgsqlDbContext<UsuarioDbContext>("apiotel");
builder.Services.AddScoped<UsuarioService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UsuarioDbContext>();
    dbContext.Database.EnsureCreated();
}

app.MapGet("/usuarios/{id:guid}", async (Guid id, UsuarioService service) =>
    {
        var usuario = await service.ObterPorIdAsync(id);
        return usuario is not null ? Results.Ok(usuario) : Results.NotFound();
    })
    .WithName("GetUsuarioPorId");

app.MapPost("/usuarios", async (UsuarioCreateRequest request, UsuarioService service) =>
    {
        var usuario = await service.CriarAsync(request);
        return Results.Created($"/usuarios/{usuario.Id}", usuario);
    })
    .WithName("CriarUsuario");

app.MapPut("/usuarios/{id:guid}", async (Guid id, UsuarioUpdateRequest request, UsuarioService service) =>
    {
        var usuario = await service.AtualizarAsync(id, request);
        return usuario is not null ? Results.Ok(usuario) : Results.NotFound();
    })
    .WithName("AtualizarUsuario");

app.MapDelete("/usuarios/{id:guid}", async (Guid id, UsuarioService service) =>
    {
        return await service.DeletarAsync(id) ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeletarUsuario");

app.Run();
