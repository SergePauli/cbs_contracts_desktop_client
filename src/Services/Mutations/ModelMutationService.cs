// Contains the shared model mutation transport for create/update/delete requests.
// Domain payload shape stays in explicit payload builders; this service only wraps and sends mutations.
// Rails mutation responses are used only as acknowledgement/new-record identity; full reads belong to the GO read service.
using System.Text.Json;
using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.Services.Mutations
{
    public sealed class ModelMutationService : ApiServiceBase, IModelMutationService
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

        public ModelMutationService(
            HttpClient httpClient,
            IUserService userService,
            IAccessTokenRefreshService? accessTokenRefreshService = null)
            : base(httpClient, userService, accessTokenRefreshService)
        {
        }

        public Task<TableDataRow> CreateAsync(
            string model,
            IReadOnlyDictionary<string, object?> payload,
            CancellationToken cancellationToken = default)
        {
            ValidateModel(model);
            ArgumentNullException.ThrowIfNull(payload);

            var request = BuildRequest(model, payload);
            return PostAsync<Dictionary<string, object?>, TableDataRow>(
                $"model/add/{model}",
                request,
                cancellationToken);
        }

        public Task<TableDataRow> UpdateAsync(
            string model,
            IReadOnlyDictionary<string, object?> payload,
            CancellationToken cancellationToken = default)
        {
            ValidateModel(model);
            ArgumentNullException.ThrowIfNull(payload);

            var id = ExtractId(payload);
            ValidateUpdatePayload(model, payload);
            var request = BuildRequest(model, payload);
            LogTrackedUpdateRequest(model, id, request);
            return PutAsync<Dictionary<string, object?>, TableDataRow>(
                $"model/{model}/{id}",
                request,
                cancellationToken);
        }

        public Task<TableDataRow> DeleteAsync(
            string model,
            long id,
            CancellationToken cancellationToken = default)
        {
            ValidateModel(model);

            return DeleteAsync<TableDataRow>($"model/{model}/{id}", cancellationToken);
        }

        private static Dictionary<string, object?> BuildRequest(
            string model,
            IReadOnlyDictionary<string, object?> payload)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                // Rails mutation endpoints only need a minimal item-shaped response.
                ["data_set"] = "item",
                [model] = payload
            };
        }

        private static void ValidateUpdatePayload(
            string model,
            IReadOnlyDictionary<string, object?> payload)
        {
            if (!string.Equals(model, "Stage", StringComparison.OrdinalIgnoreCase))
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
            string model,
            long id,
            IReadOnlyDictionary<string, object?> request)
        {
            if (!ShouldLogUpdateRequest(model))
            {
                return;
            }

            var requestUri = $"model/{model}/{id}";
            DiagnosticsFileLogger.AppendBlock(
                $"{model.ToUpperInvariant()} UPDATE REQUEST",
                $"method=PUT{Environment.NewLine}uri={requestUri}{Environment.NewLine}payload={SerializeForDiagnostics(request)}");
        }

        private static void ValidateModel(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                throw new ArgumentException("Model name is required.", nameof(model));
            }
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
