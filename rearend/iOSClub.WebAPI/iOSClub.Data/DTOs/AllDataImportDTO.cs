using iOSClub.Data.DataObjects;

namespace iOSClub.Data.DTOs;

[Serializable]
public class AllDataImportDTO
{
    public List<StudentDO> Students { get; init; } = [];

    /// <summary>
    /// 全部干部/部员档案（含部门关系）。旧版备份文件只有 Presidents，导入时两者合并处理。
    /// </summary>
    public List<StaffDO> Staffs { get; init; } = [];

    public List<StaffDO> Presidents { get; init; } = [];
    public List<ResourceDO> Resources { get; init; } = [];
    public List<DepartmentDO> Departments { get; init; } = [];
    public List<CategoryDO> Categories { get; init; } = [];
    public List<ArticleDO> Articles { get; init; } = [];
}
