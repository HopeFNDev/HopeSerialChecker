using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using HardwareSerialChecker.Models;
using HardwareSerialChecker.Services;
using HardwareSerialChecker.Controls;

namespace HardwareSerialChecker;

public partial class MainForm : Form
{
    private AnimatedBackgroundPanel backgroundPanel = null!;
    private TabControl tabControl = null!;
    private Panel gridHostPanel = null!;
    private Dictionary<string, DataGridView> dataGridViews;
    private Button btnRefresh = null!;
    private Button btnCopy = null!;
    private Button btnExportJson = null!;
    private Button btnExportCsv = null!;
    private Label lblStatus = null!;
    private HardwareInfoService hardwareService;
    private Dictionary<string, List<HardwareItem>> categoryData;

    public MainForm()
    {
        hardwareService = new HardwareInfoService();
        dataGridViews = new Dictionary<string, DataGridView>();
        categoryData = new Dictionary<string, List<HardwareItem>>();
        InitializeComponent();
    }

    // Try to load ICO from Assets first; if missing, load PNG and convert to Icon
    private Icon? LoadAppIcon()
    {
        try
        {
            var exePath = Application.ExecutablePath;
            if (!string.IsNullOrEmpty(exePath))
            {
                var exeIcon = Icon.ExtractAssociatedIcon(exePath);
                if (exeIcon != null)
                    return exeIcon;
            }

            var baseDir = AppContext.BaseDirectory;
            var icoPath = Path.Combine(baseDir, "Assets", "appicon.ico");
            if (File.Exists(icoPath))
            {
                return new Icon(icoPath);
            }

            var pngPath = Path.Combine(baseDir, "Assets", "appicon.png");
            if (File.Exists(pngPath))
            {
                using var bmp = new Bitmap(pngPath);
                var hIcon = bmp.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(hIcon).Clone();
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }
        catch { }
        return null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    // Create an .ico file from appicon.png if needed (multi-size PNG entries)
    private void EnsureIcoFromPngAssets()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var assetsDir = Path.Combine(baseDir, "Assets");
            Directory.CreateDirectory(assetsDir);

            var pngPath = Path.Combine(assetsDir, "appicon.png");
            var icoPath = Path.Combine(assetsDir, "appicon.ico");
            if (File.Exists(pngPath) && !File.Exists(icoPath))
            {
                CreateIcoFromPng(pngPath, icoPath, new[] { 16, 24, 32, 48, 64, 128, 256 });
            }
        }
        catch { }
    }

    // Writes a valid ICO with one or more PNG-compressed images (Vista+ compatible)
    private void CreateIcoFromPng(string pngPath, string icoPath, int[] sizes)
    {
        using var original = new Bitmap(pngPath);

        // Prepare PNG bytes for each size
        var images = new List<(int Size, byte[] PngBytes)>();
        foreach (var s in sizes.Distinct().OrderBy(x => x))
        {
            using var bmp = new Bitmap(s, s);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(original, new Rectangle(0, 0, s, s));
            }
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            images.Add((s, ms.ToArray()));
        }

        using var fs = new FileStream(icoPath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // ICONDIR
        bw.Write((ushort)0); // reserved
        bw.Write((ushort)1); // type = ICON
        bw.Write((ushort)images.Count); // count

        // Compute offsets: header (6) + entries (16*count)
        int offset = 6 + (16 * images.Count);
        foreach (var (size, png) in images)
        {
            // ICONDIRENTRY
            bw.Write((byte)(size == 256 ? 0 : size)); // width
            bw.Write((byte)(size == 256 ? 0 : size)); // height
            bw.Write((byte)0); // color count
            bw.Write((byte)0); // reserved
            bw.Write((ushort)1); // planes
            bw.Write((ushort)32); // bit count (informational)
            bw.Write(png.Length); // bytes in resource
            bw.Write(offset); // image offset
            offset += png.Length;
        }

        // Write the PNG images back-to-back
        foreach (var (_, png) in images)
        {
            bw.Write(png);
        }
    }

