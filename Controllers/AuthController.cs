using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using VEA.API.Models; // Company, Client, Employee, Address, RegisterClientDto
using VEA.API.Data;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using VEA.API.Services; // IEmailService
using System.ComponentModel.DataAnnotations; // Validações
using System.Linq; // Para Contains
using System.Text.Json; // Pra JsonSerializer
using System.Text.Json.Serialization; // Pra JsonPropertyName
using Microsoft.AspNetCore.Hosting; // Pra IWebHostEnvironment
using Microsoft.AspNetCore.Http; // Pra IFormFile
using System.IO; // Pra FileStream e Path
using System.Text.RegularExpressions; // Pra validações regex
using System.Collections.Generic;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;
    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _env; // Pra salvar files
    public AuthController(ApplicationDbContext context, IConfiguration config, ILogger<AuthController> logger, IEmailService emailService, IWebHostEnvironment env)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }
    // Sub-model pra OperatingHours com mapping camelCase
    public class OperatingHours
    {
        [JsonPropertyName("startTime")]
        [Required]
        public string StartTime { get; set; } = string.Empty;
        [JsonPropertyName("endTime")]
        [Required]
        public string EndTime { get; set; } = string.Empty;
    }
    // Model pra bind address dos form fields (facilita validação)
    public class AddressForm
    {
        [Required(ErrorMessage = "CEP é obrigatório")]
        public string? Cep { get; set; }
        [Required(ErrorMessage = "Logradouro é obrigatório")]
        public string? Logradouro { get; set; }
        [Required(ErrorMessage = "Número é obrigatório")]
        public string? Numero { get; set; }
        public string? Complemento { get; set; } // Opcional
        [Required(ErrorMessage = "Bairro é obrigatório")]
        public string? Bairro { get; set; }
        [Required(ErrorMessage = "Cidade é obrigatória")]
        public string? Cidade { get; set; }
        [Required(ErrorMessage = "UF é obrigatória")]
        public string? Uf { get; set; }
    }
    // LoginModel embutido
    public class LoginModel
    {
        [Required(ErrorMessage = "E-mail é obrigatório")]
        [EmailAddress(ErrorMessage = "Formato de e-mail inválido")]
        public string? Email { get; set; }
        [Required(ErrorMessage = "Senha é obrigatória")]
        public string? Password { get; set; }
    }
    [HttpPost("register/company")]
    public async Task<IActionResult> RegisterCompany()
    {
        try
        {
            // Validações manuais básicas (inclui endereço novo, remove location)
            var name = Request.Form["name"].ToString();
            var email = Request.Form["email"].ToString();
            var phone = Request.Form["phone"].ToString();
            var password = Request.Form["password"].ToString(); // <<< FIX: Usa "password" (plain)
            var cep = Request.Form["cep"].ToString();
            var logradouro = Request.Form["logradouro"].ToString();
            var numero = Request.Form["numero"].ToString();
            var complemento = Request.Form["complemento"].ToString();
            var bairro = Request.Form["bairro"].ToString();
            var cidade = Request.Form["cidade"].ToString();
            var uf = Request.Form["uf"].ToString();
            var operatingHoursJson = Request.Form["operatingHours"].ToString();
            var businessType = Request.Form["businessType"].ToString();
            var logoFile = Request.Form.Files["logo"];
            var coverImageFile = Request.Form.Files["coverImage"];
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(password) || // <<< FIX: Checa password
                string.IsNullOrEmpty(cep) || string.IsNullOrEmpty(logradouro) || string.IsNullOrEmpty(numero) || string.IsNullOrEmpty(bairro) ||
                string.IsNullOrEmpty(cidade) || string.IsNullOrEmpty(uf) || string.IsNullOrEmpty(operatingHoursJson) || string.IsNullOrEmpty(businessType))
            {
                return BadRequest(new ApiResponse<Company> { Success = false, Message = "Campos obrigatórios vazios" });
            }
            // Validações específicas pro endereço
            if (!Regex.IsMatch(cep, @"^\d{5}-\d{3}$"))
            {
                return BadRequest(new ApiResponse<Company> { Success = false, Message = "Formato de CEP inválido: use 00000-000" });
            }
            if (!Regex.IsMatch(uf, @"^[A-Z]{2}$"))
            {
                return BadRequest(new ApiResponse<Company> { Success = false, Message = "UF inválida: use 2 letras maiúsculas (ex: SP)" });
            }
            if (logoFile == null || logoFile.Length == 0)
            {
                return BadRequest(new ApiResponse<Company> { Success = false, Message = "Logo é obrigatório para empresa" });
            }
            // Parse operatingHours
            OperatingHours? operatingHours = null;
            try
            {
                operatingHours = JsonSerializer.Deserialize<OperatingHours>(operatingHoursJson);
            }
            catch (Exception)
            {
                return BadRequest(new ApiResponse<Company> { Success = false, Message = "Horário inválido: " + operatingHoursJson });
            }
            if (operatingHours == null || string.IsNullOrEmpty(operatingHours.StartTime) || string.IsNullOrEmpty(operatingHours.EndTime))
            {
                return BadRequest(new ApiResponse<Company> { Success = false, Message = "Horário de funcionamento inválido" });
            }
            if (TimeSpan.TryParse(operatingHours.StartTime, out var start) && TimeSpan.TryParse(operatingHours.EndTime, out var end) && start >= end)
            {
                return BadRequest(new ApiResponse<Company> { Success = false, Message = "Horário inválido: fim deve ser após o início" });
            }
            // <<< DEBUG: ADICIONE AQUI >>>
            var allCompaniesEmails = await _context.Companies.Select(c => c.Email).ToListAsync();
            var allClientsEmails = await _context.Clients.Select(c => c.Email).ToListAsync();
            _logger.LogInformation("[DEBUG RegisterCompany] All Companies emails: [{Emails}] (Count: {Count})", 
                string.Join(", ", allCompaniesEmails), allCompaniesEmails.Count);
            _logger.LogInformation("[DEBUG RegisterCompany] All Clients emails: [{Emails}] (Count: {Count})", 
                string.Join(", ", allClientsEmails), allClientsEmails.Count);
            var emailNormalized = email?.Trim().ToLowerInvariant() ?? "";
            bool existsInCompanies = await _context.Companies.AnyAsync(c => c.Email != null && c.Email.ToLower() == emailNormalized);
            bool existsInClients = await _context.Clients.AnyAsync(c => c.Email != null && c.Email.ToLower() == emailNormalized);
            _logger.LogInformation("[DEBUG RegisterCompany] Email '{Email}' (normalized: '{Normalized}') EXISTS in Companies: {InCompanies} | in Clients: {InClients}", 
                email, emailNormalized, existsInCompanies, existsInClients);
            if (existsInCompanies || existsInClients)
            {
                return BadRequest(new ApiResponse<Company> { Success = false, Message = "E-mail já cadastrado (em Companies ou Clients)" });
            }
            // <<< FIM DO DEBUG >>>
            // Validações extras
            var validTypes = new[] { "Barbearia", "Estética", "Manicure", "Centro de Psicologia", "Clínica Médica", "Salão de Beleza", "Auto Escola" };
            if (!validTypes.Contains(businessType))
            {
                return BadRequest(new ApiResponse<Company> { Success = false, Message = "Tipo de negócio inválido. Opções: " + string.Join(", ", validTypes) });
            }
            // Hash senha (da plain text)
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            // Salva logo
            string logoPath = await SaveFileAsync(logoFile, "logos");
            // Salva cover (opcional)
            string? coverPath = null;
            if (coverImageFile != null && coverImageFile.Length > 0)
            {
                coverPath = await SaveFileAsync(coverImageFile, "covers");
            }
            // Normaliza e-mail para lowercase antes de salvar
            email = emailNormalized;
            // Cria e salva Company PRIMEIRO (sem AddressId ainda)
            var company = new Company
            {
                Name = name,
                Email = email,
                Phone = phone,
                PasswordHash = hashedPassword, // <<< FIX: Usa hashedPassword
                OperatingHours = JsonSerializer.Serialize(operatingHours),
                BusinessType = businessType,
                Logo = logoPath,
                CoverImage = coverPath,
                IsActive = false // Confirmação por email
            };
            _context.Companies.Add(company);
            await _context.SaveChangesAsync(); // Salva Company pra pegar Id real
            // Agora cria Address com o CompanyId real
            var address = new Address
            {
                Cep = cep,
                Logradouro = logradouro,
                Numero = numero,
                Complemento = complemento,
                Bairro = bairro,
                Cidade = cidade,
                Uf = uf,
                CompanyId = company.Id // Aqui linka o FK!
            };
            _context.Addresses.Add(address);
            await _context.SaveChangesAsync(); // Salva Address
            // Linka de volta na Company (AddressId) e salva final
            company.AddressId = address.Id;
            await _context.SaveChangesAsync(); // Update rápido na Company
            // Envia email confirmação com HTML bonito e link pro front (paleta rosa VEA)
            var frontendUrl = _config["AppSettings:FrontendBaseUrl"] ?? "https://vea-nine.vercel.app"; // Fallback dev
            var confirmLink = $"{frontendUrl}/confirm/company/{company.Id}?token={GenerateTempToken(company.Email!)}";
            var htmlBody = GenerateConfirmationHtml(confirmLink, company.Name ?? "Empresa", "empresa");
            await _emailService.SendConfirmationEmail(company.Email!, "Confirme sua conta VEA - Veja, Explore e Agende", htmlBody);
            // Retorna sem senha + role = "Admin", companyId = company.Id
            company.PasswordHash = null;
            // FIX: Torna paths absolutos antes de retornar
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            if (!string.IsNullOrEmpty(company.Logo) && !company.Logo.StartsWith("http"))
            {
                company.Logo = $"{baseUrl}{company.Logo}";
            }
            if (!string.IsNullOrEmpty(company.CoverImage) && !company.CoverImage.StartsWith("http"))
            {
                company.CoverImage = $"{baseUrl}{company.CoverImage}";
            }
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { user = new { id = company.Id, name = company.Name, email = company.Email, role = "Admin", businessType = company.BusinessType, companyId = company.Id } },
                Message = "Empresa cadastrada! Verifique o e-mail para ativar."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar empresa");
            return StatusCode(500, new ApiResponse<Company> { Success = false, Message = "Erro interno ao cadastrar empresa" });
        }
    }
    [HttpPost("register/client")]
    public async Task<IActionResult> RegisterClient([FromBody] RegisterClientDto dto) // <<< FIX: Usa DTO com Password
    {
        try
        {
            _logger.LogInformation("[DEBUG RegisterClient ENTRY] Received DTO: Name={Name}, Email={Email}, Password={Password}", dto?.Name, dto?.Email, dto?.Password); // <<< FIX: Log da plain password
            if (dto == null || string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Password)) // <<< FIX: Checa DTO.Password
                return BadRequest(new ApiResponse<Client> { Success = false, Message = "E-mail ou senha é obrigatória" });
            // <<< DEBUG: ADICIONE AQUI >>>
            var allCompaniesEmails = await _context.Companies.Select(c => c.Email ?? "NULL").ToListAsync(); // ?? pra nulls
            var allClientsEmails = await _context.Clients.Select(c => c.Email ?? "NULL").ToListAsync();
            _logger.LogInformation($"[DEBUG RegisterClient] All Companies emails: [{string.Join(", ", allCompaniesEmails)}] (Count: {allCompaniesEmails.Count})");
            _logger.LogInformation($"[DEBUG RegisterClient] All Clients emails: [{string.Join(", ", allClientsEmails)}] (Count: {allClientsEmails.Count})");
            var emailNormalized = dto.Email.Trim().ToLowerInvariant(); // Trim extra
            _logger.LogInformation($"[DEBUG RegisterClient] Original email: '{dto.Email}', Normalized: '{emailNormalized}'");
            bool existsInCompanies = await _context.Companies.AnyAsync(c => !string.IsNullOrEmpty(c.Email) && c.Email.ToLower() == emailNormalized); // <<< FIX: ToLower() + null check
            bool existsInClients = await _context.Clients.AnyAsync(c => !string.IsNullOrEmpty(c.Email) && c.Email.ToLower() == emailNormalized); // <<< FIX: ToLower() + null check
            _logger.LogInformation($"[DEBUG RegisterClient] Email '{dto.Email}' (normalized: '{emailNormalized}') EXISTS in Companies: {existsInCompanies} | in Clients: {existsInClients}");
            if (existsInCompanies || existsInClients)
            {
                _logger.LogWarning($"[DEBUG RegisterClient] Duplicate email detected: {emailNormalized}");
                return BadRequest(new ApiResponse<Client> { Success = false, Message = "E-mail já cadastrado (em Companies ou Clients)" });
            }
            // <<< FIM DO DEBUG >>>
            // Cria Client do DTO
            var client = new Client
            {
                Name = dto.Name,
                Email = emailNormalized, // Salva normalized
                Phone = dto.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password), // <<< FIX: Hash da plain text do DTO
                CompanyId = null, // Default pra clients
                IsActive = false
            };
            _logger.LogInformation("[DEBUG RegisterClient] Saving client with normalized email and hashed password");
            _context.Clients.Add(client);
            await _context.SaveChangesAsync();
            // Envia email com HTML e link pro front (paleta rosa VEA)
            var frontendUrl = _config["AppSettings:FrontendBaseUrl"] ?? "https://vea-nine.vercel.app";
            var confirmLink = $"{frontendUrl}/confirm/client/{client.Id}?token={GenerateTempToken(client.Email)}";
            var htmlBody = GenerateConfirmationHtml(confirmLink, client.Name ?? "Cliente", "cliente");
            _ = _emailService.SendConfirmationEmail(client.Email, "Confirme sua conta VEA - Veja, Explore e Agende", htmlBody);
            // Retorna sem senha + role = "Client", companyId = 0
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { user = new { id = client.Id, name = client.Name, email = client.Email, role = "Client", companyId = 0 } },
                Message = "Cliente cadastrado! Verifique o e-mail para ativar."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar cliente para email: {Email}", dto?.Email);
            return StatusCode(500, new ApiResponse<Client> { Success = false, Message = "Erro interno ao cadastrar cliente" });
        }
    }
    [HttpPost("register/employee")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RegisterEmployee(Employee employee)
    {
        try
        {
            if (employee == null || string.IsNullOrEmpty(employee.Email))
                return BadRequest(new ApiResponse<Employee> { Success = false, Message = "E-mail é obrigatório" });
            var companyId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            employee.CompanyId = companyId;
            var emailNormalized = employee.Email?.Trim().ToLowerInvariant() ?? "";
            bool existsInCompany = await _context.Employees.AnyAsync(e => e.Email != null && e.Email.ToLower() == emailNormalized && e.CompanyId == companyId);
            if (existsInCompany)
                return BadRequest(new ApiResponse<Employee> { Success = false, Message = "E-mail já existe na empresa" });
            // Normaliza e-mail para lowercase antes de salvar
            employee.Email = emailNormalized;
            employee.PasswordHash = null;
            employee.IsActive = false;
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();
            // Envia invite com HTML e link pro front (paleta rosa VEA)
            var frontendUrl = _config["AppSettings:FrontendBaseUrl"] ?? "https://vea-nine.vercel.app";
            var inviteLink = $"{frontendUrl}/employee/activate/{employee.Id}?token={GenerateTempToken(employee.Email!)}";
            var htmlBody = GenerateInviteHtml(inviteLink, employee.Name ?? "Funcionário");
            _ = _emailService.SendInviteEmail(employee.Email!, "Convite VEA - Crie sua senha", htmlBody);            // Adiciona role = "Employee", companyId = employee.CompanyId
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { user = new { id = employee.Id, name = employee.Name, email = employee.Email, role = "Employee", companyId = employee.CompanyId } },
                Message = "Funcionário cadastrado! Convite enviado."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar funcionário");
            return StatusCode(500, new ApiResponse<Employee> { Success = false, Message = "Erro interno ao cadastrar funcionário" });
        }
    }
    // MUDANÇA: Agora GET pra compatibilidade com link do email (Angular chama via HttpClient)
    [HttpGet("confirm/{type}/{id}")]
    public async Task<IActionResult> ConfirmAccount(string type, int id, [FromQuery] string token)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Token inválido" });
            // Validação melhorada do token (base64 de "email ticks", checa expiração ~24h)
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = decoded.Split(' ', 2);
                if (parts.Length != 2 || !long.TryParse(parts[1], out var ticks) || Math.Abs((DateTime.Now.Ticks - ticks)) > TimeSpan.FromHours(24).Ticks)
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Token expirado ou inválido" });
                var expectedEmail = parts[0];
                // Opcional: Checa se email bate com o user (adicione se quiser mais security)
            }
            catch
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Token inválido" });
            }
            if (type == "company")
            {
                var company = await _context.Companies.FindAsync(id);
                if (company != null && !company.IsActive)
                {
                    company.IsActive = true;
                    await _context.SaveChangesAsync();
                    return Ok(new ApiResponse<object> { Success = true, Message = "Conta ativada com sucesso! Você pode fazer login agora.", Data = new { type = "company" } });
                }
            }
            else if (type == "client")
            {
                var client = await _context.Clients.FindAsync(id);
                if (client != null && !client.IsActive)
                {
                    client.IsActive = true;
                    await _context.SaveChangesAsync();
                    return Ok(new ApiResponse<object> { Success = true, Message = "Conta ativada com sucesso! Você pode fazer login agora.", Data = new { type = "client" } });
                }
            }
            return NotFound(new ApiResponse<object> { Success = false, Message = "Conta não encontrada ou já ativa" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao confirmar conta {Type}/{Id}", type, id);
            return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Erro interno" });
        }
    }
    [HttpPost("employee/activate/{id}")]
    public async Task<IActionResult> ActivateEmployee(int id, [FromBody] string password)
    {
        try
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null || employee.IsActive) return BadRequest(new ApiResponse<Employee> { Success = false, Message = "Funcionário inválido ou já ativo" });
            if (string.IsNullOrEmpty(password)) return BadRequest(new ApiResponse<Employee> { Success = false, Message = "Senha obrigatória" });
            employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            employee.IsActive = true;
            await _context.SaveChangesAsync();
            return Ok(new ApiResponse<object> { Success = true, Message = "Conta ativada! Agora faça login." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ativar funcionário");
            return StatusCode(500, new ApiResponse<Employee> { Success = false, Message = "Erro interno" });
        }
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginModel login)
    {
        try
        {
            if (login == null || string.IsNullOrEmpty(login.Email) || string.IsNullOrEmpty(login.Password))
                return BadRequest(new ApiResponse<object> { Success = false, Message = "E-mail e senha são obrigatórios" });
            var key = _config["Jwt:Key"];
            var issuer = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
            {
                _logger.LogError("Configuração JWT inválida. Key={KeyExists}, Issuer={IssuerExists}, Audience={AudienceExists}", 
                    !string.IsNullOrEmpty(key), !string.IsNullOrEmpty(issuer), !string.IsNullOrEmpty(audience));
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Erro interno: Configuração JWT inválida." });
            }
            if (key.Length < 32)
            {
                _logger.LogError("JWT Key muito curta. Mínimo 32 caracteres, atual: {Length}", key.Length);
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Erro interno: Configuração JWT inválida (key muito curta)." });
            }
            var loginEmailLower = login.Email?.Trim().ToLowerInvariant() ?? "";
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == loginEmailLower);
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email != null && e.Email.ToLower() == loginEmailLower);
            var client = await _context.Clients.FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == loginEmailLower);
            if (company == null && employee == null && client == null)
            {
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "Usuário não encontrado." });
            }
            // Check IsActive corrigido (null-safe)
            if ((company != null && !company.IsActive) || (employee != null && !employee.IsActive) || (client != null && !client.IsActive))
            {
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "Conta inativa. Verifique seu e-mail." });
            }
            string passwordHash = string.Empty;
            string role = string.Empty;
            int userId = 0;
            string name = string.Empty;
            int companyId = 0; // CompanyId default 0
            if (company != null)
            {
                passwordHash = company.PasswordHash ?? string.Empty;
                role = "Admin";
                userId = company.Id;
                name = company.Name ?? string.Empty;
                companyId = company.Id; // Pro Admin (empresa)
                // <<< NOVO LOG: Debug pro companyId no login
                _logger.LogInformation($"[DEBUG Login Company] ID={company.Id}, IsActive={company.IsActive}, CompanyId in claims={companyId}, Role={role}, Email={loginEmailLower}");
            }
            else if (employee != null)
            {
                passwordHash = employee.PasswordHash ?? string.Empty;
                role = "Employee";
                userId = employee.Id;
                name = employee.Name ?? string.Empty;
                companyId = employee.CompanyId; // Da empresa
                _logger.LogInformation($"[DEBUG Login Employee] ID={employee.Id}, CompanyId={companyId}, Role={role}, Email={loginEmailLower}");
            }
            else if (client != null)
            {
                passwordHash = client.PasswordHash ?? string.Empty;
                role = "Client";
                userId = client.Id;
                name = client.Name ?? string.Empty;
                companyId = 0; // Não aplicável
                _logger.LogInformation($"[DEBUG Login Client] ID={client.Id}, CompanyId={companyId}, Role={role}, Email={loginEmailLower}");
            }
            if (string.IsNullOrEmpty(passwordHash) || !BCrypt.Net.BCrypt.Verify(login.Password, passwordHash))
            {
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "Senha incorreta." });
            }
            // Claims com custom "role" string pra jwtDecode ler decoded.role
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, login.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, role), // Mantém pra [Authorize(Roles)]
                new Claim("role", role), // Custom "role" pra decoded.role no front
                new Claim("companyId", companyId.ToString()) // Pro guard/filtragem se precisar
            };
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: creds);
            var jwtToken = new JwtSecurityTokenHandler().WriteToken(token);
            // Response.user com companyId
            // <<< NOVO LOG: Confirma token gerado
            _logger.LogInformation($"[DEBUG Login Success] Token gerado para UserId={userId}, Role={role}, CompanyId={companyId}, Expires={token.ValidTo}");
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new { token = jwtToken, user = new { id = userId, name, email = login.Email, role, companyId, canal = companyId } }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar login para e-mail: {Email}. Detalhes: {Message}", login.Email, ex.Message);
            // Em produção, retorna mensagem genérica mas loga o erro completo
            var errorDetail = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" 
                ? ex.Message 
                : "Erro interno ao processar login";
            return StatusCode(500, new ApiResponse<object> { Success = false, Message = errorDetail });
        }
    }
    // Check if email exists (GET /auth/check-email/{email})
    [HttpGet("check-email/{email}")]
    public async Task<IActionResult> CheckEmailExists(string email)
    {
        try
        {
            // <<< DEBUG: ADICIONE AQUI >>>
            var allCompaniesEmails = await _context.Companies.Select(c => c.Email ?? "NULL").ToListAsync(); // ?? pra nulls
            var allClientsEmails = await _context.Clients.Select(c => c.Email ?? "NULL").ToListAsync();
            _logger.LogInformation($"[DEBUG CheckEmail] All Companies emails: [{string.Join(", ", allCompaniesEmails)}]");
            _logger.LogInformation($"[DEBUG CheckEmail] All Clients emails: [{string.Join(", ", allClientsEmails)}]");
            var emailNormalized = email?.Trim().ToLowerInvariant();
            bool existsInCompanies = await _context.Companies.AnyAsync(c => !string.IsNullOrEmpty(c.Email) && c.Email.ToLower() == emailNormalized); // <<< FIX: ToLower() + null check
            bool existsInClients = await _context.Clients.AnyAsync(c => !string.IsNullOrEmpty(c.Email) && c.Email.ToLower() == emailNormalized); // <<< FIX: ToLower() + null check
            var exists = existsInCompanies || existsInClients;
            _logger.LogInformation($"[DEBUG CheckEmail] Email '{email}' EXISTS: {exists} (Companies: {existsInCompanies}, Clients: {existsInClients})");
            // <<< FIM DO DEBUG >>>
            return Ok(new ApiResponse<bool> { Success = true, Data = exists });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar e-mail: {Email}", email);
            return StatusCode(500, new ApiResponse<bool> { Success = false, Data = false });
        }
    }
    // Resend verification email (POST /auth/resend-verification)
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] string email)
    {
        try
        {
            if (string.IsNullOrEmpty(email)) return BadRequest(new ApiResponse<object> { Success = false, Message = "E-mail é obrigatório" });
            var emailLower = email.Trim().ToLowerInvariant();
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == emailLower && !c.IsActive);
            if (company == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Empresa não encontrada ou já ativa" });
            var frontendUrl = _config["AppSettings:FrontendBaseUrl"] ?? "https://vea-nine.vercel.app";
            var confirmLink = $"{frontendUrl}/confirm/company/{company.Id}?token={GenerateTempToken(company.Email!)}";
            var htmlBody = GenerateConfirmationHtml(confirmLink, company.Name ?? "Empresa", "empresa");
            _ = _emailService.SendConfirmationEmail(company.Email!, "Confirme sua conta VEA - Veja, Explore e Agende", htmlBody);
            return Ok(new ApiResponse<object> { Success = true, Message = "E-mail de confirmação reenviado! Verifique sua caixa de entrada." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reenviar email para {Email}", email);
            return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Erro ao reenviar e-mail" });
        }
    }
    private string GenerateTempToken(string email) => Convert.ToBase64String(Encoding.UTF8.GetBytes(email + " " + DateTime.Now.Ticks)); // Espaço pra split na validação
    // Helper pra gerar HTML de confirmação (refinado: texto botão branco !important, mensagem VEA-specific, mais centralizado)
    private string GenerateConfirmationHtml(string confirmLink, string name, string userType)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; color: #333; line-height: 1.6; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 20px; border-radius: 10px; box-shadow: 0 0 10px rgba(0,0,0,0.1); text-align: center; }} /* Centraliza tudo */
        .header {{ color: #DB2777; font-size: 24px; margin-bottom: 20px; }} /* Rosa VEA do PDF */
        .button {{ background: linear-gradient(to right, #DB2777, #F472B6); color: white !important; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 20px auto; font-weight: bold; transition: opacity 0.3s; text-shadow: 0 1px 2px rgba(0,0,0,0.1); }} /* Branco forçado, sombra pra contraste */
        .button:hover {{ opacity: 0.9; }}
        .footer {{ color: #666; font-size: 12px; margin-top: 30px; border-top: 1px solid #eee; padding-top: 10px; text-align: center; }} /* Centraliza footer */
        .greeting {{ color: #DB2777; font-size: 18px; margin-bottom: 15px; }} /* Rosa no greeting */
        .link-fallback {{ font-size: 14px; color: #666; text-align: center; margin: 10px 0; }} /* Centraliza link fallback */
    </style>
</head>
<body>
    <div class='container'>
        <h1 class='header'>Bem-vindo ao VEA!</h1>
        <p class='greeting'>Olá, <strong>{name}</strong>,</p>
        <p>Obrigado por se cadastrar no VEA - Veja, Explore e Agende, o hub empresarial que otimiza agendamentos e conecta PMEs a clientes. Ative sua {userType} agora para gerenciar atendimentos automáticos, funcionários e serviços de forma intuitiva – ganhe visibilidade e reduza burocracias no seu dia a dia!</p>
        <a href='{confirmLink}' class='button'>Ativar Conta VEA</a>
        <p class='link-fallback'>Se o botão não funcionar, copie e cole este link no navegador:<br><small>{confirmLink}</small></p>
        <p>Essa ativação expira em 24h. Qualquer dúvida, responda este email!</p>
        <div class='footer'>
            <p>Atenciosamente,<br>Equipe VEA - Veja, Explore e Agende</p>
            <img src='https://via.placeholder.com/100x50/DB2777/FFFFFF?text=VEA' alt='Logo VEA' style='width: 100px; border-radius: 5px;'>
        </div>
    </div>
</body>
</html>";
    }
    // Helper pra gerar HTML de invite (similar, refinado igual acima)
    private string GenerateInviteHtml(string inviteLink, string name)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; color: #333; line-height: 1.6; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 20px; border-radius: 10px; box-shadow: 0 0 10px rgba(0,0,0,0.1); text-align: center; }} /* Centraliza tudo */
        .header {{ color: #DB2777; font-size: 24px; margin-bottom: 20px; }} /* Rosa VEA do PDF */
        .button {{ background: linear-gradient(to right, #DB2777, #F472B6); color: white !important; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 20px auto; font-weight: bold; transition: opacity 0.3s; text-shadow: 0 1px 2px rgba(0,0,0,0.1); }} /* Branco forçado, sombra pra contraste */
        .button:hover {{ opacity: 0.9; }}
        .footer {{ color: #666; font-size: 12px; margin-top: 30px; border-top: 1px solid #eee; padding-top: 10px; text-align: center; }} /* Centraliza footer */
        .greeting {{ color: #DB2777; font-size: 18px; margin-bottom: 15px; }} /* Rosa no greeting */
        .link-fallback {{ font-size: 14px; color: #666; text-align: center; margin: 10px 0; }} /* Centraliza link fallback */
    </style>
</head>
<body>
    <div class='container'>
        <h1 class='header'>Convite para o VEA!</h1>
        <p class='greeting'>Olá, <strong>{name}</strong>,</p>
        <p>Você foi convidado para a equipe no VEA - Veja, Explore e Agende, o sistema que automatiza agendamentos e centraliza a gestão de atendimentos. Ative sua conta para acessar sua agenda, serviços e o hub empresarial – facilite o dia a dia da empresa com eficiência e visibilidade!</p>
        <a href='{inviteLink}' class='button'>Ativar e Criar Senha</a>
        <p class='link-fallback'>Se o botão não funcionar, copie e cole este link no navegador:<br><small>{inviteLink}</small></p>
        <p>Essa ativação expira em 24h. Qualquer dúvida, responda este email!</p>
        <div class='footer'>
            <p>Atenciosamente,<br>Equipe VEA - Veja, Explore e Agende</p>
            <img src='https://via.placeholder.com/100x50/DB2777/FFFFFF?text=VEA' alt='Logo VEA' style='width: 100px; border-radius: 5px;'>
        </div>
    </div>
</body>
</html>";
    }
    // Helper pra salvar file
    private async Task<string> SaveFileAsync(IFormFile file, string subfolder)
    {
        if (file == null || file.Length == 0) throw new ArgumentException("Arquivo inválido", nameof(file));
        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", subfolder);
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);
        var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName ?? "");
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
        return $"/uploads/{subfolder}/{uniqueFileName}";
    }
}