using System.Runtime.InteropServices;
using System.Text;
using System.Net;
using System.Management;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using HardwareSerialChecker.Models;

namespace HardwareSerialChecker.Services;

public class HardwareInfoService
{
    // Allows enabling WMI as a last-resort fallback for serials
    public bool EnableWmiFallbackForSerials { get; set; } = true;

    public List<HardwareItem> GetBiosInfo()
    {
        var items = new List<HardwareItem>();
        
        // Read SMBIOS data directly
        var smbiosData = GetSmbiosData();
        if (smbiosData != null)
        {
            ParseSmbiosBiosInfo(smbiosData, items);
            ParseSmbiosBaseBoardInfo(smbiosData, items);
            ParseSmbiosSystemInfo(smbiosData, items);
            ParseSmbiosChassisInfo(smbiosData, items);

            // Diagnostics rows to verify SMBIOS contents (temporary)
            AddSmbiosDiagnostics(smbiosData, items);
        }
        else
        {
            items.Add(new HardwareItem
            {
                Category = "BIOS",
                Name = "Error",
                Value = "Failed to retrieve SMBIOS data",
                Notes = "May require administrator privileges"
            });
        }

        // Fill any missing values using registry fallbacks
        AddRegistryBiosFallback(items);

        // Optional WMI last-resort for serials
        if (EnableWmiFallbackForSerials)
        {
            AddWmiBiosFallback(items);
        }
        
        return items;
    }

