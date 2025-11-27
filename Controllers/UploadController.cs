using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Pra ILogger

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin,Employee")]
public class UploadController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<UploadController> _logger; // Pra debug

    public UploadController(IWebHostEnvironment env, ILogger<UploadController> logger)
    {
        _env = env;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> UploadImage(IFormFile file, [FromQuery] string type)
    {
        _logger.LogInformation("Upload iniciado: type={Type}, fileName={FileName}, size={Size}KB", type, file?.FileName, file?.Length / 1024);

        if (string.IsNullOrEmpty(type))
            return BadRequest(new { success = false, message = "type is required" });

        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "Arquivo inválido" });

        if (file.Length > 5 * 1024 * 1024) // 5MB
            return BadRequest(new { success = false, message = "Imagem muito grande (máx 5MB)" });

        if (!file.ContentType.StartsWith("image/"))
            return BadRequest(new { success = false, message = "Só imagens permitidas" });

        var validTypes = new[] { "logo", "cover", "employee_photo" };
        if (!validTypes.Contains(type))
            return BadRequest(new { success = false, message = "Tipo inválido" });

        var subfolder = type switch
        {
            "logo" or "cover" => "company",
            "employee_photo" => "employees",
            _ => "uploads"
        };

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", subfolder);
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName ?? "");
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var url = $"/uploads/{subfolder}/{uniqueFileName}";
        var baseUrl = $"{Request.Scheme}://{Request.Host}"; // Garante absoluta, ex: http://localhost:6365
        var fullUrl = $"{baseUrl}{url}";

        _logger.LogInformation("Upload sucesso: type={Type}, fullUrl={FullUrl}", type, fullUrl);

        // FIX: Retorna { url: fullUrl } minúsculo, pro map do frontend pegar response.url
        return Ok(new { url = fullUrl });
    }
}