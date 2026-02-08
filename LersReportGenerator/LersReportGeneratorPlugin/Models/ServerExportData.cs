using System.Collections.Generic;

namespace LersReportGeneratorPlugin.Models
{
    /// <summary>
    /// Контейнер для экспорта/импорта серверов
    /// </summary>
    public class ServerExportData
    {
        /// <summary>
        /// Версия формата экспорта (для обратной совместимости)
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Флаг: содержит ли файл зашифрованные пароли
        /// </summary>
        public bool Encrypted { get; set; }

        /// <summary>
        /// Список экспортируемых серверов
        /// </summary>
        public List<ExportedServer> Servers { get; set; } = new List<ExportedServer>();
    }

    /// <summary>
    /// Экспортируемый сервер
    /// </summary>
    public class ExportedServer
    {
        /// <summary>
        /// Название сервера
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Физический адрес объекта
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// URL сервера
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Логин
        /// </summary>
        public string Login { get; set; }

        /// <summary>
        /// Пароль в открытом виде (если Encrypted = false и includePasswords = true)
        /// </summary>
        public string PlainPassword { get; set; }

        /// <summary>
        /// Зашифрованный пароль (если Encrypted = true)
        /// </summary>
        public string EncryptedPassword { get; set; }

        /// <summary>
        /// Salt для расшифровки (если Encrypted = true)
        /// </summary>
        public string Salt { get; set; }

        /// <summary>
        /// Вектор инициализации для расшифровки (если Encrypted = true)
        /// </summary>
        public string IV { get; set; }

        /// <summary>
        /// Использовать прокси-службу
        /// </summary>
        public bool UseProxy { get; set; }

        /// <summary>
        /// Игнорировать ошибки SSL
        /// </summary>
        public bool IgnoreSslErrors { get; set; }
    }
}
