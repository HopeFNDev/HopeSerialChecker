#define NOMINMAX                // prevent windows.h from defining min/max macros
#include "hardwareinfo.h"       
#include <winioctl.h>           
#include <Ntddscsi.h>           
#include <iostream>
#include <iomanip>
#include <algorithm>
#include <map>
#include <conio.h>
#include <string>
#include <vector>

#pragma comment(lib, "user32.lib")


#define NVME_STORPORT_DRIVER         0xE000
#define NVME_PASS_THROUGH_SRB_IO_CODE \
    CTL_CODE(NVME_STORPORT_DRIVER, 0x800, METHOD_BUFFERED, FILE_ANY_ACCESS)

#define NVME_SIG_STR                 "NvmeMini"
#define NVME_SIG_STR_LEN             8
#define NVME_FROM_DEV_TO_HOST        2
#define NVME_IOCTL_VENDOR_SPECIFIC_DW_SIZE  6
#define NVME_IOCTL_CMD_DW_SIZE              16
#define NVME_IOCTL_COMPLETE_DW_SIZE         4
#define NVME_PT_TIMEOUT              40
#define NVME_ADMIN_IDENTIFY          0x06

#pragma pack(push, 1)
struct NVME_PASS_THROUGH_IOCTL {
    SRB_IO_CONTROL  SrbIoCtrl;
    DWORD           VendorSpecific[NVME_IOCTL_VENDOR_SPECIFIC_DW_SIZE];
    DWORD           NVMeCmd[NVME_IOCTL_CMD_DW_SIZE];
    DWORD           CplEntry[NVME_IOCTL_COMPLETE_DW_SIZE];
    DWORD           Direction;
    DWORD           QueueId;
    DWORD           DataBufferLen;
    DWORD           MetaDataLen;
    DWORD           ReturnBufferLen;
    UCHAR           DataBuffer[4096];
};
#pragma pack(pop)


struct NVME_IDENTIFY_CONTROLLER {
    USHORT  VID;
    USHORT  SSVID;
    CHAR    SN[20];
    CHAR    MN[40];
};


static bool IntelRaidGetPhysicalSerial(HANDLE hScsi, char* serialOut, size_t serialLen)
{
    DWORD bufSize = sizeof(NVME_PASS_THROUGH_IOCTL);
    auto* cmd = (NVME_PASS_THROUGH_IOCTL*)calloc(1, bufSize);
    if (!cmd) return false;

    cmd->SrbIoCtrl.HeaderLength = sizeof(SRB_IO_CONTROL);
    memcpy(cmd->SrbIoCtrl.Signature, NVME_SIG_STR, NVME_SIG_STR_LEN);
    cmd->SrbIoCtrl.Timeout     = NVME_PT_TIMEOUT;
    cmd->SrbIoCtrl.ControlCode = (ULONG)NVME_PASS_THROUGH_SRB_IO_CODE;
    cmd->SrbIoCtrl.Length      = bufSize - sizeof(SRB_IO_CONTROL);

   
    cmd->NVMeCmd[0]  = NVME_ADMIN_IDENTIFY;
    cmd->NVMeCmd[10] = 0x01;

    cmd->Direction       = NVME_FROM_DEV_TO_HOST;
    cmd->QueueId         = 0;   
    cmd->DataBufferLen   = 4096;
    cmd->ReturnBufferLen = bufSize;

    DWORD bytesReturned = 0;
    BOOL ok = DeviceIoControl(
        hScsi,
        IOCTL_SCSI_MINIPORT,
        cmd, bufSize,
        cmd, bufSize,
        &bytesReturned, nullptr
    );

    if (!ok || cmd->SrbIoCtrl.ReturnCode != 0) {
        free(cmd);
        return false;
    }

    auto* id = (NVME_IDENTIFY_CONTROLLER*)(cmd->DataBuffer);
    size_t copyLen = std::min((size_t)20, serialLen - 1);
    memcpy(serialOut, id->SN, copyLen);
    serialOut[copyLen] = '\0';

    
    for (int i = (int)strlen(serialOut) - 1; i >= 0 && serialOut[i] == ' '; --i)
        serialOut[i] = '\0';

    free(cmd);
    return strlen(serialOut) > 0;
}


struct IntelRaidDiskInfo {
    int         scsiPort;           
    std::string raidSerial;         
    std::string physicalSerial;     
};


