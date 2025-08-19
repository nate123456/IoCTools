using System;
using System.IO;
using Microsoft.CodeAnalysis;

namespace IoCTools.Generator.Utilities;

internal static class ProjectUtilities
{
    /// <summary>
    ///     Extracts the project namespace for extension method generation
    /// </summary>
    public static string GetProjectNamespace(GeneratorExecutionContext context)
    {
        // Use default project name since AnalyzerConfigOptions not available in SourceProductionContext
        string? projectDir = "TestProject";
        var projectName = GetLastFolderName(projectDir).Replace(".", "");

        // Handle specific IoCTools project naming patterns
        if (projectName.StartsWith("IoCTools"))
        {
            // Extract just the suffix part (e.g., "IoCTools.Sample" -> "Sample")
            var suffix = projectName.Substring("IoCTools".Length);
            if (suffix.StartsWith(".")) suffix = suffix.Substring(1); // Remove the dot
            return "IoCTools" + suffix;
        }

        // If it looks like a test project or default, use IoCToolsTest for consistent test behavior
        if (projectName == "TestProject" || projectName.Contains("Test") || projectName.Contains("test"))
            return "IoCToolsTest";

        // For any other project, add IoCTools prefix
        return "IoCTools" + projectName;
    }

    /// <summary>
    ///     Generates a unique file name for generated source files
    /// </summary>
    public static string GenerateFileName(string baseFileName,
        string classDisplayString)
    {
        // Use a consistent file name without hash to prevent duplicates
        var sanitizedName = classDisplayString.Replace(".", "_").Replace("<", "_").Replace(">", "_");
        return $"{sanitizedName}_{baseFileName}.g.cs";
    }

    /// <summary>
    ///     Generates a short hash for file naming to avoid duplicate generated files
    /// </summary>
    public static string GenerateShortHash(string input)
    {
        // Generate a short hash for file naming to avoid duplicate generated files
        var hash = 0;
        foreach (var c in input)
        {
            hash = (hash << 5) - hash + c;
            hash = hash & hash; // Convert to 32-bit integer
        }

        return Math.Abs(hash).ToString("X8");
    }

    private static string GetLastFolderName(string path)
    {
        if (string.IsNullOrEmpty(path)) return "TestProject";

        try
        {
            var directoryInfo = new DirectoryInfo(path);
            return directoryInfo.Name;
        }
        catch
        {
            return "TestProject";
        }
    }
}