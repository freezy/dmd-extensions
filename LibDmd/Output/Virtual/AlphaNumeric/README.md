# Alpha-Numeric Displays

DmdDevice.dll can render high-resolution segmented displays of pre-DMD area 
games:

<image src="https://user-images.githubusercontent.com/70426/50459439-5f81bf00-096b-11e9-9f75-f70387f2c9cc.png" width="500"/>

Every section of the display gets a separate window that can be placed 
arbitrarily on the monitor, matching the backglasses layout:

<image src="https://user-images.githubusercontent.com/70426/49953889-73570b00-feff-11e8-97ee-109f1de6c4e8.png" width="500"/>

## Setup

In order to enable alphanumeric rendering, set the flag to true in `DmdDevice.ini`:

```ini
[alphanumeric]
enabled = true
```

If you just want to enable it for a game (and disable the DMD),
create a new section:

```ini
[centaur]
alphanumeric enabled = true
virtualdmd enabled = false
```

## Customization

When hovering over the top right corner of a segmented display, a configuration
icon shows up. Clicking on it will open the customization dialog:

<image src="https://user-images.githubusercontent.com/70426/49953892-7651fb80-feff-11e8-85dc-0ab291ba14af.png" width="350"/>

You can change the preview text in the text box below. 

Customization works by creating or modifying a style and assigning it to a game.
Styles are saved to `DmdDevice.ini`. For example, the default style looks like 
that:

```ini
[alphanumeric]
style.default.skewangle = 12
style.default.backgroundcolor = ff000000
style.default.foreground.enabled = true
style.default.foreground.color = fffbe6cb
style.default.foreground.blur.enabled = true
style.default.foreground.blur.x = 2
style.default.foreground.blur.y = 2
style.default.foreground.dilate.enabled = false
style.default.innerglow.enabled = true
style.default.innerglow.color = a0dd6a03
style.default.innerglow.blur.enabled = true
style.default.innerglow.blur.x = 15
style.default.innerglow.blur.y = 13
style.default.innerglow.dilate.enabled = true
style.default.innerglow.dilate.x = 15
style.default.innerglow.dilate.y = 10
style.default.outerglow.enabled = true
style.default.outerglow.color = 40b65829
style.default.outerglow.blur.enabled = true
style.default.outerglow.blur.x = 50
style.default.outerglow.blur.y = 50
style.default.outerglow.dilate.enabled = true
style.default.outerglow.dilate.x = 90
style.default.outerglow.dilate.y = 40
style.default.background.enabled = true
style.default.background.color = 20ffffff
style.default.background.blur.enabled = true
style.default.background.blur.x = 7
style.default.background.blur.y = 7
style.default.background.dilate.enabled = false
```

The customization dialog allows to load, save and delete styles, so you usually
don't have to deal with this part of the ini.

### Apply a Style

Once you're happy with your new style, you should save it. If you want the 
currently running game to use it in the future, press the *Apply* button,
which does two things:

1. It applies the current parameters of the customization window to the running
   game's displays, without saving it.
2. It assigns the currently selected (saved) style to the running game.

Note that changes that aren't saved but applied to the currently running game 
won't be applied the next time the game starts.

Assigning a game to a style in `DmdDevice.ini` looks like that:

```ini
[centaur]
alphanumeric style = MyNewStyle
```

## Display Position

When you resize or move the displays around, their position are automatically
saved to `DmdDevice.ini` so the next time the game starts they are correctly
positioned. A configuration looks like that:


```ini
[fh_l9]
alphanumeric pos.0.left = 2561
alphanumeric pos.0.top = 260
alphanumeric pos.0.height = 120
alphanumeric pos.1.left = 2561
alphanumeric pos.1.top = 379
alphanumeric pos.1.height = 120
```

Note that there is no `width` parameter, because the width is calculated based
on the height and the number of characters of the display. This makes it easy
to apply the same height to all displays of a game by editing the `.ini`, 
something you might consider doing instead of fiddling with the mouse.
