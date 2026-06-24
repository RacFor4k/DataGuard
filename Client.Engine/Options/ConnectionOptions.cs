using System;

namespace Client.Engine.Options
{
    /// <summary>
    /// Настройки подключения GUI-клиента к локальному Engine.
    /// </summary>
    public class ConnectionOptions
    {
        /// <summary>
        /// Уникальный токен клиента (GUID), генерируется при первом запуске GUI.
        /// Используется для аутентификации локального подключения к Engine через gRPC.
        /// </summary>
        public Guid ClientToken { get; set; } = Guid.Empty;
    }
}