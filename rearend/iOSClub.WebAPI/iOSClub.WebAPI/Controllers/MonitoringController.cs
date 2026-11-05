using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

namespace iOSClub.WebAPI.Controllers;

/// <summary>
/// 监控数据控制器
/// </summary>
[Route("[controller]")]
[ApiController]
public class MonitoringController(IDataAccessStatisticsService statisticsService) : ControllerBase
{
    /// <summary>
    /// /Monitoring/performance 的固定统计窗口（秒）。
    /// <para>
    /// Prometheus 的 http_requests_total 是从进程启动累计的计数器，本身没有窗口概念。
    /// 接口只能给出"累计请求数 ÷ 固定 3600 秒"这一平均值，因此把窗口显式返回，
    /// 调用方才能正确理解 averageRequestsPerSecond 的含义。
    /// </para>
    /// </summary>
    private const int MetricsWindowSeconds = 3600;

    /// <summary>
    /// 获取API性能监控数据
    /// </summary>
    /// <returns>监控数据</returns>
    [HttpGet("performance")]
    public async Task<ActionResult<ApiResponse<object>>> GetPerformanceMetrics()
    {
        // 收集并解析指标数据
        var metricsText = await CollectMetricsAsync();
        var metrics = ParseMetrics(metricsText);

        // 计算各项性能指标
        var performanceData = new
        {
            // HTTP 性能指标
            http = new
            {
                totalRequests = CalculateTotalRequests(metrics),
                windowSeconds = MetricsWindowSeconds,
                // 累计请求数除以固定窗口，只能叫"窗口平均"，不能叫实时速率
                averageRequestsPerSecond = CalculateAverageRequestsPerSecond(metrics),
                errorRate = CalculateErrorRate(metrics),
                averageResponseTime = CalculateAverageResponseTime(metrics),
                responseTimeByStatus = CalculateResponseTimeByStatus(metrics),
                requestsByMethod = CalculateRequestsByMethod(metrics)
            },

            // 系统资源指标（如果有）
            system = new
            {
                // process_cpu_seconds_total 是进程启动以来的累计 CPU 秒数，不是使用率
                cpuSecondsTotal = GetSystemMetric(metrics, "process_cpu_seconds_total"),
                memoryUsage = GetSystemMetric(metrics, "process_working_set_bytes"),
                gcCollections = GetSystemMetric(metrics, "dotnet_gc_collection_count_total")
            },

            // ASP.NET Core 应用指标
            app = new
            {
                activeRequests = GetSystemMetric(metrics, "http_requests_in_progress"),
                connectionCount = GetSystemMetric(metrics, "http_connections_current"),
                requestQueueLength = GetSystemMetric(metrics, "http_request_queue_length")
            },

            timestamp = DateTime.UtcNow
        };

        return ApiResponse<object>.Success(performanceData);
    }

    /// <summary>
    /// 获取HTTP请求统计数据
    /// </summary>
    /// <returns>HTTP请求统计</returns>
    [HttpGet("http-stats")]
    public async Task<ActionResult<ApiResponse<object>>> GetHttpStats()
    {
        // 收集并解析指标数据
        var metricsText = await CollectMetricsAsync();
        var metrics = ParseMetrics(metricsText);

        // 计算HTTP统计数据
        var httpStats = new
        {
            totalRequests = CalculateTotalRequests(metrics),
            windowSeconds = MetricsWindowSeconds,
            // 与 /performance 保持同一含义：累计请求数除以固定窗口的窗口平均
            averageRequestsPerSecond = CalculateAverageRequestsPerSecond(metrics),
            errorRate = CalculateErrorRate(metrics),
            averageResponseTime = CalculateAverageResponseTime(metrics)
        };

        return ApiResponse<object>.Success(httpStats);
    }

    /// <summary>
    /// 获取数据访问统计
    /// </summary>
    /// <param name="entityType">实体类型（可选）</param>
    /// <param name="top">返回前N条数据</param>
    /// <returns>数据访问统计</returns>
    [HttpGet("data-access-stats")]
    public async Task<ActionResult<ApiResponse<DataAccessStatisticsResult>>> GetDataAccessStats(
        string? entityType = null, int top = 10)
    {
        var stats = await statisticsService.GetDataAccessStatisticsAsync(entityType, top);
        return ApiResponse<DataAccessStatisticsResult>.Success(stats);
    }

