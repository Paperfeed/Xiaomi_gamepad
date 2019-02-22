Xiaomi Gamepad - x360 Input
====

This project is a modified version of the one created by irungentoo.
It now includes a windows service so that you do not have to run the application before you can use your gamepad.

The Xiaomi is an excellent controller but it lacks documentation.

The Mi project contains the source of a remapper that uses SCP Bus to make Windows see it as an Xbox controller. 
Rumble works perfectly.

The accelerometer_print folder contains a linux program that enables the accelerometer and prints the values.


## Installation
Just run Mipadservice.exe and click yes to install :-)


## Hardware
The Mi gamepad has the exact same layout as an Xbox 360 controller. 
The only thing different is that it has an accelerometer.

- 11 Buttons (A, B, X, Y, L1, R1, both joysticks, start, back, MI button in the front)
- One D-Pad
- Two joysticks
- Two triggers
- Two variable speed rumble motors
- One 3-axis accelerometer


## Protocol
Reversed engineered using mostly trial and error and looking at the decompiled `sensors.gxbaby.so` from an image of a Xiaomi TV box. 
The only valid packets according to the HID descriptor is the input packet and the set feature rumble packet.

There are probably more packet types than these.

### Packets:


#### Rumble (length 3)

This is the only packet documented on the Xiaomi site. Used to make the controller rumble.

```
 [byte (0x20)] 
 [byte (0x00 - 0xFF) Small motor rumble strength] 
 [byte (0x00 - 0xFF) Big motor rumble strength]
```



#### ??? (length ???)

I managed to stop input from my controller with this packet but I'm not sure what the values I put were.

```
[byte (0x21)]
[bytes ???]
```

#### Calibration (length 24 (maybe more))

```
[byte (0x22)]
[byte (each bit seems to denote if a section is enabled. 00000001 would mean only the first section is enabled.)]
[(8 bytes) section 1, LJoy]
[(8 bytes) section 2, Rjoy]
[(3 bytes) section 3, Ltrigger]
[(3 bytes) section 4, Rtrigger]
```

Section 1 and 2 are used to calibrate the joysticks, sections 3 and 4 are for the triggers.
Each contain 2 sub sections of 4 bytes each, one for each axis.


**Calibrating the joysticks:**

Each of the 4 bytes for sections one and two are:
```
[Lower boundary] [Lower deadzone] [Upper deadzone] [Upper boundary]
```

Lower and upper boundary are used to set the minimum and maximum value.
Deadzone mean that everything between those values the controller will report as being 0x80 (centered).

To calibrate, set the values to `[0x00] [0x7F] [0x7F] [0xFF]` 
for all the 4 axis then tweak both boundaries until the joystick properly goes from 0x00 to 0xFF 
instead of values in between.

Note that the second axis boundaries are inverted meaning that if your second axis goes from (for example) 0x13 to 0xFF, 
you need to lower the upper boundary, not the lower.

Make sure that the value is 0x80 when the controller is centered.


**Calibrating the triggers:**

Each of the 3 bytes for section 3 and 4 are:
```
[Lower boundary] [???] [Upper boundary]
```

It is uncertain what the middle `???` byte does, changing its value doesn't seem to do anything. 
Setting it at 0xFF seems to works.

to calibrate, set the values to `[0x00][0x??][0xFF]` for both triggers then tweak both values.


#### Stop/Resume input (length 2?)

Stops all input (except for the mi button).
```
[byte (0x23)][byte 0x01]
```

You can resume input by sending:

```
[byte (0x23)][byte 0x00]
```


#### Unpair (length 2?)

Unpair and turn off the controller.
```
[byte (0x24)][byte 0x??]
```


#### Accelerometer (length 3)

Note that for some reason ```hidraw ioctl(fd, HIDIOCSFEATURE(3), enable_accel)``` returns a fail for this packet 
while it returns success for all the other packets.
Don't let that fool you because it actually does enable the accelerometer. 
This is one of two packets seen in `sensors.gxbaby.so`

**Change the accelerometer sensitivity (length 3)**
```
[byte (0x31)] [byte 0x01] [byte (0x00 - 0xFF) Sensitivity]
```
the lower the sensitivity value, the less you have to move the controller before it sends a report


**Disable accelerometer (length 3)**
```
[byte (0x31)][byte 0x00][byte 0x00]
```


#### ??? (length 21)
```

[byte (0x04)][???]
```

this was the another packet sent with `ioctl(fd, HIDIOCSFEATURE(21), );` in `sensors.gxbaby.so`. 
It looks like the input packet. More tests need to be done to determine its function.


Output packets:
----

there is one packet that worked with hidraw: `write(fd, rumble, 3);`. 
It returns the rumble packet (0x20) of 3 bytes. 
One thing that is different from the Set Feature report version is that `[0x20] [0x00] [0x00]` does not stop the rumble completely, 
`[0x20][0x01][0x01]` pretty much stops it but you can still hear it a bit if you listen very closely. 
It is better to use this way of sending the rumble packet because "Set feature" 
stops the input from the controller due to how HID stuff works.


Input packets:
----

#### Input (length 21)
```
[byte (0x04)] 
[byte (1 bit per button)] 
[byte (1 bit per button)] 
[byte 0] 
[byte dpad] 
[4 bytes = 4 joystick axis, 1 byte each axis]
[byte 0]
[byte 0]
[byte Ltrigger]
[byte Rtrigger]
[6 bytes accelerometer (2 bytes per axis, looks like signed little endian)]
[byte battery level]
[byte (MI button)]
```



Credits
===
- irugentoo - Original creator of this project and the person that initially reverse engineered the gamepad's protocol