import PropTypes from 'prop-types';
import React, { Component } from 'react';
import intlFormatters from 'Utilities/Localization/formatters.cjs';
import { getLocale } from 'Utilities/Localization/locale';
import TextInput from './TextInput';

const { formatNumber, parseLocalizedNumber } = intlFormatters;

function formatInputValue(value) {
  return value == null ? '' : formatNumber(value, {
    useGrouping: false,
    maximumFractionDigits: 20
  }, getLocale());
}

function parseValue(props, value) {
  const {
    isFloat,
    min,
    max
  } = props;

  if (value == null || value === '') {
    return null;
  }

  let newValue = parseLocalizedNumber(value, {
    locale: getLocale(),
    isFloat
  });

  if (newValue == null) {
    return null;
  }

  if (min != null && newValue < min) {
    newValue = min;
  } else if (max != null && newValue > max) {
    newValue = max;
  }

  return newValue;
}

class NumberInput extends Component {
  constructor(props, context) {
    super(props, context);

    this.state = {
      value: formatInputValue(props.value),
      isFocused: false
    };
  }

  componentDidUpdate(prevProps) {
    const { value } = this.props;

    if (!isNaN(value) && value !== prevProps.value && !this.state.isFocused) {
      this.setState({ value: formatInputValue(value) });
    }
  }

  onChange = ({ name, value }) => {
    this.setState({ value });

    this.props.onChange({
      name,
      value: parseValue(this.props, value)
    });
  };

  onFocus = () => {
    this.setState({ isFocused: true });
  };

  onBlur = () => {
    const { name, onChange } = this.props;
    const { value } = this.state;
    const parsedValue = parseValue(this.props, value);

    this.setState({
      value: formatInputValue(parsedValue),
      isFocused: false
    });

    onChange({
      name,
      value: parsedValue
    });
  };

  render() {
    return (
      <TextInput
        {...this.props}
        type="text"
        inputMode={this.props.isFloat ? 'decimal' : 'numeric'}
        value={this.state.value}
        onChange={this.onChange}
        onBlur={this.onBlur}
        onFocus={this.onFocus}
      />
    );
  }
}

NumberInput.propTypes = {
  name: PropTypes.string.isRequired,
  value: PropTypes.number,
  min: PropTypes.number,
  max: PropTypes.number,
  isFloat: PropTypes.bool.isRequired,
  onChange: PropTypes.func.isRequired
};

NumberInput.defaultProps = {
  value: null,
  isFloat: false
};

export default NumberInput;
