using Application.Authentication;
using Application.DependencyInjection;
using Domain.Repositories;
using Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Presentation.Security;
using System.Text;
using System.Text.Json.Serialization;

// El ejecutable debe encontrar la misma configuración desde VS, CLI o su carpeta bin.
string? startupEnvironment = null;
#if DEBUG
// F5 o ejecutar el .exe directamente pueden no aplicar launchSettings.json.
// Un entorno indicado explícitamente conserva prioridad; Release no usa este default.
var startupConfiguration = new ConfigurationBuilder()
    .AddEnvironmentVariables("ASPNETCORE_")
    .AddEnvironmentVariables("DOTNET_")
    .AddCommandLine(args)
    .Build();
startupEnvironment = startupConfiguration["environment"] ?? Environments.Development;
#endif
var startupOptions = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    EnvironmentName = startupEnvironment
};
var builder = WebApplication.CreateBuilder(startupOptions);
if (builder.Environment.IsDevelopment())
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 65536);
// IIS impone el techo global; MVC conserva 64 KiB salvo en el endpoint PDF explícito.
builder.Services.Configure<IISServerOptions>(options => options.MaxRequestBodySize = 2113536);
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 65536;
    options.ValueLengthLimit = 8192;
    options.MultipartHeadersLengthLimit = 16384;
});

var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new();
var jwtErrors = new List<string>();
if (string.IsNullOrWhiteSpace(jwtSettings.Key) || Encoding.UTF8.GetByteCount(jwtSettings.Key) < 32)
    jwtErrors.Add("JwtSettings:Key debe contener al menos 32 bytes");
if (string.IsNullOrWhiteSpace(jwtSettings.Issuer)) jwtErrors.Add("falta JwtSettings:Issuer");
if (string.IsNullOrWhiteSpace(jwtSettings.Audience)) jwtErrors.Add("falta JwtSettings:Audience");
if (jwtSettings.DurationInMinutes is <= 0 or > 30)
    jwtErrors.Add("JwtSettings:DurationInMinutes debe estar entre 1 y 30");
if (jwtErrors.Count > 0)
    throw new InvalidOperationException($"Configuración JWT inválida: {string.Join("; ", jwtErrors)}. " +
        $"Entorno: {builder.Environment.EnvironmentName}. Directorio: {builder.Environment.ContentRootPath}. " +
        "En Development se carga appsettings.Local.json; en otros entornos configure JwtSettings__Key.");

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddApiRateLimits(builder.Configuration);
builder.Services.AddScoped<PdfUploadQuotaFilter>();
builder.Services.AddSingleton<PdfAccountRateFilter>();
// Nunca activar SensitiveDataLogging: los errores de DB pueden involucrar datos personales.
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Critical);
builder.Logging.AddFilter("Npgsql", LogLevel.Critical);
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.IncludeErrorDetails = false;
    options.TokenValidationParameters = new()
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer, ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        NameClaimType = "sub", RoleClaimType = "role", ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var principal = context.Principal!;
            if (!Guid.TryParse(principal.FindFirst("sub")?.Value, out var id) ||
                !Guid.TryParse(principal.FindFirst("ver")?.Value, out var version) ||
                principal.FindFirst("role")?.Value is not string role ||
                !await context.HttpContext.RequestServices.GetRequiredService<IUsuarioRepository>()
                    .AccesoVigenteAsync(id, version, role, context.HttpContext.RequestAborted))
                context.Fail("Acceso no vigente.");
        }
    };
});
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin", "SuperAdmin"));
});
builder.Services.AddCors(options => options.AddPolicy("AllowReactApp", policy =>
    policy.WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddControllers(options => options.Filters.Add(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(65536))).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.JsonSerializerOptions.AllowDuplicateProperties = false;
    options.JsonSerializerOptions.MaxDepth = 16;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "LLA Santa Fe API", Version = "v1" });
    options.OperationFilter<AnonymousOperationFilter>();
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Ingresar solamente el token JWT."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowReactApp");
// Antes de validar JWT (consulta a DB) y antes de BCrypt.
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
