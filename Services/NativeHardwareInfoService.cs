using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using HardwareSerialChecker.Models;

namespace HardwareSerialChecker.Services
{
    public class NativeHardwareInfoService
    {
        private const int InitialBufferSize = 64 * 1024;

        [DllImport("HopesSerialCheckerCore.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetBiosInfoJson(StringBuilder buffer, int bufferLen);

        [DllImport("HopesSerialCheckerCore.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetProcessorInfoJson(StringBuilder buffer, int bufferLen);

        [DllImport("HopesSerialCheckerCore.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetDiskInfoJson(StringBuilder buffer, int bufferLen);

        [DllImport("HopesSerialCheckerCore.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetVideoControllerInfoJson(StringBuilder buffer, int bufferLen);

        [DllImport("HopesSerialCheckerCore.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetNetworkAdapterInfoJson(StringBuilder buffer, int bufferLen);

        [DllImport("HopesSerialCheckerCore.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetUsbDevicesJson(StringBuilder buffer, int bufferLen);

        [DllImport("HopesSerialCheckerCore.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetArpTableJson(StringBuilder buffer, int bufferLen);

        [DllImport("HopesSerialCheckerCore.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetMonitorInfoJson(StringBuilder buffer, int bufferLen);

        private static List<HardwareItem> Call(Func<StringBuilder, int> invoke)
        {
            var sb = new StringBuilder(InitialBufferSize);
            int needed = invoke(sb);

            if (needed > sb.Capacity)
            {
                sb.Capacity = needed;
                sb.Length = 0;
                needed = invoke(sb);
            }

            var json = sb.ToString();
            if (string.IsNullOrWhiteSpace(json))
                return new List<HardwareItem>();

            try
            {
                var result = JsonSerializer.Deserialize<List<HardwareItem>>(json);
                return result ?? new List<HardwareItem>();
            }
            catch
            {
                return new List<HardwareItem>();
            }
        }

        public List<HardwareItem> GetBiosInfo() =>
            Call(sb => GetBiosInfoJson(sb, sb.Capacity));

        public List<HardwareItem> GetProcessorInfo() =>
            Call(sb => GetProcessorInfoJson(sb, sb.Capacity));

        public List<HardwareItem> GetDiskInfo() =>
            Call(sb => GetDiskInfoJson(sb, sb.Capacity));

        public List<HardwareItem> GetVideoControllerInfo() =>
            Call(sb => GetVideoControllerInfoJson(sb, sb.Capacity));

        public List<HardwareItem> GetNetworkAdapterInfo() =>
            Call(sb => GetNetworkAdapterInfoJson(sb, sb.Capacity));

        public List<HardwareItem> GetUsbDevices() =>
            Call(sb => GetUsbDevicesJson(sb, sb.Capacity));

        public List<HardwareItem> GetArpTable() =>
            Call(sb => GetArpTableJson(sb, sb.Capacity));

        public List<HardwareItem> GetMonitorInfo() =>
            Call(sb => GetMonitorInfoJson(sb, sb.Capacity));
    }
}
