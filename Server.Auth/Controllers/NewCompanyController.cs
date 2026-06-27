using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using Common.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NanoidDotNet;
using Server.Auth.Interfaces;
using Server.Auth.Models;
using Server.Auth.Options;
using Server.Auth.Services;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace Server.Auth.Controllers;

/// <summary>
/// REST контроллер для создания новой компании через защищённое HTTPS-соединение.
/// </summary>
[ApiController]
public sealed class NewCompanyController : ControllerBase
{
    private const string GenericCreateErrorMessage = "Не удалось создать компанию.";
    private readonly ILogger<NewCompanyController> _logger;
    private readonly ISecurityService _securityService;
    private readonly SecurityOptions _securityOptions;
    private readonly CompanyManagerOptions _companyManagerOptions;
    private readonly DataGuardDbContext _dbContext;
    private readonly IDatabase _redis;

    public NewCompanyController(
        ILogger<NewCompanyController> logger,
        ISecurityService securityService,
        IOptions<SecurityOptions> securityOptions,
        IOptions<CompanyManagerOptions> companyManagerOptions,
        DataGuardDbContext dbContext,
        IConnectionMultiplexer redis)
    {
        _logger = logger;
        _securityService = securityService;
        _securityOptions = securityOptions.Value;
        _companyManagerOptions = companyManagerOptions.Value;
        _dbContext = dbContext;
        _redis = redis.GetDatabase().WithKeyPrefix("auth:");
    }

    /// <summary>
    /// Создаёт новую компанию и возвращает одноразовый регистрационный код владельца.
    /// </summary>
    /// <param name="request">Данные компании и мастер-ключ.</param>
    /// <returns>Результат создания компании с регистрационным кодом.</returns>
    [HttpPost("/new_company")]
    [RequireHttps]
    public async Task<ActionResult<NewCompanyResponse>> CreateCompanyAsync([FromBody] NewCompanyRequest request)
    {
        if (!Request.IsHttps)
        {
            _logger.LogWarning("Попытка создания компании без HTTPS, remote: {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
            return StatusCode(StatusCodes.Status403Forbidden, NewCompanyResponse.Fail(403, "Требуется HTTPS соединение."));
        }

        if (request is null)
        {
            return BadRequest(NewCompanyResponse.Fail(400, "Некорректный запрос."));
        }

        string companyName = request.CompanyName.Trim();
        string companyEmail = request.CompanyEmail.Trim();
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return BadRequest(NewCompanyResponse.Fail(400, "Введите название компании."));
        }

        if (string.IsNullOrWhiteSpace(companyEmail) || !MailAddress.TryCreate(companyEmail, out _))
        {
            return BadRequest(NewCompanyResponse.Fail(400, "Введите корректный email компании."));
        }

        if (string.IsNullOrWhiteSpace(request.MasterKey))
        {
            return BadRequest(NewCompanyResponse.Fail(400, "Введите мастер-ключ."));
        }

        byte[]? masterKeyHash = null;
        try
        {
            masterKeyHash = await _securityService.HashPasswordAsync(request.MasterKey, _securityOptions.MasterKeySalt);
            if (masterKeyHash.Length != _companyManagerOptions.MasterKeyHash.Length ||
                !CryptographicOperations.FixedTimeEquals(masterKeyHash, _companyManagerOptions.MasterKeyHash))
            {
                _logger.LogWarning("Невалидный мастер-ключ при создании компании, remote: {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
                return BadRequest(NewCompanyResponse.Fail(400, GenericCreateErrorMessage));
            }

            if (await _dbContext.Companies.AnyAsync(c => c.Name == companyName))
            {
                return Conflict(NewCompanyResponse.Fail(409, GenericCreateErrorMessage));
            }

            var company = new Company
            {
                Name = companyName
            };
            company.Groups.Add(new Group
            {
                Name = "system:owner",
                Description = "Owner group",
                IsActive = true,
                Company = company,
                CompanyId = company.CompanyId
            });

            var registrationData = new RegistrationData
            {
                CompanyId = company.CompanyId,
                Name = companyName,
                Surname = string.Empty,
                Email = companyEmail,
                Groups = company.Groups.Select(g => g.Id),
                AdminGroups = company.Groups.Select(g => g.Id)
            };

            string registrationCode = await Nanoid.GenerateAsync(Nanoid.Alphabets.UppercaseLettersAndDigits, 12);
            await _redis.StringSetAsync(registrationCode, JsonSerializer.Serialize(registrationData), TimeSpan.FromDays(30));
            await _dbContext.Companies.AddAsync(company);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Компания успешно создана через REST: {CompanyName}", companyName);
            return Ok(new NewCompanyResponse(200, "OK", registrationCode));
        }
        finally
        {
            if (masterKeyHash is not null)
            {
                CryptographicOperations.ZeroMemory(masterKeyHash);
            }
        }
    }
}

/// <summary>
/// Запрос на создание новой компании.
/// </summary>
/// <param name="CompanyName">Название компании.</param>
/// <param name="CompanyEmail">Email владельца компании.</param>
/// <param name="MasterKey">Мастер-ключ сервера.</param>
public sealed record NewCompanyRequest(string CompanyName, string CompanyEmail, string MasterKey);

/// <summary>
/// Ответ на запрос создания компании.
/// </summary>
/// <param name="Status">HTTP-совместимый статус операции.</param>
/// <param name="Message">Безопасное сообщение для пользователя.</param>
/// <param name="RegistrationCode">Регистрационный код владельца компании.</param>
public sealed record NewCompanyResponse(int Status, string Message, string? RegistrationCode)
{
    public static NewCompanyResponse Fail(int status, string message) => new(status, message, null);
}