    public List<HardwareItem> GetProcessorInfo()
    {
        var items = new List<HardwareItem>();
        
        try
        {
            // Read from Registry
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (key != null)
            {
                items.Add(new HardwareItem
                {
                    Category = "CPU",
                    Name = "ProcessorNameString",
                    Value = key.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "N/A",
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "CPU",
                    Name = "VendorIdentifier",
                    Value = key.GetValue("VendorIdentifier")?.ToString() ?? "N/A",
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "CPU",
                    Name = "Identifier",
                    Value = key.GetValue("Identifier")?.ToString() ?? "N/A",
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "CPU",
                    Name = "~MHz",
                    Value = key.GetValue("~MHz")?.ToString() ?? "N/A",
                    Notes = "Clock speed in MHz"
                });
            }
            
            // Get additional CPU info from registry
            using var key2 = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            if (key2 != null)
            {
                var featureSet = key2.GetValue("FeatureSet");
                if (featureSet != null)
                {
                    items.Add(new HardwareItem
                    {
                        Category = "CPU",
                        Name = "FeatureSet",
                        Value = featureSet.ToString() ?? "N/A",
                        Notes = "CPU feature flags"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            items.Add(new HardwareItem
            {
                Category = "CPU",
                Name = "Error",
                Value = ex.Message,
                Notes = "Failed to retrieve"
            });
        }
        
        return items;
    }

    public List<HardwareItem> GetDiskInfo()
    {
        var items = new List<HardwareItem>();
        
        try
        {
            // Enumerate physical drives
            for (int i = 0; i < 32; i++)
            {
                var devicePath = $"\\\\.\\PhysicalDrive{i}";
                var diskInfo = GetDiskInfoDirect(devicePath, i);
                if (diskInfo.Count > 0)
                {
                    items.AddRange(diskInfo);
                }
            }
            
            if (items.Count == 0)
            {
                items.Add(new HardwareItem
                {
                    Category = "Disk",
                    Name = "Info",
                    Value = "No physical drives found",
                    Notes = "May require administrator privileges"
                });
            }
        }
        catch (Exception ex)
        {
            items.Add(new HardwareItem
            {
                Category = "Disk",
                Name = "Error",
                Value = ex.Message,
                Notes = "Failed to retrieve"
            });
        }
        
        return items;
    }

    public List<HardwareItem> GetVideoControllerInfo()
    {
        var items = new List<HardwareItem>();
        
        try
        {
            // Read from Registry - enumerate display adapters
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (key != null)
            {
                int index = 0;
                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    if (!subKeyName.StartsWith("0"))
                        continue;
                        
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null)
                        continue;
                    
                    var driverDesc = subKey.GetValue("DriverDesc")?.ToString();
                    if (string.IsNullOrEmpty(driverDesc))
                        continue;
                    
                    items.Add(new HardwareItem
                    {
                        Category = "GPU",
                        Name = $"Name_{index}",
                        Value = driverDesc,
                        Notes = ""
                    });
                    
                    items.Add(new HardwareItem
                    {
                        Category = "GPU",
                        Name = $"DriverVersion_{index}",
                        Value = subKey.GetValue("DriverVersion")?.ToString() ?? "N/A",
                        Notes = ""
                    });
                    
                    items.Add(new HardwareItem
                    {
                        Category = "GPU",
                        Name = $"DriverDate_{index}",
                        Value = subKey.GetValue("DriverDate")?.ToString() ?? "N/A",
                        Notes = ""
                    });
                    
                    items.Add(new HardwareItem
                    {
                        Category = "GPU",
                        Name = $"HardwareInformation.AdapterString_{index}",
                        Value = subKey.GetValue("HardwareInformation.AdapterString")?.ToString() ?? "N/A",
                        Notes = ""
                    });
                    
                    items.Add(new HardwareItem
                    {
                        Category = "GPU",
                        Name = $"HardwareInformation.BiosString_{index}",
                        Value = subKey.GetValue("HardwareInformation.BiosString")?.ToString() ?? "N/A",
                        Notes = ""
                    });
                    
                    var memorySize = subKey.GetValue("HardwareInformation.MemorySize");
                    if (memorySize != null)
                    {
                        items.Add(new HardwareItem
                        {
                            Category = "GPU",
                            Name = $"MemorySize_{index}",
                            Value = memorySize.ToString() ?? "N/A",
                            Notes = "Bytes"
                        });
                    }
                    
                    index++;
                }
            }
            
            if (items.Count == 0)
            {
                items.Add(new HardwareItem
                {
                    Category = "GPU",
                    Name = "Info",
                    Value = "No display adapters found",
                    Notes = ""
                });
            }
        }
        catch (Exception ex)
        {
            items.Add(new HardwareItem
            {
                Category = "GPU",
                Name = "Error",
                Value = ex.Message,
                Notes = "Failed to retrieve"
            });
        }
        
        return items;
    }

    public List<HardwareItem> GetNetworkAdapterInfo()
    {
        var items = new List<HardwareItem>();
        
        // Registry MACs
        try
        {
            const string keyPath = @"SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002BE10318}";
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key != null)
            {
                int regIndex = 0;
                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    if (!subKeyName.StartsWith("0"))
                        continue;

                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null)
                        continue;

                    var mac = subKey.GetValue("NetworkAddress")?.ToString();
                    if (string.IsNullOrEmpty(mac))
                        continue;

                    var adapterName = subKey.GetValue("DriverDesc")?.ToString() ?? "Unknown";
                    
                    // Format MAC address with colons
                    if (mac.Length == 12)
                    {
                        mac = string.Join(":", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2)));
                    }

                    items.Add(new HardwareItem
                    {
                        Category = "NIC",
                        Name = $"RegistryMAC_{regIndex}",
                        Value = mac,
                        Notes = $"Adapter: {adapterName}"
                    });
                    regIndex++;
                }
            }
        }
        catch (Exception ex)
        {
            items.Add(new HardwareItem
            {
                Category = "NIC",
                Name = "RegistryMAC_Error",
                Value = ex.Message,
                Notes = "Failed to retrieve"
            });
        }
        
        // Kernel MACs via GetAdaptersInfo
        try
        {
            var kernelMacs = GetAdaptersInfoNative();
            int kernelIndex = 0;
            foreach (var (name, mac) in kernelMacs)
            {
                items.Add(new HardwareItem
                {
                    Category = "NIC",
                    Name = $"KernelMAC_{kernelIndex}",
                    Value = mac,
                    Notes = $"Adapter: {name}"
                });
                kernelIndex++;
            }
        }
        catch (Exception ex)
        {
            items.Add(new HardwareItem
            {
                Category = "NIC",
                Name = "KernelMAC_Error",
                Value = ex.Message,
                Notes = "Failed to retrieve"
            });
        }
        
        return items;
    }

    #region SMBIOS Parsing

    private byte[]? GetSmbiosData()
    {
        try
        {
            uint totalSize = GetSystemFirmwareTable(0x52534D42, 0, IntPtr.Zero, 0); // 'RSMB'
            if (totalSize < 8)
                return null;

            var buffer = Marshal.AllocHGlobal((int)totalSize);
            try
            {
                var written = GetSystemFirmwareTable(0x52534D42, 0, buffer, totalSize);
                if (written < 8)
                    return null;

                // RawSMBIOSData header: 4 bytes (Used20CallingMethod, Major, Minor, DmiRevision) + 4-byte Length
                // Table data immediately follows header
                byte used20 = Marshal.ReadByte(buffer, 0);
                byte major = Marshal.ReadByte(buffer, 1);
                byte minor = Marshal.ReadByte(buffer, 2);
                byte dmiRev = Marshal.ReadByte(buffer, 3);
                int tableLength = (int)Marshal.ReadInt32(buffer, 4);

                // Guard against invalid length
                if (tableLength <= 0)
                    return null;

                int available = (int)written - 8;
                int copyLen = Math.Min(tableLength, available);
                if (copyLen <= 0)
                    return null;

                var table = new byte[copyLen];
                Marshal.Copy(IntPtr.Add(buffer, 8), table, 0, copyLen);
                return table;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return null;
        }
    }

    private void ParseSmbiosBiosInfo(byte[] data, List<HardwareItem> items)
    {
        try
        {
            var structures = ParseSmbiosStructures(data);
            var biosStructures = structures.Where(s => s.Type == 0).ToList();
            
            if (biosStructures.Count == 0)
            {
                items.Add(new HardwareItem
                {
                    Category = "BIOS",
                    Name = "Info",
                    Value = "No BIOS structures found in SMBIOS data",
                    Notes = ""
                });
                return;
            }
            
            foreach (var structure in biosStructures)
            {
                items.Add(new HardwareItem
                {
                    Category = "BIOS",
                    Name = "Vendor",
                    Value = GetSmbiosStringAt(structure, 0),
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "BIOS",
                    Name = "Version",
                    Value = GetSmbiosStringAt(structure, 1),
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "BIOS",
                    Name = "ReleaseDate",
                    Value = GetSmbiosStringAt(structure, 2),
                    Notes = ""
                });
            }
        }
        catch (Exception ex)
        {
            items.Add(new HardwareItem
            {
                Category = "BIOS",
                Name = "Parse Error",
                Value = ex.Message,
                Notes = ex.GetType().Name
            });
        }
    }

    private void ParseSmbiosBaseBoardInfo(byte[] data, List<HardwareItem> items)
    {
        try
        {
            var structures = ParseSmbiosStructures(data);
            var baseBoardStructures = structures.Where(s => s.Type == 2).ToList();
            
            foreach (var structure in baseBoardStructures)
            {
                items.Add(new HardwareItem
                {
                    Category = "BaseBoard",
                    Name = "Manufacturer",
                    Value = GetSmbiosStringAt(structure, 0),
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "BaseBoard",
                    Name = "Product",
                    Value = GetSmbiosStringAt(structure, 1),
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "BaseBoard",
                    Name = "Version",
                    Value = GetSmbiosStringAt(structure, 2),
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "BaseBoard",
                    Name = "SerialNumber",
                    Value = GetSmbiosStringAt(structure, 3),
                    Notes = ""
                });
            }
        }
        catch (Exception ex)
        {
            items.Add(new HardwareItem
            {
                Category = "BaseBoard",
                Name = "Parse Error",
                Value = ex.Message,
                Notes = ex.GetType().Name
            });
        }
    }

    private void ParseSmbiosSystemInfo(byte[] data, List<HardwareItem> items)
    {
        try
        {
            var structures = ParseSmbiosStructures(data);
            var systemStructures = structures.Where(s => s.Type == 1).ToList();
            
            foreach (var structure in systemStructures)
            {
                items.Add(new HardwareItem
                {
                    Category = "SystemProduct",
                    Name = "Manufacturer",
                    Value = GetSmbiosStringAt(structure, 0),
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "SystemProduct",
                    Name = "ProductName",
                    Value = GetSmbiosStringAt(structure, 1),
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "SystemProduct",
                    Name = "Version",
                    Value = GetSmbiosStringAt(structure, 2),
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "SystemProduct",
                    Name = "SerialNumber",
                    Value = GetSmbiosStringAt(structure, 3),
                    Notes = ""
                });
                
                // UUID is at offset 4..19 (16 bytes) in formatted area
                if (structure.Data.Length >= 20)
                {
                    var uuid = new byte[16];
                    Array.Copy(structure.Data, 4, uuid, 0, 16);
                    var uuidStr = BitConverter.ToString(uuid).Replace("-", "");
                    items.Add(new HardwareItem
                    {
                        Category = "SystemProduct",
                        Name = "UUID",
                        Value = FormatUuid(uuid),
                        Notes = ""
                    });
                }
            }
        }
        catch { }
    }

    private void ParseSmbiosChassisInfo(byte[] data, List<HardwareItem> items)
    {
        try
        {
            var structures = ParseSmbiosStructures(data);
            var chassisStructures = structures.Where(s => s.Type == 3).ToList();
            
            foreach (var structure in chassisStructures)
            {
                items.Add(new HardwareItem
                {
                    Category = "Chassis",
                    Name = "Manufacturer",
                    Value = GetSmbiosStringAt(structure, 0),
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "Chassis",
                    Name = "Type",
                    Value = structure.Data.Length > 1 ? structure.Data[1].ToString() : "N/A",
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "Chassis",
                    Name = "Version",
                    Value = GetSmbiosStringAt(structure, 2),
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "Chassis",
                    Name = "SerialNumber",
                    Value = GetSmbiosStringAt(structure, 3),
                    Notes = ""
                });
                
                items.Add(new HardwareItem
                {
                    Category = "Chassis",
                    Name = "AssetTag",
                    Value = GetSmbiosStringAt(structure, 4),
                    Notes = ""
                });
            }
        }
        catch (Exception ex)
        {
            items.Add(new HardwareItem
            {
                Category = "Chassis",
                Name = "Parse Error",
                Value = ex.Message,
                Notes = ex.GetType().Name
            });
        }
    }

    private List<SmbiosStructure> ParseSmbiosStructures(byte[] data)
    {
        var structures = new List<SmbiosStructure>();
        
        try
        {
            // Parse starting at 0, data already excludes RawSMBIOSData header
            int offset = 0;
            int end = data.Length;
            
            while (offset + 4 < end)
            {
                byte type = data[offset];
                byte length = data[offset + 1];
                
                if (type == 127) // End of table
                    break;
                
                // Validate length
                if (length < 4 || offset + length > end)
                    break;
                
                int dataLength = length - 4;
                if (dataLength < 0)
                    break;
                
                var structData = new byte[dataLength];
                if (dataLength > 0)
                    Array.Copy(data, offset + 4, structData, 0, dataLength);
                
                // Find strings section
                var strings = new List<string>();
                int stringOffset = offset + length;
                
                while (stringOffset < end - 1)
                {
                    if (data[stringOffset] == 0 && data[stringOffset + 1] == 0)
                    {
                        stringOffset += 2;
                        break;
                    }
                    
                    var sb = new StringBuilder();
                    
                    while (stringOffset < end && data[stringOffset] != 0)
                    {
                        sb.Append((char)data[stringOffset]);
                        stringOffset++;
                    }
                    
                    if (sb.Length > 0)
                        strings.Add(sb.ToString());
                    
                    stringOffset++; // Skip null terminator
                    
                    if (stringOffset >= end)
                        break;
                }
                
                structures.Add(new SmbiosStructure
                {
                    Type = type,
                    Length = length,
                    Data = structData,
                    Strings = strings
                });
                
                offset = stringOffset;
                
                // Safety check to prevent infinite loops
                if (offset >= end)
                    break;
            }
        }
        catch
        {
            // Return whatever structures we parsed successfully
        }
        
        return structures;
    }

    private string? GetSmbiosString(SmbiosStructure structure, byte index)
    {
        if (index == 0 || index > structure.Strings.Count)
            return null;
        return structure.Strings[index - 1];
    }

    private string GetSmbiosStringAt(SmbiosStructure structure, int dataIndex)
    {
        if (structure.Data.Length <= dataIndex)
            return "N/A";
        return GetSmbiosString(structure, structure.Data[dataIndex]) ?? "N/A";
    }

    private void AddRegistryBiosFallback(List<HardwareItem> items)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\\DESCRIPTION\\System\\BIOS");
            if (key == null)
                return;

            bool IsPlaceholder(string? v)
            {
                if (string.IsNullOrWhiteSpace(v)) return true;
                var s = v.Trim();
                if (string.Equals(s, "N/A", StringComparison.OrdinalIgnoreCase)) return true;
                if (s.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase)) return true;
                if (s.Equals("Default string", StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }

            void UpsertIfEmpty(string category, string name, string? value, string notes = "")
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                var existing = items.FirstOrDefault(i => i.Category == category && i.Name == name);
                if (existing == null)
                {
                    items.Add(new HardwareItem
                    {
                        Category = category,
                        Name = name,
                        Value = value ?? "N/A",
                        Notes = notes
                    });
                }
                else if (IsPlaceholder(existing.Value))
                {
                    existing.Value = value ?? existing.Value;
                    if (!string.IsNullOrEmpty(notes)) existing.Notes = notes;
                }
            }

            // BIOS
            UpsertIfEmpty("BIOS", "Vendor", key.GetValue("BIOSVendor")?.ToString());
            UpsertIfEmpty("BIOS", "Version", key.GetValue("BIOSVersion")?.ToString());
            UpsertIfEmpty("BIOS", "ReleaseDate", key.GetValue("BIOSReleaseDate")?.ToString());

            // BaseBoard
            UpsertIfEmpty("BaseBoard", "Manufacturer", key.GetValue("BaseBoardManufacturer")?.ToString());
            UpsertIfEmpty("BaseBoard", "Product", key.GetValue("BaseBoardProduct")?.ToString());
            UpsertIfEmpty("BaseBoard", "Version", key.GetValue("BaseBoardVersion")?.ToString());
            UpsertIfEmpty("BaseBoard", "SerialNumber", key.GetValue("BaseBoardSerialNumber")?.ToString());

            // SystemProduct
            UpsertIfEmpty("SystemProduct", "Manufacturer", key.GetValue("SystemManufacturer")?.ToString());
            UpsertIfEmpty("SystemProduct", "ProductName", key.GetValue("SystemProductName")?.ToString());
            UpsertIfEmpty("SystemProduct", "Version", key.GetValue("SystemVersion")?.ToString());
            UpsertIfEmpty("SystemProduct", "SerialNumber", key.GetValue("SystemSerialNumber")?.ToString());
            UpsertIfEmpty("SystemProduct", "SKU", key.GetValue("SystemSKU")?.ToString(), "SKU Number");
            UpsertIfEmpty("SystemProduct", "Family", key.GetValue("SystemFamily")?.ToString());
        }
        catch { }
    }

    private void AddWmiBiosFallback(List<HardwareItem> items)
    {
        try
        {
            bool IsPlaceholder(string? v)
            {
                if (string.IsNullOrWhiteSpace(v)) return true;
                var s = v.Trim();
                if (string.Equals(s, "N/A", StringComparison.OrdinalIgnoreCase)) return true;
                if (s.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase)) return true;
                if (s.Equals("Default string", StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }

            void Upsert(string category, string name, string? value, string notes = "")
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                var existing = items.FirstOrDefault(i => i.Category == category && i.Name == name);
                if (existing == null)
                {
                    items.Add(new HardwareItem { Category = category, Name = name, Value = value!, Notes = notes });
                }
                else if (IsPlaceholder(existing.Value))
                {
                    existing.Value = value!;
                    if (!string.IsNullOrEmpty(notes)) existing.Notes = notes;
                }
            }

            using (var bb = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
            {
                foreach (ManagementObject mo in bb.Get())
                    Upsert("BaseBoard", "SerialNumber", mo["SerialNumber"]?.ToString());
            }

            using (var sys = new ManagementObjectSearcher("SELECT IdentifyingNumber FROM Win32_ComputerSystemProduct"))
            {
                foreach (ManagementObject mo in sys.Get())
                    Upsert("SystemProduct", "SerialNumber", mo["IdentifyingNumber"]?.ToString());
            }

            using (var bios = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
            {
                foreach (ManagementObject mo in bios.Get())
                    Upsert("BIOS", "SerialNumber", mo["SerialNumber"]?.ToString());
            }
        }
        catch { }
    }

    private string FormatUuid(byte[] uuid)
    {
        return $"{uuid[0]:X2}{uuid[1]:X2}{uuid[2]:X2}{uuid[3]:X2}-" +
               $"{uuid[4]:X2}{uuid[5]:X2}-{uuid[6]:X2}{uuid[7]:X2}-" +
               $"{uuid[8]:X2}{uuid[9]:X2}-{uuid[10]:X2}{uuid[11]:X2}{uuid[12]:X2}{uuid[13]:X2}{uuid[14]:X2}{uuid[15]:X2}";
    }

    // Diagnostics: surface which SMBIOS types were found and the serial string indices/values
    private void AddSmbiosDiagnostics(byte[] table, List<HardwareItem> items)
    {
        try
        {
            var structures = ParseSmbiosStructures(table);
            int t0 = structures.Count(s => s.Type == 0);
            int t1 = structures.Count(s => s.Type == 1);
            int t2 = structures.Count(s => s.Type == 2);
            int t3 = structures.Count(s => s.Type == 3);

            items.Add(new HardwareItem { Category = "Diagnostics", Name = "SMBIOS Types", Value = $"T0={t0}, T1={t1}, T2={t2}, T3={t3}", Notes = "" });

            foreach (var sys in structures.Where(s => s.Type == 1))
            {
                byte idxMan = sys.Data.Length > 0 ? sys.Data[0] : (byte)0;
                byte idxProd = sys.Data.Length > 1 ? sys.Data[1] : (byte)0;
                byte idxVer = sys.Data.Length > 2 ? sys.Data[2] : (byte)0;
                byte idxSer = sys.Data.Length > 3 ? sys.Data[3] : (byte)0;
                var ser = GetSmbiosString(sys, idxSer) ?? "<null>";
                items.Add(new HardwareItem { Category = "Diagnostics", Name = "Type1.Serial idx", Value = idxSer.ToString(), Notes = ser });
            }

            foreach (var brd in structures.Where(s => s.Type == 2))
            {
                byte idxSer = brd.Data.Length > 3 ? brd.Data[3] : (byte)0;
                var ser = GetSmbiosString(brd, idxSer) ?? "<null>";
                items.Add(new HardwareItem { Category = "Diagnostics", Name = "Type2.Serial idx", Value = idxSer.ToString(), Notes = ser });
            }
        }
        catch { }
    }

    private class SmbiosStructure
    {
        public byte Type { get; set; }
        public byte Length { get; set; }
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public List<string> Strings { get; set; } = new List<string>();
    }

    #endregion

    #region Disk Direct Access

    private List<HardwareItem> GetDiskInfoDirect(string devicePath, int index)
    {
        var items = new List<HardwareItem>();
        
        try
        {
            var handle = CreateFile(
                devicePath,
                0,
                FileShare.Read | FileShare.Write,
                IntPtr.Zero,
                FileMode.Open,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid)
                return items;

            try
            {
                // Disk size rows removed per user request
                
                // Get serial number
                var serial = GetDiskSerial(handle);
                if (!string.IsNullOrEmpty(serial))
                {
                    items.Add(new HardwareItem
                    {
                        Category = "Disk",
                        Name = $"SerialNumber_{index}",
                        Value = serial,
                        Notes = $"Physical Drive {index}"
                    });
                }
                
                // Get model
                var model = GetDiskModel(handle);
                if (!string.IsNullOrEmpty(model))
                {
                    items.Add(new HardwareItem
                    {
                        Category = "Disk",
                        Name = $"Model_{index}",
                        Value = model,
                        Notes = $"Physical Drive {index}"
                    });
                }
            }
            finally
            {
                handle.Close();
            }
        }
        catch
        {
            // Silently skip inaccessible drives
        }
        
        return items;
    }

    private DISK_GEOMETRY? GetDiskGeometry(SafeFileHandle handle)
    {
        try
        {
            var size = Marshal.SizeOf<DISK_GEOMETRY>();
            var buffer = Marshal.AllocHGlobal(size);
            
            try
            {
                var success = DeviceIoControl(
                    handle,
                    IOCTL_DISK_GET_DRIVE_GEOMETRY,
                    IntPtr.Zero,
                    0,
                    buffer,
                    size,
                    out var bytesReturned,
                    IntPtr.Zero);
                
                if (success)
                {
                    return Marshal.PtrToStructure<DISK_GEOMETRY>(buffer);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch { }
        
        return null;
    }

    private string? GetDiskSerial(SafeFileHandle handle)
    {
        try
        {
            var query = new STORAGE_PROPERTY_QUERY
            {
                PropertyId = STORAGE_PROPERTY_ID.StorageDeviceProperty,
                QueryType = STORAGE_QUERY_TYPE.PropertyStandardQuery
            };

            var querySize = Marshal.SizeOf(query);
            var queryPtr = Marshal.AllocHGlobal(querySize);
            Marshal.StructureToPtr(query, queryPtr, false);

            var bufferSize = 4096;
            var buffer = Marshal.AllocHGlobal(bufferSize);

            var success = DeviceIoControl(
                handle,
                IOCTL_STORAGE_QUERY_PROPERTY,
                queryPtr,
                querySize,
                buffer,
                bufferSize,
                out var bytesReturned,
                IntPtr.Zero);

            Marshal.FreeHGlobal(queryPtr);

            if (!success || bytesReturned == 0)
            {
                Marshal.FreeHGlobal(buffer);
                return null;
            }

            var descriptor = Marshal.PtrToStructure<STORAGE_DEVICE_DESCRIPTOR>(buffer);
            if (descriptor.SerialNumberOffset > 0 && descriptor.SerialNumberOffset < bytesReturned)
            {
                var serialPtr = IntPtr.Add(buffer, (int)descriptor.SerialNumberOffset);
                var serial = Marshal.PtrToStringAnsi(serialPtr)?.Trim();
                Marshal.FreeHGlobal(buffer);
                return serial;
            }

            Marshal.FreeHGlobal(buffer);
        }
        catch { }
        
        return null;
    }

    private string? GetDiskModel(SafeFileHandle handle)
    {
        try
        {
            var query = new STORAGE_PROPERTY_QUERY
            {
                PropertyId = STORAGE_PROPERTY_ID.StorageDeviceProperty,
                QueryType = STORAGE_QUERY_TYPE.PropertyStandardQuery
            };

            var querySize = Marshal.SizeOf(query);
            var queryPtr = Marshal.AllocHGlobal(querySize);
            Marshal.StructureToPtr(query, queryPtr, false);

            var bufferSize = 4096;
            var buffer = Marshal.AllocHGlobal(bufferSize);

            var success = DeviceIoControl(
                handle,
                IOCTL_STORAGE_QUERY_PROPERTY,
                queryPtr,
                querySize,
                buffer,
                bufferSize,
                out var bytesReturned,
                IntPtr.Zero);

            Marshal.FreeHGlobal(queryPtr);

            if (!success || bytesReturned == 0)
            {
                Marshal.FreeHGlobal(buffer);
                return null;
            }

            var descriptor = Marshal.PtrToStructure<STORAGE_DEVICE_DESCRIPTOR>(buffer);
            if (descriptor.ProductIdOffset > 0 && descriptor.ProductIdOffset < bytesReturned)
            {
                var modelPtr = IntPtr.Add(buffer, (int)descriptor.ProductIdOffset);
                var model = Marshal.PtrToStringAnsi(modelPtr)?.Trim();
                Marshal.FreeHGlobal(buffer);
                return model;
            }

            Marshal.FreeHGlobal(buffer);
        }
        catch { }
        
        return null;
    }

    #endregion


    #region Network Adapters

    private List<(string Name, string Mac)> GetAdaptersInfoNative()
    {
        var adapters = new List<(string, string)>();
        
        try
        {
            uint size = 0;
            GetAdaptersInfo(IntPtr.Zero, ref size);
            
            if (size == 0)
                return adapters;
            
            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                var result = GetAdaptersInfo(buffer, ref size);
                if (result != 0)
                    return adapters;
                
                var current = buffer;
                while (current != IntPtr.Zero)
                {
                    var adapter = Marshal.PtrToStructure<IP_ADAPTER_INFO>(current);
                    
                    var mac = BitConverter.ToString(adapter.Address, 0, (int)adapter.AddressLength).Replace("-", ":");
                    var name = adapter.Description;
                    
                    adapters.Add((name, mac));
                    
                    current = adapter.Next;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch { }
        
        return adapters;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetAdaptersInfo(IntPtr pAdapterInfo, ref uint pOutBufLen);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct IP_ADAPTER_INFO
    {
        public IntPtr Next;
        public uint ComboIndex;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string AdapterName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 132)]
        public string Description;
        public uint AddressLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Address;
        public uint Index;
        public uint Type;
        public uint DhcpEnabled;
        public IntPtr CurrentIpAddress;
    }

    #endregion

    #region USB Devices
    public List<HardwareItem> GetUsbDevices()
    {
        var items = new List<HardwareItem>();
        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IntPtr h = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_ALLCLASSES);
            if (h == IntPtr.Zero || h == new IntPtr(-1))
            {
                items.Add(new HardwareItem { Category = "USB", Name = "Error", Value = "SetupDiGetClassDevs failed", Notes = "" });
                return items;
            }
            try
            {
                uint index = 0;
                while (true)
                {
                    var info = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
                    if (!SetupDiEnumDeviceInfo(h, index, ref info)) break;
                    index++;

                    string? enumerator = GetDevRegPropString(h, ref info, SPDRP_ENUMERATOR_NAME);
                    if (string.IsNullOrEmpty(enumerator)) continue;
                    if (!string.Equals(enumerator, "USB", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(enumerator, "USBSTOR", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string? name = GetDevRegPropString(h, ref info, SPDRP_FRIENDLYNAME);
                    if (string.IsNullOrWhiteSpace(name))
                        name = GetDevRegPropString(h, ref info, SPDRP_DEVICEDESC) ?? "USB Device";

                    string instanceId = GetInstanceId(h, ref info) ?? string.Empty;
                    if (string.IsNullOrEmpty(instanceId)) continue;
                    if (!seen.Add(instanceId)) continue;

                    bool isUsbStor = string.Equals(enumerator, "USBSTOR", StringComparison.OrdinalIgnoreCase);
                    string serial = ExtractSerialFromInstance(instanceId, isUsbStor);

                    items.Add(new HardwareItem { Category = "USB", Name = name, Value = string.IsNullOrEmpty(serial) ? "" : serial, Notes = "" });
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(h);
            }

            if (items.Count == 0)
                items.Add(new HardwareItem { Category = "USB", Name = "Info", Value = "No connected USB devices found", Notes = "" });
        }
        catch (Exception ex)
        {
            items.Add(new HardwareItem { Category = "USB", Name = "Error", Value = ex.Message, Notes = "Failed to retrieve" });
        }
        return items;
    }
    #endregion

    #region ARP Table
    public List<HardwareItem> GetArpTable()
    {
        var items = new List<HardwareItem>();
        try
        {
            int size = 0;
            GetIpNetTable(IntPtr.Zero, ref size, true);
            if (size == 0)
            {
                items.Add(new HardwareItem { Category = "ARP", Name = "Info", Value = "No entries", Notes = "" });
                return items;
            }

            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                int result = GetIpNetTable(buffer, ref size, true);
                if (result != 0)
                {
                    items.Add(new HardwareItem { Category = "ARP", Name = "Error", Value = $"GetIpNetTable failed: {result}", Notes = "" });
                    return items;
                }

                int numEntries = Marshal.ReadInt32(buffer); // dwNumEntries
                IntPtr rowPtr = IntPtr.Add(buffer, 4);
                int rowSize = Marshal.SizeOf<MIB_IPNETROW>();

                // Build adapter index -> name map
                var indexToName = new Dictionary<uint, string>();
                try
                {
                    uint len = 0;
                    GetAdaptersInfo(IntPtr.Zero, ref len);
                    if (len > 0)
                    {
                        var buf = Marshal.AllocHGlobal((int)len);
                        try
                        {
                            if (GetAdaptersInfo(buf, ref len) == 0)
                            {
                                var cur = buf;
                                while (cur != IntPtr.Zero)
                                {
                                    var adp = Marshal.PtrToStructure<IP_ADAPTER_INFO>(cur);
                                    indexToName[adp.Index] = adp.Description ?? $"IfIndex {adp.Index}";
                                    cur = adp.Next;
                                }
                            }
                        }
                        finally { Marshal.FreeHGlobal(buf); }
                    }
                }
                catch { }

                for (int i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<MIB_IPNETROW>(rowPtr);

                    if (row.dwPhysAddrLen == 0)
                    {
                        rowPtr = IntPtr.Add(rowPtr, rowSize);
                        continue;
                    }

                    // Skip invalid entries (2 = Invalid)
                    if (row.dwType == 2)
                    {
                        rowPtr = IntPtr.Add(rowPtr, rowSize);
                        continue;
                    }

                    // Convert IPv4 from DWORD (little-endian) to bytes in network order
                    byte[] ipBytes = new byte[]
                    {
                        (byte)(row.dwAddr & 0xFF),
                        (byte)((row.dwAddr >> 8) & 0xFF),
                        (byte)((row.dwAddr >> 16) & 0xFF),
                        (byte)((row.dwAddr >> 24) & 0xFF)
                    };
                    string ip = new IPAddress(ipBytes).ToString();
                    int macLen = (int)Math.Min((uint)row.bPhysAddr.Length, row.dwPhysAddrLen);
                    string mac = BitConverter.ToString(row.bPhysAddr, 0, macLen).Replace("-", ":");
                    string typeStr = row.dwType switch
                    {
                        4 => "Static",
                        3 => "Dynamic",
                        2 => "Invalid",
                        1 => "Other",
                        _ => $"Type {row.dwType}"
                    };

                    string adapterName = indexToName.TryGetValue(row.dwIndex, out var name) ? name : $"IfIndex {row.dwIndex}";

                    items.Add(new HardwareItem
                    {
                        Category = "ARP",
                        Name = ip,
                        Value = mac,
                        Notes = $"{typeStr}; Adapter: {adapterName}"
                    });

                    rowPtr = IntPtr.Add(rowPtr, rowSize);
                }

                if (items.Count == 0)
                {
                    items.Add(new HardwareItem
                    {
                        Category = "ARP",
                        Name = "Info",
                        Value = "No ARP entries found",
                        Notes = ""
                    });
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            items.Add(new HardwareItem { Category = "ARP", Name = "Error", Value = ex.Message, Notes = "Failed to retrieve" });
        }
        return items;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetIpNetTable(IntPtr pIpNetTable, ref int pdwSize, bool bOrder);

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_IPNETROW
    {
        public uint dwIndex;
        public uint dwPhysAddrLen;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] bPhysAddr;
        public uint dwAddr;
        public uint dwType;
    }
    #endregion

    #region P/Invoke Declarations
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetSystemFirmwareTable(uint firmwareTableProviderSignature, uint firmwareTableID, IntPtr pFirmwareTableBuffer, uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        FileMode dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        IntPtr lpOutBuffer,
        int nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
    private const uint IOCTL_DISK_GET_DRIVE_GEOMETRY = 0x00070000;

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_PROPERTY_QUERY
    {
        public STORAGE_PROPERTY_ID PropertyId;
        public STORAGE_QUERY_TYPE QueryType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public byte[] AdditionalParameters;
    }

    private enum STORAGE_PROPERTY_ID
    {
        StorageDeviceProperty = 0
    }

    private enum STORAGE_QUERY_TYPE
    {
        PropertyStandardQuery = 0
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_DEVICE_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        public byte DeviceType;
        public byte DeviceTypeModifier;
        public byte RemovableMedia;
        public byte CommandQueueing;
        public uint VendorIdOffset;
        public uint ProductIdOffset;
        public uint ProductRevisionOffset;
        public uint SerialNumberOffset;
        public uint BusType;
        public uint RawPropertiesLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISK_GEOMETRY
    {
        public long Cylinders;
        public uint MediaType;
        public uint TracksPerCylinder;
        public uint SectorsPerTrack;
        public uint BytesPerSector;
    }

    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint DIGCF_ALLCLASSES = 0x00000004;
    private const uint SPDRP_DEVICEDESC = 0x00000000;
    private const uint SPDRP_FRIENDLYNAME = 0x0000000C;
    private const uint SPDRP_ENUMERATOR_NAME = 0x00000016;

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr SetupDiGetClassDevs(IntPtr ClassGuid, string? Enumerator, IntPtr hwndParent, uint Flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property, out uint PropertyRegDataType, byte[]? PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiGetDeviceInstanceId(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, StringBuilder DeviceInstanceId, int DeviceInstanceIdSize, out int RequiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    private static string? GetDevRegPropString(IntPtr h, ref SP_DEVINFO_DATA info, uint prop)
    {
        uint type;
        uint required;
        SetupDiGetDeviceRegistryProperty(h, ref info, prop, out type, null, 0, out required);
        if (required == 0) return null;
        var buf = new byte[required];
        if (!SetupDiGetDeviceRegistryProperty(h, ref info, prop, out type, buf, (uint)buf.Length, out required)) return null;
        var s = Encoding.Unicode.GetString(buf, 0, (int)required);
        int z = s.IndexOf('\0');
        if (z >= 0) s = s.Substring(0, z);
        s = s.Trim();
        if (s.Length == 0) return null;
        return s;
    }

    private static string? GetInstanceId(IntPtr h, ref SP_DEVINFO_DATA info)
    {
        int needed;
        // First call to get required size
        var sb = new StringBuilder(0);
        if (!SetupDiGetDeviceInstanceId(h, ref info, sb, 0, out needed))
        {
            if (needed <= 1) return null;
            sb = new StringBuilder(needed);
            if (!SetupDiGetDeviceInstanceId(h, ref info, sb, sb.Capacity, out needed)) return null;
        }
        return sb.ToString();
    }

    private static string ExtractSerialFromInstance(string instanceId, bool isUsbStor)
    {
        if (string.IsNullOrEmpty(instanceId)) return string.Empty;
        int idx = instanceId.LastIndexOf('\\');
        string tail = idx >= 0 ? instanceId.Substring(idx + 1) : instanceId;

        if (isUsbStor)
        {
            string serial = tail;
            int amp = serial.IndexOf('&');
            if (amp > 0) serial = serial.Substring(0, amp);
            string trimmed = serial.Trim();
            if (trimmed.Length == 0) return string.Empty;
            bool allZeros = true;
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (trimmed[i] != '0') { allZeros = false; break; }
            }
            if (allZeros) return string.Empty;
            return trimmed;
        }
        else
        {
            if (tail.IndexOf('&') >= 0) return string.Empty;
            return tail.Trim();
        }
    }

    #endregion
}
