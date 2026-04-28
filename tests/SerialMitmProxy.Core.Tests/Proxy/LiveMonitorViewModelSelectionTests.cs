using SerialMitmProxy.Application.Monitoring;
using SerialMitmProxy.Core.Models;
using SerialMitmProxy.Core.Rules;

namespace SerialMitmProxy.Core.Tests.Proxy;

public sealed class LiveMonitorViewModelSelectionTests
{
    [Fact]
    public async Task LiveMonitor_SplitsInboundAndForwardedFramesIntoSeparateCollections()
    {
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            using var monitor = new LiveMonitorViewModel
            {
                UiThrottleMs = 10,
            };

            monitor.PostInbound(Direction.AtoB, new byte[] { 0x01, 0x02 }, DateTimeOffset.UtcNow);
            monitor.PostForwarded(Frame.Create(Direction.AtoB, new byte[] { 0x01, 0x02 }, DateTimeOffset.UtcNow));

            await WaitForConditionAsync(() => monitor.InboundVisibleFrames.Count == 1);
            await WaitForConditionAsync(() => monitor.ForwardedVisibleFrames.Count == 1);

            Assert.Single(monitor.InboundVisibleFrames);
            Assert.Single(monitor.ForwardedVisibleFrames);
            Assert.True(monitor.VisibleFrames.Count >= 2);
            Assert.False(monitor.ForwardedVisibleFrames[0].IsModified);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public async Task LiveMonitor_MarksModifiedForwardedFrames()
    {
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            using var monitor = new LiveMonitorViewModel
            {
                UiThrottleMs = 10,
            };

            monitor.PostForwarded(Frame.Create(Direction.BtoA, new byte[] { 0xAA, 0xBB }, DateTimeOffset.UtcNow, isModified: true));

            await WaitForConditionAsync(() => monitor.ForwardedVisibleFrames.Count == 1);

            Assert.True(monitor.ForwardedVisibleFrames[0].IsModified);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public async Task LiveMonitor_SearchAndDirectionFiltersAffectBothTables()
    {
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            using var monitor = new LiveMonitorViewModel
            {
                UiThrottleMs = 10,
            };

            monitor.PostInbound(Direction.AtoB, new byte[] { 0xAA, 0xBB }, DateTimeOffset.UtcNow);
            monitor.PostForwarded(Frame.Create(Direction.BtoA, new byte[] { 0x01, 0x02 }, DateTimeOffset.UtcNow));

            await WaitForConditionAsync(() => monitor.VisibleFrames.Count == 2);

            monitor.SearchText = "AA BB";
            await WaitForConditionAsync(() => monitor.VisibleFrames.Count == 1);
            Assert.Single(monitor.InboundVisibleFrames);
            Assert.Empty(monitor.ForwardedVisibleFrames);

            monitor.SearchText = string.Empty;
            monitor.ShowBtoA = false;
            await WaitForConditionAsync(() => monitor.VisibleFrames.Count == 1);
            Assert.Single(monitor.InboundVisibleFrames);
            Assert.Empty(monitor.ForwardedVisibleFrames);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public async Task LiveMonitor_AppliesEnabledMonitorRulesAsAllowList()
    {
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            var rule = new Rule
            {
                Name = "hex-filter",
                Scope = RuleScope.Monitor,
                Matchers = new IRuleMatcher[]
                {
                    new DirectionMatcher(Direction.AtoB),
                    new HexPatternMatcher("AA BB"),
                },
            };

            using var monitor = new LiveMonitorViewModel
            {
                UiThrottleMs = 10,
            };
            monitor.SetMonitorRulesProvider(() => new[] { rule });

            monitor.PostInbound(Direction.AtoB, new byte[] { 0xAA, 0xBB }, DateTimeOffset.UtcNow);
            monitor.PostInbound(Direction.BtoA, new byte[] { 0x10, 0x11 }, DateTimeOffset.UtcNow);

            await WaitForConditionAsync(() => monitor.InboundVisibleFrames.Count == 1);

            Assert.Single(monitor.InboundVisibleFrames);
            Assert.Equal("AA BB", monitor.InboundVisibleFrames[0].Hex);

            rule.Enabled = false;
            monitor.RefreshFilters();

            await WaitForConditionAsync(() => monitor.InboundVisibleFrames.Count == 2);
            Assert.Equal(2, monitor.InboundVisibleFrames.Count);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    private static async Task WaitForConditionAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(1);
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(predicate());
    }
}
