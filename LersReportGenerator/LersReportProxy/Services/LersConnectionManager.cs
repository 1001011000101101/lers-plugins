using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security;
using System.Threading.Tasks;
using LersReportCommon;

// Метод LoginAsync использует reflection для вызова LERS API синхронно,
// но сохраняет async сигнатуру для совместимости с остальным кодом
#pragma warning disable CS1998 // Async method lacks 'await' operators

namespace LersReportProxy.Services
{
    /// <summary>
    /// Управление подключениями к ЛЭРС серверу.
    /// Кэширует подключения по токену сессии для повторного использования.
    /// </summary>
    public class LersConnectionManager : IDisposable
    {
        private readonly string _lersServerHost;
        private readonly ushort _lersServerPort;
        private readonly ConcurrentDictionary<string, LersSession> _sessions;
        private readonly TimeSpan _sessionTimeout = Constants.SessionTimeout;
        private bool _disposed;

        // Кэш типов для reflection (thread-safe)
        private static readonly object _typeLock = new object();
        private static volatile bool _typesLoaded = false;
        private static Type _lersServerType;
        private static Type _basicAuthInfoType;
        private static Type _secureStringHelperType;

        public LersConnectionManager(string lersServerHost, ushort lersServerPort = 10000)
        {
            _lersServerHost = lersServerHost;
            _lersServerPort = lersServerPort;
            _sessions = new ConcurrentDictionary<string, LersSession>();
        }

        /// <summary>
        /// Авторизация пользователя. Возвращает токен сессии.
        /// См. https://docs.lers.ru/fw/api/Lers.LersServer.html
        /// </summary>
        public async Task<LoginResult> LoginAsync(string login, string password)
        {
            try
            {
                Logger.Info($"Попытка авторизации: {login} на {_lersServerHost}:{_lersServerPort}");

                // Загружаем типы из Lers.System.dll
                if (!LoadLersTypes())
                {
                    return new LoginResult { Success = false, Error = "Не удалось загрузить типы ЛЭРС" };
                }

                // Создаём LersServer - ищем конструктор без параметров вручную
                Logger.Info("Ищем конструктор LersServer()...");
                ConstructorInfo serverCtor = null;
                foreach (var ctor in _lersServerType.GetConstructors())
                {
                    if (ctor.GetParameters().Length == 0)
                    {
                        serverCtor = ctor;
                        break;
                    }
                }
                if (serverCtor == null)
                {
                    Logger.Error("Не найден конструктор LersServer(). Доступные:");
                    foreach (var ctor in _lersServerType.GetConstructors())
                    {
                        var parms = ctor.GetParameters();
                        Logger.Info($"  ({string.Join(", ", Array.ConvertAll(parms, p => p.ParameterType.Name))})");
                    }
                    return new LoginResult { Success = false, Error = "Не найден конструктор LersServer" };
                }
                Logger.Info("Создаём LersServer...");
                var server = serverCtor.Invoke(null);
                Logger.Info("LersServer создан");

                // Создаём SecureString из пароля
                Logger.Info("Ищем ConvertToSecureString...");
                MethodInfo convertMethod = null;
                foreach (var m in _secureStringHelperType.GetMethods())
                {
                    if (m.Name == "ConvertToSecureString" && m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(string))
                    {
                        convertMethod = m;
                        break;
                    }
                }
                if (convertMethod == null)
                {
                    return new LoginResult { Success = false, Error = "Не найден метод ConvertToSecureString" };
                }
                Logger.Info("Вызываем ConvertToSecureString...");
                var securePassword = convertMethod.Invoke(null, new object[] { password });
                Logger.Info("SecureString создан");

                // Создаём BasicAuthenticationInfo(login, securePassword) - ищем конструктор вручную
                Logger.Info("Ищем конструктор BasicAuthenticationInfo...");
                ConstructorInfo authCtor = null;
                foreach (var ctor in _basicAuthInfoType.GetConstructors())
                {
                    var parms = ctor.GetParameters();
                    if (parms.Length == 2 &&
                        parms[0].ParameterType == typeof(string) &&
                        parms[1].ParameterType == typeof(SecureString))
                    {
                        authCtor = ctor;
                        break;
                    }
                }
                if (authCtor == null)
                {
                    Logger.Error("Не найден конструктор BasicAuthenticationInfo(string, SecureString). Доступные:");
                    foreach (var ctor in _basicAuthInfoType.GetConstructors())
                    {
                        var parms = ctor.GetParameters();
                        Logger.Info($"  ({string.Join(", ", Array.ConvertAll(parms, p => p.ParameterType.Name))})");
                    }
                    return new LoginResult { Success = false, Error = "Не найден конструктор BasicAuthenticationInfo" };
                }
                Logger.Info("Создаём BasicAuthenticationInfo...");
                var authInfo = authCtor.Invoke(new object[] { login, securePassword });
                Logger.Info("BasicAuthenticationInfo создан");

                // Ищем Connect(string, ushort, AuthenticationInfo) вручную
                Logger.Info("Ищем метод Connect...");
                MethodInfo connectMethod = null;
                foreach (var m in _lersServerType.GetMethods())
                {
                    if (m.Name != "Connect") continue;
                    var parms = m.GetParameters();
                    if (parms.Length == 3 &&
                        parms[0].ParameterType == typeof(string) &&
                        parms[1].ParameterType == typeof(ushort) &&
                        parms[2].ParameterType.IsAssignableFrom(_basicAuthInfoType))
                    {
                        connectMethod = m;
                        Logger.Info($"Найден Connect: {string.Join(", ", Array.ConvertAll(parms, p => p.ParameterType.Name))}");
                        break;
                    }
                }

                if (connectMethod == null)
                {
                    Logger.Error("Не найден подходящий метод Connect. Доступные перегрузки:");
                    foreach (var m in _lersServerType.GetMethods())
                    {
                        if (m.Name == "Connect")
                        {
                            var parms = m.GetParameters();
                            Logger.Info($"  Connect({string.Join(", ", Array.ConvertAll(parms, p => p.ParameterType.Name))})");
                        }
                    }
                    return new LoginResult { Success = false, Error = "Не найден подходящий метод Connect" };
                }

                Logger.Info($"Вызываем Connect({_lersServerHost}, {_lersServerPort}, authInfo)");
                connectMethod.Invoke(server, new object[] { _lersServerHost, _lersServerPort, authInfo });

                // Проверяем успешность подключения
                var isConnectedProp = _lersServerType.GetProperty("IsConnected");
                bool isConnected = isConnectedProp != null && (bool)isConnectedProp.GetValue(server);

                if (!isConnected)
                {
                    return new LoginResult { Success = false, Error = "Не удалось подключиться к серверу ЛЭРС" };
                }

                // Создаём токен сессии
                string sessionToken = Guid.NewGuid().ToString("N");

                // Сохраняем сессию
                var session = new LersSession
                {
                    Token = sessionToken,
                    Server = server,
                    Login = login,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessAt = DateTime.UtcNow
                };

                _sessions[sessionToken] = session;

                Logger.Info($"Авторизация успешна: {login}, токен: {sessionToken.Substring(0, 8)}...");

                return new LoginResult
                {
                    Success = true,
                    Token = sessionToken
                };
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message ?? ex.Message;
                Logger.Error($"Ошибка авторизации: {innerMessage}");
                return new LoginResult { Success = false, Error = innerMessage };
            }
        }

