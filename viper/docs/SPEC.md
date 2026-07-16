# Viper — Technical Specification & Architecture

Version 1.3 — Approved for module-by-module implementation. Round 3 closed
Session 0 UI isolation (§4.1) and ETW trace session naming (§2.2). Round 4
corrects a real error introduced in the original draft: §2.2 step 4
previously specified `CREATE_SUSPENDED`, which is only achievable when Viper
itself calls `CreateProcess` — not applicable when intercepting a
third-party launch (e.g. a user double-clicking Firefox). Replaced with
`NtSuspendProcess`/`NtResumeProcess`, explicitly documented as an
undocumented-API tradeoff with a best-effort (not guaranteed-zero) race
window. §4.1 also corrected: Session 0 privileges must be explicitly enabled
via `AdjustTokenPrivileges`, not assumed active by default.

---

## 1. Purpose & Threat Model

**What this is:** A local, offline Windows 11 service that intercepts the launch of
designated applications (Firefox, Chrome, Google Drive, WhatsApp Desktop, ChatGPT
Desktop, or any arbitrary `.exe`) and blocks them behind a password prompt until the
correct App Unlock Password is entered.

**Who this stops:** A casual coworker, family member, or opportunistic person with
physical access to an unattended, unlocked Windows session — someone with no special
technical skill and no administrator credentials on the machine.

**Who this does NOT stop, by design, and why that's acceptable:**
A local Windows Administrator can always, eventually, disable any user-mode security
software — end its process, disable its service, boot into Safe Mode, or reinstall
Windows entirely. This is a structural property of Windows, not a flaw in this
software. No user-mode application can close this gap; anything claiming otherwise is
misrepresenting Windows' security model. We explicitly do not chase this — see
"Non-Goals" below.

**Non-Goals:**
- Defeating a determined local Administrator.
- Defeating anyone with physical access to boot alternate media (Linux USB, Windows
  Recovery Environment) and access the disk directly.
- Protecting data at rest against disk removal / offline access.
- Any cloud, account, sync, telemetry, or licensing-server component.

This scoping is important — it is what makes the strong parts of the design (below)
achievable instead of an unfulfillable promise.

### 1.1 Universal engine — no per-app modules

**Decision:** The Protection Engine is application-agnostic. There is no
`FirefoxModule`, `ChromeModule`, or `WhatsAppModule` in the codebase. Instead:

```
Protection Engine
      ↓
Protected Applications Store  (list of {path, hash, display name, added date})
      ↓
Any registered .exe — Firefox, Chrome, Edge, Google Drive, ChatGPT Desktop,
WhatsApp Desktop, or anything else the owner adds via the UI
```

Adding a new protected app is a **data operation** (append a record to the
store), never a **code change**. Firefox is the first *test case* used to
validate the engine end-to-end, not a special code path. The same is true for
every app listed in this document — Firefox is used as the running example
throughout because it's concrete, but every mechanism described applies
identically to any `.exe` the owner registers.

Testing scope for the core-loop milestone (§9): Firefox first (end-to-end
validation), then Chrome, Edge, Google Drive for Desktop, ChatGPT Desktop, and
WhatsApp Desktop, confirming no code changes were needed between apps —
only registration.

---

## 2. Chosen Architecture (with rejected alternatives)

### 2.1 Alternatives considered and rejected

| Approach | Why rejected |
|---|---|
| **IFEO (Image File Execution Options) debugger redirection** | Technically the "cleanest" interception point, but it is a documented MITRE ATT&CK technique (T1546.012) used by real malware (SUNBURST, SDBbot). Modern Defender/EDR heuristics specifically watch this registry key. Building production software on a path our own OS vendor treats as a malware signature is not a durable foundation — it invites false-positive quarantine of our own tool, unpredictably, on Defender definition updates we don't control. |
| **Polling process list (`psutil`-style) + suspend-after-detect** | Confirmed by three independent research passes: creates a race condition where the browser's UI (and cached data) can render for a fraction of a second before suspension lands. Does not naturally solve multi-process browser trees. |
| **Filter driver / kernel-mode (WFP, minifilter)** | Strongest possible interception point, and the mechanism real commercial tools (Folder Guard) use — but requires a signed kernel driver (Microsoft-mandated EV code signing + attestation signing for Windows 11), a categorically larger engineering and maintenance burden, and real risk of BSOD-class bugs if wrong. Disproportionate for the stated threat model (casual coworkers, not adversaries). Documented here as the natural v2 path if requirements ever escalate to "must resist a technical admin." |
| **Renamed-exe wrapper replacement** | Destroyed by the first browser auto-update, per all three research reports. Rejected outright. |

