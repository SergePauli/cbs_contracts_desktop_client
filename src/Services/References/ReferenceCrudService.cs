using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class ReferenceCrudService : ApiServiceBase, IReferenceCrudService
    {
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
            var request = BuildRequest(definition, payload);
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