        private bool LoadLersTypes()
        {
            // Быстрая проверка без блокировки
            if (_typesLoaded)
                return true;

            lock (_typeLock)
            {
                // Повторная проверка внутри lock (double-checked locking)
                if (_typesLoaded)
                    return true;

                try
                {
                    // LersServer в Lers.System.dll
                    var systemAssembly = Assembly.Load("Lers.System");
                    var serverType = systemAssembly.GetType("Lers.LersServer");

                    if (serverType == null)
                    {
                        Logger.Error("Тип Lers.LersServer не найден в Lers.System.dll");
                        return false;
                    }

                    // BasicAuthenticationInfo и SecureStringHelper в Lers.Networking
                    var authType = systemAssembly.GetType("Lers.Networking.BasicAuthenticationInfo");
                    var helperType = systemAssembly.GetType("Lers.Networking.SecureStringHelper");

                    if (authType == null || helperType == null)
                    {
                        Logger.Error($"Не найдены типы: BasicAuthenticationInfo={authType != null}, SecureStringHelper={helperType != null}");
                        return false;
                    }

                    // Присваиваем только после успешной загрузки ВСЕХ типов
                    _lersServerType = serverType;
                    _basicAuthInfoType = authType;
                    _secureStringHelperType = helperType;
                    _typesLoaded = true;

                    Logger.Info("Типы ЛЭРС успешно загружены");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Ошибка загрузки типов ЛЭРС: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Получить сессию по токену
        /// </summary>
        public LersSession GetSession(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            if (_sessions.TryGetValue(token, out var session))
            {
                // Проверяем таймаут
                if (DateTime.UtcNow - session.LastAccessAt > _sessionTimeout)
                {
                    Logger.Info($"Сессия истекла: {token.Substring(0, 8)}...");
                    CloseSession(token);
                    return null;
                }

                session.LastAccessAt = DateTime.UtcNow;
                return session;
            }

            return null;
        }

        /// <summary>
        /// Закрыть сессию
        /// </summary>
        public void CloseSession(string token)
        {
            if (_sessions.TryRemove(token, out var session))
            {
                try
                {
                    // Отключаемся от сервера
                    var serverType = session.Server.GetType();
                    var disconnectMethod = serverType.GetMethod("Disconnect");
                    disconnectMethod?.Invoke(session.Server, null);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Ошибка при закрытии сессии: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var token in _sessions.Keys)
                {
                    CloseSession(token);
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Сессия подключения к ЛЭРС
    /// </summary>
    public class LersSession
    {
        public string Token { get; set; }
        public object Server { get; set; } // LersServer через reflection
        public string Login { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessAt { get; set; }
    }

    /// <summary>
    /// Результат авторизации
    /// </summary>
    public class LoginResult
    {
        public bool Success { get; set; }
        public string Token { get; set; }
        public string Error { get; set; }
    }
}