### 2.2 Selected architecture: Job-Object-based launch interception, running as a protected Windows Service

**Core mechanism — solving the race condition:**

Rather than detecting-then-suspending (which loses the race), and rather than
relying on shortcuts (rejected per review — shortcuts are trivially recreated,
copied, or bypassed by launching the `.exe` directly from its install folder,
so they cannot be the *primary* mechanism, only ever a convenience), interception
is based entirely on monitoring real process creation at the OS level:

1. The service subscribes to the **Windows "Process Creation" ETW provider**
   (`Microsoft-Windows-Kernel-Process`), which delivers process-start events
   system-wide — regardless of how the process was launched: Start Menu,
   taskbar pin, desktop shortcut, a portable copy run directly from a USB
   drive, `cmd.exe`, PowerShell, or any other path. This is what makes the
   engine genuinely launch-vector-agnostic rather than shortcut-dependent.
   Event delivery latency is sub-millisecond, fast enough in practice that the
   service can request an immediate suspend before the target's main thread
   executes meaningful code.
2. On detection, the launcher immediately creates a **Job Object** and assigns the
   entire process tree to it (`AssignProcessToJobObject`). Job Objects in Windows
   guarantee that *all current and future child processes* of a process placed in
   the job are governed together — this directly solves the "browser spawns
   multiple child processes" problem the research flagged, since Chrome/Firefox's
   renderer/GPU/utility processes are all children of the main process and
   automatically inherit job membership.
3. The job is created with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` — meaning if our
   service dies uncleanly, Windows itself guarantees the suspended tree is torn
   down rather than left in a zombie "half-visible" state.
4. **Suspension mechanism — corrected from an earlier draft error.** The original
   architecture stated the target is created with `CREATE_SUSPENDED`. That is only
   possible when *we* are the one calling `CreateProcess` — for the actual use
   case (a user double-clicking Firefox from the Start Menu, Explorer, or a
   shortcut), Explorer calls `CreateProcess`, not Viper, and we only learn about
   it via the ETW event *after* the process already exists. `CREATE_SUSPENDED`
   is therefore not applicable to third-party-launched processes and is corrected
   here rather than left as a spec/implementation contradiction:

   - On the ETW `win:Start` event, the service immediately calls
     `NtSuspendProcess` (`ntdll.dll`) on the new process, which suspends every
     thread in the process at once. This is an **undocumented Windows API** —
     it has no official Microsoft documentation or forward-compatibility
     guarantee, unlike every other API this design relies on. It is nonetheless
     the correct choice here: it is the same mechanism used by long-standing,
     widely-trusted tools (Process Explorer, Process Hacker) for exactly this
     purpose, and the alternative — a kernel-mode filter driver that could
     guarantee pre-execution suspension — was already rejected in §2.1 for this
     threat model. This tradeoff is deliberate and documented, not an oversight.
   - Immediately after, the process is assigned to the Job Object (step 2 above)
     with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`.
   - **This is best-effort, not a hard guarantee.** Between the actual
     `CreateProcess` call (by Explorer) and our `NtSuspendProcess` call landing,
     there is a real, non-zero window — typically sub-millisecond given ETW's
     delivery latency — during which the target process's main thread could
     execute. In practice this window is far shorter than the time needed to
     create a window and paint visible content (most GUI frameworks haven't
     even entered their message loop yet at this point), so the practical risk
     of a visible flash of cached content is very low but **not mathematically
     zero**. This is consistent with the threat model in §1: deterring a casual
     coworker, not providing a formal real-time guarantee against a
     sub-millisecond timing attack.