    /// <summary>
    /// 获取数据变化统计
    /// </summary>
    /// <param name="entityType">实体类型（可选）</param>
    /// <param name="top">返回前N条数据</param>
    /// <returns>数据变化统计</returns>
    [HttpGet("data-change-stats")]
    public async Task<ActionResult<ApiResponse<DataChangeStatisticsResult>>> GetDataChangeStats(
        string? entityType = null, int top = 10)
    {
        var stats = await statisticsService.GetDataChangeStatisticsAsync(entityType, top);
        return ApiResponse<DataChangeStatisticsResult>.Success(stats);
    }

    /// <summary>
    /// 重置数据统计
    /// </summary>
    /// <remarks>
    /// 重置会清空统计存储，属于破坏性写操作，仅 Founder / President 可用。
    /// 其余只读接口的匿名访问属于产品决策，本次不做限制。
    /// </remarks>
    /// <param name="entityType">实体类型（可选）</param>
    /// <returns>操作结果</returns>
    [Authorize(Roles = "Founder, President")]
    [HttpPost("reset-data-stats")]
    public async Task<ActionResult<ApiResponse<bool>>> ResetDataStats(string? entityType = null)
    {
        await statisticsService.ResetStatisticsAsync(entityType);
        return ApiResponse<bool>.Success(true);
    }

