using System.Net;
using System.Net.Http.Json;
using Api.Models;
using Api.Services;

namespace Api.Tests;

/// <summary>LeaveService.DaysTakenThisYearAsync: only approved leave counts as taken.</summary>
public class DaysTakenThisYearTests
{
    const int Emp = 10; // no seed leave

    static async Task<(decimal taken, decimal days)> TakenWith(LeaveStatus status)
    {
        using var db = TestDb.Create();
        var monday = Api.Data.Seed.MondayOnOrAfter(new DateOnly(DateTime.UtcNow.Year, 3, 2));
        var request = new LeaveRequest { EmployeeId = Emp, StartDate = monday, EndDate = monday.AddDays(4), Status = status };
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync();
        return (await new LeaveService(db).DaysTakenThisYearAsync(Emp), LeaveService.DaysOf(request));
    }

    [Fact]
    public async Task Approved_leave_counts_as_taken()
    {
        var (taken, days) = await TakenWith(LeaveStatus.Approved);
        Assert.True(days > 0);
        Assert.Equal(days, taken);
    }

    [Fact]
    public async Task Pending_leave_is_not_taken()
    {
        var (taken, _) = await TakenWith(LeaveStatus.Pending);
        Assert.Equal(0, taken);
    }

    [Fact]
    public async Task Rejected_leave_is_not_taken()
    {
        var (taken, _) = await TakenWith(LeaveStatus.Rejected);
        Assert.Equal(0, taken);
    }
}

/// <summary>GET /employees/{id}/summary over HTTP: AccessService decides, unknown ids are 404.</summary>
public class EmployeeSummaryApiTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    record Summary(int EmployeeId, decimal DaysTakenThisYear);

    [Fact]
    public async Task Employee_sees_their_own_summary()
    {
        var farah = await api.SignedInAs(People.Farah);
        var res = await farah.GetAsync("/employees/6/summary");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(6, (await res.Content.ReadFromJsonAsync<Summary>())!.EmployeeId);
    }

    [Fact]
    public async Task Another_employee_gets_403()
    {
        var farah = await api.SignedInAs(People.Farah);
        Assert.Equal(HttpStatusCode.Forbidden, (await farah.GetAsync("/employees/7/summary")).StatusCode);
    }

    [Fact]
    public async Task Unknown_employee_is_404()
    {
        var hr = await api.SignedInAs(People.Dina);
        Assert.Equal(HttpStatusCode.NotFound, (await hr.GetAsync("/employees/99999/summary")).StatusCode);
    }
}
