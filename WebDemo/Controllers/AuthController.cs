using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WebDemo.Data;
using WebDemo.Models;
using WebDemo.Services;

namespace WebDemo.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : Controller
    {
        private readonly IMemoryCache _cache;
        private readonly AppDbContext _context;
        private readonly TokenService _tokenService;

        public AuthController(IMemoryCache cache, AppDbContext context, TokenService tokenService)
        {
            _cache = cache;
            _context = context;
            _tokenService = tokenService;
        }

        [HttpGet("nonce/{userName}")]
        public IActionResult GetNonce(string userName)
        {
            // Генерируем случайную строку
            string nonce = Guid.NewGuid().ToString("N");
            int id = nonce.GetHashCode();
            // Сохраняем в кэш на 1 минуту специально для этого пользователя
            _cache.Set($"nonce_{id}", nonce + '|' + userName, TimeSpan.FromMinutes(1));

            return Ok(new { id, nonce });
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] ILogin request)
        {
            if (!_cache.TryGetValue($"nonce_{request.Id}", out string? storedNonce))
                return BadRequest("Nonce expired or invalid");

            string nonce = storedNonce.Split('|')[0];
            string username = storedNonce.Split('|')[1];

            _cache.Remove($"nonce_{request.Id}");

            var user = _context.Users.FirstOrDefault(u => u.UserName == username);
            if (user == null) return Unauthorized();

            string expectedHash = ComputeResponse(user.HashedKey, nonce);

            if (request.HashedKey == expectedHash)
            {
                return Ok(new { token = _tokenService.GenerateJwtToken(user.Id.ToString(), user.UserName, "User"), encodedKey = user.EncodedKey });
            }

            return Unauthorized();
        }
        
        private string ComputeResponse(string dbHash, string nonce)
        {
            using var sha = SHA256.Create();
            var combined = Encoding.UTF8.GetBytes(dbHash + nonce);
            return Convert.ToHexString(sha.ComputeHash(combined)).ToLower();
        }

        [HttpPost("signup")]
        public IActionResult Signup([FromBody] ISignup request)
        {
            if (_context.Users.Any(u => u.UserName == request.UserName))
            {
                return BadRequest("User already exists.");
            }

            User user = new User{
                UserName = request.UserName,
                HashedKey = request.HashedKey,
                EncodedKey = request.EncodedKey
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            return Ok();
        }
    }
}