    /// <summary>
    /// 异步收集所有指标
    /// </summary>
    /// <returns>指标文本</returns>
    private static async Task<string> CollectMetricsAsync()
    {
        using var stream = new MemoryStream();
        await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
        stream.Position = 0;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// 解析指标文本
    /// </summary>
    /// <param name="metricsText">指标文本</param>
    /// <returns>解析后的指标字典</returns>
    private Dictionary<string, List<Dictionary<string, object>>> ParseMetrics(string metricsText)
    {
        var metrics = new Dictionary<string, List<Dictionary<string, object>>>();
        //string currentMetric = null;

        foreach (var line in metricsText.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            if (line.Contains('{'))
            {
                // 解析指标行
                var parts = line.Split(' ');
                if (parts.Length < 2)
                    continue;

                var metricPart = parts[0];
                var valuePart = parts[1];

                // 提取指标名称
                var metricName = metricPart.Split('{')[0];

                // 提取标签
                var labels = new Dictionary<string, string>();
                if (metricPart.Contains('{'))
                {
                    var labelsPart = metricPart.Substring(metricPart.IndexOf('{') + 1,
                        metricPart.IndexOf('}') - metricPart.IndexOf('{') - 1);
                    if (!string.IsNullOrWhiteSpace(labelsPart))
                    {
                        foreach (var label in labelsPart.Split(','))
                        {
                            var labelParts = label.Split('=');
                            if (labelParts.Length == 2)
                            {
                                labels[labelParts[0]] = labelParts[1].Trim('"');
                            }
                        }
                    }
                }

                // 保存指标数据
                if (!metrics.TryGetValue(metricName, out var value))
                {
                    value = [];
                    metrics[metricName] = value;
                }

                value.Add(new Dictionary<string, object>
                {
                    { "labels", labels },
                    { "value", double.TryParse(valuePart, out var v) ? v : 0 }
                });
            }
        }

        return metrics;
    }

    /// <summary>
    /// 计算总请求数
    /// </summary>
    /// <param name="metrics">指标数据</param>
    /// <returns>总请求数</returns>
    private static long CalculateTotalRequests(Dictionary<string, List<Dictionary<string, object>>> metrics)
    {
        if (!metrics.TryGetValue("http_requests_total", out var metric))
            return 0;

        return (long)metric.Sum(m => (double)m["value"]);
    }

    /// <summary>
    /// 计算固定窗口内的平均每秒请求数
    /// <para>
    /// http_requests_total 是累计计数器，这里除以固定的 <see cref="MetricsWindowSeconds"/> 秒，
    /// 因此结果只是该窗口的平均值，字段名必须体现"average"，不能暗示实时速率。
    /// </para>
    /// </summary>
    /// <param name="metrics">指标数据</param>
    /// <returns>窗口内平均每秒请求数</returns>
    private static double CalculateAverageRequestsPerSecond(
        Dictionary<string, List<Dictionary<string, object>>> metrics)
    {
        return CalculateTotalRequests(metrics) / (double)MetricsWindowSeconds;
    }

    /// <summary>
    /// 计算错误率
    /// </summary>
    /// <param name="metrics">指标数据</param>
    /// <returns>错误率（百分比）</returns>
    private static double CalculateErrorRate(Dictionary<string, List<Dictionary<string, object>>> metrics)
    {
        if (!metrics.TryGetValue("http_requests_total", out var metric))
            return 0;

        var totalRequests = metric.Sum(m => (double)m["value"]);
        if (totalRequests == 0)
            return 0;

        var errorRequests = metrics["http_requests_total"]
            .Where(m =>
            {
                var labels = (Dictionary<string, string>)m["labels"];
                return labels.ContainsKey("status") && labels["status"].StartsWith("5");
            })
            .Sum(m => (double)m["value"]);

        return (errorRequests / totalRequests) * 100;
    }

    /// <summary>
    /// 计算平均响应时间
    /// </summary>
    /// <param name="metrics">指标数据</param>
    /// <returns>平均响应时间（毫秒）</returns>
    private static double CalculateAverageResponseTime(Dictionary<string, List<Dictionary<string, object>>> metrics)
    {
        // 简化实现，实际应使用直方图数据计算
        if (!metrics.TryGetValue("http_request_duration_seconds_sum", out var value) ||
            !metrics.TryGetValue("http_request_duration_seconds_count", out var value1)) return 0;
        var sum = value.Sum(m => (double)m["value"]);
        var count = value1.Sum(m => (double)m["value"]);

        if (count > 0)
        {
            return sum / count * 1000; // 转换为毫秒
        }

        return 0;
    }

    /// <summary>
    /// 按状态码计算响应时间分布
    /// </summary>
    /// <param name="metrics">指标数据</param>
    /// <returns>按状态码分布的响应时间</returns>
    private static Dictionary<string, double> CalculateResponseTimeByStatus(
        Dictionary<string, List<Dictionary<string, object>>> metrics)
    {
        var result = new Dictionary<string, double>();

        // 简化实现，实际应使用直方图数据计算
        if (metrics.TryGetValue("http_request_duration_seconds_sum", out var sumMetrics) &&
            metrics.TryGetValue("http_request_duration_seconds_count", out var countMetrics))
        {
            // 按状态码分组计算平均响应时间
            var sumByStatus = new Dictionary<string, double>();
            var countByStatus = new Dictionary<string, double>();

            // 处理响应时间总和
            foreach (var metric in sumMetrics)
            {
                var labels = (Dictionary<string, string>)metric["labels"];
                if (labels.TryGetValue("status", out var status))
                {
                    sumByStatus.TryAdd(status, 0);
                    sumByStatus[status] += (double)metric["value"];
                }
            }

            // 处理请求计数
            foreach (var metric in countMetrics)
            {
                var labels = (Dictionary<string, string>)metric["labels"];
                if (labels.TryGetValue("status", out var status))
                {
                    countByStatus.TryAdd(status, 0);
                    countByStatus[status] += (double)metric["value"];
                }
            }

            // 计算每个状态码的平均响应时间
            foreach (var status in sumByStatus.Keys)
            {
                if (countByStatus.TryGetValue(status, out var count) && count > 0)
                {
                    result[status] = (sumByStatus[status] / count) * 1000; // 转换为毫秒
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 按请求方法计算请求分布
    /// </summary>
    /// <param name="metrics">指标数据</param>
    /// <returns>按请求方法分布的请求数</returns>
    private static Dictionary<string, long> CalculateRequestsByMethod(
        Dictionary<string, List<Dictionary<string, object>>> metrics)
    {
        var result = new Dictionary<string, long>();

        if (metrics.TryGetValue("http_requests_total", out var requestMetrics))
        {
            // 按请求方法分组计算请求数
            foreach (var metric in requestMetrics)
            {
                var labels = (Dictionary<string, string>)metric["labels"];
                if (labels.TryGetValue("method", out var method))
                {
                    result.TryAdd(method, 0);
                    result[method] += (long)(double)metric["value"];
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 获取系统指标值
    /// </summary>
    /// <param name="metrics">指标数据</param>
    /// <param name="metricName">指标名称</param>
    /// <returns>系统指标值</returns>
    private static double GetSystemMetric(Dictionary<string, List<Dictionary<string, object>>> metrics,
        string metricName)
    {
        if (metrics.TryGetValue(metricName, out var metricValues))
        {
            return metricValues.Sum(m => (double)m["value"]);
        }

        return 0;
    }
}