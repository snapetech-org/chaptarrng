using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Calibre;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.MediaFiles.BookImport
{
    /// <summary>
    /// Reproduces BookshelfNG's weighted release-to-edition score for converted installs.
    /// The normal ChaptarrNG matcher still applies its stronger identity and ambiguity checks;
    /// this score restores the legacy minimum-percent acceptance gate after those checks pass.
    /// </summary>
    public static class BookshelfMatchCompatibilityScorer
    {
        private static readonly IReadOnlyDictionary<string, double> Weights = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["author"] = 3.0,
            ["book"] = 3.0,
            ["isbn"] = 10.0,
            ["isbn_missing"] = 0.1,
            ["asin"] = 10.0,
            ["asin_missing"] = 0.1,
            ["ebook_format"] = 0.1,
            ["audio_format"] = 0.1,
            ["wrong_format"] = 5.0,
            ["year"] = 1.0,
            ["language"] = 5.0,
            ["publisher"] = 0.5,
            ["narrator"] = 1.5,
            ["preferred_edition"] = 3.0
        };

        private static readonly Regex StripSeriesRegex = new Regex(@"\([^\)].+?\)$", RegexOptions.Compiled);
        private static readonly Regex CleanTitleCruft = new Regex(@"\((?:unabridged)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ValidAsinRegex = new Regex(@"^(B[\dA-Z]{9}|\d{9}[\dX])$", RegexOptions.Compiled);
        private static readonly Regex ValidIsbnRegex = new Regex(@"^(\d{9}[\dX]|\d{13})$", RegexOptions.Compiled);
        private static readonly Regex YearRegex = new Regex(@"\b(1\d{3}|2\d{3})\b", RegexOptions.Compiled);
        private static readonly string[] EbookFormats = { "Kindle Edition", "Nook", "ebook" };
        private static readonly string[] AudiobookFormats = { "Audiobook", "Audio CD", "Audio Cassette", "Audible Audio", "CD-ROM", "MP3 CD" };

        public static double GetMatchPercent(
            IReadOnlyList<Dictionary<string, List<string>>> trackTags,
            EditionFtsMatch candidate,
            Edition edition,
            Book book,
            BookMediaType mediaType,
            string preferredEditionTerms = null)
        {
            if (candidate == null)
            {
                return 0;
            }

            trackTags ??= Array.Empty<Dictionary<string, List<string>>>();
            var distance = new WeightedDistance();
            var fileAuthors = GetMostCommonList(trackTags, mediaType);
            var authorVariants = GetAuthorVariants(fileAuthors);
            var authorName = book?.Author?.Name ?? candidate.AuthorName;
            distance.AddString("author", authorVariants, authorName);

            var fileTitle = GetMostCommonTitle(trackTags, mediaType);
            var fileTitles = new[] { fileTitle, CleanTitleCruft.Replace(fileTitle ?? string.Empty, string.Empty) }
                .Where(value => value != null)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var editionTitle = edition?.Title ?? candidate.EditionTitle;
            var titleOptions = new List<string>();
            if (!string.IsNullOrWhiteSpace(editionTitle))
            {
                titleOptions.Add(editionTitle);
                if (editionTitle.Contains("#"))
                {
                    titleOptions.Add(StripSeriesRegex.Replace(editionTitle, string.Empty));
                }

                var (mainTitle, _) = editionTitle.SplitBookTitle(authorName ?? string.Empty);
                if (!titleOptions.Contains(mainTitle))
                {
                    titleOptions.Add(mainTitle);
                }
            }

            foreach (var link in book?.SeriesLinks ?? new List<SeriesBookLink>())
            {
                var seriesTitle = link?.Series?.Value?.Title;
                if (string.IsNullOrWhiteSpace(seriesTitle))
                {
                    continue;
                }

                var position = link.Position;
                titleOptions.Add($"{seriesTitle} {position} {editionTitle}");
                titleOptions.Add($"{seriesTitle} Book {position} {editionTitle}");
                titleOptions.Add($"{editionTitle} {seriesTitle} {position}");
                titleOptions.Add($"{editionTitle} {seriesTitle} Book {position}");
            }

            if (titleOptions.Count == 0)
            {
                titleOptions.Add(candidate.BookTitle ?? string.Empty);
            }
            distance.AddString("book", fileTitles, titleOptions);

            AddIdentifierDistance(
                distance,
                "isbn",
                "isbn_missing",
                mediaType == BookMediaType.Ebook ? GetMostCommon(trackTags, "isbn") : null,
                edition?.Isbn13,
                ValidIsbnRegex);
            AddIdentifierDistance(
                distance,
                "asin",
                "asin_missing",
                mediaType == BookMediaType.Ebook ? GetMostCommon(trackTags, "asin") : null,
                edition?.Asin,
                ValidAsinRegex);

            var localYear = mediaType == BookMediaType.Audiobook
                ? GetMostCommonYear(trackTags, "date", "year")
                : 0;
            var releaseDate = edition?.ReleaseDate;
            if (localYear > 0 && releaseDate.HasValue)
            {
                var remoteYear = releaseDate.Value.Year;
                var difference = Math.Abs(localYear - remoteYear);
                var maximumDifference = Math.Abs(DateTime.Now.Year - remoteYear);
                distance.AddRatio("year", difference, maximumDifference);
            }

            var localLanguage = mediaType == BookMediaType.Ebook
                ? GetMostCommonSeparatedValue(trackTags, "language")?.CanonicalizeLanguage()
                : null;
            var editionLanguage = mediaType == BookMediaType.Ebook ? edition?.Language?.CanonicalizeLanguage() : null;
            if (!string.IsNullOrWhiteSpace(localLanguage) && !string.IsNullOrWhiteSpace(editionLanguage))
            {
                distance.AddBool("language", localLanguage != editionLanguage);
            }

            var localPublisher = mediaType == BookMediaType.Ebook ? GetMostCommonSeparatedValue(trackTags, "publisher") : null;
            var editionPublisher = edition?.Publisher;
            if (!string.IsNullOrWhiteSpace(localPublisher) && !string.IsNullOrWhiteSpace(editionPublisher))
            {
                distance.AddString("publisher", localPublisher, editionPublisher);
            }

            if (mediaType == BookMediaType.Audiobook)
            {
                var fileNarrators = GetNarrators(GetValues(trackTags, "narrator", "narrators", "performer"));
                var editionNarrators = GetNarrators(new[] { edition?.Narrator });
                if (fileNarrators.Count > 0 && editionNarrators.Count > 0)
                {
                    distance.AddString("narrator", fileNarrators, editionNarrators);
                }
            }

            var format = edition?.Format;
            if (!string.IsNullOrWhiteSpace(format))
            {
                if (mediaType == BookMediaType.Ebook)
                {
                    distance.AddBool("ebook_format", !EbookFormats.Contains(format));
                    distance.AddBool("wrong_format", AudiobookFormats.Contains(format));
                }
                else
                {
                    distance.AddBool("audio_format", !AudiobookFormats.Contains(format));
                }
            }

            AddPreferredEditionDistance(distance, editionTitle, format, editionPublisher, preferredEditionTerms);
            return 100.0 * (1.0 - distance.NormalizedDistance());
        }

        private static void AddIdentifierDistance(WeightedDistance distance, string key, string missingKey, string localValue, string editionValue, Regex valid)
        {
            var local = ValidIdentifier(localValue, valid);
            if (!string.IsNullOrWhiteSpace(local) && string.Equals(local, editionValue, StringComparison.Ordinal))
            {
                distance.Add(key, 0);
            }
            else if (!string.IsNullOrWhiteSpace(local) || !string.IsNullOrWhiteSpace(editionValue))
            {
                distance.Add(missingKey, 1);
            }
        }

        private static string ValidIdentifier(string value, Regex valid)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalizedForValidation = value.Trim().Replace("-", string.Empty).ToUpperInvariant();
            return valid.IsMatch(normalizedForValidation) ? value : null;
        }

        private static int? ParseYear(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var match = YearRegex.Match(value);
            return match.Success && int.TryParse(match.Groups[1].Value, out var year) ? year : (int?)null;
        }

        private static string GetMostCommon(IReadOnlyList<Dictionary<string, List<string>>> trackTags, params string[] names)
        {
            var values = GetTrackValues(trackTags, names);
            return values.GroupBy(value => value)
                .OrderByDescending(group => group.Count())
                .Select(group => group.First())
                .FirstOrDefault();
        }

        private static string GetMostCommonSeparatedValue(IReadOnlyList<Dictionary<string, List<string>>> trackTags, params string[] names)
        {
            var values = GetTrackValues(trackTags, names)
                .Select(value => value?.Split(';').FirstOrDefault())
                .ToList();
            return values.GroupBy(value => value)
                .OrderByDescending(group => group.Count())
                .Select(group => group.First())
                .FirstOrDefault();
        }

        private static List<string> GetMostCommonList(IReadOnlyList<Dictionary<string, List<string>>> trackTags, BookMediaType mediaType)
        {
            if (trackTags == null || trackTags.Count == 0)
            {
                return new List<string>();
            }

            var perTrack = trackTags
                .Where(tags => tags != null)
                .Select(tags => GetLegacyAuthors(tags, mediaType))
                .ToList();

            return perTrack
                .GroupBy(values => string.Join(", ", values))
                .OrderByDescending(group => group.Count())
                .Select(group => group.First())
                .FirstOrDefault() ?? new List<string>();
        }

        private static List<string> GetLegacyAuthors(Dictionary<string, List<string>> tags, BookMediaType mediaType)
        {
            if (mediaType == BookMediaType.Ebook)
            {
                return SplitList(GetFirstValue(tags, "author", "authors"));
            }

            var bookAuthors = GetValues(tags, "albumartist", "album artist", "albumartists");
            if (bookAuthors.Count == 0)
            {
                bookAuthors = GetValues(tags, "albumartistsort", "albumartist_sort", "album artist sort");
            }

            var performers = GetValues(tags, "artist", "performers");
            if (performers.Count == 0)
            {
                performers = GetValues(tags, "performersort", "artist_sort", "performers_sort");
            }

            return bookAuthors.Concat(performers).Distinct().ToList();
        }

        private static string GetMostCommonTitle(IReadOnlyList<Dictionary<string, List<string>>> trackTags, BookMediaType mediaType)
        {
            var titles = trackTags?.Where(tags => tags != null)
                .Select(tags => mediaType == BookMediaType.Audiobook
                    ? GetLegacyAudioTitle(tags)
                    : GetFirstValue(tags, "title", "booktitle", "book title"))
                .ToList() ?? new List<string>();

            return titles.GroupBy(value => value)
                .OrderByDescending(group => group.Count())
                .Select(group => group.First())
                .FirstOrDefault();
        }

        private static string GetLegacyAudioTitle(Dictionary<string, List<string>> tags)
        {
            var bookTitle = GetFirstValue(tags, "album")
                ?? GetFirstValue(tags, "album_sort", "albumsort", "album sort");
            if (!string.IsNullOrWhiteSpace(bookTitle))
            {
                return bookTitle;
            }

            return GetFirstValue(tags, "title", "title_sort", "titlesort", "title sort");
        }

        private static int GetMostCommonYear(IReadOnlyList<Dictionary<string, List<string>>> trackTags, params string[] names)
        {
            var years = trackTags?.Where(tags => tags != null)
                .Select(tags => ParseYear(GetFirstValue(tags, names)) ?? 0)
                .ToList() ?? new List<int>();

            return years.GroupBy(year => year)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key)
                .FirstOrDefault();
        }

        private static List<string> GetTrackValues(IReadOnlyList<Dictionary<string, List<string>>> trackTags, params string[] names)
        {
            return trackTags?.Where(tags => tags != null)
                .Select(tags => GetFirstValue(tags, names))
                .ToList() ?? new List<string>();
        }

        private static string GetFirstValue(Dictionary<string, List<string>> tags, params string[] names)
        {
            if (tags == null || names == null)
            {
                return null;
            }

            foreach (var name in names)
            {
                var pair = tags.FirstOrDefault(value => string.Equals(value.Key, name, StringComparison.OrdinalIgnoreCase));
                var value = pair.Value?.FirstOrDefault();
                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        private static List<string> GetValues(IReadOnlyList<Dictionary<string, List<string>>> trackTags, params string[] names)
        {
            return trackTags?.Where(tags => tags != null)
                .SelectMany(tags => GetValues(tags, names))
                .ToList() ?? new List<string>();
        }

        private static List<string> GetValues(Dictionary<string, List<string>> tags, params string[] names)
        {
            var nameSet = new HashSet<string>(names ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return tags?.Where(pair => nameSet.Contains(pair.Key))
                .SelectMany(pair => pair.Value ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList() ?? new List<string>();
        }

        private static List<string> SplitList(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
        }

        private static List<string> GetAuthorVariants(List<string> fileAuthors)
        {
            var authors = new List<string>(fileAuthors);
            if (fileAuthors.Count == 1)
            {
                authors.AddRange(SplitAuthor(fileAuthors[0]));
            }

            foreach (var author in fileAuthors)
            {
                if (author.Contains(','))
                {
                    var split = author.Split(',', 2).Select(value => value.Trim()).ToArray();
                    if (split[0].IndexOf(' ') < 0)
                    {
                        authors.Add(string.Join(" ", split.Reverse()));
                    }
                }
            }

            return authors;
        }

        private static List<string> SplitAuthor(string input)
        {
            foreach (var separator in new[] { ';', '/' })
            {
                if (input.Contains(separator))
                {
                    return input.Split(separator).Select(value => value.Trim()).ToList();
                }
            }

            foreach (var separator in new[] { " and ", " & " })
            {
                if (input.Contains(separator))
                {
                    var result = new List<string>();
                    foreach (var value in input.Split(new[] { separator }, StringSplitOptions.None).Select(value => value.Trim()))
                    {
                        var split = SplitAuthor(value);
                        if (split.Any())
                        {
                            result.AddRange(split);
                        }
                        else
                        {
                            result.Add(value);
                        }
                    }

                    return result;
                }
            }

            if (input.Contains(','))
            {
                var split = input.Split(',').Select(value => value.Trim()).ToList();
                if (split[0].Contains(' '))
                {
                    return split;
                }
            }

            return new List<string>();
        }

        private static List<string> GetNarrators(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .SelectMany(value => value.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddPreferredEditionDistance(WeightedDistance distance, string title, string format, string publisher, string preferredEditionTerms)
        {
            if (string.IsNullOrWhiteSpace(preferredEditionTerms))
            {
                return;
            }

            var terms = preferredEditionTerms
                .Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Select(value =>
                {
                    var separator = value.IndexOf('|');
                    return separator <= 0 || separator == value.Length - 1 ||
                           !int.TryParse(value.Substring(0, separator).Trim(), out var weight) || weight <= 0
                        ? null
                        : new { Weight = weight, Term = value.Substring(separator + 1).Trim() };
                })
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.Term))
                .ToList();

            if (terms.Count == 0)
            {
                return;
            }

            var text = $"{title} {format} {publisher}";
            var maximumWeight = terms.Max(value => value.Weight);
            var matchingWeight = terms
                .Where(value => text.IndexOf(value.Term, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(value => value.Weight)
                .DefaultIfEmpty(0)
                .Max();
            distance.Add("preferred_edition", matchingWeight > 0 ? 1.0 - matchingWeight / (double)maximumWeight : 1.0);
        }

        private sealed class WeightedDistance
        {
            private readonly Dictionary<string, List<double>> _penalties = new Dictionary<string, List<double>>(StringComparer.Ordinal);

            public void Add(string key, double penalty)
            {
                if (!_penalties.TryGetValue(key, out var values))
                {
                    values = new List<double>();
                    _penalties[key] = values;
                }

                values.Add(penalty);
            }

            public void AddBool(string key, bool mismatch) => Add(key, mismatch ? 1.0 : 0.0);

            public void AddRatio(string key, int difference, int maximumDifference)
            {
                Add(key, maximumDifference > 0 ? Math.Max(Math.Min(difference, maximumDifference), 0.0) / maximumDifference : 0.0);
            }

            public void AddString(string key, string value, string target) => Add(key, StringDistance(value, target));

            public void AddString(string key, IReadOnlyCollection<string> values, string target) =>
                Add(key, values.Count == 0 ? StringDistance(string.Empty, target) : values.Min(value => StringDistance(value, target)));

            public void AddString(string key, IReadOnlyCollection<string> values, IReadOnlyCollection<string> targets) =>
                Add(key, targets.Count == 0
                    ? StringDistance(values.FirstOrDefault() ?? string.Empty, string.Empty)
                    : values.Count == 0
                        ? StringDistance(string.Empty, targets.FirstOrDefault() ?? string.Empty)
                        : values.Min(value => targets.Min(target => StringDistance(value, target))));

            public double NormalizedDistance()
            {
                var maximum = _penalties.Sum(pair => pair.Value.Count * Weights[pair.Key]);
                if (maximum <= 0)
                {
                    return 0;
                }

                var raw = _penalties.Sum(pair => pair.Value.Sum() * Weights[pair.Key]);
                return raw / maximum;
            }
        }

        private static double StringDistance(string value, string target)
        {
            var cleanValue = Clean(value);
            var cleanTarget = Clean(target);
            if (string.IsNullOrWhiteSpace(cleanValue) && !string.IsNullOrWhiteSpace(cleanTarget))
            {
                return 1.0;
            }
            if (string.IsNullOrWhiteSpace(cleanValue) && string.IsNullOrWhiteSpace(cleanTarget))
            {
                return 0.0;
            }

            return (double)cleanValue.LevenshteinDistance(cleanTarget) / Math.Max(cleanValue.Length, cleanTarget.Length);
        }

        private static string Clean(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var characters = input.ToLower().RemoveAccent().ToCharArray();
            return new string(Array.FindAll(characters, char.IsLetterOrDigit));
        }
    }
}
