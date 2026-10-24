# Windows Gateway ARP Pin

Windows utility for a specific network failure: a local gateway IP address is advertised by more than one device, so Windows keeps changing the gateway's ARP/MAC mapping. The result can be brief Internet stalls, packet loss, game disconnects, or web pages that only recover after a reload.

This tool creates a permanent neighbor entry on **one Windows computer and one network adapter**. It is a local workaround while the network owner investigates the duplicate gateway. It does not repair the router, access point, Wi-Fi extender, ISP, or the wider network.

## Download

Download the latest Windows x64 release from [GitHub Releases](https://github.com/ventriquo/windows-gateway-arp-pin/releases/latest). The release includes:

- `WindowsGatewayArpPin.exe`, a self-contained executable.
- A ZIP bundle containing the executable, this README, the incident guide, and the MIT license.

Run the executable as administrator. Windows may display an elevation prompt because changing a neighbor entry requires administrator permission.

## When this helps

Use this utility when local diagnostics support a duplicate gateway or ARP-flapping problem. Typical symptoms include:

- Online games disconnect or stop receiving updates for a fraction of a second.
- A web page hangs briefly but loads immediately after pressing reload.
- Download speed is high, while real-time applications still freeze.
- A Wi-Fi extender or repeater is reachable, but the upstream gateway is intermittent.
- The same gateway IP appears with different MAC addresses over time.

The tool is not a general Wi-Fi booster. It will not fix weak signal, channel congestion, DNS outages, ISP failures, or a faulty extender unless the actual cause is a changing gateway MAC mapping.

## Verify the problem first

Open PowerShell and inspect the active adapter and gateway:

```powershell
Get-NetIPConfiguration
Get-NetRoute -DestinationPrefix "0.0.0.0/0"
Get-NetNeighbor -IPAddress 192.168.1.1
tracert -d 8.8.8.8
```

Replace `192.168.1.1` with the gateway shown by `Get-NetRoute`. If the same gateway IP is associated with multiple MAC addresses, confirm which MAC belongs to the trusted upstream router before using this app. Do not guess the MAC address.

In the original incident that motivated this project:

- Gateway IP: `192.168.1.1`
- Trusted FiberHome LAN MAC: `90-55-DE-42-83-40`
- Conflicting MAC observed on the LAN: `2C-B6-C2-36-AF-EA`

## Use the app

1. Select the physical Wi-Fi or Ethernet adapter connected to the affected network.
2. Enter the gateway IPv4 address.
3. Enter the verified gateway MAC address.
4. Click **Pin MAC**.
5. Confirm the status says the mapping is **Permanent**.

The adapter list is populated from Windows `Get-NetAdapter` metadata and excludes virtual and filter adapters such as Npcap, WAN Miniport, Tailscale, QoS, and WFP interfaces. The dropdown shows the exact Windows adapter alias, for example `Wi-Fi 2 (Up)`. `Wi-Fi 2` is already the complete alias used by `netsh`; shortening it to `Wi-Fi` would not be reliable.

When the correct permanent mapping already exists, **Pin MAC** is disabled. **Release** removes the entry and lets Windows learn the gateway dynamically again.

The app performs the equivalent of the following commands. Replace the example interface, gateway, and MAC with values from your own network:

```powershell
Remove-NetNeighbor -InterfaceAlias "Wi-Fi 2" -IPAddress 192.168.1.1 -Confirm:$false -ErrorAction SilentlyContinue
New-NetNeighbor -InterfaceAlias "Wi-Fi 2" -IPAddress 192.168.1.1 -LinkLayerAddress "90-55-DE-42-83-40" -State Permanent
```

## Release before changing networks

The mapping is tied to the selected adapter and IP address. Release it before using a public network, another dorm network, or a different router that may also use `192.168.1.1`:

```powershell
Remove-NetNeighbor -InterfaceAlias "Wi-Fi 2" -IPAddress 192.168.1.1 -Confirm:$false
```

The app's **Release** button runs the same command. A permanent mapping can become wrong after moving to another network, changing routers, resetting an adapter, or changing the gateway address.

## Proper network-side fix

The network administrator should find the second device claiming the gateway IP and correct its configuration. A secondary router usually needs a unique management IP and either access-point/bridge mode with DHCP disabled or a separate routed subnet. Changing the main gateway address without understanding the network can disconnect other users.

## Limitations and safety

- Windows administrator permission is required.
- The pinned MAC must be verified. Guessing can block Internet access.
- The workaround affects only this Windows computer and selected interface.
- A reboot, adapter reset, network reset, driver change, or profile change may remove the entry.
- Ethernet works the same way as Wi-Fi if it has the same duplicate-gateway problem; select the physical Ethernet adapter.
- This tool does not hide traffic, bypass network access controls, or fix weak Wi-Fi signal.

## Build from source

Requirements: Windows 10 or 11 and the .NET 9 SDK.

```powershell
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

The project uses WinForms, `netsh`, and the built-in PowerShell `Get-NetAdapter` cmdlet. It has no third-party runtime dependency.

## Related searches

Windows ARP conflict, duplicate gateway IP, ARP flapping, changing gateway MAC, permanent ARP entry, static neighbor entry, MAC address pinning, Wi-Fi extender disconnects, range extender packet loss, dorm Wi-Fi gaming disconnects, intermittent Internet stalls, Windows `netsh` neighbor, PowerShell `Get-NetNeighbor`, gateway MAC fix.

## License

Copyright (c) 2026 ventriquo. Released under the [MIT License](LICENSE).
