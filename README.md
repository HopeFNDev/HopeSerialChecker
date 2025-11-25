# Hope's Serial Checker

A Windows Forms application that retrieves hardware serial numbers and identifiers using **modern Windows APIs** (SMBIOS, Registry, DeviceIoControl, GetAdaptersInfo) **without WMI dependency**.

Now featuring a stylish **Black and Blurple** theme!

## Features

The application uses a **tabbed interface** with separate tabs for each hardware category:

### BIOS/System Tab
- **BIOS Information** (via SMBIOS Type 0): Vendor, Version, ReleaseDate
- **BaseBoard Information** (via SMBIOS Type 2): Manufacturer, Product, Version, SerialNumber
- **System Information** (via SMBIOS Type 1): Manufacturer, ProductName, Version, SerialNumber, UUID
- **Chassis Information** (via SMBIOS Type 3): Manufacturer, Type, Version, SerialNumber, AssetTag

### CPU Tab
- **Registry-based**: ProcessorNameString, VendorIdentifier, Identifier, Clock Speed
- **CPUID Instruction**: CPUID value (not a true serial number)

### Disks Tab
- **Direct Hardware Access** via `DeviceIoControl`:
  - SerialNumber (via `IOCTL_STORAGE_QUERY_PROPERTY`)
  - Model (via `IOCTL_STORAGE_QUERY_PROPERTY`)
  - Size (via `IOCTL_DISK_GET_DRIVE_GEOMETRY`)
- Enumerates all physical drives (`\\.\PhysicalDrive0`, `\\.\PhysicalDrive1`, etc.)

### GPU Tab
- **Registry-based** (from `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}`):
  - Name (DriverDesc)
  - DriverVersion, DriverDate
  - HardwareInformation.AdapterString, BiosString
  - MemorySize

### Monitor Tab (New!)
- **Registry-based EDID Parsing**:
  - Retrieves Monitor Name and Serial Number directly from EDID data in the Registry (`HKLM\SYSTEM\CurrentControlSet\Enum\DISPLAY`)
  - **No WMI dependency**

### Network Tab
- **Kernel MAC addresses**: Retrieved via `GetAdaptersInfo` API (iphlpapi.dll)
- **Registry MAC addresses**: Retrieved from `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002BE10318}`
- Both sources displayed separately for comparison

## Technology Stack

**No WMI dependency** - uses modern, future-proof APIs:
- **SMBIOS** (via `GetSystemFirmwareTable`) for BIOS/System/BaseBoard/Chassis info
- **Windows Registry** for CPU, GPU, Monitor, and NIC configuration
- **DeviceIoControl** for direct disk hardware queries
- **GetAdaptersInfo** (iphlpapi.dll) for network adapter enumeration
- **P/Invoke** for low-level Windows API access

## Requirements

- **.NET 8.0 SDK** (or later) with Windows desktop workload
- **Windows 10 version 1809 (build 17763)** or later
- **Administrator privileges** may be required for:
  - SMBIOS firmware table access
  - Direct disk serial queries via DeviceIoControl
  - Some registry keys on restricted systems

## Building

If you have .NET SDK installed:

```powershell
cd (your path)
dotnet restore
dotnet build
```

## Running

```powershell
dotnet run
```

Or build a self-contained executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained
.\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\HardwareSerialChecker.exe
```

## Usage

1. **Tabs**: Switch between BIOS/System, CPU, Disks, GPU, Network, and Monitors tabs
2. **Refresh All**: Click to reload all hardware information across all tabs
3. **Copy Selected**: Select rows in the current tab and click to copy them to clipboard (tab-delimited)
4. **Export JSON**: Export all data from all tabs to a JSON file
5. **Export CSV**: Export all data from all tabs to a CSV file

## Notes

- Some fields may show "N/A" if the hardware does not provide the information or if access is restricted.
- **SMBIOS data** may be unavailable in some virtual machines or require administrator privileges.
- **Direct disk serial queries** may fail without administrator privileges.
- **CPU CPUID** is not a true serial number on modern processors.
- **Registry MAC addresses** may differ from kernel MAC addresses if custom MAC addresses are configured.

## Why No WMI?

WMI (Windows Management Instrumentation) is being deprecated in favor of modern Windows APIs. This application uses:
- **SMBIOS** for firmware/hardware tables (more direct and reliable)
- **Registry** for driver and configuration data
- **DeviceIoControl** for direct hardware communication
- **Native Windows APIs** (GetAdaptersInfo, GetSystemFirmwareTable)

These approaches are more performant, require fewer dependencies, and are future-proof.

## Troubleshooting

- **"Access Denied" errors**: Run the application as Administrator (right-click → Run as administrator)
- **Empty serial numbers**: Some OEMs do not populate BIOS/BaseBoard serial numbers, especially in VMs
- **SMBIOS unavailable**: Requires Windows 10+ and may need admin rights
- **Missing .NET SDK**: Install from https://dotnet.microsoft.com/download/dotnet/8.0

## License

This project is provided as-is for educational and diagnostic purposes.
