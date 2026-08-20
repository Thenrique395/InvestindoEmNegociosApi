using System.Security.Claims;
using FluentAssertions;
using InvestindoEmNegocio.Application.DTOs;
using InvestindoEmNegocio.Application.Exceptions;
using InvestindoEmNegocio.Application.Interfaces;
using InvestindoEmNegocio.Controllers;
using InvestindoEmNegocio.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace InvestindoEmNegocio.Tests;

public class PlansAndGoalsControllerTests
{
    [Fact]
    public async Task PlansController_Should_Create_List_Get_Update_Delete_With_Expected_Statuses()
    {
        var userId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var plan = new PlanResponse(planId, MoneyType.Expense, "Plano", 100m, ScheduleType.OneTime, null, null, DateOnly.FromDateTime(DateTime.UtcNow), "Active", null, null);
        var details = new PlanDetailsResponse(plan, []);

        var plansService = new Mock<IPlansService>();
        plansService.Setup(x => x.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreatePlanRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        plansService.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<MoneyType?>(), It.IsAny<CancellationToken>())).ReturnsAsync([plan]);
        plansService.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), planId, It.IsAny<CancellationToken>())).ReturnsAsync(details);
        plansService.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), planId, It.IsAny<CreatePlanRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        plansService.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), planId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var audit = new Mock<IAuditService>();
        var controller = new PlansController(plansService.Object, Mock.Of<IPlanHistoryService>(), audit.Object);
        SetAuth(controller, userId);

        var request = new CreatePlanRequest(MoneyType.Expense, "Plano", 100m, ScheduleType.OneTime, DateOnly.FromDateTime(DateTime.UtcNow));

        (await controller.Create(request, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.List(MoneyType.Expense, new ListQuery(1, 10, "title", "asc"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.GetById(planId, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.Update(planId, request, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>();
        (await controller.Delete(planId, CancellationToken.None)).Should().BeOfType<NoContentResult>();

        controller.Response.Headers["X-Total-Count"].ToString().Should().Be("1");
        audit.Verify(x => x.LogAsync(userId, "DELETE", "Plan", planId.ToString(), It.IsAny<string?>(), It.IsAny<string?>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlansController_Should_Return_NotFound_For_Missing_Resources_And_Map_ArgumentException()
    {
        var plansService = new Mock<IPlansService>();
        plansService.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((PlanDetailsResponse?)null);
        plansService.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreatePlanRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((PlanResponse?)null);
        plansService.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        plansService.Setup(x => x.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreatePlanRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("invalido"));

        var controller = new PlansController(plansService.Object, Mock.Of<IPlanHistoryService>(), Mock.Of<IAuditService>());
        SetAuth(controller, Guid.NewGuid());

        var request = new CreatePlanRequest(MoneyType.Expense, "Plano", 100m, ScheduleType.OneTime, DateOnly.FromDateTime(DateTime.UtcNow));
        Func<Task> create = async () => await controller.Create(request, CancellationToken.None);

        await create.Should().ThrowAsync<AppProblemException>();
        (await controller.GetById(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<NotFoundResult>();
        (await controller.Update(Guid.NewGuid(), request, CancellationToken.None)).Result.Should().BeOfType<NotFoundResult>();
        (await controller.Delete(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GoalsController_Should_Cover_Main_Flows()
    {
        var userId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var goal = new GoalResponse(goalId, "Meta", 1000m, 200m, 2026, null, GoalStatus.InProgress, DateTime.UtcNow, DateTime.UtcNow, 100m, null);

        var goalsService = new Mock<IGoalsService>();
        goalsService.Setup(x => x.GetIncomeGoalAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(goal);
        goalsService.Setup(x => x.UpsertIncomeGoalAsync(It.IsAny<Guid>(), It.IsAny<UpsertIncomeGoalRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(goal);
        goalsService.Setup(x => x.ListAsync(It.IsAny<Guid>(), It.IsAny<int?>(), It.IsAny<GoalStatus?>(), It.IsAny<CancellationToken>())).ReturnsAsync([goal]);
        goalsService.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), goalId, It.IsAny<CancellationToken>())).ReturnsAsync(goal);
        goalsService.Setup(x => x.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreateGoalRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(goal);
        goalsService.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), goalId, It.IsAny<CreateGoalRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(goal);
        goalsService.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), goalId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var audit = new Mock<IAuditService>();
        var controller = new GoalsController(goalsService.Object, Mock.Of<IGoalProgressService>(), Mock.Of<IGoalOccurrenceService>(), audit.Object);
        var incomeController = new IncomeGoalsController(goalsService.Object);
        SetAuth(controller, userId);
        SetAuth(incomeController, userId);

        var request = new CreateGoalRequest("Meta", 1000m, 2026, null, GoalStatus.InProgress, 100m, 50m, null);

        (await incomeController.Get(2026, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await incomeController.Upsert(new UpsertIncomeGoalRequest(2026, 100m), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.List(2026, null, new ListQuery(1, 10, "title", "asc"), CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.GetById(goalId, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.Create(request, CancellationToken.None)).Should().BeOfType<CreatedResult>();
        (await controller.Update(goalId, request, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.Delete(goalId, CancellationToken.None)).Should().BeOfType<NoContentResult>();

        controller.Response.Headers["X-Total-Count"].ToString().Should().Be("1");
        audit.Verify(x => x.LogAsync(userId, "DELETE", "Goal", goalId.ToString(), It.IsAny<string?>(), It.IsAny<string?>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GoalsController_Should_Return_NotFound_NoContent_And_Map_ArgumentException()
    {
        var goalsService = new Mock<IGoalsService>();
        goalsService.Setup(x => x.GetIncomeGoalAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((GoalResponse?)null);
        goalsService.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((GoalResponse?)null);
        goalsService.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateGoalRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((GoalResponse?)null);
        goalsService.Setup(x => x.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        goalsService.Setup(x => x.CreateAsync(It.IsAny<Guid>(), It.IsAny<CreateGoalRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("invalido"));
        goalsService.Setup(x => x.UpsertIncomeGoalAsync(It.IsAny<Guid>(), It.IsAny<UpsertIncomeGoalRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("invalido"));

        var controller = new GoalsController(goalsService.Object, Mock.Of<IGoalProgressService>(), Mock.Of<IGoalOccurrenceService>(), Mock.Of<IAuditService>());
        var incomeController = new IncomeGoalsController(goalsService.Object);
        var userId = Guid.NewGuid();
        SetAuth(controller, userId);
        SetAuth(incomeController, userId);

        (await incomeController.Get(2026, CancellationToken.None)).Should().BeOfType<NoContentResult>();
        (await controller.GetById(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<NotFoundResult>();
        (await controller.Update(Guid.NewGuid(), new CreateGoalRequest("Meta", 1, 2026, null, GoalStatus.Planned, 0, 0, null), CancellationToken.None)).Should().BeOfType<NotFoundResult>();
        (await controller.Delete(Guid.NewGuid(), CancellationToken.None)).Should().BeOfType<NotFoundResult>();

        Func<Task> create = async () => await controller.Create(new CreateGoalRequest("Meta", 1, 2026, null, GoalStatus.Planned, 0, 0, null), CancellationToken.None);
        Func<Task> upsert = async () => await incomeController.Upsert(new UpsertIncomeGoalRequest(2026, 100m), CancellationToken.None);
        await create.Should().ThrowAsync<AppProblemException>();
        await upsert.Should().ThrowAsync<AppProblemException>();
    }

    private static void SetAuth(ControllerBase controller, Guid userId)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        context.Request.Headers["User-Agent"] = "tests";
        context.Request.Headers["X-Forwarded-For"] = "127.0.0.1";
        controller.ControllerContext = new ControllerContext { HttpContext = context };
    }
}
