using iPath.Application.Contracts;
using iPath.Application.Features.TaskAssignments;
using iPath.Application.Features.Users;
using Microsoft.Extensions.Configuration;
using iPath.EF.Core.FeatureHandlers.TaskAssignments.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace iPath.Test.xUnit2.TaskAssignments;

public class TaskAssignmentCommandHandlerTests : IClassFixture<iPathFixture>
{
    private readonly iPathDbContext _db;
    private readonly IUserSession _sess;

    public TaskAssignmentCommandHandlerTests(iPathFixture dbFixture)
    {
        var opts = new DbContextOptionsBuilder<iPathDbContext>()
            .UseInMemoryDatabase($"TaskAssignmentTest_{Guid.NewGuid()}")
            .Options;
        _db = new iPathDbContext(opts, Substitute.For<IMediator>(), Substitute.For<IConfiguration>());
        _sess = Substitute.For<IUserSession>();
        _sess.User.Returns(new SessionUserDto(Guid.NewGuid(), "testmod", "test@test.com", "TT", ["admin"], null, null));
    }

    [Fact]
    public async Task Propose_TaskAssignment_Should_Create_And_Return_Dto()
    {
        var groupId = Guid.CreateVersion7();
        var consultantId = Guid.NewGuid();
        var srId = Guid.CreateVersion7();

        _db.Users.Add(new User { Id = consultantId, UserName = "consultant" });
        _db.Groups.Add(new Group { Id = groupId });
        _db.ServiceRequests.Add(new ServiceRequest
        {
            Id = srId, GroupId = groupId, OwnerId = consultantId, NodeType = "Test"
        });
        await _db.SaveChangesAsync();

        var mediator = Substitute.For<IMediator>();
        var dto = new TaskAssignmentDto { Id = Guid.CreateVersion7(), Status = nameof(eTaskStatus.Proposed) };
        mediator.Send(Arg.Any<GetTaskAssignmentByIdQuery>(), default)
            .Returns(Task.FromResult(dto));

        var handler = new ProposeTaskAssignmentHandler(_db, _sess, mediator, NullLogger<ProposeTaskAssignmentHandler>.Instance);
        var result = await handler.Handle(new ProposeTaskAssignmentCommand(srId, consultantId, eTaskAssignmentMode.ModeratorSuggested), default);

        result.Should().NotBeNull();
        result.Status.Should().Be(nameof(eTaskStatus.Proposed));
    }

