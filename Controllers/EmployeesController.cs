using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using VEA.API.Services; // Pra IEmailService
using System;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.AspNetCore.Http;
using BCrypt.Net; // Adicionado para hash de senha
using VEA.API.Models.Dtos; // Pro EmployeeDto

// Novos DTOs pra ativação
public class ActivateEmployeeDto
{
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ActivationDataDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class EmployeesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _env;
    public EmployeesController(ApplicationDbContext context, IEmailService emailService, IWebHostEnvironment env)
    {
        _context = context;
        _emailService = emailService;
        _env = env;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<EmployeeDto>>>> GetEmployees(int companyId)
    {
        var employees = await _context.Employees
            .Include(e => e.Company)
            .Include(e => e.Role)
            .Where(e => e.CompanyId == companyId)
            .ToListAsync();
        var employeeDtos = employees.Select(e => new EmployeeDto
        {
            Id = e.Id,
            Name = e.Name,
            Email = e.Email,
            Phone = e.Phone,
            RoleId = e.RoleId ?? 0, // Fix CS0266: fallback se nullable
            EmailVerified = e.EmailVerified,
            RoleName = e.Role?.Name,
            FullPhotoUrl = BuildFullPhotoUrl(e.PhotoUrl ?? "/uploads/employees/default-avatar.png")
        }).ToList();
        return Ok(new ApiResponse<List<EmployeeDto>> { Success = true, Data = employeeDtos });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> GetEmployee(int id)
    {
        var employee = await _context.Employees
            .Include(e => e.Company)
            .Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (employee == null)
            return NotFound(new ApiResponse<EmployeeDto> { Success = false, Message = "Funcionário não encontrado" });
        var employeeDto = new EmployeeDto
        {
            Id = employee.Id,
            Name = employee.Name,
            Email = employee.Email,
            Phone = employee.Phone,
            RoleId = employee.RoleId ?? 0, // Fix CS0266
            EmailVerified = employee.EmailVerified,
            RoleName = employee.Role?.Name,
            FullPhotoUrl = BuildFullPhotoUrl(employee.PhotoUrl ?? "/uploads/employees/default-avatar.png")
        };
        return Ok(new ApiResponse<EmployeeDto> { Success = true, Data = employeeDto });
    }

    // DTO pra bindar os campos do Employee no FromForm
    public class CreateEmployeeDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public int RoleId { get; set; }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> CreateEmployee(
        [FromForm] CreateEmployeeDto employeeDto,
        IFormFile? photo)
    {
        var companyId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (await _context.Employees.AnyAsync(e => e.Email == employeeDto.Email && e.CompanyId == companyId))
            return BadRequest(new ApiResponse<EmployeeDto> { Success = false, Message = "E-mail já existe nesta empresa" });
        var employee = new Employee
        {
            CompanyId = companyId,
            Name = employeeDto.Name,
            Email = employeeDto.Email,
            Phone = employeeDto.Phone,
            RoleId = employeeDto.RoleId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("temp_password"), // Hash temporário para PasswordHash
            EmailVerified = false,
            PhotoUrl = "/uploads/employees/default-avatar.png" // Define a foto padrão por padrão
        };
        // Substitui a foto padrão por uma nova se enviada
        if (photo != null && photo.Length > 0)
        {
            var uploadResult = await UploadPhotoAsync(photo, null);
            if (!uploadResult.Success)
                return BadRequest(new ApiResponse<EmployeeDto> { Success = false, Message = uploadResult.Message });
            employee.PhotoUrl = uploadResult.PhotoUrl;
        }
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();
        // Envia verification email
        var frontendUrl = "http://localhost:4200";
        var inviteLink = $"{frontendUrl}/employee/activate/{employee.Id}?token={GenerateTempToken(employee.Email ?? "")}";
        var htmlBody = GenerateInviteHtml(inviteLink, employee.Name ?? "Funcionário");
        await _emailService.SendInviteEmail(employee.Email ?? "", "Convite VEA - Crie sua senha", htmlBody);
        // Recarrega com Include pra pegar RoleName
        var savedEmployee = await _context.Employees
            .Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.Id == employee.Id);
        // Mapeia pro DTO
        var createdDto = new EmployeeDto
        {
            Id = savedEmployee!.Id,
            Name = savedEmployee.Name,
            Email = savedEmployee.Email,
            Phone = savedEmployee.Phone,
            RoleId = savedEmployee.RoleId ?? 0, // Fix CS0266
            EmailVerified = savedEmployee.EmailVerified,
            RoleName = savedEmployee.Role?.Name,
            FullPhotoUrl = BuildFullPhotoUrl(savedEmployee.PhotoUrl ?? "/uploads/employees/default-avatar.png")
        };
        return CreatedAtAction(nameof(GetEmployee), new { id = employee.Id }, new ApiResponse<EmployeeDto> { Success = true, Data = createdDto });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> UpdateEmployee(int id, [FromForm] CreateEmployeeDto employeeDto, IFormFile? photo)
    {
        var existingEmployee = await _context.Employees
            .Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (existingEmployee == null) return NotFound(new ApiResponse<EmployeeDto> { Success = false, Message = "Funcionário não encontrado" });
        var companyId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (existingEmployee.CompanyId != companyId) return Forbid();
        // Atualiza campos apenas se o formulário for completo
        existingEmployee.Name = employeeDto.Name;
        existingEmployee.Email = employeeDto.Email;
        existingEmployee.Phone = employeeDto.Phone;
        existingEmployee.RoleId = employeeDto.RoleId;
        // Mantém a foto existente ou usa a padrão só se nunca houve foto
        if (string.IsNullOrEmpty(existingEmployee.PhotoUrl))
        {
            existingEmployee.PhotoUrl = "/uploads/employees/default-avatar.png";
        }
        // Substitui a foto (existente ou padrão) por uma nova se enviada
        if (photo != null && photo.Length > 0)
        {
            var oldPhotoPath = existingEmployee.PhotoUrl;
            var uploadResult = await UploadPhotoAsync(photo, existingEmployee.Id.ToString());
            if (!uploadResult.Success)
                return BadRequest(new ApiResponse<EmployeeDto> { Success = false, Message = uploadResult.Message });
            existingEmployee.PhotoUrl = uploadResult.PhotoUrl;
            // Deleta foto antiga se existir e não for a padrão
            if (!string.IsNullOrEmpty(oldPhotoPath) && oldPhotoPath != "/uploads/employees/default-avatar.png")
            {
                var oldFullPath = Path.Combine(_env.WebRootPath, oldPhotoPath.TrimStart('/'));
                if (System.IO.File.Exists(oldFullPath))
                {
                    System.IO.File.Delete(oldFullPath);
                }
            }
        }
        await _context.SaveChangesAsync();
        // Mapeia pro DTO (já tem Include)
        var updatedDto = new EmployeeDto
        {
            Id = existingEmployee.Id,
            Name = existingEmployee.Name,
            Email = existingEmployee.Email,
            Phone = existingEmployee.Phone,
            RoleId = existingEmployee.RoleId ?? 0, // Fix CS0266
            EmailVerified = existingEmployee.EmailVerified,
            RoleName = existingEmployee.Role?.Name,
            FullPhotoUrl = BuildFullPhotoUrl(existingEmployee.PhotoUrl ?? "/uploads/employees/default-avatar.png")
        };
        return Ok(new ApiResponse<EmployeeDto> { Success = true, Data = updatedDto });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEmployee(int id)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == id);
        if (employee == null) return NotFound(new ApiResponse<Employee> { Success = false, Message = "Funcionário não encontrado" });
        var companyId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (employee.CompanyId != companyId) return Forbid();
        bool hasPendingAppointments = await _context.Appointments
            .AnyAsync(a => a.EmployeeId == id && a.Status != "Cancelled");
        if (hasPendingAppointments)
            return BadRequest(new ApiResponse<Employee> { Success = false, Message = "Funcionário tem agendamentos pendentes" });
        // Deleta foto se existir e não for a padrão
        if (!string.IsNullOrEmpty(employee.PhotoUrl) && employee.PhotoUrl != "/uploads/employees/default-avatar.png")
        {
            var photoFullPath = Path.Combine(_env.WebRootPath, employee.PhotoUrl.TrimStart('/'));
            if (System.IO.File.Exists(photoFullPath))
            {
                System.IO.File.Delete(photoFullPath);
            }
        }
        _context.Employees.Remove(employee);
        await _context.SaveChangesAsync();
        return Ok(new ApiResponse<Employee> { Success = true, Message = "Funcionário deletado com sucesso" });
    }

    [HttpPost("{id}/verify-email")]
    public async Task<ActionResult<ApiResponse<object>>> SendVerificationEmail(int id)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id);
        if (employee == null || employee.EmailVerified) return BadRequest(new ApiResponse<object> { Success = false, Message = "Funcionário inválido ou já verificado" });
        var companyId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (employee.CompanyId != companyId) return Forbid();
        var frontendUrl = "http://localhost:4200";
        var inviteLink = $"{frontendUrl}/employee/activate/{employee.Id}?token={GenerateTempToken(employee.Email ?? "")}";
        var htmlBody = GenerateInviteHtml(inviteLink, employee.Name ?? "Funcionário");
        await _emailService.SendInviteEmail(employee.Email ?? "", "Reenvio: Convite VEA - Crie sua senha", htmlBody);
        return Ok(new ApiResponse<object> { Success = true, Message = "Email de verificação reenviado!" });
    }

    // NOVO: GET anônimo pra dados de ativação (valida token)
    [AllowAnonymous]
    [HttpGet("{id}/activation-data")]
    public async Task<ActionResult<ApiResponse<ActivationDataDto>>> GetActivationData(int id, [FromQuery] string token)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id && !e.EmailVerified);
        if (employee == null)
            return NotFound(new ApiResponse<ActivationDataDto> { Success = false, Message = "Conta não encontrada ou já ativada." });

        // Valida token simples (sem expiração por agora)
        if (!ValidateTempToken(employee.Email ?? "", token))
            return Unauthorized(new ApiResponse<ActivationDataDto> { Success = false, Message = "Token inválido. Verifique o link do e-mail." });

        var data = new ActivationDataDto
        {
            Id = employee.Id,
            Name = employee.Name,
            Email = employee.Email ?? ""
        };

        return Ok(new ApiResponse<ActivationDataDto> { Success = true, Data = data });
    }

    // NOVO: POST anônimo pra ativar com senha
    [AllowAnonymous]
    [HttpPost("{id}/activate")]
    public async Task<ActionResult<ApiResponse<EmployeeDto>>> ActivateEmployee(int id, [FromBody] ActivateEmployeeDto dto)
    {
        if (string.IsNullOrEmpty(dto.Password) || dto.Password.Length < 8)
            return BadRequest(new ApiResponse<EmployeeDto> { Success = false, Message = "Senha deve ter pelo menos 8 caracteres." });

        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id && !e.EmailVerified);
        if (employee == null)
            return NotFound(new ApiResponse<EmployeeDto> { Success = false, Message = "Conta não encontrada ou já ativada." });

        // Valida token (sem expiração por agora)
        if (!ValidateTempToken(employee.Email ?? "", dto.Token))
            return Unauthorized(new ApiResponse<EmployeeDto> { Success = false, Message = "Token inválido. Solicite um novo e-mail." });

        // Hash da senha e ativa
        employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        employee.EmailVerified = true;

        await _context.SaveChangesAsync();

        // Recarrega com Include pra DTO
        var activatedEmployee = await _context.Employees
            .Include(e => e.Role)
            .FirstOrDefaultAsync(e => e.Id == id);

        var activatedDto = new EmployeeDto
        {
            Id = activatedEmployee!.Id,
            Name = activatedEmployee.Name,
            Email = activatedEmployee.Email,
            Phone = activatedEmployee.Phone,
            RoleId = activatedEmployee.RoleId ?? 0,
            EmailVerified = activatedEmployee.EmailVerified,
            RoleName = activatedEmployee.Role?.Name,
            FullPhotoUrl = BuildFullPhotoUrl(activatedEmployee.PhotoUrl ?? "/uploads/employees/default-avatar.png")
        };

        return Ok(new ApiResponse<EmployeeDto> { Success = true, Data = activatedDto, Message = "Conta ativada com sucesso! Você pode fazer login." });
    }

    // Helper: Upload de foto ajustado pra pasta uploads/employees
    private async Task<(bool Success, string PhotoUrl, string Message)> UploadPhotoAsync(IFormFile photo, string? employeeId)
    {
        // Validações
        if (photo == null || photo.Length == 0) return (false, "", "Nenhum arquivo selecionado");
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return (false, "", "Apenas imagens JPG, PNG ou GIF são permitidas");
        if (photo.Length > 5 * 1024 * 1024) // 5MB
            return (false, "", "Arquivo muito grande (máx 5MB)");
        try
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "employees");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder); // Cria a pasta se não existir
                Console.WriteLine($"Pasta criada: {uploadsFolder}"); // Debug
            }
            // Nome único: employeeId_timestamp_extension (se employeeId null, usa "new")
            var fileName = string.IsNullOrEmpty(employeeId) ? $"new_{DateTime.Now.Ticks}{extension}" : $"{employeeId}_{DateTime.Now.Ticks}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            var relativeUrl = $"/uploads/employees/{fileName}"; // URL relativa pro front
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await photo.CopyToAsync(stream);
            }
            // Verifica se o arquivo foi salvo corretamente
            if (!System.IO.File.Exists(filePath))
                throw new IOException("Falha ao salvar o arquivo no disco.");
            return (true, relativeUrl, "Upload realizado com sucesso");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro no upload: {ex.Message}"); // Debug (substitua por logger se preferir)
            return (false, "", $"Erro no upload: {ex.Message}");
        }
    }

    // Helper: Constrói URL completa (dinâmica pra dev/prod)
    private string BuildFullPhotoUrl(string relativePath)
    {
        return $"{Request.Scheme}://{Request.Host}{relativePath}";
    }

    // Helpers copiados do Auth
    private string GenerateTempToken(string email) => Convert.ToBase64String(Encoding.UTF8.GetBytes(email ?? "" + " " + DateTime.Now.Ticks));

    // NOVO: Helper pra validar token (com comentário pra expiração)
    private bool ValidateTempToken(string email, string providedToken)
    {
        var expectedToken = GenerateTempToken(email);
        // Pra expiração de 24h: armazene o timestamp no DB ao criar, e compare aqui (ex: if (DateTime.Now - createdAt > TimeSpan.FromHours(24)) return false;)
        return expectedToken == providedToken;
    }

    private string GenerateInviteHtml(string inviteLink, string name)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; color: #333; line-height: 1.6; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 20px; border-radius: 10px; box-shadow: 0 0 10px rgba(0,0,0,0.1); text-align: center; }}
        .header {{ color: #DB2777; font-size: 24px; margin-bottom: 20px; }}
        .button {{ background: linear-gradient(to right, #DB2777, #F472B6); color: white !important; padding: 12px 24px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 20px auto; font-weight: bold; transition: opacity 0.3s; text-shadow: 0 1px 2px rgba(0,0,0,0.1); }}
        .button:hover {{ opacity: 0.9; }}
        .footer {{ color: #666; font-size: 12px; margin-top: 30px; border-top: 1px solid #eee; padding-top: 10px; text-align: center; }}
        .greeting {{ color: #DB2777; font-size: 18px; margin-bottom: 15px; }}
        .link-fallback {{ font-size: 14px; color: #666; text-align: center; margin: 10px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <h1 class='header'>Convite para o VEA!</h1>
        <p class='greeting'>Olá, <strong>{name}</strong>,</p>
        <p>Você foi convidado para a equipe no VEA - Veja, Explore e Agende, o sistema que automatiza agendamentos e centraliza a gestão de atendimentos. Ative sua conta para acessar sua agenda e serviços  – facilite o dia a dia da empresa com eficiência e visibilidade!</p>
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
}