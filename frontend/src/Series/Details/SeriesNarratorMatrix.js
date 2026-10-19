import PropTypes from 'prop-types';
import React, { useEffect, useState } from 'react';
import Label from 'Components/Label';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import styles from './SeriesNarratorMatrix.css';

function SeriesNarratorMatrix({ title, authorId }) {
  const [state, setState] = useState({ isFetching: true, error: false, matrix: null });

  useEffect(() => {
    const query = authorId ? `&authorId=${authorId}` : '';
    const { request, abortRequest } = createAjaxRequest({
      url: `/series/narrators/matrix?title=${encodeURIComponent(title)}${query}`,
      dataType: 'json'
    });

    setState({ isFetching: true, error: false, matrix: null });
    request.done((matrix) => setState({ isFetching: false, error: false, matrix }));
    request.fail((xhr) => {
      if (xhr.aborted) {
        return;
      }

      // A series with no audiobook editions is not an error worth showing.
      setState({ isFetching: false, error: xhr.status !== 404, matrix: null });
    });

    return abortRequest;
  }, [title, authorId]);

  const { isFetching, error, matrix } = state;

  if (isFetching) {
    return <LoadingIndicator />;
  }

  if (error) {
    return <div className={styles.message} role="alert">{translate('NarratorComparisonFailed')}</div>;
  }

  if (!matrix || !matrix.narrators.length) {
    return null;
  }

  const titles = new Map(matrix.books.map((book) => [book.bookId, book.title]));

  return (
    <section className={styles.container} aria-labelledby="series-narrator-matrix">
      <h3 id="series-narrator-matrix">{translate('NarratorComparison')}</h3>
      <p className={styles.help}>{translate('NarratorComparisonHelp')}</p>
      <ul className={styles.list}>
        {matrix.narrators.map((narrator) => (
          <li key={narrator.name} className={styles.narrator}>
            <p className={styles.name}>
              {narrator.name}{' '}
              {narrator.production === 'full-cast' && <Label kind="info">{translate('EditionFullCast')}</Label>}
              {!narrator.missingBookIds.length && <Label kind="success">{translate('NarratorComplete')}</Label>}
            </p>
            <div className={styles.row}>
              {translate('NarratorCoverage')}: {narrator.coveredBookIds.length} / {matrix.totalBooks}
            </div>
            {narrator.ownedBookIds.length > 0 && (
              <div className={styles.row}>
                {translate('NarratorOwned')}: {narrator.ownedBookIds.length}
              </div>
            )}
            {narrator.missingBookIds.length > 0 && (
              <div className={styles.row}>
                {translate('NarratorMissing')}:{' '}
                <span className={styles.volumes}>
                  {narrator.missingBookIds.map((id) => titles.get(id)).filter(Boolean).join(' · ')}
                </span>
              </div>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}

SeriesNarratorMatrix.propTypes = {
  title: PropTypes.string.isRequired,
  authorId: PropTypes.number
};

export default SeriesNarratorMatrix;
