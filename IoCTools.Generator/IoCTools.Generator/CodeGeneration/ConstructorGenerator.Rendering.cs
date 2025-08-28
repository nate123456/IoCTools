namespace IoCTools.Generator.CodeGeneration;

internal static partial class ConstructorGenerator
{
    private static string RenderConstructorSource(
        string beforeUsings,
        string usingsBlock,
        string afterUsings,
        bool isNestedClass,
        string openingBraces,
        string closingBraces,
        string accessibilityModifier,
        string typeKeyword,
        string fullClassName,
        string constraintClauses,
        string fieldsStr,
        string parameterStr,
        string baseCallStr,
        string constructorName,
        string assignmentStr)
    {
        string constructorCode;
        if (isNestedClass)
            constructorCode = """
                              #nullable enable
                              {{beforeUsings}}
                              {{usings}}
                              {{afterUsings}}

                              {{openingBraces}}
                              {{accessibilityModifier}} partial {{typeKeyword}} {{fullClassName}}{{constraintClauses}}
                              {
                                  {{fieldsStr}}
                                  
                                  public {{constructorName}}({{parameterStr}}){{baseCallStr}}
                                  {
                                      {{assignmentStr}}
                                  }
                              }
                              {{closingBraces}}
                              """.Trim();
        else
            constructorCode = """
                              #nullable enable
                              {{beforeUsings}}
                              {{usings}}
                              {{afterUsings}}

                              {{accessibilityModifier}} partial {{typeKeyword}} {{fullClassName}}{{constraintClauses}}
                              {
                                  {{fieldsStr}}
                                  
                                  public {{constructorName}}({{parameterStr}}){{baseCallStr}}
                                  {
                                      {{assignmentStr}}
                                  }
                              }
                              """.Trim();

        return constructorCode
            .Replace("{{fieldsStr}}", fieldsStr)
            .Replace("{{parameterStr}}", parameterStr)
            .Replace("{{assignmentStr}}", assignmentStr)
            .Replace("{{baseCallStr}}", baseCallStr)
            .Replace("{{constructorName}}", constructorName)
            .Replace("{{fullClassName}}", fullClassName)
            .Replace("{{accessibilityModifier}}", accessibilityModifier)
            .Replace("{{typeKeyword}}", typeKeyword)
            .Replace("{{constraintClauses}}", constraintClauses)
            .Replace("{{usings}}", usingsBlock)
            .Replace("{{beforeUsings}}", beforeUsings)
            .Replace("{{afterUsings}}", afterUsings)
            .Replace("{{openingBraces}}", openingBraces)
            .Replace("{{closingBraces}}", closingBraces);
    }
}