    [Fact]
    public async Task Accept_Proposed_Task_Should_Set_Assigned()
    {
        var taskId = Guid.CreateVersion7();
        var userId = _sess.User!.Id;

        _db.TaskAssignments.Add(new TaskAssignment
        {
            Id = taskId,
            ServiceRequestId = Guid.CreateVersion7(),
            AssignedToUserId = userId,
            AssignedByUserId = userId,
            Type = eTaskType.DiagnosticReview,
            Mode = eTaskAssignmentMode.ModeratorSuggested,
            Status = eTaskStatus.Proposed,
            CreatedOn = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var mediator = Substitute.For<IMediator>();
        var dto = new TaskAssignmentDto { Id = taskId, Status = nameof(eTaskStatus.Assigned) };
        mediator.Send(Arg.Any<GetTaskAssignmentByIdQuery>(), default)
            .Returns(Task.FromResult(dto));

        var handler = new AcceptTaskAssignmentHandler(_db, _sess, mediator, NullLogger<AcceptTaskAssignmentHandler>.Instance);
        var result = await handler.Handle(new AcceptTaskAssignmentCommand(taskId), default);

        result.Should().NotBeNull();
        result.Status.Should().Be(nameof(eTaskStatus.Assigned));
    }

    [Fact]
    public async Task Decline_Proposed_Task_Should_Set_Declined()
    {
        var taskId = Guid.CreateVersion7();
        var userId = _sess.User!.Id;

        _db.TaskAssignments.Add(new TaskAssignment
        {
            Id = taskId,
            ServiceRequestId = Guid.CreateVersion7(),
            AssignedToUserId = userId,
            AssignedByUserId = userId,
            Type = eTaskType.DiagnosticReview,
            Mode = eTaskAssignmentMode.ModeratorSuggested,
            Status = eTaskStatus.Proposed,
            CreatedOn = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var mediator = Substitute.For<IMediator>();
        var dto = new TaskAssignmentDto { Id = taskId, Status = nameof(eTaskStatus.Declined) };
        mediator.Send(Arg.Any<GetTaskAssignmentByIdQuery>(), default)
            .Returns(Task.FromResult(dto));

        var handler = new DeclineTaskAssignmentHandler(_db, _sess, mediator, NullLogger<DeclineTaskAssignmentHandler>.Instance);
        var result = await handler.Handle(new DeclineTaskAssignmentCommand(taskId), default);

        result.Should().NotBeNull();
        result.Status.Should().Be(nameof(eTaskStatus.Declined));
    }

    [Fact]
    public async Task Complete_Assigned_Task_Should_Set_Completed()
    {
        var taskId = Guid.CreateVersion7();
        var userId = _sess.User!.Id;

        _db.TaskAssignments.Add(new TaskAssignment
        {
            Id = taskId,
            ServiceRequestId = Guid.CreateVersion7(),
            AssignedToUserId = userId,
            AssignedByUserId = userId,
            Type = eTaskType.DiagnosticReview,
            Mode = eTaskAssignmentMode.SelfAssigned,
            Status = eTaskStatus.Assigned,
            AcceptedOn = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var mediator = Substitute.For<IMediator>();
        var dto = new TaskAssignmentDto { Id = taskId, Status = nameof(eTaskStatus.Completed) };
        mediator.Send(Arg.Any<GetTaskAssignmentByIdQuery>(), default)
            .Returns(Task.FromResult(dto));

        var handler = new CompleteTaskAssignmentHandler(_db, _sess, mediator, NullLogger<CompleteTaskAssignmentHandler>.Instance);
        var result = await handler.Handle(new CompleteTaskAssignmentCommand(taskId), default);

        result.Should().NotBeNull();
        result.Status.Should().Be(nameof(eTaskStatus.Completed));
    }

    [Fact]
    public async Task Return_Assigned_Task_Should_Set_Returned()
    {
        var taskId = Guid.CreateVersion7();
        var userId = _sess.User!.Id;

        _db.TaskAssignments.Add(new TaskAssignment
        {
            Id = taskId,
            ServiceRequestId = Guid.CreateVersion7(),
            AssignedToUserId = userId,
            AssignedByUserId = userId,
            Type = eTaskType.DiagnosticReview,
            Mode = eTaskAssignmentMode.SelfAssigned,
            Status = eTaskStatus.InProgress,
            AcceptedOn = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var mediator = Substitute.For<IMediator>();
        var dto = new TaskAssignmentDto { Id = taskId, Status = nameof(eTaskStatus.ReturnedForReassignment) };
        mediator.Send(Arg.Any<GetTaskAssignmentByIdQuery>(), default)
            .Returns(Task.FromResult(dto));

        var handler = new ReturnTaskAssignmentHandler(_db, _sess, mediator, NullLogger<ReturnTaskAssignmentHandler>.Instance);
        var result = await handler.Handle(new ReturnTaskAssignmentCommand(taskId), default);

        result.Should().NotBeNull();
        result.Status.Should().Be(nameof(eTaskStatus.ReturnedForReassignment));
    }

    [Fact]
    public async Task Cancel_Task_By_Admin_Should_Set_Cancelled()
    {
        var taskId = Guid.CreateVersion7();
        var userId = _sess.User!.Id;
        var srId = Guid.CreateVersion7();

        _db.ServiceRequests.Add(new ServiceRequest
        {
            Id = srId, GroupId = Guid.CreateVersion7(), OwnerId = userId, NodeType = "Test"
        });
        _db.TaskAssignments.Add(new TaskAssignment
        {
            Id = taskId,
            ServiceRequestId = srId,
            AssignedToUserId = userId,
            AssignedByUserId = userId,
            Type = eTaskType.DiagnosticReview,
            Mode = eTaskAssignmentMode.SelfAssigned,
            Status = eTaskStatus.Assigned,
            AcceptedOn = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var mediator = Substitute.For<IMediator>();
        var dto = new TaskAssignmentDto { Id = taskId, Status = nameof(eTaskStatus.Cancelled) };
        mediator.Send(Arg.Any<GetTaskAssignmentByIdQuery>(), default)
            .Returns(Task.FromResult(dto));

        var handler = new CancelTaskAssignmentHandler(_db, _sess, mediator, NullLogger<CancelTaskAssignmentHandler>.Instance);
        var result = await handler.Handle(new CancelTaskAssignmentCommand(taskId), default);

        result.Should().NotBeNull();
        result.Status.Should().Be(nameof(eTaskStatus.Cancelled));
    }
}
