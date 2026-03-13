using Foundry.Shared.Kernel.Results;
using Foundry.Showcases.Api.Contracts.Requests;
using Foundry.Showcases.Api.Controllers;
using Foundry.Showcases.Application.Commands.CreateShowcase;
using Foundry.Showcases.Application.Commands.DeleteShowcase;
using Foundry.Showcases.Application.Commands.UpdateShowcase;
using Foundry.Showcases.Application.Contracts;
using Foundry.Showcases.Application.Queries.GetShowcase;
using Foundry.Showcases.Application.Queries.GetShowcases;
using Foundry.Showcases.Domain.Enums;
using Foundry.Showcases.Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Foundry.Showcases.Tests.Api.Controllers;

public class ShowcasesControllerTests
{
    private readonly IMessageBus _bus;
    private readonly ShowcasesController _controller;

    public ShowcasesControllerTests()
    {
        _bus = Substitute.For<IMessageBus>();
        _controller = new ShowcasesController(_bus);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    private static ShowcaseDto CreateShowcaseDto(ShowcaseId? id = null)
    {
        return new ShowcaseDto(
            id ?? ShowcaseId.New(),
            "My Showcase",
            "A description",
            ShowcaseCategory.WebApp,
            "https://demo.example.com",
            "https://github.com/example",
            null,
            new List<string> { "dotnet" },
            0,
            true);
    }

    #region GetAll

    [Fact]
    public async Task GetAll_ReturnsOkWithShowcases()
    {
        IReadOnlyList<ShowcaseDto> dtos = new List<ShowcaseDto> { CreateShowcaseDto(), CreateShowcaseDto() };
        _bus.InvokeAsync<Result<IReadOnlyList<ShowcaseDto>>>(
                Arg.Any<GetShowcasesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dtos));

        IActionResult result = await _controller.GetAll(null, null, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ShowcaseDto> returned = ok.Value.Should().BeAssignableTo<IReadOnlyList<ShowcaseDto>>().Subject;
        returned.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_PassesCategoryAndTagToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<ShowcaseDto>>>(
                Arg.Any<GetShowcasesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ShowcaseDto>>(new List<ShowcaseDto>()));

        await _controller.GetAll(ShowcaseCategory.Api, "dotnet", CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<ShowcaseDto>>>(
            Arg.Is<GetShowcasesQuery>(q => q.Category == ShowcaseCategory.Api && q.Tag == "dotnet"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAll_WithNoCategoryOrTag_PassesNullsToQuery()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<ShowcaseDto>>>(
                Arg.Any<GetShowcasesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ShowcaseDto>>(new List<ShowcaseDto>()));

        await _controller.GetAll(null, null, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<IReadOnlyList<ShowcaseDto>>>(
            Arg.Is<GetShowcasesQuery>(q => q.Category == null && q.Tag == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAll_WhenEmpty_ReturnsOkWithEmptyList()
    {
        _bus.InvokeAsync<Result<IReadOnlyList<ShowcaseDto>>>(
                Arg.Any<GetShowcasesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<ShowcaseDto>>(new List<ShowcaseDto>()));

        IActionResult result = await _controller.GetAll(null, null, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ShowcaseDto> returned = ok.Value.Should().BeAssignableTo<IReadOnlyList<ShowcaseDto>>().Subject;
        returned.Should().BeEmpty();
    }

    #endregion

    #region GetById

    [Fact]
    public async Task GetById_WhenFound_ReturnsOkWithDto()
    {
        ShowcaseId showcaseId = ShowcaseId.New();
        ShowcaseDto dto = CreateShowcaseDto(showcaseId);
        _bus.InvokeAsync<Result<ShowcaseDto>>(Arg.Any<GetShowcaseQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        IActionResult result = await _controller.GetById(showcaseId.Value, CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ShowcaseDto returned = ok.Value.Should().BeOfType<ShowcaseDto>().Subject;
        returned.Id.Should().Be(showcaseId);
    }

    [Fact]
    public async Task GetById_WhenNotFound_Returns404()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result<ShowcaseDto>>(Arg.Any<GetShowcaseQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ShowcaseDto>(Error.NotFound("Showcase.NotFound", "not found")));

        IActionResult result = await _controller.GetById(id, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetById_PassesCorrectIdToQuery()
    {
        Guid id = Guid.NewGuid();
        ShowcaseDto dto = CreateShowcaseDto(new ShowcaseId(id));
        _bus.InvokeAsync<Result<ShowcaseDto>>(Arg.Any<GetShowcaseQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));

        await _controller.GetById(id, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<ShowcaseDto>>(
            Arg.Is<GetShowcaseQuery>(q => q.Id.Value == id),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Create

    [Fact]
    public async Task Create_WithValidRequest_Returns201Created()
    {
        ShowcaseId showcaseId = ShowcaseId.New();
        CreateShowcaseRequest request = new(
            "My Showcase",
            "Description",
            ShowcaseCategory.WebApp,
            "https://demo.example.com",
            null,
            null,
            null,
            0,
            false);
        _bus.InvokeAsync<Result<ShowcaseId>>(Arg.Any<CreateShowcaseCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(showcaseId));

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        CreatedResult created = result.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Location.Should().Be($"/api/v1/showcases/{showcaseId.Value}");
    }

    [Fact]
    public async Task Create_PassesCorrectFieldsToCommand()
    {
        ShowcaseId showcaseId = ShowcaseId.New();
        CreateShowcaseRequest request = new(
            "My Showcase",
            "Description",
            ShowcaseCategory.Library,
            "https://demo.example.com",
            "https://github.com/example",
            null,
            new List<string> { "dotnet" },
            5,
            true);
        _bus.InvokeAsync<Result<ShowcaseId>>(Arg.Any<CreateShowcaseCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(showcaseId));

        await _controller.Create(request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result<ShowcaseId>>(
            Arg.Is<CreateShowcaseCommand>(c =>
                c.Title == "My Showcase" &&
                c.Category == ShowcaseCategory.Library &&
                c.DemoUrl == "https://demo.example.com" &&
                c.GitHubUrl == "https://github.com/example" &&
                c.DisplayOrder == 5 &&
                c.IsPublished),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WhenValidationFailure_Returns400()
    {
        CreateShowcaseRequest request = new(
            "",
            null,
            ShowcaseCategory.WebApp,
            null,
            null,
            null,
            null,
            0,
            false);
        _bus.InvokeAsync<Result<ShowcaseId>>(Arg.Any<CreateShowcaseCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ShowcaseId>(Error.Validation("Showcase.TitleRequired", "Title is required")));

        IActionResult result = await _controller.Create(request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    #endregion

    #region Update

    [Fact]
    public async Task Update_WhenSuccess_ReturnsOk()
    {
        Guid id = Guid.NewGuid();
        UpdateShowcaseRequest request = new(
            "Updated Title",
            "Updated Description",
            ShowcaseCategory.Api,
            null,
            "https://github.com/example",
            null,
            null,
            0,
            true);
        _bus.InvokeAsync<Result>(Arg.Any<UpdateShowcaseCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.Update(id, request, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task Update_PassesCorrectFieldsToCommand()
    {
        Guid id = Guid.NewGuid();
        UpdateShowcaseRequest request = new(
            "Updated Title",
            null,
            ShowcaseCategory.Mobile,
            "https://demo.example.com",
            null,
            null,
            null,
            3,
            false);
        _bus.InvokeAsync<Result>(Arg.Any<UpdateShowcaseCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.Update(id, request, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<UpdateShowcaseCommand>(c =>
                c.ShowcaseId.Value == id &&
                c.Title == "Updated Title" &&
                c.Category == ShowcaseCategory.Mobile &&
                c.DisplayOrder == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WhenNotFound_Returns404()
    {
        Guid id = Guid.NewGuid();
        UpdateShowcaseRequest request = new(
            "Updated Title",
            null,
            ShowcaseCategory.WebApp,
            "https://demo.example.com",
            null,
            null,
            null,
            0,
            false);
        _bus.InvokeAsync<Result>(Arg.Any<UpdateShowcaseCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("Showcase.NotFound", "not found")));

        IActionResult result = await _controller.Update(id, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Update_WhenValidationFailure_Returns400()
    {
        Guid id = Guid.NewGuid();
        UpdateShowcaseRequest request = new(
            "",
            null,
            ShowcaseCategory.WebApp,
            null,
            null,
            null,
            null,
            0,
            false);
        _bus.InvokeAsync<Result>(Arg.Any<UpdateShowcaseCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Validation("Showcase.TitleRequired", "Title is required")));

        IActionResult result = await _controller.Update(id, request, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    #endregion

    #region Delete

    [Fact]
    public async Task Delete_WhenSuccess_Returns204NoContent()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DeleteShowcaseCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        IActionResult result = await _controller.Delete(id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_PassesCorrectIdToCommand()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DeleteShowcaseCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        await _controller.Delete(id, CancellationToken.None);

        await _bus.Received(1).InvokeAsync<Result>(
            Arg.Is<DeleteShowcaseCommand>(c => c.Id.Value == id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenNotFound_Returns404()
    {
        Guid id = Guid.NewGuid();
        _bus.InvokeAsync<Result>(Arg.Any<DeleteShowcaseCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.NotFound("Showcase.NotFound", "not found")));

        IActionResult result = await _controller.Delete(id, CancellationToken.None);

        ObjectResult objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    #endregion
}
