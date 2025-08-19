using System;

namespace IoCTools.Abstractions.Annotations;

/// <summary>
///     Marks a field or property for configuration injection from .NET's IConfiguration system.
///     Supports direct value binding, section binding, and options pattern integration.
/// </summary>
/// <remarks>
///     <para>
///         Configuration injection allows services to declare their configuration dependencies
///         directly alongside their implementation, eliminating manual configuration binding boilerplate.
///     </para>
///     <para>
///         <strong>Direct Value Binding:</strong>
///     </para>
///     <code>
/// [InjectConfiguration("Database:ConnectionString")] private readonly string _connectionString;
/// [InjectConfiguration("Cache:TTL")] private readonly TimeSpan _cacheTtl;
/// [InjectConfiguration("Features:EnableAdvancedSearch")] private readonly bool _enableSearch;
/// </code>
///     <para>
///         <strong>Section Binding:</strong>
///     </para>
///     <code>
/// [InjectConfiguration] private readonly DatabaseSettings _dbSettings; // Binds "DatabaseSettings" section
/// [InjectConfiguration("CustomSection")] private readonly MySettings _settings; // Binds "CustomSection"
/// </code>
///     <para>
///         <strong>Options Pattern Integration:</strong>
///     </para>
///     <code>
/// [InjectConfiguration] private readonly IOptions&lt;EmailSettings&gt; _emailOptions;
/// [InjectConfiguration] private readonly IOptionsSnapshot&lt;CacheSettings&gt; _cacheOptions; // Supports hot-reloading
/// [InjectConfiguration] private readonly IOptionsMonitor&lt;LiveSettings&gt; _liveSettings; // Change notifications
/// </code>
///     <para>
///         <strong>Array and Collection Binding:</strong>
///     </para>
///     <code>
/// [InjectConfiguration("AllowedHosts")] private readonly string[] _allowedHosts;
/// [InjectConfiguration("Features:EnabledFeatures")] private readonly List&lt;string&gt; _enabledFeatures;
/// </code>
///     <para>
///         <strong>Mixed with Regular Dependency Injection:</strong>
///     </para>
///     <code>
/// [Service]
/// public partial class EmailService : IEmailService
/// {
///     [Inject] private readonly ILogger&lt;EmailService&gt; _logger;
///     [InjectConfiguration("Email:SmtpHost")] private readonly string _smtpHost;
///     [InjectConfiguration] private readonly EmailSettings _emailSettings;
/// }
/// </code>
/// </remarks>
/// <example>
///     <para>Complete email service example:</para>
///     <code>
/// [Service]
/// public partial class EmailService : IEmailService
/// {
///     [InjectConfiguration("Email:SmtpHost")] private readonly string _smtpHost;
///     [InjectConfiguration("Email:SmtpPort")] private readonly int _smtpPort;
///     [InjectConfiguration("Email:ApiKey")] private readonly string _apiKey;
///     [InjectConfiguration] private readonly EmailSettings _emailSettings; // Binds "EmailSettings" section
///     [InjectConfiguration] private readonly IOptionsSnapshot&lt;EmailSettings&gt; _dynamicSettings; // Hot-reloading
///     [Inject] private readonly ILogger&lt;EmailService&gt; _logger;
///     
///     public async Task SendEmailAsync(string to, string subject, string body)
///     {
///         // Use _smtpHost, _smtpPort, _apiKey, _emailSettings, _dynamicSettings, and _logger
///         // All injected automatically via generated constructor
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class InjectConfigurationAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InjectConfigurationAttribute" /> class
    ///     for section binding with automatic section name inference.
    /// </summary>
    /// <remarks>
    ///     <para>When used without a configuration key, the section name is inferred from the field/property type name:</para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description><c>DatabaseSettings</c> → <c>"Database"</c> section (removes "Settings" suffix)</description>
    ///         </item>
    ///         <item>
    ///             <description><c>EmailConfig</c> → <c>"EmailConfig"</c> section (uses full type name)</description>
    ///         </item>
    ///         <item>
    ///             <description><c>MyCustomClass</c> → <c>"MyCustomClass"</c> section (uses full type name)</description>
    ///         </item>
    ///     </list>
    ///     <para>
    ///         For <c>IOptions&lt;T&gt;</c>, <c>IOptionsSnapshot&lt;T&gt;</c>, and <c>IOptionsMonitor&lt;T&gt;</c> types,
    ///         the section name is inferred from the generic type argument <c>T</c>.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// [InjectConfiguration] private readonly DatabaseSettings _dbSettings; // Binds to "Database" section
    /// [InjectConfiguration] private readonly IOptions&lt;EmailSettings&gt; _emailOptions; // Binds to "Email" section
    /// </code>
    /// </example>
    public InjectConfigurationAttribute()
    {
        ConfigurationKey = null;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="InjectConfigurationAttribute" /> class
    ///     with a specific configuration key or section name.
    /// </summary>
    /// <param name="configurationKey">
    ///     The configuration key or section name to bind to. Supports nested keys using colon notation (e.g.,
    ///     "Database:ConnectionString").
    /// </param>
    /// <remarks>
    ///     <para>Configuration keys support hierarchical access using colon notation:</para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description><c>"Database:ConnectionString"</c> - Direct value binding</description>
    ///         </item>
    ///         <item>
    ///             <description><c>"Cache:TTL"</c> - Direct value binding with type conversion</description>
    ///         </item>
    ///         <item>
    ///             <description><c>"App:Features:Search:MaxResults"</c> - Deeply nested configuration</description>
    ///         </item>
    ///         <item>
    ///             <description><c>"CustomSection"</c> - Section binding to custom section name</description>
    ///         </item>
    ///     </list>
    ///     <para>The binding method depends on the target field/property type:</para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 <strong>Primitive types</strong> (string, int, bool, TimeSpan, etc.): Uses
    ///                 <c>IConfiguration.GetValue&lt;T&gt;(key)</c>
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <strong>Complex types</strong>: Uses <c>IConfiguration.GetSection(key).Get&lt;T&gt;()</c>
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <strong>Arrays/Collections</strong>: Uses <c>IConfiguration.GetSection(key).Get&lt;T&gt;()</c>
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 <strong>Options types</strong>: Injected directly as services (IOptions&lt;T&gt;,
    ///                 IOptionsSnapshot&lt;T&gt;, etc.)
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    /// <example>
    ///     <code>
    /// [InjectConfiguration("Database:ConnectionString")] private readonly string _connectionString;
    /// [InjectConfiguration("Cache:TTL")] private readonly TimeSpan _cacheTtl;
    /// [InjectConfiguration("Features:EnableAdvancedSearch")] private readonly bool _enableSearch;
    /// [InjectConfiguration("CustomEmailSection")] private readonly EmailSettings _emailSettings;
    /// [InjectConfiguration("AllowedHosts")] private readonly string[] _allowedHosts;
    /// </code>
    /// </example>
    public InjectConfigurationAttribute(string configurationKey)
    {
        ConfigurationKey = configurationKey ?? throw new ArgumentNullException(nameof(configurationKey));
    }

    /// <summary>
    ///     Gets the configuration key or section name to bind to.
    /// </summary>
    /// <value>
    ///     The configuration key for direct value binding or section name for complex type binding.
    ///     <c>null</c> when section name should be inferred from the field/property type.
    /// </value>
    /// <remarks>
    ///     <para>When <c>null</c>, the configuration section name is automatically inferred:</para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>For types ending in "Settings": removes the "Settings" suffix</description>
    ///         </item>
    ///         <item>
    ///             <description>For types ending in "Config": removes the "Config" suffix</description>
    ///         </item>
    ///         <item>
    ///             <description>For types ending in "Configuration": removes the "Configuration" suffix</description>
    ///         </item>
    ///         <item>
    ///             <description>For other types: uses the full type name</description>
    ///         </item>
    ///         <item>
    ///             <description>For generic options types (IOptions&lt;T&gt;, etc.): uses the inner type T for inference</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    public string? ConfigurationKey { get; }

    /// <summary>
    ///     Gets or sets the default value to use when the configuration key is missing or cannot be converted.
    /// </summary>
    /// <value>
    ///     The default value to use when configuration binding fails. Default is <c>null</c>.
    /// </value>
    /// <remarks>
    ///     <para>The default value is used when:</para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>The configuration key does not exist</description>
    ///         </item>
    ///         <item>
    ///             <description>The configuration value is null or empty</description>
    ///         </item>
    ///         <item>
    ///             <description>Type conversion fails (for value types)</description>
    ///         </item>
    ///     </list>
    ///     <para>
    ///         <strong>Type Compatibility:</strong> The default value must be compatible with the target field/property type
    ///         or an implicit conversion must be available. For complex types, consider using nullable types and handling
    ///         null values in your service logic instead of providing defaults.
    ///     </para>
    ///     <para>
    ///         <strong>Performance Note:</strong> Default values are evaluated at service construction time, not at
    ///         attribute declaration time.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// [InjectConfiguration("Database:Timeout", DefaultValue = 30)] private readonly int _timeout;
    /// [InjectConfiguration("Cache:TTL", DefaultValue = "00:05:00")] private readonly TimeSpan _cacheTtl;
    /// [InjectConfiguration("App:Name", DefaultValue = "MyApplication")] private readonly string _appName;
    /// [InjectConfiguration("Features:EnableDebug", DefaultValue = false)] private readonly bool _enableDebug;
    /// </code>
    /// </example>
    public object? DefaultValue { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the configuration value is required.
    /// </summary>
    /// <value>
    ///     <c>true</c> if the configuration value is required and should throw an exception when missing;
    ///     <c>false</c> if the configuration value is optional. Default is <c>true</c>.
    /// </value>
    /// <remarks>
    ///     <para>When <c>true</c> and the configuration key is missing:</para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>For value types: throws <c>InvalidOperationException</c> during service construction</description>
    ///         </item>
    ///         <item>
    ///             <description>For reference types: throws <c>InvalidOperationException</c> if no default value is provided</description>
    ///         </item>
    ///         <item>
    ///             <description>For nullable types: allows null values when <c>Required = false</c></description>
    ///         </item>
    ///     </list>
    ///     <para>When <c>false</c> and the configuration key is missing:</para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>Uses the <see cref="DefaultValue" /> if provided</description>
    ///         </item>
    ///         <item>
    ///             <description>Uses the type's default value (e.g., 0 for int, null for strings)</description>
    ///         </item>
    ///         <item>
    ///             <description>For nullable types: sets the value to null</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    /// <example>
    ///     <code>
    /// [InjectConfiguration("Database:ConnectionString")] private readonly string _connectionString; // Required by default
    /// [InjectConfiguration("Optional:Feature", Required = false)] private readonly bool _optionalFeature; // Optional
    /// [InjectConfiguration("Optional:Timeout", Required = false, DefaultValue = 30)] private readonly int _timeout; // Optional with default
    /// </code>
    /// </example>
    public bool Required { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether configuration changes should trigger service reloading.
    /// </summary>
    /// <value>
    ///     <c>true</c> to monitor configuration changes and update the injected value;
    ///     <c>false</c> to bind the configuration value once at service construction. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    ///     <para>
    ///         <strong>Hot Reloading Support:</strong>
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 When <c>true</c>: Uses <c>IOptionsSnapshot&lt;T&gt;</c> or <c>IOptionsMonitor&lt;T&gt;</c> for
    ///                 automatic updates
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 When <c>false</c>: Uses <c>IOptions&lt;T&gt;</c> or direct <c>IConfiguration</c> binding for
    ///                 static values
    ///             </description>
    ///         </item>
    ///     </list>
    ///     <para>
    ///         <strong>Scope Considerations:</strong>
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description><c>IOptionsSnapshot&lt;T&gt;</c>: Reloaded per scope (recommended for most scenarios)</description>
    ///         </item>
    ///         <item>
    ///             <description><c>IOptionsMonitor&lt;T&gt;</c>: Provides change notifications (for singleton services)</description>
    ///         </item>
    ///         <item>
    ///             <description>Direct configuration binding: No reloading support</description>
    ///         </item>
    ///     </list>
    ///     <para>
    ///         <strong>Performance Impact:</strong> Hot reloading has minimal performance overhead but may not be
    ///         necessary for configuration that rarely changes (e.g., connection strings, API keys).
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// [InjectConfiguration("Cache:TTL", SupportsReloading = true)] private readonly TimeSpan _cacheTtl;
    /// [InjectConfiguration("Database:ConnectionString")] private readonly string _connectionString; // Static, no reloading
    /// [InjectConfiguration] private readonly IOptionsSnapshot&lt;EmailSettings&gt; _emailSettings; // Always supports reloading
    /// </code>
    /// </example>
    public bool SupportsReloading { get; set; } = false;
}