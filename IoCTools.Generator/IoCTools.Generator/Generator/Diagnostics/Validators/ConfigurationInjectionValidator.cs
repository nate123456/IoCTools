namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System.Collections.Generic;
using System.Linq;

using IoCTools.Generator.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class ConfigurationInjectionValidator
{
    internal static void ValidateConfigurationInjection(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var configurationFields = GetConfigurationFieldsFromHierarchy(classSymbol);
        if (!configurationFields.Any()) return;

        if (!classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.ConfigurationOnNonPartialClass,
                classDeclaration.GetLocation(), classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }

        foreach (var fieldSymbol in configurationFields)
        {
            if (fieldSymbol.IsStatic)
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.ConfigurationOnStaticField,
                    classDeclaration.GetLocation(), fieldSymbol.Name, classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
                continue;
            }

            var configAttribute = fieldSymbol.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() ==
                                        "IoCTools.Abstractions.Annotations.InjectConfigurationAttribute");
            if (configAttribute == null) continue;

            if (configAttribute.ConstructorArguments.Length > 0)
            {
                var key = configAttribute.ConstructorArguments[0].Value?.ToString();
                var validationResult = ValidateConfigurationKey(key);
                if (!validationResult.IsValid)
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.InvalidConfigurationKey,
                        classDeclaration.GetLocation(), key ?? string.Empty, validationResult.ErrorMessage);
                    context.ReportDiagnostic(diagnostic);
                }
            }

            var fieldType = fieldSymbol.Type;
            if (!IsSupportedConfigurationType(fieldType))
            {
                var reasonMessage = GetUnsupportedTypeReason(fieldType);
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.UnsupportedConfigurationType,
                    classDeclaration.GetLocation(), fieldType.ToDisplayString(), reasonMessage);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static List<IFieldSymbol> GetConfigurationFieldsFromHierarchy(INamedTypeSymbol classSymbol)
    {
        var result = new List<IFieldSymbol>();
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentType.GetMembers().OfType<IFieldSymbol>())
            {
                var hasInjectConfigurationAttribute = member.GetAttributes()
                    .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                                 "IoCTools.Abstractions.Annotations.InjectConfigurationAttribute");
                if (hasInjectConfigurationAttribute) result.Add(member);
            }

            currentType = currentType.BaseType;
        }

        return result;
    }

    private static bool IsSupportedConfigurationType(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Object) return true;
        if (type.SpecialType == SpecialType.System_String) return true;

        if (type.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T &&
            type is INamedTypeSymbol nullableType)
            return IsSupportedConfigurationType(nullableType.TypeArguments.First());

        if (type is IArrayTypeSymbol arrayType) return IsSupportedConfigurationType(arrayType.ElementType);

        if (type is INamedTypeSymbol namedType)
        {
            var typeName = namedType.ToDisplayString();
            if (namedType.TypeKind == TypeKind.Enum) return true;

            var supportedBuiltinTypes = new[]
            {
                "System.DateTime", "System.DateTimeOffset", "System.TimeSpan", "System.Guid", "System.Uri",
                "System.Decimal"
            };
            if (supportedBuiltinTypes.Contains(typeName)) return true;

            if (typeName.StartsWith("Microsoft.Extensions.Options.IOptions") ||
                typeName.StartsWith("Microsoft.Extensions.Options.IOptionsSnapshot") ||
                typeName.StartsWith("Microsoft.Extensions.Options.IOptionsMonitor"))
                return true;

            if (typeName.StartsWith("System.Collections.Generic.List<") ||
                typeName.StartsWith("System.Collections.Generic.IList<") ||
                typeName.StartsWith("System.Collections.Generic.ICollection<") ||
                typeName.StartsWith("System.Collections.Generic.IEnumerable<") ||
                typeName.StartsWith("System.Collections.Generic.Dictionary<") ||
                typeName.StartsWith("System.Collections.Generic.IDictionary<"))
                if (namedType.TypeArguments.Length > 0)
                    return namedType.TypeArguments.All(arg => IsSupportedConfigurationType(arg));

            if (namedType.TypeKind == TypeKind.Interface) return false;
            if (namedType.IsAbstract) return false;
            return namedType.Constructors.Any(c => c.Parameters.Length == 0);
        }

        return false;
    }

    private static string GetUnsupportedTypeReason(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            if (type is INamedTypeSymbol interfaceType)
            {
                var typeName = interfaceType.ToDisplayString();
                if (typeName.StartsWith("Microsoft.Extensions.Options."))
                    return "requires parameterless constructor";
            }

            return "Interfaces cannot be bound from configuration";
        }

        if (type.IsAbstract) return "Abstract types cannot be bound from configuration";
        if (type is IArrayTypeSymbol) return "Array element type is not supported for configuration binding";
        if (type is INamedTypeSymbol namedType2)
        {
            var typeName2 = namedType2.ToDisplayString();
            if (typeName2.StartsWith("System.Collections.Generic."))
                return "Collection element type is not supported for configuration binding";
        }

        return "requires parameterless constructor";
    }

    private static ConfigurationKeyValidationResult ValidateConfigurationKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return new ConfigurationKeyValidationResult { IsValid = false, ErrorMessage = "empty or whitespace-only" };
        if (string.IsNullOrWhiteSpace(key))
            return new ConfigurationKeyValidationResult { IsValid = false, ErrorMessage = "empty or whitespace-only" };
        if (key!.Contains("::"))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false, ErrorMessage = "contains double colons (::)"
            };
        if (key!.StartsWith(":") || key.EndsWith(":"))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false, ErrorMessage = "cannot start or end with a colon (:)"
            };
        if (key!.Any(c => c == '\0' || c == '\r' || c == '\n' || c == '\t'))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "contains invalid characters (null, carriage return, newline, or tab)"
            };
        return new ConfigurationKeyValidationResult { IsValid = true, ErrorMessage = string.Empty };
    }

    private struct ConfigurationKeyValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }
}
