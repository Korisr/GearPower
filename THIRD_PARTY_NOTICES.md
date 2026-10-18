# Third-party notices

GearPower is an independent C# implementation. No source code from the
projects below is copied into this repository; their protocol descriptions,
command layouts and public reverse-engineering notes were used as references.

| Project | License | Used for |
| --- | --- | --- |
| [Fan4Metal/mouse_tray](https://github.com/Fan4Metal/mouse_tray) | Not declared in repository at the time of writing | ATK/VXE/VGN (Compx Nordic 52/54), Razer, Lamzu and Logitech protocol layouts |
| [OpenRazer](https://github.com/openrazer/openrazer) | GPL-2.0-or-later | Razer 90-byte feature report and command class |
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
