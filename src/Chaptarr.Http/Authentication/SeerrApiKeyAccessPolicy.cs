using System;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Chaptarr.Http.Authentication
{
    /// <summary>
    /// Limits the optional SeerrNG service credential to book-library operations.
    /// </summary>
    public static class SeerrApiKeyAccessPolicy
    {
        private const string ApiPrefix = "/api/v1";
        private const long MaxCommandBodyBytes = 8192;
        private static readonly Regex ReadarrFacadePath = new Regex(
            @"^/readarr/(?:hc|gr)/(?:ebook|audiobook)/api/v1(?<suffix>/.*)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static async Task<bool> IsAllowedAsync(HttpRequest request)
        {
            if (request == null)
            {
                return false;
            }

            var path = request.Path.Value?.TrimEnd('/');
            if (string.IsNullOrEmpty(path) ||
                path.Length > 2048)
            {
                return false;
            }

            if (string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase) &&
                IsMediaCoverPath(path))
            {
                return true;
            }

            var suffix = GetApiSuffix(path);
            if (suffix == null)
            {
                return false;
            }

            var method = request.Method;
            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                return IsReadPathAllowed(suffix, request);
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                if (IsExactPath(suffix, "/book"))
                {
                    return true;
                }

                return IsExactPath(suffix, "/command") && await IsBookSearchCommandAsync(request);
            }

            if (string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase))
            {
                return IsNumericItemPath(suffix, "/book");
            }

            if (string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase))
            {
                return IsNumericItemPath(suffix, "/pendingauthorimport");
            }

            return false;
        }

        private static string GetApiSuffix(string path)
        {
            if (path.StartsWith(ApiPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var suffix = path.Substring(ApiPrefix.Length);
                return suffix.Length == 0 || suffix[0] == '/' ? suffix : null;
            }

            var facade = ReadarrFacadePath.Match(path);
            return facade.Success ? facade.Groups["suffix"].Value : null;
        }

        private static bool IsReadPathAllowed(string path, HttpRequest request)
        {
            if (IsExactPath(path, "/history") || IsExactPath(path, "/bookfile"))
            {
                return HasSinglePositiveIdQuery(request, "bookId");
            }

            switch (path.ToLowerInvariant())
            {
                case "/system/status":
                case "/system/capabilities":
                case "/rootfolder":
                case "/qualityprofile":
                case "/metadataprofile":
                case "/tag":
                case "/book":
                case "/book/paged":
                case "/book/lookup":
                case "/author":
                case "/author/lookup":
                case "/edition":
                case "/queue":
                case "/pendingauthorimport":
                    return true;
            }

            return IsNumericItemPath(path, "/book") ||
                   IsNumericItemPath(path, "/author") ||
                   IsNumericItemPath(path, "/pendingauthorimport") ||
                   IsNumericItemPath(path, "/command") ||
                   IsMediaCoverPath(path);
        }

        private static bool HasSinglePositiveIdQuery(HttpRequest request, string name)
        {
            var values = request.Query[name];
            return values.Count == 1 &&
                   int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id) &&
                   id > 0;
        }

        private static bool IsExactPath(string actual, string expected)
        {
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNumericItemPath(string path, string prefix)
        {
            if (!path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var id = path.Substring(prefix.Length + 1);
            return id.Length > 0 &&
                   int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedId) &&
                   parsedId > 0;
        }

        private static bool IsMediaCoverPath(string path)
        {
            var segments = path.Split('/');
            if (segments.Length == 4 &&
                string.Equals(segments[1], "MediaCover", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(segments[2], NumberStyles.None, CultureInfo.InvariantCulture, out var bookId) &&
                bookId > 0)
            {
                return IsSafeImageName(segments[3]);
            }

            return segments.Length == 5 &&
                   string.Equals(segments[1], "MediaCover", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(segments[2], "author", StringComparison.OrdinalIgnoreCase) &&
                   int.TryParse(segments[3], NumberStyles.None, CultureInfo.InvariantCulture, out var authorId) &&
                   authorId > 0 &&
                   IsSafeImageName(segments[4]);
        }

        private static bool IsSafeImageName(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename) || filename.Contains("..") || filename.Contains('\\'))
            {
                return false;
            }

            var extension = System.IO.Path.GetExtension(filename);
            return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<bool> IsBookSearchCommandAsync(HttpRequest request)
        {
            if (request.ContentLength == null ||
                request.ContentLength <= 0 ||
                request.ContentLength > MaxCommandBodyBytes ||
                request.ContentType == null ||
                !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            request.EnableBuffering();
            try
            {
                using (var document = await JsonDocument.ParseAsync(
                    request.Body,
                    new JsonDocumentOptions { MaxDepth = 8 },
                    request.HttpContext.RequestAborted))
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object ||
                        !TryGetUniquePropertyIgnoreCase(root, "name", out var name) ||
                        name.ValueKind != JsonValueKind.String ||
                        !string.Equals(name.GetString(), "BookSearch", StringComparison.OrdinalIgnoreCase) ||
                        !TryGetUniquePropertyIgnoreCase(root, "bookIds", out var bookIds) ||
                        bookIds.ValueKind != JsonValueKind.Array ||
                        bookIds.GetArrayLength() != 1)
                    {
                        return false;
                    }

                    var bookId = bookIds[0];
                    return bookId.ValueKind == JsonValueKind.Number &&
                           bookId.TryGetInt32(out var parsedId) &&
                           parsedId > 0;
                }
            }
            catch (JsonException)
            {
                return false;
            }
            finally
            {
                if (request.Body.CanSeek)
                {
                    request.Body.Position = 0;
                }
            }
        }

        private static bool TryGetUniquePropertyIgnoreCase(
            JsonElement element,
            string propertyName,
            out JsonElement value)
        {
            var found = false;
            value = default;
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    if (found)
                    {
                        value = default;
                        return false;
                    }

                    value = property.Value;
                    found = true;
                }
            }

            if (!found)
            {
                value = default;
            }

            return found;
        }
    }
}
