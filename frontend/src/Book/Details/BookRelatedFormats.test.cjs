const test = require('node:test');
const assert = require('node:assert/strict');
const {
  buildAddFormatModalProps,
  canAddFormat,
  getBookFormatInstances
} = require('./BookRelatedFormats.cjs');

test('book details show the current format and only work-matched records in the other format', () => {
  const formats = [
    { bookId: 11, mediaType: 'audiobook', title: 'Dune' },
    { bookId: 12, mediaType: 'ebook', title: 'Dune', disambiguation: '1965' },
    { bookId: 13, mediaType: 'ebook', title: 'Dune: Illustrated' },
    { bookId: 14, mediaType: 'comic', title: 'Dune' }
  ];

  assert.deepEqual(getBookFormatInstances(formats, {
    id: 11,
    mediaType: 'audiobook',
    title: 'Dune',
    narrator: 'Simon Vance'
  }), {
    localAudiobookBooks: [
      { id: 11, mediaType: 'audiobook', title: 'Dune', narrator: 'Simon Vance', disambiguation: undefined }
    ],
    localEbookBooks: [
      { id: 12, mediaType: 'ebook', title: 'Dune', narrator: undefined, disambiguation: '1965' },
      { id: 13, mediaType: 'ebook', title: 'Dune: Illustrated', narrator: undefined, disambiguation: undefined }
    ]
  });
});

test('format instances ignore malformed records and duplicate current records', () => {
  assert.deepEqual(getBookFormatInstances([
    null,
    { bookId: 'not-an-id', mediaType: 'ebook', title: 'Dune' },
    { bookId: 14, mediaType: 'comic', title: 'Dune' },
    { bookId: 15, mediaType: ' EBOOK ', title: 'Dune' },
    { bookId: 11, mediaType: 'audiobook', title: 'Duplicate current row' }
  ], { id: 11, mediaType: 'audiobook', title: 'Dune' }), {
    localAudiobookBooks: [
      { id: 11, mediaType: 'audiobook', title: 'Dune', narrator: undefined, disambiguation: undefined }
    ],
    localEbookBooks: [
      { id: 15, mediaType: 'ebook', title: 'Dune', narrator: undefined, disambiguation: undefined }
    ]
  });
});

test('adding a missing format requires book and author provider identities', () => {
  assert.equal(canAddFormat({ foreignBookId: 'hc:123' }, { foreignAuthorId: 'hc:456' }), true);
  assert.equal(canAddFormat({ foreignBookId: 'hc:123' }, {}), false);
  assert.equal(canAddFormat({}, { foreignAuthorId: 'hc:456' }), false);
});

test('direct add uses the requested format, existing author, and known format records', () => {
  const book = {
    id: 11,
    title: 'Dune',
    mediaType: 'audiobook',
    foreignBookId: 'hc:work-123',
    images: [{ url: '/cover.jpg' }]
  };
  const author = { foreignAuthorId: 'hc:author-456', authorName: 'Frank Herbert', path: '/books/Frank Herbert' };
  const instances = getBookFormatInstances([], book);

  assert.deepEqual(buildAddFormatModalProps(book, author, instances, 'ebook'), {
    initialMediaType: 'ebook',
    foreignBookId: 'hc:work-123',
    foreignAuthorId: 'hc:author-456',
    book: { ...book, author },
    bookTitle: 'Dune',
    seriesTitle: undefined,
    disambiguation: undefined,
    authorName: 'Frank Herbert',
    overview: '',
    folder: '/books/Frank Herbert',
    images: [{ url: '/cover.jpg' }],
    localAudiobookBooks: [
      { id: 11, mediaType: 'audiobook', title: 'Dune', narrator: undefined, disambiguation: undefined }
    ],
    localEbookBooks: [],
    isExistingAuthor: true
  });
});
