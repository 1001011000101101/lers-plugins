using System;
using LersReportCommon;

namespace LersReportGeneratorPlugin.Models
{
    /// <summary>
    /// Configuration for a remote LERS server
    /// </summary>
    public class ServerConfig
    {
        /// <summary>
        /// Unique identifier
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Display name (e.g. "Main Server", "Backup")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Physical address of the object (e.g. "Голышева 10", "Бованенко 3")
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// API base URL (e.g. "https://lers.company.ru/api")
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Login username
        /// </summary>
        public string Login { get; set; }

        /// <summary>
        /// Encrypted password (Base64 of DPAPI-encrypted bytes)
        /// </summary>
        public string EncryptedPassword { get; set; }

        /// <summary>
        /// Whether this is the default server
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Использовать прокси-службу вместо REST API
        /// </summary>
        public bool UseProxy { get; set; }

        /// <summary>
        /// Игнорировать ошибки SSL сертификата (для самоподписанных сертификатов)
        /// </summary>
        public bool IgnoreSslErrors { get; set; } = true;

        /// <summary>
        /// Last time this server was used
        /// </summary>
        public DateTime? LastUsed { get; set; }

        /// <summary>
        /// Cached auth token (not persisted, runtime only)
        /// </summary>
        [System.Web.Script.Serialization.ScriptIgnore]
        public string AuthToken { get; set; }

        /// <summary>
        /// Token expiration time (not persisted, runtime only)
        /// </summary>
        [System.Web.Script.Serialization.ScriptIgnore]
        public DateTime? TokenExpiration { get; set; }

        /// <summary>
        /// Check if we have a valid (non-expired) token
        /// </summary>
        public bool HasValidToken =>
            !string.IsNullOrEmpty(AuthToken) &&
            TokenExpiration.HasValue &&
            TokenExpiration.Value > DateTime.UtcNow;

        public override string ToString() => Name ?? Url ?? "Unknown Server";
    }

    /// <summary>
    /// Server type enumeration
    /// </summary>
    public enum ServerType
    {
        /// <summary>
        /// Local server (current LERS connection via Plugin API)
        /// </summary>
        Local,

        /// <summary>
        /// Remote server via HTTP API
        /// </summary>
        Remote
    }

    /// <summary>
    /// Application settings containing server list
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// List of configured remote servers
        /// </summary>
        public System.Collections.Generic.List<ServerConfig> Servers { get; set; }
            = new System.Collections.Generic.List<ServerConfig>();

        /// <summary>
        /// ID of the last selected server (null = local)
        /// </summary>
        public Guid? LastSelectedServerId { get; set; }

        /// <summary>
        /// Индекс колонки для сортировки в списке серверов (-1 = без сортировки)
        /// </summary>
        public int ServerListSortColumn { get; set; } = -1;

        /// <summary>
        /// Направление сортировки в списке серверов (0 = None, 1 = Ascending, 2 = Descending)
        /// </summary>
        public int ServerListSortOrder { get; set; } = 0;
    }
}
