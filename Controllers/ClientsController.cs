using Microsoft.AspNetCore.Mvc;
using VEA.API.Data;
using VEA.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks; // Para Task<>
using System.Collections.Generic; // Para IEnumerable<>
using System.Security.Claims; // Para ClaimTypes
using System.Linq; // Para AnyAsync

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class ClientsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public ClientsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<Client>>>> GetClients(int companyId)
    {
        var clients = await _context.Clients
            .Where(c => c.CompanyId == companyId) // Adicionado filtro por companyId
            .ToListAsync();
        return Ok(new ApiResponse<List<Client>> { Success = true, Data = clients });
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Client")]
    public async Task<ActionResult<ApiResponse<Client>>> GetClient(int id)
    {
        var client = await _context.Clients.FindAsync(id);
        if (client == null) return NotFound(new ApiResponse<Client> { Success = false, Message = "Cliente não encontrado" });
        return Ok(new ApiResponse<Client> { Success = true, Data = client });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> UpdateClient(int id, Client client)
    {
        if (id != client.Id) return BadRequest(new ApiResponse<Client> { Success = false, Message = "ID não corresponde" });
        var existingClient = await _context.Clients.FindAsync(id);
        if (existingClient == null) return NotFound(new ApiResponse<Client> { Success = false, Message = "Cliente não encontrado" });
        var companyId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (existingClient.CompanyId != companyId) return Forbid();
        _context.Entry(client).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return Ok(new ApiResponse<Client> { Success = true, Data = client });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> DeleteClient(int id)
    {
        var client = await _context.Clients.FindAsync(id);
        if (client == null) return NotFound(new ApiResponse<Client> { Success = false, Message = "Cliente não encontrado" });
        var companyId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (client.CompanyId != companyId) return Forbid();
        if (await _context.Appointments.AnyAsync(a => a.ClientId == id && a.Status != "Cancelled"))
            return BadRequest(new ApiResponse<Client> { Success = false, Message = "Não é possível deletar com agendamentos pendentes" });
        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();
        return Ok(new ApiResponse<Client> { Success = true, Message = "Cliente deletado com sucesso" });
    }
}