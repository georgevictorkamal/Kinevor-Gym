using Kinevor.Modules.ClassesBookings.Application;
using Kinevor.Modules.ClassesBookings.Domain;
using Kinevor.Platform.Application.Common;
using Kinevor.Platform.Application.Exceptions;
using Kinevor.Platform.Domain.Enums;
using Xunit;

namespace Kinevor.Modules.ClassesBookings.Tests;

/// <summary>Object-level authorization rules for booking and cancelling on behalf of a member.</summary>
public class BookingOwnershipTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TrainerId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTrainerId = Guid.Parse("b2222222-2222-2222-2222-222222222222");
    private static readonly Guid MemberId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherMemberId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>A member cannot pass another member's id when booking.</summary>
    [Fact]
    public async Task Member_CannotBookForAnotherMember()
    {
        var gymClass = NewClass(Now.AddHours(4));
        var bookings = new FakeBookings();
        var handler = new BookClassHandler(
            new FakeClasses(gymClass),
            bookings,
            new FakeUnitOfWork(),
            new NullBranchContext(),
            new FakeUser("Member") { MemberId = MemberId },
            new FakeTimeProvider(Now));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new BookClassCommand(gymClass.Id, OtherMemberId, "Someone Else"), default));

        Assert.Empty(bookings.Items);
    }

    /// <summary>Staff booking on behalf of a member must name the member.</summary>
    [Fact]
    public async Task Staff_BookOnBehalfRequiresMemberId()
    {
        var gymClass = NewClass(Now.AddHours(4));
        var handler = new BookClassHandler(
            new FakeClasses(gymClass),
            new FakeBookings(),
            new FakeUnitOfWork(),
            new NullBranchContext(),
            new FakeUser("Staff"),
            new FakeTimeProvider(Now));

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new BookClassCommand(gymClass.Id, null, "Maya Thompson"), default));
    }

    /// <summary>Staff may book for any member when the member id is supplied.</summary>
    [Fact]
    public async Task Staff_MayBookOnBehalfOfMember()
    {
        var gymClass = NewClass(Now.AddHours(4));
        var bookings = new FakeBookings();
        var handler = new BookClassHandler(
            new FakeClasses(gymClass),
            bookings,
            new FakeUnitOfWork(),
            new NullBranchContext(),
            new FakeUser("Staff"),
            new FakeTimeProvider(Now));

        var result = await handler.Handle(
            new BookClassCommand(gymClass.Id, MemberId, "Maya Thompson"), default);

        Assert.False(result.IsWaitListed);
        var booking = Assert.Single(bookings.Items);
        Assert.Equal(MemberId, booking.MemberId);
        Assert.Equal("Maya Thompson", booking.MemberName);
    }

    /// <summary>A member cannot cancel a booking that belongs to someone else.</summary>
    [Fact]
    public async Task Member_CannotCancelAnotherMembersBooking()
    {
        var gymClass = NewClass(Now.AddHours(4));
        var booking = Booking(gymClass, OtherMemberId);
        var handler = CancelHandler(gymClass, booking, new FakeUser("Member") { MemberId = MemberId });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new CancelBookingCommand(booking.Id, null), default));
    }

    /// <summary>The trainer who owns the class may cancel its bookings.</summary>
    [Fact]
    public async Task ClassTrainer_MayCancelBooking()
    {
        var gymClass = NewClass(Now.AddHours(4));
        var booking = Booking(gymClass, MemberId);
        var handler = CancelHandler(
            gymClass,
            booking,
            new FakeUser("Trainer") { TrainerId = TrainerId });

        await handler.Handle(new CancelBookingCommand(booking.Id, "Injury"), default);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal("Injury", booking.CancellationReason);
    }

    /// <summary>A trainer who does not own the class cannot cancel its bookings.</summary>
    [Fact]
    public async Task DifferentTrainer_CannotCancelBooking()
    {
        var gymClass = NewClass(Now.AddHours(4));
        var booking = Booking(gymClass, MemberId);
        var handler = CancelHandler(
            gymClass,
            booking,
            new FakeUser("Trainer") { TrainerId = OtherTrainerId });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new CancelBookingCommand(booking.Id, null), default));

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
    }

    /// <summary>Inside two hours a member is blocked, but staff may still cancel.</summary>
    [Fact]
    public async Task ConfirmedBooking_InsideTwoHours_MemberBlockedStaffAllowed()
    {
        var gymClass = NewClass(Now.AddHours(1));
        var booking = Booking(gymClass, MemberId);
        var memberHandler = CancelHandler(
            gymClass,
            booking,
            new FakeUser("Member") { MemberId = MemberId });

        await Assert.ThrowsAsync<DomainException>(() =>
            memberHandler.Handle(new CancelBookingCommand(booking.Id, null), default));
        Assert.Equal(BookingStatus.Confirmed, booking.Status);

        var staffHandler = CancelHandler(gymClass, booking, new FakeUser("Staff"));
        await staffHandler.Handle(new CancelBookingCommand(booking.Id, "Emergency"), default);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    private static CancelBookingHandler CancelHandler(
        GymClass gymClass, ClassBooking booking, ICurrentUser user) =>
        new(
            new FakeBookings(booking),
            new FakeClasses(gymClass),
            new FakeUnitOfWork(),
            user,
            new FakeTimeProvider(Now));

    private static GymClass NewClass(DateTime scheduledAt)
    {
        var result = GymClass.Create(
            Now,
            "Sunrise Yoga",
            "A gentle flow.",
            ClassType.Yoga,
            TrainerId,
            "Amara Okafor",
            scheduledAt,
            60,
            10,
            "Studio One",
            null,
            true);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static ClassBooking Booking(GymClass gymClass, Guid memberId) =>
        ClassBooking.Create(
            gymClass.Id,
            gymClass.Name,
            gymClass.ScheduledAt,
            memberId,
            "Maya Thompson",
            false,
            Now);

    private sealed class FakeUser(params string[] roles) : ICurrentUser
    {
        public Guid? UserId { get; init; } = Guid.NewGuid();
        public IReadOnlyList<string> Roles { get; } = roles;
        public string? Email { get; init; } = "caller@kinevor.demo";
        public Guid? BranchId { get; init; }
        public Guid? MemberId { get; init; }
        public Guid? TrainerId { get; init; }
    }

    private sealed class FakeTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed class NullBranchContext : IBranchContext
    {
        public Guid? BranchId => null;
    }

    private sealed class FakeUnitOfWork : IClassesBookingsUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);

        public Task<bool> TrySaveChangesAsync(CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeClasses(GymClass gymClass) : IGymClassRepository
    {
        public Task<GymClass?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<GymClass?>(id == gymClass.Id ? gymClass : null);

        public Task AddAsync(GymClass value, CancellationToken ct = default) => Task.CompletedTask;

        public void Update(GymClass value)
        {
        }

        public Task<(IReadOnlyList<GymClass> Items, int Total)> SearchAsync(
            ClassType? type,
            ClassStatus? status,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize,
            CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<GymClass>> GetUpcomingAsync(
            DateTime now, int limit, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeBookings(params ClassBooking[] seed) : IClassBookingRepository
    {
        public List<ClassBooking> Items { get; } = [.. seed];

        public Task<ClassBooking?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(booking => booking.Id == id));

        public Task<ClassBooking?> GetActiveAsync(
            Guid memberId, Guid classId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(booking =>
                booking.MemberId == memberId
                && booking.ClassId == classId
                && booking.Status is BookingStatus.Confirmed or BookingStatus.WaitListed));

        public Task<ClassBooking?> GetWaitListHeadAsync(Guid classId, CancellationToken ct = default) =>
            Task.FromResult(Items
                .Where(booking => booking.ClassId == classId && booking.IsWaitListed)
                .OrderBy(booking => booking.BookedAt)
                .FirstOrDefault());

        public Task AddAsync(ClassBooking booking, CancellationToken ct = default)
        {
            Items.Add(booking);
            return Task.CompletedTask;
        }

        public void Update(ClassBooking booking)
        {
        }

        public Task<IReadOnlyList<ClassBooking>> GetByClassAsync(
            Guid classId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ClassBooking>>(
                Items.Where(booking => booking.ClassId == classId).ToList());

        public Task<ClassBooking?> GetLatestAsync(
            Guid memberId, Guid classId, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<ClassBooking>> GetByMemberAsync(
            Guid memberId, DateTime now, bool upcoming, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
