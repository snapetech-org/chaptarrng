using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;

namespace Chaptarr.Api.V1.Books
{
    /// <summary>
    /// One edition in terms a person chooses between. Details the catalog does not
    /// provide are reported as unknown rather than guessed.
    /// </summary>
    public class EditionCatalogResource
    {
        public int EditionId { get; set; }
        public string ForeignEditionId { get; set; }
        public string Title { get; set; }
        public string Language { get; set; }
        public string Format { get; set; }
        public bool IsEbook { get; set; }
        public List<string> Narrators { get; set; } = new List<string>();

        /// <summary>solo, full-cast, or unknown.</summary>
        public string Production { get; set; } = "unknown";

        /// <summary>abridged, unabridged, or unknown.</summary>
        public string Abridged { get; set; } = "unknown";

        public int? DurationSeconds { get; set; }
        public string Publisher { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string ImageUrl { get; set; }
        public bool Monitored { get; set; }
        public bool Owned { get; set; }
    }

    public static class EditionCatalogMapper
    {
        private static readonly Regex Unabridged = new Regex(@"\bunabridged\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Abridged = new Regex(@"\babridged\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FullCast = new Regex(@"\b(full[- ]?cast|dramati[sz]ed|graphic ?audio|multi[- ]?cast)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static List<string> NarratorsOf(Edition edition)
        {
            var names = edition.NarratorNames?.Where(n => n.IsNotNullOrWhiteSpace()).Select(n => n.Trim()).ToList();
            if (names != null && names.Any())
            {
                return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            return (edition.Narrator ?? string.Empty)
                .Split(new[] { ',', ';', '&' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string ProductionOf(Edition edition, IReadOnlyCollection<string> narrators)
        {
            var hints = string.Join(" ", new[] { edition.Title, edition.Subtitle, edition.EditionInfo, edition.Disambiguation, edition.AudioProductionType });
            if (edition.IsGraphicAudio || FullCast.IsMatch(hints))
            {
                return "full-cast";
            }

            if (narrators.Count == 1)
            {
                return "solo";
            }

            return narrators.Count >= 6 ? "full-cast" : "unknown";
        }

        public static string AbridgementOf(Edition edition)
        {
            var hints = string.Join(" ", new[] { edition.Title, edition.Subtitle, edition.EditionInfo, edition.Disambiguation });
            if (Unabridged.IsMatch(hints))
            {
                return "unabridged";
            }

            return Abridged.IsMatch(hints) ? "abridged" : "unknown";
        }

        public static EditionCatalogResource ToCatalogResource(this Edition edition)
        {
            var narrators = NarratorsOf(edition);
            var cover = edition.Images?.FirstOrDefault(i => i.CoverType == NzbDrone.Core.MediaCover.MediaCoverTypes.Cover);

            return new EditionCatalogResource
            {
                EditionId = edition.Id,
                ForeignEditionId = edition.ForeignEditionId,
                Title = edition.Title,
                Language = edition.Language,
                Format = edition.EditionFormat ?? edition.Format,
                IsEbook = edition.IsEbook,
                Narrators = narrators,
                Production = edition.IsEbook ? "unknown" : ProductionOf(edition, narrators),
                Abridged = edition.IsEbook ? "unknown" : AbridgementOf(edition),
                DurationSeconds = edition.DurationSeconds,
                Publisher = edition.Publisher,
                ReleaseDate = edition.ReleaseDate,
                ImageUrl = cover?.RemoteUrl ?? cover?.Url,
                Monitored = edition.Monitored,
                Owned = edition.BookFiles?.Any() == true
            };
        }
    }
}
