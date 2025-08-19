# Installation

## Package Installation

IoCTools consists of two packages that work together:

### 1. Install via Package Manager Console
```powershell
Install-Package IoCTools.Abstractions
Install-Package IoCTools.Generator
```

### 2. Install via .NET CLI
```bash
dotnet add package IoCTools.Abstractions
dotnet add package IoCTools.Generator
```

### 3. Install via PackageReference
Add these to your `.csproj` file:

```xml
<PackageReference Include="IoCTools.Abstractions" Version="*" />
<PackageReference Include="IoCTools.Generator" Version="*" PrivateAssets="all" />
```

## Package Breakdown

### IoCTools.Abstractions
- Contains attributes (`[Service]`, `[Inject]`, `[ConditionalService]`, etc.)
- Required at runtime for attribute metadata
- Referenced by your application code

### IoCTools.Generator  
- Source generator that analyzes code and generates DI boilerplate
- **PrivateAssets="all"** ensures it's only used during compilation
- Not deployed with your application

## Requirements

- **.NET 6.0 or later** for full incremental source generator support
- **C# 10.0 or later** for optimal source generator features  
- **MSBuild 17.0 or later** (Visual Studio 2022 17.0+ or equivalent)

**Note**: IoCTools uses incremental source generators (`IIncrementalGenerator`) which require Roslyn 4.0.1+. This is included with .NET 6.0 SDK and later versions.

## Supported Project Types

- ✅ **ASP.NET Core** applications
- ✅ **Console** applications  
- ✅ **Worker Service** applications
- ✅ **Class Libraries** (for shared services)
- ✅ **Blazor** applications
- ✅ **WPF/WinForms** applications
- ✅ **MAUI** applications

## Project Configuration

### Enable Nullable Reference Types (Recommended)
```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

### Enable Latest C# Version (Recommended)
```xml
<PropertyGroup>
  <LangVersion>latest</LangVersion>
</PropertyGroup>
```

## Build Verification

After installation, verify IoCTools is working correctly:

### 1. Create a Test Service
```csharp
using IoCTools.Abstractions.Annotations;

[Service]
public partial class TestService
{
    public string GetMessage() => "IoCTools is working!";
}
```

### 2. Build and Check Generated Files
Build your project and verify generated files appear in:
- **Visual Studio**: Solution Explorer → Dependencies → Analyzers → IoCTools.Generator
- **Rider**: External Libraries → IoCTools.Generator  
- **File System**: `obj/Debug/[TargetFramework]/IoCTools.Generator/` (e.g., `net6.0`, `net8.0`, `net9.0`)

### 3. Verify Registration Method
After build, your project should have a registration extension method:
```csharp
// Method name includes your project/assembly name
// Examples:
// - For project "MyApp": AddIoCToolsMyAppRegisteredServices()
// - For project "Sample": AddIoCToolsSampleRegisteredServices()
builder.Services.AddIoCTools[ProjectName]RegisteredServices();
```

### 4. Test Runtime Registration
Add the registration to your startup code:
```csharp
var builder = WebApplication.CreateBuilder(args);

// Register IoCTools services (no parameters required)
builder.Services.AddIoCTools[ProjectName]RegisteredServices();

var app = builder.Build();
```

### 5. Verify Service Resolution
Test that your service can be resolved:
```csharp
// In a controller or minimal API endpoint
app.MapGet("/test", (TestService testService) => 
    testService.GetMessage());
```

**Build Success Indicators:**
- ✅ No build errors related to IoCTools
- ✅ Generated files appear in the expected locations
- ✅ Registration extension method is available in IntelliSense
- ✅ Services can be resolved at runtime without exceptions

## Next Steps

- **[Basic Usage](basic-usage.md)** - Create your first IoCTools service
- **[Service Declaration](service-declaration.md)** - Learn about the `[Service]` attribute
- **[Dependency Injection](dependency-injection.md)** - Use `[Inject]` for dependencies