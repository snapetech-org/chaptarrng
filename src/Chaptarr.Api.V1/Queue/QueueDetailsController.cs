using System;
using System.Collections.Generic;
using System.Linq;
using Chaptarr.Http;
using Chaptarr.Http.Middleware;
using Chaptarr.Http.REST;
using Chaptarr.Api.V1.MediaTypes;
using NzbDrone.Core.Books;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Queue;
using NzbDrone.SignalR;

namespace Chaptarr.Api.V1.Queue
{
    [V1ApiController("queue/details")]
    public class QueueDetailsController : RestControllerWithSignalR<QueueResource, NzbDrone.Core.Queue.Queue>,
                               IHandle<QueueUpdatedEvent>, IHandle<PendingReleasesUpdatedEvent>
    {
        private readonly IQueueService _queueService;
        private readonly IPendingReleaseService _pendingReleaseService;
        private readonly IBookshelfLegacyIdentityMapService _legacyIdentityMapService;

        public QueueDetailsController(
            IBroadcastSignalRMessage broadcastSignalRMessage,
            IQueueService queueService,
            IPendingReleaseService pendingReleaseService,
            IBookshelfLegacyIdentityMapService legacyIdentityMapService = null)
            : base(broadcastSignalRMessage)
        {
            _queueService = queueService;
            _pendingReleaseService = pendingReleaseService;
            _legacyIdentityMapService = legacyIdentityMapService;
        }

        [NonAction]
        public override ActionResult<QueueResource> GetResourceByIdWithErrorHandler(int id)
        {
            return base.GetResourceByIdWithErrorHandler(id);
        }

        protected override QueueResource GetResourceById(int id)
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        public List<QueueResource> GetQueue(int? authorId, [FromQuery] List<int> bookIds, bool includeAuthor = false, bool includeBook = true, [FromQuery] string mediaType = null)
        {
            var effectiveMediaType = mediaType ?? HttpContext?.GetReadarrFacadeContext()?.MediaType;
            var resolvedBookIds = _legacyIdentityMapService?.ResolveBookIds(bookIds ?? new List<int>(), MediaTypeParameterParser.ParseOptional(effectiveMediaType))
                ?? bookIds ?? new List<int>();
            var queue = QueueMediaTypeFilter.FilterByMediaType(_queueService.GetQueue(), effectiveMediaType);
            var pending = QueueMediaTypeFilter.FilterByMediaType(_pendingReleaseService.GetPendingQueue(), effectiveMediaType);
            var fullQueue = queue.Concat(pending);

            if (authorId.HasValue)
            {
                return fullQueue.Where(q => q.Author?.Id == authorId.Value).ToResource(includeAuthor, includeBook, HttpContext.GetReadarrFacadeContext());
            }

            if (resolvedBookIds.Any())
            {
                return fullQueue.Where(q => q.Book != null && resolvedBookIds.Contains(q.Book.Id)).ToResource(includeAuthor, includeBook, HttpContext.GetReadarrFacadeContext());
            }

            return fullQueue.ToResource(includeAuthor, includeBook, HttpContext.GetReadarrFacadeContext());
        }

        [NonAction]
        public void Handle(QueueUpdatedEvent message)
        {
            BroadcastResourceChange(ModelAction.Sync);
        }

        [NonAction]
        public void Handle(PendingReleasesUpdatedEvent message)
        {
            BroadcastResourceChange(ModelAction.Sync);
        }
    }
}
