using System;

namespace Jellyfin.Plugin.HyperionGrabber.Core.Playback;

/// <summary>
/// Estimates the playback position from a client's position reports, keeping track of how precise the estimate is.
/// </summary>
/// <remarks>
/// <para>Clients report rarely, and some imprecisely: Jellyfin for Kodi truncates the position to whole seconds and
/// reports it on pause, resume and seek and otherwise about every 4 minutes. Moving the estimate to every report would
/// shift the lights by up to a second each time. Instead the tracker keeps the range the real position can be in:</para>
/// <list type="bullet">
/// <item><description>A start report gives a range of ± <see cref="StartUncertainty"/>: it carries the requested start
/// position, and clients send it at different moments around the first picture.</description></item>
/// <item><description>Any other report gives its own precision: a whole-second position (above 0) is taken as
/// truncated, so the real one lies within the following second; a millisecond position is taken as it is, but while
/// playing it may be up to <see cref="MaxReportAge"/> old (the web client sends the position of its last update).
/// Each gets ± <see cref="ReportTolerance"/> for the time the report took to arrive.</description></item>
/// <item><description>A report that agrees with the range narrows it; one that contradicts it (a seek, or the clocks
/// drifted apart) replaces it.</description></item>
/// <item><description>While playing, the range moves with time and widens by <see cref="DriftRate"/> of the elapsed
/// time on each side, because the client's and the server's clocks do not run at exactly the same speed.</description></item>
/// <item><description>It moves at the playback speed (<see cref="Rate"/>). Jellyfin does not report a speed, and Kodi's
/// clock reports only pause, fast forward and rewind, not a tempo such as 1.5. So the tracker measures the speed between
/// positions of one stretch of playing (a player's clock readings, otherwise the reports) and takes a new speed once two
/// measurements in a row agree, so a seek never changes it. Fast forward and rewind on the player's clock hold the
/// estimate; normal playback resumes as a seek.</description></item>
/// </list>
/// <para>The estimate is the middle of the range, so every report that agrees makes it more precise instead of
/// resetting it. Not thread-safe: a tracker belongs to one session's task.</para>
/// </remarks>
internal sealed class PositionTracker
{
    /// <summary>How much faster or slower than the server's clock a client may play (0.1 %).</summary>
    internal const double DriftRate = 0.001;

    /// <summary>The slowest playback speed the lights follow.</summary>
    internal const double MinFollowedRate = 0.25;

    /// <summary>The fastest playback speed the lights follow; faster (fast forward) or backwards (rewind) holds the
    /// picture until normal playback resumes.</summary>
    internal const double MaxFollowedRate = 2;

    /// <summary>How far a measured speed must be from the current one to count as a change.</summary>
    internal const double RateChangeThreshold = 0.08;

    /// <summary>How close two measured speeds must be to confirm each other.</summary>
    internal const double RateAgreement = 0.05;

    /// <summary>The speeds of Jellyfin's apps are quarter steps (0.75, 1.25, 1.5, ...); a speed measured from reports is
    /// rounded to the nearest one when close.</summary>
    internal const double ReportRateStep = 0.25;

    /// <summary>Kodi's tempos are tenth steps (0.8 to 1.5); a tempo measured from its clock is rounded to the nearest one
    /// when close.</summary>
    internal const double ClockTempoStep = 0.1;

    /// <summary>How far the real position may be from a start report.</summary>
    internal static readonly TimeSpan StartUncertainty = TimeSpan.FromSeconds(1);

    /// <summary>How far the real position may be from any other report, beyond its precision.</summary>
    internal static readonly TimeSpan ReportTolerance = TimeSpan.FromMilliseconds(100);

    /// <summary>The precision of a whole-second position: the real one may be up to this much later.</summary>
    internal static readonly TimeSpan TruncatedPrecision = TimeSpan.FromSeconds(1);

    /// <summary>How old a millisecond position reported while playing may be (the web client's update interval).</summary>
    internal static readonly TimeSpan MaxReportAge = TimeSpan.FromMilliseconds(250);

    /// <summary>How far apart two millisecond reports must be to measure the speed from them.</summary>
    internal static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(5);

    /// <summary>How far apart reports must be when one is truncated to whole seconds (up to a second off).</summary>
    internal static readonly TimeSpan TruncatedRateWindow = TimeSpan.FromSeconds(30);

    /// <summary>How far apart two readings of a player's clock (milliseconds, a few ms off) must be to measure the tempo
    /// from them; Kodi's clock is read every 2 s.</summary>
    internal static readonly TimeSpan ClockRateWindow = TimeSpan.FromSeconds(3);

    // The real position at _at lies between _earliest and _latest.
    private TimeSpan _earliest;
    private TimeSpan _latest;
    private DateTimeOffset _at;
    private bool _paused;

    // Playback speed: 1 unless two measurements in a row agree on another one, or the player's clock shows 2x.
    private double _rate = 1;
    private double _tempo = 1; // The speed the clock's readings measured while the player says it plays normally.
    private bool _scanning; // The player's clock shows fast forward or rewind: hold the position.
    private bool _rateFromClock; // Once the clock is read, its readings measure the speed, no longer the reports.
    private RatePoint? _rateAnchor;
    private double? _rateCandidate;

