# IOC086 migration measurements

MEASURED on 2026-09-08 with .NET SDK 10.0.105, macOS arm64.

Base: `7d93ed04ca99f8a715a38ee99c1d6ad4e5f5d022`.
Branch: `fix/ioc41-migration-message`.

All commands run from the `lane-ioc41` worktree. Each test command redirects stdout and stderr to its listed local log.
The excerpts below are tracked; the full local logs under `artifacts/ioc41/` are ignored.

## message-before.log

MEASURED command: `dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --filter FullyQualifiedName~ManualRegistrationSuggestionTests > artifacts/ioc41/message-before.log 2>&1`. Exit: `1`.

```text
   Expected diagnostics[0].GetMessage() "'Test.IService' is registered manually as Scoped but the implementation 'Test.Service' lacks IoCTools lifetime attributes. Consider adding [Scoped]/[Singleton]/[Transient] (and [RegisterAs]) instead of manual registration." to contain "call the generated AddTestAssemblyRegisteredServices(...) extension".
Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1, Duration: 584 ms - IoCTools.Generator.Tests.dll (net10.0)
```

## manual-after.log

MEASURED command: `dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --filter 'FullyQualifiedName~ManualRegistration' > artifacts/ioc41/manual-after.log 2>&1`. Exit: `1`.

```text
   Expected result.HasErrors to be false, but found True.
Failed!  - Failed:     1, Passed:    25, Skipped:     0, Total:    26, Duration: 751 ms - IoCTools.Generator.Tests.dll (net10.0)
```

## runtime.log

MEASURED command: `dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj -c Debug --filter FullyQualifiedName~ManualRegistrationMigrationTests > artifacts/ioc41/runtime.log 2>&1`. Exit: `0`.

```text
Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2, Duration: 669 ms - IoCTools.Generator.Tests.dll (net10.0)
```

## manual-final.log

MEASURED command: `dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj --filter 'FullyQualifiedName~ManualRegistration' > artifacts/ioc41/manual-final.log 2>&1`. Exit: `0`.

```text
Passed!  - Failed:     0, Passed:    26, Skipped:     0, Total:    26, Duration: 729 ms - IoCTools.Generator.Tests.dll (net10.0)
```

The first message assertion fails against the original descriptor. The final registration group executes all 26 tests.
The intermediate group failure comes from an incorrect namespace import in the new runtime test, corrected before the final runs.
The runtime tests compile the consumer and execute its composition method.
The complete migration resolves the same singleton twice. The incomplete migration compiles and throws `InvalidOperationException` during resolution.

MEASURED by diff inspection: the default `Warning` severity and all eligibility conditions remain unchanged.
The diagnostic uses the current implementation assembly name with the existing generator naming rules.
For a referenced implementation assembly, the message identifies the assembly instead of guessing an emitted method.

NOT_ESTABLISHED: runtime behavior for all registration mappings and generation options; these runtime tests cover a plain sealed singleton.
`CONTEXT.md` is absent from the worktree and the tracked file list. `AGENTS.md` specifies the solution test gate.

Related: sansiquay/IoCTools#41 and sansiquay/keel#356.

## Solution gate

MEASURED command: `dotnet test IoCTools.sln -c Debug > artifacts/ioc41/gate-debug.log 2>&1`. Exit: `1`.

```text
Testhost process for source(s) '/Users/dev/Documents/projects/sansiquay-workbench/repos/IoCTools/.worktrees/lane-ioc41/IoCTools.Generator.Shared.Tests/bin/Debug/net8.0/IoCTools.Generator.Shared.Tests.dll' exited with error: You must install or update .NET to run this application.
Framework: 'Microsoft.NETCore.App', version '8.0.0' (arm64)
Testhost process for source(s) '/Users/dev/Documents/projects/sansiquay-workbench/repos/IoCTools/.worktrees/lane-ioc41/IoCTools.Abstractions.Tests/bin/Debug/net8.0/IoCTools.Abstractions.Tests.dll' exited with error: You must install or update .NET to run this application.
Framework: 'Microsoft.NETCore.App', version '8.0.0' (arm64)
Test Run Aborted.
Test Run Aborted.
Testhost process for source(s) '/Users/dev/Documents/projects/sansiquay-workbench/repos/IoCTools/.worktrees/lane-ioc41/IoCTools.Generator.Analyzer.Tests/bin/Debug/net8.0/IoCTools.Generator.Analyzer.Tests.dll' exited with error: You must install or update .NET to run this application.
Framework: 'Microsoft.NETCore.App', version '8.0.0' (arm64)
Test Run Aborted.
Passed!  - Failed:     0, Passed:    32, Skipped:     0, Total:    32, Duration: 1 s - IoCTools.FluentValidation.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:    57, Skipped:     0, Total:    57, Duration: 4 s - IoCTools.Testing.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:  1979, Skipped:     0, Total:  1979, Duration: 26 s - IoCTools.Generator.Tests.dll (net10.0)
Passed!  - Failed:     0, Passed:   118, Skipped:     0, Total:   118, Duration: 32 s - IoCTools.Tools.Cli.Tests.dll (net10.0)
Test Run Aborted.
```

MEASURED: three net8.0 test projects abort because the installed runtime set contains .NET 10.0.5 but lacks .NET 8.
These projects are Abstractions.Tests, Generator.Shared.Tests, and Generator.Analyzer.Tests.
The CLI testhost remains active without a result after five minutes. I send SIGTERM to that owned testhost (PID 56922).
The solution command then returns exit 1. No full solution pass is claimed.
After SIGTERM, the log contains a CLI summary with 118 passed tests and a final `Test Run Aborted.` line.
This output does not establish successful completion of the CLI process.

NOT_ESTABLISHED: the three net8.0 projects, successful CLI process completion, Release verification, package validation, and hosted CI.
The successful project results above are executed tests, not cached test receipts.

## Other checks

MEASURED: `bash scripts/verify-ideas-backlog.sh > artifacts/ioc41/backlog.log 2>&1` exits `0`.
MEASURED: `git diff --check` exits `0`.

The runtime test development also has three failed attempts (exit 1 each).
They use `dotnet test IoCTools.Generator.Tests/IoCTools.Generator.Tests.csproj -c Debug --filter FullyQualifiedName~ManualRegistrationMigrationTests`.
Their local logs are `runtime-build-failed.log`, `runtime-namespace-failed.log`, and `runtime-diagnostic-failed.log` under `artifacts/ioc41/`.
These failures concern the test context disposal and the generated namespace import. The final runtime log above supersedes them.
