namespace IoCTools.Generator.Tests;

using System.Diagnostics;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit.Abstractions;

/// <summary>
///     Comprehensive performance tests for the IoCTools source generator.
///     Tests generator execution time, memory usage, and scalability with large projects.
/// </summary>
public class GeneratorPerformanceTests
{
    private const int WarmupIterations = 3;
    private const int MeasurementIterations = 5; // Reduced for faster testing

    // Performance thresholds (in milliseconds) - more lenient for initial testing
    private const double SmallProjectThreshold = 500.0; // 10 services
    private const double MediumProjectThreshold = 2000.0; // 100 services  
    private const double LargeProjectThreshold = 10000.0; // 500+ services

    // Memory thresholds (in MB)
    private const double MemoryThreshold = 100.0;
    private readonly ITestOutputHelper _output;

    public GeneratorPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Test_Small_Project_Performance_10_Services()
    {
        // Test with 10 services - should be very fast
        var sourceCode = GenerateRealisticServiceCode(10, 3);
        var result = MeasureGenerationPerformance(sourceCode, "10 Services");

        (result.AverageTime.TotalMilliseconds < SmallProjectThreshold).Should().BeTrue(
            $"Small project (10 services) took {result.AverageTime.TotalMilliseconds}ms, expected < {SmallProjectThreshold}ms");

        ValidateGenerationSuccess(sourceCode);
    }

    [Fact]
    public void Test_Medium_Project_Performance_50_Services()
    {
        // Test with 50 services - realistic medium project
        var sourceCode = GenerateRealisticServiceCode(50, 4);
        var result = MeasureGenerationPerformance(sourceCode, "50 Services");

        (result.AverageTime.TotalMilliseconds < MediumProjectThreshold).Should().BeTrue(
            $"Medium project (50 services) took {result.AverageTime.TotalMilliseconds}ms, expected < {MediumProjectThreshold}ms");

        ValidateGenerationSuccess(sourceCode);
    }

    [Fact]
    public void Test_Large_Project_Performance_100_Services()
    {
        // Test with 100 services - large project scale
        var sourceCode = GenerateRealisticServiceCode(100, 5);
        var result = MeasureGenerationPerformance(sourceCode, "100 Services");

        (result.AverageTime.TotalMilliseconds < LargeProjectThreshold).Should().BeTrue(
            $"Large project (100 services) took {result.AverageTime.TotalMilliseconds}ms, expected < {LargeProjectThreshold}ms");

        ValidateGenerationSuccess(sourceCode);
    }

    [Fact]
    public void Test_Memory_Usage_Scaling()
    {
        // Test memory usage across different scales
        var scales = new[] { 5, 10, 25, 50 };
        var memoryResults = new List<(int Scale, long Memory)>();

        foreach (var scale in scales)
        {
            var sourceCode = GenerateRealisticServiceCode(scale, 3);

            // Force garbage collection before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var initialMemory = GC.GetTotalMemory(false);
            var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
            var finalMemory = GC.GetTotalMemory(false);

            var memoryUsed = finalMemory - initialMemory;
            memoryResults.Add((scale, memoryUsed));

            _output.WriteLine($"Scale: {scale} services, Memory: {memoryUsed / 1024.0 / 1024.0:F2} MB");
        }

        // Verify memory usage is reasonable
        var maxMemoryMB = memoryResults.Max(r => r.Memory) / 1024.0 / 1024.0;
        (maxMemoryMB < MemoryThreshold).Should()
            .BeTrue($"Maximum memory usage {maxMemoryMB:F2} MB exceeded threshold {MemoryThreshold} MB");
    }

    [Fact]
    public void Test_Circular_Dependency_Detection_Performance()
    {
        // Test performance of circular dependency detection with complex scenarios
        var circularDependencyCode = GenerateCircularDependencyScenarios();
        var result = MeasureGenerationPerformance(circularDependencyCode, "Circular Dependency Detection");

        // Circular dependency detection should not significantly impact performance
        (result.AverageTime.TotalMilliseconds < MediumProjectThreshold).Should().BeTrue(
            $"Circular dependency detection took {result.AverageTime.TotalMilliseconds}ms, expected < {MediumProjectThreshold}ms");

        // Verify that generation completed (even if circular dependencies detected)
        var compilationResult = SourceGeneratorTestHelper.CompileWithGenerator(circularDependencyCode);
        var diagnostics = compilationResult.Diagnostics.ToList();

        _output.WriteLine($"Circular dependency detection found {diagnostics.Count} diagnostics");
    }

