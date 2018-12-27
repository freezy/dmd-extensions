# Alpha-Numeric Displays

DmdDevice.dll can render high-resolution segmented displays of pre-DMD area 
games. 

Every section of the display gets a separate window that can be placed 
arbitrarily on the monitor, matching the backglasses layout:

![image](https://user-images.githubusercontent.com/70426/49953889-73570b00-feff-11e8-97ee-109f1de6c4e8.png)

## Setup

In order to enable alphanumeric rendering, set the flag to true in DmdDevice.ini:

```
[alphanumeric]
enabled = true
```

If you just want to enable it for a game (and disable the DMD rendering of it),
create a new section:

```
[centaur]
alphanumeric enabled = true
virtualdmd enabled = false
```

## Customization

When hovering over the top right corner of a segmented display, a configuration
icon shows up. Clicking on it will open the customization dialog:

<image src="https://user-images.githubusercontent.com/70426/49953892-7651fb80-feff-11e8-85dc-0ab291ba14af.png" width="350"/>

You can change the preview text in the text box below. 

Customization works by creating a style and assigning it to a game. Changing
the style will apply it to all games using that style.

When loading or saving a style, the style is automatically applied to the 
running game.