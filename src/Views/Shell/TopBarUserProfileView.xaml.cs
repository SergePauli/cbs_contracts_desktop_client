using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class TopBarUserProfileView : UserControl
    {
        private readonly IUserService _userService;

        public TopBarUserProfileView()
        {
            _userService = App.Services.GetRequiredService<IUserService>();
            InitializeComponent();

            if (_userService is INotifyPropertyChanged observableUserService)
            {
                observableUserService.PropertyChanged += OnUserServicePropertyChanged;
            }
        }

        public string DepartmentLabel => FormatValue(CurrentUser?.DepartmentName, FormatValue(CurrentUser?.Username, "Профиль"));

        public string ProfileToolTip => $"Профиль: {DepartmentLabel}";

        public string UsernameText => FormatValue(CurrentUser?.Username);

        public string FullNameText => FormatValue(CurrentUser?.FullName);

        public string EmailText => FormatValue(CurrentUser?.Email);

        public string RolesText => FormatRoles(CurrentUser?.Role);

        private User? CurrentUser => _userService.CurrentUser;

        private void OnUserServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IUserService.CurrentUser))
            {
                Bindings.Update();
            }
        }

        private static string FormatValue(string? value, string emptyText = "-")
        {
            return string.IsNullOrWhiteSpace(value) ? emptyText : value.Trim();
        }

        private static string FormatRoles(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return string.Join(
                ", ",
                value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(static role => !string.IsNullOrWhiteSpace(role)));
        }
    }
}