5. The lock-screen prompt (a separate, always-trusted process, see §4) is shown.
6. **On correct password:** `NtResumeProcess` — the symmetric counterpart to
   `NtSuspendProcess`, resuming every thread that was suspended. (`ResumeThread`
   alone would only resume one thread and is not the correct counterpart once
   suspension happens via `NtSuspendProcess` rather than `CREATE_SUSPENDED`.)
   The app resumes from wherever its threads actually were — for a process this
   young, functionally identical to a normal fresh launch, since so little had
   executed.
7. **On failed / cancelled auth:** the entire Job Object is terminated
   (`TerminateJobObject`), cleanly killing every process in the tree at once —
   this avoids the "ungraceful termination → Restore Session prompt" failure mode
   called out in the research, because from the browser's own perspective it never
   got far enough to create session state worth restoring.
8. **Re-lock on exit:** the service holds a handle to the Job Object and waits on
   it; when the last process in the tree exits, the protection state resets to
   locked for that app, satisfying "every time it's closed, it locks again."

**Why a Windows Service, not a tray app:**

- Services start at boot, before any user logs in, closing the "start the PC and
  immediately click Firefox before the tray app has loaded" gap.
- Services are not listed in the simple Task Manager "Apps" tab (only under
  "Services" / `services.msc` / "Details"), meaningfully raising the skill floor
  needed to even find the process to stop — matching the stated threat model of
  "casual coworker," not "penetration tester."
- Services can be configured with **Recovery Actions** (`sc.exe failure` /
  Service Control Manager settings) to auto-restart on crash or on being killed —
  Windows itself will relaunch a terminated service process within seconds.
- Services can run under `LocalSystem`, so a Standard (non-admin) user account
  cannot open a handle to stop, pause, or reconfigure the service — Windows'
  Service Control Manager enforces this natively via the service's security
  descriptor (SDDL), which we explicitly lock down (see §5.2).

**Self-protection — watchdog pairing:**

A second minimal process (`Viper.Watchdog.exe`), also installed as a service,
monitors the primary `Viper.Service.exe`. Each holds a named, signaled event that
the other polls; if either disappears, the survivor recreates it via the Service
Control Manager. This mutual-restart pattern means a casual user stopping one
service (if they somehow got access to `services.msc`) doesn't durably disable
protection — it self-heals within seconds. This is a deterrent multiplier, not a
guarantee (an admin can still stop both, or disable the SCM entries outright — see
§1 Non-Goals).

---

## 3. Password & Cryptography Design

Two independent secrets, two independent purposes — never shared, never
interchangeable:

| | App Unlock Password | Master Password |
|---|---|---|
| Purpose | Gate on every protected-app launch | Gate on all configuration changes |
| Entered | Every time a locked app is opened | Only when changing settings/apps/passwords, or uninstalling |
| Forgotten → | Reset via Master Password | **Unrecoverable** — full local reset only |

### 3.1 Hashing

- **Algorithm: Argon2id**, the currently-recommended password hashing algorithm
  (OWASP, and winner of the Password Hashing Competition), chosen specifically for
  its resistance to both GPU/ASIC cracking (memory-hard) and side-channel timing
  issues (the "id" hybrid variant resists both GPU and cache-timing attacks better
  than pure Argon2i or Argon2d).
- **Parameters** (tunable, stored in config, versioned — see §3.4):
  memory=`19456` KiB (19 MiB), iterations=`2`, parallelism=`1` as a starting point
  in line with current OWASP guidance for interactive login use; documented as
  configurable so hardware-appropriate tuning can happen at install time without
  a code change.
- **Salt:** 16-byte cryptographically secure random salt per password
  (`RNGCryptoServiceProvider` / `System.Security.Cryptography.RandomNumberGenerator`
  on .NET), generated fresh for the App Unlock Password and, separately, fresh
  again for the Master Password. Never reused.
