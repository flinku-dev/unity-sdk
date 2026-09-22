using System;
using System.Text.RegularExpressions;

namespace Flinku
{
    /// <summary>
    /// Pure URL/host helpers for baseUrl, customDomain, clipboard, and public short URLs.
    /// Kept free of UnityEngine so unit tests can compile them under .NET.
    /// </summary>
    public static class FlinkuUrlHelpers
    {
        public const string InvalidBaseUrlMessage =
            "Invalid baseUrl: expected https://{subdomain}.flku.dev (e.g. https://myapp.flku.dev)";

        private static readonly Regex BaseUrlRegex =
            new Regex(@"^https?://([^.]+)\.flku\.dev/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool TryParseBaseUrl(
            string baseUrl,
            out string subdomain,
            out string apiBaseUrl,
            out string baseUrlHost,
            out string error)
        {
            subdomain = null;
            apiBaseUrl = null;
            baseUrlHost = null;
            error = null;

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                error = InvalidBaseUrlMessage;
                return false;
            }

            var trimmed = baseUrl.Trim().TrimEnd('/');
            var match = BaseUrlRegex.Match(trimmed);
            if (!match.Success)
            {
                error = InvalidBaseUrlMessage;
                return false;
            }

            subdomain = match.Groups[1].Value;
            apiBaseUrl = "https://flku.dev";
            baseUrlHost = subdomain + ".flku.dev";
            return true;
        }

        /// <summary>
        /// Validates and normalizes a custom domain host (lowercase, no trailing dot).
        /// Returns false with an error message on rejection.
        /// </summary>
        public static bool TryParseCustomDomain(string input, out string host, out string error)
        {
            host = null;
            error = null;

            if (input == null || string.IsNullOrWhiteSpace(input))
            {
                host = null;
                return true; // optional
            }

            if (Regex.IsMatch(input, @"\s"))
            {
                error = "Invalid customDomain: must not contain whitespace";
                return false;
            }

            var d = input.Trim().ToLowerInvariant();
            if (d.EndsWith("."))
                d = d.Substring(0, d.Length - 1);

            if (string.IsNullOrEmpty(d))
            {
                error = "Invalid customDomain: domain is required";
                return false;
            }

            if (d.Contains("://"))
            {
                error = "Invalid customDomain: must not include protocol";
                return false;
            }

            if (d.IndexOf('/') >= 0 || d.IndexOf('?') >= 0 || d.IndexOf('#') >= 0)
            {
                error = "Invalid customDomain: must not include path";
                return false;
            }

            if (d.IndexOf(':') >= 0)
            {
                error = "Invalid customDomain: must not include port";
                return false;
            }

            host = d;
            return true;
        }

        public static string BuildPublicShortUrl(string subdomain, string slug, string customDomain = null)
        {
            if (!string.IsNullOrEmpty(customDomain))
                return "https://" + customDomain + "/" + slug;
            return "https://" + subdomain + ".flku.dev/" + slug;
        }

        public static string ParseClipboardUrlHost(string clipText)
        {
            if (string.IsNullOrWhiteSpace(clipText))
                return null;

            var trimmed = clipText.Trim();
            try
            {
                var withScheme = Regex.IsMatch(trimmed, @"^[a-z][a-z0-9+.-]*://", RegexOptions.IgnoreCase)
                    ? trimmed
                    : "https://" + trimmed;
                var uri = new Uri(withScheme);
                return uri.Host.ToLowerInvariant();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsFlkuDevHost(string host)
        {
            return host == "flku.dev" || host.EndsWith(".flku.dev", StringComparison.Ordinal);
        }

        public static bool IsAcceptedClipboardHost(
            string host,
            string baseUrlHost,
            string customDomain = null)
        {
            if (string.IsNullOrEmpty(host))
                return false;

            var h = host.ToLowerInvariant();
            if (IsFlkuDevHost(h))
                return true;
            if (!string.IsNullOrEmpty(baseUrlHost) &&
                h == baseUrlHost.ToLowerInvariant())
                return true;
            if (!string.IsNullOrEmpty(customDomain) && h == customDomain)
                return true;
            return false;
        }

        public static bool IsClipboardUrlAccepted(
            string clipText,
            string baseUrlHost,
            string customDomain = null)
        {
            var host = ParseClipboardUrlHost(clipText);
            return host != null && IsAcceptedClipboardHost(host, baseUrlHost, customDomain);
        }
    }
}
