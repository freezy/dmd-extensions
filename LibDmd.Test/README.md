# Testing

This project contains unit tests that test the render graph, conversion between frame formats, and up- and downscaling.

However, many features are difficult to cover through unit tests, so this document describes all the different cases
that dmdext should be able to handle.

## PinMAME / Visual Pinball

Easiest is to just run PinMAME's `setup.exe` and test a few ROMs (see list below). You should be able to change settings
in `DmdDevice.ini` between games without having to restart `setup.exe`.

This is a list of games with non-standard display sizes or colorizations.

| Title                                | ROM Name  | Output                          | Comments               |  🎨  |
|--------------------------------------|-----------|---------------------------------|------------------------|:----:|
| AC/DC Limited Edition (v1.70.0) MOD  | acd_170hc | 128x32                          |                        | ✔️🩹 |
| Alley Cats (Shuffle) (L-7)           | alcat_l7  | 6x4Num_4x1Num                   |                        |  ❌   |
| Atlantis (rev. 3)                    | atlantis  | 4x7Num10                        |                        |  ❌   |
| Back to the Future (2.7)             | bttf_a27  | 2x16Alpha                       |                        |  ✔️  |
| Bad Girls                            | badgirls  | 2x20Alpha                       |                        |  ❌   |
| Batman (1.06)                        | btmn_106  | 128x16 DMD                      |                        |  ✔️  |
| Baywatch (4.00)                      | baywatch  | 192x64 DMD                      |                        | ✔️️  |
| Black Knight (L-4)                   | bk_l4     | 2x7Num 2x7Num 4x1Num_gen7       |                        |  ❌   |
| Checkpoint (1.7)                     | ckpt_a17  | 128x16 DMD                      |                        |  ✔️  |
| Flipper Football (1.04)              | ffv104    | 256x64 DMD                      |                        |  ❌️  |
| High Speed (L-3)                     | hs_l3     | 2x7Alpha 2x7Num 4x1Num          |                        |  ❌   |
| Hook (4.08)                          | hook_408  | 128x16 DMD                      |                        |  ✔️  |
| Jokerz (L-6)                         | jokrz_l6  | 2x7Num 2x16Alpha                |                        |  ❌   |
| Police Force (LA-4)                  | polic_l4  | 2x7Num 2x7Num 4x1Num            | Support recently added |  ❌   |
| Riverboat Gambler (L-3)              | rvrbt     | 1x16Alpha 1x16Num 1x7Num 1x4Num | Support recently added |  ❌   |
| Scared Stiff (1.5)                   | ss_15     | 128x32 DMD                      |                        | ✔️💉 |
| Space Shuttle (L-3)                  | sshtl_l3  | 2x7Num10 2x7Num10 4x1Num        |                        |  ❌   |
| Star Trek 25th Anniversary (2.01)    | trek_201  | 128x16 DMD                      |                        |  ✔️  |
| Supersonic (7-digit conversion)      | sstb      | 2x7Num 2x7Num 4x1Num            |                        |  ❌   |
| TRON: Legacy Limited Edition (v1.74) | trn_174h  | 128x32                          |                        |  💉  |
| Teenage Mutant Ninja Turtles (1.04)  | tmnt_104  | 128x16 DMD                      |                        | ✔↕️️ |
| Topaz (Shuffle) (L-1)                | topaz_l1  | 2x6Num 2x6Num 4x1Num            |                        |  ❌   |

*Legend:*<br>
🎨 Colorization available?<br>
❌ None<br>
✔️ Pin2Color available<br>
💉 Serum available<br>
↕️ Contains upscaled frames<br>
🩹️ Colorization driven by ROM<br>

## Pinball FX

Pinball FX uses the same `DmdDevice.dll` as PinMAME. All games are supported, inclusively segment display games and LCD
games.

## Pinball FX2/FX3

Pinball FX2 and FX3 use the `mirror` feature of `dmdext.exe`. FX2 is very legacy, but FX3 should work with all but segment
display games. Here are some test commands:

```bash
dmdext.exe mirror --source pinballfx3
dmdext.exe mirror --source pinballfx3 --colorize --plugin "C:\Visual Pinball\VPinMAME\pin2color.dll"
```

## The Pinball Arcade
## Pro Pinball: Timeshock
## Future Pinball
## Image/GIF file on disk
## Screen Grabber
## Network