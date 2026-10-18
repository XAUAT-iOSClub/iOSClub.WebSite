using iOSClub.Data.DataObjects;
using iOSClub.Data.VOs;
using Mapster;

namespace iOSClub.Data.Mappers;

public static class MapperConfig
{
    public static void Configure()
    {
        var config = TypeAdapterConfig.GlobalSettings;

        // DO → VO 映射（VO不含PasswordHash/ClientSecret，Mapster自动跳过不存在的目标属性）
        config.NewConfig<StudentDO, StudentVO>();
        config.NewConfig<StudentDO, MemberVO>();

        config.NewConfig<StaffDO, StaffVO>()
            .Map(dest => dest.DepartmentName,
                src => src.Department != null ? src.Department.Name : null);

        config.NewConfig<ArticleDO, ArticleVO>()
            .Map(dest => dest.CategoryName,
                src => src.Category != null ? src.Category.Name : null);

        config.NewConfig<ArticleDO, ArticleListItemVO>()
            .Map(dest => dest.CategoryName,
                src => src.Category != null ? src.Category.Name : null);

        config.NewConfig<ResourceDO, ResourceVO>();

        config.NewConfig<CategoryDO, CategoryVO>()
            .Map(dest => dest.Articles,
                src => src.Articles.Select(a => a.Adapt<ArticleListItemVO>()).ToList());

        config.NewConfig<DepartmentDO, DepartmentVO>()
            .Map(dest => dest.Staffs, src => src.Staffs.Adapt<List<StaffVO>>());

        // DO 里回调地址是分号拼接的字符串，VO 暴露为数组（与 ClientAppResultVO 一致）。
        config.NewConfig<ClientApplicationDO, ClientAppVO>()
            .Map(dest => dest.RedirectUris,
                src => src.RedirectUris
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList());

        config.NewConfig<ClientApplicationDO, ClientAppResultVO>()
            .Map(dest => dest.RedirectUris,
                src => src.RedirectUris.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList());

        // DTO → DO 映射
        config.NewConfig<DTOs.StudentCreateDTO, StudentDO>()
            .Map(dest => dest.PasswordHash, src => DataTool.StringToHash(src.Password));

        // UserId 必须映射过去：它在仓库层是**定位键**（UpdateProfileAsync 靠它 FirstOrDefault），
        // 而不是待覆写的字段。以前这里 .Ignore(dest => dest.UserId) 把它抹成空串，
        // 于是仓库层第一道守卫就把所有更新请求拒了 —— /User/profile 与 /MemberManagement/update
        // 从未成功过。主键本身没有被改的风险：StudentDO.Update 的覆写列表里根本没有 UserId。
        // JoinTime 不在 DTO 里，保持忽略即可。
        config.NewConfig<DTOs.StudentUpdateDTO, StudentDO>()
            .Ignore(dest => dest.JoinTime);

        // StaffDO 只有导航属性 Department（没有 DepartmentName 字符串字段），
        // 所以必须显式把 DTO 的部门名映射成导航对象。之前这里的 .Ignore 会把部门
        // 整个丢掉，导致 StaffRepository.CreateStaffAsync 判定"会员必须指定部门"而失败。
        // 真正校验部门是否存在由仓库层按名称查询完成。
        config.NewConfig<DTOs.StaffCreateDTO, StaffDO>()
            .Map(dest => dest.Department,
                src => string.IsNullOrWhiteSpace(src.DepartmentName)
                    ? null
                    : new DepartmentDO { Name = src.DepartmentName });

        config.NewConfig<DTOs.ArticleCreateDTO, ArticleDO>()
            .Ignore(dest => dest.CategoryId)
            .Ignore(dest => dest.Category)
            .Ignore(dest => dest.LastWriteTime);

        config.NewConfig<DTOs.ResourceCreateUpdateDTO, ResourceDO>();
        config.NewConfig<DTOs.CategoryCreateUpdateDTO, CategoryDO>();
        config.NewConfig<DTOs.DepartmentCreateUpdateDTO, DepartmentDO>();
        config.NewConfig<DTOs.ClientAppCreateDTO, ClientApplicationDO>();
        config.NewConfig<DTOs.ClientAppUpdateDTO, ClientApplicationDO>();
        // 活动记录系统
        config.NewConfig<DTOs.ActivityCreateDTO, ActivityDO>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.Status)
            .Ignore(dest => dest.CreatedAt)
            .Ignore(dest => dest.UpdatedAt)
            .Ignore(dest => dest.Participants);

        config.NewConfig<DTOs.ActivityUpdateDTO, ActivityDO>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.Status)
            .Ignore(dest => dest.CreatedAt)
            .Ignore(dest => dest.UpdatedAt)
            .Ignore(dest => dest.Participants);

        config.NewConfig<DTOs.ActivityParticipantCreateDTO, ActivityParticipantDO>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.ActivityId)
            .Ignore(dest => dest.CreatedAt)
            .Ignore(dest => dest.Activity);

        config.NewConfig<ActivityOperationLogDO, ActivityOperationLogVO>();
    }
}
