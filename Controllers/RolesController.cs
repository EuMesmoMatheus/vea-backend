using Microsoft.AspNetCore.Mvc;
using VEA.API.Data;
using VEA.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System; // Adicionado: para Exception e outros tipos do System

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class RolesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public RolesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<Role>>>> GetRoles(int companyId)
    {
        var roles = await _context.Roles
            .Where(r => r.CompanyId == companyId)
            .ToListAsync();
        return Ok(new ApiResponse<List<Role>> { Success = true, Data = roles });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<Role>>> GetRole(int id)
    {
        var role = await _context.Roles.FindAsync(id);
        if (role == null) return NotFound(new ApiResponse<Role> { Success = false, Message = "Cargo não encontrado" });
        return Ok(new ApiResponse<Role> { Success = true, Data = role });
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Role>>> CreateRole([FromBody] Role role)
    {
        var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "companyId")?.Value;
        if (string.IsNullOrEmpty(companyIdClaim))
            return StatusCode(403, new ApiResponse<Role> { Success = false, Message = "ID da empresa não encontrado no token" });
        var companyId = int.Parse(companyIdClaim);
        role.CompanyId = companyId;
        if (await _context.Roles.AnyAsync(r => r.Name == role.Name && r.CompanyId == companyId))
            return BadRequest(new ApiResponse<Role> { Success = false, Message = "Cargo já existe" });
        _context.Roles.Add(role);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetRole), new { id = role.Id }, new ApiResponse<Role> { Success = true, Data = role });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] Role role)
    {
        if (id != role.Id) return BadRequest(new ApiResponse<Role> { Success = false, Message = "ID não corresponde" });
        var existingRole = await _context.Roles
            .Include(r => r.Employees)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (existingRole == null) return NotFound(new ApiResponse<Role> { Success = false, Message = "Cargo não encontrado" });

        var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "companyId")?.Value;
        if (string.IsNullOrEmpty(companyIdClaim))
            return StatusCode(403, new ApiResponse<Role> { Success = false, Message = "ID da empresa não encontrado no token" });
        var companyId = int.Parse(companyIdClaim);
        if (existingRole.CompanyId != companyId) return Forbid();

        // Validação de duplicata no update (excluindo si mesmo)
        var duplicate = await _context.Roles
            .AnyAsync(r => r.Id != id && r.Name == role.Name && r.CompanyId == companyId);
        if (duplicate)
            return BadRequest(new ApiResponse<Role> { Success = false, Message = "Já existe um cargo com esse nome!" });

        // Nova validação: não permitir inativar se tiver funcionários
        if (!role.Active && existingRole.Employees.Any())
            return BadRequest(new ApiResponse<Role> { Success = false, Message = "Não é possível inativar o cargo pois existem funcionários associados a ele." });

        existingRole.Name = role.Name;
        existingRole.Active = role.Active;
        try
        {
            await _context.SaveChangesAsync();
            return Ok(new ApiResponse<Role> { Success = true, Data = existingRole });
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.ToLower().Contains("unique") == true || ex.InnerException?.Message?.ToLower().Contains("duplicate") == true)
        {
            return BadRequest(new ApiResponse<Role> { Success = false, Message = "Já existe um cargo com esse nome!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<Role> { Success = false, Message = "Erro interno ao atualizar cargo: " + ex.Message });
        }
    }

    [HttpPatch("{id}/active")]
    public async Task<IActionResult> ToggleRoleActive(int id, [FromBody] bool active)
    {
        var role = await _context.Roles
            .Include(r => r.Employees)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound(new ApiResponse<Role> { Success = false, Message = "Cargo não encontrado" });

        var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "companyId")?.Value;
        if (string.IsNullOrEmpty(companyIdClaim))
            return StatusCode(403, new ApiResponse<Role> { Success = false, Message = "ID da empresa não encontrado no token" });
        var companyId = int.Parse(companyIdClaim);
        if (role.CompanyId != companyId) return Forbid();

        // Validação: não permitir inativar se tiver funcionários
        if (!active && role.Employees.Any())
            return BadRequest(new ApiResponse<Role> { Success = false, Message = "Não é possível inativar o cargo pois existem funcionários associados a ele." });

        role.Active = active;
        await _context.SaveChangesAsync();
        return Ok(new ApiResponse<Role> { Success = true, Data = role });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRole(int id)
    {
        var role = await _context.Roles
            .Include(r => r.Employees)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound(new ApiResponse<Role> { Success = false, Message = "Cargo não encontrado" });

        var companyIdClaim = User.Claims.FirstOrDefault(c => c.Type == "companyId")?.Value;
        if (string.IsNullOrEmpty(companyIdClaim))
            return StatusCode(403, new ApiResponse<Role> { Success = false, Message = "ID da empresa não encontrado no token" });
        var companyId = int.Parse(companyIdClaim);
        if (role.CompanyId != companyId) return Forbid();

        if (role.Employees.Any())
            return BadRequest(new ApiResponse<Role> { Success = false, Message = "Não é possível excluir o cargo pois existem funcionários associados a ele." });

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();
        return Ok(new ApiResponse<Role> { Success = true, Message = "Cargo deletado com sucesso" });
    }
}