using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using LersReportCommon;
using LersReportProxy.Services;

namespace LersReportProxy.Http.Handlers
{
    /// <summary>
    /// Обработчик запросов узлов (домов)
    /// </summary>
    public class NodesHandler
    {
        private readonly LersConnectionManager _connectionManager;

        public NodesHandler(LersConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        /// <summary>
        /// GET /proxy/nodes?type=House
        /// Получить список узлов
        /// </summary>
        public async Task GetListAsync(HttpListenerContext context, LersSession session)
        {
            try
            {
                var query = context.Request.QueryString;
                string nodeType = query["type"]; // House, Node, PowerSource

                var server = session.Server;
                var serverType = server.GetType();

                // Получаем Nodes через reflection
                var nodesProp = serverType.GetProperty("Nodes");
                var nodesManager = nodesProp?.GetValue(server);
                if (nodesManager == null)
                {
                    await RequestRouter.SendJsonAsync(context, 500, new { error = "Nodes manager not found" });
                    return;
                }

                // Вызываем GetListAsync()
                var getListMethod = nodesManager.GetType().GetMethod("GetListAsync", Type.EmptyTypes);
                var task = getListMethod?.Invoke(nodesManager, null) as Task;
                if (task == null)
                {
                    await RequestRouter.SendJsonAsync(context, 500, new { error = "GetListAsync method not found" });
                    return;
                }

                await task;

                var resultProperty = task.GetType().GetProperty("Result");
                var nodes = resultProperty?.GetValue(task) as IEnumerable;
                if (nodes == null)
                {
                    await RequestRouter.SendJsonAsync(context, 200, new { nodes = new object[0] });
                    return;
                }

                var result = new List<object>();
                int totalCount = 0;
                int filteredCount = 0;

                foreach (var node in nodes)
                {
                    totalCount++;
                    var nodeTypeValue = ReflectionHelper.GetPropertyValue<object>(node, "Type");
                    string nodeTypeStr = nodeTypeValue?.ToString();

                    // Фильтр по типу узла (свойство Type, не NodeType)
                    if (!string.IsNullOrEmpty(nodeType))
                    {
                        if (!string.Equals(nodeTypeStr, nodeType, StringComparison.OrdinalIgnoreCase))
                        {
                            filteredCount++;
                            continue;
                        }
                    }

                    result.Add(new
                    {
                        id = ReflectionHelper.GetPropertyValue<int>(node, "Id"),
                        title = ReflectionHelper.GetPropertyValue<string>(node, "Title"),
                        fullTitle = ReflectionHelper.GetPropertyValue<string>(node, "FullTitle"),
                        address = ReflectionHelper.GetPropertyValue<string>(node, "Address"),
                        nodeType = nodeTypeStr
                    });
                }

                Logger.Info($"Узлы: всего {totalCount}, отфильтровано {filteredCount}, возвращено {result.Count} (filter type={nodeType})");

                await RequestRouter.SendJsonAsync(context, 200, new { nodes = result });
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка получения узлов: {ex.Message}");
                await RequestRouter.SendJsonAsync(context, 500, new { error = ex.Message });
            }
        }

    }
}
