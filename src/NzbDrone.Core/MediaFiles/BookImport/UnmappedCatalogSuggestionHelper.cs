using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NLog;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles.BookImport;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.BookImport
{
    internal static class UnmappedCatalogSuggestionHelper
    {
        private static readonly string[] CanonicalProviderIdTagNames =
        {
            "PROVIDER_ID",
            "BOOK_PROVIDER_ID",
            "WORK_PROVIDER_ID",
            "EDITION_PROVIDER_ID",
            "FOREIGN_BOOK_ID",
            "FOREIGN_WORK_ID",
            "FOREIGN_EDITION_ID"
        };

        private static readonly string[] GoodreadsWorkIdTagNames =
        {
            "GOODREADS_WORK_ID"
        };

        private static readonly string[] GoodreadsEditionIdTagNames =
        {
            "GOODREADS_ID",
            "GOODREADS_BOOK_ID",
            "GOODREADS_EDITION_ID",
            "GR_ID"
        };

        public static int AddSuggestions(ISearchForNewBook catalogSearch, Logger logger, IReadOnlyCollection<UnmatchedFile> unmatchedFiles)
        {
            if (catalogSearch == null)
            {
                return 0;
            }

            var queries = (unmatchedFiles ?? Array.Empty<UnmatchedFile>())
                .Where(unmatched => unmatched?.File?.Path != null &&
                    unmatched.PotentialAuthors?.Any(author => !string.IsNullOrWhiteSpace(author?.ProviderId)) != true)
                .Select(unmatched => TryBuildCatalogSearchQuery(unmatched.File, out var query)
                    ? new CatalogSearchQueryItem(unmatched, query)
                    : null)
                .Where(item => item != null)
                .GroupBy(item => item.Query.Key, StringComparer.Ordinal)
                .ToList();

            var suggestionsAdded = 0;
            foreach (var queryGroup in queries)
            {
                var query = queryGroup.First().Query;
                var results = new List<Book>();
                if (!string.IsNullOrWhiteSpace(query.Isbn))
                {
                    try
                    {
                        results.AddRange(WithRequestedMediaType(catalogSearch.SearchByIsbn(query.Isbn), query.MediaType));
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, "[UNMAPPED-RETRY] Catalog ISBN search failed");
                    }
                }

                if (!string.IsNullOrWhiteSpace(query.Asin))
                {
                    try
                    {
                        results.AddRange(WithRequestedMediaType(catalogSearch.SearchByAsin(query.Asin), query.MediaType));
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, "[UNMAPPED-RETRY] Catalog ASIN search failed");
                    }
                }

                if (!string.IsNullOrWhiteSpace(query.ProviderId))
                {
                    try
                    {
                        if (query.GoodreadsEditionLookup && int.TryParse(query.ProviderId.Substring("gr:".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var goodreadsId))
                        {
                            results.AddRange(WithRequestedMediaType(catalogSearch.SearchByGoodreadsBookId(goodreadsId, getAllEditions: true), query.MediaType));
                        }
                        else
                        {
                            results.AddRange(WithRequestedMediaType(
                                catalogSearch.SearchForNewBook(query.ProviderId, null, getAllEditions: true, mediaType: query.MediaType),
                                query.MediaType));
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, "[UNMAPPED-RETRY] Catalog provider-ID search failed for '{0}'", query.ProviderId);
                    }
                }

                if (!string.IsNullOrWhiteSpace(query.Title) && !string.IsNullOrWhiteSpace(query.Author))
                {
                    try
                    {
                        results.AddRange(catalogSearch.SearchForNewBook(query.Title, query.Author, getAllEditions: false, mediaType: query.MediaType)
                            ?? new List<Book>());
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, "[UNMAPPED-RETRY] Catalog title search failed for unmatched title '{0}'", query.Title);
                    }
                }

                var suggestion = BuildExactCatalogSuggestion(results, query);
                if (suggestion == null)
                {
                    continue;
                }

                foreach (var item in queryGroup)
                {
                    item.Unmatched.PotentialAuthors = new[] { suggestion };
                    suggestionsAdded++;
                }
            }

            if (suggestionsAdded > 0)
            {
                logger.Info("[UNMAPPED-RETRY] Added {0} exact-identity catalog suggestions for pending author import", suggestionsAdded);
            }

            return suggestionsAdded;
        }

        private static IEnumerable<Book> WithRequestedMediaType(IEnumerable<Book> books, BookMediaType mediaType)
        {
            foreach (var book in books ?? Enumerable.Empty<Book>())
            {
                if (book == null)
                {
                    continue;
                }

                book.MediaType = mediaType;
                yield return book;
            }
        }

        private static bool TryBuildCatalogSearchQuery(DiscoveredFileWithMetadata file, out CatalogSearchQuery query)
        {
            query = null;
            if (file == null || string.IsNullOrWhiteSpace(file.Path))
            {
                return false;
            }

            var mediaType = QualityMediaTypeHelper.GetMediaTypeFromPath(file.Path);
            if (!mediaType.HasValue)
            {
                return false;
            }

            var titleKeys = mediaType == BookMediaType.Audiobook
                ? new[] { "ALBUM", "BOOK" }
                : new[] { "TITLE", "ALBUM", "BOOK" };
            var authorKeys = mediaType == BookMediaType.Audiobook
                ? new[] { "ALBUMARTIST", "AUTHOR", "ARTIST" }
                : new[] { "AUTHOR", "ARTIST", "ALBUMARTIST" };

            var title = GetSingleTagValue(file.AllTags, titleKeys);
            var author = GetSingleTagValue(file.AllTags, authorKeys);
            var normalizedTitle = NormalizeCatalogEvidence(title);
            var normalizedAuthor = NormalizeCatalogEvidence(author);
            var isbn = NormalizeCatalogIsbn(GetSingleTagValue(file.AllTags, "ISBN", "ISBN13", "ISBN10", "ISBN-13", "ISBN-10"));
            var asin = NormalizeCatalogAsin(GetSingleTagValue(file.AllTags, "ASIN", "AUDIBLE_ASIN"));
            if (!TryGetProviderIdEvidence(file.AllTags, out var providerIdEvidence))
            {
                return false;
            }

            if ((normalizedTitle == null || normalizedAuthor == null) && isbn == null && asin == null && providerIdEvidence == null)
            {
                return false;
            }

            query = new CatalogSearchQuery
            {
                Title = title,
                Author = author,
                NormalizedTitle = normalizedTitle,
                NormalizedAuthor = normalizedAuthor,
                Isbn = isbn,
                Asin = asin,
                ProviderId = providerIdEvidence?.ProviderId,
                GoodreadsEditionLookup = providerIdEvidence?.GoodreadsEditionLookup == true,
                MediaType = mediaType.Value,
                Key = $"{(int)mediaType.Value}\u001f{normalizedTitle}\u001f{normalizedAuthor}\u001f{isbn}\u001f{asin}\u001f{providerIdEvidence?.ProviderId}\u001f{providerIdEvidence?.GoodreadsEditionLookup}"
            };

            return true;
        }

        private static bool TryGetProviderIdEvidence(Dictionary<string, List<string>> tags, out ProviderIdEvidence providerIdEvidence)
        {
            providerIdEvidence = null;
            if (tags == null || tags.Count == 0)
            {
                return true;
            }

            var evidence = new List<ProviderIdEvidence>();
            foreach (var key in CanonicalProviderIdTagNames)
            {
                if (!TryGetSingleTagValue(tags, key, out var value, out var ambiguous))
                {
                    continue;
                }

                if (ambiguous || !TryCanonicalizeProviderId(value, null, out var providerId))
                {
                    return false;
                }

                evidence.Add(new ProviderIdEvidence(providerId, false));
            }

            foreach (var key in GoodreadsWorkIdTagNames)
            {
                if (!TryGetSingleTagValue(tags, key, out var value, out var ambiguous))
                {
                    continue;
                }

                if (ambiguous || !TryCanonicalizeProviderId(value, "gr", out var providerId))
                {
                    return false;
                }

                evidence.Add(new ProviderIdEvidence(providerId, false));
            }

            foreach (var key in GoodreadsEditionIdTagNames)
            {
                if (!TryGetSingleTagValue(tags, key, out var value, out var ambiguous))
                {
                    continue;
                }

                if (ambiguous || !TryCanonicalizeProviderId(value, "gr", out var providerId))
                {
                    return false;
                }

                evidence.Add(new ProviderIdEvidence(providerId, true));
            }

            var distinctIds = evidence
                .Select(item => item.ProviderId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctIds.Count == 0)
            {
                return true;
            }

            if (distinctIds.Count != 1)
            {
                return false;
            }

            providerIdEvidence = new ProviderIdEvidence(
                distinctIds[0],
                evidence.Any(item => item.GoodreadsEditionLookup));
            return true;
        }

        private static bool TryGetSingleTagValue(Dictionary<string, List<string>> tags, string key, out string value, out bool ambiguous)
        {
            value = null;
            ambiguous = false;
            var entry = tags.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase));
            if (entry.Key == null)
            {
                return false;
            }

            var values = (entry.Value ?? new List<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (values.Count == 0)
            {
                return false;
            }

            if (values.Count != 1)
            {
                ambiguous = true;
                return true;
            }

            value = values[0];
            return true;
        }

        private static bool TryCanonicalizeProviderId(string value, string defaultPrefix, out string providerId)
        {
            providerId = null;
            try
            {
                providerId = ProviderIdHelper.Canonicalize(value, defaultPrefix);
                return !string.IsNullOrWhiteSpace(providerId);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static string GetSingleTagValue(Dictionary<string, List<string>> tags, params string[] keys)
        {
            foreach (var key in keys)
            {
                var entry = tags?.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase));
                var values = entry?.Value?
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (values?.Count == 1)
                {
                    return values[0];
                }

                if (values?.Count > 1)
                {
                    return null;
                }
            }

            return null;
        }

        private static AuthorSuggestion BuildExactCatalogSuggestion(IEnumerable<Book> results, CatalogSearchQuery query)
        {
            var candidates = (results ?? Enumerable.Empty<Book>())
                .Where(book => book != null && book.MediaType == query.MediaType &&
                    CatalogEvidenceMatches(book, query))
                .Select(book => new
                {
                    Book = book,
                    AuthorProviderId = NormalizeProviderId(AuthorIdentity.GetPreferredProviderId(book.Author), null),
                    WorkProviderId = GetPreferredWorkProviderId(book)
                })
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.AuthorProviderId) &&
                    !string.IsNullOrWhiteSpace(candidate.WorkProviderId) &&
                    !string.IsNullOrWhiteSpace(candidate.Book.Author?.Name))
                .GroupBy(candidate => $"{candidate.AuthorProviderId}\u001f{candidate.WorkProviderId}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            // Search is only a source of a reviewable author suggestion. It cannot
            // establish a file-to-work match when more than one catalog identity
            // exactly fits the file's available identity evidence.
            if (candidates.Count != 1)
            {
                return null;
            }

            var candidate = candidates[0];
            return new AuthorSuggestion
            {
                ProviderId = candidate.AuthorProviderId,
                AuthorName = candidate.Book.Author.Name,
                Confidence = 0.95,
                BookProviderId = candidate.WorkProviderId,
                BookTitle = candidate.Book.Title
            };
        }

        private static bool CatalogEvidenceMatches(Book book, CatalogSearchQuery query)
        {
            if (query.NormalizedTitle != null && NormalizeCatalogEvidence(book.Title) != query.NormalizedTitle)
            {
                return false;
            }

            if (query.NormalizedAuthor != null && NormalizeCatalogEvidence(book.Author?.Name) != query.NormalizedAuthor)
            {
                return false;
            }

            if (query.NormalizedTitle == null && query.NormalizedAuthor == null &&
                query.Isbn == null && query.Asin == null && query.ProviderId == null)
            {
                return false;
            }

            if (query.Isbn != null && !BookHasIsbn(book, query.Isbn))
            {
                return false;
            }

            if (query.Asin != null && !BookHasAsin(book, query.Asin))
            {
                return false;
            }

            if (query.ProviderId != null &&
                !BookIdentity.GetStableWorkProviderIdentityTokens(book).Contains(query.ProviderId) &&
                !BookIdentity.GetEditionProviderIdentityTokens(book).Contains(query.ProviderId))
            {
                return false;
            }

            return true;
        }

        private static bool BookHasIsbn(Book book, string isbn)
        {
            return new[] { book.ISBN10, book.ISBN13 }
                .Concat((book.Editions ?? new List<Edition>()).SelectMany(edition => new[] { edition?.Isbn10, edition?.Isbn13 }))
                .Select(NormalizeCatalogIsbn)
                .Any(candidate => candidate == isbn);
        }

        private static bool BookHasAsin(Book book, string asin)
        {
            return new[] { book.ASIN, book.AudibleASIN }
                .Concat((book.Editions ?? new List<Edition>()).SelectMany(edition => new[]
                {
                    edition?.Asin,
                    edition?.AudibleASIN
                }.Concat(edition?.Asins ?? new List<string>())))
                .Select(NormalizeCatalogAsin)
                .Any(candidate => candidate == asin);
        }

        private static string NormalizeCatalogIsbn(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = new string(value
                .Where(character => character is >= '0' and <= '9' || character == 'X' || character == 'x')
                .Select(char.ToUpperInvariant)
                .ToArray());
            if (normalized.Length == 10 && IsValidCatalogIsbn(normalized))
            {
                return ConvertIsbn10To13(normalized);
            }

            return IsValidCatalogIsbn(normalized) ? normalized : null;
        }

        private static bool IsValidCatalogIsbn(string isbn)
        {
            if (isbn.Length == 10)
            {
                var sum = 0;
                for (var index = 0; index < 10; index++)
                {
                    var digit = isbn[index] == 'X' && index == 9 ? 10 :
                        isbn[index] is >= '0' and <= '9' ? isbn[index] - '0' : -1;
                    if (digit < 0)
                    {
                        return false;
                    }

                    sum += (10 - index) * digit;
                }

                return sum % 11 == 0;
            }

            if (isbn.Length == 13 && isbn.All(character => character is >= '0' and <= '9'))
            {
                var sum = 0;
                for (var index = 0; index < 13; index++)
                {
                    sum += (isbn[index] - '0') * (index % 2 == 0 ? 1 : 3);
                }

                return sum % 10 == 0;
            }

            return false;
        }

        private static string ConvertIsbn10To13(string isbn10)
        {
            var prefix = "978" + isbn10.Substring(0, 9);
            var sum = 0;
            for (var index = 0; index < prefix.Length; index++)
            {
                sum += (prefix[index] - '0') * (index % 2 == 0 ? 1 : 3);
            }

            var checkDigit = (10 - sum % 10) % 10;
            return prefix + checkDigit;
        }

        private static string NormalizeCatalogAsin(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = new string(value
                .Where(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
                .Select(char.ToUpperInvariant)
                .ToArray());
            return normalized.Length == 10 ? normalized : null;
        }

        private static string GetPreferredWorkProviderId(Book book)
        {
            return BookIdentity.GetStableWorkProviderIdentityTokens(book)
                .OrderBy(GetProviderIdPriority)
                .ThenBy(providerId => providerId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static int GetProviderIdPriority(string providerId)
        {
            var separator = providerId?.IndexOf(':') ?? -1;
            var prefix = separator > 0 ? providerId.Substring(0, separator) : string.Empty;
            return prefix.ToLowerInvariant() switch
            {
                "hc" => 0,
                "gr" => 1,
                "ol" => 2,
                "rn" => 3,
                "meta" => 4,
                _ => 5
            };
        }

        private static string NormalizeCatalogEvidence(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string normalized;
            try
            {
                normalized = value.Trim().Normalize(NormalizationForm.FormKD);
            }
            catch (ArgumentException)
            {
                return null;
            }
            var builder = new StringBuilder(normalized.Length);
            foreach (var character in normalized)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLower(character, CultureInfo.InvariantCulture));
                }
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        private sealed class CatalogSearchQuery
        {
            public string Title { get; set; }
            public string Author { get; set; }
            public string NormalizedTitle { get; set; }
            public string NormalizedAuthor { get; set; }
            public string Isbn { get; set; }
            public string Asin { get; set; }
            public string ProviderId { get; set; }
            public bool GoodreadsEditionLookup { get; set; }
            public BookMediaType MediaType { get; set; }
            public string Key { get; set; }
        }

        private sealed class ProviderIdEvidence
        {
            public ProviderIdEvidence(string providerId, bool goodreadsEditionLookup)
            {
                ProviderId = providerId;
                GoodreadsEditionLookup = goodreadsEditionLookup;
            }

            public string ProviderId { get; }
            public bool GoodreadsEditionLookup { get; }
        }

        private sealed class CatalogSearchQueryItem
        {
            public CatalogSearchQueryItem(UnmatchedFile unmatched, CatalogSearchQuery query)
            {
                Unmatched = unmatched;
                Query = query;
            }

            public UnmatchedFile Unmatched { get; }
            public CatalogSearchQuery Query { get; }
        }

        private static string NormalizeProviderId(string value, string defaultPrefix)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            try
            {
                return ProviderIdHelper.Canonicalize(value.Trim(), defaultPrefix);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
