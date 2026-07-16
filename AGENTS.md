# AGENTS.md — Instructions for AI Coding Agents on Viper

This is the single source of truth for any AI agent (Claude, Gemini,
Antigravity, or otherwise) working on this codebase. `docs/SPEC.md` is the
architecture and rationale; this file is the operating rulebook for how to
implement it. If the two ever conflict, `SPEC.md` wins — flag the
conflict rather than silently picking one.

---

## 0. Before you write any code

Read `docs/SPEC.md` in full. It contains the threat model (§1), the
rejected alternatives and why (§2.1 — do not re-propose IFEO injection,
polling-based detection, or shortcut-repointing; all three were
considered and rejected for documented reasons), and the module
boundaries (§6) this file assumes you already know.

**If you hit a genuine ambiguity the spec doesn't resolve — stop and ask.**
Do not silently pick an interpretation and move on. Two examples of the
right instinct, from this project's actual history: an earlier agent
paused to ask how the service (Session 0, no desktop) actually shows a
UI window, and separately asked whether the ETW trace session needed a
unique name to avoid colliding with other tools. Both were real gaps,
both got resolved and folded into `SPEC.md` (§4.1, §2.2) rather than
guessed at. That is the correct behavior — keep doing it.

---

## Development Principle

The implementation is more important than endless planning.

If there are multiple technically valid solutions:

- Research them.
- Compare them.
- Choose the strongest practical solution.
- Document the decision briefly.
- Continue implementation.

Do not repeatedly stop development for implementation-level decisions.

Only ask the project owner questions when the decision affects product behavior, security policy, or user requirements.

Otherwise, continue building.

---

## 1. Project Overview & Target Environment

- **Project Name:** Viper
- **Description:** A local, offline Windows 11 application locker. Runs
  as a Windows Service (`LocalSystem`) that intercepts target process
  launches via ETW, freezes the entire process tree with a Job Object,
  shows a password prompt in the active user's session, and resumes or
  terminates based on the result.
- **Target OS:** Windows 11 (64-bit) only. This cannot be built, tested,
  or meaningfully reasoned about on Linux/macOS — Job Objects, ETW, and
  Windows Services don't exist there. If you are an agent running in a
  non-Windows sandbox, you can write and review code, but cannot build
  or run it; say so explicitly rather than reporting untested code as
  verified.
- **Tech Stack:** C# / .NET 8 / WPF / Windows Service (Worker Service
  template) / Named Pipes. See `SPEC.md` §7 for the full rationale,
  including why Python, Electron, and raw C++ were rejected.

---

## 2. Directory & Solution Structure

```
Viper/
├── src/
│   ├── Viper.Service/         # Windows Worker Service — Session 0 orchestrator
│   ├── Viper.UI/               # WPF — Lock Screen & Owner Settings, logic-free
│   ├── Viper.Core/              # Orchestration: wires Engine → IPC → Security → Config
│   ├── Viper.Security/          # Argon2id, salting, constant-time compare (trust root)
│   ├── Viper.ProcessEngine/      # ETW consumption, Job Objects, suspend/resume/terminate
│   ├── Viper.Config/              # Protected-app store, Lockdown state, ACL'd I/O
│   ├── Viper.IPC/                  # Authenticated named-pipe service<->UI channel
│   ├── Viper.Watchdog/              # Mutual-restart pairing with Viper.Service
│   └── Viper.Common/                 # Shared constants/types only — no logic
├── tests/
│   ├── Viper.Security.Tests/
│   ├── Viper.ProcessEngine.Tests/
│   ├── Viper.Config.Tests/
│   └── ...one test project per src module with logic worth testing
├── installer/                # WiX config, SDDL/ACL setup scripts
├── docs/
│   └── SPEC.md                # Architecture source of truth — read this first
└── AGENTS.md                  # This file
```

Every `src/` module maps 1:1 to a row in `SPEC.md` §6. Don't add a new
top-level module without updating that table — the dependency graph in
§3 below is exactly what makes the leaf modules independently testable,
and an undocumented new module breaks that guarantee silently.

---

## 3. Dependency & Module Boundaries

Dependencies flow one way, acyclically:

```
Viper.UI            → Viper.IPC
Viper.Service        → Viper.Core
Viper.Core            → Viper.ProcessEngine, Viper.Config, Viper.IPC
Viper.Config           → Viper.Security
Viper.ProcessEngine      → Viper.Common
Viper.Security             → Viper.Common
Viper.Watchdog               → Viper.Service  (via Service Control Manager only — NOT a project reference)
```

