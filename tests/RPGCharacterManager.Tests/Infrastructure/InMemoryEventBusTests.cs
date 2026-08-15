using Microsoft.Extensions.Logging.Abstractions;
using RPGCharacterManager.Infrastructure.Events;

namespace RPGCharacterManager.Tests.Infrastructure;

public sealed class InMemoryEventBusTests
{
    private sealed record TestEvent(int Value);

    private sealed record OtherEvent(string Text);

    private static InMemoryEventBus CreateBus() => new(NullLogger<InMemoryEventBus>.Instance);

    [Fact]
    public async Task PublishAsync_ВызываетПодписчика()
    {
        var bus = CreateBus();
        var received = 0;

        using var subscription = bus.Subscribe<TestEvent>(payload => received = payload.Value);
        await bus.PublishAsync(new TestEvent(5));

        Assert.Equal(5, received);
    }

    [Fact]
    public async Task PublishAsync_НеВызываетПодписчиковДругихТипов()
    {
        var bus = CreateBus();
        var called = false;

        using var subscription = bus.Subscribe<OtherEvent>(_ => called = true);
        await bus.PublishAsync(new TestEvent(1));

        Assert.False(called);
    }

    [Fact]
    public async Task Dispose_ОтменяетПодписку()
    {
        var bus = CreateBus();
        var calls = 0;

        var subscription = bus.Subscribe<TestEvent>(_ => calls++);
        await bus.PublishAsync(new TestEvent(1));
        subscription.Dispose();
        await bus.PublishAsync(new TestEvent(2));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task PublishAsync_ПродолжаетРаботу_ЕслиОбработчикБросилИсключение()
    {
        var bus = CreateBus();
        var secondHandlerCalled = false;

        using var failing = bus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("сбой"));
        using var working = bus.Subscribe<TestEvent>(_ => secondHandlerCalled = true);

        // Сбой одного подписчика не должен нарушать работу остальных и издателя.
        await bus.PublishAsync(new TestEvent(1));

        Assert.True(secondHandlerCalled);
    }

    [Fact]
    public async Task PublishAsync_ВызываетНесколькихПодписчиков()
    {
        var bus = CreateBus();
        var total = 0;

        using var first = bus.Subscribe<TestEvent>(payload => total += payload.Value);
        using var second = bus.Subscribe<TestEvent>(payload => total += payload.Value);

        await bus.PublishAsync(new TestEvent(3));

        Assert.Equal(6, total);
    }
}
