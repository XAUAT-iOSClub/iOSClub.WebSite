using iOSClub.DataApi.Repositories;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common.Security;
using iOSClub.WebAPI.Services;

namespace iOSClub.WebAPI.Common.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// 注册仓库和服务
        /// </summary>
        public void RegisterRepositoriesAndServices()
        {
            services.AddScoped<IArticleRepository, ArticleRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IDepartmentRepository, DepartmentRepository>();
            services.AddScoped<IResourceRepository, ResourceRepository>();
            services.AddScoped<IStaffRepository, StaffRepository>();
            services.AddScoped<IStudentRepository, StudentRepository>();

            services.AddScoped<IDataCentreService, DataCentreService>();
            services.AddScoped<IClientApplicationRepository, ClientApplicationRepository>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<ILoginService, LoginService>();
            services.AddScoped<IDataAccessStatisticsService, DataAccessStatisticsService>();
            services.AddScoped<IDepartmentImportService, DepartmentImportService>();
        }

        /// <summary>
        /// 注册安全相关服务
        /// </summary>
        public void RegisterSecurityServices()
        {
            services.AddSingleton<MaskingConfig>();
            services.AddSingleton<DataMaskingService>();
            services.AddSingleton<LogAuditService>();

            services.AddSingleton<RateLimitConfig>();
            services.AddSingleton<RateLimitService>();

            services.AddSingleton<JwtService>();
            services.AddSingleton<RsaKeyManager>();

            // 注册IP黑名单缓存服务
            services.AddSingleton<IIpBlacklistCacheService, IpBlacklistCacheService>();
        }
    }
}
