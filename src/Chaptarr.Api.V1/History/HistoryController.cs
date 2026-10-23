using System;
using System.Collections.Generic;
using System.Linq;
using Chaptarr.Api.V1.Author;
using Chaptarr.Api.V1.Books;
using Chaptarr.Api.V1.MediaTypes;
using Chaptarr.Http;
using Chaptarr.Http.Extensions;
using Chaptarr.Http.Middleware;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Download;
using NzbDrone.Core.History;

namespace Chaptarr.Api.V1.History
{
    [V1ApiController]
    public class HistoryController : Controller
    {
        private readonly IHistoryService _historyService;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly IUpgradableSpecification _upgradableSpecification;
        private readonly IFailedDownloadService _failedDownloadService;
        private readonly IAuthorService _authorService;
        private readonly IBookshelfLegacyIdentityMapService _legacyIdentityMapService;

        public HistoryController(IHistoryService historyService,
                             ICustomFormatCalculationService formatCalculator,
                             IUpgradableSpecification upgradableSpecification,
                             IFailedDownloadService failedDownloadService,
                             IAuthorService authorService,
                             IBookshelfLegacyIdentityMapService legacyIdentityMapService = null)
        {
            _historyService = historyService;
            _formatCalculator = formatCalculator;
            _upgradableSpecification = upgradableSpecification;
            _failedDownloadService = failedDownloadService;
            _authorService = authorService;
            _legacyIdentityMapService = legacyIdentityMapService;
        }

        protected HistoryResource MapToResource(EntityHistory model, bool includeAuthor, bool includeBook)
        {
            var resource = model.ToResource(_formatCalculator);

            if (includeAuthor)
            {
                resource.Author = model.Author != null ? model.Author.ToResource(HttpContext.GetReadarrFacadeContext()) : null;
            }

            if (includeBook)
            {
                resource.Book = model.Book != null ? model.Book.ToResource(new BookResourceMappingOptions { FacadeContext = HttpContext.GetReadarrFacadeContext() }) : null;
            }

            if (model.Author != null)
            {
                var qualityProfile = model.Author.GetQualityProfileForQuality(resource.Quality.Quality);
                resource.QualityCutoffNotMet = qualityProfile != null ? _upgradableSpecification.QualityCutoffNotMet(qualityProfile, resource.Quality) : false;
            }

            return resource;
        }

        [HttpGet]
        [Produces("application/json")]
        public PagingResource<HistoryResource> GetHistory([FromQuery] PagingRequestResource paging, bool includeAuthor, bool includeBook, [FromQuery(Name = "eventType")] int[] eventTypes, int? bookId, string downloadId, [FromQuery] string mediaType = null)
        {
            mediaType ??= HttpContext?.GetReadarrFacadeContext()?.MediaType;
            var parsedMediaType = MediaTypeParameterParser.ParseOptional(mediaType);
            var pagingResource = new PagingResource<HistoryResource>(paging);
            var pagingSpec = pagingResource.MapToPagingSpec<HistoryResource, EntityHistory>("date", SortDirection.Descending);

            if (eventTypes != null && eventTypes.Any())
            {
                pagingSpec.FilterExpressions.Add(v => eventTypes.Contains((int)v.EventType));
            }

            if (bookId.HasValue)
            {
                var resolvedBookIds = _legacyIdentityMapService?.ResolveBookIds(new[] { bookId.Value }, parsedMediaType)
                    ?? new List<int> { bookId.Value };
                var bookIds = resolvedBookIds.ToArray();
                pagingSpec.FilterExpressions.Add(h => bookIds.Contains(h.BookId));
            }

            if (downloadId.IsNotNullOrWhiteSpace())
            {
                pagingSpec.FilterExpressions.Add(h => h.DownloadId == downloadId);
            }

            return pagingSpec.ApplyToPage(spec => _historyService.Paged(spec, parsedMediaType), h => MapToResource(h, includeAuthor, includeBook));
        }

        [HttpGet("since")]
        public List<HistoryResource> GetHistorySince(DateTime date, EntityHistoryEventType? eventType = null, bool includeAuthor = false, bool includeBook = false, [FromQuery] string mediaType = null)
        {
            return FilterByMediaType(_historyService.Since(date, eventType), mediaType).Select(h => MapToResource(h, includeAuthor, includeBook)).ToList();
        }

        [HttpGet("author")]
        public List<HistoryResource> GetAuthorHistory(int authorId, int? bookId = null, EntityHistoryEventType? eventType = null, bool includeAuthor = false, bool includeBook = false, [FromQuery] string mediaType = null)
        {
            mediaType ??= HttpContext?.GetReadarrFacadeContext()?.MediaType;
            var author = _authorService.GetAuthor(authorId);

            if (bookId.HasValue)
            {
                var parsedMediaType = MediaTypeParameterParser.ParseOptional(mediaType);
                var resolvedBookIds = _legacyIdentityMapService?.ResolveBookIds(new[] { bookId.Value }, parsedMediaType)
                    ?? new List<int> { bookId.Value };
                var history = resolvedBookIds
                    .SelectMany(resolvedBookId => _historyService.GetByBook(resolvedBookId, eventType) ?? new List<EntityHistory>())
                    .GroupBy(entry => entry.Id)
                    .Select(group => group.First());
                return FilterByMediaType(history, mediaType).Select(h =>
                {
                    h.Author = author;

                    return MapToResource(h, includeAuthor, includeBook);
                }).ToList();
            }

            return FilterByMediaType(_historyService.GetByAuthor(authorId, eventType), mediaType).Select(h =>
            {
                h.Author = author;

                return MapToResource(h, includeAuthor, includeBook);
            }).ToList();
        }

        private static IEnumerable<EntityHistory> FilterByMediaType(IEnumerable<EntityHistory> history, string mediaType)
        {
            var parsedMediaType = MediaTypeParameterParser.ParseOptional(mediaType);
            if (!parsedMediaType.HasValue)
            {
                return history;
            }

            return history.Where(h => h?.Book != null && h.Book.MediaType == parsedMediaType.Value);
        }

        [HttpPost("failed/{id}")]
        public object MarkAsFailed([FromRoute] int id)
        {
            _failedDownloadService.MarkAsFailed(id);
            return new { };
        }
    }
}
