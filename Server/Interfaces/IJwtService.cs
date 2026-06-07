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
    /// <param name="userId">UUID пользователя.</param>
    /// <param name="email">Email пользователя.</param>
    /// <returns>JWT токен с 30-минутным сроком действия.</returns>
    Task<string> GenerateAccessTokenAsync(UserJwt userJwt);

    /// <summary>
    /// Генерирует долгий Refresh токен для обновления Access токена.
    /// </summary>
    /// <returns>JWT токен.</returns>
    Task<string> GenerateRefreshTokenAsync(UserJwt userJwt);

    /// <summary>
    /// Проверяет валидность JWT токена (подпись, срок действия).
    /// </summary>
    /// <param name="token">JWT токен для валидации.</param>
    /// <param name="isRefreshToken">Признак, что проверяем Refresh токен.</param>
    /// <returns>Список claims из токена, если токен валиден, иначе null.</returns>
    Task<UserJwt?> VerifyTokenAsync(string token);

    /// <summary>
    /// Извлекает UserJwt из токена.
    /// </summary>
    /// <param name="token">JWT токен.</param>
    /// <returns>UserJwt объект или null, если не найден.</returns>
    UserJwt? ParceToken(string token);

    /// <summary>
    /// Принудительное аннулирование Access и Refresh токенов.
    /// </summary>
    /// <param name="token">JWT токен.</param>
    /// <returns>True, если токен успешно аннулирован, иначе false.</returns>
    Task<bool> RevokeTokenAsync(UserJwt userJwt);

    Task<bool> RevokeTokenAsync(string token);

    /// <summary>
    /// Проверяет, является ли токен в черном списке.
    /// </summary>
    /// <param name="token">JWT токен.</param>
    /// <returns>True, если токен в черном списке, иначе false.</returns>
    Task<bool> IsTokenBlacklistedAsync(UserJwt userJwt);

    /// <summary>
    /// Добавляет токен в черный список.
    /// </summary>
    /// <param name="token">JWT токен.</param>
    Task AddTokenToBlacklistAsync(UserJwt userJwt);
}