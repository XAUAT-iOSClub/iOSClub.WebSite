using System.Security.Cryptography;
using System.Text;
using iOSClub.WebAPI.Common.Config;

namespace iOSClub.WebAPI.Common.Security;

/// <summary>
/// RSA密钥管理类，用于生成、存储和轮换RSA密钥对
/// </summary>
public class RsaKeyManager(JwtConfig jwtConfig, ILogger<RsaKeyManager> logger)
{
    // 单一锁：GetCurrentPublicKey 持锁时会触发 EnsureKeysValid → RotateKeys → ClearKeyCache，
    // 若公私钥各用一把锁，两侧加锁顺序相反会构成 ABBA 死锁（且 /jwks 是匿名端点，可被外部触发）。
    private readonly Lock _keyLock = new();
    private RSA? _cachedPrivateKey;
    private RSA? _cachedPublicKey;

    /// <summary>
    /// 生成RSA密钥对
    /// </summary>
    /// <param name="keySize">密钥大小（建议至少2048位）</param>
    /// <returns>RSA密钥对</returns>
    public RSA GenerateKeyPair(int keySize = 2048)
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("开始生成RSA密钥对，密钥大小：{KeySize}位", keySize);
            }

            var rsa = RSA.Create(keySize);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("RSA密钥对生成成功");
            }
            return rsa;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "生成RSA密钥对失败");
            }
            throw;
        }
    }

    /// <summary>
    /// 将RSA密钥对存储到文件
    /// </summary>
    /// <param name="rsa">RSA对象</param>
    /// <param name="privateKeyPath">私钥文件路径</param>
    /// <param name="publicKeyPath">公钥文件路径</param>
    public void StoreKeyPair(RSA rsa, string privateKeyPath, string publicKeyPath)
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("开始存储RSA密钥对到文件");
            }

            // 确保目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(privateKeyPath) ?? string.Empty);
            Directory.CreateDirectory(Path.GetDirectoryName(publicKeyPath) ?? string.Empty);

            // 导出私钥（PKCS#8格式）
            var privateKey = rsa.ExportPkcs8PrivateKeyPem();
            File.WriteAllText(privateKeyPath, privateKey, Encoding.UTF8);

            // 导出公钥（X509格式）
            var publicKey = rsa.ExportRSAPublicKeyPem();
            File.WriteAllText(publicKeyPath, publicKey, Encoding.UTF8);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("RSA密钥对存储成功，私钥路径：{PrivateKeyPath}，公钥路径：{PublicKeyPath}",
                    privateKeyPath, publicKeyPath);
            }
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "存储RSA密钥对失败");
            }
            throw;
        }
    }

    /// <summary>
    /// 从文件加载RSA私钥
    /// </summary>
    /// <param name="privateKeyPath">私钥文件路径</param>
    /// <returns>RSA对象</returns>
    public RSA LoadPrivateKey(string privateKeyPath)
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("开始从文件加载RSA私钥：{PrivateKeyPath}", privateKeyPath);
            }

            if (!File.Exists(privateKeyPath))
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("私钥文件不存在：{PrivateKeyPath}", privateKeyPath);
                }
                throw new FileNotFoundException("私钥文件不存在", privateKeyPath);
            }

            var privateKey = File.ReadAllText(privateKeyPath, Encoding.UTF8);
            var rsa = RSA.Create();
            rsa.ImportFromPem(privateKey);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("RSA私钥加载成功");
            }
            return rsa;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "加载RSA私钥失败");
            }
            throw;
        }
    }

    /// <summary>
    /// 从文件加载RSA公钥
    /// </summary>
    /// <param name="publicKeyPath">公钥文件路径</param>
    /// <returns>RSA对象</returns>
    public RSA LoadPublicKey(string publicKeyPath)
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("开始从文件加载RSA公钥：{PublicKeyPath}", publicKeyPath);
            }

            if (!File.Exists(publicKeyPath))
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("公钥文件不存在：{PublicKeyPath}", publicKeyPath);
                }
                throw new FileNotFoundException("公钥文件不存在", publicKeyPath);
            }

            var publicKey = File.ReadAllText(publicKeyPath, Encoding.UTF8);
            var rsa = RSA.Create();
            rsa.ImportFromPem(publicKey);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("RSA公钥加载成功");
            }
            return rsa;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "加载RSA公钥失败");
            }
            throw;
        }
    }

    /// <summary>
    /// 检查密钥是否需要轮换
    /// </summary>
    /// <param name="keyPath">密钥文件路径</param>
    /// <returns>是否需要轮换</returns>
    public bool IsKeyRotationNeeded(string keyPath)
    {
        try
        {
            if (!File.Exists(keyPath))
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("密钥文件不存在，需要生成新密钥");
                }
                return true;
            }

            var fileInfo = new FileInfo(keyPath);
            var daysSinceCreation = (DateTime.UtcNow - fileInfo.CreationTimeUtc).TotalDays;

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("密钥文件创建于：{CreationTime}，已使用：{DaysSinceCreation}天，轮换周期：{RotationDays}天",
                    fileInfo.CreationTimeUtc, daysSinceCreation, jwtConfig.KeyRotationDays);
            }

            return daysSinceCreation >= jwtConfig.KeyRotationDays;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "检查密钥轮换状态失败");
            }
            // 发生错误时，默认需要轮换密钥
            return true;
        }
    }

    /// <summary>
    /// 执行密钥轮换
    /// </summary>
    public void RotateKeys()
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("开始执行密钥轮换");
            }

            // 生成新的密钥对
            var newRsa = GenerateKeyPair();

            // 备份旧密钥（如果存在）
            BackupOldKeys();

            // 存储新密钥
            StoreKeyPair(newRsa, jwtConfig.RsaPrivateKeyPath, jwtConfig.RsaPublicKeyPath);

            // 清除内存缓存，下次请求会加载新密钥
            ClearKeyCache();

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("密钥轮换成功");
            }
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "密钥轮换失败");
            }
            throw;
        }
    }

    /// <summary>
    /// 备份旧密钥
    /// </summary>
    private void BackupOldKeys()
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("开始备份旧密钥");
            }

            // 私钥与公钥共用同一个时间戳：分开取 UtcNow 会跨秒错配，
            // 同一秒内连续两次轮换也会因重名互相覆盖。加毫秒降低碰撞概率。
            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

            // 备份私钥
            if (File.Exists(jwtConfig.RsaPrivateKeyPath))
            {
                var privateKeyBackupPath = $"{jwtConfig.RsaPrivateKeyPath}.{stamp}.bak";
                File.Copy(jwtConfig.RsaPrivateKeyPath, privateKeyBackupPath, true);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("私钥备份成功：{BackupPath}", privateKeyBackupPath);
                }
            }

            // 备份公钥
            if (File.Exists(jwtConfig.RsaPublicKeyPath))
            {
                var publicKeyBackupPath = $"{jwtConfig.RsaPublicKeyPath}.{stamp}.bak";
                File.Copy(jwtConfig.RsaPublicKeyPath, publicKeyBackupPath, true);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("公钥备份成功：{BackupPath}", publicKeyBackupPath);
                }
            }
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "备份旧密钥失败");
            }
            // 备份失败不影响主流程
        }
    }

    /// <summary>
    /// 确保密钥存在且有效，如果需要则生成或轮换密钥
    /// </summary>
    public void EnsureKeysValid()
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("开始检查密钥有效性");
            }

            // 检查私钥是否存在且需要轮换
            if (!File.Exists(jwtConfig.RsaPrivateKeyPath) || IsKeyRotationNeeded(jwtConfig.RsaPrivateKeyPath))
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("私钥不存在或需要轮换，生成新密钥对");
                }
                RotateKeys();
            }

            // 检查公钥是否存在
            if (!File.Exists(jwtConfig.RsaPublicKeyPath))
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("公钥不存在，重新生成密钥对");
                }
                RotateKeys();
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("密钥有效性检查完成，密钥有效");
            }
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "检查密钥有效性失败");
            }
            throw;
        }
    }

    /// <summary>
    /// 获取当前有效的RSA私钥（带内存缓存，避免每次请求都读磁盘）
    /// </summary>
    /// <returns>RSA私钥对象</returns>
    public RSA GetCurrentPrivateKey()
    {
        if (_cachedPrivateKey is not null) return _cachedPrivateKey;

        lock (_keyLock)
        {
            if (_cachedPrivateKey is not null) return _cachedPrivateKey;
            EnsureKeysValid();
            _cachedPrivateKey = LoadPrivateKey(jwtConfig.RsaPrivateKeyPath);
            return _cachedPrivateKey;
        }
    }

    /// <summary>
    /// 获取当前有效的RSA公钥（带内存缓存，避免每次请求都读磁盘）
    /// </summary>
    /// <returns>RSA公钥对象</returns>
    public RSA GetCurrentPublicKey()
    {
        if (_cachedPublicKey is not null) return _cachedPublicKey;

        lock (_keyLock)
        {
            if (_cachedPublicKey is not null) return _cachedPublicKey;
            EnsureKeysValid();
            _cachedPublicKey = LoadPublicKey(jwtConfig.RsaPublicKeyPath);
            return _cachedPublicKey;
        }
    }

    /// <summary>
    /// 清除密钥缓存（在密钥轮换后调用）
    /// <para>
    /// 只丢弃引用、不 Dispose：GetCurrentPublicKey / GetCurrentPrivateKey 会把缓存实例交给调用方，
    /// 调用方可能在锁外继续使用（例如 JWKS 端点正导出参数、JwtService 正签发 token）。
    /// 在这里 Dispose 会让并发请求抛 ObjectDisposedException。轮换本身 90 天一次，
    /// 让旧实例交给 GC 终结器释放，代价可以忽略。
    /// </para>
    /// </summary>
    public void ClearKeyCache()
    {
        lock (_keyLock)
        {
            _cachedPrivateKey = null;
            _cachedPublicKey = null;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("RSA密钥缓存已清除");
        }
    }

    /// <summary>
    /// 按 JWT header 的口径计算 kid：公钥 PKCS#1 DER 的 SHA256，取 Base64 前 16 位。
    /// 签发（JwtService）与公布（JWKS 端点）必须共用本方法，否则客户端按 kid 取不到钥匙。
    /// </summary>
    /// <param name="rsa">RSA 密钥（私钥或公钥均可，只导出公钥部分）</param>
    /// <returns>kid</returns>
    public static string ComputeKeyId(RSA rsa)
    {
        var publicKeyBytes = rsa.ExportRSAPublicKey();
        return Convert.ToBase64String(SHA256.HashData(publicKeyBytes))[..16];
    }

    /// <summary>
    /// 尝试加载最近一次轮换前的公钥（RotateKeys 产生的 .bak 备份）。
    /// 轮换后旧 token 仍有最长 20 分钟有效期，JWKS 需要在这段窗口内继续公布上一代公钥，
    /// 否则客户端会因找不到 kid 而拒绝验签。
    /// </summary>
    /// <returns>上一代公钥；不存在或读取失败时返回 null（调用方按失败降级处理）</returns>
    public RSA? TryLoadPreviousPublicKey()
    {
        try
        {
            var directory = Path.GetDirectoryName(jwtConfig.RsaPublicKeyPath);
            var fileName = Path.GetFileName(jwtConfig.RsaPublicKeyPath);
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName) || !Directory.Exists(directory))
            {
                return null;
            }

            var candidate = Directory.GetFiles(directory, $"{fileName}.*.bak")
                .OrderByDescending(path => path, StringComparer.Ordinal)
                .FirstOrDefault();
            if (candidate is null)
            {
                return null;
            }

            var publicKey = File.ReadAllText(candidate, Encoding.UTF8);
            var rsa = RSA.Create();
            try
            {
                rsa.ImportFromPem(publicKey);
            }
            catch
            {
                // 备份可能正在被 File.Copy 半写，或内容损坏：释放后交给外层统一记录，
                // 不在这里吞掉异常（否则句柄会按请求累积）。
                rsa.Dispose();
                throw;
            }

            return rsa;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(ex, "加载上一代RSA公钥失败，JWKS 将只返回当前公钥");
            }
            return null;
        }
    }
}