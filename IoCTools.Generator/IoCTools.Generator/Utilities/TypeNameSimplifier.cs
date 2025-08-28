namespace IoCTools.Generator.Utilities;

internal static class TypeNameSimplifier
{
    public static string SimplifySystemTypesForServiceRegistration(string fullyQualifiedTypeName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedTypeName)) return fullyQualifiedTypeName;
        return fullyQualifiedTypeName
            .Replace("global::System.Collections.Generic.List<", "List<")
            .Replace("global::System.Collections.Generic.Dictionary<", "Dictionary<")
            .Replace("global::System.Collections.Generic.IEnumerable<", "IEnumerable<")
            .Replace("global::System.Collections.Generic.ICollection<", "ICollection<")
            .Replace("global::System.Collections.Generic.IList<", "IList<")
            .Replace("global::System.Collections.Generic.HashSet<", "HashSet<")
            .Replace("global::System.String", "string")
            .Replace("global::System.Int32", "int")
            .Replace("global::System.Boolean", "bool")
            .Replace("global::System.Double", "double")
            .Replace("global::System.Decimal", "decimal")
            .Replace("global::System.DateTime", "DateTime")
            .Replace("global::System.TimeSpan", "TimeSpan")
            .Replace("global::System.Guid", "Guid");
    }

    public static string SimplifyTypesForConditionalServices(string fullyQualifiedTypeName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedTypeName)) return fullyQualifiedTypeName;
        var simplified = SimplifySystemTypesForServiceRegistration(fullyQualifiedTypeName);
        return simplified.StartsWith("global::") ? simplified.Substring("global::".Length) : simplified;
    }
}
