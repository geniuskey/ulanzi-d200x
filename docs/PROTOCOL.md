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

Commands used: page images `0x0001`, display mode `0x0006`, brightness `0x000a`, and title style `0x000b`. The wide display occupies manifest position `3_2`. Clock and usage updates also upload complete page bundles with a manifest and all 14 display images. Partial `0x000d` transfers are avoided because they may cause other images to go black later. Unchanged button PNGs stay in memory and are replaced when synchronizing a page.

Input events `0x0101` / `0x0102` contain an index and state. Button indices are `0..12`, page navigation uses `15/16`, and knobs use `17/18/19`. States `1/2/3` mean press / turn left / turn right.

The clock renders PC local time once per second. The audio overlay remains visible for the configured time after input. A 250 ms timer coalesces updates; page and wide-display uploads share one write lock to preserve ordering.
