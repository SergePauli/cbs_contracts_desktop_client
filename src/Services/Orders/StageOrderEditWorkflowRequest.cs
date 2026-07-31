using System.Text.Json;
using Microsoft.UI.Xaml;

namespace CbsContractsDesktopClient.Services.Orders
{
    public sealed class StageOrderEditWorkflowRequest
    {
        public required XamlRoot XamlRoot { get; init; }
        public long? OrderId { get; init; }
        public long? StageId { get; init; }
        public string StageLabel { get; init; } = string.Empty;
        public JsonElement? SourceRow { get; init; }
        public required Action<string> Trace { get; init; }
        public StageOrderEditAccessMode AccessMode { get; init; } = StageOrderEditAccessMode.Full;
        public bool IsCreateMode => SourceRow is null;
        public bool IsStageFixed => StageId is not null;
    }

    public enum StageOrderEditAccessMode
    {
        Full,
        ControlFieldsOnly
    }
}
