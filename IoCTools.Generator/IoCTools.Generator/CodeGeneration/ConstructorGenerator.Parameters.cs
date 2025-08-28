namespace IoCTools.Generator.CodeGeneration;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

internal static partial class ConstructorGenerator
{
    private static string GetParameterNameFromFieldName(string fieldName)
    {
        if (fieldName.StartsWith("_"))
        {
            var nameWithoutUnderscore = fieldName.Substring(1);
            if (nameWithoutUnderscore.Contains("_"))
            {
                var parts = nameWithoutUnderscore.Split('_');
                var camelCaseName = parts[0].ToLowerInvariant() +
                                    string.Concat(parts.Skip(1)
                                        .Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant()));
                return EscapeReservedKeyword(camelCaseName);
            }

            var paramName1 = char.ToLowerInvariant(nameWithoutUnderscore[0]) + nameWithoutUnderscore.Substring(1);
            return EscapeReservedKeyword(paramName1);
        }

        var paramName = char.ToLowerInvariant(fieldName[0]) + fieldName.Substring(1);
        return EscapeReservedKeyword(paramName);
    }

    private static string GetTypeStringWithNullableAnnotation(ITypeSymbol serviceType,
        string fieldName,
        INamedTypeSymbol? classSymbol,
        HashSet<string> namespacesForStripping)
    {
        var formatWithNullable = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                                  SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        var fullTypeName = serviceType.ToDisplayString(formatWithNullable);

        if (namespacesForStripping != null)
        {
            var sortedNamespaces = namespacesForStripping.Where(ns => !string.IsNullOrEmpty(ns))
                .OrderByDescending(ns => ns.Length);

            foreach (var ns in sortedNamespaces)
            {
                if (fullTypeName.StartsWith($"{ns}.")) fullTypeName = fullTypeName.Substring(ns.Length + 1);
                fullTypeName = fullTypeName.Replace($"{ns}.", "");
            }
        }

        return fullTypeName;
    }

    private static string EscapeReservedKeyword(string identifier)
    {
        var reservedKeywords = new HashSet<string>
        {
            "abstract",
            "as",
            "base",
            "bool",
            "break",
            "byte",
            "case",
            "catch",
            "char",
            "checked",
            "class",
            "const",
            "continue",
            "decimal",
            "default",
            "delegate",
            "do",
            "double",
            "else",
            "enum",
            "event",
            "explicit",
            "extern",
            "false",
            "finally",
            "fixed",
            "float",
            "for",
            "foreach",
            "goto",
            "if",
            "implicit",
            "in",
            "int",
            "interface",
            "internal",
            "is",
            "lock",
            "long",
            "namespace",
            "new",
            "null",
            "object",
            "operator",
            "out",
            "override",
            "params",
            "private",
            "protected",
            "public",
            "readonly",
            "ref",
            "return",
            "sbyte",
            "sealed",
            "short",
            "sizeof",
            "stackalloc",
            "static",
            "string",
            "struct",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "uint",
            "ulong",
            "unchecked",
            "unsafe",
            "ushort",
            "using",
            "virtual",
            "void",
            "volatile",
            "while"
        };

        return reservedKeywords.Contains(identifier) ? identifier + "Value" : identifier;
    }
}
