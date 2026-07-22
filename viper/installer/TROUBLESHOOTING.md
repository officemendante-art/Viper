# Viper Installer Troubleshooting & Diagnostic Guide

If the installer encounters an issue or stalls during setup (e.g. at "Starting services"), follow these steps to capture diagnostic logs and pinpoint the root cause.

---

## 1. Run Verbose Logging Installation

Always run the installer with full verbose logging enabled:

```cmd
msiexec /i Viper.Installer.msi /l*v install.log
```

This creates a detailed, timestamped log file named `install.log` in the current directory.

### Finding Errors in `install.log`:
1. Open `install.log` in Notepad or VS Code.
2. Search for the text: **`Return value 3`** (Windows Installer's standard indicator for a failed action).
3. Look at the **15–20 lines directly above** `Return value 3` to find the exact Win32 error code, custom action exit code, or exception text.

---

## 2. Windows Event Logs

If `ViperService` or `ViperWatchdog` fails to start during installation:

1. Press `Win + R`, type **`eventvwr.msc`**, and press Enter.
2. Navigate to **Windows Logs → System**:
   - Filter by Error level and look for **Service Control Manager**.
   - Look for Event ID 7000, 7009, or 7043 (Service start timeout or failure).
3. Navigate to **Windows Logs → Application**:
   - Look for errors from **`.NET Runtime`** or **`ViperService`**.
   - The log will contain the full C# stack trace if an unhandled exception occurred during service initialization.

---

## 3. Common Error Codes & Fixes

| Symptom / Log Error | Cause | Resolution |
|---|---|---|
| **Error 1925** ("You do not have sufficient privileges") | MSI opened without elevation | Run `msiexec /i Viper.Installer.msi` from an **Elevated (Admin) Command Prompt**, or double-click to accept the UAC prompt. |
| **Error 1920 / Event 1053** ("Service failed to start") | Missing runtime assembly (e.g., `runtimes\win\lib\*`) or missing DLL | Ensure the MSI was built with recursive dependency harvesting (`dotnet build installer\Viper.Installer.wixproj`). |
| **Access Denied on Config Folder** | ACL custom action sequence issue | Verify `SetFolderAcl1` ran after folder creation (`InstallFiles`). |

---

## 4. Manual Diagnostics & Service Query

To manually check service status in PowerShell:

```powershell
# Check service status
sc.exe query ViperService
sc.exe query ViperWatchdog

# Inspect system log for SCM errors
Get-EventLog -LogName System -Source "Service Control Manager" -EntryType Error -Newest 5 | Format-List
```
