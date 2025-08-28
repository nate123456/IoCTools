namespace IoCTools.Abstractions.Annotations;

using System;

/// <summary>
///     Marks a service for conditional registration based on environment variables and/or configuration values.
///     Services with this attribute are only registered when their specified conditions are met at runtime.
/// </summary>
/// <remarks>
///     <para>
///         Conditional service registration enables environment-specific and configuration-driven service implementations,
///         allowing different services to be registered based on runtime conditions without requiring manual registration
///         logic.
///     </para>
///     <para>
///         <strong>Environment-Based Conditions:</strong>
///     </para>
///     <code>
/// [ConditionalService(Environment = "Development")]
/// [Scoped]
/// public partial class MockPaymentService : IPaymentService { }
/// 
/// [ConditionalService(Environment = "Production")]
/// [Scoped]
/// public partial class StripePaymentService : IPaymentService { }
/// 
/// [ConditionalService(Environment = "Testing,Staging")] // Multiple environments
/// [Scoped]
/// public partial class TestPaymentService : IPaymentService { }
/// 
/// [ConditionalService(NotEnvironment = "Production")] // Exclude specific environments
/// [Scoped]
/// public partial class DebugService : IDebugService { }
/// </code>
///     <para>
///         <strong>Configuration-Based Conditions:</strong>
///     </para>
///     <code>
/// [ConditionalService(ConfigValue = "Features:UseRedisCache", Equals = "true")]
/// [Scoped]
/// public partial class RedisCacheService : ICacheService { }
/// 
/// [ConditionalService(ConfigValue = "Database:Type", NotEquals = "InMemory")]
/// [Scoped]
/// public partial class SqlDatabaseService : IDatabaseService { }
/// 
/// [ConditionalService(ConfigValue = "Cache:Provider", Equals = "Redis")]
/// [Scoped]
/// public partial class RedisProvider : ICacheProvider { }
/// </code>
///     <para>
///         <strong>Combined Conditions (AND Logic):</strong>
///     </para>
///     <code>
/// [ConditionalService(Environment = "Development", ConfigValue = "Features:UseMocks", Equals = "true")]
/// [Scoped]
/// public partial class MockEmailService : IEmailService { }
/// 
/// [ConditionalService(Environment = "Production", ConfigValue = "Features:PremiumTier", Equals = "true")]
/// [Scoped]
/// public partial class PremiumFeatureService : IPremiumService { }
/// </code>
///     <para>
///         <strong>Complex Scenarios:</strong>
///     </para>
///     <code>
/// [ConditionalService(Environment = "Development,Testing", ConfigValue = "Features:EnableDebug", NotEquals = "false")]
/// [Scoped]
/// public partial class DebugLoggerService : ILoggerService { }
/// 
/// [ConditionalService(NotEnvironment = "Development", ConfigValue = "External:ApiEnabled", Equals = "true")]
/// [Scoped]
/// public partial class ExternalApiService : IApiService { }
/// </code>
/// </remarks>
/// <example>
///     <para>Complete payment service switching example:</para>
///     <code>
/// // Development: Use mock implementation
/// [ConditionalService(Environment = "Development")]
/// [Scoped]
/// public partial class MockPaymentService : IPaymentService
/// {
///     public Task&lt;PaymentResult&gt; ProcessPaymentAsync(decimal amount)
///     {
///         return Task.FromResult(new PaymentResult { Success = true, TransactionId = "MOCK-12345" });
///     }
/// }
/// 
/// // Production: Use real Stripe implementation
/// [ConditionalService(Environment = "Production")]
/// [Scoped]
/// public partial class StripePaymentService : IPaymentService
/// {
///     [Inject] private readonly IStripeClient _stripeClient;
///     [InjectConfiguration("Stripe:ApiKey")] private readonly string _apiKey;
///     
///     public async Task&lt;PaymentResult&gt; ProcessPaymentAsync(decimal amount)
///     {
///         // Real Stripe integration
///         return await _stripeClient.ChargeAsync(amount, _apiKey);
///     }
/// }
/// 
/// // Testing: Use configurable test implementation
/// [ConditionalService(Environment = "Testing", ConfigValue = "Testing:PaymentSuccess", Equals = "true")]
/// [Scoped]
/// public partial class SuccessTestPaymentService : IPaymentService
/// {
///     public Task&lt;PaymentResult&gt; ProcessPaymentAsync(decimal amount)
///     {
///         return Task.FromResult(new PaymentResult { Success = true, TransactionId = "TEST-SUCCESS" });
///     }
/// }
/// 
/// [ConditionalService(Environment = "Testing", ConfigValue = "Testing:PaymentSuccess", NotEquals = "true")]
/// [Scoped]
/// public partial class FailureTestPaymentService : IPaymentService
/// {
///     public Task&lt;PaymentResult&gt; ProcessPaymentAsync(decimal amount)
///     {
///         return Task.FromResult(new PaymentResult { Success = false, Error = "TEST-FAILURE" });
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ConditionalServiceAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ConditionalServiceAttribute" /> class.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         When using the parameterless constructor, you must set at least one condition property
    ///         (<see cref="Environment" />, <see cref="NotEnvironment" />, <see cref="ConfigValue" /> with
    ///         <see cref="Equals" /> or <see cref="NotEquals" />) for the conditional service to function properly.
    ///     </para>
    ///     <para>Empty conditions may generate diagnostic warnings during compilation.</para>
    /// </remarks>
    public ConditionalServiceAttribute()
    {
    }

    /// <summary>
    ///     Gets or sets the target environment(s) for which this service should be registered.
    /// </summary>
    /// <value>
    ///     A comma-separated list of environment names (e.g., "Development", "Production", "Testing,Staging").
    ///     The service will be registered if the current environment matches any of the specified values.
    ///     Default is <c>null</c> (no environment condition).
    /// </value>
    /// <remarks>
    ///     <para>
    ///         Environment detection uses the <c>ASPNETCORE_ENVIRONMENT</c> environment variable.
    ///         Multiple environments can be specified using comma separation without spaces.
    ///     </para>
    ///     <para>
    ///         <strong>Supported Patterns:</strong>
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description><c>"Development"</c> - Single environment</description>
    ///         </item>
    ///         <item>
    ///             <description><c>"Testing,Staging"</c> - Multiple environments (OR logic)</description>
    ///         </item>
    ///         <item>
    ///             <description><c>"Development,Testing,Staging"</c> - Multiple environments</description>
    ///         </item>
    ///     </list>
    ///     <para>
    ///         <strong>Case Sensitivity:</strong> Environment names are case-sensitive and should match
    ///         exactly with the values used in your application configuration.
    ///     </para>
    ///     <para>
    ///         <strong>Validation:</strong> Cannot be used together with <see cref="NotEnvironment" /> that contains
    ///         overlapping environment names, as this would create impossible conditions.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// [ConditionalService(Environment = "Development")]
    /// [ConditionalService(Environment = "Testing,Staging")]
    /// [ConditionalService(Environment = "Production")]
    /// </code>
    /// </example>
    public string? Environment { get; set; }

    /// <summary>
    ///     Gets or sets the environment(s) for which this service should NOT be registered.
    /// </summary>
    /// <value>
    ///     A comma-separated list of environment names to exclude (e.g., "Production", "Development,Testing").
    ///     The service will be registered only if the current environment does NOT match any of the specified values.
    ///     Default is <c>null</c> (no environment exclusion).
    /// </value>
    /// <remarks>
    ///     <para>
    ///         This property is useful for registering services in all environments except specific ones,
    ///         such as debug services that should not run in production.
    ///     </para>
    ///     <para>
    ///         <strong>Supported Patterns:</strong>
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description><c>"Production"</c> - Exclude single environment</description>
    ///         </item>
    ///         <item>
    ///             <description><c>"Production,Staging"</c> - Exclude multiple environments</description>
    ///         </item>
    ///     </list>
    ///     <para>
    ///         <strong>Logic:</strong> The service is registered if the current environment is NOT in the
    ///         comma-separated list of excluded environments.
    ///     </para>
    ///     <para>
    ///         <strong>Validation:</strong> Cannot be used together with <see cref="Environment" /> that contains
    ///         overlapping environment names, as this would create impossible conditions.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// [ConditionalService(NotEnvironment = "Production")] // Register in all environments except Production
    /// [ConditionalService(NotEnvironment = "Development,Testing")] // Register only in Production and Staging
    /// </code>
    /// </example>
    public string? NotEnvironment { get; set; }

    /// <summary>
    ///     Gets or sets the configuration key to evaluate for conditional registration.
    /// </summary>
    /// <value>
    ///     The configuration key path using colon notation (e.g., "Features:UseRedisCache", "Database:Type").
    ///     Must be used together with <see cref="Equals" /> or <see cref="NotEquals" /> to define the condition.
    ///     Default is <c>null</c> (no configuration condition).
    /// </value>
    /// <remarks>
    ///     <para>
    ///         Configuration keys support hierarchical access using colon notation, consistent with
    ///         .NET's IConfiguration system:
    ///     </para>
    ///     <para>
    ///         <strong>Supported Key Patterns:</strong>
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description><c>"Features:UseRedisCache"</c> - Simple nested key</description>
    ///         </item>
    ///         <item>
    ///             <description><c>"Database:ConnectionStrings:Default"</c> - Deep nesting</description>
    ///         </item>
    ///         <item>
    ///             <description><c>"App:Environment:Features:AdvancedLogging"</c> - Complex hierarchy</description>
    ///         </item>
    ///         <item>
    ///             <description><c>"SimpleKey"</c> - Root-level configuration</description>
    ///         </item>
    ///     </list>
    ///     <para>
    ///         <strong>Value Retrieval:</strong> Configuration values are retrieved as strings using
    ///         <c>IConfiguration.GetValue&lt;string&gt;(key)</c>. All comparisons are performed as string comparisons.
    ///     </para>
    ///     <para>
    ///         <strong>Missing Keys:</strong> If the configuration key does not exist, it is treated as an empty string
    ///         or null, depending on the comparison operation.
    ///     </para>
    ///     <para>
    ///         <strong>Required Usage:</strong> This property has no effect unless used with <see cref="Equals" />
    ///         or <see cref="NotEquals" />.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// [ConditionalService(ConfigValue = "Features:UseRedisCache", Equals = "true")]
    /// [ConditionalService(ConfigValue = "Database:Type", NotEquals = "InMemory")]
    /// [ConditionalService(ConfigValue = "App:Environment:Tier", Equals = "Premium")]
    /// </code>
    /// </example>
    public string? ConfigValue { get; set; }

    /// <summary>
    ///     Gets or sets the expected configuration value for positive condition matching.
    /// </summary>
    /// <value>
    ///     The string value that the configuration key must equal for the service to be registered.
    ///     Must be used together with <see cref="ConfigValue" />.
    ///     Default is <c>null</c> (no positive condition).
    /// </value>
    /// <remarks>
    ///     <para>
    ///         The comparison is performed as a case-sensitive string equality check between the configuration
    ///         value and this property value.
    ///     </para>
    ///     <para>
    ///         <strong>Type Handling:</strong>
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>Boolean values: <c>"true"</c>, <c>"false"</c> (lowercase)</description>
    ///         </item>
    ///         <item>
    ///             <description>Numeric values: <c>"42"</c>, <c>"3.14"</c> (as strings)</description>
    ///         </item>
    ///         <item>
    ///             <description>String values: Exact match required</description>
    ///         </item>
    ///         <item>
    ///             <description>Null/missing config: Treated as empty string or null</description>
    ///         </item>
    ///     </list>
    ///     <para>
    ///         <strong>Validation:</strong> Cannot be used together with <see cref="NotEquals" /> on the same
    ///         <see cref="ConfigValue" />, as this would create conflicting conditions.
    ///     </para>
    ///     <para><strong>Case Sensitivity:</strong> All string comparisons are case-sensitive.</para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// [ConditionalService(ConfigValue = "Features:EnableAdvanced", Equals = "true")]
    /// [ConditionalService(ConfigValue = "Cache:Provider", Equals = "Redis")]
    /// [ConditionalService(ConfigValue = "Database:MaxConnections", Equals = "100")]
    /// </code>
    /// </example>
    public new string? Equals { get; set; }

    /// <summary>
    ///     Gets or sets the configuration value that must NOT match for the service to be registered.
    /// </summary>
    /// <value>
    ///     The string value that the configuration key must NOT equal for the service to be registered.
    ///     Must be used together with <see cref="ConfigValue" />.
    ///     Default is <c>null</c> (no negative condition).
    /// </value>
    /// <remarks>
    ///     <para>
    ///         The comparison is performed as a case-sensitive string inequality check. The service is registered
    ///         if the configuration value does NOT equal this property value.
    ///     </para>
    ///     <para><strong>Multiple Values:</strong> For excluding multiple values, use comma separation:</para>
    ///     <code>
    /// [ConditionalService(ConfigValue = "Cache:Provider", NotEquals = "InMemory,None")]
    /// </code>
    ///     <para>
    ///         <strong>Null Handling:</strong> If the configuration key is missing or null, it is typically
    ///         considered different from any non-null value, causing the service to be registered.
    ///     </para>
    ///     <para>
    ///         <strong>Use Cases:</strong>
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>Register service unless explicitly disabled: <c>NotEquals = "false"</c></description>
    ///         </item>
    ///         <item>
    ///             <description>Exclude specific implementations: <c>NotEquals = "Mock,Test"</c></description>
    ///         </item>
    ///         <item>
    ///             <description>Register fallback services: <c>NotEquals = "Advanced,Premium"</c></description>
    ///         </item>
    ///     </list>
    ///     <para>
    ///         <strong>Validation:</strong> Cannot be used together with <see cref="Equals" /> on the same
    ///         <see cref="ConfigValue" />, as this would create conflicting conditions.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// [ConditionalService(ConfigValue = "Features:DisableService", NotEquals = "true")]
    /// [ConditionalService(ConfigValue = "Database:Type", NotEquals = "InMemory")]
    /// [ConditionalService(ConfigValue = "Cache:Provider", NotEquals = "None,Disabled")]
    /// </code>
    /// </example>
    public string? NotEquals { get; set; }
}
