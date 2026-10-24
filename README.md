# Windows Gateway ARP Pin

Suggested repository name: `windows-gateway-arp-pin`.

This small Windows utility pins a local IPv4 gateway address to a verified MAC address on one network interface. It is intended for a narrow problem: intermittent Internet stalls, game disconnects, packet loss, or short freezes caused by two devices claiming the same gateway IP address.

It is useful when diagnostics show symptoms such as:

- `192.168.1.1` sometimes resolving to different MAC addresses.
- An ARP entry changing between two gateway MAC addresses.
- A Wi-Fi extender or repeater forwarding a duplicate-gateway conflict.
- Web pages hanging briefly and loading immediately after reload.
- Online games disconnecting even though signal strength and download speed look good.
- Stable pings to the local extender but intermittent loss when reaching the gateway or Internet.

This is a laptop-only workaround. It does not repair the dormitory, apartment, office, ISP, router, access point, or Wi-Fi extender network. The proper network fix is for the network administrator to find the second device using the gateway IP and correct its addressing or routing mode.

## What the app does

The app requires administrator permission and performs the equivalent of these Windows commands:

```powershell
Remove-NetNeighbor -InterfaceAlias "Wi-Fi 2" -IPAddress 192.168.1.1 -Confirm:$false -ErrorAction SilentlyContinue
New-NetNeighbor -InterfaceAlias "Wi-Fi 2" -IPAddress 192.168.1.1 -LinkLayerAddress "90-55-DE-42-83-40" -State Permanent
```

The interface dropdown is filtered using Windows `Get-NetAdapter` metadata so it lists physical Ethernet and Wi-Fi adapters while excluding virtual and filter adapters such as Npcap, WAN Miniport, Tailscale, QoS, and WFP interfaces. The displayed name is the exact Windows interface alias. For example, `Wi-Fi 2` is already the complete alias used by `netsh`; a shortened prefix such as `Wi-Fi` would not be reliable.

The app checks the current neighbor entry when it opens. It reports whether the gateway mapping is permanent, dynamic, or absent. **Pin MAC** is disabled when the selected gateway is already permanently mapped to the configured MAC.

## When to use it

Use it only after verifying the gateway IP and trusted gateway MAC. A typical investigation uses:

```powershell
Get-NetNeighbor -IPAddress 192.168.1.1
Get-NetRoute -DestinationPrefix "0.0.0.0/0"
tracert -d 8.8.8.8
```

If the gateway IP is claimed by more than one MAC address, confirm which MAC belongs to the real upstream router before pinning it. In the original case, the trusted FiberHome LAN MAC was `90-55-DE-42-83-40` and the conflicting device was `2C-B6-C2-36-AF-EA`.

The pin is specific to one local adapter and gateway IP. It does not change the router, DHCP, Wi-Fi password, extender configuration, or other users' devices.

## Release the pin

Release the entry before using a different network if that network may use the same gateway address:

```powershell
Remove-NetNeighbor -InterfaceAlias "Wi-Fi 2" -IPAddress 192.168.1.1 -Confirm:$false
```

The app's **Release** button runs the same operation. A permanent mapping can become wrong after moving to a public Wi-Fi network, changing routers, resetting an adapter, or changing the gateway address.

## Limitations and safety

- Windows administrator permission is required.
- The pinned MAC must be verified. Guessing can block Internet access.
- The workaround affects only this Windows computer and selected interface.
- A reboot, adapter reset, network reset, driver change, or profile change may remove the entry.
- Ethernet works the same way as Wi-Fi if it has the same duplicate-gateway problem; select the physical Ethernet adapter.
- This tool does not hide traffic, bypass network access controls, or fix weak Wi-Fi signal.

## Build

Requirements: Windows 10/11 and the .NET 9 SDK.

```powershell
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

Run the published executable as administrator. The project is intentionally small and uses WinForms, `netsh`, and the built-in PowerShell `Get-NetAdapter` cmdlet; it has no third-party runtime dependency.

## Search terms

Windows ARP conflict, duplicate gateway IP, ARP flapping, changing gateway MAC, static ARP entry, permanent neighbor entry, MAC address pinning, Wi-Fi extender disconnects, range extender packet loss, dorm Wi-Fi gaming disconnects, intermittent Internet stalls, Windows netsh neighbor, PowerShell Get-NetNeighbor, gateway MAC fix.

## License

Add a license before publishing the repository. MIT is a reasonable choice for a small utility if you want others to reuse and modify it.
