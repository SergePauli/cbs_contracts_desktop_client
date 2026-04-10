using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using CbsContractsDesktopClient.Services;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class FooterBarView : UserControl
    {
        public FooterBarView()
        {
            InitializeComponent();

            var userService = App.Services.GetRequiredService<IUserService>();
            var user = userService.CurrentUser;
            var department = string.IsNullOrWhiteSpace(user?.DepartmentName) ? "ќтдел не определен" : user!.DepartmentName;
            var role = string.IsNullOrWhiteSpace(user?.Role) ? "роль не определена" : $"роль: {user!.Role}";
            FooterSummary = $"{department} Х {role}";
        }

        public string FooterSummary { get; }
    }
}
