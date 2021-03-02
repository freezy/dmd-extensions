# Virtual DMD

DMD Extensions does not only support hardware displays but can also render 
high-resolution dot matrix displays on normal PC monitors. The output can
be sized and positioned as needed, and supports effects to make it look like
a real DMD. Those effects are entirely rendered on the GPU for maximal 
performance.

![image](https://user-images.githubusercontent.com/70426/109708090-3ee0cf80-7b9b-11eb-9fdd-83523aa265f9.png)
<small>*Virtual DMD running on a 4k monitor*</small>

## Setup

In order to enable the virtual DMD via `DmdDevice.dll`, set the flag to true 
in `DmdDevice.ini`:

```ini
[virtualdmd]
enabled = true
```

When using `dmdext.exe`, you can enable it by setting it as output destination:

```bash
dmdext test --destination=virtual
```

This the default behavior, so we assume you're familiar how it works.

## Customization

The virtual DMD can be moved to the preferred location by dragging it with the mouse. It can also
be resized by dragging the bottom right corner. When running dmdext through `DmdDevice.dll`, you
can also right-click on the DMD and get a few more options:

<img width="640" src="https://user-images.githubusercontent.com/70426/109722467-02b66a80-7bad-11eb-857b-b3e258dcd083.png"/>

- **Save position globally** sets the default position and size, i.e. these 
  values in `DmdDevice.ini`:
  ```ini
  [virtualdmd]
  left = 0
  top = 0
  width = 1024
  height = 256
  ```
- **Save position for "{game name}"** only saves the position for the running game.
  This option might not be available if no game is running. It will add this to 
  `DmdDevice.ini`:
  ```ini
  [afm_113b]
  virtualdmd style = default
  virtualdmd left = 0
  virtualdmd top = 0
  virtualdmd width = 1024
  virtualdmd height = 256
  ```  
- **Ignore Aspect Ratio** lets the visually challenged freely resize the DMD.
- Since v1.9.0, **Customize Style** opens the customization panel where you
  can parameterize various effects to make the DMD look more realistic.

Styles are saved to `DmdDevice.ini`. As you know, [there are two ways to 
configure dmdext](https://github.com/freezy/dmd-extensions#configuration) 
depending on how you're running it. While you can provide all style parameters
via command line `dmdext.exe`, we recommend loading `DmdDevice.ini` with the 
`--use-ini` option. This allows you to keep all styles at one place.

For example, `dmdext test --use-ini=C:\Visual Pinball\VPinMAME\DmdDevice.ini` 
allows you to quickly customize the styles.

The way this works is that you:

- Create or update a style
- Apply the style globally or to the running game only

<img width="450" src="https://user-images.githubusercontent.com/70426/109726696-6fccfe80-7bb3-11eb-97a9-34fe00087031.png"/>

The upper sections (*DMD*, *Frame* and *Glass*) allow to customize your style. 
Changing values will immediately update the preview image at the top.

### Working with Styles

The *Load* button in the bottom section loads a style saved in `DmdDevice.ini`
into the upper sections and applies it to the preview image. This allows you to 
edit it.

Once you're happy with your new style, you should save it, which is what the 
*Save* button does. If you set the name to an existing one, that style will
be overridden by your changes. Otherwise, a new style is created. Saving only
saves the style, it doesn't apply it to the current display.

Usually you'd want to apply your style to the current DMD and make dmdext use
it the next time you run that game. For that, press the *Apply* button, which
does two things:

1. It applies the current parameters of the customization window to the running 
   game's display (without saving it).
2. It assigns the currently selected (saved) style to the running game.

That means that changes that aren't saved but applied to the currently running
game won't be applied the next time the game starts.

The assign button also has a dropdown option that also applies the style to the
game's DMD, but sets the style as the global default. This is also true when no
game is running, in which case there is no dropdown and the button is just 
labeled *Apply*.
