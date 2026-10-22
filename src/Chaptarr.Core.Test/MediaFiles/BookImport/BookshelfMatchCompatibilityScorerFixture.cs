using System.Collections.Generic;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Repositories;
using NzbDrone.Core.MediaFiles.BookImport;

namespace Chaptarr.Core.Test.MediaFiles.BookImport
{
    [TestFixture]
    public class BookshelfMatchCompatibilityScorerFixture
    {
        [Test]
        public void should_score_exact_legacy_metadata_as_a_full_match()
        {
            var tags = new Dictionary<string, List<string>>
            {
                ["author"] = new List<string> { "Ursula Le Guin" },
                ["title"] = new List<string> { "A Wizard of Earthsea" },
                ["isbn"] = new List<string> { "9780123456789" },
                ["asin"] = new List<string> { "B000000001" },
                ["language"] = new List<string> { "en; fr" },
                ["publisher"] = new List<string> { "Good Press; Second Press" }
            };
            var candidate = new EditionFtsMatch
            {
                AuthorName = "Ursula Le Guin",
                BookTitle = "A Wizard of Earthsea",
                EditionTitle = "A Wizard of Earthsea",
                EditionFormat = "Kindle Edition",
                Lang = "en",
                Publisher = "Good Press",
                ReleaseDate = new System.DateTime(2018, 1, 1)
            };
            var edition = new Edition
            {
                Title = "A Wizard of Earthsea",
                Isbn13 = "9780123456789",
                Asin = "B000000001",
                Format = "Kindle Edition",
                Language = "en",
                Publisher = "Good Press",
                ReleaseDate = new System.DateTime(2018, 1, 1)
            };
            var book = new Book { Title = "A Wizard of Earthsea", Author = new Author { Name = "Ursula Le Guin" } };

            var percent = BookshelfMatchCompatibilityScorer.GetMatchPercent(new[] { tags }, candidate, edition, book, BookMediaType.Ebook);

            Assert.That(percent, Is.EqualTo(100));
        }

        [Test]
        public void should_keep_bookshelf_weighting_for_strong_edition_identifiers()
        {
            var tags = new Dictionary<string, List<string>>
            {
                ["author"] = new List<string> { "Unrelated Author" },
                ["album"] = new List<string> { "Unrelated Title" },
                ["isbn"] = new List<string> { "9780123456789" },
                ["asin"] = new List<string> { "B000000001" }
            };
            var candidate = new EditionFtsMatch
            {
                AuthorName = "Expected Author",
                BookTitle = "Expected Title",
                EditionTitle = "Expected Title",
                EditionFormat = "Kindle Edition"
            };
            var edition = new Edition
            {
                Title = "Expected Title",
                Isbn13 = "9780123456789",
                Asin = "B000000001",
                Format = "Kindle Edition"
            };
            var book = new Book { Title = "Expected Title", Author = new Author { Name = "Expected Author" } };

            var percent = BookshelfMatchCompatibilityScorer.GetMatchPercent(new[] { tags }, candidate, edition, book, BookMediaType.Ebook);

            Assert.That(percent, Is.GreaterThan(70));
            Assert.That(percent, Is.LessThan(95));
        }

        [Test]
        public void should_score_weak_author_and_title_evidence_below_the_default_threshold()
        {
            var tags = new Dictionary<string, List<string>>
            {
                ["author"] = new List<string> { "Unrelated Author" },
                ["album"] = new List<string> { "Unrelated Title" }
            };
            var candidate = new EditionFtsMatch
            {
                AuthorName = "Expected Author",
                BookTitle = "Expected Title",
                EditionTitle = "Expected Title"
            };
            var edition = new Edition { Title = "Expected Title" };
            var book = new Book { Title = "Expected Title", Author = new Author { Name = "Expected Author" } };

            var percent = BookshelfMatchCompatibilityScorer.GetMatchPercent(new[] { tags }, candidate, edition, book, BookMediaType.Ebook);

            Assert.That(percent, Is.LessThan(70));
        }

