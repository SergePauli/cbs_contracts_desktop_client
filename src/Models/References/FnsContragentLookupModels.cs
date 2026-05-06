using System.Text.Json.Serialization;

namespace CbsContractsDesktopClient.Models.References
{
    public sealed class FnsResponse
    {
        [JsonPropertyName("items")]
        public IReadOnlyList<FnsResponseItem> Items { get; init; } = [];
    }

    public sealed class FnsResponseItem
    {
        [JsonPropertyName("ЮЛ")]
        public FnsLegalEntity? LegalEntity { get; init; }
    }

    public sealed class FnsLegalEntity
    {
        [JsonPropertyName("ИНН")]
        public string? Inn { get; init; }

        [JsonPropertyName("КПП")]
        public string? Kpp { get; init; }

        [JsonPropertyName("ОГРН")]
        public string? Ogrn { get; init; }

        [JsonPropertyName("ОКОПФ")]
        public string? OkopfName { get; init; }

        [JsonPropertyName("КодОКОПФ")]
        public string? OkopfCode { get; init; }

        [JsonPropertyName("Статус")]
        public string? Status { get; init; }

        [JsonPropertyName("НаимСокрЮЛ")]
        public string? ShortName { get; init; }

        [JsonPropertyName("НаимПолнЮЛ")]
        public string? FullName { get; init; }

        [JsonPropertyName("Адрес")]
        public FnsAddress? Address { get; init; }

        [JsonPropertyName("КодыСтат")]
        public FnsStatCodes? StatCodes { get; init; }

        [JsonPropertyName("Контакты")]
        public object? Contacts { get; init; }

        [JsonPropertyName("Филиалы")]
        public IReadOnlyList<FnsBranch> Branches { get; init; } = [];
    }

    public sealed class FnsAddress
    {
        [JsonPropertyName("КодРегион")]
        public string? RegionCode { get; init; }

        [JsonPropertyName("Индекс")]
        public string? PostalCode { get; init; }

        [JsonPropertyName("АдресПолн")]
        public string? FullAddress { get; init; }

        [JsonPropertyName("ИдНомФИАС")]
        public string? FiasNumberId { get; init; }
    }

    public sealed class FnsStatCodes
    {
        [JsonPropertyName("ОКПО")]
        public string? Okpo { get; init; }

        [JsonPropertyName("ОКТМО")]
        public string? Oktmo { get; init; }

        [JsonPropertyName("ОКФС")]
        public string? Okfc { get; init; }

        [JsonPropertyName("ОКОГУ")]
        public string? Okogu { get; init; }
    }

    public sealed class FnsBranch
    {
        [JsonPropertyName("Адрес")]
        public string? Address { get; init; }

        [JsonPropertyName("КПП")]
        public string? Kpp { get; init; }

        [JsonPropertyName("Индекс")]
        public string? PostalCode { get; init; }

        [JsonPropertyName("Наименование")]
        public string? Name { get; init; }
    }

    public sealed class FnsContragentLookupResult
    {
        public string ObjUuid { get; init; } = Guid.NewGuid().ToString();

        public required FnsContragentOrganization Organization { get; init; }

        public string RequisitesListKey { get; init; } = Guid.NewGuid().ToString();

        public FnsContragentAddress RealAddress { get; init; } = new()
        {
            Kind = "real"
        };

        public FnsContragentAddress RegisteredAddress { get; init; } = new()
        {
            Kind = "registred"
        };

        public FnsLookupItem? Region { get; init; }

        public IReadOnlyList<FnsContactItem> Contacts { get; init; } = [];

        public string? Description { get; init; }

        public IReadOnlyDictionary<string, object?> ToContragentPayload()
        {
            return new Dictionary<string, object?>
            {
                ["obj_uuid"] = ObjUuid,
                ["description"] = Description,
                ["region_id"] = Region?.Id,
                ["contragent_organizations_attributes"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["list_key"] = RequisitesListKey,
                        ["used"] = true,
                        ["organization_attributes"] = Organization.ToPayload()
                    }
                },
                ["contragent_addresses_attributes"] = new[]
                {
                    RealAddress.ToPayload(),
                    RegisteredAddress.ToPayload()
                },
                ["contragent_contacts_attributes"] = Contacts
                    .Select(static contact => contact.ToPayload())
                    .ToList()
            };
        }
    }

    public sealed class FnsContragentOrganization
    {
        public string? Name { get; init; }

        public string? FullName { get; init; }

        public string? Inn { get; init; }

        public string? Kpp { get; init; }

        public string? Okopf { get; init; }

        public string? Ogrn { get; init; }

        public string? Okfc { get; init; }

        public string? Okogu { get; init; }

        public string? Okpo { get; init; }

        public string? Oktmo { get; init; }

        public long? OwnershipId { get; init; }

        public string? OwnershipOkopf { get; init; }

        public IReadOnlyDictionary<string, object?> ToPayload()
        {
            return new Dictionary<string, object?>
            {
                ["name"] = Name,
                ["full_name"] = FullName,
                ["inn"] = Inn,
                ["kpp"] = Kpp,
                ["okopf"] = Okopf,
                ["ogrn"] = Ogrn,
                ["okfc"] = Okfc,
                ["okogu"] = Okogu,
                ["okpo"] = Okpo,
                ["oktmo"] = Oktmo,
                ["ownership_id"] = OwnershipId
            };
        }
    }

    public sealed class FnsContragentAddress
    {
        public string Kind { get; init; } = string.Empty;

        public string ListKey { get; init; } = Guid.NewGuid().ToString();

        public string? Value { get; init; }

        public long? AreaId { get; init; }

        public IReadOnlyDictionary<string, object?> ToPayload()
        {
            return new Dictionary<string, object?>
            {
                ["kind"] = Kind,
                ["list_key"] = ListKey,
                ["address_attributes"] = new Dictionary<string, object?>
                {
                    ["value"] = Value,
                    ["area_id"] = AreaId
                }
            };
        }
    }

    public sealed class FnsLookupItem
    {
        public long? Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    public sealed class FnsContactItem
    {
        public string ListKey { get; init; } = Guid.NewGuid().ToString();

        public required string Value { get; init; }

        public required string Type { get; init; }

        public IReadOnlyDictionary<string, object?> ToPayload()
        {
            return new Dictionary<string, object?>
            {
                ["list_key"] = ListKey,
                ["contact_attributes"] = new Dictionary<string, object?>
                {
                    ["value"] = Value,
                    ["type"] = Type
                }
            };
        }
    }
}
