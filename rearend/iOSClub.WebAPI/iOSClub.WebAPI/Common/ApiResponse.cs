namespace iOSClub.WebAPI.Common;

/// <summary>
/// 统一响应信封的非泛型视图。
/// 供 <see cref="ApiResponseResultFilter"/> 等中间环节在不关心具体数据类型时读取状态字段，
/// 从而无须反射即可识别任何 <c>ApiResponse&lt;T&gt;</c>。
/// </summary>
public interface IApiResponse
{
    /// <summary>
    /// 请求状态码（HTTP 状态码，200 表示成功）
    /// </summary>
    int Code { get; set; }

    /// <summary>
    /// 业务错误码（0 表示成功）
    /// </summary>
    int ErrorCode { get; set; }

    /// <summary>
    /// 响应消息
    /// </summary>
    string Message { get; set; }

    /// <summary>
    /// 详细描述
    /// </summary>
    string? Detail { get; set; }

    /// <summary>
    /// 请求ID，用于追踪请求
    /// </summary>
    string? RequestId { get; set; }

    /// <summary>
    /// 响应时间戳（UTC时间）
    /// </summary>
    string? Timestamp { get; set; }
}

/// <summary>
    /// 统一API响应模型
    /// </summary>
    /// <typeparam name="T">响应数据类型</typeparam>
    public class ApiResponse<T> : IApiResponse
    {
        /// <summary>
        /// 请求状态码（200表示成功，其他表示失败）
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// 业务错误码（0表示成功，其他表示具体错误类型）
        /// </summary>
        public int ErrorCode { get; set; }

        /// <summary>
        /// 响应消息
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// 详细描述
        /// </summary>
        public string? Detail { get; set; }

        /// <summary>
        /// 响应数据
        /// </summary>
        public T? Data { get; set; }
        
        /// <summary>
        /// 请求ID，用于追踪请求
        /// </summary>
        public string? RequestId { get; set; }
        
        /// <summary>
        /// 响应时间戳（UTC时间）
        /// </summary>
        public string? Timestamp { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ApiResponse()
        {
            Code = 200;
            ErrorCode = 0;
            Message = "Success";
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        /// <summary>
        /// 成功响应
        /// </summary>
        /// <param name="data">响应数据</param>
        /// <param name="message">响应消息</param>
        /// <param name="detail">详细描述</param>
        /// <param name="requestId">请求ID</param>
        /// <returns>成功响应模型</returns>
        public static ApiResponse<T> Success(T data, string message = "Success", string? detail = null, string? requestId = null)
        {
            return new ApiResponse<T>
            {
                Code = 200,
                ErrorCode = 0,
                Message = message,
                Detail = detail,
                Data = data,
                RequestId = requestId,
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
        }

        /// <summary>
        /// 失败响应
        /// </summary>
        /// <param name="errorCode">业务错误码</param>
        /// <param name="message">错误消息</param>
        /// <param name="detail">详细描述</param>
        /// <param name="requestId">请求ID</param>
        /// <returns>失败响应模型</returns>
        public static ApiResponse<T> Fail(int errorCode, string message, string? detail = null, string? requestId = null)
        {
            return new ApiResponse<T>
            {
                Code = ErrorCodeHttpMapper.HttpStatusFor(errorCode),
                ErrorCode = errorCode,
                Message = message,
                Detail = detail,
                RequestId = requestId,
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
        }

        /// <summary>
        /// 带数据的失败响应
        /// <para>
        /// 供"业务失败但需要把逐行结果带回给界面"的场景使用（如批量更新的零写入失败）：
        /// <c>Code</c> / <c>ErrorCode</c> 表达失败，<c>Data</c> 仍然是完整的结果对象。
        /// 与 <see cref="Fail"/> 区分开，避免同参数签名的重载产生歧义。
        /// </para>
        /// </summary>
        /// <param name="errorCode">业务错误码</param>
        /// <param name="data">响应数据</param>
        /// <param name="message">错误消息</param>
        /// <param name="detail">详细描述</param>
        /// <param name="requestId">请求ID</param>
        /// <returns>带数据的失败响应模型</returns>
        public static ApiResponse<T> FailWithData(int errorCode, T data, string message, string? detail = null,
            string? requestId = null)
        {
            return new ApiResponse<T>
            {
                Code = ErrorCodeHttpMapper.HttpStatusFor(errorCode),
                ErrorCode = errorCode,
                Message = message,
                Detail = detail,
                Data = data,
                RequestId = requestId,
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
        }
    }

/// <summary>
    /// 无数据的API响应模型
    /// </summary>
    public class ApiResponse : ApiResponse<object>
    {
        /// <summary>
        /// 成功响应
        /// </summary>
        /// <param name="message">响应消息</param>
        /// <param name="detail">详细描述</param>
        /// <param name="requestId">请求ID</param>
        /// <returns>成功响应模型</returns>
        public static ApiResponse Success(string message = "Success", string? detail = null, string? requestId = null)
        {
            return new ApiResponse
            {
                Code = 200,
                ErrorCode = 0,
                Message = message,
                Detail = detail,
                RequestId = requestId,
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
        }

        /// <summary>
        /// 失败响应
        /// </summary>
        /// <param name="errorCode">业务错误码</param>
        /// <param name="message">错误消息</param>
        /// <param name="detail">详细描述</param>
        /// <param name="requestId">请求ID</param>
        /// <returns>失败响应模型</returns>
        public new static ApiResponse Fail(int errorCode, string message, string? detail = null, string? requestId = null)
        {
            return new ApiResponse
            {
                Code = ErrorCodeHttpMapper.HttpStatusFor(errorCode),
                ErrorCode = errorCode,
                Message = message,
                Detail = detail,
                RequestId = requestId,
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };
        }
    }