# Viper Application Lock — Comprehensive Progress & Status Report

**Document Name:** `progress.md`  
**Last Updated:** 2026-07-25  
**Current Milestone:** Milestone B (App Management & Smart UX Routing)  
**Git Commit:** `5390ca8` (`main`)  

---

## 1. Executive Summary

**Viper** is a local, offline Windows 11 application locker running as a `LocalSystem` Windows Worker Service (`ViperService`) paired with a self-healing supervisor (`ViperWatchdog`). It intercepts target application launches (e.g., Chrome, Firefox, ChatGPT, Google Drive, WhatsApp) via Event Tracing for Windows (ETW), freezes process trees using Windows Job Objects, and authenticates launches via a logic-free WPF user interface over an ACL-protected Named Pipe (`Viper.IPC`).

All core architecture requirements specified in `docs/SPEC.md` and `AGENTS.md` have been implemented, hardened, and verified with zero build warnings and 100% test suite pass rates.

---

## 2. Module Implementation & Architecture Matrix

| Module | Status | Role & Scope | Key Technologies |
| :--- | :---: | :--- | :--- |
| **`Viper.Security`** | ✅ Complete | Zero-trust cryptographic root (Argon2id hashing, salts, constant-time compare). | Argon2id, `RandomNumberGenerator`, `FixedTimeEquals` |
| **`Viper.ProcessEngine`** | ✅ Complete | Intercepts launches via ETW, freezes process trees in Job Objects. | ETW (`TraceEvent`), `JobObject`, `NtSuspendProcess` |
| **`Viper.IPC`** | ✅ Complete | Authenticated Named Pipe communication (`\\.\pipe\ViperIPC`). | `NamedPipeServerStreamAcl`, Caller PID verification |
| **`Viper.Config`** | ✅ Complete | Machine-wide config & state store (`%ProgramData%\Viper\viper.json`). | Atomic JSON serialization, App state tracking |
| **`Viper.Service`** | ✅ Complete | Session 0 orchestrator running ETW monitor, IPC server, and session launcher. | `Microsoft.Extensions.Hosting.WindowsServices`, `CreateProcessAsUser` |
| **`Viper.Watchdog`** | ✅ Complete | SCM supervisor monitoring `ViperService` with mutual auto-restart logic. | `ServiceController`, SCM supervision |
| **`Viper.UI`** | ✅ Complete | Logic-free WPF interface for Lock Screen, First-Run Setup, and Owner Settings. | WPF, Smart UX Routing, Dark JetBrains Mono theme |
| **`Viper.Installer`** | ✅ Complete | Production WiX v5 64-bit installer (`.msi`) with service registration & ACL setup. | WiX v5, SDDL (`sc sdset`), `icacls` |

---

## 3. History of Fixes & System Evolution

### Phase 1: Cryptographic Root & Job Object Engine
* Implemented `Viper.Security` with Argon2id (`MemoryKib=19456, Iterations=2, Parallelism=1`), 16-byte random salts, and `CryptographicOperations.FixedTimeEquals`.
* Implemented `Viper.ProcessEngine` using `Microsoft-Windows-Kernel-Process` ETW trace session `"Viper-ProcessMonitor"` and Job Object tree freezing with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`.

### Phase 2: Installer & Dependency Packaging
* **Watchdog Runtime Bundle:** Fixed `Package.wxs` to include `Viper.Watchdog.dll`, `Viper.Watchdog.deps.json`, and `Viper.Watchdog.runtimeconfig.json`, resolving the SCM startup crash (`Error 1053` / `Error 7000`).
* **ACL Execution Ordering:** Moved `SetServiceAcl` and `SetWatchdogAcl` custom actions to execute `Before="StartServices"` so security descriptors are applied before executables start.
* **Explicit 64-bit Target:** Added `<Platform>x64</Platform>` to `Viper.Installer.wixproj` ensuring installation natively into `C:\Program Files\Viper\` (64-bit) instead of `(x86)`.
* **Elevated UAC Execution:** Identified that `msiexec /qn` (quiet install) fails with `Error 1925` / `1603` if run without Administrator elevation, and established elevated install procedures.

### Phase 3: Windows Service Lifetime & Async Startup
* Added `Microsoft.Extensions.Hosting.WindowsServices` to `Viper.Service` and `Viper.Watchdog` and invoked `builder.Services.AddWindowsService()` in `Program.cs` for native Windows SCM integration.
* Fixed synchronous blocking during service startup by adding `await Task.Yield()` and wrapping `sc.WaitForStatus(...)` in `Task.Run`.

### Phase 4: Smart UX Routing & Dynamic App Name Displays
* **Smart App Routing (`App.xaml.cs`):** Double-clicking `Viper.UI.exe` directly from File Explorer now inspects `%ProgramData%\Viper\viper.json`:
  * If setup is incomplete -> automatically opens **`SetupWindow`** (First-Run Setup).
  * If setup is complete -> automatically opens **`SettingsWindow`** (Owner Settings).
  * If called by Service -> opens **`MainWindow` (Lock Screen)**.
* **Dynamic Titles:** Fixed `Worker.cs` to pass `"{AppName}"` in launch arguments and updated `MainWindow.xaml` to dynamically display `"{AppName} is locked"` (eliminating hardcoded "Firefox is locked" text).
* **WPF Application Lifetime:** Fixed `App.xaml.cs` by assigning `MainWindow = window;` on startup to prevent premature WPF application exit.

---

## 4. Verification & Test Matrix

```powershell
dotnet test Viper.sln
```

```text
Passed!  - Failed: 0, Passed:  3, Total:  3 - Viper.ProcessEngine.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed: 11, Total: 11 - Viper.Security.Tests.dll (net10.0)
Total Tests Passed: 14 / 14 (100%)
```

### Build Status
* **`dotnet build Viper.sln`**: 0 Errors, 0 Warnings
* **`dotnet build installer\Viper.Installer.wixproj`**: 0 Errors, 0 Warnings
* **Target MSI Location:** [`installer\bin\x64\Debug\Viper.Installer.msi`](file:///C:/Users/DANTE/Downloads/Viper/viper/installer/bin/x64/Debug/Viper.Installer.msi)

---

## 5. Next Planned Milestones

1. **Milestone B Completion:** Validate lock/resume flows across Chrome, Edge, WhatsApp, ChatGPT, and Google Drive without code changes.
2. **Lockdown Mode Recovery Verification:** Test 5-consecutive-failure lockdown triggers and Master Password recovery paths.
3. **Milestone C Hardening:** Service SDDL hardening, IPC DACL validation, and auto-update hash re-registration checks.
