using System.ComponentModel.DataAnnotations;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.Mappers;
using iOSClub.WebAPI.Common;
using Mapster;

namespace iOSClub.Tests;

/// <summary>
/// 错误码 → HTTP 状态码的契约测试。
/// <para>
/// 这张映射表是前后端共同依赖的契约，前端 <c>src/constants/ErrorCode.ts</c> 是它的镜像。
/// 改动这里意味着前端也要跟着改。
/// </para>
/// </summary>
public class ErrorCodeHttpMapperTests
{
    [Theory]
    [InlineData(ErrorCode.Success, 200)]
    [InlineData(ErrorCode.ParameterEmpty, 400)]
    [InlineData(ErrorCode.ParameterValidationFailed, 400)]
    [InlineData(ErrorCode.ResourceAlreadyExists, 400)]
    [InlineData(ErrorCode.InvalidStatusForOperation, 400)]
    // 3000-3999 区间默认是 403，但下面三个语义上是"没通过认证"，必须覆盖成 401
    [InlineData(ErrorCode.Unauthorized, 401)]
    [InlineData(ErrorCode.LoginExpired, 401)]
    [InlineData(ErrorCode.InvalidToken, 401)]
    [InlineData(ErrorCode.InsufficientPermission, 403)]
    [InlineData(ErrorCode.ResourceNotFound, 404)]
    [InlineData(ErrorCode.UserNotFound, 404)]
    [InlineData(ErrorCode.FileNotFound, 404)]
    [InlineData(ErrorCode.InternalServerError, 500)]
    [InlineData(ErrorCode.NetworkError, 500)]
    [InlineData(ErrorCode.ExternalServiceTimeout, 500)]
    [InlineData(ErrorCode.TooManyRequests, 429)]
    [InlineData(ErrorCode.InvalidRequest, 400)]
    public void HttpStatusFor_MapsErrorCodeToExpectedStatus(int errorCode, int expected)
    {
        Assert.Equal(expected, ErrorCodeHttpMapper.HttpStatusFor(errorCode));
    }

    [Fact]
    public void ApiResponse_Fail_DerivesCodeFromErrorCode()
    {
        var response = ApiResponse<string>.Fail(ErrorCode.UserNotFound, "用户不存在");

        Assert.Equal(404, response.Code);
        Assert.Equal(ErrorCode.UserNotFound, response.ErrorCode);
    }

    [Fact]
    public void ApiResponse_Success_IsCode200ErrorCode0()
    {
        var response = ApiResponse<string>.Success("payload");

        Assert.Equal(200, response.Code);
        Assert.Equal(ErrorCode.Success, response.ErrorCode);
    }
}

/// <summary>
/// Staff 部门映射的回归测试。
/// <para>
/// 回归背景：MapperConfig 里曾经是 <c>.Ignore(dest =&gt; dest.Department)</c>，而 StaffDO 只有
/// 导航属性 Department（没有 DepartmentName 字符串字段），于是 StaffCreateDTO.DepartmentName
/// 被整个丢掉，StaffRepository 判定"会员必须指定部门"抛异常 —— 加部员从未成功过。
/// </para>
/// </summary>
public class MapperConfigStaffMappingTests
{
    public MapperConfigStaffMappingTests()
    {
        // Mapster 的配置是全局的，正常只由 Program.cs 调用。测试里直接断言映射行为，
        // 这里显式配置一次，避免依赖"别的测试先启动过应用"这种顺序耦合。
        MapperConfig.Configure();
    }

