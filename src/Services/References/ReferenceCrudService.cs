using System.Text.Json;
using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class ReferenceCrudService : ApiServiceBase, IReferenceCrudService
    {
        private static readonly HashSet<string> StageReadModelUpdateKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "comments",
            "contract",
            "contragent",
            "performers",
            "revision",
            "revisions",
            "stages",
            "status",
            "task_kind",
            "tasks"
        };

        public ReferenceCrudService(HttpClient httpClient, IUserService userService)
            : base(httpClient, userService)
        {
        }

        public Task<ReferenceDataRow> CreateAsync(
            ReferenceDefinition definition,
            IReadOnlyDictionary<string, object?> payload,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(payload);

            var request = BuildRequest(definition, payload);
            return PostAsync<Dictionary<string, object?>, ReferenceDataRow>(
                $"model/add/{definition.Model}",
                request,
                cancellationToken);
        }

        public Task<ReferenceDataRow> UpdateAsync(
            ReferenceDefinition definition,
            IReadOnlyDictionary<string, object?> payload,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(payload);

            var id = ExtractId(payload);
            ValidateUpdatePayload(definition, payload);
            var request = BuildRequest(definition, payload);
            LogTrackedUpdateRequest(definition, id, request);
            return PutAsync<Dictionary<string, object?>, ReferenceDataRow>(
                $"model/{definition.Model}/{id}",
                request,
                cancellationToken);
        }

        public Task<ReferenceDataRow> DeleteAsync(
            ReferenceDefinition definition,
            long id,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(definition);

            return DeleteAsync<ReferenceDataRow>($"model/{definition.Model}/{id}", cancellationToken);
        }

        private static Dictionary<string, object?> BuildRequest(
            ReferenceDefinition definition,
            IReadOnlyDictionary<string, object?> payload)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["data_set"] = string.IsNullOrWhiteSpace(definition.Preset) ? "edit" : definition.Preset,
                [definition.Model] = payload
            };
        }

        private static void ValidateUpdatePayload(
            ReferenceDefinition definition,
            IReadOnlyDictionary<string, object?> payload)
        {
            if (!string.Equals(definition.Model, "Stage", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var readModelKeys = payload.Keys
                .Where(StageReadModelUpdateKeys.Contains)
                .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (readModelKeys.Length == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "Stage update payload contains read-model fields: "
                + string.Join(", ", readModelKeys)
                + ". Use Rails nested attributes such as comments_attributes/tasks_attributes instead of sending edit data_set objects back.");
        }

        private void LogTrackedUpdateRequest(
            ReferenceDefinition definition,
            long id,
            IReadOnlyDictionary<string, object?> request)
        {
            if (!ShouldLogUpdateRequest(definition.Model))
            {
                return;
            }

            var requestUri = $"model/{definition.Model}/{id}";
            DiagnosticsFileLogger.AppendBlock(
                $"{definition.Model.ToUpperInvariant()} UPDATE REQUEST",
                $"method=PUT{Environment.NewLine}uri={requestUri}{Environment.NewLine}payload={SerializeForDiagnostics(request)}");
        }

        private static bool ShouldLogUpdateRequest(string model)
        {
            return string.Equals(model, "Contract", StringComparison.OrdinalIgnoreCase)
                || string.Equals(model, "Stage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(model, "Revision", StringComparison.OrdinalIgnoreCase);
        }

        private string SerializeForDiagnostics(IReadOnlyDictionary<string, object?> request)
        {
            try
            {
                return JsonSerializer.Serialize(request, SerializerOptions);
            }
            catch (Exception ex)
            {
                return $"<serialization failed: {ex.GetType().Name}: {ex.Message}>";
            }
        }

        private static long ExtractId(IReadOnlyDictionary<string, object?> payload)
        {
            if (!payload.TryGetValue("id", out var rawId) || rawId is null)
            {
                throw new InvalidOperationException("Update payload must contain 'id'.");
            }

            return rawId switch
            {
                long int64Value => int64Value,
                int int32Value => int32Value,
                decimal decimalValue => (long)decimalValue,
                string stringValue when long.TryParse(stringValue, out var parsedValue) => parsedValue,
                _ => throw new InvalidOperationException("Update payload contains unsupported 'id' type.")
            };
        }
    }
}
