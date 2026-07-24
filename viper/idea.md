# Viper Application Lock — Product Idea, Architecture & Specification

## 1. Overview & Core Mission
**Viper** is a local, offline Windows 11 application locker designed to intercept and freeze unauthorized application launches (e.g. Chrome, Firefox, ChatGPT, Google Drive, WhatsApp).

It operates under a strict threat model: protecting a shared or accessible PC against unauthorized users (colleagues, children, guest users) without requiring online server authentication or kernel-mode drivers.

---

## 2. System Architecture & Lifecycle

```
                     ┌──────────────────────────────────────┐
                     │          Target Executable           │
                     │  (e.g., chrome.exe / firefox.exe)    │
                     └──────────────────┬───────────────────┘
                                        │ (Kernel Launch Event)
                                        ▼
 ┌──────────────────────────────────────────────────────────────────────────┐
 │                  Microsoft-Windows-Kernel-Process (ETW)                  │
 └──────────────────────────────────────┬───────────────────────────────────┘
                                        │ (Process Start Interception)
                                        ▼
 ┌──────────────────────────────────────────────────────────────────────────┐
 │                    Viper.Service (Session 0 Service)                     │
 │  1. Placed in Job Object (JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE)            │
 │  2. Suspends all process threads via NtSuspendProcess                    │
 │  3. Spawns interactive Viper.UI via CreateProcessAsUser                  │
 └──────────────────┬────────────────────────────────────┬──────────────────┘
                    │                                    │
       (IPC / Authenticated Pipe)             (Supervised via SCM)
                    │                                    │
                    ▼                                    ▼
 ┌────────────────────────────────────┐ ┌─────────────────────────────────┐
 │       Viper.UI (User Session)      │ │   Viper.Watchdog (SCM Watchdog) │
 │  - Lock Screen (Password Prompt)   │ │  - Ensures ViperService runs    │
 │  - First-Run Setup (Master Pass)   │ │  - Auto-restarts if terminated  │
 │  - Settings (App Management)       │ └─────────────────────────────────┘
 └────────────────────────────────────┘
```

---

## 3. Key Components & Responsibilities

| Component | Responsibility | Access Context |
| :--- | :--- | :--- |
| **`Viper.Service`** | Intercepts launches via ETW, freezes process trees in Job Objects, authenticates IPC requests. | `LocalSystem` (Session 0) |
| **`Viper.Watchdog`** | Self-healing SCM supervisor that monitors and restarts `ViperService` if stopped or killed. | `LocalSystem` (Session 0) |
| **`Viper.UI`** | WPF interface displaying Lock Screen, First-Run Setup, or Owner Settings. Logic-free view component. | Interactive User Session |
| **`Viper.IPC`** | Authenticated, ACL-protected Named Pipe (`\\.\pipe\ViperIPC`) validating caller PID & session. | Inter-Process |
| **`Viper.Security`** | Zero-trust cryptographic root (Argon2id hashing, 16-byte random salts, constant-time comparisons). | Library |
| **`Viper.Config`** | Protected application list & state management (`%ProgramData%\Viper\viper.json`). | Machine-Wide ACL |

---

## 4. User Experience (UX) & Smart Routing

### A. Direct Execution (`Viper.UI.exe` from File Explorer)
When a user double-clicks `Viper.UI.exe` directly:
1. **Unconfigured System (First Run):** Automatically routes to **`SetupWindow`** to configure Master Password & App Unlock Password.
2. **Configured System:** Automatically routes to **`SettingsWindow`** (prompting for Master Password authentication to manage protected apps).

### B. Intercepted Application Launch
When a protected app (e.g. `chrome.exe`) is launched:
1. `Viper.Service` catches the process launch via ETW before the application initializes.
2. `Viper.Service` suspends the process tree in a Job Object.
3. `Viper.UI.exe` is spawned in the active user session displaying **`MainWindow` (Lock Screen)** with the exact name of the app (e.g., *"Chrome is locked"*).
4. Entering the correct password sends an authenticated IPC message to `ViperService`, which resumes the Job Object.

---

## 5. Security & Cryptographic Rules
1. **Argon2id Hashing:** `MemoryKib=19456, Iterations=2, Parallelism=1` per password.
2. **Salt Generation:** Unique 16-byte cryptographically secure random salt (`RandomNumberGenerator`).
3. **Memory Hygiene:** Zero password byte arrays immediately after use (`CryptographicOperations.ZeroMemory`).
4. **Timing Attack Protection:** Constant-time comparison (`CryptographicOperations.FixedTimeEquals`).
5. **Lockdown Mode:** 5 consecutive failed App Unlock attempts lock out the app. Only Master Password can unlock it.