static std::vector<IntelRaidDiskInfo> DetectIntelRaid(
    const std::vector<hardwareitem>& diskitems)
{
    std::vector<IntelRaidDiskInfo> results;

    for (int port = 0; port < 32; port++) {
        char path[32];
        snprintf(path, sizeof(path), "\\\\.\\Scsi%d:", port);

        HANDLE h = CreateFileA(
            path,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            nullptr, OPEN_EXISTING, 0, nullptr
        );
        if (h == INVALID_HANDLE_VALUE) continue;

        char physSerial[32] = {};
        if (IntelRaidGetPhysicalSerial(h, physSerial, sizeof(physSerial))) {
            IntelRaidDiskInfo info;
            info.scsiPort       = port;
            info.physicalSerial = physSerial;
            info.raidSerial = "(see disk information below)";
            results.push_back(info);
        }

        CloseHandle(h);
    }

    return results;
}

static std::vector<hardwareitem> BuildDiskInfoWithRaid(
    const std::vector<hardwareitem>& original,
    const std::vector<IntelRaidDiskInfo>& raidDisks)
{
    if (raidDisks.empty()) return original;

    std::vector<hardwareitem> result = original;

    
    {
        hardwareitem sep;
        sep.category = L"Intel RAID";
        sep.name     = L"--- Intel RST/VROC ---";
        sep.value    = L"Physical serials via miniport passthrough";
        sep.notes    = L"";
        result.push_back(sep);
    }

    for (auto& rd : raidDisks) {
        hardwareitem raidRow;
        raidRow.category = L"Intel RAID";
        raidRow.name     = L"Scsi" + std::to_wstring(rd.scsiPort) + L": RAID serial";
        raidRow.value    = L"(virtual - see disk above)";
        raidRow.notes    = L"changed by RAID";
        result.push_back(raidRow);

  
        hardwareitem physRow;
        physRow.category = L"Intel RAID";
        physRow.name     = L"Scsi" + std::to_wstring(rd.scsiPort) + L": physical serial";
        std::wstring ws(rd.physicalSerial.begin(), rd.physicalSerial.end());
        physRow.value    = ws;
        physRow.notes    = L"real / original";
        result.push_back(physRow);
    }

    return result;
}


void setupconsole() {
    HANDLE hout = GetStdHandle(STD_OUTPUT_HANDLE);
    HANDLE hin  = GetStdHandle(STD_INPUT_HANDLE);
    if (hout != INVALID_HANDLE_VALUE) {
        DWORD dwmode = 0;
        if (GetConsoleMode(hout, &dwmode)) {
            dwmode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
            dwmode &= ~DISABLE_NEWLINE_AUTO_RETURN;
            SetConsoleMode(hout, dwmode);
        }
    }
    if (hin != INVALID_HANDLE_VALUE) {
        DWORD dwmode = 0;
        if (GetConsoleMode(hin, &dwmode)) {
            dwmode &= ~(ENABLE_QUICK_EDIT_MODE | ENABLE_INSERT_MODE);
            dwmode |= ENABLE_EXTENDED_FLAGS;
            SetConsoleMode(hin, dwmode);
        }
    }
    SetConsoleOutputCP(CP_UTF8);
    SetConsoleCP(CP_UTF8);
    SetConsoleTitleW(L"");
    HWND consolewindow = GetConsoleWindow();
    if (consolewindow) {
        LONG style = GetWindowLong(consolewindow, GWL_STYLE);
        style &= ~(WS_MAXIMIZEBOX | WS_SIZEBOX);
        SetWindowLong(consolewindow, GWL_STYLE, style);
        SMALL_RECT windowsize = {0, 0, 89, 29};
        SetConsoleWindowInfo(hout, TRUE, &windowsize);
        COORD buffersize = {90, 300};
        SetConsoleScreenBufferSize(hout, buffersize);
    }
}

void clearscreen() {
    std::cout << "\033[2J\033[H";
}

std::string widetoutf8(const std::wstring& wide) {
    if (wide.empty()) return "";
    int size = WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), (int)wide.size(),
                                   nullptr, 0, nullptr, nullptr);
    std::string result(size, 0);
    WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), (int)wide.size(),
                        &result[0], size, nullptr, nullptr);
    return result;
}

void printheader() {
    std::cout << " machinist serial checker" << std::endl;
}

