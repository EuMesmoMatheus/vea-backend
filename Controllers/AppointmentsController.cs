using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using VEA.API.Models.Dtos;

namespace VEA.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AppointmentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AppointmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // DTO INTERNO PARA agenda-day
        // =========================================================
        private class AgendaEventDto
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public string Type { get; set; } = "appointment";
            public string? Title { get; set; }
            public string? ClientName { get; set; }
        }

        // =========================================================
        // ENDPOINTS PÚBLICOS (tudo igual)
        // =========================================================

        [HttpGet("services")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<ServiceDto>>>> GetServicesByCompany([FromQuery] int companyId)
        {
            var services = await _context.Services
                .Where(s => s.CompanyId == companyId && s.Active)
                .Select(s => new ServiceDto
                {
                    Id = s.Id,
                    Name = s.Name ?? string.Empty,
                    Description = s.Description,
                    Duration = s.Duration,
                    Price = s.Price,
                    CompanyId = s.CompanyId,
                    Active = s.Active
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<ServiceDto>> { Success = true, Data = services });
        }

        [HttpGet("employees")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<EmployeeDto>>>> GetEmployeesByService(
            [FromQuery] int companyId,
            [FromQuery] int serviceId = 0)
        {
            var employees = await _context.Employees
                .Include(e => e.Role)
                .Where(e => e.CompanyId == companyId && e.IsActive && e.EmailVerified)
                .Select(e => new EmployeeDto
                {
                    Id = e.Id,
                    Name = e.Name ?? string.Empty,
                    Email = e.Email ?? string.Empty,
                    Phone = e.Phone,
                    RoleName = e.Role != null ? e.Role.Name : "Profissional",
                    FullPhotoUrl = e.PhotoUrl,
                    EmailVerified = e.EmailVerified  // Adicionado para o frontend
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<EmployeeDto>> { Success = true, Data = employees });
        }

        [HttpGet("available-slots")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetAvailableSlots(
            [FromQuery] int companyId,
            [FromQuery] int employeeId,
            [FromQuery] string dateStr,
            [FromQuery] int[] serviceIds)
        {
            if (serviceIds == null || serviceIds.Length == 0)
                return BadRequest(new ApiResponse<List<string>> { Success = false, Message = "Selecione pelo menos um serviço" });

            if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return BadRequest(new ApiResponse<List<string>> { Success = false, Message = "Data inválida" });

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == employeeId && e.CompanyId == companyId && e.IsActive && e.EmailVerified);
            if (employee == null)
                return NotFound(new ApiResponse<List<string>> { Success = false, Message = "Profissional não encontrado" });

            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId && c.IsActive);
            if (company == null || string.IsNullOrWhiteSpace(company.OperatingHours))
                return BadRequest(new ApiResponse<List<string>> { Success = false, Message = "Horário de funcionamento não configurado" });

            var parts = company.OperatingHours.Trim().Split('-');
            if (parts.Length != 2 || !TimeSpan.TryParse(parts[0].Trim(), out var open) || !TimeSpan.TryParse(parts[1].Trim(), out var close))
                return BadRequest(new ApiResponse<List<string>> { Success = false, Message = "Formato de horário inválido (ex: 08:00-18:00)" });

            var dayStart = date.Date.Add(open);
            var dayEnd = date.Date.Add(close);

            var services = await _context.Services
                .Where(s => serviceIds.Contains(s.Id) && s.CompanyId == companyId && s.Active)
                .ToListAsync();

            if (services.Count != serviceIds.Length)
                return BadRequest(new ApiResponse<List<string>> { Success = false, Message = "Um ou mais serviços inválidos ou inativos" });

            var totalDuration = services.Sum(s => s.Duration);
            var possibleStarts = new List<DateTime>();
            for (var t = dayStart; t.AddMinutes(totalDuration) <= dayEnd; t = t.AddMinutes(15))
                possibleStarts.Add(t);

            var appointments = await _context.Appointments
                .Where(a => a.CompanyId == companyId && a.EmployeeId == employeeId && a.StartDateTime.Date == date.Date && a.Status != "Cancelled")
                .Select(a => new { Start = a.StartDateTime, End = a.EndDateTime ?? a.StartDateTime.AddMinutes(a.TotalDurationMinutes) })
                .ToListAsync();

            var blocks = await _context.EmployeeBlocks
                .Where(b => b.EmployeeId == employeeId && b.BlockDate == date.Date)
                .Select(b => new { Start = b.BlockDate + b.StartTime, End = b.BlockDate + b.EndTime })
                .ToListAsync();

            var occupied = appointments.Concat(blocks).ToList();

            bool HasConflict(DateTime start)
            {
                var end = start.AddMinutes(totalDuration);
                return occupied.Any(o => start < o.End && end > o.Start);
            }

            var available = possibleStarts.Where(t => !HasConflict(t)).Select(t => t.ToString("HH:mm")).ToList();

            return Ok(new ApiResponse<List<string>> { Success = true, Data = available });
        }

        [HttpGet("agenda-day")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAgendaDoDia([FromQuery] int companyId, [FromQuery] int employeeId, [FromQuery] string date)
        {
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var targetDate))
                return BadRequest(new ApiResponse<List<AgendaEventDto>> { Success = false, Message = "Data inválida" });

            var targetDateOnly = targetDate.Date;

            var appointments = await _context.Appointments
                .Include(a => a.Client)
                .Where(a => a.CompanyId == companyId && a.EmployeeId == employeeId && a.StartDateTime.Date == targetDateOnly && a.Status != "Cancelled")
                .Select(a => new AgendaEventDto
                {
                    Start = a.StartDateTime,
                    End = a.EndDateTime ?? a.StartDateTime.AddMinutes(a.TotalDurationMinutes),
                    Type = "appointment",
                    Title = "Agendamento",
                    ClientName = a.Client != null ? a.Client.Name : "Cliente"
                })
                .ToListAsync();

            var blocks = await _context.EmployeeBlocks
                .Where(b => b.EmployeeId == employeeId && b.BlockDate == targetDateOnly)
                .Select(b => new AgendaEventDto
                {
                    Start = b.BlockDate + b.StartTime,
                    End = b.BlockDate + b.EndTime,
                    Type = "block",
                    Title = "Bloqueado" + (!string.IsNullOrEmpty(b.Reason) ? $" - {b.Reason}" : "")
                })
                .ToListAsync();

            var allEvents = appointments.Concat(blocks).OrderBy(e => e.Start).ToList();

            return Ok(new ApiResponse<List<AgendaEventDto>> { Success = true, Data = allEvents });
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<Appointment>>> CreateAppointment([FromBody] CreateAppointmentDto dto)
        {
            var serviceIds = dto.ServiceIds?.Length > 0 ? dto.ServiceIds : (dto.ServiceId.HasValue ? new[] { dto.ServiceId.Value } : Array.Empty<int>());

            if (serviceIds.Length == 0)
                return BadRequest(new ApiResponse<Appointment> { Success = false, Message = "Selecione pelo menos um serviço" });

            var services = await _context.Services.Where(s => serviceIds.Contains(s.Id) && s.CompanyId == dto.CompanyId && s.Active).ToListAsync();
            if (services.Count != serviceIds.Length)
                return BadRequest(new ApiResponse<Appointment> { Success = false, Message = "Serviço inválido ou inativo" });

            var totalDuration = services.Sum(s => s.Duration);

            if (!await IsSlotAvailable(dto.CompanyId, dto.EmployeeId, dto.StartDateTime, totalDuration))
                return Conflict(new ApiResponse<Appointment> { Success = false, Message = "Horário já ocupado ou bloqueado" });

            var appointment = new Appointment
            {
                CompanyId = dto.CompanyId,
                EmployeeId = dto.EmployeeId,
                ClientId = dto.ClientId ?? 0,
                StartDateTime = dto.StartDateTime,
                EndDateTime = dto.StartDateTime.AddMinutes(totalDuration),
                Status = "Scheduled",
                ServicesJson = string.Join(",", serviceIds),
                TotalDurationMinutes = totalDuration
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            return Created("", new ApiResponse<Appointment>
            {
                Success = true,
                Data = appointment,
                Message = "Agendamento criado com sucesso!"
            });
        }

        private async Task<bool> IsSlotAvailable(int companyId, int employeeId, DateTime start, int durationMinutes)
        {
            var end = start.AddMinutes(durationMinutes);

            var hasConflict = await _context.Appointments.AnyAsync(a =>
                a.CompanyId == companyId &&
                a.EmployeeId == employeeId &&
                a.StartDateTime.Date == start.Date &&
                a.Status != "Cancelled" &&
                start < (a.EndDateTime ?? a.StartDateTime.AddMinutes(a.TotalDurationMinutes)) &&
                end > a.StartDateTime);

            if (hasConflict) return false;

            var hasBlock = await _context.EmployeeBlocks.AnyAsync(b =>
                b.EmployeeId == employeeId &&
                b.BlockDate == start.Date &&
                start < b.BlockDate + b.EndTime &&
                end > b.BlockDate + b.StartTime);

            return !hasBlock;
        }

        [HttpGet("week")]
        [Authorize(Roles = "Admin,Employee")]
        public async Task<ActionResult<ApiResponse<List<Appointment>>>> GetAppointmentsWeek([FromQuery] DateTime start, [FromQuery] DateTime end, [FromQuery] int companyId)
        {
            var userCompanyId = int.Parse(User.FindFirst("companyId")?.Value ?? "0");
            if (companyId == 0) companyId = userCompanyId;
            if (companyId != userCompanyId) return Forbid();

            var appointments = await _context.Appointments
                .Include(a => a.Client)
                .Include(a => a.Employee)
                .Where(a => a.CompanyId == companyId && a.StartDateTime >= start && a.StartDateTime <= end)
                .ToListAsync();

            return Ok(new ApiResponse<List<Appointment>> { Success = true, Data = appointments });
        }

        [HttpPatch("{id}/cancel")]
        [Authorize(Roles = "Admin,Employee,Client")]
        public async Task<IActionResult> CancelAppointment(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
                return NotFound(new ApiResponse<Appointment> { Success = false, Message = "Agendamento não encontrado" });

            // Pega role e IDs do usuário logado
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var userCompanyId = int.Parse(User.FindFirst("companyId")?.Value ?? "0");

            // Verifica permissão baseada no role
            bool canCancel = false;

            if (userRole == "Client")
            {
                // Cliente só pode cancelar seus próprios agendamentos
                canCancel = appointment.ClientId == userId;
            }
            else if (userRole == "Employee")
            {
                // Funcionário só pode cancelar agendamentos da sua empresa
                canCancel = appointment.CompanyId == userCompanyId;
            }
            else if (userRole == "Admin")
            {
                // Admin só pode cancelar agendamentos da sua empresa
                canCancel = appointment.CompanyId == userCompanyId;
            }

            if (!canCancel)
                return Forbid();

            appointment.Status = "Cancelled";
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<Appointment>
            {
                Success = true,
                Data = appointment,
                Message = "Agendamento cancelado com sucesso"
            });
        }

        // VERSÃO 100% FUNCIONAL SEM PRECISAR DE ServiceAppointment
        [HttpGet("my-appointments")]
        [Authorize(Roles = "Client")]
        public async Task<ActionResult<ApiResponse<List<ClientAppointmentDto>>>> GetMyAppointments([FromQuery] int? companyId = null)
        {
            var clientIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                               User.FindFirstValue("sub") ??
                               User.FindFirstValue("id");

            if (!int.TryParse(clientIdClaim, out var clientId) || clientId == 0)
                return Unauthorized(new ApiResponse<List<ClientAppointmentDto>> { Success = false, Message = "Usuário não autorizado" });

            var query = _context.Appointments
                .Include(a => a.Employee)
                .Where(a => a.ClientId == clientId && a.Status != "Cancelled");

            if (companyId.HasValue && companyId.Value > 0)
                query = query.Where(a => a.CompanyId == companyId.Value);

            var appointments = await query
                .OrderByDescending(a => a.StartDateTime)
                .ToListAsync();

            var result = new List<ClientAppointmentDto>();

            foreach (var a in appointments)
            {
                var serviceIds = string.IsNullOrWhiteSpace(a.ServicesJson)
                    ? Array.Empty<int>()
                    : a.ServicesJson.Split(',').Select(int.Parse).ToArray();

                ClientServiceDto? primaryService = null;
                var allServices = new List<ClientServiceDto>();

                if (serviceIds.Any())
                {
                    var services = await _context.Services
                        .Where(s => serviceIds.Contains(s.Id))
                        .Select(s => new ClientServiceDto { Id = s.Id, Name = s.Name ?? string.Empty })
                        .ToListAsync();

                    primaryService = services.FirstOrDefault();
                    allServices = services;
                }

                result.Add(new ClientAppointmentDto
                {
                    Id = a.Id,
                    StartDateTime = a.StartDateTime,
                    EndDateTime = a.EndDateTime ?? a.StartDateTime.AddMinutes(a.TotalDurationMinutes),
                    Status = a.Status,
                    Employee = a.Employee != null ? new ClientEmployeeDto { Id = a.Employee.Id, Name = a.Employee.Name ?? string.Empty } : null,
                    Service = primaryService,
                    Services = allServices
                });
            }

            return Ok(new ApiResponse<List<ClientAppointmentDto>>
            {
                Success = true,
                Data = result
            });
        }

        // =========================================================
        // DTOs DO CLIENTE (dentro do mesmo arquivo)
        // =========================================================

        public class ClientAppointmentDto
        {
            public int Id { get; set; }
            public DateTime StartDateTime { get; set; }
            public DateTime EndDateTime { get; set; }
            public string Status { get; set; } = string.Empty;
            public ClientEmployeeDto? Employee { get; set; }
            public ClientServiceDto? Service { get; set; }
            public List<ClientServiceDto> Services { get; set; } = new();
        }

        public class ClientEmployeeDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        public class ClientServiceDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        // =========================================================
        // ENDPOINTS DO PRESTADOR (EMPLOYEE) - AGENDAMENTOS
        // =========================================================

        /// <summary>
        /// Retorna os agendamentos do dia para o prestador logado
        /// GET /api/appointments/employee/today
        /// </summary>
        [HttpGet("employee/today")]
        [Authorize(Roles = "Employee")]
        public async Task<ActionResult<ApiResponse<EmployeeAgendaDto>>> GetEmployeeTodayAppointments()
        {
            var employeeId = GetEmployeeIdFromClaims();
            if (employeeId == 0)
                return Unauthorized(new ApiResponse<EmployeeAgendaDto> { Success = false, Message = "Funcionário não autorizado" });

            var today = DateTime.Today;
            var appointments = await GetEmployeeAppointmentsForDateRange(employeeId, today, today);

            return Ok(new ApiResponse<EmployeeAgendaDto>
            {
                Success = true,
                Data = new EmployeeAgendaDto
                {
                    Period = "today",
                    StartDate = today,
                    EndDate = today,
                    TotalAppointments = appointments.Count,
                    Appointments = appointments
                }
            });
        }

        /// <summary>
        /// Retorna os agendamentos da semana para o prestador logado
        /// GET /api/appointments/employee/week?date=2025-12-03 (opcional, default = hoje)
        /// </summary>
        [HttpGet("employee/week")]
        [Authorize(Roles = "Employee")]
        public async Task<ActionResult<ApiResponse<EmployeeAgendaDto>>> GetEmployeeWeekAppointments([FromQuery] string? date = null)
        {
            var employeeId = GetEmployeeIdFromClaims();
            if (employeeId == 0)
                return Unauthorized(new ApiResponse<EmployeeAgendaDto> { Success = false, Message = "Funcionário não autorizado" });

            DateTime targetDate = DateTime.Today;
            if (!string.IsNullOrEmpty(date) && DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                targetDate = parsedDate;
            }

            // Calcula início e fim da semana (domingo a sábado)
            var startOfWeek = targetDate.AddDays(-(int)targetDate.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(6);

            var appointments = await GetEmployeeAppointmentsForDateRange(employeeId, startOfWeek, endOfWeek);

            return Ok(new ApiResponse<EmployeeAgendaDto>
            {
                Success = true,
                Data = new EmployeeAgendaDto
                {
                    Period = "week",
                    StartDate = startOfWeek,
                    EndDate = endOfWeek,
                    TotalAppointments = appointments.Count,
                    Appointments = appointments
                }
            });
        }

        /// <summary>
        /// Retorna os agendamentos do mês para o prestador logado
        /// GET /api/appointments/employee/month?year=2025&month=12 (opcional, default = mês atual)
        /// </summary>
        [HttpGet("employee/month")]
        [Authorize(Roles = "Employee")]
        public async Task<ActionResult<ApiResponse<EmployeeAgendaDto>>> GetEmployeeMonthAppointments([FromQuery] int? year = null, [FromQuery] int? month = null)
        {
            var employeeId = GetEmployeeIdFromClaims();
            if (employeeId == 0)
                return Unauthorized(new ApiResponse<EmployeeAgendaDto> { Success = false, Message = "Funcionário não autorizado" });

            var targetYear = year ?? DateTime.Today.Year;
            var targetMonth = month ?? DateTime.Today.Month;

            var startOfMonth = new DateTime(targetYear, targetMonth, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            var appointments = await GetEmployeeAppointmentsForDateRange(employeeId, startOfMonth, endOfMonth);

            return Ok(new ApiResponse<EmployeeAgendaDto>
            {
                Success = true,
                Data = new EmployeeAgendaDto
                {
                    Period = "month",
                    StartDate = startOfMonth,
                    EndDate = endOfMonth,
                    TotalAppointments = appointments.Count,
                    Appointments = appointments
                }
            });
        }

        // =========================================================
        // HELPERS PARA EMPLOYEE
        // =========================================================

        private int GetEmployeeIdFromClaims()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                          User.FindFirstValue("sub") ??
                          User.FindFirstValue("id");

            return int.TryParse(idClaim, out var id) ? id : 0;
        }

        private async Task<List<EmployeeAppointmentDto>> GetEmployeeAppointmentsForDateRange(int employeeId, DateTime startDate, DateTime endDate)
        {
            var appointments = await _context.Appointments
                .Include(a => a.Client)
                .Where(a => a.EmployeeId == employeeId &&
                           a.StartDateTime.Date >= startDate.Date &&
                           a.StartDateTime.Date <= endDate.Date &&
                           a.Status != "Cancelled")
                .OrderBy(a => a.StartDateTime)
                .ToListAsync();

            var result = new List<EmployeeAppointmentDto>();

            foreach (var a in appointments)
            {
                var serviceIds = string.IsNullOrWhiteSpace(a.ServicesJson)
                    ? Array.Empty<int>()
                    : a.ServicesJson.Split(',').Select(int.Parse).ToArray();

                var services = serviceIds.Any()
                    ? await _context.Services.Where(s => serviceIds.Contains(s.Id)).Select(s => new ServiceInfoDto { Id = s.Id, Name = s.Name ?? string.Empty, Duration = s.Duration, Price = s.Price }).ToListAsync()
                    : new List<ServiceInfoDto>();

                result.Add(new EmployeeAppointmentDto
                {
                    Id = a.Id,
                    StartDateTime = a.StartDateTime,
                    EndDateTime = a.EndDateTime ?? a.StartDateTime.AddMinutes(a.TotalDurationMinutes),
                    Status = a.Status,
                    ClientName = a.Client?.Name ?? "Cliente",
                    ClientPhone = a.Client?.Phone,
                    Services = services,
                    TotalDurationMinutes = a.TotalDurationMinutes
                });
            }

            return result;
        }

        // =========================================================
        // DTOs DO PRESTADOR (EMPLOYEE)
        // =========================================================

        public class EmployeeAgendaDto
        {
            public string Period { get; set; } = string.Empty; // "today", "week", "month"
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public int TotalAppointments { get; set; }
            public List<EmployeeAppointmentDto> Appointments { get; set; } = new();
        }

        public class EmployeeAppointmentDto
        {
            public int Id { get; set; }
            public DateTime StartDateTime { get; set; }
            public DateTime EndDateTime { get; set; }
            public string Status { get; set; } = string.Empty;
            public string ClientName { get; set; } = string.Empty;
            public string? ClientPhone { get; set; }
            public List<ServiceInfoDto> Services { get; set; } = new();
            public int TotalDurationMinutes { get; set; }
        }

        public class ServiceInfoDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Duration { get; set; }
            public decimal Price { get; set; }
        }

    }
}