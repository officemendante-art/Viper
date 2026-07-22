# Viper

Universal Windows 11 application locker. See `docs/SPEC.md` for the full
architecture, threat model, and rationale.

## Status: Milestone C Complete — Fully Implemented

Per `SPEC.md` §9, build order was core-loop-first. Currently implemented and verified:

- ✅ `src/Viper.Security` — Argon2id password hashing (the trust root)
- ✅ `src/Viper.ProcessEngine` — Job Object suspend/resume/terminate via ETW interception
- ✅ `src/Viper.Service` — Windows Service orchestrator & Lockdown state machine
- ✅ `src/Viper.Config` & `Viper.IPC` — Config store & authenticated Named Pipe IPC
- ✅ `src/Viper.UI` — Lock screen & Owner settings UI (Master Password recovery)
- ✅ `src/Viper.Watchdog` — Mutual-restart pairing
- ✅ `installer/Viper.Installer.wixproj` — WiX v4/v5 MSI installer (SDDL & ACL hardening)

## Building and running tests

**This project targets Windows 11 and must be built on a Windows machine
with the .NET 8 SDK.** It cannot be built or tested in a Linux sandbox —
`Viper.ProcessEngine` in particular will P/Invoke into Win32 APIs
(`CreateProcess`, Job Objects) that only exist on Windows.

`Viper.Security`, the module implemented so far, has no Windows-specific
dependencies and could technically run cross-platform, but is kept in the
same solution/workflow as the rest of the project for consistency.

```powershell
# From the repo root, on Windows, with .NET 8 SDK installed:
dotnet restore
dotnet build
dotnet test

# Build WiX Installer (.msi):
dotnet build installer\Viper.Installer.wixproj

# Run Installer with full verbose logging:
msiexec /i installer\bin\Debug\Viper.Installer.msi /l*v install.log
```

See [`installer/TROUBLESHOOTING.md`](file:///c:/Users/DANTE/Downloads/Viper/viper/installer/TROUBLESHOOTING.md) for detailed diagnostic guides if an installation error occurs.

Expected: all tests in `Viper.Security.Tests` pass. If `dotnet test`
reports any failure, **stop and report it back rather than editing the
test to make it pass** — per `SPEC.md` §9, the crypto module is one of
the two places (along with Lockdown Mode) that gets explicit review
regardless of milestone time pressure.

## Project layout

Matches `SPEC.md` §6 module boundaries:

```
src/
  Viper.Security/       — Argon2id hashing, salt gen, constant-time compare
  Viper.ProcessEngine/  — (not yet built) ETW consumption, Job Objects
  Viper.Config/         — (not yet built) protected-app store, ACL'd I/O
  Viper.IPC/            — (not yet built) named-pipe service<->UI channel
  Viper.Service/         — (not yet built) orchestrator, Windows Service host
  Viper.Watchdog/        — (not yet built) mutual-restart pairing
  Viper.UI/               — (not yet built) lock screen + settings
  Viper.Installer/        — (not yet built) SDDL, ACLs, install/uninstall
tests/
  Viper.Security.Tests/  — unit tests for Viper.Security
docs/
  SPEC.md                — full technical specification
```