    /// <summary>Initializes a new instance of the <see cref="PositionTracker"/> class.</summary>
    /// <param name="first">The first known state of the playback: usually the start report, but a session can also
    /// begin from a later report (Jellyfin restarted during playback, the device filter changed).</param>
    public PositionTracker(PlaybackState first)
    {
        ArgumentNullException.ThrowIfNull(first);
        _at = first.ReportedAt;
        _paused = first.IsPaused;
        (_earliest, _latest) = first.IsStart || !first.IsPositionReported
            ? StartRange(first.Position)
            : ReportRange(first.Position, first.IsPaused);
        _rateAnchor = first.IsPaused || !first.IsPositionReported ? null : ReportPoint(first);
    }

    /// <summary>Gets half the width of the range: how far the estimate may be off at most.</summary>
    public TimeSpan Uncertainty => TimeSpan.FromTicks((_latest.Ticks - _earliest.Ticks) / 2);

    /// <summary>Gets the playback speed the estimate moves at while playing (1 = normal).</summary>
    public double Rate => _rate;

    /// <summary>Estimates the position at a time: the middle of the range, moved on by the time since (at
    /// <see cref="Rate"/>) while playing.</summary>
    /// <param name="now">The time to estimate for.</param>
    /// <returns>The position; may be negative just before the start.</returns>
    public TimeSpan Estimate(DateTimeOffset now)
    {
        var middle = TimeSpan.FromTicks(_earliest.Ticks + ((_latest.Ticks - _earliest.Ticks) / 2));
        return _paused || now <= _at ? middle : middle + ((now - _at) * _rate);
    }

    /// <summary>Takes a new report into account.</summary>
    /// <param name="report">The playback state from the report.</param>
    /// <returns>How far the report moved the estimate; more than a second means a seek.</returns>
    public TimeSpan Apply(PlaybackState report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var at = report.ReportedAt > _at ? report.ReportedAt : _at;
        var before = Estimate(at);
        MoveTo(at);
        if (report.IsStart)
        {
            (_rate, _tempo, _scanning, _rateFromClock) = (1, 1, false, false);
        }

        // While the player's clock shows a scan, reported positions jump; the clock ends the scan.
        if (report.IsPositionReported && !_scanning)
        {
            // Reports arrive in order; one that is older than the range still describes a playing position later on.
            var position = report.Position;
            if (!report.IsPaused && at > report.ReportedAt)
            {
                position += (at - report.ReportedAt) * _rate;
            }

            if (report.IsStart)
            {
                // A new start: the old range says nothing about it.
                (_earliest, _latest) = StartRange(position);
            }
            else
            {
                Narrow(ReportRange(position, report.IsPaused));
            }

            if (!_rateFromClock)
            {
                MeasureReportRate(report);
            }
        }

        _paused = report.IsPaused || _scanning;
        return Estimate(at) - before;
    }

    /// <summary>Takes a reading of the player's own clock into account: a precise report, ± its uncertainty.</summary>
    /// <param name="reading">The reading.</param>
    /// <returns>How far the reading moved the estimate; more than a second means a seek.</returns>
    public TimeSpan Apply(ClockReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        var at = reading.At > _at ? reading.At : _at;
        var before = Estimate(at);
        MoveTo(at);

        if (!_rateFromClock)
        {
            // The clock's readings are more precise than the reports: from now on they measure the speed.
            (_rateFromClock, _rateAnchor, _rateCandidate) = (true, null, null);
        }

        // Kodi's speed is 1 also at another tempo, 2 or more for fast forward, below 0 for rewind (0 is a pause).
        var normal = !reading.IsPaused && reading.Speed == 1;
        _scanning = !reading.IsPaused && reading.Speed is < MinFollowedRate or > MaxFollowedRate;
        if (!normal)
        {
            // A pause, 2x fast forward or a scan ends the stretch the tempo is measured on.
            (_rateAnchor, _rateCandidate) = (null, null);
        }

        if (!_scanning)
        {
            // While scanning, positions jump: the estimate holds until normal playback resumes there (a seek).
            if (!reading.IsPaused)
            {
                _rate = normal ? _tempo : reading.Speed;
            }

            var position = reading.IsPaused || at <= reading.At ? reading.Position : reading.Position + ((at - reading.At) * _rate);
            Narrow((position - reading.Uncertainty, position + reading.Uncertainty));
        }

        if (normal)
        {
            var point = new RatePoint(reading.Position, reading.At, reading.Uncertainty, ClockRateWindow, ClockTempoStep);
            if (_rateAnchor is null || _paused)
            {
                (_rateAnchor, _rateCandidate) = (point, null);
            }
            else if (Measure(point) is { } tempo)
            {
                (_rate, _tempo) = (tempo, tempo);
            }
        }

        _paused = reading.IsPaused || _scanning;
        return Estimate(at) - before;
    }