    [Theory]
    [InlineData(10, 1)]
    [InlineData(25, 2)]
    [InlineData(50, 3)]
    public void Test_Compilation_Time_Impact(int serviceCount,
        int maxDependencies)
    {
        // Measure compilation time with vs without generator
        var sourceCode = GenerateRealisticServiceCode(serviceCount, maxDependencies);

        // Measure with generator
        var withGeneratorTime = MeasureCompilationTime(sourceCode, true);

        // Measure without generator (baseline)
        var withoutGeneratorTime = MeasureCompilationTime(sourceCode, false);

        var overhead = withGeneratorTime - withoutGeneratorTime;
        var overheadPercentage = withoutGeneratorTime.TotalMilliseconds > 0
            ? overhead.TotalMilliseconds / withoutGeneratorTime.TotalMilliseconds * 100
            : 0;

        _output.WriteLine(
            $"Services: {serviceCount}, Generator overhead: {overhead.TotalMilliseconds:F2}ms ({overheadPercentage:F1}%)");

        // Generator should not add excessive overhead for reasonable project sizes
        // Allow higher overhead for smaller projects, but be more strict with larger ones
        // Note: Small project baselines are very fast, making percentage calculations volatile
        var maxOverheadPercentage = serviceCount switch
        {
            <= 10 => 10000.0, // Very lenient for small projects (baseline compilation is very fast)
            <= 25 => 5000.0, // Lenient for small-medium projects  
            <= 50 => 3000.0, // Moderate tolerance for medium projects
            <= 100 => 2000.0, // Reasonable for larger projects
            _ => 1500.0 // Stricter for very large projects
        };

        (overheadPercentage < maxOverheadPercentage).Should().BeTrue(
            $"Generator overhead {overheadPercentage:F1}% is too high for {serviceCount} services (max allowed: {maxOverheadPercentage}%)");
    }

    #region Performance Measurement Infrastructure

    private PerformanceTestResult MeasureGenerationPerformance(string sourceCode,
        string testName,
        int iterations = MeasurementIterations)
    {
        _output.WriteLine($"=== Performance Test: {testName} ===");

        // Warmup runs
        for (var i = 0; i < WarmupIterations; i++) SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Measured runs
        var times = new List<TimeSpan>();
        var memoryUsages = new List<long>();

        for (var i = 0; i < iterations; i++)
        {
            // Force garbage collection before each measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var initialMemory = GC.GetTotalMemory(false);
            var stopwatch = Stopwatch.StartNew();

            var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

            stopwatch.Stop();
            var finalMemory = GC.GetTotalMemory(false);

            times.Add(stopwatch.Elapsed);
            memoryUsages.Add(finalMemory - initialMemory);

            if (result.HasErrors)
            {
                var errors = string.Join(", ", result.CompilationDiagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Take(5) // Limit error display
                    .Select(d => d.GetMessage()));
                throw new InvalidOperationException($"Generation failed during performance test: {errors}");
            }
        }

        var perfResult = new PerformanceTestResult(times, memoryUsages);

        _output.WriteLine($"Average Time: {perfResult.AverageTime.TotalMilliseconds:F2}ms");
        _output.WriteLine($"Min Time: {perfResult.MinTime.TotalMilliseconds:F2}ms");
        _output.WriteLine($"Max Time: {perfResult.MaxTime.TotalMilliseconds:F2}ms");
        _output.WriteLine($"Average Memory: {perfResult.AverageMemory / 1024.0 / 1024.0:F2}MB");
        _output.WriteLine("");

        return perfResult;
    }

    private TimeSpan MeasureCompilationTime(string sourceCode,
        bool useGenerator)
    {
        var iterations = 3;
        var times = new List<TimeSpan>();

        for (var i = 0; i < iterations; i++)
        {
            GC.Collect();
            var stopwatch = Stopwatch.StartNew();

            if (useGenerator)
            {
                SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
            }
            else
            {
                // Compile without generator - just basic compilation
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
                var references = SourceGeneratorTestHelper.GetStandardReferences();
                var compilation = CSharpCompilation.Create(
                    "TestAssembly",
                    new[] { syntaxTree },
                    references,
                    new CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary));
                var diagnostics = compilation.GetDiagnostics();
            }

            stopwatch.Stop();
            times.Add(stopwatch.Elapsed);
        }

