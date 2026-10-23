using System.Collections.Generic;
using System.Linq;
using Chaptarr.Api.V1.MediaTypes;
using Chaptarr.Http;
using Chaptarr.Http.Middleware;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;

namespace Chaptarr.Api.V1.Books
{
    [V1ApiController("rename")]
    public class RenameBookController : Controller
    {
        private readonly IRenameBookFileService _renameBookFileService;
        private readonly IBookshelfLegacyIdentityMapService _legacyIdentityMapService;

        public RenameBookController(
            IRenameBookFileService renameBookFileService,
            IBookshelfLegacyIdentityMapService legacyIdentityMapService = null)
        {
            _renameBookFileService = renameBookFileService;
            _legacyIdentityMapService = legacyIdentityMapService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<RenameBookResource>), 200)]
        [ProducesResponseType(typeof(ApiErrorResource), 400)]
        public ActionResult<List<RenameBookResource>> GetBookFiles(int authorId, int? bookId, string mediaType = null, bool moveToCanonicalAuthorFolder = false)
        {
            mediaType ??= HttpContext?.GetReadarrFacadeContext()?.MediaType;
            if (bookId.HasValue)
            {
                if (moveToCanonicalAuthorFolder)
                {
                    return BadRequest(new ApiErrorResource
                    {
                        Message = "moveToCanonicalAuthorFolder is only supported for author-level previews; omit bookId."
                    });
                }

                var resolvedBookIds = _legacyIdentityMapService?.ResolveBookIds(
                    new[] { bookId.Value },
                    MediaTypeParameterParser.ParseOptional(mediaType));
                if (resolvedBookIds != null)
                {
                    if (!resolvedBookIds.Any())
                    {
                        return NotFound();
                    }

                    bookId = resolvedBookIds[0];
                }

                return _renameBookFileService.GetRenamePreviews(authorId, bookId.Value).ToResource();
            }

            return _renameBookFileService.GetRenamePreviews(authorId, MediaTypeParameterParser.NormalizeOptional(mediaType), moveToCanonicalAuthorFolder).ToResource();
        }
    }
}