void printmainmenu() {
    clearscreen();
    printheader();
    std::cout << std::endl;
    std::cout << " select a category to view:" << std::endl;
    std::cout << std::endl;
    std::cout << " _________________________________________ " << std::endl;
    std::cout << " | [1] motherboard information          |" << std::endl;
    std::cout << " | [2] cpu information                  |" << std::endl;
    std::cout << " | [3] disk information                 |" << std::endl;
    std::cout << " | [4] gpu information                  |" << std::endl;
    std::cout << " | [5] network adapters                 |" << std::endl;
    std::cout << " | [6] monitor information              |" << std::endl;
    std::cout << " | [7] usb devices                      |" << std::endl;
    std::cout << " | [8] arp table                        |" << std::endl;
    std::cout << " |_____________________________________|" << std::endl;
    std::cout << " | [0] exit                             |" << std::endl;
    std::cout << " |_____________________________________|" << std::endl;
    std::cout << std::endl;
    std::cout << " press a number key to select..." << std::endl;
}

void printcategoryheader(const std::string& category) {
    std::cout << std::endl;
    std::string lowered = category;
    std::transform(lowered.begin(), lowered.end(), lowered.begin(), ::tolower);
    std::cout << "================================================================================" << std::endl;
    std::cout << " " << std::left << std::setw(78) << lowered << std::endl;
    std::cout << "================================================================================" << std::endl;
}

void printcategoryfooter() {
    std::cout << "================================================================================" << std::endl;
}

void printitem(const hardwareitem& item) {
    std::string name  = widetoutf8(item.name);
    std::string value = widetoutf8(item.value);
    std::string notes = widetoutf8(item.notes);

    if (name.length()  > 25) name  = name.substr(0, 22)  + "...";
    if (value.length() > 35) value = value.substr(0, 32) + "...";
    if (notes.length() > 15) notes = notes.substr(0, 12) + "...";

    std::cout << "| ";
    std::cout << std::left << std::setw(25) << name;
    std::cout << " | ";
    std::cout << std::left << std::setw(35) << value;
    if (!notes.empty()) {
        std::cout << " | " << notes;
    }
    std::cout << std::endl;
}

void printsection(const std::string& sectionname, const std::vector<hardwareitem>& items) {
    if (items.empty()) return;
    printcategoryheader(sectionname);

    std::map<std::wstring, std::vector<hardwareitem>> grouped;
    for (const auto& item : items) {
        grouped[item.category].push_back(item);
    }

    bool first = true;
    for (const auto& [category, categoryitems] : grouped) {
        if (!first) {
            std::cout << "--------------------------------------------------------------------------------" << std::endl;
        }
        first = false;
        if (grouped.size() > 1) {
            std::cout << "| [ " << widetoutf8(category) << " ]" << std::endl;
        }
        for (const auto& item : categoryitems) {
            printitem(item);
        }
    }
    printcategoryfooter();
}

void showcategorypage(const std::string& title, const std::vector<hardwareitem>& items) {
    clearscreen();
    printheader();
    printsection(title, items);
    std::cout << std::endl;
    std::cout << " press esc to go back to main menu..." << std::endl;
    while (true) {
        int key = _getch();
        if (key == 27) break;
    }
}

