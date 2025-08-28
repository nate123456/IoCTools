namespace IoCTools.Generator.CodeGeneration;

using System.Collections.Generic;
using System.Linq;

using Analysis;

using Microsoft.CodeAnalysis;

using Models;

using Utilities;

internal static partial class ConstructorGenerator
{
    private static List<string> GenerateConfigurationAssignments(INamedTypeSymbol? classSymbol,
        SemanticModel semanticModel,
        HashSet<string> namespacesForStripping,
        string configurationParameterName = "configuration")
    {
        var assignments = new List<string>();
        if (classSymbol == null) return assignments;

        var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(classSymbol, semanticModel);
        var hasInheritance = classSymbol?.BaseType != null &&
                             classSymbol.BaseType.SpecialType != SpecialType.System_Object;

        foreach (var configField in configFields)
        {
            if (configField.IsOptionsPattern || (configField.SupportsReloading && !configField.IsDirectValueBinding))
                continue;

            string assignment;
            if (configField.IsDirectValueBinding)
            {
                var fieldTypeName = RemoveNamespacesAndDots(configField.FieldType, namespacesForStripping);
                var configKey = configField.ConfigurationKey ?? "";
                var typeName = configField.FieldType.ToDisplayString();
                var fullTypeName = configField.FieldType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var metadataName = configField.FieldType.MetadataName;
                var namespaceAndMetadata = configField.FieldType.ContainingNamespace?.ToDisplayString() + "." +
                                           configField.FieldType.MetadataName;

                var isSystemType = typeName.StartsWith("System.") || fullTypeName.StartsWith("global::System.") ||
                                   metadataName is "TimeSpan" or "DateTime" or "DateTimeOffset" or "Guid" or "Uri" ||
                                   namespaceAndMetadata is "System.TimeSpan" or "System.DateTime"
                                       or "System.DateTimeOffset" or "System.Guid" or "System.Uri";

                if (isSystemType)
                {
                    if (fullTypeName.StartsWith("global::")) typeName = fullTypeName;
                    else if (fullTypeName.StartsWith("System.")) typeName = "global::" + fullTypeName;
                    else if (namespaceAndMetadata.StartsWith("System.")) typeName = "global::" + namespaceAndMetadata;
                    else if (metadataName is "TimeSpan" or "DateTime" or "DateTimeOffset" or "Guid" or "Uri")
                        typeName = "global::System." + metadataName;
                    else typeName = "global::" + fullTypeName;
                }
                else
                {
                    typeName = fieldTypeName;
                }

                if (configField.DefaultValue != null)
                {
                    var formattedDefault =
                        FormatDefaultValueForGetValue(configField.DefaultValue, configField.FieldType);
                    assignment =
                        $"this.{configField.FieldName} = {configurationParameterName}.GetValue<{typeName}>(\"{configKey}\", {formattedDefault});";
                }
                else if (configField.Required && IsReferenceTypeOrNullable(configField.FieldType) && !hasInheritance)
                {
                    assignment =
                        $"this.{configField.FieldName} = {configurationParameterName}.GetValue<{typeName}>(\"{configKey}\") ?? throw new global::System.ArgumentException(\"Required configuration '{configKey}' is missing\", \"{configKey}\");";
                }
                else
                {
                    assignment =
                        $"this.{configField.FieldName} = {configurationParameterName}.GetValue<{typeName}>(\"{configKey}\")!;";
                }
            }
            else
            {
                var sectionName = configField.GetSectionName();
                if (CollectionUtilities.IsCollectionInterfaceType(configField.FieldType))
                {
                    var (concreteTypeName, conversionMethod) = CollectionUtilities
                        .GetConcreteCollectionBinding(configField.FieldType, namespacesForStripping);
                    if (configField.Required && !hasInheritance)
                        assignment =
                            $"this.{configField.FieldName} = {configurationParameterName}.GetSection(\"{sectionName}\").Get<{concreteTypeName}>(){conversionMethod} ?? throw new global::System.InvalidOperationException(\"Required configuration section '{sectionName}' is missing\");";
                    else
                        assignment =
                            $"this.{configField.FieldName} = {configurationParameterName}.GetSection(\"{sectionName}\").Get<{concreteTypeName}>(){conversionMethod}!;";
                }
                else
                {
                    var fieldTypeName = RemoveNamespacesAndDots(configField.FieldType, namespacesForStripping);
                    if (configField.Required && !hasInheritance)
                        assignment =
                            $"this.{configField.FieldName} = {configurationParameterName}.GetSection(\"{sectionName}\").Get<{fieldTypeName}>() ?? throw new global::System.InvalidOperationException(\"Required configuration section '{sectionName}' is missing\");";
                    else
                        assignment =
                            $"this.{configField.FieldName} = {configurationParameterName}.GetSection(\"{sectionName}\").Get<{fieldTypeName}>()!;";
                }
            }

            assignments.Add(assignment);
        }

        return assignments;
    }

