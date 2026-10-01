using Carpediem.Modules.Staffing.Domain.Availability;

namespace Carpediem.Domain.Tests.Staffing;

public sealed class AvailabilityResponseTests
{
    private readonly Guid _staffingCycleId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTimeOffset _futureEventStart = DateTimeOffset.UtcNow.AddDays(7);
    private readonly DateTimeOffset _declaredAt = DateTimeOffset.UtcNow;

    [Theory]
    [InlineData(AvailabilityResponseType.Available)]
    [InlineData(AvailabilityResponseType.NotAvailable)]
    public void DeclareWithValidParametersCreatesAvailabilityResponse(AvailabilityResponseType responseType)
    {
        var response = AvailabilityResponse.Declare(
            _staffingCycleId,
            _eventId,
            _userId,
            responseType,
            _futureEventStart,
            _declaredAt,
            isChiefLeaderOnEvent: false,
            eventStatus: "Published");

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal(_staffingCycleId, response.StaffingCycleId);
        Assert.Equal(_eventId, response.EventId);
        Assert.Equal(_userId, response.UserId);
        Assert.Equal(responseType, response.Response);
        Assert.Equal(_declaredAt, response.DeclaredAt);
    }

    [Fact]
    public void DeclareWhenEventIsNotPublishedThrowsInvalidEventStatusException()
    {
        var ex = Assert.Throws<InvalidEventStatusException>(() =>
            AvailabilityResponse.Declare(
                _staffingCycleId,
                _eventId,
                _userId,
                AvailabilityResponseType.Available,
                _futureEventStart,
                _declaredAt,
                isChiefLeaderOnEvent: false,
                eventStatus: "Draft"));

        Assert.Equal(_eventId, ex.EventId);
        Assert.Equal("Draft", ex.CurrentStatus);
    }

    [Fact]
    public void DeclareWhenEventStartHasPassedThrowsAvailabilityWindowClosedException()
    {
        var pastStart = DateTimeOffset.UtcNow.AddHours(-1);
        var ex = Assert.Throws<AvailabilityWindowClosedException>(() =>
            AvailabilityResponse.Declare(
                _staffingCycleId,
                _eventId,
                _userId,
                AvailabilityResponseType.Available,
                pastStart,
                _declaredAt,
                isChiefLeaderOnEvent: false,
                eventStatus: "Published"));

        Assert.Equal(_eventId, ex.EventId);
        Assert.Equal(pastStart, ex.StartDateTime);
    }

    [Fact]
    public void DeclareWhenUserIsActiveChiefLeaderOnEventThrowsChiefLeaderExclusivityException()
    {
        var ex = Assert.Throws<ChiefLeaderExclusivityException>(() =>
            AvailabilityResponse.Declare(
                _staffingCycleId,
                _eventId,
                _userId,
                AvailabilityResponseType.Available,
                _futureEventStart,
                _declaredAt,
                isChiefLeaderOnEvent: true,
                eventStatus: "Published"));

        Assert.Equal(_eventId, ex.EventId);
        Assert.Equal(_userId, ex.UserId);
    }

    [Fact]
    public void UpdateResponseTogglesResponseSuccessfully()
    {
        var response = AvailabilityResponse.Declare(
            _staffingCycleId,
            _eventId,
            _userId,
            AvailabilityResponseType.Available,
            _futureEventStart,
            _declaredAt,
            isChiefLeaderOnEvent: false,
            eventStatus: "Published");

        var updatedAt = _declaredAt.AddHours(1);
        response.UpdateResponse(
            AvailabilityResponseType.NotAvailable,
            _futureEventStart,
            updatedAt,
            isChiefLeaderOnEvent: false,
            eventStatus: "Published");

        Assert.Equal(AvailabilityResponseType.NotAvailable, response.Response);
        Assert.Equal(updatedAt, response.DeclaredAt);
    }

    [Fact]
    public void UpdateResponseWhenWindowClosedThrowsAvailabilityWindowClosedException()
    {
        var response = AvailabilityResponse.Declare(
            _staffingCycleId,
            _eventId,
            _userId,
            AvailabilityResponseType.Available,
            _futureEventStart,
            _declaredAt,
            isChiefLeaderOnEvent: false,
            eventStatus: "Published");

        var pastStart = DateTimeOffset.UtcNow.AddMinutes(-10);
        var ex = Assert.Throws<AvailabilityWindowClosedException>(() =>
            response.UpdateResponse(
                AvailabilityResponseType.NotAvailable,
                pastStart,
                DateTimeOffset.UtcNow,
                isChiefLeaderOnEvent: false,
                eventStatus: "Published"));

        Assert.Equal(_eventId, ex.EventId);
    }

    [Fact]
    public void UpdateResponseWhenUserBecameChiefLeaderThrowsChiefLeaderExclusivityException()
    {
        var response = AvailabilityResponse.Declare(
            _staffingCycleId,
            _eventId,
            _userId,
            AvailabilityResponseType.Available,
            _futureEventStart,
            _declaredAt,
            isChiefLeaderOnEvent: false,
            eventStatus: "Published");

        var ex = Assert.Throws<ChiefLeaderExclusivityException>(() =>
            response.UpdateResponse(
                AvailabilityResponseType.NotAvailable,
                _futureEventStart,
                DateTimeOffset.UtcNow,
                isChiefLeaderOnEvent: true,
                eventStatus: "Published"));

        Assert.Equal(_eventId, ex.EventId);
    }
}
