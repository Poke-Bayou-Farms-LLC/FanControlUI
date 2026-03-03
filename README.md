# Universal Fan Hub 

A dynamic, multi-channel hardware telemetry and fan control engine for Windows. This application opportunistically maps all available thermal probes (CPU, GPU, Motherboard) and dynamically generates independent control interfaces for every accessible fan header in your system.

**Publisher:** Poke Bayou Farms, LLC  
**Repository:** [https://github.com/Poke-Bayou-Farms-LLC/FanControlUI](https://github.com/Poke-Bayou-Farms-LLC/FanControlUI)

---

##  Critical Architecture & Security Notes

### 1. Administrator Privileges Required (Ring-0 Access)
This application **will not run** without Administrator privileges. It relies on the `LibreHardwareMonitorLib.sys` kernel driver to communicate directly with your motherboard's Super I/O sensor chips via the SMBus. When you launch the executable, Windows will trigger a User Account Control (UAC) prompt. If declined, the application will safely terminate to prevent memory access violations.

### 2. OEM Hardware Lockouts (Pre-built PCs)
This engine is designed to be universally compatible with standard unlocked motherboards (e.g., MSI, standard ASUS ROG, Gigabyte, ASRock). 
**If you are using a pre-built OEM system (e.g., Dell, HP, Alienware, or OEM-specific boards), your motherboard case fans and CPU fans may not appear in the UI.** OEM manufacturers frequently lock their fan telemetry behind proprietary ACPI (Advanced Configuration and Power Interface) WMI methods. If the app only shows your GPU fans, your motherboard's Super I/O is physically locked out of standard open-source polling.

### 3. Windows SmartScreen & Code Signing
The standalone `.exe` provided in the Releases tab is cryptographically signed by Poke Bayou Farms, LLC. However, because it is currently a self-signed local certificate rather than an Extended Validation (EV) certificate, Windows SmartScreen may still flag the file as an "Unknown Publisher" upon first launch. You must click "More Info" -> "Run Anyway".

---

## Core Features

* **Universal Telemetry Harvesting:** Scrapes and displays temperatures across the entire hardware tree, including individual CPU cores, GPU Hot Spots, VRMs, and Chipset probes.
* **Dynamic Multi-Channel Routing:** Automatically generates a dedicated UI controller for every fan node discovered (Motherboard headers, GPU fans, external USB Hubs).
* **Hardware-Safe State Machine:** Features a strict `AUTO` and `MANUAL` toggle per fan. Prevents thread race conditions by completely decoupling the background polling loop from the UI override sliders.
* **Thermal Decoupling:** GPU fans automatically bind their thermal curves to the GPU temperature, while case and motherboard fans bind to the CPU temperature, preventing cross-hardware thermal throttling.
* **Zero-Install Deployment:** Compiled as a massive, fully self-contained .NET 8 binary. No SDK or framework installations are required for the end-user.

---

## Installation & Execution

### For Standard Users (Recommended)
1. Navigate to the **[Releases](../../releases)** tab on the right side of this GitHub repository.
2. Download the latest `FanControlUI.exe`.
3. Double-click the `.exe` and accept the UAC Administrator prompt.

### For Developers (Building from Source)
If you wish to compile the code yourself to audit the C# logic or modify the linear fan curve thresholds:

```powershell
# Clone the repository
git clone [https://github.com/designategold7/FanControlUI.git](https://github.com/designategold7/FanControlUI.git)

# Navigate into the project directory
cd FanControlUI

# Build and run the project (will trigger UAC)
dotnet run

# Publish a self-contained single-file executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
