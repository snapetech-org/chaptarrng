using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles.BookImport;
using NzbDrone.Core.MetadataSource;

namespace Chaptarr.Core.Test.MediaFiles.BookImport
{
    [TestFixture]
    public class CatalogIdentificationFallbackFixture
    {
        private sealed class RecordingCatalogSearch : ISearchForNewBook
        {
            public List<string> Calls { get; } = new List<string>();
            public Func<string, string, List<Book>> OnTextSearch { get; set; } = (_, _) => new List<Book>();
            public List<Book> IsbnResults { get; set; } = new List<Book>();
            public List<Book> AsinResults { get; set; } = new List<Book>();
            public List<Book> GoodreadsResults { get; set; } = new List<Book>();

            public List<Book> SearchForNewBook(string title, string author, bool getAllEditions = true, BookMediaType? mediaType = null)
            {
                Calls.Add($"text:{title}|{author}");
                return OnTextSearch(title, author);
            }

            public List<Book> SearchByIsbn(string isbn)
            {
                Calls.Add($"isbn:{isbn}");
                return IsbnResults;
            }

            public List<Book> SearchByAsin(string asin)
            {
                Calls.Add($"asin:{asin}");
                return AsinResults;
            }

            public List<Book> SearchByGoodreadsBookId(int goodreadsId, bool getAllEditions)
            {
                Calls.Add($"goodreads:{goodreadsId}");
                return GoodreadsResults;
            }
        }

