using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WifiGatewayPin;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class AppSettings
{
    public string InterfaceName { get; set; } = "Wi-Fi 2";
    public string GatewayAddress { get; set; } = "192.168.1.1";
    public string GatewayMac { get; set; } = "90-55-DE-42-83-40";
}

internal sealed class CommandResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
}

internal sealed class AdapterChoice
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public OperationalStatus Status { get; init; }
    public string StatusText { get; init; } = string.Empty;

    // Keep the exact Windows alias visible; this is the value used by netsh.
    public override string ToString() => $"{Name} ({StatusText})";
}

internal sealed class NeighborStatus
{
    public bool Exists { get; init; }
    public string IpAddress { get; init; } = string.Empty;
    public string MacAddress { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public bool IsPermanent => State.Equals("Permanent", StringComparison.OrdinalIgnoreCase);
}

internal sealed class MainForm : Form
{
    private static readonly Regex MacPattern = new(
        "^[0-9A-Fa-f]{2}([-:])[0-9A-Fa-f]{2}(\\1[0-9A-Fa-f]{2}){4}$",
        RegexOptions.Compiled);

    private readonly ComboBox interfaceBox = new();
    private readonly TextBox gatewayBox = new();
    private readonly TextBox macBox = new();
    private readonly Label statusLabel = new();
    private readonly Button pinButton = new();
    private readonly Button releaseButton = new();
    private NeighborStatus? currentNeighbor;
    private readonly string settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WifiGatewayPin",
        "settings.json");

