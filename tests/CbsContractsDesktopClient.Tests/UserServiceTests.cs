using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Services;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public class UserServiceTests
{
    [Fact]
    public void SetCurrentUser_MarksServiceAsAuthenticated()
    {
        var service = new UserService();

        service.SetCurrentUser(new User
        {
            Id = 1,
            Username = "tester",
            FullName = "Test User",
            Role = "admin"
        });

        Assert.True(service.IsAuthenticated);
        Assert.NotNull(service.CurrentUser);
        Assert.Equal("tester", service.CurrentUser!.Username);
    }

    [Fact]
    public void ClearCurrentUser_RemovesAuthenticationState()
    {
        var service = new UserService();
        service.SetCurrentUser(new User { Username = "tester" });

        service.ClearCurrentUser();

        Assert.False(service.IsAuthenticated);
        Assert.Null(service.CurrentUser);
    }

    [Fact]
    public void HasRole_IsCaseInsensitive_ForCurrentUserRole()
    {
        var service = new UserService();
        service.SetCurrentUser(new User { Role = "Admin" });

        var hasRole = service.HasRole("admin");

        Assert.True(hasRole);
    }
}