// big penis start
int main() {
    setupconsole();
    clearscreen();
    printheader();

    hardwareinfo hwinfo;
    const int delay_per_fetch = 625;

    std::vector<hardwareitem> biosinfo, cpuinfo, diskinfo, gpuinfo,
                               nicinfo, monitorinfo, usbinfo, arpinfo;

    int loadingline = 10;

    auto showfetching = [&](const std::string& name, int index) {
        std::cout << "\033[" << (loadingline + index) << ";1H";
        std::cout << "                                                  ";
        std::cout << "\033[" << (loadingline + index) << ";1H";
        std::string lowername = name;
        std::transform(lowername.begin(), lowername.end(), lowername.begin(), ::tolower);
        std::cout << "  [" << (index + 1) << "] fetching " << lowername << " information..." << std::flush;
    };

    auto showcomplete = [&](const std::string& name, int index, int itemcount) {
        std::cout << "\033[" << (loadingline + index) << ";1H";
        std::cout << "                                                  ";
        std::cout << "\033[" << (loadingline + index) << ";1H";
        std::string lowername = name;
        std::transform(lowername.begin(), lowername.end(), lowername.begin(), ::tolower);
        std::cout << "  [" << (index + 1) << "] + " << lowername
                  << " (" << itemcount << " items)" << std::flush;
    };

    std::cout << "\033[" << (loadingline - 1) << ";1H";
    std::cout << "  initializing hardware detection..." << std::endl;

    showfetching("bios/system", 0);  Sleep(delay_per_fetch);
    biosinfo = hwinfo.getbiosinfo();
    showcomplete("bios/system", 0, (int)biosinfo.size());

    showfetching("cpu", 1);          Sleep(delay_per_fetch);
    cpuinfo = hwinfo.getprocessorinfo();
    showcomplete("cpu", 1, (int)cpuinfo.size());

    showfetching("disk", 2);         Sleep(delay_per_fetch);
    diskinfo = hwinfo.getdiskinfo();
    showcomplete("disk", 2, (int)diskinfo.size());

    showfetching("gpu", 3);          Sleep(delay_per_fetch);
    gpuinfo = hwinfo.getvideocontrollerinfo();
    showcomplete("gpu", 3, (int)gpuinfo.size());

    showfetching("network adapter", 4); Sleep(delay_per_fetch);
    nicinfo = hwinfo.getnetworkadapterinfo();
    showcomplete("network adapter", 4, (int)nicinfo.size());

    showfetching("monitor", 5);      Sleep(delay_per_fetch);
    monitorinfo = hwinfo.getmonitorinfo();
    showcomplete("monitor", 5, (int)monitorinfo.size());

    showfetching("usb device", 6);   Sleep(delay_per_fetch);
    usbinfo = hwinfo.getusbdevices();
    showcomplete("usb device", 6, (int)usbinfo.size());

    showfetching("arp table", 7);    Sleep(delay_per_fetch);
    arpinfo = hwinfo.getarptable();
    showcomplete("arp table", 7, (int)arpinfo.size());

    // teh method trust
    showfetching("intel raid check", 8); Sleep(200);

    auto raidDisks = DetectIntelRaid(diskinfo);
    bool intelRaidFound = !raidDisks.empty();

    // show both because we are chuds and proud to use IOCTLs (love you easy anti cheat!)
    std::vector<hardwareitem> diskinfo_display = BuildDiskInfoWithRaid(diskinfo, raidDisks);

    if (intelRaidFound) {
        std::cout << "\033[" << (loadingline + 8) << ";1H";
        std::cout << "                                                  ";
        std::cout << "\033[" << (loadingline + 8) << ";1H";
        std::cout << "  [9] ! intel rst/vroc raid detected (" << raidDisks.size() << " drive(s))" << std::flush;
    } else {
        showcomplete("intel raid check", 8, 0);
    }
    // ────────────────────────────────────────────────────────────────────────

    std::cout << "\033[" << (loadingline + 10) << ";1H";
    std::cout << std::endl;
    std::cout << "  + all hardware information loaded!" << std::endl;
    std::cout << "  press any key to continue..." << std::flush;
    _getch();

    // raid detection because we are cool like that!
    if (intelRaidFound) {
        MessageBoxA(
            nullptr,
            "Hey, you are using Intel RAID (RST/VROC).\n\n"
            "Just know your disk serials may appear as changed (they show a\n"
            "virtual RAID volume serial), but they aren't really changed --\n"
            "your physical drives still have their original serials.\n\n"
            "Go to [3] Disk Information to see both the RAID serial and\n"
            "the original physical serial side by side.",
            "Intel RAID Detected",
            MB_OK | MB_ICONINFORMATION
        );
    }
    // ────────────────────────────────────────────────────────────────────────

    bool running = true;
    while (running) {
        printmainmenu();
        int key = _getch();
        switch (key) {
        case '1': showcategorypage("bios / system information",    biosinfo);         break;
        case '2': showcategorypage("cpu information",              cpuinfo);          break;
        case '3': showcategorypage("disk information",             diskinfo_display); break; // augmented
        case '4': showcategorypage("gpu information",              gpuinfo);          break;
        case '5': showcategorypage("network adapter information",  nicinfo);          break;
        case '6': showcategorypage("monitor information (edid)",   monitorinfo);      break;
        case '7': showcategorypage("usb devices",                  usbinfo);          break;
        case '8': showcategorypage("arp table",                    arpinfo);          break;
        case '0':
        case 27:
            running = false;
            break;
        default:
            break;
        }
    }

    clearscreen();
    std::cout << "  goodbye!" << std::endl;
    return 0;
}
