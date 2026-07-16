# App-Lock Solutions for Windows 11

## 1. Third-Party App-Locker Software

Several third-party tools claim to password-protect specific executables on Windows. These include commercial and free utilities. Below is a sampling of notable options, with current pricing and capabilities:

- **GiliSoft EXE Lock** – Commercial ($19.95 one‐year license for 1 PC).  Supports locking any `.exe`, including browsers.  Its “Password Verification” mode intercepts app launches and requires a password.  GiliSoft advertises full Windows 11 support (we’ve verified it explicitly mentions Windows 11 compatibility).  In practice it hooks into the executable (so renaming the file won’t bypass it) and can either block or prompt for a password on launch.  A known weakness is that if the locking service is stopped or uninstalled, the protection falls away; also an admin user may kill the GiliSoft background process or remove the lock.  GiliSoft notes it can password-protect apps even if they auto-update, but if an update changes the `.exe` path or name, the lock may need reconfiguration.  (No independent recent user reviews were found; we rely on the vendor page and the Q&A suggestion.)

- **Cisdem AppCrypt (Windows)** – Commercial (≈$19.99/year or $39.99 one-time for 1 PC).  Locks arbitrary apps (e.g. Chrome, Firefox) and websites.  The vendor specifies support for Windows 10 and later and explicitly lists Chrome/Firefox among supported browsers.  It runs as a background service, requiring a password to launch a locked app or to quit AppCrypt.  In trials users report it can lock browsers successfully.  Mechanism: presumably an injected driver or API hook to intercept process launches (details aren’t public).  Potential weaknesses: if an attacker kills the AppCrypt process (Task Manager) or uninstalls it, locks are bypassed.  It also uses hardware-binding for “license control” (so an administrator could uninstall or run on another machine without unlocking).  Because it locks the executable file itself, browser updates that replace the `.exe` might reset the lock until re-added.  (Sources: official site and Uptodown summary.)

- **AskAdmin (Sordum)** – Free (open-source).  The Sordum “AskAdmin” utility lets you block any app by name or path and optionally set a password to protect the block list.  It explicitly supports Windows 11 (see “Supported OS: Windows 11…”).  It can block Chrome, Edge, UWP apps, etc..  It appears to work by hooking or filtering executable launches: Sordum notes that it uses a “hash security method,” so renaming the `.exe` won’t bypass the block.  If a blocked app is attempted, AskAdmin simply prevents it from running.  Drawbacks: it does **not** prompt for a password on each launch; it just outright blocks.  An optional password protects the AskAdmin settings, but if the user is an administrator, they could edit or remove blocks.  In safe mode, AskAdmin’s filters likely don’t run, so a blocked program can launch.  Also, if one locks Explorer (as the page warns) it can crash Windows.  (Cited from Sordum’s official page.)  

- **Application Locker (freeware)** – Free.  A simple app-locker (often just called “AppLocker” or “Application Locker” from various sources) that lets you lock `.exe` files via a GUI.  For example, Download.it describes an “Application Locker” that locks any EXE by name.  It is extremely basic: you select an `.exe` name, and the tool adds it to a block list.  It does not securely protect this list – the Download.it review warns “anyone with moderate PC knowledge can get around the program. There is no security on Application Locker, so anyone who opens it can unlock locked programs”.  In short, it *can* lock browsers by EXE name, but it’s trivial to bypass (and it apparently won’t even run on Windows 7 or later according to one review).  

- **KakaSoft ExeLock** – Free.  Another EXE-password tool.  Unlike others, ExeLock **directly modifies the executable file** to insert a password prompt.  You run ExeLock and point it at a program; it patches the `.exe` so that upon launch, a password dialog appears (no background service needed).  The developer’s site emphasizes that **any** EXE (32/64-bit) can be protected.  It does not keep a separate running service; the lock is in the binary itself.  This means it *survives updates poorly* – if Chrome/Firefox auto-updates their `.exe`, the patch will be removed and needs redoing.  Also, modifying binaries triggers antivirus alerts.  Because the patch is local to the EXE, running a portable copy of the browser (with the original unmodified EXE) would bypass it.  KakaSoft claims it’s “safe and efficient” and not easily disabled (since it doesn’t rely on a service), but an administrator could simply replace the EXE with a fresh one, undoing the lock.  (Uptodown confirms ExeLock is free and proprietary.)

