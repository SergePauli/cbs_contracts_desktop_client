using System.Threading;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.ViewModels.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ProfileEditViewModelTests
{
    [Fact]
    public void CommitPositionInput_CapitalizesManualValue_WhenOptionsAreMissing()
    {
        var viewModel = CreateViewModel();

        viewModel.CommitPositionInput("manager");

        Assert.Equal("Manager", viewModel.PositionName);
        Assert.Equal("Manager", viewModel.PositionInput);
        Assert.Null(viewModel.SelectedPositionOption);
    }

    [Fact]
    public async Task UpdatePositionOptionsAsync_DoesNotOpenAutocomplete_ForPersistedStateValue()
    {
        var loaderCalls = 0;
        var viewModel = CreateViewModel(
            loader: (_, _) =>
            {
                loaderCalls++;
                return Task.FromResult<IReadOnlyList<CbsTableFilterOptionDefinition>>(
                [
                    new CbsTableFilterOptionDefinition
                    {
                        Value = 7L,
                        Label = "Senior Manager"
                    }
                ]);
            },
            positionName: "Senior Manager");

        await viewModel.UpdatePositionOptionsAsync("Senior Manager");

        Assert.Equal(0, loaderCalls);
        Assert.Empty(viewModel.PositionOptions);
        Assert.Equal("Senior Manager", viewModel.PositionName);
    }

    [Fact]
    public async Task CommitPositionInput_RequiresExistingOption_WhenLookupReturnsMatches()
    {
        var viewModel = CreateViewModel(static (_, _) =>
            Task.FromResult<IReadOnlyList<CbsTableFilterOptionDefinition>>(
            [
                new CbsTableFilterOptionDefinition
                {
                    Value = 7L,
                    Label = "Senior Manager"
                }
            ]));

        await viewModel.UpdatePositionOptionsAsync("man");
        viewModel.CommitPositionInput("manager");

        Assert.Equal(string.Empty, viewModel.PositionName);
        Assert.Equal("manager", viewModel.PositionInput);
        Assert.Null(viewModel.SelectedPositionOption);
    }

    [Fact]
    public async Task CommitPositionInput_AcceptsExactLookupOption()
    {
        var viewModel = CreateViewModel(static (_, _) =>
            Task.FromResult<IReadOnlyList<CbsTableFilterOptionDefinition>>(
            [
                new CbsTableFilterOptionDefinition
                {
                    Value = 7L,
                    Label = "Senior Manager"
                }
            ]));

        await viewModel.UpdatePositionOptionsAsync("sen");
        viewModel.CommitPositionInput("senior manager");

        Assert.Equal("Senior Manager", viewModel.PositionName);
        Assert.Equal("Senior Manager", viewModel.PositionInput);
        Assert.NotNull(viewModel.SelectedPositionOption);
        Assert.Equal(7L, viewModel.PositionId);
    }

    [Fact]
    public async Task PositionSuggestionLabels_ReturnsLoadedLabels()
    {
        var viewModel = CreateViewModel(static (_, _) =>
            Task.FromResult<IReadOnlyList<CbsTableFilterOptionDefinition>>(
            [
                new CbsTableFilterOptionDefinition
                {
                    Value = 7L,
                    Label = "Senior Manager"
                },
                new CbsTableFilterOptionDefinition
                {
                    Value = 8L,
                    Label = "Tech Director"
                }
            ]));

        await viewModel.UpdatePositionOptionsAsync("r");

        Assert.Equal(["Senior Manager", "Tech Director"], viewModel.PositionSuggestionLabels);
        Assert.Equal(8L, viewModel.FindPositionOption("Tech Director")?.Value);
        Assert.Equal(2, viewModel.PositionOptions.Count);
    }

    private static ProfileEditViewModel CreateViewModel(
        Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>>? loader = null,
        string positionName = "")
    {
        return new ProfileEditViewModel(
            new ProfileEditDialogState
            {
                Definition = new ReferenceDefinition
                {
                    Route = "/users",
                    Model = "Profile",
                    Title = "Users",
                    EditorKind = ReferenceEditorKind.Profile
                },
                PositionName = positionName
            },
            loader);
    }
}
