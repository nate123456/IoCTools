# IoCTools

**Eliminate dependency injection boilerplate and scattered service registrations.** IoCTools is a .NET source generator
that lets services declare their own dependencies and lifetime directly in their implementation, turning verbose DI
configuration into simple, maintainable code.

```csharp
// Before: Verbose constructor + separate registration
public class OrderService(IPaymentService payment, IEmailService email, 
    ILogger<OrderService> logger) : IOrderService 
{
    private readonly IPaymentService _payment = payment;
    private readonly IEmailService _email = email;
    private readonly ILogger<OrderService> _logger = logger;
}

// Program.cs
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
// ... many more lines

// After: Clean service with co-located registration
[Service] 
public partial class OrderService : IOrderService
{
    [Inject] private readonly IPaymentService _payment;
    [Inject] private readonly IEmailService _email;
    [Inject] private readonly ILogger<OrderService> _logger;
}

// Program.cs
builder.Services.AddIoCTools[ProjectName]RegisteredServices(); // All services discovered automatically
```

[![NuGet](https://img.shields.io/nuget/v/IoCTools.Abstractions?label=IoCTools.Abstractions)](https://www.nuget.org/packages/IoCTools.Abstractions)
[![NuGet](https://img.shields.io/nuget/v/IoCTools.Generator?label=IoCTools.Generator)](https://www.nuget.org/packages/IoCTools.Generator)
![Status](https://img.shields.io/badge/status-v1.0.0--alpha-green)
![Sample Coverage](https://img.shields.io/badge/sample%20coverage-59%20comprehensive%20examples-brightgreen)

---

## 🚀 Quick Start

**1. Install the packages:**

```xml
<PackageReference Include="IoCTools.Abstractions" Version="*" />
<PackageReference Include="IoCTools.Generator" Version="*" PrivateAssets="all" />
```

**2. Mark your service with `[Service]` and use `[Inject]` for dependencies:**

```csharp
[Service]
public partial class EmailService : IEmailService
{
    [Inject] private readonly ILogger<EmailService> _logger;
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        _logger.LogInformation("Sending email to {To}", to);
        // Implementation here
    }
}
```

**3. Register all services automatically:**

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddIoCTools[ProjectName]RegisteredServices();
var app = builder.Build();
```

**That's it!** 🎉 Your service is now automatically registered with the correct lifetime and all dependencies are
injected.

### 🔧 Optional: Configure Diagnostics

Add these MSBuild properties to your project file to customize diagnostic behavior:

```xml
<PropertyGroup>
  <!-- Configure severity for missing implementations (default: Warning) -->
  <IoCToolsNoImplementationSeverity>Error</IoCToolsNoImplementationSeverity>
  
  <!-- Configure severity for unregistered implementations (default: Warning) -->
  <IoCToolsUnregisteredSeverity>Info</IoCToolsUnregisteredSeverity>
  
  <!-- Disable all dependency validation diagnostics (default: false) -->
  <IoCToolsDisableDiagnostics>true</IoCToolsDisableDiagnostics>
</PropertyGroup>
```

**Severity Options**: `Error`, `Warning`, `Info`, `Hidden`

---

## ✨ Key Benefits

### 🔥 **Co-located Registration**

Service registration happens right where the service is defined, eliminating the hunt through Program.cs

### ⚡ **Zero Boilerplate**

No more verbose constructors, parameter assignments, or manual registration calls

### 🛡️ **Compile-Time Safety**

Comprehensive diagnostics catch missing implementations, circular dependencies, and lifetime mismatches at build time

### 🎯 **IntelliSense Friendly**

Full IDE support with code completion, refactoring, and go-to-definition for generated code

### 📦 **Comprehensive Testing**

Extensive sample application with 59 comprehensive examples covering all features and edge cases

---

## 🎯 Feature Highlights

**Core Features:**

- **[Service Declaration](docs/service-declaration.md)** - Mark services with `[Service]` for automatic registration
- **[Dependency Injection](docs/dependency-injection.md)** - Use `[Inject]` for clean, declarative dependencies
- **[Lifetime Management](docs/lifetime-management.md)** - Singleton, Scoped, Transient with validation
- **[Inheritance Support](docs/inheritance.md)** - Full inheritance hierarchy support with automatic base constructor
  generation

**Advanced Features:**

- **[Configuration Injection](docs/configuration-injection.md)** - Inject configuration values directly with
  `[InjectConfiguration]`
- **[Conditional Services](docs/conditional-services.md)** - Environment and configuration-based service registration
- **[Multi-Interface Registration](docs/multi-interface-registration.md)** - Register services for multiple interfaces
  with instance sharing control
- **[Background Services](docs/background-services.md)** - Simplified background service registration and lifetime
  validation

**Developer Experience:**

- **[Diagnostics](docs/diagnostics.md)** - 25+ diagnostic codes for comprehensive validation
- **[MSBuild Integration](docs/msbuild-configuration.md)** - Configure diagnostic behavior and feature toggles
- **[Namespace Management](docs/namespace-generation.md)** - Automatic using statement generation for clean generated
  code
- **[Advanced Generics](docs/advanced-generics.md)** - Full support for complex generic scenarios and constraints

---

## 📚 Documentation

### Getting Started

- **[Installation](docs/installation.md)** - Setup and configuration
- **[Basic Usage](docs/basic-usage.md)** - Your first IoCTools service
- **[Migration Guide](docs/migration-guide.md)** - Migrating from manual DI registration

### Core Features

- **[Service Declaration](docs/service-declaration.md)** - `[Service]` attribute and lifetime options
- **[Dependency Injection](docs/dependency-injection.md)** - `[Inject]` and `[DependsOn]` patterns
- **[Constructor Generation](docs/constructor-generation.md)** - How constructors are generated and customized
- **[Service Registration](docs/service-registration.md)** - Automatic registration and discovery
- **[Diagnostics Configuration](docs/diagnostics.md)** - MSBuild properties for diagnostic severity control

### Advanced Features

- **[Configuration Injection](docs/configuration-injection.md)** - `[InjectConfiguration]` for direct config binding
- **[Conditional Services](docs/conditional-services.md)** - Environment and configuration-based registration
- **[Multi-Interface Registration](docs/multi-interface-registration.md)** - `[RegisterAsAll]` and instance sharing
- **[Inheritance](docs/inheritance.md)** - Complex inheritance scenarios and base constructor calls
- **[Background Services](docs/background-services.md)** - `[BackgroundService]` attribute and validation
- **[Advanced Generics](docs/advanced-generics.md)** - Complex generic patterns and constraints

### Developer Tools

- **[Diagnostics](docs/diagnostics.md)** - Complete diagnostic reference (IOC001-IOC026+)
- **[MSBuild Configuration](docs/msbuild-configuration.md)** - Customizing diagnostic behavior
- **[Namespace Management](docs/namespace-generation.md)** - Understanding generated using statements
- **[Performance](docs/performance.md)** - Optimization for large codebases
- **[Architectural Limits](ARCHITECTURAL_LIMITS.md)** - Understanding design boundaries and workarounds
- **[Troubleshooting](docs/troubleshooting.md)** - Common issues and solutions

### Integration Guides

- **[ASP.NET Core](docs/aspnet-core-integration.md)** - Web application patterns
- **[Worker Services](docs/worker-service-integration.md)** - Background service applications
- **[Testing](docs/testing.md)** - Testing strategies with IoCTools
- **[Microservices](docs/microservices.md)** - Patterns for distributed applications

---

## 🔥 Real-World Example

Here's a complete e-commerce service showcasing multiple IoCTools features:

```csharp
[Service(Lifetime.Scoped)]
[ConditionalService(Environment = "Production")]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class OrderService : IOrderService, IOrderValidator
{
    [Inject] private readonly IPaymentService _paymentService;
    [Inject] private readonly IEmailService _emailService;
    [Inject] private readonly ILogger<OrderService> _logger;
    
    [InjectConfiguration("OrderProcessing:MaxRetries")] 
    private readonly int _maxRetries;
    
    [InjectConfiguration] 
    private readonly ShippingOptions _shippingOptions;

    public async Task<OrderResult> ProcessOrderAsync(Order order)
    {
        _logger.LogInformation("Processing order {OrderId}", order.Id);
        
        // Use injected configuration
        for (int i = 0; i < _maxRetries; i++)
        {
            var paymentResult = await _paymentService.ProcessPaymentAsync(order.Payment);
            if (paymentResult.Success) break;
        }
        
        // Generated constructor handles all dependencies automatically:
        // public OrderService(IPaymentService paymentService, IEmailService emailService, 
        //     ILogger<OrderService> logger, IConfiguration configuration)
    }
}
```

**Generated registration code:**

```csharp
// Only registers in Production environment
if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production")
{
    services.AddScoped<IOrderService>(provider => provider.GetRequiredService<OrderService>());
    services.AddScoped<IOrderValidator>(provider => provider.GetRequiredService<OrderService>());
    services.AddScoped<OrderService>();
}
```

## 📋 Comprehensive Sample Application

The [IoCTools.Sample project](/IoCTools.Sample) provides **59 comprehensive examples** demonstrating every feature:

- **Basic Services** ([BasicUsageExamples.cs](/IoCTools.Sample/Services/BasicUsageExamples.cs)) - Field injection and lifecycle management
- **Advanced Patterns** ([AdvancedPatternsDemo.cs](/IoCTools.Sample/Services/AdvancedPatternsDemo.cs)) - Complex scenarios and edge cases
- **Configuration Injection** ([ConfigurationInjectionExamples.cs](/IoCTools.Sample/Services/ConfigurationInjectionExamples.cs)) - All configuration binding patterns
- **Multi-Interface Registration** ([MultiInterfaceExamples.cs](/IoCTools.Sample/Services/MultiInterfaceExamples.cs)) - Different modes and instance sharing
- **Background Services** ([BackgroundServiceExamples.cs](/IoCTools.Sample/Services/BackgroundServiceExamples.cs)) - Hosted service integration
- **Conditional Services** ([ConditionalServiceExamples.cs](/IoCTools.Sample/Services/ConditionalServiceExamples.cs)) - Environment and configuration-based registration
- **Diagnostic Examples** ([DiagnosticExamples.cs](/IoCTools.Sample/Services/DiagnosticExamples.cs)) - All 26 diagnostic scenarios
- **Generic Services** ([GenericServiceExamples.cs](/IoCTools.Sample/Services/GenericServiceExamples.cs)) - Complex generic patterns
- **DependsOn Patterns** ([DependsOnExamples.cs](/IoCTools.Sample/Services/DependsOnExamples.cs)) - All naming conventions and configurations

Run the sample application to see all features in action:
```bash
dotnet run --project IoCTools.Sample
```

---

## 🛠️ Development Status

- **✅ Core Features**: Stable and feature-complete with comprehensive sample coverage
- **⚠️ Advanced Features**: Stable with extensive examples, some edge cases documented as architectural limits
- **🚀 v1.0.0-alpha**: Feature-complete alpha release with production-ready architecture and comprehensive validation
- **🏗️ Architectural Limits**: Intentional design boundaries with documented workarounds

**Sample Coverage**: [59 comprehensive examples](/IoCTools.Sample) covering all features, diagnostics, and integration patterns with 100% test success rate

**Note**: Some advanced scenarios (complex access modifiers, extreme generic constraints) are intentional architectural limits. See [Architectural Limits](ARCHITECTURAL_LIMITS.md) for details and workarounds.

---

## 🤝 Contributing

We welcome contributions! See our [Contributing Guide](CONTRIBUTING.md) for details.

**Found a bug?** [Open an issue](https://github.com/your-org/IoCTools/issues)
**Have a feature idea?** [Start a discussion](https://github.com/your-org/IoCTools/discussions)

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🌟 Why Choose IoCTools?

**For Teams**: Eliminates DI configuration drift and makes service dependencies explicit and maintainable

**For Performance**: Zero runtime overhead - all resolution happens at compile time with build-time validation

**For Developers**: IntelliSense-friendly generated code that integrates seamlessly with existing .NET patterns

**For Architecture**: Supports sophisticated patterns like conditional services, multi-interface registration, and
configuration injection while maintaining simplicity

---

*Built with ❤️ for the .NET community*