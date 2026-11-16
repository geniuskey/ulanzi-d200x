# D200X protocol notes

An independent implementation informed by public protocol information from [edubox/opendeck-ulanzi-d200x](https://github.com/edubox/opendeck-ulanzi-d200x). Neither that plugin nor Ulanzi Studio is required. This is an unofficial protocol; firmware compatibility can vary.

USB VID/PID: `2207:0019`. Select a HID interface whose input/output reports are at least 1024 bytes. The observed Windows report lengths are 1025 bytes in both directions.

| Field | Length | Encoding |
| --- | --- | --- |
| Header | 2 | `7c 7c` |
| Command | 2 | Big-endian |
| Total body length | 4 | Little-endian |
| First body chunk | Up to 1016 | Remainder of the first 1024-byte frame |
| Subsequent body chunks | Up to 1024 per frame | Consecutive bytes from the original payload |

For 1025-byte Windows HID reports, prepend report ID `0`. Package 196×196 PNG images with `manifest.json` in a ZIP. Draw the wide display at 392×196, then compress it to 196×196 for firmware input. Adjust padding to avoid firmware misinterpreting particular bytes at ZIP packet boundaries. Keep the corresponding tests.

Commands used: page images `0x0001`, display mode `0x0006`, brightness `0x000a`, and title style `0x000b`. The wide display occupies manifest position `3_2`. All image transfers include the manifest and all 14 display images and restore image mode afterward. Do not use `0x000d`: testing on the user's D200X left every button black except the wide display, even with all assets included in the ZIP. This supersedes the initial theory that complete assets alone would make that opcode compatible.

Input events `0x0101` / `0x0102` contain an index and state. Button indices are `0..12`, page navigation uses `15/16`, and knobs use `17/18/19`. States `1/2/3` mean press / turn left / turn right.

The clock shows hours and minutes and uploads when the PC local minute changes. The audio overlay remains visible for the configured time after input and restores the current clock immediately when it expires. A 250 ms timer coalesces updates; page and wide-display uploads share one write lock to preserve ordering. Each wide update includes the latest usage images. Usage checks compare rendered PNG bytes and skip uploads if nothing visible changed, including after a combined clock/usage update. ZIP bytes are unsuitable for comparison because random padding and timestamps vary.

The November 2026 flicker investigation found repeated ~120 KB usage uploads every minute alongside clock uploads, without corresponding disconnect errors. Coalescing and PNG comparison reduce redundant transfers, but firmware page replacement can still visibly repaint on genuine changes. An attempted switch to `0x000d` based on other implementations caused black buttons on the target device and was reverted to the previously working `0x0001` plus image-mode sequence. Automated ZIP/framing checks do not prove physical display compatibility or absence of flicker.