        [Test]
        public void should_fall_back_to_title_search_and_require_one_exact_catalog_work()
        {
            var catalog = new RecordingCatalogSearch
            {
                OnTextSearch = (title, author) => author == null && title == "Dune"
                    ? new List<Book> { Candidate("Dune", "Frank Herbert", "9001", "42") }
                    : new List<Book>()
            };
            var unmatched = Unmatched("/incoming/dune.epub", Tags(("TITLE", "Dune")));

            var added = UnmappedCatalogSuggestionHelper.AddSuggestions(catalog, LogManager.GetCurrentClassLogger(), new[] { unmatched }, stopAfterFirstCandidate: true);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.EqualTo(1));
                Assert.That(catalog.Calls, Is.EquivalentTo(new[] { "text:Dune|" }));
                Assert.That(unmatched.PotentialAuthors.Single().ProviderId, Is.EqualTo("hc:42"));
                Assert.That(unmatched.PotentialAuthors.Single().BookProviderId, Is.EqualTo("hc:9001"));
                Assert.That(unmatched.PotentialAuthors.Single().BookTitle, Is.EqualTo("Dune"));
                Assert.That(unmatched.File.LastRemoteSearchTime, Is.Not.Null);
            });
        }

        [Test]
        public void successful_empty_catalog_search_should_stamp_remote_search_time()
        {
            var catalog = new RecordingCatalogSearch();
            var unmatched = Unmatched("/incoming/dune.epub", Tags(("TITLE", "Dune")));

            var added = UnmappedCatalogSuggestionHelper.AddSuggestions(
                catalog,
                LogManager.GetCurrentClassLogger(),
                new[] { unmatched },
                stopAfterFirstCandidate: true);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.Zero);
                Assert.That(unmatched.File.LastRemoteSearchTime, Is.Not.Null);
                Assert.That(unmatched.File.LastRemoteSearchTime.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            });
        }

        [Test]
        public void failed_catalog_search_should_clear_an_old_remote_search_marker()
        {
            var catalog = new RecordingCatalogSearch
            {
                OnTextSearch = (_, _) => throw new InvalidOperationException("provider unavailable")
            };
            var unmatched = Unmatched("/incoming/dune.epub", Tags(("TITLE", "Dune")));
            unmatched.File.LastRemoteSearchTime = DateTime.UtcNow.AddDays(-1);

            UnmappedCatalogSuggestionHelper.AddSuggestions(
                catalog,
                LogManager.GetCurrentClassLogger(),
                new[] { unmatched },
                stopAfterFirstCandidate: true);

            Assert.That(unmatched.File.LastRemoteSearchTime, Is.Null);
        }

        [Test]
        public void should_use_exact_identifier_before_text_and_preserve_requested_media_type()
        {
            var catalog = new RecordingCatalogSearch
            {
                IsbnResults = new List<Book> { Candidate("Dune", "Frank Herbert", "9001", "42", isbn13: "9780441172719") },
                OnTextSearch = (_, _) => throw new AssertionException("Text search should not run after the exact ISBN match")
            };
            var unmatched = Unmatched("/incoming/dune.epub", Tags(("TITLE", "Dune"), ("AUTHOR", "Frank Herbert"), ("ISBN13", "978-0-441-17271-9")));

            var added = UnmappedCatalogSuggestionHelper.AddSuggestions(catalog, LogManager.GetCurrentClassLogger(), new[] { unmatched }, stopAfterFirstCandidate: true);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.EqualTo(1));
                Assert.That(catalog.Calls, Is.EquivalentTo(new[] { "isbn:9780441172719" }));
                Assert.That(unmatched.PotentialAuthors.Single().ProviderId, Is.EqualTo("hc:42"));
            });
        }

        [Test]
        public void should_keep_explicit_retry_ambiguous_when_identifier_and_text_results_disagree()
        {
            var catalog = new RecordingCatalogSearch
            {
                IsbnResults = new List<Book> { Candidate("Dune", "Frank Herbert", "9001", "42", isbn13: "9780441172719") },
                OnTextSearch = (title, author) => title == "Dune" && author == "Frank Herbert"
                    ? new List<Book> { Candidate("Dune", "Frank Herbert", "9002", "42", isbn13: "9780441172719") }
                    : new List<Book>()
            };
            var unmatched = Unmatched("/incoming/dune.epub", Tags(("TITLE", "Dune"), ("AUTHOR", "Frank Herbert"), ("ISBN13", "978-0-441-17271-9")));

            var added = UnmappedCatalogSuggestionHelper.AddSuggestions(catalog, LogManager.GetCurrentClassLogger(), new[] { unmatched });

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.Zero);
                Assert.That(catalog.Calls, Is.EquivalentTo(new[] { "isbn:9780441172719", "text:Dune|Frank Herbert" }));
                Assert.That(unmatched.PotentialAuthors, Is.Empty);
            });
        }

        [Test]
        public void should_restore_asin_goodreads_and_provider_id_lookups()
        {
            var asinBook = Candidate("Dune", "Frank Herbert", "9001", "42");
            asinBook.AudibleASIN = "B000123456";
            var goodreadsBook = Candidate("Dune", "Frank Herbert", "9002", "42");
            goodreadsBook.Editions = new List<Edition> { new Edition { GoodreadsEditionId = 881 } };
            var providerBook = Candidate("Dune", "Frank Herbert", "9003", "42");
            var catalog = new RecordingCatalogSearch
            {
                AsinResults = new List<Book> { asinBook },
                GoodreadsResults = new List<Book> { goodreadsBook },
                OnTextSearch = (title, author) => title == "hc:9003" && author == null
                    ? new List<Book> { providerBook }
                    : new List<Book>()
            };
            var unmatched = new[]
            {
                Unmatched("/incoming/asin.m4b", Tags(("ASIN", "B000123456"))),
                Unmatched("/incoming/goodreads.m4b", Tags(("GOODREADS_ID", "881"))),
                Unmatched("/incoming/provider.m4b", Tags(("WORK_PROVIDER_ID", "hc:9003")))
            };

            var added = UnmappedCatalogSuggestionHelper.AddSuggestions(catalog, LogManager.GetCurrentClassLogger(), unmatched, stopAfterFirstCandidate: true);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.EqualTo(3));
                Assert.That(catalog.Calls, Is.EquivalentTo(new[] { "asin:B000123456", "goodreads:881", "text:hc:9003|" }));
                Assert.That(unmatched.Select(file => file.PotentialAuthors.Single().ProviderId), Is.All.EqualTo("hc:42"));
            });
        }

        [Test]
        public void should_prefer_exact_author_title_search()
        {
            var catalog = new RecordingCatalogSearch
            {
                OnTextSearch = (title, author) => title == "Dune" && author == "Frank Herbert"
                    ? new List<Book> { Candidate("Dune", "Frank Herbert", "9001", "42") }
                    : new List<Book>()
            };
            var unmatched = Unmatched("/incoming/Dune.m4b", Tags(("ALBUM", "Dune"), ("ALBUMARTIST", "Frank Herbert")));

            var added = UnmappedCatalogSuggestionHelper.AddSuggestions(catalog, LogManager.GetCurrentClassLogger(), new[] { unmatched }, stopAfterFirstCandidate: true);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.EqualTo(1));
                Assert.That(catalog.Calls, Is.EquivalentTo(new[] { "text:Dune|Frank Herbert" }));
                Assert.That(unmatched.PotentialAuthors.Single().BookProviderId, Is.EqualTo("hc:9001"));
            });
        }

        [Test]
        public void should_not_suggest_when_title_fallback_is_ambiguous()
        {
            var catalog = new RecordingCatalogSearch
            {
                OnTextSearch = (title, author) => author == null && title == "Dune"
                    ? new List<Book>
                    {
                        Candidate("Dune", "Frank Herbert", "9001", "42"),
                        Candidate("Dune", "Another Author", "9002", "43")
                    }
                    : new List<Book>()
            };
            var unmatched = Unmatched("/incoming/dune.epub", Tags(("TITLE", "Dune")));

            var added = UnmappedCatalogSuggestionHelper.AddSuggestions(catalog, LogManager.GetCurrentClassLogger(), new[] { unmatched }, stopAfterFirstCandidate: true);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.Zero);
                Assert.That(unmatched.PotentialAuthors, Is.Empty);
            });
        }

        [Test]
        public void should_keep_catalog_suggestions_inside_the_scoped_author_identity()
        {
            var catalog = new RecordingCatalogSearch
            {
                OnTextSearch = (title, author) => title == "Dune" && author == null
                    ? new List<Book>
                    {
                        Candidate("Dune", "Frank Herbert", "9001", "42"),
                        Candidate("Dune", "Another Author", "9002", "43")
                    }
                    : new List<Book>()
            };
            var unmatched = Unmatched("/incoming/dune.epub", Tags(("TITLE", "Dune")));
            var allowedAuthor = new Author { Name = "Frank Herbert", HardcoverAuthorId = "42" };

            var added = UnmappedCatalogSuggestionHelper.AddSuggestions(
                catalog,
                LogManager.GetCurrentClassLogger(),
                new[] { unmatched },
                allowedAuthor,
                stopAfterFirstCandidate: true);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.EqualTo(1));
                Assert.That(unmatched.PotentialAuthors.Single().ProviderId, Is.EqualTo("hc:42"));
                Assert.That(unmatched.PotentialAuthors.Single().BookProviderId, Is.EqualTo("hc:9001"));
            });
        }

        [Test]
        public void should_use_a_single_file_filename_when_embedded_title_is_missing()
        {
            var catalog = new RecordingCatalogSearch
            {
                OnTextSearch = (title, author) => author == null && title == "Dune"
                    ? new List<Book> { Candidate("Dune", "Frank Herbert", "9001", "42") }
                    : new List<Book>()
            };
            var unmatched = Unmatched("/incoming/Dune.m4b", Tags());

            var added = UnmappedCatalogSuggestionHelper.AddSuggestions(catalog, LogManager.GetCurrentClassLogger(), new[] { unmatched }, stopAfterFirstCandidate: true);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.EqualTo(1));
                Assert.That(catalog.Calls, Is.EquivalentTo(new[] { "text:Dune|" }));
                Assert.That(unmatched.PotentialAuthors.Single().BookProviderId, Is.EqualTo("hc:9001"));
            });
        }

        [Test]
        public void should_retry_the_single_file_name_when_embedded_title_search_has_no_candidates()
        {
            var catalog = new RecordingCatalogSearch
            {
                OnTextSearch = (title, author) => title == "Dune" && author == null
                    ? new List<Book> { Candidate("Dune", "Frank Herbert", "9001", "42") }
                    : new List<Book>()
            };
            var unmatched = Unmatched("/incoming/Dune.m4b", Tags(("ALBUM", "Unparsed Tag"), ("ALBUMARTIST", "Frank Herbert")));

            var added = UnmappedCatalogSuggestionHelper.AddSuggestions(catalog, LogManager.GetCurrentClassLogger(), new[] { unmatched }, stopAfterFirstCandidate: true);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.EqualTo(1));
                Assert.That(catalog.Calls, Is.EquivalentTo(new[]
                {
                    "text:Unparsed Tag|Frank Herbert",
                    "text:Unparsed Tag|",
                    "text:Dune|"
                }));
                Assert.That(unmatched.PotentialAuthors.Single().BookProviderId, Is.EqualTo("hc:9001"));
            });
        }

        [Test]
        public void should_not_broaden_to_text_search_when_identifier_lookup_returns_conflicting_identity()
        {
            var catalog = new RecordingCatalogSearch
            {
                OnTextSearch = (title, _) => title == "hc:999"
                    ? new List<Book> { Candidate("Dune", "Frank Herbert", "9001", "42") }
                    : throw new AssertionException("Text search should not run after a conflicting provider-ID result")
            };
            var unmatched = Unmatched("/incoming/dune.epub", Tags(
                ("TITLE", "Dune"),
                ("AUTHOR", "Frank Herbert"),
                ("WORK_PROVIDER_ID", "hc:999")));

            var added = UnmappedCatalogSuggestionHelper.AddSuggestions(catalog, LogManager.GetCurrentClassLogger(), new[] { unmatched }, stopAfterFirstCandidate: true);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.Zero);
                Assert.That(catalog.Calls, Is.EquivalentTo(new[] { "text:hc:999|" }));
                Assert.That(unmatched.PotentialAuthors, Is.Empty);
            });
        }

        [Test]
        public void should_stop_after_ambiguous_results_at_the_current_search_tier()
        {
            var catalog = new RecordingCatalogSearch
            {
                OnTextSearch = (title, author) => author == null && title == "Dune"
                    ? new List<Book>
                    {
                        Candidate("Dune", "Frank Herbert", "9001", "42"),
                        Candidate("Dune", "Another Author", "9002", "43")
                    }
                    : new List<Book>()
            };
            var unmatched = Unmatched("/incoming/dune.epub", Tags(("TITLE", "Dune")));

            var added = UnmappedCatalogSuggestionHelper.AddSuggestions(catalog, LogManager.GetCurrentClassLogger(), new[] { unmatched }, stopAfterFirstCandidate: true);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.Zero);
                Assert.That(catalog.Calls, Is.EquivalentTo(new[] { "text:Dune|" }));
                Assert.That(unmatched.PotentialAuthors, Is.Empty);
            });
        }

        [Test]
        public void should_honor_cancellation_before_searching_catalog()
        {
            var catalog = new RecordingCatalogSearch();
            var unmatched = Unmatched("/incoming/dune.epub", Tags(("TITLE", "Dune")));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() => UnmappedCatalogSuggestionHelper.AddSuggestions(
                catalog,
                LogManager.GetCurrentClassLogger(),
                new[] { unmatched },
                cancellationToken: cancellation.Token));
            Assert.That(catalog.Calls, Is.Empty);
        }

        [Test]
        public void should_propagate_cancellation_wrapped_by_a_catalog_provider()
        {
            var catalog = new RecordingCatalogSearch
            {
                OnTextSearch = (_, _) => throw new AggregateException(new OperationCanceledException())
            };
            var unmatched = Unmatched("/incoming/dune.epub", Tags(("TITLE", "Dune")));

            Assert.Throws<OperationCanceledException>(() => UnmappedCatalogSuggestionHelper.AddSuggestions(
                catalog,
                LogManager.GetCurrentClassLogger(),
                new[] { unmatched },
                stopAfterFirstCandidate: true));
        }

        [Test]
        public void should_not_search_by_author_alone_for_a_multi_member_file_group()
        {
            var catalog = new RecordingCatalogSearch();
            var unmatched = Unmatched("/incoming/dune.epub", Tags(("AUTHOR", "Frank Herbert")));
            unmatched.File.GroupMemberTags = new[]
            {
                Tags(("AUTHOR", "Frank Herbert")),
                Tags(("AUTHOR", "Frank Herbert"))
            };

            var added = UnmappedCatalogSuggestionHelper.AddSuggestions(
                catalog,
                LogManager.GetCurrentClassLogger(),
                new[] { unmatched },
                stopAfterFirstCandidate: true);

            Assert.Multiple(() =>
            {
                Assert.That(added, Is.Zero);
                Assert.That(catalog.Calls, Is.Empty);
                Assert.That(unmatched.PotentialAuthors, Is.Empty);
            });
        }

        [Test]
        public void catalog_search_should_be_enabled_only_for_matching_contexts_with_provider_identification()
        {
            Assert.Multiple(() =>
            {
                Assert.That(MatchingContextPresets.ForScanLocal().AllowCatalogSearch, Is.False);
                Assert.That(MatchingContextPresets.ForScanV5().AllowCatalogSearch, Is.True);
                Assert.That(MatchingContextPresets.ForManualPreview().AllowCatalogSearch, Is.True);
                Assert.That(MatchingContextPresets.ForDownloaded(true).AllowCatalogSearch, Is.True);
                Assert.That(MatchingContextPresets.ForDownloaded(false).AllowCatalogSearch, Is.False);
                Assert.That(MatchingContextPresets.ForScanScopedRematch().AllowCatalogSearch, Is.False);
                Assert.That(MatchingContextPresets.ForAuthorReady().AllowCatalogSearch, Is.True);
                Assert.That(MatchingContextPresets.ForDirectDefault().AllowCatalogSearch, Is.True);
            });
        }

        private static Book Candidate(string title, string author, string workId, string authorId, string isbn13 = null)
        {
            return new Book
            {
                Title = title,
                HardcoverBookId = workId,
                ISBN13 = isbn13,
                Author = new Author
                {
                    Name = author,
                    HardcoverAuthorId = authorId
                }
            };
        }

        private static UnmatchedFile Unmatched(string path, Dictionary<string, List<string>> tags)
        {
            return new UnmatchedFile
            {
                File = new DiscoveredFileWithMetadata
                {
                    Path = path,
                    AllTags = tags
                },
                Reason = "No local match"
            };
        }

        private static Dictionary<string, List<string>> Tags(params (string Key, string Value)[] tags)
        {
            return tags
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Select(item => item.Value).ToList(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