    public MainForm()
    {
        Text = "Gateway ARP Pin";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        ClientSize = new Size(760, 500);
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(18),
            AutoSize = false
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0, 0, 0, 8)
        };
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var title = new Label
        {
            Text = "Gateway ARP Pin",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        var subtitle = new Label
        {
            Text = "Pin this PC's gateway MAC for the selected network interface.",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(90, 90, 90),
            TextAlign = ContentAlignment.MiddleLeft
        };
        heading.Controls.Add(title, 0, 0);
        heading.Controls.Add(subtitle, 0, 1);
        root.Controls.Add(heading, 0, 0);
        root.SetColumnSpan(heading, 2);

        interfaceBox.DropDownStyle = ComboBoxStyle.DropDown;
        interfaceBox.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        interfaceBox.AutoCompleteSource = AutoCompleteSource.ListItems;
        interfaceBox.DropDownWidth = 560;
        interfaceBox.IntegralHeight = false;
        interfaceBox.MaxDropDownItems = 12;
        var interfaceRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Margin = new Padding(0),
            Padding = new Padding(0, 2, 0, 2)
        };
        interfaceBox.Width = 430;
        var refreshAdaptersButton = new Button { Text = "Reload", AutoSize = true, MinimumSize = new Size(82, 30), Height = 30 };
        refreshAdaptersButton.Click += (_, _) => RefreshAdapters();
        interfaceRow.Controls.Add(interfaceBox);
        interfaceRow.Controls.Add(refreshAdaptersButton);
        var targetGroup = new GroupBox
        {
            Text = "Connection target",
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0, 0, 0, 10)
        };
        var targetLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Margin = new Padding(0)
        };
        targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 152));
        targetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        targetLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        targetLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        targetLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        targetLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AddField(targetLayout, "Network interface", interfaceRow, 0);
        AddField(targetLayout, "Gateway IP", gatewayBox, 1);
        AddField(targetLayout, "Gateway MAC", macBox, 2);
        var note = new Label
        {
            Text = "Use the MAC shown by the trusted gateway. This setting affects this PC only.",
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(90, 90, 90),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, 2, 0, 0)
        };
        targetLayout.Controls.Add(note, 0, 3);
        targetLayout.SetColumnSpan(note, 2);
        targetGroup.Controls.Add(targetLayout);
        root.Controls.Add(targetGroup, 0, 1);
        root.SetColumnSpan(targetGroup, 2);

        var actionGroup = new GroupBox
        {
            Text = "Actions",
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 8),
            Margin = new Padding(0, 0, 0, 10)
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Margin = new Padding(0)
        };
        pinButton.Text = "Pin MAC";
        pinButton.MinimumSize = new Size(112, 32);
        pinButton.Height = 32;
        pinButton.UseVisualStyleBackColor = true;
        releaseButton.Text = "Release";
        releaseButton.MinimumSize = new Size(112, 32);
        releaseButton.Height = 32;
        releaseButton.UseVisualStyleBackColor = true;
        var defaultsButton = new Button { Text = "FiberHome default", AutoSize = true, MinimumSize = new Size(150, 32), Height = 32 };
        var refreshButton = new Button { Text = "Refresh status", AutoSize = true, MinimumSize = new Size(122, 32), Height = 32 };
        pinButton.Click += (_, _) => PinMac();
        releaseButton.Click += (_, _) => ReleaseMac();
        defaultsButton.Click += (_, _) => SetFiberHomeDefaults();
        refreshButton.Click += (_, _) => RefreshNeighborStatus();
        buttons.Controls.Add(pinButton);
        buttons.Controls.Add(releaseButton);
        buttons.Controls.Add(defaultsButton);
        buttons.Controls.Add(refreshButton);
        actionGroup.Controls.Add(buttons);
        root.Controls.Add(actionGroup, 0, 2);
        root.SetColumnSpan(actionGroup, 2);

        var statusGroup = new GroupBox
        {
            Text = "Status",
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(0)
        };
        statusLabel.Text = "Ready. This app requires administrator permission.";
        statusLabel.AutoSize = false;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Padding = new Padding(10, 4, 10, 4);
        statusLabel.BorderStyle = BorderStyle.FixedSingle;
        statusLabel.BackColor = Color.FromArgb(247, 247, 247);
        statusLabel.ForeColor = Color.DimGray;
        statusGroup.Controls.Add(statusLabel);
        root.Controls.Add(statusGroup, 0, 3);
        root.SetColumnSpan(statusGroup, 2);

        Controls.Add(root);
        interfaceBox.SelectedIndexChanged += (_, _) => RefreshNeighborStatus();
        interfaceBox.TextChanged += (_, _) => { currentNeighbor = null; UpdateButtonStates(); };
        gatewayBox.TextChanged += (_, _) => { currentNeighbor = null; UpdateButtonStates(); };
        gatewayBox.Validated += (_, _) => RefreshNeighborStatus();
        macBox.TextChanged += (_, _) => UpdateButtonStates();
        RefreshAdapters();
        LoadSettings();
        Shown += (_, _) => RefreshNeighborStatus();
    }

    private static void AddField(TableLayoutPanel root, string labelText, Control control, int row)
    {
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TextAlign = ContentAlignment.MiddleLeft
        };
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 3, 0, 3);
        root.Controls.Add(label, 0, row);
        root.Controls.Add(control, 1, row);
    }

    private string CurrentInterfaceName => interfaceBox.SelectedItem is AdapterChoice choice
        ? choice.Name
        : interfaceBox.Text.Trim();

    private void SelectInterface(string name)
    {
        var match = interfaceBox.Items.Cast<AdapterChoice>()
            .FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            interfaceBox.SelectedItem = match;
            return;
        }

        interfaceBox.SelectedItem = null;
        interfaceBox.Text = name;
    }

    private void RefreshAdapters()
    {
        var selectedName = CurrentInterfaceName;
        List<AdapterChoice> adapters;
        try
        {
            adapters = LoadPhysicalAdaptersFromWindows();
        }
        catch
        {
            // Keep the app usable on older Windows installations without NetAdapter cmdlets.
            adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsPhysicalNetworkAdapter)
                .OrderByDescending(adapter => adapter.OperationalStatus == OperationalStatus.Up)
                .ThenBy(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase)
                .Select(adapter => new AdapterChoice
                {
                    Name = adapter.Name,
                    Description = adapter.Description,
                    Status = adapter.OperationalStatus,
                    StatusText = adapter.OperationalStatus.ToString()
                })
                .ToList();
        }

        interfaceBox.Items.Clear();
        foreach (var adapter in adapters)
            interfaceBox.Items.Add(adapter);

        SelectInterface(string.IsNullOrWhiteSpace(selectedName) ? "Wi-Fi 2" : selectedName);
        SetStatus($"Detected {adapters.Count} physical network interface{(adapters.Count == 1 ? "" : "s")}.", false);
    }

    private static List<AdapterChoice> LoadPhysicalAdaptersFromWindows()
    {
        const string command =
            "Get-NetAdapter -ErrorAction Stop | " +
            "Where-Object { $_.HardwareInterface -eq $true -and $_.Virtual -ne $true } | " +
            "Select-Object Name,InterfaceDescription,Status | ConvertTo-Json -Compress";
        var result = RunPowerShell(command);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(result.Output.Trim());

        var json = result.Output.Trim();
        if (string.IsNullOrWhiteSpace(json))
            return new List<AdapterChoice>();

        using var document = JsonDocument.Parse(json);
        var elements = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToList()
            : new List<JsonElement> { document.RootElement };

        return elements
            .Where(element => element.ValueKind == JsonValueKind.Object)
            .Select(element => new AdapterChoice
            {
                Name = ReadJsonString(element, "Name"),
                Description = ReadJsonString(element, "InterfaceDescription"),
                Status = ParseOperationalStatus(ReadJsonString(element, "Status")),
                StatusText = ReadJsonString(element, "Status")
            })
            .Where(adapter => !string.IsNullOrWhiteSpace(adapter.Name))
            .OrderByDescending(adapter => adapter.Status == OperationalStatus.Up)
            .ThenBy(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ReadJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static OperationalStatus ParseOperationalStatus(string value)
    {
        return Enum.TryParse<OperationalStatus>(value, true, out var status)
            ? status
            : OperationalStatus.Unknown;
    }

    private static bool IsPhysicalNetworkAdapter(NetworkInterface adapter)
    {
        return adapter.NetworkInterfaceType is
            NetworkInterfaceType.Wireless80211 or
            NetworkInterfaceType.Ethernet or
            NetworkInterfaceType.GigabitEthernet or
            NetworkInterfaceType.FastEthernetT or
            NetworkInterfaceType.FastEthernetFx;
    }

    private void SetFiberHomeDefaults()
    {
        if (interfaceBox.Items.Cast<AdapterChoice>().Any(item => item.Name.Equals("Wi-Fi 2", StringComparison.OrdinalIgnoreCase)))
            SelectInterface("Wi-Fi 2");
        gatewayBox.Text = "192.168.1.1";
        macBox.Text = "90-55-DE-42-83-40";
        SetStatus("FiberHome defaults loaded.", false);
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(settingsPath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath));
                if (settings is not null)
                {
                    SelectInterface(settings.InterfaceName);
                    gatewayBox.Text = settings.GatewayAddress;
                    macBox.Text = settings.GatewayMac;
                    return;
                }
            }
        }
        catch
        {
            // Defaults remain usable if the settings file is damaged.
        }

        SetFiberHomeDefaults();
    }

    private void SaveSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var settings = new AppSettings
        {
            InterfaceName = CurrentInterfaceName,
            GatewayAddress = gatewayBox.Text.Trim(),
            GatewayMac = NormalizeMac(macBox.Text)
        };
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private bool ValidateFields(out string normalizedMac)
    {
        normalizedMac = string.Empty;
        if (string.IsNullOrWhiteSpace(CurrentInterfaceName) || CurrentInterfaceName.Contains('"'))
        {
            SetStatus("Enter a valid Wi-Fi interface name.", true);
            return false;
        }

        if (!System.Net.IPAddress.TryParse(gatewayBox.Text.Trim(), out var gateway) || gateway.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            SetStatus("Enter a valid IPv4 gateway address.", true);
            return false;
        }

        if (!MacPattern.IsMatch(macBox.Text.Trim()))
        {
            SetStatus("Enter a MAC address such as 90-55-DE-42-83-40.", true);
            return false;
        }

        normalizedMac = NormalizeMac(macBox.Text);
        macBox.Text = normalizedMac;
        return true;
    }

    private void PinMac()
    {
        if (!ValidateFields(out var mac))
            return;

        try
        {
            SaveSettings();
            RunNetsh("interface", "ipv4", "delete", "neighbors", $"interface={CurrentInterfaceName}", $"address={gatewayBox.Text.Trim()}");
            var add = RunNetsh(
                "interface", "ipv4", "add", "neighbors",
                $"interface={CurrentInterfaceName}",
                $"address={gatewayBox.Text.Trim()}",
                $"neighbor={mac}",
                "store=persistent");

            if (add.ExitCode != 0)
            {
                SetStatus("Pin failed: " + add.Output.Trim(), true);
                return;
            }

            var check = RunNetsh("interface", "ipv4", "show", "neighbors", $"interface={CurrentInterfaceName}", $"address={gatewayBox.Text.Trim()}");
            currentNeighbor = ParseNeighbor(check.Output);
            SetStatus("Pinned successfully. " + DescribeNeighbor(currentNeighbor, mac), false);
            UpdateButtonStates();
        }
        catch (Exception ex)
        {
            SetStatus("Pin failed: " + ex.Message, true);
            UpdateButtonStates();
        }
    }

    private void ReleaseMac()
    {
        if (!ValidateFields(out _))
            return;

        try
        {
            SaveSettings();
            var result = RunNetsh("interface", "ipv4", "delete", "neighbors", $"interface={CurrentInterfaceName}", $"address={gatewayBox.Text.Trim()}");
            currentNeighbor = null;
            SetStatus(result.ExitCode == 0 ? "Released. Windows can learn the gateway MAC dynamically again." : "Release requested. The entry may already be absent.", false);
            UpdateButtonStates();
        }
        catch (Exception ex)
        {
            SetStatus("Release failed: " + ex.Message, true);
            UpdateButtonStates();
        }
    }

    private void RefreshNeighborStatus()
    {
        if (string.IsNullOrWhiteSpace(CurrentInterfaceName) || string.IsNullOrWhiteSpace(gatewayBox.Text))
        {
            currentNeighbor = null;
            UpdateButtonStates();
            SetStatus("Enter an interface and gateway, then refresh status.", true);
            return;
        }

        try
        {
            var result = RunNetsh("interface", "ipv4", "show", "neighbors", $"interface={CurrentInterfaceName}", $"address={gatewayBox.Text.Trim()}");
            currentNeighbor = result.ExitCode == 0 ? ParseNeighbor(result.Output) : null;
            SetStatus(result.ExitCode == 0 ? DescribeNeighbor(currentNeighbor, GetConfiguredMac()) : result.Output.Trim(), result.ExitCode != 0);
            UpdateButtonStates();
        }
        catch (Exception ex)
        {
            currentNeighbor = null;
            SetStatus("Could not read neighbor status: " + ex.Message, true);
            UpdateButtonStates();
        }
    }

    private void UpdateButtonStates()
    {
        var configuredMac = GetConfiguredMac();
        pinButton.Enabled = configuredMac is not null &&
            !(currentNeighbor?.IsPermanent == true && currentNeighbor.MacAddress.Equals(configuredMac, StringComparison.OrdinalIgnoreCase));
        releaseButton.Enabled = currentNeighbor?.Exists == true;
    }

    private string? GetConfiguredMac()
    {
        return MacPattern.IsMatch(macBox.Text.Trim()) ? NormalizeMac(macBox.Text) : null;
    }

    private static NeighborStatus ParseNeighbor(string output)
    {
        var match = Regex.Match(output, @"(?im)^\s*(?<ip>\d{1,3}(?:\.\d{1,3}){3})\s+(?<mac>[0-9a-f]{2}(?:-[0-9a-f]{2}){5})\s+(?<type>\w+)\s*$");
        if (!match.Success)
            return new NeighborStatus();

        return new NeighborStatus
        {
            Exists = true,
            IpAddress = match.Groups["ip"].Value,
            MacAddress = match.Groups["mac"].Value.ToUpperInvariant(),
            State = match.Groups["type"].Value
        };
    }

    private static string DescribeNeighbor(NeighborStatus? neighbor, string? configuredMac)
    {
        if (neighbor is not { Exists: true })
            return "Not pinned: no neighbor entry is currently present.";

        if (neighbor.IsPermanent && configuredMac is not null && neighbor.MacAddress.Equals(configuredMac, StringComparison.OrdinalIgnoreCase))
            return $"Pinned: {neighbor.IpAddress} -> {neighbor.MacAddress} (Permanent).";

        return neighbor.IsPermanent
            ? $"Permanent mapping found: {neighbor.IpAddress} -> {neighbor.MacAddress}. It differs from the configured MAC."
            : $"Not pinned: {neighbor.IpAddress} -> {neighbor.MacAddress} ({neighbor.State}, dynamic).";
    }

    private static CommandResult RunNetsh(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "netsh.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start netsh.exe.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CommandResult { ExitCode = process.ExitCode, Output = string.IsNullOrWhiteSpace(error) ? output : error };
    }

    private static CommandResult RunPowerShell(string command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(command);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start powershell.exe.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CommandResult { ExitCode = process.ExitCode, Output = string.IsNullOrWhiteSpace(error) ? output : error };
    }

    private void SetStatus(string text, bool error)
    {
        statusLabel.Text = text;
        statusLabel.ForeColor = error
            ? Color.Firebrick
            : text.StartsWith("Pinned", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Pinned successfully", StringComparison.OrdinalIgnoreCase)
                ? Color.DarkGreen
                : text.StartsWith("Not pinned", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Permanent mapping", StringComparison.OrdinalIgnoreCase)
                    ? Color.DarkGoldenrod
                    : Color.DimGray;
        statusLabel.BackColor = error ? Color.FromArgb(255, 242, 242) : Color.FromArgb(247, 247, 247);
    }

    private static string NormalizeMac(string value)
    {
        var hex = new string(value.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        return string.Join("-", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
    }
}