- **Storage:** only `(salt, hash, algorithm_id, parameters)` tuple is written to
  disk. The plaintext password exists only transiently in process memory during
  hashing/verification.
- **Comparison:** constant-time comparison of hash bytes
  (`CryptographicOperations.FixedTimeEquals` in .NET, which is specifically
  designed for this purpose) — never a short-circuiting `==` or `string.Equals`.
- **Memory hygiene:** password material is held in `SecureString` where the
  runtime supports it, and manually zeroed (`Array.Clear` on the byte buffer)
  immediately after the hash/verify call returns, minimizing the window it's
  recoverable via memory-dump.

### 3.2 Algorithm agility

The stored record includes an explicit `algorithm_id` and `params` blob (not just
a raw hash), e.g.:

```
{
  "algorithm": "argon2id",
  "version": 1,
  "salt": "<base64>",
  "hash": "<base64>",
  "params": { "memory_kib": 19456, "iterations": 2, "parallelism": 1 }
}
```

This means a future version can introduce a new algorithm or re-tuned parameters,
and verify old records against their original parameters while re-hashing under
the new scheme transparently on next successful login — a standard "lazy
migration" pattern — without a breaking redesign of the storage format.

### 3.3 No recovery, by design

- Master Password: no recovery path exists. Setup screen displays a clear,
  un-skippable warning to this effect requiring explicit acknowledgment before
  the password is even set.
- App Unlock Password: recoverable *only* via Master Password (since the Master
  Password holder is definitionally the trusted owner).
- If the Master Password is lost, the only path is a full local reset — the
  service is stopped (requires admin), the encrypted config store is deleted, and
  setup runs again from scratch. This is documented, not hidden.

### 3.4 Password composition

- No practical length ceiling — support at minimum 256 characters, since long
  high-entropy passwords are the stated requirement rather than composition
  rules; we don't impose artificial complexity requirements (per current
  NIST 800-63B guidance, which favors length over forced character-class rules),
  though the input allows the full range of uppercase/lowercase/digit/symbol/
  Unicode input.

---

### 4.1 Session 0 isolation — how the UI actually gets on screen

`Viper.Service` runs as a Windows Service under `LocalSystem`, which since
Windows Vista means it executes in **Session 0** — an isolated session with
no desktop, no window station a user can see, and no ability to directly
show a window on the interactive user's screen. This is a hard OS boundary,
not a permissions setting, and it means "the service just pops up a WPF
window" is not actually possible as stated anywhere else in this document —
this section makes the real mechanism explicit.

**Mechanism:** when the service needs to show the lock screen, it:

1. Calls `WTSQueryUserToken` to obtain a primary token for the currently
   active interactive session (identified via
   `WTSGetActiveConsoleSessionId`).
2. Duplicates that token (`DuplicateTokenEx`) to a primary token suitable
   for process creation.
3. Calls `CreateProcessAsUser` with that token to launch
   `Viper.LockScreen.exe` **inside the interactive user's session**, not
   Session 0 — this is what makes the window actually visible and
   interactive to whoever is sitting at the keyboard.
4. The service must hold `SE_ASSIGNPRIMARYTOKEN_NAME` and
   `SE_INCREASE_QUOTA_NAME` privileges to do this. `LocalSystem`'s token
   contains both by default — but Windows privileges are **present-but-disabled**
   by default even when held; they must be explicitly turned on for the current
   process via `AdjustTokenPrivileges` before the calls in steps 1-3 will
   succeed. Do not assume they are already active — call
   `AdjustTokenPrivileges` (enabling both) once during service startup, check
   its return value, and fail loudly (log + refuse to start the interception
   loop) rather than silently if it fails, since a silent failure here means
   step 3 will throw an unclear access-denied error the first time a lock
   screen actually needs to appear.

**No interactive user / locked Windows session edge case:** if
`WTSGetActiveConsoleSessionId` returns no active session (e.g. the Windows
login screen itself is showing, nobody is logged in yet), there is nothing
to protect yet either — a protected app cannot be launched by anyone until
someone logs into Windows first. The service should handle this case by
simply waiting for a session-connect notification
(`WTSRegisterSessionNotification`) rather than treating it as an error.

