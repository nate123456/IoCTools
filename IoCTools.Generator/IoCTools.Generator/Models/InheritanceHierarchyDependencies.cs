namespace IoCTools.Generator.Models;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

internal class InheritanceHierarchyDependencies(
    List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> allDependencies,
    List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> baseDependencies,
    List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> derivedDependencies,
    List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)> rawAllDependencies,
    List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal)>
        allDependenciesWithExternalFlag)
{
    public List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> AllDependencies { get; } =
        allDependencies;

    public List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> BaseDependencies { get; } =
        baseDependencies;

    public List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> DerivedDependencies { get; } =
        derivedDependencies;

    public List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)> RawAllDependencies
    {
        get;
    } = rawAllDependencies;

    public List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal)>
        AllDependenciesWithExternalFlag
    { get; } = allDependenciesWithExternalFlag;

    /// <summary>
    ///     Gets inheritance chain lifetime analysis data for comprehensive lifetime validation
    /// </summary>
    public InheritanceChainLifetimeAnalysis GetLifetimeAnalysis(Dictionary<string, string> serviceLifetimes) =>
        new(RawAllDependencies, AllDependenciesWithExternalFlag, serviceLifetimes);
}

/// <summary>
///     Provides comprehensive lifetime analysis for inheritance chains
/// </summary>
internal class InheritanceChainLifetimeAnalysis
{
    private readonly List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal)>
        _dependenciesWithExternalFlag;

    private readonly List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)>
        _rawDependencies;

    private readonly Dictionary<string, string> _serviceLifetimes;

    public InheritanceChainLifetimeAnalysis(
        List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)> rawDependencies,
        List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal)>
            dependenciesWithExternalFlag,
        Dictionary<string, string> serviceLifetimes)
    {
        _rawDependencies = rawDependencies;
        _dependenciesWithExternalFlag = dependenciesWithExternalFlag;
        _serviceLifetimes = serviceLifetimes;
    }

    /// <summary>
    ///     Gets dependencies at a specific inheritance level
    /// </summary>
    public List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)> GetDependenciesAtLevel(int level)
    {
        return _rawDependencies
            .Where(d => d.Level == level)
            .Select(d => (d.ServiceType, d.FieldName, d.Source))
            .ToList();
    }

    /// <summary>
    ///     Gets all inheritance levels that have dependencies
    /// </summary>
    public List<int> GetInheritanceLevels()
    {
        return _rawDependencies
            .Select(d => d.Level)
            .Distinct()
            .OrderBy(level => level)
            .ToList();
    }

    /// <summary>
    ///     Gets the deepest inheritance level
    /// </summary>
    public int GetMaxInheritanceLevel() => _rawDependencies.Any() ? _rawDependencies.Max(d => d.Level) : 0;

    /// <summary>
    ///     Analyzes lifetime compatibility across the entire inheritance chain
    /// </summary>
    public InheritanceLifetimeCompatibilityResult AnalyzeLifetimeCompatibility(string serviceLifetime)
    {
        var violations = new List<InheritanceLifetimeViolation>();
        var processedDependencies = new HashSet<string>();

        // Only analyze if the service is Singleton (most restrictive)
        if (serviceLifetime != "Singleton") return new InheritanceLifetimeCompatibilityResult(violations, false);

        // Group dependencies by level to analyze inheritance chain
        var dependenciesByLevel = _rawDependencies
            .GroupBy(d => d.Level)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var levelGroup in dependenciesByLevel)
        {
            var level = levelGroup.Key;

            foreach (var dependency in levelGroup)
            {
                var dependencyKey = $"{dependency.ServiceType.ToDisplayString()}_{dependency.FieldName}";

                // Skip if already processed (deduplication)
                if (processedDependencies.Contains(dependencyKey)) continue;
                processedDependencies.Add(dependencyKey);

                // Check if this dependency is external
                var isExternal = _dependenciesWithExternalFlag
                    .Any(d => SymbolEqualityComparer.Default.Equals(d.ServiceType, dependency.ServiceType)
                              && d.FieldName == dependency.FieldName
                              && d.IsExternal);

                if (isExternal) continue;

                // Get dependency lifetime
                var dependencyLifetime = GetDependencyLifetime(dependency.ServiceType);
                if (dependencyLifetime == null) continue;

                // Check for lifetime violations
                if (IsLifetimeIncompatible(serviceLifetime, dependencyLifetime))
                    violations.Add(new InheritanceLifetimeViolation(
                        dependency.ServiceType,
                        dependency.FieldName,
                        dependency.Source,
                        level,
                        dependencyLifetime,
                        GetViolationType(serviceLifetime, dependencyLifetime)
                    ));
            }
        }

        var hasViolations = violations.Any();
        return new InheritanceLifetimeCompatibilityResult(violations, hasViolations);
    }

    /// <summary>
    ///     Gets accumulated dependencies across all inheritance levels with lifetime information
    /// </summary>
    public List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level, string? Lifetime)>
        GetAccumulatedDependenciesWithLifetimes()
    {
        return _rawDependencies
            .Select(d => (d.ServiceType, d.FieldName, d.Source, d.Level, GetDependencyLifetime(d.ServiceType)))
            .ToList();
    }

    private string? GetDependencyLifetime(ITypeSymbol dependencyType)
    {
        var dependencyTypeName = dependencyType.ToDisplayString();
        return _serviceLifetimes.TryGetValue(dependencyTypeName, out var lifetime) ? lifetime : null;
    }

    private static bool IsLifetimeIncompatible(string serviceLifetime,
        string dependencyLifetime) => serviceLifetime == "Singleton" &&
                                      (dependencyLifetime == "Scoped" || dependencyLifetime == "Transient");

    private static LifetimeViolationType GetViolationType(string serviceLifetime,
        string dependencyLifetime)
    {
        if (serviceLifetime == "Singleton" && dependencyLifetime == "Scoped")
            return LifetimeViolationType.SingletonDependsOnScoped;

        if (serviceLifetime == "Singleton" && dependencyLifetime == "Transient")
            return LifetimeViolationType.SingletonDependsOnTransient;

        return LifetimeViolationType.Other;
    }
}

