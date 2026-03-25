using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Identity.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Wallow.Identity.Tests.Api.Controllers;

public class RolesControllerTests
{
    private readonly RoleManager<WallowRole> _roleManager;
    private readonly IRolePermissionLookup _rolePermissionLookup;
    private readonly RolesController _controller;

    public RolesControllerTests()
    {
        IRoleStore<WallowRole> roleStore = Substitute.For<IRoleStore<WallowRole>>();
        _roleManager = Substitute.For<RoleManager<WallowRole>>(
            roleStore, null, null, null, null);
        _rolePermissionLookup = Substitute.For<IRolePermissionLookup>();
        _controller = new RolesController(_roleManager, _rolePermissionLookup);
    }

    #region GetRoles

    [Fact]
    public async Task GetRoles_ReturnsRolesFromDatabase()
    {
        List<WallowRole> roles =
        [
            new WallowRole { Name = "admin" },
            new WallowRole { Name = "user" }
        ];
        _roleManager.Roles.Returns(new TestAsyncEnumerable<WallowRole>(roles));

        ActionResult result = await _controller.GetRoles(CancellationToken.None);

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    #endregion

    #region GetRolePermissions

    [Fact]
    public void GetRolePermissions_ReturnsPermissionsForRole()
    {
        IReadOnlyCollection<string> permissions = new[] { PermissionType.UsersRead, PermissionType.UsersCreate };
        _rolePermissionLookup.GetPermissions(Arg.Is<IEnumerable<string>>(r => r.Contains("admin")))
            .Returns(permissions);

        ActionResult result = _controller.GetRolePermissions("admin");

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    [Fact]
    public void GetRolePermissions_WithNoPermissions_ReturnsEmptyList()
    {
        _rolePermissionLookup.GetPermissions(Arg.Any<IEnumerable<string>>())
            .Returns([]);

        ActionResult result = _controller.GetRolePermissions("guest");

        OkObjectResult ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    #endregion
}

internal sealed class TestAsyncEnumerable<T>(IEnumerable<T> source) : EnumerableQuery<T>(source), IAsyncEnumerable<T>, IQueryable<T>
{
    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }
}

internal sealed class TestAsyncQueryProvider<T>(IQueryable<T> source) : IAsyncQueryProvider, IQueryProvider
{
    private readonly IQueryProvider _inner = source.Provider;

    public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<T>(Expression.Lambda<Func<IEnumerable<T>>>(expression).Compile()());

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return new TestAsyncEnumerable<TElement>(
            _inner.CreateQuery<TElement>(expression));
    }

    public object? Execute(Expression expression) => _inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => _inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        Type resultType = typeof(TResult).GetGenericArguments().First();
        object? enumerable = _inner.CreateQuery(expression);
        object? result = typeof(Enumerable)
            .GetMethod(nameof(Enumerable.ToList))!
            .MakeGenericMethod(resultType)
            .Invoke(null, [enumerable]);

        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(result!.GetType())
            .Invoke(null, [result])!;
    }
}

internal sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;

    public ValueTask DisposeAsync()
    {
        inner.Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> MoveNextAsync()
    {
        return new ValueTask<bool>(inner.MoveNext());
    }
}
