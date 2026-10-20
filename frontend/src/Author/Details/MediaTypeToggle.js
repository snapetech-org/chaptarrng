import PropTypes from 'prop-types';
import React from 'react';
import SegmentedToggle from 'Components/Form/SegmentedToggle';
import translate from 'Utilities/String/translate';
import styles from './MediaTypeToggle.css';

function MediaTypeToggle(props) {
  const {
    selectedMediaType,
    onMediaTypeChange,
    hasAudiobookRootFolder = true,
    hasEbookRootFolder = true,
    includeBoth = false
  } = props;

  const options = [
    {
      key: 'audiobook',
      label: translate('Audiobooks'),
      icon: <span className={styles.icon}>🎧</span>,
      isDisabled: !hasAudiobookRootFolder
    },
    {
      key: 'ebook',
      label: translate('Ebooks'),
      icon: <span className={styles.icon}>📖</span>,
      isDisabled: !hasEbookRootFolder
    }
  ];

  if (includeBoth) {
    options.push({
      key: 'both',
      label: translate('Formats'),
      icon: <span className={styles.icon}>{'🎧📖'}</span>,
      isDisabled: !hasAudiobookRootFolder && !hasEbookRootFolder
    });
  }

  return (
    <div className={styles.mediaToggleContainer}>
      <SegmentedToggle
        value={selectedMediaType}
        options={options}
        onChange={onMediaTypeChange}
      />
    </div>
  );
}

MediaTypeToggle.propTypes = {
  selectedMediaType: PropTypes.oneOf(['audiobook', 'ebook', 'both']).isRequired,
  onMediaTypeChange: PropTypes.func.isRequired,
  hasAudiobookRootFolder: PropTypes.bool,
  hasEbookRootFolder: PropTypes.bool,
  includeBoth: PropTypes.bool
};

export default MediaTypeToggle;