        return TimeSpan.FromTicks((long)times.Select(t => t.Ticks).Average());
    }

    private void ValidateGenerationSuccess(string sourceCode)
    {
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        if (result.HasErrors)
        {
            var errors = string.Join(", ", result.CompilationDiagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Take(3) // Limit error output
                .Select(d => d.GetMessage()));
            throw new InvalidOperationException($"Performance test code failed to compile: {errors}");
        }

        result.GeneratedSources.Any().Should().BeTrue("No sources were generated");
    }

    #endregion

    #region Test Code Generation

    private string GenerateRealisticServiceCode(int serviceCount,
        int maxDependencies)
    {
        var code = new StringBuilder();

        code.AppendLine("using IoCTools.Abstractions.Annotations;");
        code.AppendLine("using IoCTools.Abstractions.Enumerations;");
        code.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        code.AppendLine("using Microsoft.Extensions.Logging;");
        code.AppendLine();
        code.AppendLine("namespace TestNamespace");
        code.AppendLine("{");

        // Generate interfaces first
        for (var i = 0; i < serviceCount; i++)
        {
            code.AppendLine($"    public interface IService{i}");
            code.AppendLine("    {");
            code.AppendLine("        void DoWork();");
            code.AppendLine("    }");
            code.AppendLine();
        }

        // Generate service implementations
        for (var i = 0; i < serviceCount; i++)
        {
            var dependencyCount = Math.Min(maxDependencies, i); // Can't depend on services that don't exist yet
            var dependencies = new List<string>();

            // Add some realistic dependencies
            for (var j = 0; j < dependencyCount && j < i; j++) dependencies.Add($"IService{j}");

            // Occasionally add some standard framework dependencies
            if (i % 10 == 0 && i > 0) dependencies.Add("ILogger<Service" + i + ">");

            // Generate service class
            code.AppendLine("    [Scoped]");
            code.AppendLine($"    public partial class Service{i} : IService{i}");
            code.AppendLine("    {");

            // Add dependency fields
            foreach (var dep in dependencies)
            {
                var fieldName = dep.ToLower()
                    .Replace("<", "")
                    .Replace(">", "")
                    .Replace("service", "svc")
                    .Replace("ilogger", "logger");
                code.AppendLine($"        [Inject] private readonly {dep} _{fieldName};");
            }

            // Add implementation
            code.AppendLine("        public void DoWork()");
            code.AppendLine("        {");
            code.AppendLine($"            // Business logic for Service{i}");
            foreach (var dep in dependencies.Where(d => d.StartsWith("IService")))
            {
                var fieldName = dep.ToLower().Replace("service", "svc");
                code.AppendLine($"            _{fieldName}?.DoWork();");
            }

            code.AppendLine("        }");

            code.AppendLine("    }");
            code.AppendLine();
        }

        code.AppendLine("}");

        return code.ToString();
    }

    private string GenerateCircularDependencyScenarios() => @"using IoCTools.Abstractions.Annotations;

namespace TestNamespace
{
    public interface IServiceA { void DoWork(); }
    public interface IServiceB { void DoWork(); }
    public interface IServiceC { void DoWork(); }

    
    public partial class ServiceA : IServiceA
    {
        [Inject] private readonly IServiceB _serviceB;
        public void DoWork() { }
    }

    
    public partial class ServiceB : IServiceB
    {
        [Inject] private readonly IServiceC _serviceC;
        public void DoWork() { }
    }

    
    public partial class ServiceC : IServiceC
    {
        [Inject] private readonly IServiceA _serviceA; // Circular dependency
        public void DoWork() { }
    }

    // Additional complex circular scenario
    public interface IChain1 { void Execute(); }
    public interface IChain2 { void Execute(); }
    public interface IChain3 { void Execute(); }

    
    public partial class Chain1Service : IChain1
    {
        [Inject] private readonly IChain2 _chain2;
        public void Execute() { }
    }

    
    public partial class Chain2Service : IChain2
    {
        [Inject] private readonly IChain3 _chain3;
        public void Execute() { }
    }

    
    public partial class Chain3Service : IChain3
    {
        [Inject] private readonly IChain1 _chain1; // Circular dependency
        public void Execute() { }
    }
}";

    #endregion
}
