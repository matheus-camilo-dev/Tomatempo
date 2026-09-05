using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Tomatempo.Api;
using Tomatempo.Api.Auth;
using Tomatempo.Api.Endpoints;
using Tomatempo.Api.Services;
using Tomatempo.Domain;
using Tomatempo.Infrastructure;
using Tomatempo.Infrastructure.Entities;

var builder = WebApplication.CreateBuilder(args);

// A resposta não precisa dizer em que servidor roda; dizer só ajuda quem procura alvo por
// versão conhecida (§7.2/A05).
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// --- Log estruturado (§9) ---------------------------------------------------
builder.Services.AddSerilog(config => config
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// --- Erros: nada de exceção virando corpo de resposta (§7.2/A05) ------------
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();

// Sem isto, corpo malformado vira um 400 vazio; com isto, vira problem+json com code (§4).
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

// --- Composição -------------------------------------------------------------
builder.Services.AddTomatempoInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AccountWorkflow>();
builder.Services.AddScoped<IExternalIdentityReader, CookieExternalIdentityReader>();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton<AuthAudit>();

builder.Services
    .AddIdentityCore<TomatempoUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        // Tamanho mínimo e lista de senhas comuns são a política inteira (§7.2/A07). Regra de
        // composição (maiúscula, dígito, símbolo) não entra: empurra o usuário para senha curta
        // e decorável, que é pior do que uma frase longa.
        options.Password.RequiredLength = RuleConstants.PasswordMinLength;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireDigit = false;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<TomatempoDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

var autenticacao = builder.Services.AddAuthentication(AuthSchemes.Application);
autenticacao.AddIdentityCookies();

var googleClientId = builder.Configuration["Auth:Google:ClientId"];
var googleClientSecret = builder.Configuration["Auth:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    autenticacao.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = AuthSchemes.External;
        // Explícito para não depender de default: sem PKCE, um código interceptado no
        // redirecionamento vira sessão para quem o interceptou (§7.3.4).
        options.UsePkce = true;
        // É esta claim que decide entrar ou recusar (§3.10) — sem mapeá-la, a decisão viraria
        // um "confio no provedor" implícito.
        options.ClaimActions.MapJsonKey("email_verified", "email_verified", "boolean");
    });
}

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = AuthSchemes.SessionCookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;

    // A API não tem tela de login: quem não está autenticado recebe 401, não um redirecionamento.
    options.Events.OnRedirectToLogin = context =>
        Problems.WriteAsync(context.Response, StatusCodes.Status401Unauthorized, Problems.Unauthenticated, "Não autenticado.");
    options.Events.OnRedirectToAccessDenied = context =>
        Problems.WriteAsync(context.Response, StatusCodes.Status403Forbidden, Problems.Forbidden, "Sem permissão.");
});

// Revalidar o carimbo de segurança a cada requisicao: sem isso, a troca de senha só
// derrubaria as sessões antigas 30 minutos depois — tempo em que quem roubou a senha
// continuaria dentro (§3.10). O intervalo é negativo, e não zero, porque a comparação do
// framework é estrita (`decorrido > intervalo`): com zero, uma sessão recém-emitida, cujo
// tempo decorrido ainda é zero, escaparia da validação.
builder.Services.Configure<SecurityStampValidatorOptions>(
    options => options.ValidationInterval = TimeSpan.FromSeconds(-1));

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Resposta de limite também é erro de contrato: problem+json com code estável (§4).
    options.OnRejected = async (context, _) => await Problems.WriteAsync(
        context.HttpContext.Response,
        StatusCodes.Status429TooManyRequests,
        Problems.TooManyRequests,
        "Tentativas demais.");

    options.AddPolicy(RateLimitPolicies.AnonymousAuth, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "sem-ip",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = RuleConstants.AnonymousAuthAttemptsPerMinutePerIp,
            Window = TimeSpan.FromMinutes(1),
        }));
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Particionado por conta. Login e recuperação, que são anônimos, têm o próprio limite
        // por e-mail em LoginThrottle (§4).
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null
            ? RateLimitPartition.GetNoLimiter("anonimo")
            : RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
            });
    });
});

var webOrigin = builder.Configuration["App:WebOrigin"];
if (!string.IsNullOrWhiteSpace(webOrigin))
{
    // Origem explícita e nominal. `AllowAnyOrigin` com credenciais é proibido (§7.2/A05).
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(webOrigin)
        .AllowCredentials()
        .AllowAnyHeader()
        .AllowAnyMethod()));
}

var app = builder.Build();

// Em dev e em teste a migração roda na subida; em produção é etapa separada, antes do
// rollout, nunca automática no startup (§10).
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<TomatempoDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();

// Os cabeçalhos são escritos no início da resposta, e não aqui na entrada, porque o
// tratador de exceção limpa a resposta antes de escrever a dele — e levava os cabeçalhos
// junto, deixando justamente as respostas de erro sem proteção (§7.2/A05).
app.Use((context, next) =>
{
    context.Response.OnStarting(static estado =>
    {
        var headers = ((HttpContext)estado).Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        // A API só devolve JSON: nada aqui pode carregar script, estilo ou moldura.
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        return Task.CompletedTask;
    }, context);

    return next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (!string.IsNullOrWhiteSpace(webOrigin))
{
    app.UseCors();
}

// Formulário de outro site só consegue enviar estes três tipos. Recusá-los em quem muda
// estado fecha o caminho de CSRF que sobra além do SameSite=Lax — e nenhum cliente legítimo
// do Tomatempo manda formulário: a API fala JSON (§7.2/A05).
app.Use(async (context, next) =>
{
    string[] tiposDeFormulario =
    [
        "application/x-www-form-urlencoded",
        "multipart/form-data",
        "text/plain",
    ];

    var mudaEstado = !HttpMethods.IsGet(context.Request.Method)
        && !HttpMethods.IsHead(context.Request.Method)
        && !HttpMethods.IsOptions(context.Request.Method);

    var tipo = context.Request.ContentType;
    if (mudaEstado && tipo is not null
        && tiposDeFormulario.Any(t => tipo.StartsWith(t, StringComparison.OrdinalIgnoreCase)))
    {
        await Problems.WriteAsync(
            context.Response,
            StatusCodes.Status415UnsupportedMediaType,
            Problems.UnsupportedMediaType,
            "Esta API aceita apenas JSON.");
        return;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAuthEndpoints();
app.MapMeEndpoints();

app.Run();

/// <summary>
/// Existe para que <c>WebApplicationFactory&lt;Program&gt;</c> enxergue o entry point
/// gerado pelas top-level statements (§6.3, contrato de API).
/// </summary>
public partial class Program;
