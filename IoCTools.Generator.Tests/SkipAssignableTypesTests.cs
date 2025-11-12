using IoCTools.Generator.Tests;

public class SkipAssignableTypesTests
{
    [Fact]
    public void Default_Skips_AspNetCore_ControllerBase_Registration_But_Generates_Constructor()
    {
        var code = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;
namespace Microsoft.AspNetCore.Mvc { public abstract class ControllerBase { } }

namespace Test
{
    [Scoped]
    public partial class UsersController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        [Inject] private readonly ILogger<UsersController> _logger;
    }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(code, analyzerBuildProperties: null);
        result.HasErrors.Should().BeFalse();
        var reg = result.GetServiceRegistrationSource();
        if (reg != null) reg.Content.Should().NotContain("UsersController");
        var ctor = result.GetRequiredConstructorSource("UsersController");
        ctor!.Content.Should().Contain("public UsersController(");
        ctor.Content.Should().Contain("ILogger<UsersController> logger");
    }

    [Fact]
    public void Default_DoesNotSkip_Mediator_RequestHandler_Without_Config()
    {
        var code = @"
using IoCTools.Abstractions.Annotations;
namespace Mediator { public interface IRequestHandler<TReq, TRes> {} }

namespace Test
{
    public class CreateUser {}
    public class CreateResult {}

    [Scoped]
    public partial class CreateUserHandler : Mediator.IRequestHandler<CreateUser, CreateResult> { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(code);
        result.HasErrors.Should().BeFalse();
        var reg = result.GetServiceRegistrationSource();
        reg!.Content.Should().Contain("CreateUserHandler");
    }

    [Fact]
    public void Default_DoesNotSkip_MediatR_RequestHandler_Without_Config()
    {
        var code = @"
using IoCTools.Abstractions.Annotations;
namespace MediatR { public interface IRequestHandler<TReq, TRes> {} }

namespace Test
{
    public class CreateUser {}
    public class CreateResult {}

    [Scoped]
    public partial class CreateUserHandler : MediatR.IRequestHandler<CreateUser, CreateResult> { }
}
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(code);
        result.HasErrors.Should().BeFalse();
        var reg = result.GetServiceRegistrationSource();
        reg!.Content.Should().Contain("CreateUserHandler");
    }

    [Fact]
    public void Can_Add_Custom_SkipAssignableType_Via_Options()
    {
        var code = @"
using IoCTools.Abstractions.Annotations;
namespace Lib { public abstract class CustomFrameworkBase { } }
namespace Test
{
    public interface IService { }
    [Scoped]
    public partial class Concrete : Lib.CustomFrameworkBase, IService { }
}
";
        code += @"
namespace IoCTools.Generator.Configuration { public static class GeneratorOptions { public const string SkipAssignableTypesAdd = ""Lib.CustomFrameworkBase""; } }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(code);
        result.HasErrors.Should().BeFalse();
        var reg = result.GetServiceRegistrationSource();
        if (reg != null) reg.Content.Should().NotContain("Concrete");
    }

    [Fact]
    public void Can_Remove_Default_Skip_For_ControllerBase()
    {
        var code = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
namespace Microsoft.AspNetCore.Mvc { public abstract class ControllerBase { } }
namespace Test
{
    public interface IService { }
    [Scoped]
    [RegisterAsAll(RegistrationMode.All)]
    public partial class ProductsController : Microsoft.AspNetCore.Mvc.ControllerBase, IService { }
}
";
        code += @"
namespace IoCTools.Generator.Configuration { public static class GeneratorOptions { public const string SkipAssignableTypesRemove = ""Microsoft.AspNetCore.Mvc.ControllerBase""; } }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(code);
        result.HasErrors.Should().BeFalse();
        var reg = result.GetServiceRegistrationSource();
        reg!.Content.Should().Contain("ProductsController");
    }

    [Fact]
    public void Exception_List_Bypasses_Skip()
    {
        var code = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
namespace Microsoft.AspNetCore.Mvc { public abstract class ControllerBase { } }
namespace Test
{
    public interface IService { }
    [Scoped]
    [RegisterAsAll(RegistrationMode.All)]
    public partial class AdminController : Microsoft.AspNetCore.Mvc.ControllerBase, IService { }
}
";
        code += @"
namespace IoCTools.Generator.Configuration { public static class GeneratorOptions { public const string SkipAssignableExceptions = ""Test.AdminController""; } }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(code);
        result.HasErrors.Should().BeFalse();
        var reg = result.GetServiceRegistrationSource();
        reg!.Content.Should().Contain("AdminController");
    }

    [Fact]
    public void Glob_AddPattern_Skips_All_Mediator_Types()
    {
        var code = @"
using IoCTools.Abstractions.Annotations;
namespace Mediator { public interface IRequestHandler<TReq, TRes> {} }

namespace Test
{
    public class Cmd {}
    public class Result {}

    [Scoped]
    public partial class CmdHandler : Mediator.IRequestHandler<Cmd, Result> { }
}
";
        code += @"
namespace IoCTools.Generator.Configuration { public static class GeneratorOptions { public const string SkipAssignableTypesAdd = ""Mediator.*""; } }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(code);
        result.HasErrors.Should().BeFalse();
        var reg = result.GetServiceRegistrationSource();
        if (reg != null) reg.Content.Should().NotContain("CmdHandler");
    }

    [Fact]
    public void Glob_ExceptionForNamespace_Registers_Controllers_Under_Namespace()
    {
        var code = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
namespace Microsoft.AspNetCore.Mvc { public abstract class ControllerBase { } }

namespace Test.Controllers
{
    public interface IService { }
    [Scoped]
    [RegisterAsAll(RegistrationMode.All)]
    public partial class AdminController : Microsoft.AspNetCore.Mvc.ControllerBase, IService { }
}

namespace Test.Other
{
    public interface IService { }
    [Scoped]
    [RegisterAsAll(RegistrationMode.All)]
    public partial class OtherController : Microsoft.AspNetCore.Mvc.ControllerBase, IService { }
}
";
        code += @"
namespace IoCTools.Generator.Configuration { public static class GeneratorOptions { public const string SkipAssignableExceptions = ""Test.Controllers.*""; } }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(code);
        result.HasErrors.Should().BeFalse();
        var reg = result.GetRequiredServiceRegistrationSource();
        reg!.Content.Should().Contain("AdminController");
        reg.Content.Should().NotContain("OtherController");
    }
}
