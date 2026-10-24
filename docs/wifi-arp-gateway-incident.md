# Wi-Fi Gateway Conflict: RE305 and Dorm Network

## Confirmed setup

- Laptop Wi-Fi interface: `Wi-Fi 2`
- Laptop address: `192.168.1.65`
- RE305 management address: `192.168.1.199`
- RE305 SSID: `ventriquo2`
- RE305 BSSID: `a8:29:dc:46:55:c9`
- FiberHome HG6245N GPON router: LAN MAC `90-55-DE-42-83-40`
- Other device seen on the same LAN: `2C-B6-C2-36-AF-EA`
- Gateway address: `192.168.1.1`

The FiberHome has an active GPON/PPPoE Internet connection and is the working upstream gateway. The laptop repeatedly saw both `90-55-DE-42-83-40` and `2C-B6-C2-36-AF-EA` claiming `192.168.1.1`.

## What was happening

ARP maps an IP address to a local hardware MAC address. The laptop normally needs one mapping:

```text
192.168.1.1 -> one gateway MAC
```

The dorm network was advertising two different MAC addresses for the same gateway IP. Windows replaced its ARP entry as the advertisements changed. This is an ARP-flapping or duplicate-gateway conflict.

The RE305 is operating as a bridge/repeater, so it forwards this conflict to the laptop. The Wi-Fi radio itself was stable: the BSSID and channel stayed fixed, signal was about 90%, and the RE305 remained reachable. The changing gateway mapping caused short interruptions to existing game and web connections.

## Laptop-only workaround

### Using the utility

Open the included `WifiGatewayPin-physical-*.exe` as administrator (the package uses a timestamped filename). The app asks Windows for hardware network adapters and excludes Npcap, QoS, WFP, WAN Miniport, Tailscale, and other virtual/filter interfaces. Disconnected physical Ethernet adapters remain available in the dropdown. Choose `Wi-Fi 2`, `Ethernet`, or another interface, then confirm the gateway IP and MAC.

At startup, and when **Refresh status** is pressed, the app checks the neighbor entry for the selected interface and gateway. It reports whether the mapping is `Permanent` (pinned), dynamic, or absent. **Pin MAC** applies the mapping; **Release** removes it. When the selected gateway is already permanently mapped to the configured MAC, **Pin MAC** is disabled because no change is needed. If a permanent entry points to a different MAC, the button remains enabled and the status calls out the mismatch.

Run PowerShell as Administrator while connected to `ventriquo2`:

```powershell
Remove-NetNeighbor -InterfaceAlias "Wi-Fi 2" -IPAddress 192.168.1.1 -Confirm:$false -ErrorAction SilentlyContinue
New-NetNeighbor -InterfaceAlias "Wi-Fi 2" -IPAddress 192.168.1.1 -LinkLayerAddress "90-55-DE-42-83-40" -State Permanent
Get-NetNeighbor -InterfaceAlias "Wi-Fi 2" -IPAddress 192.168.1.1 | Format-List IPAddress,LinkLayerAddress,State
```

Expected result:

```text
IPAddress        : 192.168.1.1
LinkLayerAddress : 90-55-DE-42-83-40
State            : Permanent
```

This pins only this laptop's traffic for `192.168.1.1` to the FiberHome MAC. It does not change the FiberHome, RE305, DHCP, Wi-Fi, or any other resident's device.

## Rollback

To return to normal dynamic ARP behavior:

```powershell
Remove-NetNeighbor -InterfaceAlias "Wi-Fi 2" -IPAddress 192.168.1.1 -Confirm:$false
```

After removal, Windows will learn the gateway MAC dynamically again.

## Persistence and limitations

`State Permanent` prevents normal ARP aging on this laptop. It is not a permanent repair of the dorm network. Windows may remove the entry after a reboot, adapter reset, driver change, network reset, or profile change, so verify it with `Get-NetNeighbor` when needed.

The entry is tied to the address `192.168.1.1` and the `Wi-Fi 2` interface. It is safe when using this dorm network with the FiberHome at that address, but it can be wrong on another network.

For a public library or another Wi-Fi network, remove the entry before connecting if that network might also use `192.168.1.1`:

```powershell
Remove-NetNeighbor -InterfaceAlias "Wi-Fi 2" -IPAddress 192.168.1.1 -Confirm:$false
```

If the other network uses a different gateway, such as `192.168.0.1` or `10.x.x.x`, this entry will not be used for normal Internet traffic. Still, removing it when changing networks is the safest habit. Re-add the FiberHome mapping only after reconnecting to `ventriquo2` and confirming the gateway is `192.168.1.1`.

## Proper network fix

The dorm administrator should locate `2C-B6-C2-36-AF-EA` and ensure that it does not also use `192.168.1.1`. If it is a secondary router or access point, it needs a unique management IP and either AP/bridge mode with DHCP disabled, or a separate LAN subnet and proper WAN routing.

Changing the FiberHome gateway address yourself would affect other residents and would not reliably fix the second device. The laptop-only ARP mapping is the low-risk workaround; renumbering the other device is the network-wide repair.
