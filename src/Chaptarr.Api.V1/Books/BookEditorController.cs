using System.Linq;
using System.Collections.Generic;
using Chaptarr.Api.V1.MediaTypes;
using Chaptarr.Http;
using Chaptarr.Http.Middleware;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Books;
using NzbDrone.Core.Messaging.Commands;

namespace Chaptarr.Api.V1.Books
{
    [V1ApiController("book/editor")]
    public class BookEditorController : Controller
    {
        private readonly IBookService _bookService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IBookshelfLegacyIdentityMapService _legacyIdentityMapService;

        public BookEditorController(
            IBookService bookService,
            IManageCommandQueue commandQueueManager,
            IBookshelfLegacyIdentityMapService legacyIdentityMapService = null)
        {
            _bookService = bookService;
            _commandQueueManager = commandQueueManager;
            _legacyIdentityMapService = legacyIdentityMapService;
        }

        [HttpPut] // nosemgrep: csharp.dotnet.security.mvc-missing-antiforgery.mvc-missing-antiforgery -- API fallback auth accepts API keys or bearer tokens, never browser cookies.
        [Consumes("application/json")]
        public IActionResult SaveAll([FromBody] BookEditorResource resource)
        {
            // Validate mediaType up front: absent = legacy (per-book), audiobook/ebook = scoped,
            // anything else (including "all") returns 400 instead of silently crossing the
            // audiobook/ebook boundary.
            var effectiveMediaType = resource.MediaType ?? HttpContext?.GetReadarrFacadeContext()?.MediaType;
            var mediaType = MediaTypeParameterParser.ParseOptional(effectiveMediaType, allowAll: false);

            var requestedBookIds = resource.BookIds ?? new List<int>();
            var bookIds = string.IsNullOrWhiteSpace(effectiveMediaType)
                ? requestedBookIds.Where(id => id > 0).Distinct().ToList()
                : _legacyIdentityMapService?.ResolveBookIds(requestedBookIds, mediaType) ?? requestedBookIds;
            var booksToUpdate = _bookService.GetBooks(bookIds);

            foreach (var book in booksToUpdate)
            {
                if (!resource.Monitored.HasValue)
                {
                    continue;
                }

                switch (mediaType ?? book.MediaType)
                {
                    case BookMediaType.Audiobook:
                        book.AudiobookMonitored = resource.Monitored.Value;
                        break;
                    case BookMediaType.Ebook:
                        book.EbookMonitored = resource.Monitored.Value;
                        break;
                }
            }

            _bookService.UpdateMany(booksToUpdate);
            return Accepted(booksToUpdate.ToResource());
        }

        [HttpDelete]
        [Consumes("application/json")]
        public void DeleteBook([FromBody] BookEditorResource resource)
        {
            var effectiveMediaType = resource.MediaType ?? HttpContext?.GetReadarrFacadeContext()?.MediaType;
            var requestedBookIds = resource.BookIds ?? new List<int>();
            var bookIds = string.IsNullOrWhiteSpace(effectiveMediaType)
                ? requestedBookIds.Where(id => id > 0).Distinct().ToList()
                : _legacyIdentityMapService?.ResolveBookIds(requestedBookIds, MediaTypeParameterParser.ParseOptional(effectiveMediaType)) ?? requestedBookIds;

            foreach (var bookId in bookIds)
            {
                _bookService.DeleteBook(bookId, resource.DeleteFiles ?? false, resource.AddImportListExclusion ?? false);
            }
        }
    }
}
