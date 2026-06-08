using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Server.Models;

namespace Server.Interfaces;

/// <summary>
/// Интерфейс для работы с JWT токенами.
/// Предоставляет методы для генерации, валидации и парсинга JWT токенов.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Генерирует короткий Access токен для аутентифицированного пользователя.
    /// </summary>
    /// <param name="subject">Идентификатор пользователя.</param>
    /// <param name="name">Имя пользователя.</param>
    /// <param name="surname">Фамилия пользователя.</param>
    /// <param name="email">Email пользователя.</param>
    /// <param name="groups">Группы пользователя.</param>
    /// <returns>JWT access токен.</returns>
    string GenerateAccessToken(string subject, string name, string surname, string email, string[] groups);

    /// <summary>
    /// Генерирует долгий Refresh токен для обновления Access токена.
    /// </summary>
    /// <param name="subject">Идентификатор пользователя.</param>
    /// <returns>JWT refresh токен.</returns>
    string GenerateRefreshToken(string subject, string name, string surname, string email, string[] groups);

    /// <summary>
    /// Проверяет валидность JWT токена (подпись, срок действия).
    /// </summary>
    /// <param name="token">JWT токен для валидации.</param>
    /// <returns>Токен или null, если токен не валиден.</returns>
    Task<JwtSecurityToken?> VerifyTokenAsync(string token);

    /// <summary>
    /// Принудительное аннулирование Access и Refresh токенов.
    /// </summary>
    /// <param name="jwtToken">JWT токен.</param>
    /// <returns>True, если токен успешно аннулирован, иначе false.</returns>
    Task<bool> RevokeTokenAsync(JwtSecurityToken jwtToken);

    /// <summary>
    /// Проверяет, аннулирован ли токен.
    /// </summary>
    /// <param name="Jwttoken">JWT токен.</param>
    /// <returns>True, если токен аннулирован, иначе false.</returns>
    Task<bool> IsTokenRevokedAsync(JwtSecurityToken jwtToken);

    /// <summary>
    /// Разбирает JWT токен.
    /// </summary>
    /// <param name="token">JWT токен.</param>
    /// <returns>JWT токен.</returns>
    JwtSecurityToken ParseToken(string token);
}