    /// <summary>Rounds a measured speed to the nearest step when it is close to it, otherwise to hundredths.</summary>
    private static double Snap(double rate, double step)
    {
        var nearest = Math.Round(rate / step) * step;
        return Math.Abs(rate - nearest) <= Math.Min(RateAgreement, step / 4) ? Math.Round(nearest, 2) : Math.Round(rate, 2);
    }

    /// <summary>A report as a point to measure the speed from, with its precision.</summary>
    private static RatePoint ReportPoint(PlaybackState report)
    {
        // 0 is not taken as truncated: every client reports the very start as 0.
        var truncated = report.Position > TimeSpan.Zero && report.Position.Ticks % TimeSpan.TicksPerSecond == 0;
        var precision = report.IsStart ? StartUncertainty
            : (truncated ? TruncatedPrecision : MaxReportAge) + ReportTolerance;
        return new RatePoint(report.Position, report.ReportedAt, precision, truncated ? TruncatedRateWindow : RateWindow, ReportRateStep);
    }

    private static (TimeSpan Earliest, TimeSpan Latest) StartRange(TimeSpan position)
        => (position - StartUncertainty, position + StartUncertainty);

    private static (TimeSpan Earliest, TimeSpan Latest) ReportRange(TimeSpan position, bool paused)
    {
        // 0 is not taken as truncated: every client reports the very start as 0.
        var precision = position > TimeSpan.Zero && position.Ticks % TimeSpan.TicksPerSecond == 0
            ? TruncatedPrecision
            : paused ? TimeSpan.Zero : MaxReportAge;
        return (position - ReportTolerance, position + precision + ReportTolerance);
    }

    private void MoveTo(DateTimeOffset at)
    {
        if (!_paused && at > _at)
        {
            var elapsed = at - _at;
            var moved = elapsed * _rate;
            var drift = elapsed * DriftRate;
            _earliest += moved - drift;
            _latest += moved + drift;
        }

        _at = at;
    }

    /// <summary>Measures the speed from a report, when no player's clock is read.</summary>
    private void MeasureReportRate(PlaybackState report)
    {
        if (report.IsStart || report.IsPaused || _paused || _rateAnchor is null)
        {
            // Only reports of one stretch of playing measure the speed.
            (_rateAnchor, _rateCandidate) = (report.IsPaused ? null : ReportPoint(report), null);
            return;
        }

        if (Measure(ReportPoint(report)) is { } rate)
        {
            _rate = rate;
        }
    }

    /// <summary>
    /// Measures the speed between the anchor and a later point of the same stretch of playing. Players do not tell their
    /// speed, so a new one is taken only when two measurements in a row agree on it; a seek measures an impossible
    /// speed and is ignored.
    /// </summary>
    /// <returns>The new speed once two measurements agree on it, otherwise <see langword="null"/>.</returns>
    private double? Measure(RatePoint point)
    {
        var anchor = _rateAnchor!.Value;
        var elapsed = point.At - anchor.At;
        if (elapsed <= TimeSpan.Zero)
        {
            return null;
        }

        var moved = point.Position - anchor.Position;
        var error = point.Precision + anchor.Precision;
        if ((moved - error) / elapsed > MaxFollowedRate + RateChangeThreshold || (moved + error) / elapsed < MinFollowedRate)
        {
            // Even allowing for both positions' precision no followed speed explains it: a seek, usually reported as it
            // happens. Measuring starts over from here, so the seek is not part of the next measurement.
            (_rateAnchor, _rateCandidate) = (point, null);
            return null;
        }

        if (elapsed < (anchor.Window > point.Window ? anchor.Window : point.Window))
        {
            return null; // Too close to measure; the next point measures from the same anchor.
        }

        _rateAnchor = point;
        var measured = moved / elapsed;
        if (measured is < MinFollowedRate or > MaxFollowedRate + RateChangeThreshold)
        {
            _rateCandidate = null; // A seek.
            return null;
        }

        var rate = Snap(measured, point.Step);
        if (Math.Abs(rate - _rate) <= RateChangeThreshold)
        {
            _rateCandidate = null;
            return null;
        }

        if (_rateCandidate is { } candidate && Math.Abs(candidate - rate) <= RateAgreement)
        {
            _rateCandidate = null;
            return rate;
        }

        _rateCandidate = rate;
        return null;
    }

    private void Narrow((TimeSpan Earliest, TimeSpan Latest) report)
    {
        if (report.Earliest > _latest || report.Latest < _earliest)
        {
            (_earliest, _latest) = report;
            return;
        }

        if (report.Earliest > _earliest)
        {
            _earliest = report.Earliest;
        }

        if (report.Latest < _latest)
        {
            _latest = report.Latest;
        }
    }

    /// <summary>A position of one stretch of playing, to measure the speed from.</summary>
    /// <param name="Position">The position.</param>
    /// <param name="At">When the player was there.</param>
    /// <param name="Precision">How far the real position may be from <paramref name="Position"/>.</param>
    /// <param name="Window">How far apart two points must be to measure the speed from them.</param>
    /// <param name="Step">The step a measured speed is rounded to when close.</param>
    private readonly record struct RatePoint(TimeSpan Position, DateTimeOffset At, TimeSpan Precision, TimeSpan Window, double Step);
}
