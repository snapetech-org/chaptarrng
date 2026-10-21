const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { transformSync } = require('@babel/core');

const propType = () => propType;
propType.isRequired = propType;

const PropTypes = new Proxy({}, {
  get() {
    return propType;
  }
});

function Component(props, context) {
  this.props = props;
  this.context = context;
  this.state = {};
}

Component.prototype.setState = function(update) {
  const nextState = typeof update === 'function' ? update(this.state, this.props) : update;
  this.state = { ...this.state, ...nextState };
};

const React = {
  Component,
  createElement(type, props, ...children) {
    return {
      type,
      props: {
        ...props,
        children: children.length === 1 ? children[0] : children
      }
    };
  }
};

function loadComponent(componentPath) {
  const filePath = path.join(__dirname, componentPath);
  const source = fs.readFileSync(filePath, 'utf8');
  const { code } = transformSync(source, {
    filename: filePath,
    babelrc: false,
    configFile: false,
    presets: [
      [
        '@babel/preset-env',
        {
          targets: { browsers: ['last 2 versions'] },
          loose: true,
          modules: 'commonjs'
        }
      ],
      '@babel/preset-react'
    ]
  });

  const moduleMocks = new Map([
    ['react', { __esModule: true, default: React, Component }],
    ['prop-types', { __esModule: true, default: PropTypes }],
    ['Components/Alert', { __esModule: true, default: 'Alert' }],
    ['Components/Form/TextInput', { __esModule: true, default: 'TextInput' }],
    ['Components/Link/Button', { __esModule: true, default: 'Button' }],
    ['Components/Loading/LoadingIndicator', { __esModule: true, default: 'LoadingIndicator' }],
    ['Components/Modal/ModalBody', { __esModule: true, default: 'ModalBody' }],
    ['Components/Modal/ModalContent', { __esModule: true, default: 'ModalContent' }],
    ['Components/Modal/ModalFooter', { __esModule: true, default: 'ModalFooter' }],
    ['Components/Modal/ModalHeader', { __esModule: true, default: 'ModalHeader' }],
    ['Components/Scroller/Scroller', { __esModule: true, default: 'Scroller' }],
    ['Components/Table/Table', { __esModule: true, default: 'Table' }],
    ['Components/Table/TableBody', { __esModule: true, default: 'TableBody' }],
    ['Helpers/Props', {
      kinds: { DANGER: 'danger', INFO: 'info' },
      scrollDirections: { NONE: 'none' }
    }],
    ['Utilities/Object/getErrorMessage', {
      __esModule: true,
      default: (_error, fallback) => fallback
    }],
    ['Utilities/String/translate', { __esModule: true, default: (key) => key }],
    ['./SelectBookRow', { __esModule: true, default: 'SelectBookRow' }],
    ['./SelectAuthorRow', { __esModule: true, default: 'SelectAuthorRow' }],
    ['./SelectBookModalContent.css', { __esModule: true, default: { modalBody: 'modalBody', filterInput: 'filterInput', scroller: 'scroller', emptyState: 'emptyState' } }],
    ['./SelectAuthorModalContent.css', { __esModule: true, default: { modalBody: 'modalBody', filterInput: 'filterInput', scroller: 'scroller', emptyState: 'emptyState' } }]
  ]);

  const sandbox = {
    exports: {},
    require(moduleName) {
      return moduleMocks.get(moduleName) || { __esModule: true, default: moduleName };
    }
  };

  vm.runInNewContext(code, sandbox, { filename: filePath });
  return sandbox.exports.default;
}

function findElements(element, predicate, matches = []) {
  if (Array.isArray(element)) {
    element.forEach((child) => findElements(child, predicate, matches));
    return matches;
  }

  if (!element || typeof element !== 'object') {
    return matches;
  }

  if (predicate(element)) {
    matches.push(element);
  }

  findElements(element.props?.children, predicate, matches);
  return matches;
}

function renderBookPicker(overrides = {}) {
  const SelectBookModalContent = loadComponent('Book/SelectBookModalContent.js');
  const noop = () => null;
  const component = new SelectBookModalContent({
    items: [],
    isFetching: false,
    isPopulated: true,
    onBookSelect: noop,
    onRetryPress: noop,
    onModalClose: noop,
    ...overrides
  });

  return { component, output: component.render() };
}

function renderAuthorPicker(overrides = {}) {
  const SelectAuthorModalContent = loadComponent('Author/SelectAuthorModalContent.js');
  const noop = () => null;
  const component = new SelectAuthorModalContent({
    items: [],
    onAuthorSelect: noop,
    onModalClose: noop,
    ...overrides
  });

  return { component, output: component.render() };
}

test('book picker distinguishes an empty library from a filter with no matches', () => {
  const empty = renderBookPicker().output;
  const emptyAlert = findElements(empty, (element) => element.type === 'Alert')[0];
  assert.equal(emptyAlert.props.children, 'ManualImportNoBooksAvailable');

  const { component, output } = renderBookPicker({
    items: [{ id: 1, title: 'Dune' }]
  });
  component.state.filter = 'not here';
  const filtered = component.render();
  const filterAlert = findElements(filtered, (element) => element.type === 'Alert')[0];
  const clearButton = findElements(filtered, (element) => element.type === 'Button')[0];

  assert.equal(filterAlert.props.children[0].props.children, 'ManualImportNoBooksMatchFilter');
  assert.equal(clearButton.props.children, 'ClearFilter');
  clearButton.props.onPress();
  assert.equal(component.state.filter, '');
  assert.ok(findElements(output, (element) => element.type === 'Table').length);
});

test('book picker hides stale rows during loading and retries a failed load', () => {
  const loading = renderBookPicker({
    items: [{ id: 1, title: 'Stale result' }],
    isFetching: true,
    isPopulated: true
  }).output;
  assert.equal(findElements(loading, (element) => element.type === 'Table').length, 0);
  assert.equal(findElements(loading, (element) => element.type === 'LoadingIndicator').length, 1);

  let retries = 0;
  const error = renderBookPicker({
    items: [{ id: 1, title: 'Stale result' }],
    isPopulated: false,
    error: { message: 'offline' },
    onRetryPress: () => retries++
  }).output;
  const errorAlert = findElements(error, (element) => element.type === 'Alert')[0];
  const retryButton = findElements(error, (element) => element.type === 'Button')[0];

  assert.equal(errorAlert.props.role, 'alert');
  assert.equal(findElements(error, (element) => element.type === 'Table').length, 0);
  assert.equal(retryButton.props.children, 'Retry');
  retryButton.props.onPress();
  assert.equal(retries, 1);
});

test('author picker explains empty and filtered results and clears the filter', () => {
  const empty = renderAuthorPicker().output;
  const emptyAlert = findElements(empty, (element) => element.type === 'Alert')[0];
  assert.equal(emptyAlert.props.children, 'ManualImportNoAuthorsAvailable');

  const { component } = renderAuthorPicker({
    items: [{ id: 2, authorName: 'Frank Herbert' }]
  });
  component.state.filter = 'not here';
  const filtered = component.render();
  const filterAlert = findElements(filtered, (element) => element.type === 'Alert')[0];
  const clearButton = findElements(filtered, (element) => element.type === 'Button')[0];

  assert.equal(filterAlert.props.children[0].props.children, 'ManualImportNoAuthorsMatchFilter');
  assert.equal(clearButton.props.children, 'ClearFilter');
  clearButton.props.onPress();
  assert.equal(component.state.filter, '');
});
