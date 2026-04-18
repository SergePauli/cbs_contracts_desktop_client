using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.ViewModels.References
{
    public partial class ReferenceEditFieldViewModel : ObservableObject
    {
        private readonly object? _originalValue;

        public ReferenceEditFieldViewModel(
            ReferenceFieldDefinition definition,
            bool isCreateMode,
            object? initialValue = null)
        {
            Definition = definition;
            IsCreateMode = isCreateMode;
            _originalValue = NormalizeValue(initialValue);

            if (IsBooleanEditor)
            {
                BoolValue = _originalValue as bool? ?? false;
            }
            else
            {
                TextValue = FormatInitialValue(_originalValue);
            }

            RefreshState();
        }

        public ReferenceFieldDefinition Definition { get; }

        public bool IsCreateMode { get; }

        public string FieldKey => Definition.FieldKey;

        public string Label => Definition.Label;

        public string DisplayLabel => Definition.IsRequired ? $"{Definition.Label} *" : Definition.Label;

        public string? ApiField => Definition.ApiField;

        public ReferenceFieldEditorType EditorType => Definition.EditorType;

        public bool IsRequired => Definition.IsRequired;

        public bool IsBooleanEditor => EditorType == ReferenceFieldEditorType.Boolean;

        public bool IsTextEditor => !IsBooleanEditor;

        public bool IsReadOnly => IsCreateMode
            ? Definition.IsReadOnlyOnCreate
            : Definition.IsReadOnlyOnEdit;

        public bool IsEditableBoolean => IsBooleanEditor && !IsReadOnly;

        public object? CurrentValue => GetCurrentValue();

        public bool HasValue => CurrentValue is not null;

        public bool IsDirty => !AreEquivalent(_originalValue, CurrentValue);

        public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

        [ObservableProperty]
        public partial string TextValue { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool BoolValue { get; set; }

        [ObservableProperty]
        public partial string ValidationMessage { get; set; } = string.Empty;

        partial void OnTextValueChanged(string value)
        {
            RefreshState();
        }

        partial void OnBoolValueChanged(bool value)
        {
            RefreshState();
        }

        private void RefreshState()
        {
            ValidationMessage = BuildValidationMessage();
            OnPropertyChanged(nameof(CurrentValue));
            OnPropertyChanged(nameof(HasValue));
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(HasValidationError));
        }

        private string BuildValidationMessage()
        {
            if (IsReadOnly)
            {
                return string.Empty;
            }

            if (EditorType == ReferenceFieldEditorType.Number)
            {
                if (!string.IsNullOrWhiteSpace(TextValue) && TryParseNumber(TextValue, out _) is false)
                {
                    return "Введите корректное числовое значение.";
                }
            }

            if (IsRequired && CurrentValue is null)
            {
                return "Поле обязательно для заполнения.";
            }

            return string.Empty;
        }

        private object? GetCurrentValue()
        {
            if (IsBooleanEditor)
            {
                return BoolValue;
            }

            if (string.IsNullOrWhiteSpace(TextValue))
            {
                return null;
            }

            if (EditorType == ReferenceFieldEditorType.Number)
            {
                return TryParseNumber(TextValue, out var number)
                    ? number
                    : TextValue.Trim();
            }

            return TextValue.Trim();
        }

        private static object? NormalizeValue(object? value)
        {
            return value switch
            {
                null => null,
                string text when string.IsNullOrWhiteSpace(text) => null,
                string text => text.Trim(),
                bool booleanValue => booleanValue,
                sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToInt64(value, CultureInfo.InvariantCulture),
                float or double or decimal => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }

        private static bool TryParseNumber(string rawValue, out object? number)
        {
            var normalizedValue = rawValue.Trim().Replace(',', '.');

            if (long.TryParse(normalizedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                number = integer;
                return true;
            }

            if (decimal.TryParse(normalizedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
            {
                number = decimalValue;
                return true;
            }

            number = null;
            return false;
        }

        private static bool AreEquivalent(object? left, object? right)
        {
            if (left is null && right is null)
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            if (left is decimal leftDecimal && right is long rightLong)
            {
                return leftDecimal == rightLong;
            }

            if (left is long leftLong && right is decimal rightDecimal)
            {
                return leftLong == rightDecimal;
            }

            return Equals(left, right);
        }

        private static string FormatInitialValue(object? value)
        {
            return value switch
            {
                null => string.Empty,
                decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty
            };
        }
    }
}