        [Test]
        public void should_ignore_audiobook_tags_not_used_by_bookshelf_and_keep_missing_id_penalty()
        {
            var tags = new Dictionary<string, List<string>>
            {
                ["albumartist"] = new List<string> { "Ursula Le Guin" },
                ["artist"] = new List<string> { "Ursula Le Guin" },
                ["composer"] = new List<string> { "A Different Author" },
                ["album"] = new List<string> { "A Wizard of Earthsea" },
                ["isbn"] = new List<string> { "9780123456789" },
                ["asin"] = new List<string> { "B000000001" },
                ["language"] = new List<string> { "en" },
                ["publisher"] = new List<string> { "Good Press" },
                ["originaldate"] = new List<string> { "1900" }
            };
            var candidate = new EditionFtsMatch
            {
                AuthorName = "Ursula Le Guin",
                BookTitle = "A Wizard of Earthsea",
                EditionTitle = "A Wizard of Earthsea",
                EditionFormat = "Audiobook"
            };
            var edition = new Edition
            {
                Title = "A Wizard of Earthsea",
                Isbn13 = "9780123456789",
                Asin = "B000000001",
                Format = "Audiobook",
                Language = "fr",
                Publisher = "Different Press",
                ReleaseDate = new System.DateTime(2024, 1, 1)
            };
            var book = new Book { Title = "A Wizard of Earthsea", Author = new Author { Name = "Ursula Le Guin" } };

            var percent = BookshelfMatchCompatibilityScorer.GetMatchPercent(new[] { tags }, candidate, edition, book, BookMediaType.Audiobook);

            Assert.That(percent, Is.GreaterThan(95));
            Assert.That(percent, Is.LessThan(100));
        }

        [Test]
        public void should_use_bookshelf_audio_title_and_sort_fallbacks()
        {
            var tags = new Dictionary<string, List<string>>
            {
                ["album"] = new List<string> { string.Empty },
                ["albumsort"] = new List<string> { "A Different Title" },
                ["title"] = new List<string> { "A Wizard of Earthsea" },
                ["albumartist"] = new List<string> { "Ursula Le Guin" },
                ["artist"] = new List<string> { "Ursula Le Guin" }
            };
            var candidate = new EditionFtsMatch
            {
                AuthorName = "Ursula Le Guin",
                BookTitle = "A Wizard of Earthsea",
                EditionTitle = "A Wizard of Earthsea",
                EditionFormat = "Audiobook"
            };
            var edition = new Edition
            {
                Title = "A Wizard of Earthsea",
                Format = "Audiobook"
            };
            var book = new Book { Title = "A Wizard of Earthsea", Author = new Author { Name = "Ursula Le Guin" } };

            var percent = BookshelfMatchCompatibilityScorer.GetMatchPercent(new[] { tags }, candidate, edition, book, BookMediaType.Audiobook);

            Assert.That(percent, Is.EqualTo(100));
        }

        [Test]
        public void should_not_use_isbn10_or_audible_asin_as_bookshelf_match_identifiers()
        {
            var tags = new Dictionary<string, List<string>>
            {
                ["author"] = new List<string> { "Ursula Le Guin" },
                ["title"] = new List<string> { "A Wizard of Earthsea" },
                ["isbn"] = new List<string> { "9780123456789" },
                ["asin"] = new List<string> { "B000000001" }
            };
            var candidate = new EditionFtsMatch
            {
                AuthorName = "Ursula Le Guin",
                BookTitle = "A Wizard of Earthsea",
                EditionTitle = "A Wizard of Earthsea",
                EditionFormat = "Kindle Edition"
            };
            var edition = new Edition
            {
                Title = "A Wizard of Earthsea",
                Isbn10 = "9780123456789",
                AudibleASIN = "B000000001",
                Format = "Kindle Edition"
            };
            var book = new Book { Title = "A Wizard of Earthsea", Author = new Author { Name = "Ursula Le Guin" } };

            var percent = BookshelfMatchCompatibilityScorer.GetMatchPercent(new[] { tags }, candidate, edition, book, BookMediaType.Ebook);

            Assert.That(percent, Is.LessThan(100));
        }

        [Test]
        public void should_preserve_bookshelf_case_sensitive_format_and_identifier_comparisons()
        {
            var tags = new Dictionary<string, List<string>>
            {
                ["author"] = new List<string> { "Ursula Le Guin" },
                ["title"] = new List<string> { "A Wizard of Earthsea" },
                ["isbn"] = new List<string> { "9780123456789" },
                ["asin"] = new List<string> { "b000000001" }
            };
            var candidate = new EditionFtsMatch
            {
                AuthorName = "Ursula Le Guin",
                BookTitle = "A Wizard of Earthsea",
                EditionTitle = "A Wizard of Earthsea",
                EditionFormat = "kindle edition"
            };
            var edition = new Edition
            {
                Title = "A Wizard of Earthsea",
                Isbn13 = "9780123456789",
                Asin = "B000000001",
                Format = "kindle edition"
            };
            var book = new Book { Title = "A Wizard of Earthsea", Author = new Author { Name = "Ursula Le Guin" } };

            var percent = BookshelfMatchCompatibilityScorer.GetMatchPercent(new[] { tags }, candidate, edition, book, BookMediaType.Ebook);

            Assert.That(percent, Is.LessThan(100));
        }
    }
}
