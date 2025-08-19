using Microsoft.CodeAnalysis;

namespace IoCTools.Generator.Models;

/// <summary>
///     Represents configuration options registration information
/// </summary>
internal class ConfigurationOptionsRegistration
{
    public ConfigurationOptionsRegistration(ITypeSymbol optionsType,
        string sectionName)
    {
        OptionsType = optionsType;
        SectionName = sectionName;
    }

    /// <summary>
    ///     The options type (e.g., EmailSettings)
    /// </summary>
    public ITypeSymbol OptionsType { get; }

    /// <summary>
    ///     The configuration section name (e.g., "Email")
    /// </summary>
    public string SectionName { get; }
}