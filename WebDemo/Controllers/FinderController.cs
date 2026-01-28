using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using WebDemo.Data;
using WebDemo.Services;

namespace WebDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FinderController : Controller
    {
        private readonly AppDbContext _context;

        public FinderController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet()]
        public async Task<IActionResult> GetItems(string path)
        {
            var userId = Int16.Parse(User.FindFirst("uid").Value);

            var Folders = await _context.Folders
                .Where(f => f.OwnerId == userId && f.Path == path)
                .OrderByDescending(f => f.Name)
                .ToListAsync();
            var Files = await _context.Files
                .Where(f => f.OwnerId == userId && f.Path == path)
                .OrderByDescending(f => f.Name)
                .ToListAsync();

            return Ok(new {Folders, Files});
        }

        [HttpPost("new-file")]
        public async Task<IActionResult> NewFile([FromBody] Models.NewFile request)
        {
            //добавить проверку на существование папки
            string? origin = FileSystem.NewFile(request.Path, request.Name, User.FindFirstValue("name"));
            if (origin == null)
            {
                return BadRequest("Файл уже существует");
            }
            var userId = Int16.Parse(User.FindFirst("uid").Value);


            Models.File file = new Models.File
            {
                Origin = origin,
                OwnerId = userId,
                Path = request.Path,
                Name = request.Name,
                Size = request.Size,
            };
            await _context.Files.AddAsync(file);
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost("new-folder")]
        public async Task<IActionResult> NewFolder([FromBody] Models.NewFile request)
        {
            var userId = Int16.Parse(User.FindFirst("uid").Value);

            Models.Folder folder = new Models.Folder
            {
                OwnerId = userId,
                Path = request.Path,
                Name = request.Name,
            };
            await _context.Folders.AddAsync(folder);
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromForm] Models.UploadFile request)
        {
            if (request.Chunk == null || request.Chunk.Length == 0)
                return BadRequest("Чанк отсутствует.");
            if (await FileSystem.WriteChunk(request.Path, request.Chunk.FileName, User.FindFirstValue("name"), request.Chunk.OpenReadStream()))
            {
                return Ok();
            }
            return StatusCode(500);

        }

        [HttpGet("download")]
        public async Task<IActionResult> DownloadFile(string path, string fileName)
        {
            var userId = Int16.Parse(User.FindFirst("uid").Value);

            // 1. Проверяем в БД, принадлежит ли файл пользователю
            var fileRecord = await _context.Files
                .FirstOrDefaultAsync(f => f.OwnerId == userId && f.Path == path && f.Name == fileName);
            if (fileRecord == null) return NotFound("Файл не найден");
            var stream = System.IO.File.OpenRead(fileRecord.Origin);
            return File(stream, "application/octet-stream", fileName);
        }
    }
}