/// <summary>
///     Result of inheritance chain lifetime compatibility analysis
/// </summary>
internal class InheritanceLifetimeCompatibilityResult
{
    public InheritanceLifetimeCompatibilityResult(List<InheritanceLifetimeViolation> violations,
        bool hasViolations)
    {
        Violations = violations;
        HasViolations = hasViolations;
    }

    public List<InheritanceLifetimeViolation> Violations { get; }
    public bool HasViolations { get; }

    /// <summary>
    ///     Gets violations grouped by lifetime type
    /// </summary>
    public Dictionary<LifetimeViolationType, List<InheritanceLifetimeViolation>> GetViolationsByType()
    {
        return Violations.GroupBy(v => v.ViolationType)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    ///     Gets the most restrictive violation type (Error > Warning)
    /// </summary>
    public LifetimeViolationType? GetMostRestrictiveViolationType()
    {
        if (!HasViolations) return null;

        // Scoped violations are errors, Transient violations are warnings
        if (Violations.Any(v => v.ViolationType == LifetimeViolationType.SingletonDependsOnScoped))
            return LifetimeViolationType.SingletonDependsOnScoped;

        if (Violations.Any(v => v.ViolationType == LifetimeViolationType.SingletonDependsOnTransient))
            return LifetimeViolationType.SingletonDependsOnTransient;

        return Violations.First().ViolationType;
    }
}

/// <summary>
///     Represents a lifetime violation in an inheritance chain
/// </summary>
internal class InheritanceLifetimeViolation
{
    public InheritanceLifetimeViolation(ITypeSymbol dependencyType,
        string fieldName,
        DependencySource source,
        int inheritanceLevel,
        string dependencyLifetime,
        LifetimeViolationType violationType)
    {
        DependencyType = dependencyType;
        FieldName = fieldName;
        Source = source;
        InheritanceLevel = inheritanceLevel;
        DependencyLifetime = dependencyLifetime;
        ViolationType = violationType;
    }

    public ITypeSymbol DependencyType { get; }
    public string FieldName { get; }
    public DependencySource Source { get; }
    public int InheritanceLevel { get; }
    public string DependencyLifetime { get; }
    public LifetimeViolationType ViolationType { get; }

    /// <summary>
    ///     Gets a formatted name for the dependency for diagnostic messages
    /// </summary>
    public string GetFormattedDependencyName()
    {
        if (DependencyType is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var typeName = namedType.Name;
            var typeArgs = namedType.TypeArguments.Select(arg => arg.Name).ToArray();

            if (typeArgs.Length > 0) return $"{typeName}<{string.Join(", ", typeArgs)}>";
        }

        return DependencyType.Name;
    }
}

/// <summary>
///     Types of lifetime violations
/// </summary>
internal enum LifetimeViolationType
{
    SingletonDependsOnScoped,
    SingletonDependsOnTransient,
    Other
}