    [Fact]
    public void StaffCreateDTO_DepartmentName_MapsToDepartmentNavigation()
    {
        var dto = new StaffCreateDTO
        {
            UserId = "2021000001",
            Name = "张三",
            Identity = "Department",
            DepartmentName = "技术部"
        };

        var staff = dto.Adapt<StaffDO>();

        Assert.NotNull(staff.Department);
        Assert.Equal("技术部", staff.Department!.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StaffCreateDTO_WithoutUsableDepartmentName_LeavesDepartmentNull(string? departmentName)
    {
        var dto = new StaffCreateDTO
        {
            UserId = "2021000002",
            Name = "李四",
            Identity = "President",
            DepartmentName = departmentName
        };

        var staff = dto.Adapt<StaffDO>();

        Assert.Null(staff.Department);
    }

    [Fact]
    public void StudentCreateDTO_HashesPasswordIntoPasswordHash()
    {
        var dto = new StudentCreateDTO
        {
            UserId = "2021000003",
            UserName = "王五",
            Password = "Password123"
        };

        var student = dto.Adapt<StudentDO>();

        Assert.False(string.IsNullOrEmpty(student.PasswordHash));
        Assert.True(iOSClub.Data.DataTool.IsOk("Password123", student.PasswordHash));
    }
}

/// <summary>
/// StudentUpdateDTO → StudentDO 映射的回归测试。
/// <para>
/// 回归背景：MapperConfig 里曾经是 <c>.Ignore(dest =&gt; dest.UserId)</c>，把 DTO 的 UserId 抹成空串。
/// 而 UserId 在仓库层是**定位键**（UpdateProfileAsync 靠它 FirstOrDefault），于是 /User/profile
/// 与 /MemberManagement/update 的每个请求都在第一道守卫被拒 —— 这两个接口从未成功过。
/// 主键本身没有被改的风险：StudentDO.Update 的覆写列表里根本没有 UserId。
/// </para>
/// </summary>
public class MapperConfigStudentUpdateMappingTests
{
    public MapperConfigStudentUpdateMappingTests()
    {
        MapperConfig.Configure();
    }

    [Fact]
    public void StudentUpdateDTO_UserId_SurvivesMapping()
    {
        var dto = new StudentUpdateDTO { UserId = "2021000001", UserName = "张三" };

        var student = dto.Adapt<StudentDO>();

        // UserId 必须原样带过去，否则仓库层查不到人，整个更新接口变成 no-op
        Assert.Equal("2021000001", student.UserId);
        Assert.Equal("张三", student.UserName);
    }

    [Fact]
    public void StudentUpdateDTO_CarriesNoPassword()
    {
        var dto = new StudentUpdateDTO { UserId = "2021000001", UserName = "张三" };

        var student = dto.Adapt<StudentDO>();

        // StudentUpdateDTO 刻意不暴露密码字段 —— 配合 UpdateProfileAsync 永不触碰
        // PasswordHash，这是"改资料不可能顺手改掉密码"的两道闸门。
        Assert.True(string.IsNullOrEmpty(student.PasswordHash));
    }
}

/// <summary>
/// 注册 DTO 的校验测试。
/// <para>
/// 空密码必须在进入 action 之前就被 DataAnnotations 拦下：
/// MapperConfig 的哈希映射是在 Mapster 表达式里调用 DataTool.StringToHash 的，
/// 一旦空值放行到映射阶段，就只能在映射内部抛异常，最终变成 500 而不是可读的 400。
/// </para>
/// </summary>
public class StudentCreateDtoValidationTests
{
    private static bool Validate(object model, out List<ValidationResult> results)
    {
        results = [];
        return Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);
    }

    private static StudentCreateDTO ValidDto() => new()
    {
        UserId = "2021000001",
        UserName = "张三",
        Academy = "计算机与信息工程学院",
        PoliticalLandscape = "共青团员",
        Gender = "男",
        ClassName = "2021级1班",
        PhoneNum = "13800138000",
        Password = "Password123"
    };

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPassword_FailsValidation(string password)
    {
        var dto = ValidDto();
        dto.Password = password;

        Assert.False(Validate(dto, out var results));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(StudentCreateDTO.Password)));
    }

    [Fact]
    public void NonEmptyPassword_PassesValidation()
    {
        Assert.True(Validate(ValidDto(), out _));
    }

    [Theory]
    [InlineData("23456")]      // 姓名是纯数字
    [InlineData("张三3")]      // 姓名混入数字
    [InlineData("张")]         // 姓名过短
    public void InvalidUserName_FailsValidation(string userName)
    {
        var dto = ValidDto();
        dto.UserName = userName;

        Assert.False(Validate(dto, out var results));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(StudentCreateDTO.UserName)));
    }

    [Theory]
    [InlineData("LIJIAJUN")]   // 字母
    [InlineData("20210001")]   // 8 位
    [InlineData("20210000011")] // 11 位
    [InlineData("20210000AB")] // 混入字母
    public void InvalidUserId_FailsValidation(string userId)
    {
        var dto = ValidDto();
        dto.UserId = userId;

        Assert.False(Validate(dto, out var results));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(StudentCreateDTO.UserId)));
    }

    [Theory]
    [InlineData(nameof(StudentCreateDTO.Academy), "不存在的学院")]
    [InlineData(nameof(StudentCreateDTO.PoliticalLandscape), "不明身份")]
    [InlineData(nameof(StudentCreateDTO.Gender), "未知")]
    [InlineData(nameof(StudentCreateDTO.PhoneNum), "12345")]
    public void InvalidEnumeratedOrFormattedField_FailsValidation(string field, string value)
    {
        var dto = ValidDto();
        typeof(StudentCreateDTO).GetProperty(field)!.SetValue(dto, value);

        Assert.False(Validate(dto, out var results));
        Assert.Contains(results, r => r.MemberNames.Contains(field));
    }
}

/// <summary>
/// 更新 DTO 的格式校验测试。
/// <para>
/// StudentUpdateDTO 的语义是"空值表示不修改该字段"，所以可选字段为空时必须放行，
/// 否则前端只改手机号也会因为没带姓名而被 400 拦下。
/// </para>
/// </summary>
public class StudentUpdateDtoValidationTests
{
    private static bool Validate(object model, out List<ValidationResult> results)
    {
        results = [];
        return Validator.TryValidateObject(model, new ValidationContext(model), results,
            validateAllProperties: true);
    }

    [Fact]
    public void EmptyOptionalFields_PassValidation()
    {
        var dto = new StudentUpdateDTO { UserId = "2021000001" };

        Assert.True(Validate(dto, out _));
    }

    [Theory]
    [InlineData("23456")]
    [InlineData("张三3")]
    public void InvalidUserName_FailsValidation(string invalid)
    {
        var dto = new StudentUpdateDTO { UserId = "2021000001", UserName = invalid };

        Assert.False(Validate(dto, out var results));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(StudentUpdateDTO.UserName)));
    }

    [Theory]
    [InlineData("LIJIAJUN")]
    [InlineData("12345")]
    public void InvalidPhoneNum_FailsValidation(string invalid)
    {
        var dto = new StudentUpdateDTO { UserId = "2021000001", PhoneNum = invalid };

        Assert.False(Validate(dto, out var results));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(StudentUpdateDTO.PhoneNum)));
    }
}
