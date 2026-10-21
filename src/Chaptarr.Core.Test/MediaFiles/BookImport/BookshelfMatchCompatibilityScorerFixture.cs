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
                ["album"] = new List<string> { "A Wizard of Earthsea" },
                ["isbn13"] = new List<string> { "9780123456789" },
                ["asin"] = new List<string> { "B000000001" },
                ["date"] = new List<string> { "2018" },
                ["language"] = new List<string> { "en" },
                ["publisher"] = new List<string> { "Good Press" }
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
                ["isbn13"] = new List<string> { "9780123456789" },
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

            Assert.That(percent, Is.GreaterThan(90));
            Assert.That(percent, Is.LessThan(100));
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
    }
}
