namespace IoCTools.Abstractions.Annotations;

using System;

using Enumerations;

// Template for backwards-compatible DependsOn attribute:
// 1. Parameterless constructor with original defaults
// 2. Optional parameter constructor for new features

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class
    DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class
    DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13, TDep14> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13, TDep14, TDep15> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13, TDep14, TDep15, TDep16> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13, TDep14, TDep15, TDep16, TDep17> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13, TDep14, TDep15, TDep16, TDep17, TDep18> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13, TDep14, TDep15, TDep16, TDep17, TDep18, TDep19> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependsOnAttribute<TDep1, TDep2, TDep3, TDep4, TDep5, TDep6, TDep7, TDep8, TDep9, TDep10, TDep11,
    TDep12, TDep13, TDep14, TDep15, TDep16, TDep17, TDep18, TDep19, TDep20> : Attribute
{
    // BACKWARDS COMPATIBLE: Parameterless constructor with defaults (original behavior)
    public DependsOnAttribute()
    {
        // Properties have default values already set
    }

    // NEW: Constructor with all parameters for advanced usage
    public DependsOnAttribute(
        NamingConvention namingConvention = NamingConvention.CamelCase,
        bool stripI = true,
        string prefix = "_",
        bool external = false)
    {
        NamingConvention = namingConvention;
        StripI = stripI;
        Prefix = prefix;
        External = external;
    }

    public NamingConvention NamingConvention { get; set; } = NamingConvention.CamelCase;
    public bool StripI { get; set; } = true;
    public string Prefix { get; set; } = "_";
    public bool External { get; set; }
}
