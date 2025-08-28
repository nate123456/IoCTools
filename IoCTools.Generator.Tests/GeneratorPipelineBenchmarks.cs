namespace IoCTools.Generator.Tests;

using System.Diagnostics;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Xunit.Abstractions;

/// <summary>
///     Detailed benchmarks for individual stages of the IoCTools generator pipeline.
///     Tests performance of specific generator components in isolation.
/// </summary>
public class GeneratorPipelineBenchmarks
{
    private const int DefaultIterations = 10;
    private readonly ITestOutputHelper _output;

    public GeneratorPipelineBenchmarks(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(250)]
    public void Benchmark_Syntax_Provider_Performance(int serviceCount)
    {
        // Test the performance of the syntax provider stage (finding service classes)
        var sourceCode = GenerateServiceCode(serviceCount);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilation = CreateTestCompilation(syntaxTree);

        var times = new List<TimeSpan>();

        for (var i = 0; i < DefaultIterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();

            // Simulate what the syntax provider does
            var serviceClasses = compilation.SyntaxTrees
                .SelectMany(tree => tree.GetRoot().DescendantNodes())
                .OfType<TypeDeclarationSyntax>()
                .Select(node => compilation.GetSemanticModel(node.SyntaxTree).GetDeclaredSymbol(node))
                .OfType<INamedTypeSymbol>()
                .Where(symbol => HasLifetimeAttribute(symbol))
                .ToList();

            stopwatch.Stop();
            times.Add(stopwatch.Elapsed);
        }

        var avgTime = TimeSpan.FromTicks((long)times.Select(t => t.Ticks).Average());
        var throughput = serviceCount / avgTime.TotalSeconds;

        _output.WriteLine(
            $"Syntax Provider ({serviceCount} services): {avgTime.TotalMilliseconds:F2}ms, {throughput:F0} services/sec");

        // Performance should scale reasonably - account for compilation setup overhead
        var maxAllowedTime = serviceCount switch
        {
            <= 10 => 50.0, // Base overhead for small projects
            <= 50 => serviceCount * 2.0, // 2ms per service for medium
            _ => serviceCount * 1.0 // 1ms per service for large projects (realistic for compilation overhead)
        };
        Assert.True(avgTime.TotalMilliseconds < maxAllowedTime,
            $"Syntax provider too slow: {avgTime.TotalMilliseconds:F2}ms for {serviceCount} services (limit: {maxAllowedTime:F2}ms)");
    }

    [Theory]
    [InlineData(5, 3)] // 5 services, max 3 dependencies each
    [InlineData(20, 5)] // 20 services, max 5 dependencies each
    [InlineData(50, 8)] // 50 services, max 8 dependencies each
    public void Benchmark_Dependency_Analysis_Performance(int serviceCount,
        int maxDependencies)
    {
        // Test the performance of dependency analysis
        var sourceCode = GenerateServiceCodeWithDependencies(serviceCount, maxDependencies);
        var compilation = CompileCode(sourceCode);

        var times = new List<TimeSpan>();

        for (var i = 0; i < DefaultIterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();

            // Simulate dependency analysis
            var services = FindServiceClasses(compilation);
            var dependencyMap =
                new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

            foreach (var service in services)
            {
                var dependencies = GetServiceDependencies(service);
                dependencyMap[service] = dependencies;
            }

            stopwatch.Stop();
            times.Add(stopwatch.Elapsed);
        }

        var avgTime = TimeSpan.FromTicks((long)times.Select(t => t.Ticks).Average());
        var totalDependencies = serviceCount * maxDependencies / 2; // Estimate
        var throughput = totalDependencies / avgTime.TotalSeconds;

        _output.WriteLine(
            $"Dependency Analysis ({serviceCount} services, ~{totalDependencies} deps): {avgTime.TotalMilliseconds:F2}ms, {throughput:F0} deps/sec");

        // Should handle dependencies efficiently - account for setup overhead
        var baseCost = serviceCount * maxDependencies;
        var maxAllowedTime = baseCost switch
        {
            <= 20 => 50.0, // Base overhead for small scenarios
            <= 100 => baseCost * 1.0, // 1ms per potential dependency for medium
            _ => baseCost * 0.5 // 0.5ms per potential dependency for large
        };
        Assert.True(avgTime.TotalMilliseconds < maxAllowedTime,
            $"Dependency analysis too slow: {avgTime.TotalMilliseconds:F2}ms (limit: {maxAllowedTime:F2}ms)");
    }

