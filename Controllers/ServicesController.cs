using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models; // Inclui Service e novo ServiceDto
using Microsoft.Extensions.Logging; // <<< NOVO: Pra ILogger

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class ServicesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ServicesController> _logger; // <<< NOVO: Injeta logger

    public ServicesController(ApplicationDbContext context, ILogger<ServicesController> logger) // <<< FIX: Adiciona logger no ctor
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // GET: Lista servi�os (use DTO pra leveza) - <<< FIX: Route pra /company/{companyId}
    [HttpGet("company/{companyId}")] // <<< FIX: Suporte � URL do erro, com [FromRoute]
    public async Task<ActionResult<ApiResponse<List<ServiceDto>>>> GetServices(int companyId, [FromQuery] bool includeInactive = false)
    {
        // <<< NOVO LOG: In�cio do m�todo
        _logger.LogInformation($"[DEBUG Services {nameof(GetServices)}] Request: CompanyId={companyId}, IncludeInactive={includeInactive}");

        // FIX: Use custom claim "companyId" (lowercase, como no Auth)
        if (!int.TryParse(User.FindFirst("companyId")?.Value ?? "0", out var userCompanyId)) // <<< FIX: lowercase + TryParse
        {
            userCompanyId = 0;
            _logger.LogWarning($"[DEBUG Services {nameof(GetServices)}] Invalid companyId claim: {User.FindFirst("companyId")?.Value}");
        }
        _logger.LogInformation($"[DEBUG Services {nameof(GetServices)}] User companyId from token={userCompanyId}, Requested={companyId}, Role={User.FindFirst("role")?.Value ?? "Unknown"}");

        if (companyId != userCompanyId)
        {
            // <<< NOVO LOG: No 403
            _logger.LogWarning($"[DEBUG Services {nameof(GetServices)}] Unauthorized: user companyId={userCompanyId} != requested {companyId}. User: {User.Identity?.Name ?? "Anonymous"}");
            return StatusCode(403, new ApiResponse<List<ServiceDto>> { Success = false, Message = "Acesso negado: empresa n�o autorizada" });
        }

        var query = _context.Services
          .Where(s => s.CompanyId == companyId);
        if (!includeInactive)
            query = query.Where(s => s.Active);
        var services = await query.Select(s => new ServiceDto
        {
            Id = s.Id,
            Name = s.Name ?? string.Empty,
            Description = s.Description,
            Duration = s.Duration,
            Price = s.Price,
            CompanyId = s.CompanyId,
            Active = s.Active,
            EmployeeIds = s.Employees.Select(e => e.Id).ToList()
        }).ToListAsync();

        _logger.LogInformation($"[DEBUG Services {nameof(GetServices)}] Success: {services.Count} services loaded for company {companyId}");
        return Ok(new ApiResponse<List<ServiceDto>> { Success = true, Data = services });
    }

    // GET: Detalhes (DTO com EmployeeIds)
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<ServiceDto>>> GetService(int id)
    {
        // <<< NOVO LOG: In�cio
        _logger.LogInformation($"[DEBUG Services {nameof(GetService)}] Request: ServiceId={id}");

        var service = await _context.Services
          .Include(s => s.Employees) // Include s� pra mapear IDs
          .FirstOrDefaultAsync(s => s.Id == id);
        if (service == null)
        {
            _logger.LogWarning($"[DEBUG Services {nameof(GetService)}] Service not found: {id}");
            return NotFound(new ApiResponse<ServiceDto> { Success = false, Message = "Servi�o n�o encontrado" });
        }

        // FIX: lowercase claim + TryParse
        if (!int.TryParse(User.FindFirst("companyId")?.Value ?? "0", out var userCompanyId))
        {
            userCompanyId = 0;
            _logger.LogWarning($"[DEBUG Services {nameof(GetService)}] Invalid companyId claim: {User.FindFirst("companyId")?.Value}");
        }
        _logger.LogInformation($"[DEBUG Services {nameof(GetService)}] User companyId={userCompanyId}, Service companyId={service.CompanyId}");

        if (service.CompanyId != userCompanyId)
        {
            // <<< NOVO LOG: No 403
            _logger.LogWarning($"[DEBUG Services {nameof(GetService)}] Unauthorized: service companyId={service.CompanyId} != user {userCompanyId}");
            return StatusCode(403, new ApiResponse<ServiceDto> { Success = false, Message = "Acesso negado: servi�o n�o autorizado" });
        }
        var dto = new ServiceDto
        {
            Id = service.Id,
            Name = service.Name ?? string.Empty,
            Description = service.Description,
            Duration = service.Duration,
            Price = service.Price,
            CompanyId = service.CompanyId,
            Active = service.Active,
            EmployeeIds = service.Employees.Select(e => e.Id).ToList()
        };
        _logger.LogInformation($"[DEBUG Services {nameof(GetService)}] Success: Service {id} loaded");
        return Ok(new ApiResponse<ServiceDto> { Success = true, Data = dto });
    }

    // POST: Cria (recebe model, retorna DTO)
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ServiceDto>>> CreateService([FromBody] Service service)
    {
        // <<< NOVO LOG: In�cio
        _logger.LogInformation($"[DEBUG Services {nameof(CreateService)}] Request: Name={service.Name}, CompanyId={service.CompanyId}");

        // FIX: lowercase claim + TryParse
        if (!int.TryParse(User.FindFirst("companyId")?.Value ?? "0", out var userCompanyId))
        {
            userCompanyId = 0;
            _logger.LogWarning($"[DEBUG Services {nameof(CreateService)}] Invalid companyId claim: {User.FindFirst("companyId")?.Value}");
        }

        if (service.CompanyId != userCompanyId)
        {
            // <<< NOVO LOG: No 403
            _logger.LogWarning($"[DEBUG Services {nameof(CreateService)}] Unauthorized: service companyId={service.CompanyId} != user {userCompanyId}");
            return StatusCode(403, new ApiResponse<ServiceDto> { Success = false, Message = "Acesso negado: empresa n�o autorizada" });
        }
        // Valida��es
        if (string.IsNullOrEmpty(service.Name))
            return BadRequest(new ApiResponse<ServiceDto> { Success = false, Message = "Nome � obrigat�rio" });
        if (service.Duration <= 0)
            return BadRequest(new ApiResponse<ServiceDto> { Success = false, Message = "Dura��o deve ser maior que 0 minutos" });
        if (service.Price < 0)
            return BadRequest(new ApiResponse<ServiceDto> { Success = false, Message = "Pre�o deve ser maior ou igual a 0" });
        service.Active = true;
        _context.Services.Add(service);
        try
        {
            await _context.SaveChangesAsync();
            // Recarrega e mapeia pra DTO
            var created = await _context.Services
              .Include(s => s.Employees)
              .FirstOrDefaultAsync(s => s.Id == service.Id);
            var dto = new ServiceDto
            {
                Id = created!.Id,
                Name = created.Name ?? string.Empty,
                Description = created.Description,
                Duration = created.Duration,
                Price = created.Price,
                CompanyId = created.CompanyId,
                Active = created.Active,
                EmployeeIds = created.Employees.Select(e => e.Id).ToList()
            };
            // <<< FIX: Mudei {companyId} pra {service.CompanyId} no log
            _logger.LogInformation($"[DEBUG Services {nameof(CreateService)}] Success: Created service {service.Id} for company {service.CompanyId}");
            return CreatedAtAction(nameof(GetService), new { id = service.Id }, new ApiResponse<ServiceDto> { Success = true, Data = dto });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, $"[DEBUG Services {nameof(CreateService)}] Db error creating service {service.Id}");
            return BadRequest(new ApiResponse<ServiceDto> { Success = false, Message = $"Erro ao salvar: {ex.Message}" });
        }
    }

    // PUT: Atualiza (recebe model, retorna DTO)
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateService(int id, [FromBody] Service service)
    {
        // <<< NOVO LOG: In�cio
        _logger.LogInformation($"[DEBUG Services {nameof(UpdateService)}] Request: ServiceId={id}, Name={service.Name ?? "unchanged"}");

        if (id != service.Id)
        {
            _logger.LogWarning($"[DEBUG Services {nameof(UpdateService)}] ID mismatch: path={id}, body={service.Id}");
            return BadRequest(new ApiResponse<ServiceDto> { Success = false, Message = "ID do servi�o n�o corresponde" });
        }
        var existingService = await _context.Services.Include(s => s.Employees).FirstOrDefaultAsync(s => s.Id == id);
        if (existingService == null)
        {
            _logger.LogWarning($"[DEBUG Services {nameof(UpdateService)}] Service not found: {id}");
            return NotFound(new ApiResponse<ServiceDto> { Success = false, Message = "Servi�o n�o encontrado" });
        }

        // FIX: lowercase claim + TryParse
        if (!int.TryParse(User.FindFirst("companyId")?.Value ?? "0", out var userCompanyId))
        {
            userCompanyId = 0;
            _logger.LogWarning($"[DEBUG Services {nameof(UpdateService)}] Invalid companyId claim: {User.FindFirst("companyId")?.Value}");
        }

        if (existingService.CompanyId != userCompanyId)
        {
            // <<< NOVO LOG: No 403
            _logger.LogWarning($"[DEBUG Services {nameof(UpdateService)}] Unauthorized: service companyId={existingService.CompanyId} != user {userCompanyId}");
            return StatusCode(403, new ApiResponse<ServiceDto> { Success = false, Message = "Acesso negado: servi�o n�o autorizado" });
        }
        // Atualiza
        existingService.Name = service.Name ?? existingService.Name;
        existingService.Description = service.Description ?? existingService.Description;
        existingService.Duration = service.Duration > 0 ? service.Duration : existingService.Duration;
        existingService.Price = service.Price >= 0 ? service.Price : existingService.Price;
        existingService.Active = service.Active;
        if (service.Employees != null)
        {
            existingService.Employees.Clear();
            foreach (var emp in service.Employees)
                existingService.Employees.Add(emp);
        }
        await _context.SaveChangesAsync();
        // Mapeia pra DTO
        var dto = new ServiceDto
        {
            Id = existingService.Id,
            Name = existingService.Name ?? string.Empty,
            Description = existingService.Description,
            Duration = existingService.Duration,
            Price = existingService.Price,
            CompanyId = existingService.CompanyId,
            Active = existingService.Active,
            EmployeeIds = existingService.Employees.Select(e => e.Id).ToList()
        };
        _logger.LogInformation("[DEBUG Services {Method}] Success: Updated service {Id}", nameof(UpdateService), id);
        return Ok(new ApiResponse<ServiceDto> { Success = true, Data = dto });
    }

    // PATCH: Toggle (retorna DTO)
    [HttpPatch("{id}/active")]
    public async Task<IActionResult> ToggleServiceActive(int id, [FromBody] bool active)
    {
        // <<< NOVO LOG: In�cio
        _logger.LogInformation($"[DEBUG Services {nameof(ToggleServiceActive)}] Request: ServiceId={id}, Active={active}");

        var service = await _context.Services.Include(s => s.Employees).FirstOrDefaultAsync(s => s.Id == id);
        if (service == null)
        {
            _logger.LogWarning($"[DEBUG Services {nameof(ToggleServiceActive)}] Service not found: {id}");
            return NotFound(new ApiResponse<ServiceDto> { Success = false, Message = "Servi�o n�o encontrado" });
        }

        // FIX: lowercase claim + TryParse
        if (!int.TryParse(User.FindFirst("companyId")?.Value ?? "0", out var userCompanyId))
        {
            userCompanyId = 0;
            _logger.LogWarning($"[DEBUG Services {nameof(ToggleServiceActive)}] Invalid companyId claim: {User.FindFirst("companyId")?.Value}");
        }

        if (service.CompanyId != userCompanyId)
        {
            // <<< NOVO LOG: No 403
            _logger.LogWarning($"[DEBUG Services {nameof(ToggleServiceActive)}] Unauthorized: service companyId={service.CompanyId} != user {userCompanyId}");
            return StatusCode(403, new ApiResponse<ServiceDto> { Success = false, Message = "Acesso negado: servi�o n�o autorizado" });
        }
        service.Active = active;
        await _context.SaveChangesAsync();
        var dto = new ServiceDto
        {
            Id = service.Id,
            Name = service.Name ?? string.Empty,
            Description = service.Description,
            Duration = service.Duration,
            Price = service.Price,
            CompanyId = service.CompanyId,
            Active = service.Active,
            EmployeeIds = service.Employees.Select(e => e.Id).ToList()
        };
        var message = active ? "Servi�o ativado com sucesso" : "Servi�o desativado (oculto do hub)";
        _logger.LogInformation($"[DEBUG Services {nameof(ToggleServiceActive)}] Success: Toggled service {id} to {active}");
        return Ok(new ApiResponse<ServiceDto> { Success = true, Data = dto, Message = message });
    }

    // DELETE: Sem DTO, s� message
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteService(int id)
    {
        // <<< NOVO LOG: In�cio
        _logger.LogInformation($"[DEBUG Services {nameof(DeleteService)}] Request: ServiceId={id}");

        var service = await _context.Services
          .Include(s => s.Appointments)
          .FirstOrDefaultAsync(s => s.Id == id);
        if (service == null)
        {
            _logger.LogWarning($"[DEBUG Services {nameof(DeleteService)}] Service not found: {id}");
            return NotFound(new ApiResponse<object> { Success = false, Message = "Servi�o n�o encontrado" });
        }

        // FIX: lowercase claim + TryParse
        if (!int.TryParse(User.FindFirst("companyId")?.Value ?? "0", out var userCompanyId))
        {
            userCompanyId = 0;
            _logger.LogWarning($"[DEBUG Services {nameof(DeleteService)}] Invalid companyId claim: {User.FindFirst("companyId")?.Value}");
        }

        if (service.CompanyId != userCompanyId)
        {
            // <<< NOVO LOG: No 403
            _logger.LogWarning($"[DEBUG Services {nameof(DeleteService)}] Unauthorized: service companyId={service.CompanyId} != user {userCompanyId}");
            return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Acesso negado: servi�o n�o autorizado" });
        }
        bool hasPendingAppointments = service.Appointments.Any(a => a.Status != "Cancelled");
        if (hasPendingAppointments)
        {
            _logger.LogWarning($"[DEBUG Services {nameof(DeleteService)}] Pending appointments for service {id}");
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Servi�o tem agendamentos pendentes. Cancele-os primeiro." });
        }
        _context.Services.Remove(service);
        await _context.SaveChangesAsync();
        _logger.LogInformation($"[DEBUG Services {nameof(DeleteService)}] Success: Deleted service {id}");
        return Ok(new ApiResponse<object> { Success = true, Message = "Servi�o deletado com sucesso" });
    }
}