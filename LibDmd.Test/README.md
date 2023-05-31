# Testing

This project contains unit tests that test the render graph, conversion between frame formats, and up- and downscaling.

However, many features are difficult to cover through unit tests, so this document describes all the different cases
that dmdext should be able to handle.

## PinMAME Games

This is a list of games with non-standard display sizes.

| Title                               | ROM Name | Output                          | Comments               |  🎨  |
|-------------------------------------|----------|---------------------------------|------------------------|:----:|
| Alley Cats (Shuffle) (L-7)          | alcat_l7 | 6x4Num_4x1Num                   |                        |  ❌   |
| Atlantis (rev. 3)                   | atlantis | 4x7Num10                        |                        |  ❌   |
| Bad Girls                           | badgirls | 2x20Alpha                       |                        |  ❌   |
| Black Knight (L-4)                  | bk_l4    | 2x7Num 2x7Num 4x1Num_gen7       |                        |  ❌   |
| High Speed (L-3)                    | hs_l3    | 2x7Alpha 2x7Num 4x1Num          |                        |  ❌   |
| Jokerz (L-6)                        | jokrz_l6 | 2x7Num 2x16Alpha                |                        |  ❌   |
| Police Force (LA-4)                 | polic_l4 | 2x7Num 2x7Num 4x1Num            | Support recently added |  ❌   |
| Riverboat Gambler (L-3)             | rvrbt    | 1x16Alpha 1x16Num 1x7Num 1x4Num | Support recently added |  ❌   |
| Space Shuttle (L-3)                 | sshtl_l3 | 2x7Num10 2x7Num10 4x1Num        |                        |  ❌   |
| Supersonic (7-digit conversion)     | sstb     | 2x7Num 2x7Num 4x1Num            |                        |  ❌   |
| Topaz (Shuffle) (L-1)               | topaz_l1 | 2x6Num 2x6Num 4x1Num            |                        |  ❌   |
| Hook (4.08)                         | hook_408 | 128x16 DMD                      |                        |  ✔️  |
| Scared Stiff (1.5)                  | ss_15    | 128x32 DMD                      |                        | ✔️💉 |
| Batman (1.06)                       | btmn_106 | 128x16 DMD                      |                        |  ✔️  |
| Star Trek 25th Anniversary (2.01)   | trek_201 | 128x16 DMD                      |                        |  ✔️  |
| Checkpoint (1.7)                    | ckpt_a17 | 128x16 DMD                      |                        |  ✔️  |
| Teenage Mutant Ninja Turtles (1.04) | tmnt_104 | 128x16 DMD                      |                        | ✔↕️️ |
| Flipper Football (1.04)             | ffv104   | 256x64 DMD                      |                        |  ❌️  |

*Legend:*<br>
🎨 - Colorization available?<br>
❌ - None<br>
✔️ - Pin2Color available<br>
💉 - Serum available<br>
↕️ - Contains upscaled frames<br>