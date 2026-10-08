using GestorRecursos.Core.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Registra os DAOs no container: quem pedir um no construtor recebe uma
// instancia por requisicao (Scoped), que e o tempo de vida de uma chamada HTTP.
builder.Services.AddScoped<ClienteDAO>();
builder.Services.AddScoped<VeiculoDAO>();
builder.Services.AddScoped<MultaDAO>();
builder.Services.AddScoped<ContratacaoDAO>();
builder.Services.AddScoped<InstanciaDAO>();
builder.Services.AddScoped<PagamentoDAO>();
builder.Services.AddScoped<AgendaDAO>();
builder.Services.AddScoped<TipoServicoDAO>();
builder.Services.AddScoped<BuscaDAO>();

// Gera a documentacao OpenAPI, que a interface do Swagger consome.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// A interface de testes so fica de pe em desenvolvimento.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