**Multi-user machines:** if Windows Fast User Switching is in play, the
service tracks the *currently active* console session at the moment of
each interception — the lock screen always appears in front of whoever is
actually at the keyboard right now, not a stale session.

### 4.2 UI behavior

A minimal, separate, always-on-top process (`Viper.LockScreen.exe`) launched by the
service in an isolated session — deliberately dumb and small, since it is
attack surface:

- Password field, masked by default.
- Show/Hide toggle.
- Unlock button + Enter-to-submit.
- Cancel/Close → denies the launch (terminates the suspended job, per §2.2 step 7).
- Generic error message on failure ("Incorrect password.") — no distinction
  between "wrong password" and any other failure mode, to avoid leaking state.
- No password is ever pre-filled, cached, or remembered between launches —
  every single invocation requires fresh entry, per your requirement.
- No network calls, no telemetry, no external resource loading — fully static,
  fully offline, reducing its attack surface to just local IPC with the service.
- Runs at a **matching or higher integrity level** than a standard user process,
  and the IPC channel to the service is authenticated (see §5.3) so a fake,
  spoofed "lock screen" cannot be used to phish the password to a malicious
  process instead of the real service.

---

## 5. Hardening Details

### 5.1 Configuration storage

- Location: `%ProgramData%\Viper\` (machine-wide, not per-user `%AppData%`,
  since protection must apply regardless of which Windows account is active) —
  `ProgramData` is writable by admins/SYSTEM only by default.
- ACL'd at install time (via `icacls`) to explicitly deny write access to the
  `Users` group, leaving only `SYSTEM` and `Administrators` with write access —
  so even a Standard user who somehow locates the config file cannot tamper
  with it, only the service (running as SYSTEM) can.
- Config includes: list of protected executables (by path + hash, see §5.4),
  the two password records (§3.2 format), and tunable settings — all local,
  all plaintext-password-free.

### 5.2 Service security descriptor

- The service is installed with an explicit SDDL security descriptor
  (`sc.exe sdset`) that denies `SERVICE_STOP`, `SERVICE_PAUSE_CONTINUE`, and
  `SERVICE_CHANGE_CONFIG` rights to the `Interactive Users` / `Authenticated
  Users` SID, leaving only `Administrators`/`SYSTEM` able to control the
  service via the Service Control Manager — this is the specific Windows
  mechanism that stops a Standard-user "casual coworker" from disabling the
  service even via `services.msc`, going beyond what several of the reviewed
  commercial competitors (AppCrypt, GiliSoft) reportedly do, per the research.

### 5.3 IPC authentication

- Named pipe between `Viper.LockScreen.exe` and `Viper.Service.exe`, created with
  an explicit security descriptor restricting connection to processes launched
  by the service itself (verified via the connecting process's PID → parent
  chain, and the pipe's own DACL) — preventing a rogue process from impersonating
  either end.

### 5.4 Target identification — beyond filename matching

Several competitor tools (per research) key purely off filename/path, which
breaks the moment someone runs a portable copy from a USB drive. We mitigate,
without eliminating, this:

- Primary match: full path + file hash (SHA-256) of the target executable,
  refreshed automatically on detected browser auto-updates (the service
  recognizes a version-bump via the file's `FileVersionInfo` and silently
  re-registers the new hash rather than requiring manual admin re-linking —
  directly solving the "orphaned lock rule after auto-update" problem GiliSoft
  reportedly has).
- Secondary, best-effort match: process image name globally (so a portable
  `firefox.exe` run from a USB drive is *also* caught by name, even though it
  isn't the hash-verified installed copy) — this is explicitly documented as a
  deterrent-strength, not guarantee-strength, control, consistent with our
  stated threat model.

### 5.5 Safe Mode

Per research, Safe Mode is a documented weak point for nearly all third-party
lockers, since Windows disables non-Microsoft services and drivers by default
in Safe Mode.

**Decision: accepted as a documented limitation.** Not implemented. Reaching
Safe Mode requires physical access, a deliberate reboot, and (on most Windows
11 setups) admin credentials — outside the stated "casual coworker" threat
model, and inside the Non-Goals already scoped in §1. Registering the service
to also run in Safe Mode was rejected: it would add real complexity and a real
risk (a service bug becoming a boot-blocking liability) for a threat already
out of scope. This limitation should be stated plainly in user-facing docs,
not hidden.

---

## 6. Module Boundaries

Per review, the codebase is organized as loosely-coupled modules with single
responsibilities, so extending or replacing one doesn't require touching the
others:

| Module | Responsibility | Depends on |
|---|---|---|
| `Viper.Security` | Argon2id hashing, salt generation, constant-time compare, memory hygiene (§3) | nothing (leaf module — the trust root) |
| `Viper.ProcessEngine` | ETW consumption, Job Object suspend/resume/terminate, process-tree handling (§2.2) | nothing (leaf module) |
| `Viper.Config` | Protected-app store, password records, Lockdown state, ACL'd `%ProgramData%` I/O (§5.1, §7) | `Viper.Security` (to store hash records, never plaintext) |
| `Viper.IPC` | Named-pipe channel + authentication between service and UI (§5.3) | nothing (leaf module) |
| `Viper.Service` | Orchestrates: wires ProcessEngine detections → IPC → Security verification → Config lookups; owns the Lockdown Mode state machine (§8) | `Viper.ProcessEngine`, `Viper.Security`, `Viper.Config`, `Viper.IPC` |
| `Viper.Watchdog` | Mutual-restart pairing with `Viper.Service` (§2.2) | `Viper.Service` (via SCM only, not a code reference) |
| `Viper.UI` | Lock screen + owner settings window; talks to the service *only* through `Viper.IPC`, contains no business logic itself (per review's UI/logic separation) | `Viper.IPC` |
| `Viper.Installer` | Service registration, SDDL, ACLs, uninstall/reset flow (§5.1, §5.2) | none at runtime (build/install-time only) |

The dependency direction is deliberately one-way and acyclic: `Security`,
`ProcessEngine`, and `IPC` are leaf modules with zero knowledge of each
other or of Viper's business logic, which is what lets each be unit-tested
in complete isolation (this is why Milestone A, §9, can build and verify
`Viper.Security` before anything else exists).

---

## 7. Technology Stack

| Component | Choice | Why |
|---|---|---|
| Service + Launcher | **C# / .NET 10 (Worker Service)** | First-class Windows Service support, first-class Job Object / P/Invoke access to the Win32 APIs this design needs (`CreateProcess`, `AssignProcessToJobObject`, `ResumeThread`), memory-safe by default (versus C++, which the research didn't require and which meaningfully raises maintenance cost), strong crypto libraries (`System.Security.Cryptography`) with Argon2id available via a well-maintained NuGet package (`Konscious.Security.Cryptography`). *Note: Originally scoped as .NET 8, updated to .NET 10.0 to match the local development environment constraints.* |
| Lock screen UI | **WPF** (.NET) | Lightweight, native-feeling, no Chromium/Electron overhead (which would be ironic for a *browser*-locking tool), easy always-on-top + no-taskbar-icon configuration. |
| IPC | Named Pipes (`System.IO.Pipes`) | Built into .NET, supports the ACL/security-descriptor requirements in §5.3 natively. |
| Process interception | **Win32 API via P/Invoke** (`CreateProcess`, Job Objects, ETW consumption via `Microsoft.Diagnostics.Tracing.TraceEvent`) | No safe, high-level .NET wrapper exists for these — direct P/Invoke is standard practice here and is what the underlying OS actually expects. |

**Rejected:** Electron (ironic for a browser-locker, heavier footprint, weaker
native Win32 access), Rust (excellent fit technically, but higher development
time for no capability gain here, and .NET's Job Object + service tooling is
more mature for this specific task), plain C++ (more control, but memory-safety
risk in a security tool is a real cost the research didn't require us to pay).

---

## 8. Failed-Attempt Policy — Lockdown Mode

**Decision:** no unlimited retries, and no silent growing-delay-only scheme
either — a hard Lockdown Mode gated behind the Master Password, chosen
specifically to avoid two opposite failure modes: brute-forceability on one
side, and an accidental-typo/Caps-Lock permanent self-lockout on the other.

- The App Unlock Password gets **5 consecutive incorrect attempts** per
  protected app.
- On the 5th failure, that app enters **Lockdown Mode**:
  - The App Unlock Password is disabled entirely for that app — even the
    *correct* password is rejected while Lockdown Mode is active. This is
    deliberate: it removes any incentive for an attacker to keep guessing.
  - The suspended process tree for that launch attempt is terminated
    (per §2.2 step 7).
  - The only way out of Lockdown Mode is opening Viper's owner-facing UI and
    authenticating with the **Master Password**.
- After a successful Master Password authentication, the owner can:
  - Reset the failed-attempt counter for that app.
  - Clear Lockdown Mode and restore normal App Unlock Password access.
  - Optionally rotate the App Unlock Password, if they suspect it was being
    guessed.
- The failed-attempt counter is **persisted** (in the ACL'd
  `%ProgramData%\Viper\` store, §5.1) so it survives service restarts and
  full reboots — a restart is not a way to reset the counter.
- The counter resets to zero only on: (a) a successful App Unlock Password
  entry, or (b) the owner explicitly clearing Lockdown Mode via Master
  Password.

This is intentionally **not** a permanent, irreversible lockout — the
Master Password always provides a recovery path, consistent with §3's
principle that only the Master Password itself is unrecoverable, never the
app-level state.

---

## 9. Build Sequence — Core Loop First

Per review, the build order is re-sequenced to prioritize a working
end-to-end flow over full hardening. Watchdog pairing and the full ACL/SDDL
hardening pass are explicitly deferred to *after* the core loop works, not
dropped — they remain required for the release Viper is meant to be, just
not blocking the first milestone.

**Milestone A — Core loop (this is "Version 1" in the minimal sense):**
1. Argon2id password module + storage format, unit-tested in isolation —
   the trust root, built and verified first, independent of everything else.
2. Job-Object suspend/resume/terminate core, tested against a throwaway
   dummy `.exe` before pointing it at real Firefox.
3. Minimal Windows Service scaffold (no SDDL hardening yet) that can detect
   a protected process launching and route it through step 2.
4. Named-pipe IPC + lock screen UI, wired to the real password module from
   step 1.
5. End-to-end validation against real Firefox: launch → suspend → prompt →
   correct password → resume; wrong password → deny; close → re-lock.

At the end of Milestone A, stop and validate with you before continuing —
this is the "genuine engineering decision" checkpoint, not a routine one.

**Milestone B — Universal engine validation:**
6. Config/setup flow (first-run Master Password creation, add/remove
   protected apps via path+hash, per §1.1) — Master-Password-gated per §3.
7. Re-run the Milestone A loop against Chrome, Edge, Google Drive for
   Desktop, ChatGPT Desktop, WhatsApp Desktop — confirming zero code changes
   were needed, only registration (validates §1.1's core claim).
8. Lockdown Mode (§8) implemented and tested, including the Master-Password
   recovery path specifically (this path must never itself be breakable by
   the failed-attempt counter).

**Milestone C — Hardening (deferred from Milestone A, not dropped):**
9. Service SCM security descriptor lockdown (§5.2).
10. Watchdog pairing + service recovery actions (§2.2).
11. ACLs on `%ProgramData%\Viper`, named-pipe DACL (§5.1, §5.3).
12. Auto-update hash re-registration test — force a real browser update,
    confirm no manual re-link needed (§5.4).

Known-bug tolerance: per your direction, Milestone A may ship with minor,
*documented* bugs. It may not ship with an unreviewed change to the
Lockdown Mode / Master Password recovery path (§8) or the crypto module
(§3) — those two are the ones where a subtle bug removes your own access
or silently weakens the one thing this software exists to guarantee, so
they get explicit review regardless of milestone pressure.
