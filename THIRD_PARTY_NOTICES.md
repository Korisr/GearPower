# Third-party notices

GearPower is an independent C# implementation. No source code from the
projects below is copied verbatim; protocol descriptions, command layouts,
public reverse-engineering notes and selected UI/config concepts were used as
references and adapted.

| Project | License | Used for |
| --- | --- | --- |
| [Cxxhh/GearPower](https://github.com/Cxxhh/GearPower) | MIT | v2.0.0 device-profile schema/UI, grouped inventory and recent-log concepts, adapted to the single-Windows-app version |
| [Fan4Metal/mouse_tray](https://github.com/Fan4Metal/mouse_tray) | Not declared in repository at the time of writing | ATK/VXE/VGN (Compx Nordic 52/54), Razer, Lamzu and Logitech protocol layouts |
| [OpenRazer](https://github.com/openrazer/openrazer) | GPL-2.0-or-later | Razer 90-byte feature report and command class |
| [tcr27/razer-blackshark-battery-info](https://github.com/tcr27/razer-blackshark-battery-info) | MIT | Razer BlackShark V2 Pro `1532:0555` 64-byte interrupt OUT/IN battery layout |
| [seerge/g-helper](https://github.com/seerge/g-helper) | MIT | ASUS ROG `0x12 0x07` battery query |
| [LennardKittner/HyperHeadset](https://github.com/LennardKittner/HyperHeadset) | MIT | HyperX Cloud III and Cloud Flight 64-byte fallback battery packets |
| [hux0192/HyperX-Battery](https://github.com/hux0192/HyperX-Battery) | MIT (see LICENSE.txt) | HyperX Cloud Flight 20-byte `21 FF 05` battery packet |
| [csmith/alphabat](https://github.com/csmith/alphabat) | MIT | HyperX Cloud Alpha Wireless `21 BB 0B` query |
| [vibecode / Mchose-cli](https://gitlab.com/unk9201422/Mchose) | GPL-2.0 | MCHOSE K7 Ultra Realtek `0xFF01` report `0x11/0x13` and `0xE2` battery/status layout |
| [Linux kernel HID HyperX patch](https://lkml.iu.edu/hypermail/linux/kernel/2606.3/06496.html) | GPL-2.0 | HyperX Cloud III `0x66` commands and response layout |
| [Microsoft HID documentation](https://learn.microsoft.com/windows-hardware/drivers/hid/) | Microsoft docs license | HID capabilities, value caps, IOCTL usage |

Additional thanks to the reverse-engineering community around wireless
peripherals: without their public packet captures and notes, these protocols
would not be documented.

Trademarks and brand names (Razer, ASUS ROG, HyperX, ATK, VXE, Lamzu, Logitech,
etc.) belong to their respective owners. This project is not affiliated with or
endorsed by any of them.

If you are a rights holder and believe an attribution is missing or incorrect,
please open an issue and it will be corrected.

## Apple rechargeable keyboard battery report reference

Read-only Input report `0x90` (battery percentage in byte 2) was independently
implemented using the protocol behavior documented by:

- https://github.com/gozaltech/mkBatteryChecker
- https://github.com/fixtan/MagicKeyBattery

No source code from those projects was copied. Only Apple keyboard product IDs
`0267` and `026C` are enabled. Local hardware verification: `004C:026C` returned
85% over Bluetooth using
Input `0x90` after Magic Utilities was uninstalled and the Apple `KeyMagic2`
driver restored. With the Magic Utilities filter active, the same query
failed; that case remains visible with an unknown level and diagnostics.

Windows battery property decoding follows Microsoft's DEVPROPERTY type values:
https://learn.microsoft.com/en-us/windows-hardware/drivers/install/devproperty
The HFP battery node and BYTE value (90%) were verified locally on a HUAWEI
FreeBuds Pro 2; no third-party source code was copied for this implementation.
