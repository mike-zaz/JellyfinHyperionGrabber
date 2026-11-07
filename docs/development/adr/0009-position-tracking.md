# 0009: Track the playback position as a range that every report narrows

**Status:** Accepted (2026-10-07)

## Context

The lights can only be as accurate as the plugin's idea of where the client is. Clients report their position to
Jellyfin now and then, and very differently ([Jellyfin integration](../jellyfin-integration.md#playback-events-input-for-m1m2)):
most apps report milliseconds every 3-10 s, but Jellyfin for Kodi truncates the position to **whole seconds** and,
during playback, reports only about **every 4 minutes** (plus pause, resume and seek). Its start report carries the
requested start position rather than the player's clock.

Until 0.3 the session moved its estimate to every report (`last report + elapsed`). With Kodi each report put the
estimate 0-1 s behind the real position, by a different amount each time, so the lights' delay changed after every
pause, resume or periodic report. The first real test (Kodi on LibreELEC, WLED) showed about 1 s of delay that a
constant light timing offset could not remove.

Options considered:

- **Re-anchor on every report** (status quo): exact for precise clients, up to 1 s late and jumping for Kodi.
- **Add half a second to whole-second reports:** right on average, but every report still moves the lights by up to
  half a second.
- **Read Kodi's clock over JSON-RPC:** precise, but only for Kodi, needs a Kodi setting and a second connection.
  Still possible later as an optional source; it does not help any other client.
- **Keep the range the real position can be in and narrow it with every report.**

## Decision

A `PositionTracker` (Core) per session keeps the range `[earliest, latest]` of the real position at a point in time:

- A start report gives ± 1 s: it carries the requested start position, and clients send it at different moments
  around the first picture. A session can also begin from a later report (Jellyfin restarted during playback, the
  device filter changed); that one gets its usual precision, so `PlaybackState.IsStart` marks start reports.
- Any other report gives its own precision, plus ± 100 ms for the time it took to arrive:
    - a whole-second position above 0 is taken as truncated and covers the following second; Kodi reads its clock
      when it reports (0 is not taken as truncated: every client reports the very start as 0);
    - a millisecond position reported while playing may be up to 250 ms old (the web client sends the position of its
      last `timeupdate`), so its range reaches 250 ms further;
    - a millisecond position reported while paused is taken as it is: a paused position cannot be old.
- A report that overlaps the range narrows it to the overlap; one that does not (a seek, or clocks that drifted apart)
  replaces it.
- While playing the range moves on with time and widens by 0.1 % of the elapsed time on each side (the client may play
  slightly faster or slower than the server's clock, for example 24 Hz output of 23.976 fps video). While paused it
  stays still.
- The estimate is the middle of the range. A report that moves the estimate by more than 1 s is a seek (decoding
  restarts there); the light timing offset is added on top, as before.

Each report is logged at Debug with how far it moved the estimate and the remaining uncertainty.

## Consequences

- Precise clients: a report while playing narrows the range to 0.1 s before to 0.35 s after it, and reports of
  different ages narrow it further, so the web client's 0-250 ms report age averages out instead of
  making the lights jitter. A client whose playing reports are fresh (Android TV) is estimated about 0.1 s early, a
  constant that the light timing offset absorbs.
- Jellyfin for Kodi no longer jumps up to a second late at each report. After a truncated report the estimate is the
  middle of that second (at most about half a second off), and every further report at another point of a second
  narrows it: in a test with four Kodi-like reports the error went from up to 0.95 s (re-anchoring) to 0.01 s. A
  truncated pause or resume report that agrees with a precise estimate leaves it untouched.
- Right after the start the range is ± 1 s, and with Kodi it stays that wide until a pause, seek or periodic report
  narrows it (at most about half a second off after the first). Reading Kodi's own clock
  ([ADR 0011](0011-kodi-clock.md)) gives exact sync from the first second when Kodi allows remote control. A session that begins from a mid-playback Kodi report starts from the middle
  of that report's second.
- A precise client that happens to report an exact whole second gets a wider range than needed; the overlap with what
  is already known keeps the estimate, so this only costs precision right after a seek.
- A client that *rounds* to whole seconds instead of truncating would get estimates up to about half a second early
  (about as far off as re-anchoring would be); none of the checked clients rounds. If one turns up, its reports need
  their own precision rule.
- The rules use only the reports' values and arrival times, so they work for any client, including ones not checked
  yet; clients that report more often converge faster.

## Amendment: playback speed (2026-11-07)

The range used to move at 1× while playing. Jellyfin has no playback speed in `PlayerStateInfo` or
`PlaybackProgressInfo` (checked in 10.11.0 and 12.0.0), so at 1.5× in the web player every report contradicted the
range and restarted decoding.

**Decision.** The range moves at a rate, 1 by default, measured from positions of one stretch of playing:

- **Measuring:** the speed between two positions at least a window apart. A new rate needs two measurements in a row
  that agree within 0.05 and differ from the current one by more than 0.08. A measurement outside 0.25-2.08 is a seek
  and resets the candidate. A position that no followed speed can explain even before the window is over (allowing
  for both positions' precision) is a seek too, usually the client's report of it, and the measurement starts over
  from there, so a seek is not measured as part of the next speed. Pause, resume and a start begin a new stretch; a
  start also resets the rate to 1.
- **Player's clock (Kodi):** Kodi's `speed` is a whole number (`(int)lrint(GetPlaySpeed())` in
  `xbmc/interfaces/json-rpc/PlayerOperations.cpp`), and its tempo (0.8-1.5, `GetPlayTempo()`) cannot be read: at a
  tempo of 1.5 it says 1. So the clock gives pause (0), 2× fast forward (2, followed) and scanning (above 2 or below 0:
  the estimate and reported positions are held until a reading shows normal playback, which then moves the range like
  a seek). At 1 the tempo is measured from the readings themselves (milliseconds, every 2 s): 3 s window, rounded to
  Kodi's tenth steps when within 0.025. Once a clock reading was applied, reports no longer measure the speed.
- **Reports only:** 5 s window, 30 s if either report is truncated to whole seconds (up to a second off); a
  measurement near a quarter step (0.75, 1.25, 1.5, ...) snaps to it, as Jellyfin's apps offer those.

**Consequences.** Kodi with its clock follows a tempo change after about 8-12 s (two agreeing measurements of two
readings each), with one or two jumps on the way. Other clients catch up after two reports (about 20 s for the
web client), and the one or two reports before that still look like seeks. Two equal skips in a row that the client
does not report as they happen, each measured as one speed (for example +5 s every 10 s), would be taken for 1.5×
until the next two measurements; that is unlikely when watching. The jitter of the web client's 0-250 ms report age is
about 2.5 % over 10 s, well inside the 8 % threshold. At 1.5× or 2× the session still sends at the configured frame
rate, so a third or half of the decoded pictures count as dropped.
