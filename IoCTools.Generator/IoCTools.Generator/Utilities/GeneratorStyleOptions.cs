namespace IoCTools.Generator.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
///     Generator style options sourced from AnalyzerConfig/MSBuild properties.
/// </summary>
internal sealed class GeneratorStyleOptions
{
    private GeneratorStyleOptions(HashSet<string> skipAssignableTypes,
        HashSet<string> exceptions,
        HashSet<string> skipTypePatterns,
        HashSet<string> exceptionPatterns,
        string implicitLifetime)
    {
        SkipAssignableTypes = skipAssignableTypes;
        SkipAssignableExceptions = exceptions;
        SkipAssignableTypePatterns = skipTypePatterns;
        SkipAssignableExceptionPatterns = exceptionPatterns;
        DefaultImplicitLifetime = implicitLifetime;
    }

    public HashSet<string> SkipAssignableTypes { get; }
    public HashSet<string> SkipAssignableExceptions { get; }
    public HashSet<string> SkipAssignableTypePatterns { get; }
    public HashSet<string> SkipAssignableExceptionPatterns { get; }
    public string DefaultImplicitLifetime { get; }

    public static GeneratorStyleOptions From(AnalyzerConfigOptionsProvider optionsProvider,
        Compilation compilation)
    {
        var opts = optionsProvider.GlobalOptions;

        bool TryGet(string key,
            out string? value)
        {
            if (opts.TryGetValue(key, out value)) return true;
            // Fallback: check per-tree options
            foreach (var tree in compilation.SyntaxTrees)
            {
                var treeOpts = optionsProvider.GetOptions(tree);
                if (treeOpts.TryGetValue(key, out value)) return true;
            }

            value = null;
            return false;
        }

        // Defaults: skip ASP.NET controllers and Mediator/MediatR handler types
        // Use metadata names (include arity for generics)
        var defaults = new HashSet<string>(StringComparer.Ordinal)
        {
            // ASP.NET Core
            "Microsoft.AspNetCore.Mvc.ControllerBase"
        };

        // Toggle default set
        var useDefaults = true;
        if (TryGet("build_property.IoCToolsSkipAssignableTypesUseDefaults", out var defaultsValue))
            if (bool.TryParse(defaultsValue, out var flag))
                useDefaults = flag;

        var working = useDefaults
            ? new HashSet<string>(defaults, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        // Full override list (optional)
        if (TryGet("build_property.IoCToolsSkipAssignableTypes", out var overrideList) &&
            !string.IsNullOrWhiteSpace(overrideList))
            working = new HashSet<string>(SplitTypes(overrideList), StringComparer.Ordinal);

        // Additions
        if (TryGet("build_property.IoCToolsSkipAssignableTypesAdd", out var addList) &&
            !string.IsNullOrWhiteSpace(addList))
            foreach (var t in SplitTypes(addList))
                working.Add(t);

        // Removals
        if (TryGet("build_property.IoCToolsSkipAssignableTypesRemove", out var removeList) &&
            !string.IsNullOrWhiteSpace(removeList))
            foreach (var t in SplitTypes(removeList))
            {
                // Support both fully-qualified and simple names via suffix match removal
                var toRemove = working
                    .Where(x => TypeNameEqualsOrEndsWith(x, t) || (ContainsGlob(t) && Glob.IsMatch(x, t))).ToList();
                foreach (var rm in toRemove) working.Remove(rm);
            }

        var exceptions = new HashSet<string>(StringComparer.Ordinal);
        if (TryGet("build_property.IoCToolsSkipAssignableExceptions", out var exceptionsList) &&
            !string.IsNullOrWhiteSpace(exceptionsList))
            foreach (var t in SplitTypes(exceptionsList))
                exceptions.Add(t);

        // Also support code-based fallback via const fields
        MergeFromCodeConstants(compilation, working, exceptions, ref useDefaults);

        // Partition into exacts vs glob patterns
        var typePatterns = new HashSet<string>(working.Where(ContainsGlob), StringComparer.Ordinal);
        var typeExacts = new HashSet<string>(working.Where(s => !ContainsGlob(s)), StringComparer.Ordinal);

        var exceptionPatterns = new HashSet<string>(exceptions.Where(ContainsGlob), StringComparer.Ordinal);
        var exceptionExacts = new HashSet<string>(exceptions.Where(s => !ContainsGlob(s)), StringComparer.Ordinal);

        var implicitLifetime = GetImplicitLifetime(optionsProvider, compilation);

        return new GeneratorStyleOptions(typeExacts, exceptionExacts, typePatterns, exceptionPatterns,
            implicitLifetime);
    }

    public static string GetImplicitLifetime(AnalyzerConfigOptionsProvider optionsProvider,
        Compilation compilation)
    {
        const string propertyName = "build_property.IoCToolsDefaultServiceLifetime";

        if (TryGetOption(propertyName, optionsProvider, compilation, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            var normalized = value!.Trim();
            return normalized.ToLowerInvariant() switch
            {
                "singleton" => "Singleton",
                "transient" => "Transient",
                "scoped" => "Scoped",
                _ => "Scoped"
            };
        }

        return "Scoped";

        static bool TryGetOption(string key,
            AnalyzerConfigOptionsProvider provider,
            Compilation compilation,
            out string? result)
        {
            if (TryGetCaseInsensitive(provider.GlobalOptions, key, out result)) return true;
            foreach (var tree in compilation.SyntaxTrees)
            {
                var treeOptions = provider.GetOptions(tree);
                if (TryGetCaseInsensitive(treeOptions, key, out result)) return true;
            }

            result = null;
            return false;
        }

        static bool TryGetCaseInsensitive(AnalyzerConfigOptions options,
            string key,
            out string? result)
        {
            if (options.TryGetValue(key, out result)) return true;
            var lowerKey = key.ToLowerInvariant();
            if (!string.Equals(lowerKey, key, StringComparison.Ordinal) && options.TryGetValue(lowerKey, out result))
                return true;
            result = null;
            return false;
        }

    }

    private static IEnumerable<string> SplitTypes(string? value)
    {
        return (value ?? string.Empty)
            .Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0);
    }

    internal static bool TypeNameEqualsOrEndsWith(string left,
        string right)
    {
        if (left.Equals(right, StringComparison.Ordinal)) return true;
        return left.EndsWith("." + right, StringComparison.Ordinal) ||
               left.EndsWith("<" + right + ">", StringComparison.Ordinal) ||
               left.EndsWith("`" + right, StringComparison.Ordinal);
    }

    private static bool ContainsGlob(string s) => s.IndexOf('*') >= 0 || s.IndexOf('?') >= 0;

    private static void MergeFromCodeConstants(Compilation compilation,
        HashSet<string> working,
        HashSet<string> exceptions,
        ref bool useDefaults)
    {
        var optionsType = compilation.GetTypeByMetadataName("IoCTools.Generator.Configuration.GeneratorOptions");
        if (optionsType is null) return;

        string? GetConstString(string name)
        {
            var fld = optionsType.GetMembers(name).OfType<IFieldSymbol>().FirstOrDefault();
            return fld?.IsConst == true ? fld.ConstantValue as string : null;
        }

        bool? GetConstBool(string name)
        {
            var fld = optionsType.GetMembers(name).OfType<IFieldSymbol>().FirstOrDefault();
            if (fld?.IsConst == true && fld.ConstantValue is bool b) return b;
            return null;
        }

        var useDefaultsConst = GetConstBool("SkipAssignableTypesUseDefaults");
        if (useDefaultsConst.HasValue) useDefaults = useDefaultsConst.Value;

        var overrideList = GetConstString("SkipAssignableTypes");
        if (!string.IsNullOrWhiteSpace(overrideList))
        {
            working.Clear();
            foreach (var t in SplitTypes(overrideList!)) working.Add(t);
        }

        var addList = GetConstString("SkipAssignableTypesAdd");
        if (!string.IsNullOrWhiteSpace(addList))
            foreach (var t in SplitTypes(addList!))
                working.Add(t);

        var removeList = GetConstString("SkipAssignableTypesRemove");
        if (!string.IsNullOrWhiteSpace(removeList))
            foreach (var t in SplitTypes(removeList!))
            {
                var toRemove = working
                    .Where(x => TypeNameEqualsOrEndsWith(x, t) || (ContainsGlob(t) && Glob.IsMatch(x, t))).ToList();
                foreach (var rm in toRemove) working.Remove(rm);
            }

        var exceptionsList = GetConstString("SkipAssignableExceptions");
        if (!string.IsNullOrWhiteSpace(exceptionsList))
            foreach (var t in SplitTypes(exceptionsList!))
                exceptions.Add(t);
    }
}
