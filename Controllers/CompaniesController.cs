// VEA.API/Controllers/CompaniesController.cs
using Microsoft.AspNetCore.Mvc;
using VEA.API.Data;
using VEA.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging; // ESSA LINHA ERA O QUE FALTAVA
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BCrypt.Net;

[Route("api/[controller]")]
[ApiController]
public class CompaniesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CompaniesController> _logger;

    public CompaniesController(ApplicationDbContext context, IWebHostEnvironment env, ILogger<CompaniesController> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<CompanyDto>>>> GetCompanies([FromQuery] string? location = null)
    {
        var query = _context.Companies.Include(c => c.Address).AsQueryable();

        if (!string.IsNullOrEmpty(location))
        {
            query = query.Where(c => c.Address != null &&
                                     (c.Address.Logradouro.Contains(location) ||
                                      c.Address.Bairro.Contains(location) ||
                                      c.Address.Cidade.Contains(location)));
        }

        var companies = await query.ToListAsync();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var companyDtos = companies.Select(c => new CompanyDto
        {
            Id = c.Id,
            Name = c.Name,
            Email = c.Email,
            Phone = c.Phone,
            OperatingHours = c.OperatingHours,
            BusinessType = c.BusinessType,
            IsActive = c.IsActive,
            Logo = string.IsNullOrEmpty(c.Logo) ? "/uploads/logos/default-logo.png" : c.Logo,
            CoverImage = string.IsNullOrEmpty(c.CoverImage) ? "/uploads/covers/default-cover.png" : c.CoverImage,
            Cep = c.Address?.Cep,
            Logradouro = c.Address?.Logradouro,
            Numero = c.Address?.Numero,
            Complemento = c.Address?.Complemento,
            Bairro = c.Address?.Bairro,
            Cidade = c.Address?.Cidade,
            Uf = c.Address?.Uf
        }).ToList();

        foreach (var dto in companyDtos)
        {
            if (!dto.Logo.StartsWith("http"))
                dto.Logo = $"{baseUrl}{dto.Logo}";
            if (!string.IsNullOrEmpty(dto.CoverImage) && !dto.CoverImage.StartsWith("http"))
                dto.CoverImage = $"{baseUrl}{dto.CoverImage}";
        }

        return Ok(new ApiResponse<List<CompanyDto>> { Success = true, Data = companyDtos });
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Client")]
    public async Task<ActionResult<ApiResponse<CompanyDto>>> GetCompany(int id)
    {
        var company = await _context.Companies
            .Include(c => c.Address)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (company == null)
            return NotFound(new ApiResponse<CompanyDto> { Success = false, Message = "Empresa não encontrada" });

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var dto = new CompanyDto
        {
            Id = company.Id,
            Name = company.Name,
            Email = company.Email,
            Phone = company.Phone,
            OperatingHours = company.OperatingHours,
            BusinessType = company.BusinessType,
            IsActive = company.IsActive,
            Logo = string.IsNullOrEmpty(company.Logo) ? "/uploads/logos/default-logo.png" : company.Logo,
            CoverImage = string.IsNullOrEmpty(company.CoverImage) ? "/uploads/covers/default-cover.png" : company.CoverImage,
            Cep = company.Address?.Cep,
            Logradouro = company.Address?.Logradouro,
            Numero = company.Address?.Numero,
            Complemento = company.Address?.Complemento,
            Bairro = company.Address?.Bairro,
            Cidade = company.Address?.Cidade,
            Uf = company.Address?.Uf
        };

        if (!dto.Logo.StartsWith("http")) dto.Logo = $"{baseUrl}{dto.Logo}";
        if (!string.IsNullOrEmpty(dto.CoverImage) && !dto.CoverImage.StartsWith("http"))
            dto.CoverImage = $"{baseUrl}{dto.CoverImage}";

        return Ok(new ApiResponse<CompanyDto> { Success = true, Data = dto });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<Company>>> RegisterCompany([FromForm] CompanyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Phone) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new ApiResponse<Company> { Success = false, Message = "Nome, email, telefone e senha são obrigatórios." });

        if (await _context.Companies.AnyAsync(c => c.Email == dto.Email))
            return BadRequest(new ApiResponse<Company> { Success = false, Message = "E-mail já existe." });

        Address? address = null;
        if (!string.IsNullOrEmpty(dto.Cep) && !string.IsNullOrEmpty(dto.Logradouro) &&
            !string.IsNullOrEmpty(dto.Numero) && !string.IsNullOrEmpty(dto.Bairro) &&
            !string.IsNullOrEmpty(dto.Cidade) && !string.IsNullOrEmpty(dto.Uf))
        {
            if (!Regex.IsMatch(dto.Cep, @"^\d{5}-\d{3}$"))
                return BadRequest(new ApiResponse<Company> { Success = false, Message = "CEP inválido. Use formato 00000-000" });

            address = new Address
            {
                Cep = dto.Cep,
                Logradouro = dto.Logradouro,
                Numero = dto.Numero,
                Complemento = dto.Complemento,
                Bairro = dto.Bairro,
                Cidade = dto.Cidade,
                Uf = dto.Uf
            };
            _context.Addresses.Add(address);
            await _context.SaveChangesAsync();
        }

        var company = new Company
        {
            Name = dto.Name,
            Email = dto.Email,
            Phone = dto.Phone,
            OperatingHours = dto.OperatingHours ?? "09:00-18:00",
            BusinessType = dto.BusinessType,
            IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            AddressId = address?.Id
        };

        if (dto.LogoFile != null && dto.LogoFile.Length > 0)
            company.Logo = await SaveFileAsync(dto.LogoFile, "logos");
        else if (!string.IsNullOrEmpty(dto.Logo))
            company.Logo = dto.Logo;

        if (dto.CoverImageFile != null && dto.CoverImageFile.Length > 0)
            company.CoverImage = await SaveFileAsync(dto.CoverImageFile, "covers");
        else if (!string.IsNullOrEmpty(dto.CoverImage))
            company.CoverImage = dto.CoverImage;

        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        company.PasswordHash = null;
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        if (!string.IsNullOrEmpty(company.Logo) && !company.Logo.StartsWith("http"))
            company.Logo = $"{baseUrl}{company.Logo}";
        if (!string.IsNullOrEmpty(company.CoverImage) && !company.CoverImage.StartsWith("http"))
            company.CoverImage = $"{baseUrl}{company.CoverImage}";

        return CreatedAtAction(nameof(GetCompany), new { id = company.Id },
            new ApiResponse<Company> { Success = true, Data = company });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<Company>>> CreateCompany([FromBody] Company company)
    {
        if (await _context.Companies.AnyAsync(c => c.Email == company.Email))
            return BadRequest(new ApiResponse<Company> { Success = false, Message = "E-mail já existe" });

        company.PasswordHash = BCrypt.Net.BCrypt.HashPassword(company.PasswordHash ?? Guid.NewGuid().ToString());
        company.IsActive = true;

        _context.Companies.Add(company);
        await _context.SaveChangesAsync();

        company.PasswordHash = null;
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        if (!string.IsNullOrEmpty(company.Logo) && !company.Logo.StartsWith("http"))
            company.Logo = $"{baseUrl}{company.Logo}";
        if (!string.IsNullOrEmpty(company.CoverImage) && !company.CoverImage.StartsWith("http"))
            company.CoverImage = $"{baseUrl}{company.CoverImage}";

        return CreatedAtAction(nameof(GetCompany), new { id = company.Id },
            new ApiResponse<Company> { Success = true, Data = company });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateCompany(int id, [FromForm] CompanyDto dto)
    {
        var company = await _context.Companies
            .Include(c => c.Address)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (company == null)
            return NotFound(new ApiResponse<Company> { Success = false, Message = "Empresa não encontrada" });

        if (!string.IsNullOrEmpty(dto.Name)) company.Name = dto.Name;
        if (!string.IsNullOrEmpty(dto.Email))
        {
            if (await _context.Companies.AnyAsync(c => c.Email == dto.Email && c.Id != id))
                return BadRequest(new ApiResponse<Company> { Success = false, Message = "E-mail já existe" });
            company.Email = dto.Email;
        }
        if (!string.IsNullOrEmpty(dto.Phone)) company.Phone = dto.Phone;
        if (!string.IsNullOrEmpty(dto.OperatingHours)) company.OperatingHours = dto.OperatingHours;
        if (!string.IsNullOrEmpty(dto.BusinessType)) company.BusinessType = dto.BusinessType;

        bool hasFullAddress = !string.IsNullOrEmpty(dto.Cep) && !string.IsNullOrEmpty(dto.Logradouro) &&
                              !string.IsNullOrEmpty(dto.Numero) && !string.IsNullOrEmpty(dto.Bairro) &&
                              !string.IsNullOrEmpty(dto.Cidade) && !string.IsNullOrEmpty(dto.Uf);

        if (hasFullAddress)
        {
            if (!Regex.IsMatch(dto.Cep, @"^\d{5}-\d{3}$"))
                return BadRequest(new ApiResponse<Company> { Success = false, Message = "CEP inválido" });

            if (company.Address == null)
            {
                company.Address = new Address();
                _context.Addresses.Add(company.Address);
            }

            company.Address.Cep = dto.Cep;
            company.Address.Logradouro = dto.Logradouro;
            company.Address.Numero = dto.Numero;
            company.Address.Complemento = dto.Complemento;
            company.Address.Bairro = dto.Bairro;
            company.Address.Cidade = dto.Cidade;
            company.Address.Uf = dto.Uf;
        }

        if (dto.LogoFile != null && dto.LogoFile.Length > 0)
        {
            if (!string.IsNullOrEmpty(company.Logo))
                DeleteFile(company.Logo);
            company.Logo = await SaveFileAsync(dto.LogoFile, "logos");
        }
        else if (!string.IsNullOrEmpty(dto.Logo))
            company.Logo = dto.Logo;

        if (dto.CoverImageFile != null && dto.CoverImageFile.Length > 0)
        {
            if (!string.IsNullOrEmpty(company.CoverImage))
                DeleteFile(company.CoverImage);
            company.CoverImage = await SaveFileAsync(dto.CoverImageFile, "covers");
        }
        else if (!string.IsNullOrEmpty(dto.CoverImage))
            company.CoverImage = dto.CoverImage;

        if (!string.IsNullOrEmpty(dto.Password))
            company.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        await _context.SaveChangesAsync();

        company.PasswordHash = null;
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        if (!string.IsNullOrEmpty(company.Logo) && !company.Logo.StartsWith("http"))
            company.Logo = $"{baseUrl}{company.Logo}";
        if (!string.IsNullOrEmpty(company.CoverImage) && !company.CoverImage.StartsWith("http"))
            company.CoverImage = $"{baseUrl}{company.CoverImage}";

        return Ok(new ApiResponse<Company> { Success = true, Data = company });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCompany(int id)
    {
        var company = await _context.Companies
            .Include(c => c.Employees)
            .Include(c => c.Address)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (company == null)
            return NotFound(new ApiResponse<Company> { Success = false, Message = "Empresa não encontrada" });

        if (company.Employees.Any())
            return BadRequest(new ApiResponse<Company> { Success = false, Message = "Não é possível deletar: empresa possui funcionários" });

        var hasActiveAppointments = await _context.Appointments
            .AnyAsync(a => a.CompanyId == id && (a.Status == "Scheduled" || a.Status == "Pending"));

        if (hasActiveAppointments)
            return BadRequest(new ApiResponse<Company> { Success = false, Message = "Não é possível deletar: empresa possui agendamentos ativos" });

        _context.Companies.Remove(company);
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<Company> { Success = true, Message = "Empresa deletada com sucesso" });
    }

    private async Task<string> SaveFileAsync(IFormFile file, string subfolder)
    {
        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", subfolder);
        Directory.CreateDirectory(uploadsFolder);

        var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/{subfolder}/{fileName}";
    }

    private void DeleteFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        var fullPath = Path.Combine(_env.WebRootPath, filePath.TrimStart('/'));
        if (System.IO.File.Exists(fullPath))
            System.IO.File.Delete(fullPath);
    }
}