import PropTypes from 'prop-types';
import React from 'react';
import Link from 'Components/Link/Link';
import BidiText from 'Components/BidiText';

function BookTitleLink({ bookId, title, disambiguation }) {
  const link = `/book/${bookId}`;

  return (
    <Link to={link}>
      <BidiText>{title}{disambiguation ? ` (${disambiguation})` : ''}</BidiText>
    </Link>
  );
}

BookTitleLink.propTypes = {
  bookId: PropTypes.number.isRequired,
  title: PropTypes.string.isRequired,
  disambiguation: PropTypes.string
};

export default BookTitleLink;
