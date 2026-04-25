using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.ViewModels.References
{
    public partial class ReferenceEditViewModel : ObservableObject
    {
        public ReferenceEditViewModel(
            ReferenceDefinition definition,
            bool isCreateMode,
            ReferenceDataRow? sourceRow = null)
        {
            Definition = definition;
            IsCreateMode = isCreateMode;
            SourceRow = sourceRow;

            Fields = new ObservableCollection<ReferenceEditFieldViewModel>(
                definition.Fields.Select(definitionItem => new ReferenceEditFieldViewModel(
                    definitionItem,
                    isCreateMode,
                    sourceRow?.GetValue(definitionItem.ApiField ?? definitionItem.FieldKey)))
                .Where(item => !(isCreateMode && string.Equals(item.FieldKey, "id", StringComparison.OrdinalIgnoreCase))));

            foreach (var item in Fields)
            {
                item.PropertyChanged += OnFieldPropertyChanged;
            }

            ValidateCrossFieldRules();
        }

        public ReferenceDefinition Definition { get; }

        public bool IsCreateMode { get; }

        public ReferenceDataRow? SourceRow { get; }

        public ObservableCollection<ReferenceEditFieldViewModel> Fields { get; }

        public IEnumerable<ReferenceEditFieldViewModel> EditableFields =>
            Fields.Where(static item => !item.IsReadOnly);

        public IEnumerable<ReferenceEditFieldViewModel> DirtyFields =>
            EditableFields.Where(static item => item.IsDirty);

        public string DialogTitle => IsCreateMode
            ? $"{Definition.Title}: новая запись"
            : $"{Definition.Title}: редактирование";

        public string DescriptionText => IsCreateMode
            ? "Заполните поля новой записи"
            : "Измените только нужные поля";

        public string PrimaryButtonText => IsCreateMode ? "Создать" : "Сохранить";

        public bool HasChanges => IsCreateMode
            ? EditableFields.Any(static item => item.HasValue)
            : DirtyFields.Any();

        public bool HasValidationErrors => Fields.Any(static item => item.HasValidationError);

        public bool RequiredFieldsSatisfied => EditableFields
            .Where(static item => item.IsRequired)
            .All(static item => item.HasValue && !item.HasValidationError);

        public bool CanSubmit => !HasValidationErrors
            && RequiredFieldsSatisfied
            && (IsCreateMode || HasChanges);

        public static ReferenceEditViewModel CreateForCreate(ReferenceDefinition definition)
        {
            return new ReferenceEditViewModel(definition, isCreateMode: true);
        }

        public static ReferenceEditViewModel CreateForEdit(ReferenceDefinition definition, ReferenceDataRow sourceRow)
        {
            return new ReferenceEditViewModel(definition, isCreateMode: false, sourceRow);
        }

        private void OnFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            ValidateCrossFieldRules();
            OnPropertyChanged(nameof(EditableFields));
            OnPropertyChanged(nameof(DirtyFields));
            OnPropertyChanged(nameof(HasChanges));
            OnPropertyChanged(nameof(HasValidationErrors));
            OnPropertyChanged(nameof(RequiredFieldsSatisfied));
            OnPropertyChanged(nameof(CanSubmit));
        }

        private void ValidateCrossFieldRules()
        {
            var beginAtField = Fields.FirstOrDefault(static item =>
                string.Equals(item.FieldKey, "begin_at", StringComparison.OrdinalIgnoreCase));
            var endAtField = Fields.FirstOrDefault(static item =>
                string.Equals(item.FieldKey, "end_at", StringComparison.OrdinalIgnoreCase));

            if (beginAtField is null || endAtField is null)
            {
                return;
            }

            if (beginAtField.DateValue is DateTimeOffset beginAt
                && endAtField.DateValue is DateTimeOffset endAt
                && beginAt.Date > endAt.Date)
            {
                endAtField.SetExternalValidationMessage("Поле <окончание> должно быть не раньше начала.");
                return;
            }

            endAtField.SetExternalValidationMessage(null);
        }
    }
}
