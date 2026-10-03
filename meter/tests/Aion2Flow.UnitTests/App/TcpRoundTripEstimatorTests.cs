using Cloris.Aion2Flow.Capture;

namespace Cloris.Aion2Flow.Tests.App;

public sealed class TcpRoundTripEstimatorTests
{
    [Fact] public void ServerAcknowledgmentProducesPassiveSampleAndExpires()
    {
        var rtt = new TcpRoundTripEstimator();
        rtt.Observe(1, true, 100, 40, true, 0, 1000);
        rtt.Observe(1, false, 0, 0, true, 140, 1078);
        Assert.Equal(78d, rtt.GetCurrentMilliseconds(1, 1078));
        Assert.Null(rtt.GetCurrentMilliseconds(1, 22000));
        Assert.Equal(20922, rtt.GetSampleAgeMilliseconds(1, 22000));
        Assert.Null(rtt.GetSampleAgeMilliseconds(2, 22000));
        Assert.Null(rtt.GetSampleAgeMilliseconds(1, 1000));
        Assert.Null(rtt.GetCurrentMilliseconds(2, 1100));
    }
    [Fact] public void RetransmissionCannotGenerateAmbiguousSample()
    {
        var rtt = new TcpRoundTripEstimator();
        rtt.Observe(1, true, 100, 40, false, 0, 1000);
        rtt.Observe(1, true, 100, 40, false, 0, 1100);
        rtt.Observe(1, false, 0, 0, true, 140, 1150);
        Assert.Null(rtt.GetCurrentMilliseconds(1, 1150));
        rtt.Observe(1, true, 140, 20, false, 0, 2000);
        rtt.Observe(1, false, 0, 0, true, 160, 2050);
        Assert.Equal(50d, rtt.GetCurrentMilliseconds(1, 2050));
    }
    [Fact] public void AcknowledgmentHandlesSequenceWrapAndGenerationChange()
    {
        var rtt = new TcpRoundTripEstimator();
        rtt.Observe(1, true, uint.MaxValue - 9, 20, false, 0, 1000);
        rtt.Observe(1, false, 0, 0, true, 10, 1040);
        Assert.Equal(40d, rtt.GetCurrentMilliseconds(1, 1040));
        rtt.Observe(2, false, 0, 0, true, 10, 1050);
        Assert.Null(rtt.GetCurrentMilliseconds(2, 1050));
    }
    [Fact] public void PureOutboundAcksAndPartialServerAcksDoNotCreateSamples()
    {
        var rtt = new TcpRoundTripEstimator();
        rtt.Observe(1, true, 100, 0, true, 800, 1000);
        rtt.Observe(1, false, 0, 0, true, 100, 1050);
        Assert.Null(rtt.GetCurrentMilliseconds(1, 1050));
        rtt.Observe(1, true, 100, 40, true, 800, 2000);
        rtt.Observe(1, false, 0, 0, true, 120, 2050);
        Assert.Null(rtt.GetCurrentMilliseconds(1, 2050));
        rtt.Observe(1, false, 0, 0, true, 140, 2070);
        Assert.Equal(70d, rtt.GetCurrentMilliseconds(1, 2070));
    }
    [Fact] public void InvalidAcknowledgmentAndCaptureRestartCannotReuseOldSample()
    {
        var rtt = new TcpRoundTripEstimator();
        rtt.Observe(1, true, 100, 40, false, 0, 1000);
        rtt.Observe(1, false, 0, 0, true, 800, 1050);
        Assert.Null(rtt.GetCurrentMilliseconds(1, 1050));
        rtt.Observe(1, false, 0, 0, true, 140, 1080);
        Assert.Equal(80d, rtt.GetCurrentMilliseconds(1, 1080));
        rtt.Clear();
        Assert.Null(rtt.GetSampleAgeMilliseconds(1, 1100));
        Assert.Null(rtt.GetCurrentMilliseconds(1, 1100));
    }
}
