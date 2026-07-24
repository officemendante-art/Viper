# Viper v2.0 Architecture Specification

## 1. Product Goal
Viper is a minimalist, local-only Windows application locker. It allows a user to require a password before launching specific applications (e.g., Chrome, Discord) on a shared Windows PC.

## 2. Architecture
Viper v2 moves away from the over-engineered Service/IPC model of v1. It is a single standalone WPF application.

- **Process Detection:** Polling `Process.GetProcessesByName` every 300ms.
- **Intervention:** When a new instance of a protected app is detected, Viper suspends the process using `NtSuspendProcess` (via P/Invoke).
- **Authentication:** Viper shows a topmost, system-modal-like WPF lock screen (`UnlockDialog`). 
- **Resolution:** 
  - If the password is correct, the process is resumed (`NtResumeProcess`).
  - If the user cancels or closes the dialog, the process is terminated (`Process.Kill()`).

## 3. Configuration & Security
- **Storage:** A single `config.json` file stored in `%ProgramData%\Viper\`. This ensures protection applies machine-wide to all Windows accounts.
- **Passwords:** Single password. No master password. No lockdown modes.
- **Hashing:** Argon2id (via `Konscious.Security.Cryptography`). 16-byte random salt per installation.
- **Validation:** Uses `CryptographicOperations.FixedTimeEquals` to prevent timing attacks. Sensitive memory is zeroed out using `CryptographicOperations.ZeroMemory` immediately after use.

## 4. Installer
- **Type:** Inno Setup.
- **Responsibilities:**
  1. Install the standalone `Viper.exe` and its dependencies to `C:\Program Files\Viper`.
  2. Create the `%ProgramData%\Viper` directory and grant `Users` group write permissions (so the app doesn't need to run as Admin to update settings).
  3. Optionally register the app to launch at startup in the registry (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).

## 5. UI Layer
- **Framework:** WPF (.NET 10).
- **Design:** Modern dark mode, strict JetBrains/Linear aesthetics.
- **Behavior:** Viper runs in the background. The main settings window hides itself instead of closing, and the app resides in the system tray (`NotifyIcon`) until explicitly exited.