    private void InitializeComponent()
    {
        this.Text = "Hope's Serial Checker";
        this.Size = new Size(1200, 700);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.Black;

        // If only PNG is present, create an ICO alongside it
        EnsureIcoFromPngAssets();

        // Attempt to set window/taskbar icon from Assets
        var appIcon = LoadAppIcon();
        if (appIcon != null)
            this.Icon = appIcon;

        // Animated Background Panel
        backgroundPanel = new AnimatedBackgroundPanel
        {
            Dock = DockStyle.Fill
        };
        this.Controls.Add(backgroundPanel);

        // TabControl - header strip only (content is rendered separately
        // in gridHostPanel below).
        tabControl = new TransparentTabControl
        {
            Location = new Point(10, 10),
            Size = new Size(1160, 40),
            BackColor = Color.Black,
            ForeColor = Color.FromArgb(88, 101, 242),
            SizeMode = TabSizeMode.Fixed,
            ItemSize = new Size(120, 30)
        };
        backgroundPanel.Controls.Add(tabControl);

        // Panel that hosts the per-category DataGridViews, visually
        // separated from the tab strip.
        gridHostPanel = new Panel
        {
            Location = new Point(10, 70),
            Size = new Size(1160, 460),
            BackColor = Color.Black
        };
        backgroundPanel.Controls.Add(gridHostPanel);

        tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;

        // Create tabs for each category
        CreateTab("BIOS/System", "BIOS");
        CreateTab("CPU", "CPU");
        CreateTab("Disks", "Disk");
        CreateTab("GPU", "GPU");
        CreateTab("Network", "NIC");
        CreateTab("Monitors", "Monitor");
        CreateTab("USB", "USB");
        CreateTab("ARP", "ARP");

        // Buttons - semi-transparent
        btnRefresh = new Button
        {
            Text = "Refresh All",
            Location = new Point(10, 570),
            Size = new Size(100, 30),
            BackColor = Color.FromArgb(88, 101, 242),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(88, 101, 242);
        btnRefresh.Click += BtnRefresh_Click;
        backgroundPanel.Controls.Add(btnRefresh);

        btnCopy = new Button
        {
            Text = "Copy Selected",
            Location = new Point(120, 570),
            Size = new Size(120, 30),
            BackColor = Color.FromArgb(88, 101, 242),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnCopy.FlatAppearance.BorderColor = Color.FromArgb(88, 101, 242);
        btnCopy.Click += BtnCopy_Click;
        backgroundPanel.Controls.Add(btnCopy);

        btnExportJson = new Button
        {
            Text = "Export JSON",
            Location = new Point(250, 570),
            Size = new Size(120, 30),
            BackColor = Color.FromArgb(88, 101, 242),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnExportJson.FlatAppearance.BorderColor = Color.FromArgb(88, 101, 242);
        btnExportJson.Click += BtnExportJson_Click;
        backgroundPanel.Controls.Add(btnExportJson);

        btnExportCsv = new Button
        {
            Text = "Export CSV",
            Location = new Point(380, 570),
            Size = new Size(120, 30),
            BackColor = Color.FromArgb(88, 101, 242),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnExportCsv.FlatAppearance.BorderColor = Color.FromArgb(88, 101, 242);
        btnExportCsv.Click += BtnExportCsv_Click;
        backgroundPanel.Controls.Add(btnExportCsv);

        // Status Label
        lblStatus = new Label
        {
            Location = new Point(10, 610),
            Size = new Size(1160, 40),
            Text = "Click 'Refresh All' to load hardware information.",
            AutoSize = false,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(88, 101, 242)
        };
        backgroundPanel.Controls.Add(lblStatus);

        // Load data on startup (wrapped in try-catch to prevent startup crashes)
        this.Load += (s, e) =>
        {
            try
            {
                LoadHardwareInfo();
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error loading hardware info: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"Failed to load hardware information on startup:\n{ex.Message}\n\nYou can try clicking 'Refresh All' manually.",
                    "Startup Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
    }

    private void CreateTab(string tabName, string category)
    {
        var tabPage = new TransparentTabPage(tabName)
        {
            Tag = category
        };
        
        var dgv = new TransparentDataGridView
        {
            Dock = DockStyle.None,
            Location = new Point(10, 10),
            Size = new Size(gridHostPanel.Width - 40, 280),
            Anchor = AnchorStyles.None,
            AutoGenerateColumns = true,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.Black,
            GridColor = Color.FromArgb(88, 101, 242),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            EnableHeadersVisualStyles = false,
            RowHeadersVisible = false,
            EditMode = DataGridViewEditMode.EditProgrammatically,
            ScrollBars = ScrollBars.Both
        };
        
        // Style column headers - very dark, non-selectable
        dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(88, 101, 242);
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(88, 101, 242);
        dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
        dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        
        dgv.DefaultCellStyle.BackColor = Color.Black;
        dgv.DefaultCellStyle.ForeColor = Color.White;
        dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(88, 101, 242);
        dgv.DefaultCellStyle.SelectionForeColor = Color.White;
        
        dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(15, 15, 35);
        dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = Color.FromArgb(88, 101, 242);
        dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
        
        gridHostPanel.Controls.Add(dgv);
        // Center grid within the shared host panel and keep positioned on resize
        gridHostPanel.Resize += (s, e) => CenterGridInContainer(gridHostPanel, dgv);
        // Adjust height to content any time data changes
        dgv.DataBindingComplete += (s, e) => AdjustGridToContent(dgv);
        dgv.RowsAdded += (s, e) => AdjustGridToContent(dgv);
        dgv.RowsRemoved += (s, e) => AdjustGridToContent(dgv);
        tabControl.TabPages.Add(tabPage);
        dataGridViews[category] = dgv;
        // Initial centering and visibility (only the first tab's grid is shown)
        CenterGridInContainer(gridHostPanel, dgv);
        dgv.Visible = (tabControl.TabPages.Count == 1);
    }

    // Resize a grid's height to its content, leaving background visible, and
    // then recenter it inside its parent container.
    private void AdjustGridToContent(DataGridView dgv, int minRows = 1, int maxHeight = 450)
    {
        try
        {
            int header = dgv.ColumnHeadersVisible ? dgv.ColumnHeadersHeight : 0;
            int rowCount = dgv.Rows.Count;
            int rowHeight = dgv.RowTemplate?.Height > 0 ? dgv.RowTemplate.Height : 22;
            if (rowCount > 0)
            {
                // Use first row height if available (accounts for font/style)
                rowHeight = Math.Max(rowHeight, dgv.Rows[0].Height);
            }
            int rowsToMeasure = Math.Max(minRows, rowCount);
            int height = header + rowsToMeasure * rowHeight + 2; // small padding
            height = Math.Min(maxHeight, height);
            height = Math.Max(header + (minRows * rowHeight) + 2, height);
            dgv.Height = height;

            if (dgv.Parent is Control container)
            {
                CenterGridInContainer(container, dgv);
            }
        }
        catch { }
    }

    private void CenterGridInContainer(Control container, DataGridView dgv)
    {
        if (container == null || dgv == null) return;
        int margin = 20;
        int minWidth = 400;
        int maxWidth = Math.Min(container.ClientSize.Width - margin * 2, 1100);
        dgv.Width = Math.Max(minWidth, maxWidth);
        int x = Math.Max(margin, (container.ClientSize.Width - dgv.Width) / 2);
        int y = Math.Max(margin, (container.ClientSize.Height - dgv.Height) / 2);
        dgv.Location = new Point(x, y);
    }

    private void TabControl_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var tab = tabControl.SelectedTab;
        if (tab?.Tag is not string category)
            return;

        foreach (var grid in dataGridViews.Values)
            grid.Visible = false;

        if (dataGridViews.TryGetValue(category, out var dgv))
        {
            dgv.Visible = true;
            CenterGridInContainer(gridHostPanel, dgv);
        }
    }

    private void BtnRefresh_Click(object? sender, EventArgs e)
    {
        LoadHardwareInfo();
    }

    private void LoadHardwareInfo()
    {
        lblStatus.Text = "Loading hardware information...";
        lblStatus.ForeColor = Color.Blue;
        Application.DoEvents();

        try
        {
            categoryData.Clear();
            
            // Load BIOS/System info
            var biosData = hardwareService.GetBiosInfo();
            categoryData["BIOS"] = biosData;
            dataGridViews["BIOS"].DataSource = null;
            dataGridViews["BIOS"].DataSource = biosData;
            AdjustGridToContent(dataGridViews["BIOS"]);
            
            // Load CPU info
            var cpuData = hardwareService.GetProcessorInfo();
            categoryData["CPU"] = cpuData;
            dataGridViews["CPU"].DataSource = null;
            dataGridViews["CPU"].DataSource = cpuData;
            AdjustGridToContent(dataGridViews["CPU"]);
            
            // Load Disk info
            var diskData = hardwareService.GetDiskInfo();
            categoryData["Disk"] = diskData;
            dataGridViews["Disk"].DataSource = null;
            dataGridViews["Disk"].DataSource = diskData;
            AdjustGridToContent(dataGridViews["Disk"]);
            
            // Load GPU info
            var gpuData = hardwareService.GetVideoControllerInfo();
            categoryData["GPU"] = gpuData;
            dataGridViews["GPU"].DataSource = null;
            dataGridViews["GPU"].DataSource = gpuData;
            AdjustGridToContent(dataGridViews["GPU"]);
            
            // Load NIC info
            var nicData = hardwareService.GetNetworkAdapterInfo();
            categoryData["NIC"] = nicData;
            dataGridViews["NIC"].DataSource = null;
            dataGridViews["NIC"].DataSource = nicData;
            AdjustGridToContent(dataGridViews["NIC"]);

            // Load Monitor info
            var monitorData = hardwareService.GetMonitorInfo();
            categoryData["Monitor"] = monitorData;
            dataGridViews["Monitor"].DataSource = null;
            dataGridViews["Monitor"].DataSource = monitorData;
            AdjustGridToContent(dataGridViews["Monitor"]);

            // Load USB devices
            var usbData = hardwareService.GetUsbDevices();
            categoryData["USB"] = usbData;
            dataGridViews["USB"].DataSource = null;
            dataGridViews["USB"].DataSource = usbData;
            AdjustGridToContent(dataGridViews["USB"]);

            // Load ARP table
            var arpData = hardwareService.GetArpTable();
            categoryData["ARP"] = arpData;
            dataGridViews["ARP"].DataSource = null;
            dataGridViews["ARP"].DataSource = arpData;
            AdjustGridToContent(dataGridViews["ARP"]);
            
            var totalItems = categoryData.Values.Sum(list => list.Count);
            lblStatus.Text = $"Loaded {totalItems} hardware items across {categoryData.Count} categories.";
            lblStatus.ForeColor = Color.Green;
        }
        catch (Exception ex)
        {
            lblStatus.Text = $"Error: {ex.Message}";
            lblStatus.ForeColor = Color.Red;
            MessageBox.Show($"Failed to load hardware information:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnCopy_Click(object? sender, EventArgs e)
    {
        var currentTab = tabControl.SelectedTab;
        if (currentTab?.Tag is not string category)
            return;

        if (!dataGridViews.TryGetValue(category, out var dgv))
            return;
        
        if (dgv.SelectedRows.Count == 0)
        {
            MessageBox.Show("No rows selected.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Category\tName\tValue\tNotes");

        foreach (DataGridViewRow row in dgv.SelectedRows)
        {
            if (row.DataBoundItem is HardwareItem item)
            {
                sb.AppendLine($"{item.Category}\t{item.Name}\t{item.Value}\t{item.Notes}");
            }
        }

        try
        {
            Clipboard.SetText(sb.ToString());
            lblStatus.Text = $"Copied {dgv.SelectedRows.Count} rows to clipboard.";
            lblStatus.ForeColor = Color.Green;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy to clipboard:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnExportJson_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            FileName = $"hardware_info_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var allData = categoryData.Values.SelectMany(list => list).ToList();
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(allData, options);
                File.WriteAllText(dialog.FileName, json);
                lblStatus.Text = $"Exported {allData.Count} items to {dialog.FileName}";
                lblStatus.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export JSON:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void BtnExportCsv_Click(object? sender, EventArgs e)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = "csv",
            FileName = $"hardware_info_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Category,Name,Value,Notes");

                var allData = categoryData.Values.SelectMany(list => list).ToList();
                foreach (var item in allData)
                {
                    sb.AppendLine($"\"{item.Category}\",\"{item.Name}\",\"{item.Value}\",\"{item.Notes}\"");
                }

                File.WriteAllText(dialog.FileName, sb.ToString());
                lblStatus.Text = $"Exported {allData.Count} items to {dialog.FileName}";
                lblStatus.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export CSV:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
