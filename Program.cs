// CI/CD + SonarCloud + Railway ativado – 01/12/2025
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Pomelo.EntityFrameworkCore.MySql;
using System;
using System.Text;
using System.Text.Json.Serialization;
using VEA.API.Data;
using VEA.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ================= NOVO: Garante que variáveis do Railway sejam lidas =================
builder.Configuration.AddEnvironmentVariables(); // ← essencial pro Railway funcionar

// Configuração forte do SMTP (com fallback pra variável de ambiente)
builder.Services.Configure<VEA.API.Models.SmtpSettings>(options =>
{
    builder.Configuration.GetSection("Smtp").Bind(options);
    // Sobrescreve a senha com a variável de ambiente (Railway)
    var smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
    if (!string.IsNullOrEmpty(smtpPassword))
        options.Password = smtpPassword;
});

// Serviços (o resto continua igual)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddScoped<ServiceService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// DbContext - configuração diferenciada para ambiente de teste
if (builder.Environment.IsEnvironment("Testing"))
{
    // Em ambiente de teste, usa InMemory database (será sobrescrito pela TestFactory)
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("TestingDb"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
        throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
               .LogTo(Console.WriteLine, LogLevel.Information)
               .EnableSensitiveDataLogging();
    });
}

// JWT (já está ótimo)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not found.")))
        };
        if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
            options.RequireHttpsMetadata = false;
    });

// CORS (perfeito como está) → MANTIVE EXATAMENTE IGUAL
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200",
                "https://vea-nine.vercel.app",
                "https://vea-ebebk185i-eumesmomatheus-projects.vercel.app"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "VEA API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando o formato Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "VEA API v1"));

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var error = "{\"error\": \"Erro interno no servidor\"}";
        var bytes = Encoding.UTF8.GetBytes(error);
        await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
    });
});

// AS DUAS ÚNICAS MUDANÇAS QUE RESOLVEM TUDO:
// 1) CORS tem que vir ANTES de Authentication/Authorization
app.UseCors("AllowAngular");           // ← movi pra cá (era depois)
app.UseAuthentication();
app.UseAuthorization();

// 2) StaticFiles depois do CORS e antes do MapControllers (ordem correta)
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
    }
});

app.UseHttpsRedirection();
app.MapControllers();

// Cria o banco se não existir (apenas em ambientes não-teste)
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();

        // ⚠️ DROPA TODAS AS TABELAS E RECRIA DO ZERO (REMOVER APÓS USAR!)
        try
        {
            Console.WriteLine("[Migration] ⚠️ DROPANDO TODAS AS TABELAS DO BANCO...");
            dbContext.Database.ExecuteSqlRaw("SET FOREIGN_KEY_CHECKS = 0");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS Appointments");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS EmployeeBlocks");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS EmployeeService");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS Services");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS Employees");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS Clients");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS Roles");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS Companies");
            dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS __EFMigrationsHistory");
            dbContext.Database.ExecuteSqlRaw("SET FOREIGN_KEY_CHECKS = 1");
            Console.WriteLine("[Migration] ✅ TODAS AS TABELAS FORAM DROPADAS!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Migration] Aviso ao dropar tabelas: {ex.Message}");
        }

        // Recria todas as tabelas com os tipos corretos
        Console.WriteLine("[Migration] 🔄 Recriando todas as tabelas...");
        dbContext.Database.EnsureCreated();
        Console.WriteLine("[Migration] ✅ Banco de dados recriado do zero!");
    }
}

app.Run();