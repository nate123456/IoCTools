using System;
using IoCTools.Abstractions.Enumerations;

namespace IoCTools.Abstractions.Annotations;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13, TDep14>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13, TDep14, TDep15>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13, TDep14, TDep15, TDep16>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13, TDep14, TDep15, TDep16, TDep17>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13, TDep14, TDep15, TDep16, TDep17, TDep18>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13, TDep14, TDep15, TDep16, TDep17, TDep18, TDep19>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13, TDep14, TDep15, TDep16, TDep17, TDep18, TDep19, TDep20>(
    NamingConvention namingConvention = NamingConvention.CamelCase,
    bool stripI = true,
    string prefix = "_")
    : Attribute
{
    public NamingConvention NamingConvention { get; } = namingConvention;
    public bool StripI { get; } = stripI;
    public string Prefix { get; } = prefix;
}