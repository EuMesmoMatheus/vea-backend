// Testes/TestWebApplicationFactory.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using VEA.API.Data;
using VEA.API.Services;
using Moq;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace VEA.API.Testes
{
    /// <summary>
    /// Factory customizada para testes de integração.
    /// Configura banco InMemory compartilhado e autenticação de teste.
    /// </summary>
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName;
        public Mock<IEmailService> MockEmailService { get; }

        public CustomWebApplicationFactory()
        {
            _databaseName = "TestDb_" + Guid.NewGuid().ToString("N");
            MockEmailService = new Mock<IEmailService>();
            MockEmailService.Setup(x => x.SendConfirmationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                           .Returns(System.Threading.Tasks.Task.CompletedTask);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var projectDir = GetProjectPath();
            builder.UseContentRoot(projectDir);
            builder.UseEnvironment("Testing");

            builder.ConfigureTestServices(services =>
            {
                // Remove o DbContext real (MySQL)
                var descriptors = services.Where(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                         d.ServiceType == typeof(ApplicationDbContext)).ToList();

                foreach (var descriptor in descriptors)
                    services.Remove(descriptor);

                // Adiciona DbContext InMemory com nome fixo para esta instância
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_databaseName);
                    options.EnableSensitiveDataLogging();
                });

                // Mock do serviço de email
                services.RemoveAll<IEmailService>();
                services.AddScoped(_ => MockEmailService.Object);

                // Remove todas as configurações de autenticação existentes
                services.RemoveAll<IConfigureOptions<AuthenticationOptions>>();
                
                // Configura autenticação de teste como padrão
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                    options.DefaultScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }

        /// <summary>
        /// Cria um escopo para acessar o banco de dados nos testes
        /// </summary>
        public IServiceScope CreateScope() => Services.CreateScope();

        /// <summary>
        /// Obtém o DbContext para manipulação de dados nos testes
        /// </summary>
        public ApplicationDbContext GetDbContext()
        {
            var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        }

        /// <summary>
        /// Cria um HttpClient autenticado como um usuário específico
        /// </summary>
        public HttpClient CreateAuthenticatedClient(int userId = 1, string role = "Client", int companyId = 1)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
            client.DefaultRequestHeaders.Add("X-Test-Claims", $"{userId},{role},{companyId}");
            return client;
        }

        /// <summary>
        /// Cria um HttpClient autenticado como Admin
        /// </summary>
        public HttpClient CreateAdminClient(int adminId = 1, int companyId = 1)
            => CreateAuthenticatedClient(adminId, "Admin", companyId);

        /// <summary>
        /// Limpa todos os dados do banco (útil para isolar testes)
        /// </summary>
        public void ClearDatabase()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            db.Appointments.RemoveRange(db.Appointments);
            db.Services.RemoveRange(db.Services);
            db.Employees.RemoveRange(db.Employees);
            db.Clients.RemoveRange(db.Clients);
            db.Addresses.RemoveRange(db.Addresses);
            db.Companies.RemoveRange(db.Companies);
            db.SaveChanges();
        }

        private static string GetProjectPath()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            
            if (File.Exists(Path.Combine(currentDirectory, "VEA.API.csproj")))
                return currentDirectory;

            var directory = new DirectoryInfo(currentDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "VEA.API.csproj")))
                    return directory.FullName;
                    
                directory = directory.Parent;
            }

            var assemblyLocation = typeof(Program).Assembly.Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            
            directory = new DirectoryInfo(assemblyDirectory!);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "VEA.API.csproj")))
                    return directory.FullName;
                    
                directory = directory.Parent;
            }

            return @"C:\Projetos\VEA.API";
        }
    }

    // Alias para compatibilidade com código existente
    public class TestWebApplicationFactory : CustomWebApplicationFactory { }
}
