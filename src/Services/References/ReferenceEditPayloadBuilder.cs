using CbsContractsDesktopClient.ViewModels.References;

namespace CbsContractsDesktopClient.Services.References
{
    public static class ReferenceEditPayloadBuilder
    {
        public static IReadOnlyDictionary<string, object?> BuildForCreate(ReferenceEditViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            return Build(viewModel.EditableFields.Where(static item => item.HasValue));
        }

        public static IReadOnlyDictionary<string, object?> BuildForUpdate(ReferenceEditViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            var fields = viewModel.DirtyFields.ToList();
            var idField = viewModel.Fields.FirstOrDefault(static item =>
                string.Equals(item.FieldKey, "id", StringComparison.OrdinalIgnoreCase));

            if (idField is not null && idField.HasValue)
            {
                fields.Insert(0, idField);
            }

            return Build(fields);
        }

        private static IReadOnlyDictionary<string, object?> Build(IEnumerable<ReferenceEditFieldViewModel> fields)
        {
            return fields.ToDictionary(
                static item => item.ApiField ?? item.FieldKey,
                static item => item.CurrentValue,
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
