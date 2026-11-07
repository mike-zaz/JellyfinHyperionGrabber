using System;
using Jellyfin.Plugin.HyperionGrabber.Core.Playback;
using Xunit;

namespace Jellyfin.Plugin.HyperionGrabber.Core.Tests.Playback;

/// <summary>
/// Position estimates from reports of different precision. Times and positions are in seconds.
/// </summary>
public class PositionTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 6, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_EstimateIsTheStartPositionPlusTheTimeSince()
    {
        var tracker = new PositionTracker(Start(60, at: 0));

        AssertNear(62.5, tracker.Estimate(At(2.5)));
        Assert.Equal(PositionTracker.StartUncertainty, tracker.Uncertainty);
    }

    [Fact]
    public void Start_WhilePaused_StaysAtTheStartPosition()
    {
        var tracker = new PositionTracker(Start(60, at: 0, paused: true));

        AssertNear(60, tracker.Estimate(At(30)));
    }

    [Fact]
    public void FirstStateFromAWholeSecondProgressReport_UsesItsPrecision()
    {
        // Jellyfin restarted while Kodi kept playing: the session begins from Kodi's next (truncated) report.
        var tracker = new PositionTracker(Report(600, at: 0));

        AssertNear(600.5, tracker.Estimate(At(0)));
        AssertNear(0.6, tracker.Uncertainty);
    }

    [Fact]
    public void PreciseReport_WhilePaused_IsTakenAsItIs()
    {
        var tracker = new PositionTracker(Start(0, at: 0));

        var moved = tracker.Apply(Report(10.3, at: 10, paused: true));

        AssertNear(10.3, tracker.Estimate(At(20)));
        AssertNear(0.3, moved);
        AssertNear(0.1, tracker.Uncertainty);
    }

    [Fact]
    public void PreciseReport_WhilePlaying_MayBeUpToAQuarterSecondOld()
    {
        var tracker = new PositionTracker(Start(0, at: 0));

        tracker.Apply(Report(10.3, at: 10));

        AssertNear(10.425, tracker.Estimate(At(10))); // The middle of 10.2-10.65 s.
        AssertNear(0.225, tracker.Uncertainty);
    }

    [Fact]
    public void PreciseReportsWithDifferentAges_NarrowTheRange()
    {
        // The web client reports every 10 s the position of its last update, taken 0-250 ms before sending.
        static double Real(double at) => at + 0.5;
        var tracker = new PositionTracker(Start(0.5, at: 0));

        tracker.Apply(Report(Real(10) - 0.2, at: 10)); // Alone: 75 ms off, ±225 ms.
        tracker.Apply(Report(Real(20), at: 20)); // Alone: 125 ms off, ±225 ms.

        AssertNear(Real(20), tracker.Estimate(At(20)), toleranceMs: 40);
        Assert.InRange(tracker.Uncertainty, TimeSpan.Zero, TimeSpan.FromMilliseconds(150));
    }

    [Fact]
    public void WholeSecondReport_AfterASeek_EstimatesTheMiddleOfThatSecond()
    {
        var tracker = new PositionTracker(Start(0, at: 0));

        tracker.Apply(Report(600, at: 10)); // Jellyfin for Kodi truncates: the real position is between 600 and 601 s.

        AssertNear(600.5, tracker.Estimate(At(10)));
        AssertNear(0.6, tracker.Uncertainty);
    }

    [Fact]
    public void TruncatedReports_AtDifferentMoments_ConvergeOnTheRealPosition()
    {
        // The client really is 0.7 s further than its start report said, and truncates every later report.
        static double Real(double at) => at + 0.7;
        var tracker = new PositionTracker(Start(0, at: 0));

        foreach (var at in new[] { 10, 41.5, 73.25, 100.4 })
        {
            tracker.Apply(Report(Math.Floor(Real(at)), at));

            var error = Real(at) - tracker.Estimate(At(at)).TotalSeconds;
            Assert.InRange(error, -tracker.Uncertainty.TotalSeconds, tracker.Uncertainty.TotalSeconds);
        }

        // Moving the estimate to the last report would be 0.1 s late here, and up to a second after the others.
        AssertNear(Real(100.4), tracker.Estimate(At(100.4)), toleranceMs: 30);
        Assert.InRange(tracker.Uncertainty, TimeSpan.Zero, TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void TruncatedPauseAndResumeReports_KeepThePreciseEstimate()
    {
        var tracker = new PositionTracker(Start(0, at: 0));
        tracker.Apply(Report(40.6, at: 40, paused: true));
        tracker.Apply(Report(40.6, at: 41));

        tracker.Apply(Report(50, at: 51, paused: true)); // Paused at 50.6 s, reported truncated.
        AssertNear(50.6, tracker.Estimate(At(80)), toleranceMs: 15);
        var moved = tracker.Apply(Report(50, at: 80)); // Resumed, reported truncated again.

        AssertNear(0, moved, toleranceMs: 15);
        AssertNear(51.6, tracker.Estimate(At(81)), toleranceMs: 15);
    }

    [Fact]
    public void ContradictingReport_ReplacesTheEstimate()
    {
        var tracker = new PositionTracker(Start(0, at: 0));
        tracker.Apply(Report(30.25, at: 30, paused: true));

        var moved = tracker.Apply(Report(5.5, at: 40, paused: true)); // The user jumped back.

        AssertNear(5.5, tracker.Estimate(At(40)));
        AssertNear(-24.75, moved);
    }

    [Fact]
    public void NewStartReport_StartsOver()
    {
        var tracker = new PositionTracker(Start(0, at: 0));
        tracker.Apply(Report(10.3, at: 10, paused: true));

        tracker.Apply(Start(20, at: 30));

        AssertNear(20, tracker.Estimate(At(30)));
        Assert.Equal(PositionTracker.StartUncertainty, tracker.Uncertainty);
    }

    [Fact]
    public void ReportWithoutPosition_OnlyChangesThePause()
    {
        var tracker = new PositionTracker(Start(0, at: 0));
        tracker.Apply(Report(20.5, at: 20, paused: true));
        tracker.Apply(Report(20.5, at: 20));

        tracker.Apply(Report(0, at: 30, paused: true) with { IsPositionReported = false });

        AssertNear(30.5, tracker.Estimate(At(60)));
    }

    [Fact]
    public void Uncertainty_GrowsWhilePlaying_NotWhilePaused()
    {
        var playing = new PositionTracker(Start(60, at: 0));
        var paused = new PositionTracker(Start(60, at: 0, paused: true));

        playing.Apply(Report(0, at: 100) with { IsPositionReported = false });
        paused.Apply(Report(0, at: 100, paused: true) with { IsPositionReported = false });

        AssertNear(1.1, playing.Uncertainty); // ±1 s from the start report plus 0.1 % of 100 s.
        AssertNear(1, paused.Uncertainty);
    }

    [Fact]
    public void ReportAtZero_IsNotTakenAsTruncated()
    {
        var tracker = new PositionTracker(Start(0, at: 0, paused: true)); // Opening, before the first picture.

        tracker.Apply(Report(0, at: 2, paused: true));

        AssertNear(0, tracker.Estimate(At(2)));
        AssertNear(0.1, tracker.Uncertainty);
    }

    [Fact]
    public void ReportOlderThanTheEstimate_IsMovedOnToItsTime()
    {
        var tracker = new PositionTracker(Start(0, at: 10));

        tracker.Apply(Report(8.5, at: 9)); // 8.5 s at 9 s is 9.5 s at 10 s (and may be up to 250 ms old).

        AssertNear(9.625, tracker.Estimate(At(10)));
    }

    [Fact]
    public void ClockReading_NarrowsTheRangeToItsUncertainty()
    {
        var tracker = new PositionTracker(Start(0, at: 0));

        var moved = tracker.Apply(new ClockReading(TimeSpan.FromSeconds(10.6), IsPaused: false, At(10), TimeSpan.FromMilliseconds(20)));

        AssertNear(10.6, tracker.Estimate(At(10)));
        AssertNear(0.6, moved);
        AssertNear(0.02, tracker.Uncertainty);
    }

    [Fact]
    public void ClockReading_AndAgreeingTruncatedReports_KeepThePreciseEstimate()
    {
        var tracker = new PositionTracker(Start(0, at: 0));
        tracker.Apply(new ClockReading(TimeSpan.FromSeconds(10.6), IsPaused: false, At(10), TimeSpan.FromMilliseconds(20)));

        tracker.Apply(Report(20, at: 20)); // Jellyfin for Kodi: 20.6 s truncated.

        AssertNear(20.6, tracker.Estimate(At(20)));
        Assert.InRange(tracker.Uncertainty, TimeSpan.Zero, TimeSpan.FromMilliseconds(40));
    }

    [Fact]
    public void ClockReading_WhilePaused_StopsTheEstimate()
    {
        var tracker = new PositionTracker(Start(0, at: 0));

        tracker.Apply(new ClockReading(TimeSpan.FromSeconds(42.25), IsPaused: true, At(45), TimeSpan.FromMilliseconds(20)));

        AssertNear(42.25, tracker.Estimate(At(90)));
    }

    [Fact]
    public void ClockReading_AtATempo_MovesTheEstimateAtThatSpeed()
    {
        var tracker = new PositionTracker(Start(0, at: 0));

        tracker.Apply(Clock(10, at: 10, speed: 1.5));

        Assert.Equal(1.5, tracker.Rate);
        AssertNear(25, tracker.Estimate(At(20)));
    }

    [Theory]
    [InlineData(8)] // Fast forward.
    [InlineData(-2)] // Rewind.
    public void ClockReading_WhileScanning_HoldsUntilNormalPlaybackResumes(double speed)
    {
        var tracker = new PositionTracker(Start(0, at: 0));
        tracker.Apply(Clock(10, at: 10));

        tracker.Apply(Clock(30, at: 12, speed));
        AssertNear(12, tracker.Estimate(At(20)));
        tracker.Apply(Report(15.4, at: 14)); // Jellyfin's report during the scan does not move it either.
        var moved = tracker.Apply(Clock(95, at: 22));

        AssertNear(83, moved, toleranceMs: 100); // A seek: decoding restarts at the new position.
        AssertNear(96, tracker.Estimate(At(23)));
        Assert.Equal(1, tracker.Rate);
    }

    [Fact]
    public void Reports_AtOneAndAHalfTimes_SetTheSpeedOnceTwoMeasurementsAgree()
    {
        // The web player at 1.5x reports every 10 s; Jellyfin tells nothing about the speed.
        static double Real(double at) => 1.5 * at;
        var tracker = new PositionTracker(Start(0, at: 0));

        tracker.Apply(Report(Real(10) - 0.1, at: 10));
        Assert.Equal(1, tracker.Rate);
        tracker.Apply(Report(Real(20) - 0.1, at: 20));

        Assert.Equal(1.5, tracker.Rate);
        AssertNear(Real(30), tracker.Estimate(At(30)), toleranceMs: 300);
    }

    [Fact]
    public void Reports_BackAtNormalSpeed_SetItAgain()
    {
        var tracker = new PositionTracker(Start(0, at: 0));
        tracker.Apply(Report(14.9, at: 10));
        tracker.Apply(Report(29.9, at: 20)); // 1.5x from here.

        tracker.Apply(Report(39.8, at: 30)); // Normal speed again.
        tracker.Apply(Report(49.8, at: 40));

        Assert.Equal(1, tracker.Rate);
        AssertNear(59.8, tracker.Estimate(At(50)), toleranceMs: 300);
    }

    [Fact]
    public void Reports_AtNormalSpeed_WithTheWebClientsJitter_KeepTheSpeed()
    {
        var tracker = new PositionTracker(Start(0, at: 0));

        foreach (var (position, at) in new[] { (9.8, 10.0), (20.05, 20.0), (29.75, 30.0), (40.1, 40.0) })
        {
            tracker.Apply(Report(position, at));
        }

        Assert.Equal(1, tracker.Rate);
    }

    [Fact]
    public void Seek_DoesNotChangeTheSpeed()
    {
        var tracker = new PositionTracker(Start(0, at: 0));
        tracker.Apply(Report(10.2, at: 10));

        tracker.Apply(Report(300.3, at: 20)); // The user jumped ahead.
        tracker.Apply(Report(310.4, at: 30));

        Assert.Equal(1, tracker.Rate);
    }

    [Fact]
    public void Pause_StartsTheMeasurementOver()
    {
        var tracker = new PositionTracker(Start(0, at: 0));
        tracker.Apply(Report(14.9, at: 10)); // A first measurement of 1.5x ...
        tracker.Apply(Report(15.2, at: 12, paused: true));
        tracker.Apply(Report(15.2, at: 60));

        tracker.Apply(Report(30.1, at: 70)); // ... does not confirm one after the pause.

        Assert.Equal(1, tracker.Rate);
    }

    [Fact]
    public void TruncatedReports_CloseTogether_DoNotMeasureASpeed()
    {
        // Jellyfin for Kodi: whole seconds, up to a second off, so 10 s apart could look like 1.1x.
        var tracker = new PositionTracker(Start(0, at: 0));

        tracker.Apply(Report(10, at: 9.5));
        tracker.Apply(Report(21, at: 19.6));
        tracker.Apply(Report(32, at: 29.7));

        Assert.Equal(1, tracker.Rate);
    }

    [Fact]
    public void ClockReadings_DecideTheSpeed_ReportsNoLonger()
    {
        var tracker = new PositionTracker(Start(0, at: 0));
        tracker.Apply(Clock(5, at: 5));

        tracker.Apply(Report(14.9, at: 10));
        tracker.Apply(Report(29.9, at: 20));

        Assert.Equal(1, tracker.Rate);
    }

    [Fact]
    public void NewStart_IsAtNormalSpeed()
    {
        var tracker = new PositionTracker(Start(0, at: 0));
        tracker.Apply(Clock(10, at: 10, speed: 1.5));

        tracker.Apply(Start(0, at: 30));

        Assert.Equal(1, tracker.Rate);
        AssertNear(10, tracker.Estimate(At(40)));
    }

    private static ClockReading Clock(double position, double at, double speed = 1)
        => new(TimeSpan.FromSeconds(position), speed == 0, At(at), TimeSpan.FromMilliseconds(20), speed);

    private static DateTimeOffset At(double seconds) => T0 + TimeSpan.FromSeconds(seconds);

    private static PlaybackState Start(double position, double at, bool paused = false)
        => Report(position, at, paused) with { IsStart = true };

    private static PlaybackState Report(double position, double at, bool paused = false) => new()
    {
        SessionId = "session",
        DeviceId = "kodi",
        ItemId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Position = TimeSpan.FromSeconds(position),
        IsPaused = paused,
        ReportedAt = At(at),
    };

    private static void AssertNear(double expectedSeconds, TimeSpan actual, double toleranceMs = 1)
        => Assert.InRange(actual.TotalMilliseconds, (expectedSeconds * 1000) - toleranceMs, (expectedSeconds * 1000) + toleranceMs);
}
