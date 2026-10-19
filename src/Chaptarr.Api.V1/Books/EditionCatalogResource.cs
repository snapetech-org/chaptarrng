using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Chaptarr.Http.Middleware;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaCover;

namespace Chaptarr.Api.V1.Books
{
    /// <summary>
    /// A concise edition shape for clients that let a person compare audiobook performances.
    /// Classification stays unknown unless the edition metadata provides clear evidence.
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
        public string Production { get; set; } = "unknown";
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

        public static EditionCatalogResource ToCatalogResource(
            this Edition edition,
            bool owned,
            ReadarrFacadeContext facadeContext)
        {
            var narrators = NarratorsOf(edition);
            var hints = string.Join(" ", new[] { edition.Title, edition.Disambiguation });
            var production = "unknown";
            if (!edition.IsEbook)
            {
                production = edition.IsGraphicAudio || FullCast.IsMatch(hints)
                    ? "full-cast"
                    : narrators.Count == 1 ? "solo" : "unknown";
            }

            var abridged = "unknown";
            if (!edition.IsEbook)
            {
                abridged = Unabridged.IsMatch(hints)
                    ? "unabridged"
                    : Abridged.IsMatch(hints) ? "abridged" : "unknown";
            }

            var cover = edition.Images?.FirstOrDefault(image => image.CoverType == MediaCoverTypes.Cover);
            return new EditionCatalogResource
            {
                EditionId = edition.Id,
                ForeignEditionId = ReadarrFacadeEditionIdentity.BuildForeignEditionId(edition, facadeContext),
                Title = edition.Title,
                Language = edition.Language,
                Format = edition.Format,
                IsEbook = edition.IsEbook,
                Narrators = narrators,
                Production = production,
                Abridged = abridged,
                DurationSeconds = edition.DurationSeconds,
                Publisher = edition.Publisher,
                ReleaseDate = edition.ReleaseDate,
                ImageUrl = cover?.Url,
                Monitored = edition.Monitored,
                Owned = owned
            };
        }

        public static List<string> NarratorsOf(Edition edition)
        {
            var source = edition.NarratorNames?.Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
            if (source == null || source.Length == 0)
            {
                source = new[] { edition.Narrator };
            }

            return source
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .SelectMany(name => name.Split(new[] { ',', ';', '&' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(name => name.Trim())
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
