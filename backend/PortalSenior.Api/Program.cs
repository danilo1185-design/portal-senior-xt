using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PortalSenior.Api.Configuration;
using PortalSenior.Api.Controllers;
using PortalSenior.Api.Services.Auth;
using PortalSenior.Api.Services.Senior;
using PortalSenior.Api.Services.Session;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.Configure<SeniorOptions>(builder.Configuration.GetSection(SeniorOptions.SectionName));
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

    var seniorOptions = builder.Configuration.GetSection(SeniorOptions.SectionName).Get<SeniorOptions>() ?? new SeniorOptions();
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

    // Falha cedo e com mensagem clara em vez de emitir tokens com chave fraca/ausente.
    if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) || jwtOptions.SecretKey.Length < 32)
    {
        throw new InvalidOperationException(
            "Jwt:SecretKey ausente ou com menos de 32 caracteres. Configure via appsettings ou a variável de ambiente Jwt__SecretKey.");
    }

    if (string.IsNullOrWhiteSpace(seniorOptions.BaseUrl))
    {
        throw new InvalidOperationException("Senior:BaseUrl não configurado. Ex.: http://10.64.16.5:8080");
    }

    builder.Services.AddMemoryCache();
    builder.Services.AddDataProtection();
    builder.Services.AddSingleton<ISeniorCredentialStore, SeniorCredentialStore>();
    builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

    builder.Services.AddHttpClient<ISeniorAuthService, SeniorAuthService>(client =>
        client.Timeout = TimeSpan.FromSeconds(seniorOptions.TimeoutSeconds));

    builder.Services.AddHttpClient<ISeniorSalesService, SeniorSalesService>(client =>
        client.Timeout = TimeSpan.FromSeconds(seniorOptions.TimeoutSeconds));

    builder.Services.AddHttpClient<ISeniorClientService, SeniorClientService>(client =>
        client.Timeout = TimeSpan.FromSeconds(seniorOptions.TimeoutSeconds));

    builder.Services.AddHttpClient(HealthController.HealthClientName, client =>
        client.Timeout = TimeSpan.FromSeconds(seniorOptions.TimeoutSeconds));

    // CORS para o dev server do Vite (frontend React).
    const string devCorsPolicy = "frontend-dev";
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:5173"];
    builder.Services.AddCors(options => options.AddPolicy(devCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // Mantém os claims como emitidos ("sub" continua "sub"), sem o mapeamento
            // legado para URIs longas — caso contrário NameClaimType abaixo não acha nada.
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                NameClaimType = JwtRegisteredClaimNames.Sub,
                ClockSkew = TimeSpan.FromMinutes(1),
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Portal Senior XT — API",
            Version = "v1",
            Description = "API interna do portal. Autenticação via credenciais do ERP Senior XT (SGU).",
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "Informe o token no formato: Bearer {token}",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                },
                Array.Empty<string>()
            },
        });
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    // Em produção o TLS é encerrado na borda (Cloud Run / Firebase Hosting); um
    // UseHttpsRedirection interno geraria redirect incorreto atrás do proxy.

    // Serve a tela de login estática de fallback (wwwroot/index.html).
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseCors(devCorsPolicy);

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    Log.Information("Portal Senior XT iniciando. ERP configurado em {BaseUrl}", seniorOptions.BaseUrl);
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Falha fatal ao iniciar o Portal Senior XT");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
