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
            var fileAuthors = GetMostCommonList(trackTags, "artist", "albumartist", "album artist", "author", "composer", "writer");
            var authorVariants = GetAuthorVariants(fileAuthors);
            var authorName = book?.Author?.Name ?? candidate.AuthorName;
            distance.AddString("author", authorVariants, authorName);

            var fileTitle = GetMostCommon(trackTags, "album", "booktitle", "book title", "title");
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

            AddIdentifierDistance(distance, "isbn", "isbn_missing", GetMostCommon(trackTags, "isbn", "isbn13", "isbn-13", "isbn10", "isbn-10"), edition?.Isbn13 ?? edition?.Isbn10, ValidIsbnRegex);
            AddIdentifierDistance(distance, "asin", "asin_missing", GetMostCommon(trackTags, "asin", "audible_asin", "audible asin"), edition?.Asin ?? edition?.AudibleASIN, ValidAsinRegex);

            var localYear = GetMostCommonYear(trackTags, "date", "year", "originaldate", "original date");
            var releaseDate = edition?.ReleaseDate ?? candidate.ReleaseDate;
            if (localYear.HasValue && releaseDate.HasValue)
            {
                var remoteYear = releaseDate.Value.Year;
                var difference = Math.Abs(localYear.Value - remoteYear);
                var maximumDifference = Math.Abs(DateTime.Now.Year - remoteYear);
                distance.AddRatio("year", difference, maximumDifference);
            }

            var localLanguage = GetMostCommon(trackTags, "language", "lang")?.CanonicalizeLanguage();
            var editionLanguage = (edition?.Language ?? candidate.Lang)?.CanonicalizeLanguage();
            if (!string.IsNullOrWhiteSpace(localLanguage) && !string.IsNullOrWhiteSpace(editionLanguage))
            {
                distance.AddBool("language", localLanguage != editionLanguage);
            }

            var localPublisher = GetMostCommon(trackTags, "publisher");
            var editionPublisher = edition?.Publisher ?? candidate.Publisher;
            if (!string.IsNullOrWhiteSpace(localPublisher) && !string.IsNullOrWhiteSpace(editionPublisher))
            {
                distance.AddString("publisher", localPublisher, editionPublisher);
            }

            if (mediaType == BookMediaType.Audiobook)
            {
                var fileNarrators = GetNarrators(GetValues(trackTags, "narrator", "narrators"));
                var editionNarrators = GetNarrators(
                    (edition?.NarratorNames ?? new List<string>())
                    .Concat(new[] { edition?.Narrator ?? candidate.NarratorNames }));
                if (fileNarrators.Count > 0 && editionNarrators.Count > 0)
                {
                    distance.AddString("narrator", fileNarrators, editionNarrators);
                }
            }

            var format = edition?.Format ?? edition?.EditionFormat ?? candidate.EditionFormat;
            if (!string.IsNullOrWhiteSpace(format))
            {
                if (mediaType == BookMediaType.Ebook)
                {
                    distance.AddBool("ebook_format", !EbookFormats.Contains(format, StringComparer.OrdinalIgnoreCase));
                    distance.AddBool("wrong_format", AudiobookFormats.Contains(format, StringComparer.OrdinalIgnoreCase));
                }
                else
                {
                    distance.AddBool("audio_format", !AudiobookFormats.Contains(format, StringComparer.OrdinalIgnoreCase));
                }
            }

            AddPreferredEditionDistance(distance, editionTitle, format, editionPublisher, preferredEditionTerms);
            return 100.0 * (1.0 - distance.NormalizedDistance());
        }

        private static void AddIdentifierDistance(WeightedDistance distance, string key, string missingKey, string localValue, string editionValue, Regex valid)
        {
            var local = NormalizeIdentifier(localValue, valid);
            var remote = NormalizeIdentifier(editionValue, valid);
            if (!string.IsNullOrWhiteSpace(local) && string.Equals(local, remote, StringComparison.Ordinal))
            {
                distance.Add(key, 0);
            }
            else if (!string.IsNullOrWhiteSpace(local) || !string.IsNullOrWhiteSpace(remote))
            {
                distance.Add(missingKey, 1);
            }
        }

        private static string NormalizeIdentifier(string value, Regex valid)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim().Replace("-", string.Empty).ToUpperInvariant();
            return valid.IsMatch(normalized) ? normalized : null;
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
            var values = GetValues(trackTags, names);
            return values.GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .Select(group => group.First())
                .FirstOrDefault();
        }

        private static List<string> GetMostCommonList(IReadOnlyList<Dictionary<string, List<string>>> trackTags, params string[] names)
        {
            if (trackTags == null || trackTags.Count == 0)
            {
                return new List<string>();
            }

            var nameSet = new HashSet<string>(names ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var perTrack = trackTags
                .Where(tags => tags != null)
                .Select(tags => tags
                    .Where(pair => nameSet.Contains(pair.Key))
                    .SelectMany(pair => pair.Value ?? new List<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList())
                .ToList();

            return perTrack
                .GroupBy(values => string.Join("\u001f", values), StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .Select(group => group.First())
                .FirstOrDefault() ?? new List<string>();
        }

        private static int? GetMostCommonYear(IReadOnlyList<Dictionary<string, List<string>>> trackTags, params string[] names)
        {
            var years = GetValues(trackTags, names)
                .Select(ParseYear)
                .Where(year => year.HasValue)
                .Select(year => year.Value)
                .ToList();

            return years.GroupBy(year => year)
                .OrderByDescending(group => group.Count())
                .Select(group => (int?)group.Key)
                .FirstOrDefault();
        }

        private static List<string> GetValues(IReadOnlyList<Dictionary<string, List<string>>> trackTags, params string[] names)
        {
            if (trackTags == null || trackTags.Count == 0)
            {
                return new List<string>();
            }

            var nameSet = new HashSet<string>(names ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return trackTags
                .Where(tags => tags != null)
                .SelectMany(tags => tags
                    .Where(pair => nameSet.Contains(pair.Key))
                    .SelectMany(pair => pair.Value ?? new List<string>()))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
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
                if (input.Contains(separator, StringComparison.OrdinalIgnoreCase))
                {
                    return input.Split(new[] { separator }, StringSplitOptions.None)
                        .SelectMany(SplitAuthor)
                        .ToList();
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

            var characters = input.ToLowerInvariant().RemoveAccent().ToCharArray();
            return new string(Array.FindAll(characters, char.IsLetterOrDigit));
        }
    }
}