    [Theory]
    [InlineData(3, 5)] // 3 levels, 5 services per level
    [InlineData(5, 3)] // 5 levels, 3 services per level  
    [InlineData(7, 2)] // 7 levels, 2 services per level
    public void Benchmark_Inheritance_Analysis_Performance(int inheritanceLevels,
        int servicesPerLevel)
    {
        // Test performance of inheritance hierarchy analysis
        var sourceCode = GenerateInheritanceHierarchy(inheritanceLevels, servicesPerLevel);
        var compilation = CompileCode(sourceCode);

        var times = new List<TimeSpan>();

        for (var i = 0; i < DefaultIterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();

            // Simulate inheritance analysis
            var services = FindServiceClasses(compilation);
            var hierarchies = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

            foreach (var service in services)
            {
                var hierarchy = new List<INamedTypeSymbol>();
                var currentType = service.BaseType;
                while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
                {
                    hierarchy.Add(currentType);
                    currentType = currentType.BaseType;
                }

                hierarchies[service] = hierarchy;
            }

            stopwatch.Stop();
            times.Add(stopwatch.Elapsed);
        }

        var avgTime = TimeSpan.FromTicks((long)times.Select(t => t.Ticks).Average());
        var totalServices = inheritanceLevels * servicesPerLevel;
        var throughput = totalServices / avgTime.TotalSeconds;

        _output.WriteLine(
            $"Inheritance Analysis ({inheritanceLevels} levels, {servicesPerLevel} per level): {avgTime.TotalMilliseconds:F2}ms, {throughput:F0} services/sec");

        // Should handle inheritance efficiently - account for compilation overhead
        var maxAllowedTime =
            totalServices * inheritanceLevels * 1.0; // 1ms per service per level (realistic for compilation overhead)
        Assert.True(avgTime.TotalMilliseconds < maxAllowedTime,
            $"Inheritance analysis too slow: {avgTime.TotalMilliseconds:F2}ms (limit: {maxAllowedTime:F2}ms)");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void Benchmark_Constructor_Generation_Performance(int serviceCount)
    {
        // Test performance of constructor generation
        var sourceCode = GenerateServiceCodeWithInjectFields(serviceCount);
        var compilation = CompileCode(sourceCode);

        var times = new List<TimeSpan>();
        var generatedCodeLength = 0;

        for (var i = 0; i < DefaultIterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();

            // Simulate constructor generation
            var services = FindServiceClasses(compilation);
            var generatedCode = new StringBuilder();

            foreach (var service in services)
            {
                var injectFields = GetInjectFields(service);
                if (injectFields.Any())
                {
                    generatedCode.AppendLine($"// Constructor for {service.Name}");
                    generatedCode.AppendLine($"public {service.Name}(");

                    for (var j = 0; j < injectFields.Count; j++)
                    {
                        var field = injectFields[j];
                        generatedCode.AppendLine($"    {field.Type.Name} param{j}");
                        if (j < injectFields.Count - 1) generatedCode.Append(",");
                    }

                    generatedCode.AppendLine(")");
                    generatedCode.AppendLine("{");

                    for (var j = 0; j < injectFields.Count; j++)
                    {
                        var field = injectFields[j];
                        generatedCode.AppendLine($"    {field.Name} = param{j};");
                    }

                    generatedCode.AppendLine("}");
                }
            }

            stopwatch.Stop();
            times.Add(stopwatch.Elapsed);
            generatedCodeLength = generatedCode.Length;
        }

        var avgTime = TimeSpan.FromTicks((long)times.Select(t => t.Ticks).Average());
        var throughput = serviceCount / avgTime.TotalSeconds;
        var codeGenerationRate = generatedCodeLength / avgTime.TotalSeconds;

        _output.WriteLine($"Constructor Generation ({serviceCount} services): {avgTime.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Throughput: {throughput:F0} services/sec, {codeGenerationRate:F0} chars/sec");

        // Code generation should be reasonably fast - account for compilation overhead
        var maxAllowedTime = serviceCount switch
        {
            <= 10 => 50.0, // Base overhead for small projects
            <= 50 => serviceCount * 2.0, // 2ms per service for medium
            _ => serviceCount * 1.0 // 1ms per service for large
        };
        Assert.True(avgTime.TotalMilliseconds < maxAllowedTime,
            $"Constructor generation too slow: {avgTime.TotalMilliseconds:F2}ms (limit: {maxAllowedTime:F2}ms)");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void Benchmark_Service_Registration_Generation_Performance(int serviceCount)
    {
        // Test performance of service registration method generation
        var sourceCode = GenerateServiceCode(serviceCount);
        var compilation = CompileCode(sourceCode);

        var times = new List<TimeSpan>();

        for (var i = 0; i < DefaultIterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();

            // Simulate service registration generation
            var services = FindServiceClasses(compilation);
            var registrationCode = new StringBuilder();

            registrationCode.AppendLine("public static class ServiceCollectionExtensions");
            registrationCode.AppendLine("{");
            registrationCode.AppendLine(
                "    public static IServiceCollection AddGeneratedServices(this IServiceCollection services)");
            registrationCode.AppendLine("    {");

            foreach (var service in services)
            {
                var interfaces = service.Interfaces;
                if (interfaces.Any())
                    foreach (var iface in interfaces)
                        registrationCode.AppendLine($"        services.AddScoped<{iface.Name}, {service.Name}>();");
                else
                    registrationCode.AppendLine($"        services.AddScoped<{service.Name}>();");
            }

            registrationCode.AppendLine("        return services;");
            registrationCode.AppendLine("    }");
            registrationCode.AppendLine("}");

            stopwatch.Stop();
            times.Add(stopwatch.Elapsed);
        }

        var avgTime = TimeSpan.FromTicks((long)times.Select(t => t.Ticks).Average());
        var throughput = serviceCount / avgTime.TotalSeconds;

        _output.WriteLine(
            $"Service Registration Generation ({serviceCount} services): {avgTime.TotalMilliseconds:F2}ms, {throughput:F0} services/sec");

        // Registration generation should be reasonably fast - account for compilation overhead
        var maxAllowedTime = serviceCount switch
        {
            <= 10 => 50.0, // Base overhead for small projects
            <= 50 => serviceCount * 2.0, // 2ms per service for medium
            _ => serviceCount * 1.0 // 1ms per service for large
        };
        Assert.True(avgTime.TotalMilliseconds < maxAllowedTime,
            $"Service registration generation too slow: {avgTime.TotalMilliseconds:F2}ms for {serviceCount} services (limit: {maxAllowedTime:F2}ms)");
    }

    [Theory]
    [InlineData(5, 3)] // 5 services in circular dependency
    [InlineData(10, 5)] // 10 services, max chain length 5
    [InlineData(20, 8)] // 20 services, max chain length 8
    public void Benchmark_Circular_Dependency_Detection_Performance(int serviceCount,
        int maxChainLength)
    {
        // Test performance of circular dependency detection algorithms
        var sourceCode = GenerateCircularDependencyScenario(serviceCount, maxChainLength);
        var compilation = CompileCode(sourceCode);

        var times = new List<TimeSpan>();

        for (var i = 0; i < DefaultIterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();

            // Simulate circular dependency detection
            var services = FindServiceClasses(compilation);
            var dependencyMap =
                new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

            foreach (var service in services) dependencyMap[service] = GetServiceDependencies(service);

            // Detect cycles using DFS
            var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var recursionStack = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            var cycles = new List<List<INamedTypeSymbol>>();

            bool DetectCycle(INamedTypeSymbol service,
                List<INamedTypeSymbol> path)
            {
                if (recursionStack.Contains(service))
                {
                    var cycleStart = path.IndexOf(service);
                    cycles.Add(path.Skip(cycleStart).ToList());
                    return true;
                }

                if (visited.Contains(service)) return false;

                visited.Add(service);
                recursionStack.Add(service);
                path.Add(service);

                if (dependencyMap.TryGetValue(service, out var dependencies))
                    foreach (var dependency in dependencies)
                        DetectCycle(dependency, path);

                recursionStack.Remove(service);
                path.RemoveAt(path.Count - 1);
                return false;
            }

            foreach (var service in services)
                if (!visited.Contains(service))
                    DetectCycle(service, new List<INamedTypeSymbol>());

            stopwatch.Stop();
            times.Add(stopwatch.Elapsed);
        }

        var avgTime = TimeSpan.FromTicks((long)times.Select(t => t.Ticks).Average());
        var throughput = serviceCount / avgTime.TotalSeconds;

        _output.WriteLine(
            $"Circular Dependency Detection ({serviceCount} services, max chain {maxChainLength}): {avgTime.TotalMilliseconds:F2}ms, {throughput:F0} services/sec");

        // Circular dependency detection should be efficient even for complex scenarios
        // Account for compilation setup overhead in small test scenarios
        var baseCost = serviceCount * maxChainLength;
        var maxAllowedTime = baseCost switch
        {
            <= 20 => 50.0, // Base overhead for small scenarios (compilation setup)
            <= 100 => baseCost * 1.0, // 1ms per service-chain combination for medium
            _ => baseCost * 0.2 // 0.2ms per service-chain combination for large
        };
        Assert.True(avgTime.TotalMilliseconds < maxAllowedTime,
            $"Circular dependency detection too slow: {avgTime.TotalMilliseconds:F2}ms (limit: {maxAllowedTime:F2}ms)");
    }

    [Fact]
    public void Benchmark_Full_Pipeline_vs_Individual_Stages()
    {
        // Compare full pipeline performance vs sum of individual stages
        var serviceCount = 50;
        var sourceCode = GenerateComplexServiceCode(serviceCount);

        // Measure full pipeline
        var fullPipelineTime = MeasureFullPipeline(sourceCode);

        // Measure individual stages
        var compilation = CompileCode(sourceCode);
        var syntaxProviderTime = MeasureSyntaxProvider(compilation);
        var dependencyAnalysisTime = MeasureDependencyAnalysis(compilation);
        var codeGenerationTime = MeasureCodeGeneration(compilation);

        var individualStagesTotal = syntaxProviderTime + dependencyAnalysisTime + codeGenerationTime;

        _output.WriteLine($"Pipeline Breakdown ({serviceCount} services):");
        _output.WriteLine($"  Syntax Provider: {syntaxProviderTime.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Dependency Analysis: {dependencyAnalysisTime.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Code Generation: {codeGenerationTime.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Individual Total: {individualStagesTotal.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Full Pipeline: {fullPipelineTime.TotalMilliseconds:F2}ms");
        _output.WriteLine($"  Pipeline Overhead: {(fullPipelineTime - individualStagesTotal).TotalMilliseconds:F2}ms");

        // Pipeline overhead should be reasonable - account for compilation and generator setup costs
        // Full pipeline includes compilation setup, generator driver initialization, etc.
        // However, the comparison is inherently unfair since individual stages work on pre-compiled objects
        // while full pipeline includes complete compilation from source code.

        // Focus on absolute performance rather than relative overhead
        // The individual stages are measured on an already-compiled object, while full pipeline
        // includes syntax parsing, metadata reference loading, and compilation driver setup
        var fullPipelineMs = fullPipelineTime.TotalMilliseconds;
        var individualMs = individualStagesTotal.TotalMilliseconds;

        // Set realistic absolute performance limits based on compilation complexity
        var maxAllowedFullPipelineTime = serviceCount switch
        {
            <= 10 => 5000.0, // 5 seconds for small projects (includes compilation setup)
            <= 50 => 10000.0, // 10 seconds for medium projects
            _ => 20000.0 // 20 seconds for large projects
        };

        // Ensure individual stages remain efficient (these should be fast since they work on compiled objects)
        var maxAllowedIndividualTime = serviceCount switch
        {
            <= 10 => 100.0, // 100ms for individual stages on small projects
            <= 50 => 200.0, // 200ms for individual stages on medium projects  
            _ => 500.0 // 500ms for individual stages on large projects
        };

        Assert.True(fullPipelineMs < maxAllowedFullPipelineTime,
            $"Full pipeline too slow: {fullPipelineMs:F1}ms (limit: {maxAllowedFullPipelineTime:F1}ms)");
        Assert.True(individualMs < maxAllowedIndividualTime,
            $"Individual stages too slow: {individualMs:F1}ms (limit: {maxAllowedIndividualTime:F1}ms)");
    }

    #region Helper Methods

    private Compilation CompileCode(string sourceCode)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        return CreateTestCompilation(syntaxTree);
    }

    private Compilation CreateTestCompilation(SyntaxTree syntaxTree)
    {
        var references = SourceGeneratorTestHelper.GetStandardReferences();
        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private List<INamedTypeSymbol> FindServiceClasses(Compilation compilation)
    {
        return compilation.SyntaxTrees
            .SelectMany(tree => tree.GetRoot().DescendantNodes())
            .OfType<TypeDeclarationSyntax>()
            .Select(node => compilation.GetSemanticModel(node.SyntaxTree).GetDeclaredSymbol(node))
            .OfType<INamedTypeSymbol>()
            .Where(HasLifetimeAttribute)
            .ToList();
    }

    private bool HasLifetimeAttribute(INamedTypeSymbol symbol)
    {
        return symbol.GetAttributes().Any(attr =>
            attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ScopedAttribute" ||
            attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.SingletonAttribute" ||
            attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.TransientAttribute");
    }

    private List<INamedTypeSymbol> GetServiceDependencies(INamedTypeSymbol service)
    {
        var dependencies = new List<INamedTypeSymbol>();

        foreach (var field in service.GetMembers().OfType<IFieldSymbol>())
            if (field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute"))
                if (field.Type is INamedTypeSymbol dependencyType)
                    dependencies.Add(dependencyType);

        return dependencies;
    }

    private List<IFieldSymbol> GetInjectFields(INamedTypeSymbol service)
    {
        return service.GetMembers().OfType<IFieldSymbol>()
            .Where(field => field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute"))
            .ToList();
    }

    private TimeSpan MeasureFullPipeline(string sourceCode)
    {
        var times = new List<TimeSpan>();

        for (var i = 0; i < 5; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
            stopwatch.Stop();
            times.Add(stopwatch.Elapsed);
        }

        return TimeSpan.FromTicks((long)times.Select(t => t.Ticks).Average());
    }

    private TimeSpan MeasureSyntaxProvider(Compilation compilation)
    {
        var stopwatch = Stopwatch.StartNew();
        var services = FindServiceClasses(compilation);
        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private TimeSpan MeasureDependencyAnalysis(Compilation compilation)
    {
        var services = FindServiceClasses(compilation);
        var stopwatch = Stopwatch.StartNew();

        foreach (var service in services) GetServiceDependencies(service);

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    private TimeSpan MeasureCodeGeneration(Compilation compilation)
    {
        var services = FindServiceClasses(compilation);
        var stopwatch = Stopwatch.StartNew();

        var code = new StringBuilder();
        foreach (var service in services)
        {
            var fields = GetInjectFields(service);
            code.AppendLine($"// Generated code for {service.Name}");
            foreach (var field in fields) code.AppendLine($"    {field.Type.Name} {field.Name};");
        }

        stopwatch.Stop();
        return stopwatch.Elapsed;
    }

    #endregion

    #region Test Code Generation

    private string GenerateServiceCode(int count)
    {
        var code = new StringBuilder();
        code.AppendLine("using IoCTools.Abstractions.Annotations;");
        code.AppendLine("namespace TestNamespace;");
        code.AppendLine();

        for (var i = 0; i < count; i++)
        {
            code.AppendLine($"public interface IService{i} {{ }}");
            code.AppendLine($"[Scoped] public partial class Service{i} : IService{i} {{ }}");
            code.AppendLine();
        }

        return code.ToString();
    }

    private string GenerateServiceCodeWithDependencies(int count,
        int maxDependencies)
    {
        var code = new StringBuilder();
        code.AppendLine("using IoCTools.Abstractions.Annotations;");
        code.AppendLine("namespace TestNamespace;");
        code.AppendLine();

        for (var i = 0; i < count; i++)
        {
            code.AppendLine($"public interface IService{i} {{ }}");
            code.AppendLine($"[Scoped] public partial class Service{i} : IService{i}");
            code.AppendLine("{");

            var depCount = Math.Min(maxDependencies, i);
            for (var j = 0; j < depCount; j++) code.AppendLine($"    [Inject] private readonly IService{j} _dep{j};");

            code.AppendLine("}");
            code.AppendLine();
        }

        return code.ToString();
    }

    private string GenerateServiceCodeWithInjectFields(int count)
    {
        var code = new StringBuilder();
        code.AppendLine("using IoCTools.Abstractions.Annotations;");
        code.AppendLine("namespace TestNamespace;");
        code.AppendLine();

        for (var i = 0; i < count; i++)
        {
            code.AppendLine($"public interface IService{i} {{ }}");
            code.AppendLine($"[Scoped] public partial class Service{i} : IService{i}");
            code.AppendLine("{");

            // Add 2-3 inject fields per service
            var fieldCount = 2 + i % 2;
            for (var j = 0; j < fieldCount && j < i; j++)
                code.AppendLine($"    [Inject] private readonly IService{j} _field{j};");

            code.AppendLine("}");
            code.AppendLine();
        }

        return code.ToString();
    }

    private string GenerateInheritanceHierarchy(int levels,
        int servicesPerLevel)
    {
        var code = new StringBuilder();
        code.AppendLine("using IoCTools.Abstractions.Annotations;");
        code.AppendLine("namespace TestNamespace;");
        code.AppendLine();

        for (var level = 0; level < levels; level++)
        for (var service = 0; service < servicesPerLevel; service++)
        {
            var baseClass = level == 0 ? "" : $" : BaseLevel{level - 1}Service{service % servicesPerLevel}";
            code.AppendLine($"[Scoped] public partial class BaseLevel{level}Service{service}{baseClass}");
            code.AppendLine("{");
            code.AppendLine($"    public virtual string GetLevel() => \"Level{level}\";");
            code.AppendLine("}");
            code.AppendLine();
        }

        return code.ToString();
    }

    private string GenerateCircularDependencyScenario(int serviceCount,
        int maxChainLength)
    {
        var code = new StringBuilder();
        code.AppendLine("using IoCTools.Abstractions.Annotations;");
        code.AppendLine("namespace TestNamespace;");
        code.AppendLine();

        for (var i = 0; i < serviceCount; i++)
        {
            code.AppendLine($"public interface ICircularService{i} {{ }}");
            code.AppendLine($"[Scoped] public partial class CircularService{i} : ICircularService{i}");
            code.AppendLine("{");

            // Create dependency chains that may form cycles
            var nextService = (i + 1) % Math.Min(maxChainLength, serviceCount);
            if (nextService != i)
                code.AppendLine($"    [Inject] private readonly ICircularService{nextService} _next;");

            code.AppendLine("}");
            code.AppendLine();
        }

        return code.ToString();
    }

    private string GenerateComplexServiceCode(int count)
    {
        var code = new StringBuilder();
        code.AppendLine("using IoCTools.Abstractions.Annotations;");
        code.AppendLine("using Microsoft.Extensions.Logging;");
        code.AppendLine("namespace TestNamespace;");
        code.AppendLine();

        for (var i = 0; i < count; i++)
        {
            code.AppendLine($"public interface IService{i} {{ }}");
            code.AppendLine($"[Scoped] public partial class Service{i} : IService{i}");
            code.AppendLine("{");

            // Add various types of dependencies
            if (i > 0) code.AppendLine($"    [Inject] private readonly IService{i - 1} _previous;");
            if (i % 3 == 0) code.AppendLine($"    [Inject] private readonly ILogger<Service{i}> _logger;");
            if (i % 5 == 0 && i > 4) code.AppendLine($"    [Inject] private readonly IService{i - 5} _distant;");

            code.AppendLine("}");
            code.AppendLine();
        }

        return code.ToString();
    }

    #endregion
}
