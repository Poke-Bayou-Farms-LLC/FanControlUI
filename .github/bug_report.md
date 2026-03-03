---
name: Hardware Detection / Bug Report
about: Create a report to help us troubleshoot undetected fans or application crashes.
title: "[BUG] - "
labels: bug, hardware-compatibility
assignees: designategold7
---

### ⚠️ READ BEFORE SUBMITTING
If your motherboard case fans are not showing up, but your GPU fans are, **this is likely an OEM manufacturer lock (Dell, HP, ASUS Pre-builts) and cannot be fixed by software.**

---

### Hardware Specifications
* **Motherboard Make & Model:** * **CPU:** * **GPU:** * **Are you using a USB Fan Hub? (Corsair/NZXT/etc.):** ### Describe the Bug
A clear and concise description of what the bug is (e.g., "Application crashes on launch", "Sliders are greyed out").

### Expected Behavior
A clear and concise description of what you expected to happen.

### Screenshots / UI Behavior
If applicable, add screenshots to help explain your problem.

### Hardware Sensor Diagnostic Dump (REQUIRED)
You must run a hardware diagnostic tool (like LibreHardwareMonitor or OpenHardwareMonitor) and paste the specific sensor tree for your Motherboard and GPU here. If your Super I/O chip does not expose a `Type: Control` node, we cannot control your fans.

```text
PASTE DIAGNOSTIC DUMP HERE
```