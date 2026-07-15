using Wallow.Shared.Kernel.Pagination;

namespace Wallow.Shared.Kernel.Tests.Results;

public class PagedResultTests
{
    [Fact]
    public void Constructor_WithValidData_SetsProperties()
    {
        List<string> items = ["a", "b", "c"];

        PagedResult<string> result = new(items, TotalCount: 10, Page: 1, PageSize: 3);

        result.Items.Should().BeEquivalentTo(items);
        result.TotalCount.Should().Be(10);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(3);
    }

    [Fact]
    public void TotalPages_WithExactDivision_ReturnsCorrectCount()
    {
        PagedResult<int> result = new([], TotalCount: 20, Page: 1, PageSize: 5);

        result.TotalPages.Should().Be(4);
    }

    [Fact]
    public void TotalPages_WithRemainder_RoundsUp()
    {
        PagedResult<int> result = new([], TotalCount: 7, Page: 1, PageSize: 3);

        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public void TotalPages_WithZeroPageSize_ReturnsZero()
    {
        PagedResult<int> result = new([], TotalCount: 10, Page: 1, PageSize: 0);

        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public void TotalPages_WithZeroTotalCount_ReturnsZero()
    {
        PagedResult<int> result = new([], TotalCount: 0, Page: 1, PageSize: 10);

        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public void HasNextPage_WhenOnFirstOfMultiplePages_ReturnsTrue()
    {
        PagedResult<int> result = new([], TotalCount: 30, Page: 1, PageSize: 10);

        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_WhenOnLastPage_ReturnsFalse()
    {
        PagedResult<int> result = new([], TotalCount: 30, Page: 3, PageSize: 10);

        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void HasNextPage_WhenSinglePage_ReturnsFalse()
    {
        PagedResult<int> result = new([], TotalCount: 5, Page: 1, PageSize: 10);

        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_WhenOnFirstPage_ReturnsFalse()
    {
        PagedResult<int> result = new([], TotalCount: 30, Page: 1, PageSize: 10);

        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_WhenOnSecondPage_ReturnsTrue()
    {
        PagedResult<int> result = new([], TotalCount: 30, Page: 2, PageSize: 10);

        result.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void EmptyResult_HasNoItems()
    {
        PagedResult<string> result = new([], TotalCount: 0, Page: 1, PageSize: 10);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void SingleItemResult_HasCorrectPagination()
    {
        PagedResult<string> result = new(["only"], TotalCount: 1, Page: 1, PageSize: 10);

        result.Items.Should().HaveCount(1);
        result.TotalPages.Should().Be(1);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
    }
}
