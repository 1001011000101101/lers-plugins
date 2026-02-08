using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using LersReportCommon;
using LersReportGeneratorPlugin.Models;

namespace LersReportGeneratorPlugin.Services
{
    /// <summary>
    /// Service for managing application settings (server list)
    /// </summary>
    public class SettingsService
    {
        private static readonly Lazy<SettingsService> _instance =
            new Lazy<SettingsService>(() => new SettingsService());

        public static SettingsService Instance => _instance.Value;

        private readonly string _settingsPath;
        private readonly JavaScriptSerializer _serializer;
        private AppSettings _settings;

        private SettingsService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string pluginFolder = Path.Combine(appDataPath, "LersReportGeneratorPlugin");

            if (!Directory.Exists(pluginFolder))
            {
                Directory.CreateDirectory(pluginFolder);
            }

            _settingsPath = Path.Combine(pluginFolder, "servers.json");
            _serializer = new JavaScriptSerializer();

            Load();
        }

        /// <summary>
        /// Current application settings
        /// </summary>
        public AppSettings Settings => _settings;

        /// <summary>
        /// List of configured servers
        /// </summary>
        public List<ServerConfig> Servers => _settings.Servers;

        /// <summary>
        /// Load settings from disk
        /// </summary>
        public void Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    _settings = _serializer.Deserialize<AppSettings>(json);

                    if (_settings == null)
                    {
                        _settings = new AppSettings();
                    }

                    Logger.Info($"Settings loaded: {_settings.Servers.Count} servers");
                }
                else
                {
                    _settings = new AppSettings();
                    Logger.Info("No settings file, using defaults");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load settings: {ex.Message}");
                _settings = new AppSettings();
            }
        }

        /// <summary>
        /// Save settings to disk
        /// </summary>
        public void Save()
        {
            try
            {
                string json = _serializer.Serialize(_settings);

                // Format JSON for readability
                json = FormatJson(json);

                File.WriteAllText(_settingsPath, json);
                Logger.Info($"Settings saved: {_settings.Servers.Count} servers");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Add a new server
        /// </summary>
        public void AddServer(ServerConfig server)
        {
            if (server.Id == Guid.Empty)
            {
                server.Id = Guid.NewGuid();
            }

            // If this is the first server or marked as default, make it default
            if (_settings.Servers.Count == 0 || server.IsDefault)
            {
                foreach (var s in _settings.Servers)
                {
                    s.IsDefault = false;
                }
                server.IsDefault = true;
            }

            _settings.Servers.Add(server);
            Save();
        }

        /// <summary>
        /// Update an existing server
        /// </summary>
        public void UpdateServer(ServerConfig server)
        {
            var existing = _settings.Servers.FirstOrDefault(s => s.Id == server.Id);
            if (existing != null)
            {
                int index = _settings.Servers.IndexOf(existing);
                _settings.Servers[index] = server;

                // Handle default flag
                if (server.IsDefault)
                {
                    foreach (var s in _settings.Servers.Where(s => s.Id != server.Id))
                    {
                        s.IsDefault = false;
                    }
                }

                Save();
            }
        }

        /// <summary>
        /// Remove a server by ID
        /// </summary>
        public void RemoveServer(Guid serverId)
        {
            var server = _settings.Servers.FirstOrDefault(s => s.Id == serverId);
            if (server != null)
            {
                _settings.Servers.Remove(server);

                // If removed server was default, make another one default
                if (server.IsDefault && _settings.Servers.Count > 0)
                {
                    _settings.Servers[0].IsDefault = true;
                }

                // Clear last selected if it was removed
                if (_settings.LastSelectedServerId == serverId)
                {
                    _settings.LastSelectedServerId = null;
                }

                Save();
            }
        }

        /// <summary>
        /// Get the default server
        /// </summary>
        public ServerConfig GetDefaultServer()
        {
            return _settings.Servers.FirstOrDefault(s => s.IsDefault)
                ?? _settings.Servers.FirstOrDefault();
        }

        /// <summary>
        /// Get server by ID
        /// </summary>
        public ServerConfig GetServer(Guid id)
        {
            return _settings.Servers.FirstOrDefault(s => s.Id == id);
        }

        /// <summary>
        /// Set last selected server
        /// </summary>
        public void SetLastSelectedServer(Guid? serverId)
        {
            _settings.LastSelectedServerId = serverId;
            Save();
        }

        /// <summary>
        /// Get last selected server
        /// </summary>
        public ServerConfig GetLastSelectedServer()
        {
            if (_settings.LastSelectedServerId.HasValue)
            {
                return GetServer(_settings.LastSelectedServerId.Value);
            }
            return null;
        }

        /// <summary>
        /// Simple JSON formatting for readability
        /// </summary>
        private string FormatJson(string json)
        {
            // Basic indentation for readability
            var sb = new System.Text.StringBuilder();
            int indent = 0;
            bool inString = false;

            foreach (char c in json)
            {
                if (c == '"' && (sb.Length == 0 || sb[sb.Length - 1] != '\\'))
                {
                    inString = !inString;
                }

                if (!inString)
                {
                    switch (c)
                    {
                        case '{':
                        case '[':
                            sb.Append(c);
                            sb.AppendLine();
                            indent++;
                            sb.Append(new string(' ', indent * 2));
                            break;
                        case '}':
                        case ']':
                            sb.AppendLine();
                            indent--;
                            sb.Append(new string(' ', indent * 2));
                            sb.Append(c);
                            break;
                        case ',':
                            sb.Append(c);
                            sb.AppendLine();
                            sb.Append(new string(' ', indent * 2));
                            break;
                        case ':':
                            sb.Append(c);
                            sb.Append(' ');
                            break;
                        default:
                            sb.Append(c);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
