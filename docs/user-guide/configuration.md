# Configuration

Open **Dashboard → Plugins → Hyperion Grabber**. Settings are saved with **Save**; the **Test connection** and
**Send test pattern** buttons use the values currently in the form, so you can try them before saving.

## Status

The **Status** section at the top of the page shows what the lights are doing, updated every few seconds while the
page is open. With [several Hyperion servers](#more-tvs) each one gets its own entry with these lines:

| Line | Meaning |
| --- | --- |
| First line | What happens now: waiting for playback, starting, streaming, paused, the lights released after a long pause, reconnecting to Hyperion, or why the lights do not follow the current playback. If **Follow playback** is off or no device is selected, it says so. |
| ✖ … | What keeps the lights from following playback right now, for example `Hyperion is unreachable: Could not connect …`. |
| Playing | The video and the device that drive the lights. |
| Position | Where the plugin thinks the playback is, and how far off that can be (±). With the Jellyfin add-on for Kodi the ± shrinks with every pause, resume and seek. At another [playback speed](#playback-speed) it adds, for example, `at 1.5× speed`. |
| Pictures | Pictures sent to Hyperion per second compared to the frame rate you set, how many were sent and dropped, and how often decoding restarted (seeks). Many drops or a rate well below the setting mean the server, the network or Hyperion cannot keep up. |
| Timing | Where the position comes from: **Kodi's clock** when the plugin reads it from Kodi, otherwise the client's reports to Jellyfin (with the reason if Kodi's clock cannot be used). |
| Light timing offset | The offset this playback uses, for example `120 ms earlier, set for Living room TV` or `0 ms, the general setting`. See [Calibrating the timing](#calibrating-the-timing). |
| Decoder | The hardware decoder in use, or **CPU**. *Hardware decoding failed* means Jellyfin's hardware acceleration did not work for this video; see the log for FFmpeg's message. |
| Picture | The size of the picture sent to Hyperion, and for HDR video how it becomes SDR, for example `160×90, HDR10, tone mapped to SDR with zscale`. See [HDR and Dolby Vision](#hdr-and-dolby-vision). |
| Last problem | The most recent problem with its time, also after it was resolved (for example a Hyperion restart during the last movie). |

**Show live preview** under the status draws the picture the plugin sends to each Hyperion server, about four times a
second, while a video plays: the small picture Hyperion turns into LED colors with its layout and calibration. It is
handy to check colors, black bars and HDR tone mapping, and to tell whether a problem is in the picture or in Hyperion.
**Hide live preview** (or leaving the page) stops it.

## Hyperion server

| Setting | Default | Description |
| --- | --- | --- |
| **Name (optional)** | *(empty)* | Shown in the status when you have [several Hyperion servers](#more-tvs), for example `Living room`. |
| **Host** | *(empty)* | Host name or IP address of the machine running Hyperion.ng or HyperHDR. See [Docker notes](installation.md#docker-notes) if Jellyfin runs in a container. |
| **FlatBuffers port** | `19400` | Port of Hyperion's FlatBuffers server. Not the web UI port (8090) and not the JSON-RPC port (19444). |
| **Priority** | `150` | Hyperion priority of the Jellyfin picture, 100-199. Lower numbers win. |

### Choosing a priority

Hyperion shows the source with the **lowest** priority number that is active. Typical values:

| Source | Usual priority |
| --- | --- |
| Colors/effects set manually in the Hyperion UI or apps | 1 - 50 |
| **Jellyfin (this plugin)** | **150** |
| Hyperion's own USB/screen grabbers | 240 - 250 |
| Background effect | 254 |

With the default, starting a movie overrides grabbers and the background effect, while a color you pick in the
Hyperion app still wins. Hyperion.ng only accepts 100-199 from FlatBuffers clients.

## Playback

Choose which playback drives the lights. Nothing happens until you turn on **Follow playback** and select at least
one device.

While a matching video plays, the plugin decodes it on the server and sends the picture to Hyperion. Pausing holds
the last picture; after a longer pause (15 s by default) the lights go back to Hyperion's default until you resume.
Seeking jumps with the video, and stopping releases the priority so Hyperion falls back to whatever it shows
otherwise. Only videos from your libraries are streamed; see
[what is not supported](troubleshooting.md#the-lights-do-not-follow-a-video).

### Kodi's clock

Jellyfin for Kodi tells Jellyfin the position only in whole seconds and, while playing, only every few minutes. For
exact timing the plugin reads the position from Kodi itself over Kodi's remote control interface (JSON-RPC on TCP port
9090), at the address Jellyfin sees for the Kodi device. In Kodi turn on:

**Settings → Services → Control → Allow remote control from applications on other systems**

The plugin only reads where the video is; it never controls Kodi. Kodi's remote control interface has no password,
so with this setting on, any device on your network can control Kodi; that is how Kodi works. The **Timing** line of
the [status](#status) shows whether Kodi's clock is used. Without the setting, or when Kodi cannot be reached (a
firewall, or Jellyfin seeing a proxy instead of the Kodi device), the plugin uses Jellyfin's reports as before.

### Playback speed

The lights follow playback speeds from 0.25× to 2×, for example a talk at 1.5× in the web player. There is nothing to
set:

- **Kodi** (with [Kodi's clock](#kodis-clock)): the plugin reads Kodi's position every 2 seconds and measures the tempo
  from it, so after you change the tempo the lights catch up within about 10 seconds. Fast forward and rewind hold the
  picture; the lights continue where normal playback resumes.
- **Other apps** do not tell Jellyfin their speed. The plugin measures it from their position reports: after you
  change the speed, the lights catch up within about 20 seconds (two reports), with one or two short jumps on the
  way. A seek is never taken for a speed change.

The plugin still sends pictures at the frame rate you set, so at 1.5× a third of the decoded pictures (at 2× half)
show up as dropped in the [status](#status); that is expected.

### HDR and Dolby Vision

LEDs cannot show HDR, so the plugin turns HDR video into SDR colors before sending it (tone mapping). There is nothing
to set up: HDR10, HDR10+, HLG and Dolby Vision, including profile 5 (common in streaming rips, where it otherwise shows
green and purple), are detected from Jellyfin's media info and mapped automatically. Jellyfin's own **Enable tone
mapping** setting under **Dashboard → Playback → Transcoding** only affects transcoding for clients, not the lights.

The plugin uses Jellyfin's FFmpeg, and Jellyfin's bundled FFmpeg (in every official package and Docker image) does
all of this. With a custom FFmpeg build, HDR10 and HLG need zimg (the `zscale` filter), and Dolby Vision profile 5
keeps wrong colors; the **Picture** line of the [status](#status) says so. Tone mapping happens on the small picture
after scaling and adds almost no load.

If Hyperion restarts or the network drops during playback, the plugin reconnects by itself (after 1 s, then up to every
30 s) and the lights continue at the current picture; see
[Hyperion restarted during playback](troubleshooting.md#hyperion-restarted-during-playback).

| Setting | Default | Description |
| --- | --- | --- |
| **Follow playback** | off | Turns playback detection on. |
| **Frame rate** | `25` | Pictures per second sent to Hyperion, 1-60. 25 looks smooth; lower it (for example to 10-15) if the Jellyfin server is short on CPU, especially without hardware decoding. Applies from the next playback. |
| **Read the position from Kodi (precise timing)** | on | For playback in Kodi, the plugin asks Kodi itself where the video is, to the millisecond, instead of relying on Jellyfin's whole-second reports. Needs **Settings → Services → Control → Allow remote control from applications on other systems** in Kodi; without it nothing changes. Other clients are not affected. See [Kodi's clock](#kodis-clock). |
| **Kodi remote control port** | `9090` | Kodi's JSON-RPC TCP port. Applies from the next playback. |
| **Light timing offset (ms)** | `0` | Shifts the lights against the picture, -2000 to 2000. If the lights change **after** the TV picture, increase it; if they change **before**, make it negative. A device can have its own value (below). See [Calibrating the timing](#calibrating-the-timing). Applies from the next playback. |
| **Release the lights after pausing for (seconds)** | `15` | While paused the lights hold the picture; after this time FFmpeg stops and Hyperion shows its default (effect, other source or off) until you resume. `0` holds the picture for the whole pause. |
| **Devices** | none | Devices whose playback drives the lights of the Hyperion server above, usually the TV your LEDs are mounted on. With none selected (here and under [More TVs](#more-tvs)), no playback does. A ticked device shows **Light timing offset for this device (ms)**: leave it empty to use the general offset, or enter this TV's own value. |
| **Users (optional)** | none | Only playback by these users drives the lights. Leave all unticked to react to every user. |

The lists show the devices and users of Jellyfin's recent sessions, most recently active device first. If your TV is
missing, start playing something on it and press **Refresh**. Saved devices stay in the list (marked
*not seen recently*) while they are offline. Jellyfin identifies a device by its app installation, so after
reinstalling the Jellyfin app or add-on on the TV, select the device again.

If two devices selected for the same Hyperion server play at the same time, the one that started most recently drives
the lights; when it stops, the other one takes over. Devices of [other Hyperion servers](#more-tvs) do not affect it.

The picture is decoded with Jellyfin's FFmpeg and the hardware acceleration set under
**Dashboard → Playback → Transcoding**, scaled to 160 pixels wide in the shape the TV shows (anamorphic DVDs
included) and, for HDR video, tone mapped to SDR; see [Installation](installation.md#docker-notes). Black bars of
widescreen films are handled by Hyperion; see [Black bars](hyperion-setup.md#black-bars-widescreen-films).

### Calibrating the timing

The delay between the TV picture and the LEDs depends on the TV (picture processing, game mode), the player and the
LED setup. Measure it once with the timing test video:

1. Download the [timing test video](../assets/calibration/hyperion-grabber-timing-test.mp4) (64 seconds, 0.4 MB) and
   put it in one of your Jellyfin libraries, for example a *Test* folder in a movie library. Scan the library.
2. Play it on the TV. Every 2 seconds the whole screen flashes white, red, green or blue for a moment, with a beep.
   Watch the screen and the wall (or lamps) at the same time.
3. If the wall lights up **after** the screen, raise the offset; if it lights up **before**, lower it. Start in steps of
   100 ms, then 20 ms.
4. Save and play the video again: the offset applies from the next playback. The [status](#status) line
   **Light timing offset** shows the value in use and whether it is the device's own.
5. Stop when screen and wall flash together.

Enter the result as the general **Light timing offset** when all your TVs behave alike. When one TV or player is
slower or faster, tick it under **Devices** and enter its own value there; the other devices keep the general one.
The value belongs to that device on that Hyperion server, so a device ticked for two servers can have a different value
on each.

- Calibrate with Hyperion's smoothing set as you normally watch: smoothing adds a delay that the offset makes up for.
- Keep the frame rate at 10 or more while calibrating, so that no flash falls between two pictures.
- Kodi clients time best with [Kodi's clock](#kodis-clock) on; calibrate after turning it on.

## More TVs

For LEDs on a second TV, press **Add a Hyperion server** under **More TVs**, enter that TV's Hyperion server (name,
host, FlatBuffers port, priority), use its own **Test connection** and **Send test pattern**, and tick the devices
that play on that TV. Up to 8 Hyperion servers in total.

- **Each TV needs its own Hyperion.ng or HyperHDR server**, for example a Raspberry Pi per TV or a second Hyperion
  container on another port; see [Several TVs](hyperion-setup.md#several-tvs). Instances of one server all show the
  same picture, so they cannot serve two TVs.
- Each server follows only its own devices: a movie started in the bedroom does not take the living room's lights.
- A device ticked for two servers drives both (for example a strip on one server and room lamps on another). Each
  costs its own decoder on the Jellyfin server.
- **Users (optional)**, frame rate, the general light timing offset, pause release and Kodi's clock apply to every
  server. A device's own timing offset is set per server, under that server's devices.
- Two servers may not use the same host, port and priority; the page says so when you save.
- With several servers the [status](#status) shows one entry per server, with its name.

Changing a server's host, port or priority while it plays restarts its lights at once; other changes apply from the
next playback.