    private static List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>
        GetConfigurationDependencies(
            INamedTypeSymbol? classSymbol,
            SemanticModel semanticModel,
            HashSet<string> namespacesForStripping)
    {
        var dependencies = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();
        if (classSymbol == null) return dependencies;

        var allConfigFields = new List<ConfigurationInjectionInfo>();
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(currentType, semanticModel);
            allConfigFields.AddRange(configFields);
            currentType = currentType.BaseType;
        }

        if (!allConfigFields.Any()) return dependencies;

        var uniqueConfigFields = allConfigFields
            .Where(f => f.IsOptionsPattern || (f.SupportsReloading && !f.IsDirectValueBinding))
            .GroupBy(f => f.FieldName)
            .Select(g => g.First())
            .ToList();

        foreach (var configField in uniqueConfigFields)
        {
            ITypeSymbol serviceType;
            if (configField.SupportsReloading && !configField.IsOptionsPattern && !configField.IsDirectValueBinding)
            {
                var optionsType =
                    semanticModel.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Options.IOptionsSnapshot`1");
                serviceType = optionsType != null
                    ? optionsType.Construct(configField.FieldType)
                    : configField.FieldType;
            }
            else
            {
                serviceType = configField.FieldType;
            }

            dependencies.Add((serviceType, configField.FieldName, DependencySource.Inject));
        }

        return dependencies;
    }

    private static bool IsIConfigurationField(string fieldName,
        ITypeSymbol serviceType,
        SemanticModel semanticModel)
    {
        var iConfigurationType =
            semanticModel.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Configuration.IConfiguration");
        return iConfigurationType != null && SymbolEqualityComparer.Default.Equals(serviceType, iConfigurationType);
    }

    private static ITypeSymbol? FindIConfigurationType(Compilation compilation) =>
        compilation.GetTypeByMetadataName("Microsoft.Extensions.Configuration.IConfiguration");

    private static bool IsOptionsPatternAssignment(string fieldName,
        INamedTypeSymbol? classSymbol,
        SemanticModel semanticModel)
    {
        if (classSymbol == null) return false;
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(currentType, semanticModel);
            var configField = configFields.FirstOrDefault(f => f.FieldName == fieldName);
            if (configField?.IsOptionsPattern == true) return true;
            currentType = currentType.BaseType;
        }

        return false;
    }

    private static bool IsOptionsPatternOrConfigObjectAssignment(string fieldName,
        INamedTypeSymbol? classSymbol,
        SemanticModel semanticModel)
    {
        if (classSymbol == null) return false;
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(currentType, semanticModel);
            var configField = configFields.FirstOrDefault(f => f.FieldName == fieldName);
            if (configField != null && (configField.IsOptionsPattern || !configField.IsDirectValueBinding)) return true;
            currentType = currentType.BaseType;
        }

        return false;
    }

    private static bool IsSupportsReloadingField(string fieldName,
        INamedTypeSymbol? classSymbol,
        SemanticModel semanticModel)
    {
        if (classSymbol == null) return false;
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(currentType, semanticModel);
            var configField = configFields.FirstOrDefault(f => f.FieldName == fieldName);
            if (configField != null && configField.SupportsReloading && !configField.IsOptionsPattern) return true;
            currentType = currentType.BaseType;
        }

        return false;
    }

    private static bool IsSupportsReloadingFieldWithOptionsPattern(string fieldName,
        INamedTypeSymbol? classSymbol,
        SemanticModel semanticModel)
    {
        if (classSymbol == null) return false;
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(currentType, semanticModel);
            var configField = configFields.FirstOrDefault(f => f.FieldName == fieldName);
            if (configField != null && configField.SupportsReloading && !configField.IsOptionsPattern &&
                !configField.IsDirectValueBinding) return true;
            currentType = currentType.BaseType;
        }

        return false;
    }

    private static bool IsPrimitiveSupportsReloadingField(string fieldName,
        INamedTypeSymbol? classSymbol,
        SemanticModel semanticModel)
    {
        if (classSymbol == null) return false;
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(currentType, semanticModel);
            var configField = configFields.FirstOrDefault(f => f.FieldName == fieldName);
            if (configField != null && configField.SupportsReloading && configField.IsDirectValueBinding) return true;
            currentType = currentType.BaseType;
        }

        return false;
    }


    private static string FormatDefaultValueForGetValue(object defaultValue,
        ITypeSymbol targetType)
    {
        if (defaultValue == null) return "default";
        if (defaultValue is string stringValue)
            return targetType.SpecialType switch
            {
                SpecialType.System_String => $"\"{EscapeStringLiteral(stringValue)}\"",
                SpecialType.System_Int32 when int.TryParse(stringValue, out var intVal) => intVal.ToString(),
                SpecialType.System_Boolean when bool.TryParse(stringValue, out var boolVal) => boolVal.ToString()
                    .ToLowerInvariant(),
                SpecialType.System_Double when double.TryParse(stringValue, out var doubleVal) => doubleVal.ToString(),
                SpecialType.System_Decimal when decimal.TryParse(stringValue, out var decimalVal) => $"{decimalVal}m",
                _ => HandleComplexDefaultValue(stringValue, targetType)
            };

        return defaultValue switch
        {
            bool b => b.ToString().ToLowerInvariant(),
            string s => $"\"{EscapeStringLiteral(s)}\"",
            _ => defaultValue.ToString() ?? "default"
        };
    }

    private static string HandleComplexDefaultValue(string stringValue,
        ITypeSymbol targetType)
    {
        var typeName = targetType.ToDisplayString();
        return typeName switch
        {
            "System.TimeSpan" => $"global::System.TimeSpan.Parse(\"{EscapeStringLiteral(stringValue)}\")",
            "System.DateTime" => $"global::System.DateTime.Parse(\"{EscapeStringLiteral(stringValue)}\")",
            "System.DateTimeOffset" => $"global::System.DateTimeOffset.Parse(\"{EscapeStringLiteral(stringValue)}\")",
            "System.Guid" => $"global::System.Guid.Parse(\"{EscapeStringLiteral(stringValue)}\")",
            "System.Uri" => $"new global::System.Uri(\"{EscapeStringLiteral(stringValue)}\")",
            _ => $"\"{EscapeStringLiteral(stringValue)}\""
        };
    }

    private static string EscapeStringLiteral(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    private static bool IsReferenceTypeOrNullable(ITypeSymbol typeSymbol) =>
        typeSymbol.IsReferenceType || (typeSymbol is INamedTypeSymbol namedType &&
                                       namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T);

    private static bool IsNullableValueType(ITypeSymbol typeSymbol) =>
        typeSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    private static string FormatDefaultValue(object defaultValue,
        string targetTypeName)
    {
        return defaultValue switch
        {
            string str => targetTypeName switch
            {
                "string" => $"\"{str}\"",
                "TimeSpan" => $"TimeSpan.Parse(\"{str}\")",
                "DateTime" => $"DateTime.Parse(\"{str}\")",
                "DateTimeOffset" => $"DateTimeOffset.Parse(\"{str}\")",
                "Guid" => $"Guid.Parse(\"{str}\")",
                "Uri" => $"new Uri(\"{str}\")",
                _ => $"\"{str}\""
            },
            bool b => b ? "true" : "false",
            null => "null",
            _ => defaultValue.ToString() ?? "null"
        };
    }
}
