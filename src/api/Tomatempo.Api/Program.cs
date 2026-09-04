var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();

/// <summary>
/// Existe para que <c>WebApplicationFactory&lt;Program&gt;</c> enxergue o entry point
/// gerado pelas top-level statements (§6.3, contrato de API).
/// </summary>
public partial class Program;
