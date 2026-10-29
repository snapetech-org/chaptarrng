import PropTypes from 'prop-types';
import React from 'react';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons } from 'Helpers/Props';
import getUniqueElememtId from 'Utilities/getUniqueElementId';
import translate from 'Utilities/String/translate';
import ModalHeader from './ModalHeader';
import styles from './ModalContent.css';

function ModalContent(props) {
  const [titleId] = React.useState(() => getUniqueElememtId());
  const {
    className,
    children,
    showCloseButton,
    onModalClose,
    ...otherProps
  } = props;

  const hasTitle = React.Children.toArray(children).some((child) =>
    React.isValidElement(child) && child.type === ModalHeader
  );
  const childrenWithAccessibleTitle = React.Children.map(children, (child) => {
    if (React.isValidElement(child) && child.type === ModalHeader) {
      return React.cloneElement(child, { id: titleId });
    }

    return child;
  });

  return (
    <div
      className={className}
      role="dialog"
      aria-modal="true"
      aria-labelledby={hasTitle ? titleId : undefined}
      aria-label={hasTitle ? undefined : (typeof document !== 'undefined' ? document.title : 'Dialog')}
      {...otherProps}
    >
      {
        showCloseButton &&
          <Link
            className={styles.closeButton}
            aria-label={translate('Close')}
            onPress={onModalClose}
          >
            <Icon
              name={icons.CLOSE}
              size={18}
            />
          </Link>
      }

      {childrenWithAccessibleTitle}
    </div>
  );
}

ModalContent.propTypes = {
  className: PropTypes.string,
  children: PropTypes.node,
  showCloseButton: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired
};

ModalContent.defaultProps = {
  className: styles.modalContent,
  showCloseButton: true
};

export default ModalContent;
