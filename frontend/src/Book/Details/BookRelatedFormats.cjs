function normalizeMediaType(mediaType) {
  return (mediaType || '').toString().trim().toLowerCase();
}

function getBookFormatInstances(formats, currentBook) {
  const related = Array.isArray(formats) ? formats : [];
  const currentMediaType = normalizeMediaType(currentBook?.mediaType);
  const instances = [];

  if (
    Number.isInteger(Number(currentBook?.id)) &&
    Number(currentBook.id) > 0 &&
    ['audiobook', 'ebook'].includes(currentMediaType)
  ) {
    instances.push({
      id: Number(currentBook.id),
      mediaType: currentMediaType,
      title: currentBook.title,
      narrator: currentBook.narrator,
      disambiguation: currentBook.disambiguation
    });
  }

  related.forEach((format) => {
    const id = Number(format?.bookId);
    const mediaType = normalizeMediaType(format?.mediaType);

    if (
      !Number.isInteger(id) ||
      id <= 0 ||
      !['audiobook', 'ebook'].includes(mediaType) ||
      (currentMediaType && mediaType === currentMediaType) ||
      instances.some((instance) => instance.id === id)
    ) {
      return;
    }

    instances.push({
      id,
      mediaType,
      title: format.title,
      narrator: format.narrator,
      disambiguation: format.disambiguation
    });
  });

  return {
    localAudiobookBooks: instances.filter((instance) => instance.mediaType === 'audiobook'),
    localEbookBooks: instances.filter((instance) => instance.mediaType === 'ebook')
  };
}

function canAddFormat(book, author) {
  return !!(
    String(book?.foreignBookId ?? '').trim() &&
    String(author?.foreignAuthorId ?? '').trim()
  );
}

function buildAddFormatModalProps(book, author, instances, mediaType) {
  const title = book?.title || '';

  return {
    initialMediaType: mediaType,
    foreignBookId: book?.foreignBookId,
    foreignAuthorId: author?.foreignAuthorId,
    book: { ...book, author },
    bookTitle: title,
    seriesTitle: book?.seriesTitle,
    disambiguation: book?.disambiguation,
    authorName: author?.authorName || author?.authorNameLastFirst || title,
    overview: book?.overview || '',
    folder: author?.folder || author?.path,
    images: Array.isArray(book?.images) ? book.images : [],
    localAudiobookBooks: instances.localAudiobookBooks,
    localEbookBooks: instances.localEbookBooks,
    isExistingAuthor: true
  };
}

module.exports = { buildAddFormatModalProps, canAddFormat, getBookFormatInstances };
