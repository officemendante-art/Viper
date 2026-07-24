# AGENTS.md — Instructions for AI Coding Agents on Viper

This is the single source of truth for AI agents working on this codebase. 
**Viper is currently at v2.0.** The original v1 architecture (Windows Services, ETW, Job Objects, WiX, IPC, SCM Watchdogs) was deliberately deleted because it was too complex. **Do not reintroduce these patterns.**

## 1. Project Philosophy
Viper is a minimalist, local-only Windows application locker. 
- **Rule 1:** Simplicity over complexity. If you are writing a named pipe, a Windows Service, or P/Invoking anything other than `NtSuspendProcess`/`NtResumeProcess`, you are violating this rule.
- **Rule 2:** The app runs as a standard desktop application. It does NOT require administrator privileges to run (only to install).
- **Rule 3:** Configuration is stored in `%ProgramData%\Viper\config.json`.
- **Rule 4:** The UI is built in WPF (.NET 10). Keep the styling strict, dark mode, with a modern aesthetic (no gradients, sharp corners).

## 2. Process Engine Mechanics
- **Suspend/Resume:** The engine polls for protected apps via `Process.GetProcessesByName()`. When a match is found, it uses `NtSuspendProcess` to freeze it. It then prompts the user for the password. On success, it calls `NtResumeProcess`. On failure, it kills the process. 
- **NO ETW:** Do not use Event Tracing for Windows. It requires admin rights and complex asynchronous event handling.
- **NO Job Objects:** Do not assign processes to Job Objects.

## 3. Security Rules
- Hashing must use `Konscious.Security.Cryptography.Argon2`.
- Passwords must use a 16-byte random salt.
- Comparisons must use `CryptographicOperations.FixedTimeEquals`.
- Plaintext password buffers must be zeroed using `CryptographicOperations.ZeroMemory` immediately after use.

## 4. Definition of Done
1. Code compiles under `dotnet build` with zero warnings.
2. `dotnet test` passes.
3. No enterprise patterns (services, IPC, ETW) were reintroduced.