- **My Lockbox (FSPro)** – Freemium.  Technically a *folder*-locking tool, but it can be used to “lock” applications by hiding or blocking their installation folder.  In practice, you set My Lockbox on the `C:\Program Files\Mozilla Firefox\` folder, for example.  Then attempts to open any file there (including `firefox.exe`) produce an “access denied” error.  The free version locks only one folder.  Setup is simple (GUI wizard).  It effectively *blocks* access (no prompt) until you open the My Lockbox control panel and disable protection.  It *does not* prompt per-launch – instead the OS just denies access.  Weaknesses: it’s not running as a guard but using OS ACLs, so an admin user can take ownership or disable the protection.  Also, because it locks at the folder level, it can break shortcuts or updates (an updater will fail to write).  A recent tutorial shows My Lockbox causing an error dialog “You may not have appropriate permissions” when launching a locked app.  (Cite: My Lockbox official description and MinTool guide.)  

- **Folder Guard** – Commercial (≈$29.95 one-time).  Primarily a folder/file access controller, but it has a “Program Lock” feature that can require a password to start applications.  According to the vendor, it can “prevent other users (even administrators)” from using certain programs.  It works by intercepting file access requests and hiding/protecting folders or files.  For example, you could protect `C:\Program Files\Google\Chrome\chrome.exe`.  It supports Windows 10/11.  Mechanism: hooks into the filesystem to deny access unless the master password is entered.  It is relatively sophisticated (can also hide folders, disable Control Panel, etc.).  However, if the user has the master password (or is an admin with knowledge of it), they can unlock.  Bypasses: Safe Mode disables it by design, and an admin with physical access could use the “Emergency Recovery” tool to reset it.

There are other tools occasionally mentioned (e.g. “Steganos Privacy Suite” or “Anvi Folder Locker”), but these focus on folder encryption and do not natively prompt at application launch.  Some free/unmaintained tools exist (e.g. “Software Restriction Policy” GUIs or old AppLockers), but they tend to be insecure or Windows-version limited.

## 2. Windows 11 Built-in Options

Windows itself does **not** have a simple “password on app launch” feature.  Relevant native controls include:

- **AppLocker (Group Policy)**: A Windows Enterprise/Pro feature that lets an administrator **restrict** who can run which executables based on path/publisher/hash.  AppLocker can block apps for certain user groups, but it does *not* display any password prompt.  It simply prevents the program from running for unauthorized users.  (Even Microsoft documentation emphasizes AppLocker as a policy tool, not a password-gate.)  Moreover, AppLocker rules only apply on Windows Pro/Enterprise/Education editions, and even there they require the “Application Identity” service to be running.

- **Software Restriction Policy (SRP)**: An older GPO alternative to AppLocker.  It can similarly disallow specified programs (e.g. via “Don’t run specified Windows applications”), but again it *blocks* executables rather than prompting for a password.  (The Microsoft Q&A suggests using this policy to block store apps.)  Like AppLocker, it works by policy, not by prompt.

- **Local Group Policy “Don’t run specified applications”**:  Through `gpedit.msc` (User Configuration → Administrative Templates → System → “Don’t run specified Windows applications”), you can enter a list of exe names to block.  This simply prevents launching, with an error dialog (“restricted by policy”).  It requires a Pro or higher edition, and no password prompt.

- **Windows Family Safety / Parental Controls**: Offers “app and game limits” by account and time, but *no built-in way to require a password* per app launch.  Family Safety can lock an account when screen time is up, but cannot gate-launch an individual program.  (The Family Safety user experience is for time limiting on a child’s account; it does not use a password prompt on a generic share PC.)

- **User Accounts / Multiple Accounts**:  Not exactly an app-lock, but the standard practice: each person logs in to their own Windows user.  You can protect your account with a PIN or password, and share a guest/user account with limited rights.  This is the recommended secure solution: “all people using your PC should have their own user accounts. Your account should be protected by a PIN and always lock your PC when away” (Reddit advice).  However, it does not password‐gate individual apps beyond what Windows normally requires (UAC for admin tasks, etc.).

- **Controlled Folder Access / UAC / Credential Guard / Dynamic Lock**:  None of these features provide an on-demand password for launching specific programs.  Controlled Folder Access is an anti-malware folder monitor; UAC prompts for administrative elevation (not relevant if apps run as normal user); Dynamic Lock uses a Bluetooth device to lock the PC; Credential Guard is virtualization-based for credentials.  In short, Windows has no native “password to open Chrome” feature.

In summary: *Windows built-ins can block apps or enforce account-level restrictions, but there is no native feature that asks the user for a password or PIN specifically when launching a given application*.  (AppLocker/SRP only restrict by policy, and Family Safety only enforces time limits.)

## 3. Feasibility of a Custom Script/Watcher

Technically it is **possible but fragile** to build your own app-locker as a background script or service.  One approach is:

1. **Monitor process creation events**.  For example, using Windows Management Instrumentation (WMI) or APIs like `CreateProcessHook`, a program can detect when `firefox.exe` or `chrome.exe` starts.

2. **Suspend the new process immediately**.  Once detected, the tool could suspend (pause) the process threads (e.g. via `NtSuspendProcess` or similar) before it renders its window.

3. **Prompt for a password**.  Show a dialog (e.g. via a GUI toolkit) asking for the user’s password or PIN.

4. **If correct, resume the process**; else kill it.  You could call `NtResumeProcess` or `Process.Resume()` if using a library, or `TerminateProcess` on wrong password.

However, browsers make this tricky.  Modern browsers (Chrome, Firefox) are multi-process: launching them spawns multiple executables (browser, renderers, GPU, helper, etc.).  A naive script might suspend only one process, while others keep running.  Also, browsers often have auto-update updaters that may restart the main executable, which complicates detection.  You’d need to suspend the correct process (usually the main browser process) but allow updates.

A Python or PowerShell watcher could use something like the `wmi` or `watchdog` module, but this has challenges:  
- **Race conditions**: There is a narrow time to suspend before the app’s UI appears.  
- **User experience**: Suspending a process may briefly freeze the UI, which could confuse the user if the prompt is delayed.  
- **Service account**: The script must run with sufficient privilege (probably as system or admin) to suspend other processes; but then the user, if admin, could just kill the script.  
- **Bypass**: If the coworker has administrative rights, they could kill the watcher or modify it (just like any other solution).  If the user is a normal account, they might not easily kill it, but they could restart in Safe Mode where the watcher might not run, then launch the app unimpeded.

Another idea is a **wrapper/launcher**: rename `firefox.exe` to something else, and put a custom stub named `firefox.exe` that prompts for a password and only then launches the real one.  This effectively intercepts all launches.  The drawback is that many shortcuts and registry entries point directly to `firefox.exe`; renaming it might break these unless done carefully.  Also, browsers auto-update by overwriting their exe, which would overwrite or bypass the wrapper.

Windows provides no easy “hook CreateProcess globally” without writing a kernel-mode driver or DLL injection.  Some tools use Windows Filtering Platform or AppInit_DLLs, but these are complex and can be blocked by modern Windows protections.

In summary: **a DIY watcher is possible but delicate**.  It can work for simple cases (detect exe, suspend, prompt, resume), but it will struggle with multi-process apps, rapid spawns, and updates.  It would also be relatively easy for a determined user to circumvent (e.g. by running a portable copy).  We found no mature open-source project for this; most commercial app-lockers use similar logic behind the scenes.

## 4. Browser-Specific Native Options

Browsers themselves offer limited built-in protection, but **not full app locks**:

- **Firefox Primary (Master) Password**:  Firefox can ask for a “Primary Password” on startup, but this only protects *saved credentials*.  Once set, Firefox encrypts all saved usernames/passwords and requires the primary password the first time a saved login is used in each session.  It does *not* prevent the browser itself from opening or being used (except that it won’t autofill passwords without the master).  It also does *not* protect cookies, history, or logged-in sessions.  In short, it only stops someone from viewing your stored passwords, not from browsing once Firefox is open.  (Support docs: “you will be prompted to enter it once for each Firefox session, when Firefox needs access to your stored passwords”.)

- **Chrome (and Edge) Password Lock**:  Chrome offers a Windows Hello integration: you can require your Windows PIN or biometric to *view autofilled passwords*.  But like Firefox’s feature, this is only for the password manager.  Chrome does not lock the entire profile or session out of the box.  There *are* Chrome extensions (e.g. “Chrome Profile Lock”) that add a PIN prompt on startup, but these are third-party and can be removed.  Chrome also supports “Guest mode” (no profile data) and multiple user profiles, but switching profiles doesn’t enforce a password per profile (the primary profile is protected by the OS login, however).

- **Edge Browser**:  Similar to Chrome (also Blink-based) it can use Windows Hello for passwords, but no standalone app-lock.  (In Edge’s case, Microsoft accounts and Windows Hello tie into syncing but do not block the app itself.)

So, built-in browser locks only address saved logins.  They do *not* solve the problem of “prompt for PIN when Chrome/Firefox opens” in the way Android’s App Lock does.  However, enabling Primary Password in Firefox and requiring Windows Hello for Chrome’s passwords could cover the *majority of risk*: if an attacker just grabs an open browser, they still might be able to browse sites where you were already logged in, but they couldn’t easily extract new passwords or see saved accounts.  It is *not* a true app lock.

## 5. Security and Bypass Considerations

Regardless of method, a semi-technical coworker with *some* privileges can often bypass app locks:

- **Task Manager / Killing**: If the app-lock tool runs as a user process (not a kernel driver), the user might simply kill it.  For example, if AskAdmin or AppCrypt is running in background, ending that process could disable the lock and let you run the apps.  Some tools (like Smart PC Locker Pro, mentioned in passing) can even disable Task Manager, but that is for locking the *desktop*, not individual apps.

- **Safe Mode**: Many of these tools do not run in Safe Mode.  If someone boots into Safe Mode (pressing F8 at boot, for instance), most third-party services and startup programs are disabled, so you could launch any EXE directly from there.  For instance, My Lockbox explicitly allows using Safe Mode to unlock if you locked yourself out.  In practice, Safe Mode can bypass folder locks and some executable hooks, so it’s a known weak point.

- **Portable Apps / Copying Executable**: If the lock is applied to a specific file path, one can take a portable copy of the browser to a USB and run it (the lock tool has no effect on executables it wasn’t told to protect).  Even for patched tools like ExeLock, running a fresh, unmodified copy defeats it.  I.e. locking “chrome.exe” doesn’t prevent “chromePort.exe” or any .exe you rename or copy elsewhere (unless the tool has a hardware-binding or registry-trick, most do not).

- **Admin Rights**: If the coworker is already an Administrator on the PC, they can usually bypass any app-lock by uninstalling or disabling it.  Even some tools claim to protect *even admins* (Folder Guard, for example, says it can deny actions even to administrators), but ultimately an admin can boot from external media or use recovery tools to remove restrictions.  In general, *if someone has full admin access, no app-level protection is foolproof*.  Most software locks assume the target user is a **standard (non-admin) account**.  Sordum’s AskAdmin note about Explorer restarting with admin rights is telling – when Explorer is elevated, it can override restrictions.

- **Browser Auto-Updates**: For browser locks, updates are a nuisance.  If the lock is on the `chrome.exe` itself, an update will typically replace that file, removing the lock.  Any good solution needs monitoring.  None of the above tools explicitly state how they handle browser updates; most likely you must re-lock after an update.  

- **File Renaming or Running As Admin**:  Sordum claims renaming the EXE won’t bypass AskAdmin, and KakaSoft’s method resists mere shortcut hacks (it says it “protects programs, not icons”).  However, an admin user can always take ownership or use elevated privileges to undo permissions or unpack an EXE.  Even AppLocker policies can be changed by someone with admin rights unless you encrypt the policy (which is advanced).

In short, **no solution is ironclad** on a PC where the “attacker” has physical or admin access.  The effectiveness depends on locking down their account (making them a standard user) and trusting they won’t simply reboot and circumvent things.  The strongest approach in practice is defense-in-depth: use Windows accounts correctly, enable browser password-protection (so even if they open the browser, they can’t see saved logins), and consider a third-party app-lock as an extra hurdle.  

## 6. Summary Comparison

| Solution                            | Cost               | Setup Difficulty               | Locks Firefox? | Locks Chrome? | Survives Browser Updates | Bypass Difficulty (non-admin) | Shows Password Prompt on Launch? |
|-------------------------------------|--------------------|--------------------------------|---------------|--------------|--------------------------|-------------------------------|-------------------------------|
| **Windows AppLocker (GPO)**         | Free (Win Pro/Ent) | High (requires GPO editing)    | Yes (block)   | Yes (block)  | N/A (policy-based)       | Moderate (needs admin to change policy) | No (just denies)            |
| **Software Restriction Policy (GPO)** | Free (Win Pro/Ent) | High (GPO)                     | Yes (block)   | Yes (block)  | N/A                      | Moderate                      | No                          |
| **Windows Family Safety (App limits)** | Free             | Low (Family settings)          | Partial (time) | Partial      | N/A                      | Low (time limits bypassable)  | No (time-limit-based)       |
| **Firefox Primary Password**        | Free               | Low (in-browser setting)       | N/A (built-in) | N/A          | Yes (login data only)    | High (protects saved logins)  | Yes (for saved passwords only) |
| **Chrome/Edge Windows Hello**       | Free               | Low (browser setting)          | N/A           | N/A          | Yes (affects password manager) | High (protects saved passwords) | Yes (for saved passwords) |
| **KakaSoft ExeLock**               | Free               | Low (simple UI)                | Yes (patch exe) | Yes (patch exe) | No (updates overwrite exe) | Low (rename or use unpatched copy) | Yes (modifies exe)       |
| **AskAdmin (Sordum)**             | Free (donation)    | Low (drag-drop interface)      | Yes (blocks)   | Yes (blocks) | Yes (hash-based, path?)  | Low (safe mode or killing app) | No (blocks without prompt)|
| **Application Locker (free)**      | Free               | Low (simple UI)                | Yes (blocks)   | Yes (blocks) | Yes                      | Very Low (easy to bypass)      | No                          |
| **GiliSoft EXE Lock**             | Paid (~$20)        | Low (point-and-click)          | Yes (password) | Yes (password) | ? (likely needs manual update after exe change) | Moderate (admin can stop service) | Yes (password prompt) |
| **Cisdem AppCrypt**               | Paid (~$20/yr or $40) | Medium (install & config)   | Yes (password) | Yes (password) | ? (requires relocking after update) | Moderate (admin can disable service) | Yes (password prompt) |
| **Folder Guard**                  | Paid (~$30)        | Medium (install & config)      | Yes (password) | Yes (password) | Yes (protects at OS level) | Moderate (admin can recover password) | No (requires unlocking)|
| **My Lockbox**                    | Free (1 folder) / Paid ($) | Low (install & select folder) | Yes (folder lock) | Yes (folder lock) | No (depends on selected folder) | Low (admin can use recovery tool) | No (denies access)        |
| **Custom Script/Watcher**         | Free (DIY)         | High (programming needed)      | Yes (if coded) | Yes (if coded) | No (fragile)             | Low (easy to kill by user)    | Potentially (if coded)      |

**Notes:** “Yes (password)” in the table means the solution can be configured to *prompt* for a password on launch.  “Blocks” means it simply denies access without a prompt.  “Bypass Difficulty” is from the point of view of a standard (non-admin) user; “Very Low” means trivial to bypass, “High” means fairly secure for non-admins. 

**Sources:** Third-party tool details from vendor sites and user/community posts, and comparisons from GiliSoft’s analysis. This summary is based on our 2024–26 research; actual product behavior can change, so testing in your environment is recommended. 

