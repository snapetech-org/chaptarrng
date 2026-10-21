using System.Collections.Generic;
using Chaptarr.Api.V1.Books;
using NUnit.Framework;
using NzbDrone.Core.Books;

namespace Chaptarr.Core.Test.Api
{
    [TestFixture]
    public class BookResourceWorkProviderIdsFixture
    {
        [Test]
        public void resource_should_expose_only_canonical_work_level_provider_ids()
        {
            var book = new Book
            {
                Title = "Dune",
                MediaType = BookMediaType.Audiobook,
                HardcoverBookId = "123",
                GoodreadsWorkId = "gr:456",
                OpenLibraryWorkId = "OL123W",
                ASIN = "B000123",
                RemoteProviderIds = new HashSet<string>
                {
                    "gr:789",
                    "rn:100"
                }
            };

            var resource = BookResourceMapper.ToResource(book, BookResourceMappingOptions.Lean());

            Assert.That(resource.WorkProviderIds, Is.EquivalentTo(new[]
            {
                "hc:123",
                "gr:456",
                "gr:789",
                "ol:OL123W"
            }));
        }
    }
}
