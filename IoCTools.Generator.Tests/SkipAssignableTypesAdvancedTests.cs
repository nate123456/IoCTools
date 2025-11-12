using static IoCTools.Generator.Tests.SourceGeneratorTestHelper;

public class SkipAssignableTypesAdvancedTests
{
    [Fact]
    public void FullOverride_IgnoresDefaults_RegistersController()
    {
        var code = @"namespace Microsoft.AspNetCore.Mvc { public abstract class ControllerBase { } }
using IoCTools.Abstractions.Annotations; using IoCTools.Abstractions.Enumerations;
namespace Test { [Scoped][RegisterAsAll(RegistrationMode.All)] public partial class MyController : Microsoft.AspNetCore.Mvc.ControllerBase, IService {} public interface IService{} }
namespace IoCTools.Generator.Configuration { public static class GeneratorOptions { public const string SkipAssignableTypes = ""Lib.CustomBase""; } }
";

        var result = CompileWithGenerator(code);
        var reg = result.GetServiceRegistrationSource();
        reg!.Content.Should().Contain("MyController");
    }

    // Note: UseDefaults=false is implicitly covered by the FullOverride test (which blanks defaults by overriding the list).

    [Fact]
    public void RemoveViaGlob_Unskips_All_AspNetControllers()
    {
        var code = @"namespace Microsoft.AspNetCore.Mvc { public abstract class ControllerBase { } }
using IoCTools.Abstractions.Annotations; using IoCTools.Abstractions.Enumerations;
namespace Test { [Scoped][RegisterAsAll(RegistrationMode.All)] public partial class MyController : Microsoft.AspNetCore.Mvc.ControllerBase, IService {} public interface IService{} }
namespace IoCTools.Generator.Configuration { public static class GeneratorOptions { public const string SkipAssignableTypesRemove = ""Microsoft.AspNetCore.Mvc.*""; } }
";

        var result = CompileWithGenerator(code);
        var reg = result.GetServiceRegistrationSource();
        reg!.Content.Should().Contain("MyController");
    }

    [Fact]
    public void SeparatorRobustness_AddsMultipleBases_MixedDelimiters()
    {
        var code = @"
namespace Lib { public abstract class Base1{} public abstract class Base2{} }
using IoCTools.Abstractions.Annotations;
namespace Test { [Scoped] public partial class S1 : Lib.Base1{} [Scoped] public partial class S2 : Lib.Base2{} }
namespace IoCTools.Generator.Configuration { public static class GeneratorOptions { public const string SkipAssignableTypesAdd = ""Lib.Base1; Lib.Base2, Lib.Base3\n Lib.Base4""; } }
";

        var result = CompileWithGenerator(code);
        var reg = result.GetServiceRegistrationSource();
        if (reg != null)
        {
            reg.Content.Should().NotContain("S1");
            reg.Content.Should().NotContain("S2");
        }
    }
}
