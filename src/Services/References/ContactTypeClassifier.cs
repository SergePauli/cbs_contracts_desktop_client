using System.Text.RegularExpressions;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed record ContactTypeMatch(string Type, string Glyph, string? UriPrefix);

    public static partial class ContactTypeClassifier
    {
        public static bool TryClassify(string? rawValue, out ContactTypeMatch match)
        {
            var value = rawValue?.Trim() ?? string.Empty;
            if (EmailRegex().IsMatch(value))
            {
                match = new ContactTypeMatch("Email", "\uE715", "mailto:");
                return true;
            }

            if (FaxRegex().IsMatch(value))
            {
                match = new ContactTypeMatch("Fax", "\uE749", null);
                return true;
            }

            if (PhoneRegex().IsMatch(value) || ShortPhoneRegex().IsMatch(value))
            {
                match = new ContactTypeMatch("Phone", "\uE717", "tel:");
                return true;
            }

            if (SiteUrlRegex().IsMatch(value))
            {
                match = new ContactTypeMatch("SiteUrl", "\uE71B", "http://");
                return true;
            }

            if (TelegramRegex().IsMatch(value))
            {
                match = new ContactTypeMatch("Telegram", "\uE74A", "tg://resolve?domain=");
                return true;
            }

            match = new ContactTypeMatch(string.Empty, string.Empty, null);
            return false;
        }

        public static Uri? TryCreateLaunchUri(string value, ContactTypeMatch match)
        {
            if (string.IsNullOrWhiteSpace(match.UriPrefix))
            {
                return null;
            }

            var normalizedValue = match.Type == "Telegram"
                ? value.Trim().TrimStart('@')
                : value.Trim();
            var rawUri = normalizedValue.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || normalizedValue.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || normalizedValue.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                || normalizedValue.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
                || normalizedValue.StartsWith("tg://", StringComparison.OrdinalIgnoreCase)
                    ? normalizedValue
                    : $"{match.UriPrefix}{normalizedValue}";

            return Uri.TryCreate(rawUri, UriKind.Absolute, out var uri)
                ? uri
                : null;
        }

        [GeneratedRegex("""^(([^<>()[\]\\.,;:\s@"]+(\.[^<>()[\]\\.,;:\s@"]+)*)|.(".+"))@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\])|(([a-zA-Z\-0-9]+\.)+[a-zA-Z]{2,}))$""", RegexOptions.IgnoreCase)]
        private static partial Regex EmailRegex();

        [GeneratedRegex("""^fax:((8|\+7)[\- ]?)?\(?\d{3,5}\)?[\- ]?\d{1}[\- ]?\d{1}[\- ]?\d{1}[\- ]?\d{1}[\- ]?\d{1}(([\- ]?\d{1})?[\- ]?\d{1})?""", RegexOptions.IgnoreCase)]
        private static partial Regex FaxRegex();

        [GeneratedRegex("""((8|\+7)[\- ]?)?\(?\d{3,5}\)?[\- ]?\d{1}[\- ]?\d{1}[\- ]?\d{1}[\- ]?\d{1}[\- ]?\d{1}(([\- ]?\d{1})?[\- ]?\d{1})?""", RegexOptions.IgnoreCase)]
        private static partial Regex PhoneRegex();

        [GeneratedRegex("""\d{1}[\- ]?\d{1}[\- ]?\d{1}[\- ]?\d{1}[\- ]?\d{1}(([\- ]?\d{1})?[\- ]?\d{1})?""", RegexOptions.IgnoreCase)]
        private static partial Regex ShortPhoneRegex();

        [GeneratedRegex("""^[a-zA-Z\u0400-\u04FF0-9][a-zA-Z\u0400-\u04FF0-9-]{1,61}[a-zA-Z\u0400-\u04FF0-9](?:\.[a-zA-Z\u0400-\u04FF]{2,})+$""", RegexOptions.IgnoreCase)]
        private static partial Regex SiteUrlRegex();

        [GeneratedRegex(""".*\B@(?=\w{5,64}\b)[a-zA-Z0-9]+(?:_[a-zA-Z0-9]+)*.*""", RegexOptions.IgnoreCase)]
        private static partial Regex TelegramRegex();
    }
}