- **Leaf modules** (`Viper.Security`, `Viper.ProcessEngine`, `Viper.IPC`,
  `Viper.Common`) have **zero** dependency on Viper's business logic,
  config schema, or UI state. They must be unit-testable with no other
  Viper module loaded. If you find yourself importing `Viper.Config`
  into `Viper.Security` "just for one type," stop — that's the boundary
  breaking, not a shortcut.
- **UI/logic separation:** `Viper.UI` never touches passwords, config,
  or the process engine directly. Every UI action is a message sent
  over `Viper.IPC` to `Viper.Service`, which does the actual work. A
  `Viper.UI` class that calls into `Viper.Security` or reads
  `%ProgramData%\Viper\` directly is a bug, not a convenience.
- **`Viper.Watchdog` is a separate service**, not a library dependency —
  it supervises `Viper.Service` via the SCM (starting/stopping it,
  checking its status), never by referencing its assembly.

---

## 4. Coding Standards

- **Style:** Microsoft C# conventions — PascalCase for types/methods,
  camelCase for locals, `_camelCase` for private fields.
- **Async:** `async`/`await` for all I/O (named pipes, file access).
  Never `.Result` or `.Wait()` — these deadlock easily in service
  contexts and hide errors.
- **P/Invoke:** Win32 signatures live in a dedicated `NativeMethods` (or
  `SafeNativeMethods`) nested static class within the calling class. Set
  `SetLastError = true` and surface `Marshal.GetLastWin32Error()` on
  failure. Wrap OS handles (Job Object handles, process handles, tokens)
  in `SafeHandle` subclasses — never a raw `IntPtr` held past the call
  that produced it.
- **Logging:** `Microsoft.Extensions.Logging` via DI. **Never** log a
  plaintext password, a password hash, a salt, or any Argon2 parameter
  value alongside enough context to be useful for an offline attack.
  "Authentication failed for protected app 'Firefox'" is fine.
  Anything containing password bytes, hash bytes, or salt bytes is not,
  under any log level, including Debug/Trace.

---

## 5. Security & Cryptographic Rules

These mirror `SPEC.md` §3 exactly — this section exists so an agent
implementing, say, `Viper.Config`, doesn't have to re-derive crypto
rules from a different document while working:

- Plaintext passwords never touch disk, ever, in any form (not even
  "temporarily" during a reset flow).
- Password hashing: Argon2id via `Konscious.Security.Cryptography`,
  parameters `MemoryKib=19456, Iterations=2, Parallelism=1` by default
  (`Viper.Security.Argon2Parameters.Default` — already implemented, use
  it rather than reintroducing magic numbers elsewhere).
- Every password gets its own fresh 16-byte random salt
  (`RandomNumberGenerator`), generated independently for the App Unlock
  Password and the Master Password — never derived from each other,
  never reused even if the owner enters the same string for both.
- Zero password buffers with `CryptographicOperations.ZeroMemory`
  immediately after use, in a `finally` block — not "when convenient."
- All password comparisons go through
  `CryptographicOperations.FixedTimeEquals`. A `==`, `.Equals()`, or
  `SequenceEqual()` on password-derived bytes is a bug.
- IPC (`Viper.IPC`) authenticates the connecting process — verify the
  caller's PID against the process the service itself launched, per
  `SPEC.md` §5.3. Don't trust "it connected to the right pipe name" as
  authentication on its own.

**`Viper.Security` and the Lockdown Mode recovery path in
`Viper.Config`/`Viper.Core` are the two places that get human review
regardless of any "keep moving" pressure elsewhere in the workflow**
(`SPEC.md` §9's explicit carve-out). If you're implementing either, flag
the PR/change for review rather than treating it as routine — a subtle
bug in either one either weakens the one thing this software exists to
guarantee, or locks the owner out of their own machine.

---

## 6. Process Interception Mechanics

- **ETW:** subscribe to `Microsoft-Windows-Kernel-Process` for process-
  start events via `TraceEvent`. Use a dedicated, hardcoded, uniquely-
  named trace session (e.g. `"Viper-ProcessMonitor"`) — see `SPEC.md`
  §2.2 for why a shared/default session name is wrong here. Create the
  session in the service's `OnStart`, dispose it cleanly in `OnStop`.
- **Race condition:** never detect-then-suspend. The whole point of the
  ETW-plus-Job-Object design (`SPEC.md` §2.2) is that the target is
  placed in a suspended Job Object before its first thread does
  anything meaningful. If an implementation resorts to polling
  `Process.GetProcesses()` and suspending after the fact, that's the
  exact race condition the spec rejected — don't reintroduce it as an
  "optimization."
- **Multi-process trees:** never manage child processes individually.
  `AssignProcessToJobObject` on the parent, with
  `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` set, cascades to all current and
  future children automatically — that's the mechanism, not a manual
  recursive suspend loop.
- **Session 0 → interactive session UI:** the service cannot show a
  window directly (Session 0 isolation). Use
  `WTSQueryUserToken` + `DuplicateTokenEx` + `CreateProcessAsUser` to
  launch `Viper.UI`'s lock-screen process inside the active console
  session. See `SPEC.md` §4.1 for the no-active-session edge case
  (nobody logged in yet) and the fast-user-switching case.

---

## 7. Configuration & Hardening Rules

- **Storage:** `%ProgramData%\Viper\` — machine-wide, not per-user
  `%AppData%`, since protection applies regardless of which account is
  active.
- **ACLs:** installer configures this directory so only `SYSTEM` and
  `Administrators` have write access; `Users` is explicitly denied
  write (`SPEC.md` §5.1). This is a Milestone C / hardening-phase item
  (`SPEC.md` §9) — don't skip it before shipping, but it's also not
  required for the Milestone A core-loop demo to work locally.
- **Lockdown Mode:** 5 consecutive failed App Unlock Password attempts
  → that app enters Lockdown Mode. While locked down, *even the correct*
  App Unlock Password is rejected. Only a successful Master Password
  authentication clears it. The failed-attempt counter is persisted (not
  reset by a service restart or reboot) and only resets on success or
  explicit owner action via Master Password. Full detail: `SPEC.md` §8.
- **Safe Mode & Administrator bypass:** explicitly out of scope,
  documented as accepted limitations (`SPEC.md` §1, §5.5). Do not write
  a kernel-mode driver or register Viper to run in Safe Mode to try to
  close this gap — that tradeoff was already considered and rejected.

---

## 8. Build Order — Do Not Reorder Without Discussion

Per `SPEC.md` §9, the build is sequenced core-loop-first, not
hardening-first, deliberately:

**Milestone A (stop for review at the end of this milestone):**
1. `Viper.Security` — Argon2id hashing, unit-tested standalone. *(Done —
   see `src/Viper.Security`, `tests/Viper.Security.Tests`.)*
2. `Viper.ProcessEngine` — Job Object suspend/resume/terminate, tested
   against a throwaway dummy `.exe` first, then real Firefox.
3. Minimal `Viper.Service` scaffold (no SDDL hardening yet) wiring ETW
   detection into step 2.
4. `Viper.IPC` + minimal `Viper.UI` lock screen, wired to step 1's real
   password module.
5. End-to-end validation against real Firefox: launch → suspend →
   prompt → correct password → resume; wrong password → deny; close →
   re-lock.

**Milestone B:** config/setup flow, universal-engine validation across
Chrome/Edge/Drive/ChatGPT/WhatsApp (proving zero code changes needed,
only registration — this is the actual test of `SPEC.md` §1.1's central
claim), Lockdown Mode.

**Milestone C:** service SDDL hardening, watchdog pairing, ACLs, named-
pipe DACL, auto-update hash re-registration.

A known-bug tolerance applies to Milestone A per project direction —
minor, documented bugs are acceptable to ship in a first working loop.
It does **not** apply to the crypto module or the Lockdown/Master
Password recovery path (§5 above) — those get reviewed regardless of
which milestone is in flight.

---

## 9. Testing & Validation

- Any change to `Viper.Security` or `Viper.Config` requires xUnit tests
  covering the change before it's considered done.
- Tests must not depend on a running Windows Service, real Firefox, or
  any external hardware — mock/fake at the boundary (e.g.
  `Viper.ProcessEngine.Tests` uses a throwaway dummy `.exe` per §8 step
  2, not real Firefox, for the automated suite; real-Firefox validation
  is a manual step, not a CI test).
- Run `dotnet test` from the repo root before calling anything done. All
  tests pass — not "all except the flaky one."
- This repo cannot be built or tested outside Windows with the .NET 8
  SDK installed. An agent in a non-Windows sandbox should say so
  explicitly rather than claiming tests were run.

---

## 10. Definition of Done

A module or change is complete only when:

1. Compiles under `dotnet build` with zero warnings, zero errors.
2. Relevant unit tests exist and `dotnet test` passes completely.
3. Any cryptographic code uses `FixedTimeEquals` for comparisons and
   `ZeroMemory`/`Array.Clear` for cleanup — verified by re-reading the
   diff, not assumed.
4. No `TODO`, `NotImplementedException`, or placeholder logic remains
   in a code path that's supposed to be functional. A genuinely deferred
   piece (e.g. Milestone C hardening not yet reached) is fine — it's
   tracked in this file's build order, not silently stubbed inside
   Milestone A code.
5. No module boundary from §3 was crossed to make the change easier.
6. If a spec ambiguity was hit, it was raised as a question — not
   silently resolved in whichever direction was fastest to code.
