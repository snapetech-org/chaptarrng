const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { transformSync } = require('@babel/core');

const propType = new Proxy(() => propType, {
  get() {
    return propType;
  },
  apply() {
    return propType;
  }
});

const PropTypes = new Proxy({}, {
  get() {
    return propType;
  }
});

const React = {
  Component: class Component {
    constructor(props) {
      this.props = props;
    }

    setState(update, callback) {
      this.state = { ...this.state, ...update };

      if (callback) {
        return callback();
      }

      return undefined;
    }
  },
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

function loadConnector(fileName, extraModules = {}) {
  const filePath = path.join(__dirname, fileName);
  const source = fs.readFileSync(filePath, 'utf8');
  const { code } = transformSync(source, {
    filename: filePath,
    babelrc: false,
    configFile: false,
    presets: [
      ['@babel/preset-env', { targets: { node: 'current' }, modules: 'commonjs' }],
      '@babel/preset-react'
    ]
  });

  const modules = {
    'prop-types': PropTypes,
    react: React,
    'react-redux': {
      connect: () => (component) => component
    },
    'lodash/map': (items, mapper) => items.map(mapper),
    'Utilities/Array/sortByName': (items) => items,
    'Utilities/createAjaxRequest': () => {
      const request = {
        then() {
          return this;
        },
        catch() {
          return this;
        }
      };

      return {
        request,
        abortRequest() {
          return false;
        }
      };
    },
    'Utilities/String/translate': (key) => key,
    'Helpers/Props': { metadataProfileNames: { NONE: 'None' } },
    './SelectInput': function SelectInput() {
      return null;
    },
    ...extraModules
  };

  const module = { exports: {} };
  vm.runInNewContext(code, {
    module,
    exports: module.exports,
    require(name) {
      assert.ok(Object.prototype.hasOwnProperty.call(modules, name), `Unexpected module: ${name}`);
      return modules[name];
    }
  }, { filename: filePath });

  return module.exports.default;
}

function toPlainValue(value) {
  return JSON.parse(JSON.stringify(value));
}

test('author quality profile can reset to its root folder default as a nullable API value', () => {
  const QualityProfileSelectInputConnector = loadConnector('QualityProfileSelectInputConnector.js');
  const changes = [];
  const connector = new QualityProfileSelectInputConnector({
    name: 'audiobookQualityProfileId',
    value: null,
    includeNone: true,
    noneValue: null,
    noneLabel: 'Use root folder defaults',
    includeNoChange: false,
    includeMixed: false,
    onChange: (change) => changes.push(change)
  });

  connector.state = {
    profiles: [{ id: 41, name: 'Audiobook Default' }],
    isFetching: false
  };

  assert.equal(connector.render().props.value, 'none');
  assert.deepEqual(toPlainValue(connector.getSelectValues().map((item) => item.key)), ['none', '41']);

  connector.onChange({ name: 'audiobookQualityProfileId', value: 'none' });
  connector.onChange({ name: 'audiobookQualityProfileId', value: '41' });

  assert.deepEqual(toPlainValue(changes), [
    { name: 'audiobookQualityProfileId', value: null },
    { name: 'audiobookQualityProfileId', value: 41 }
  ]);
});

test('existing quality profile selectors retain their none sentinel by default', () => {
  const QualityProfileSelectInputConnector = loadConnector('QualityProfileSelectInputConnector.js');
  const changes = [];
  const connector = new QualityProfileSelectInputConnector({
    name: 'qualityProfileId',
    includeNone: true,
    noneValue: 'none',
    includeNoChange: false,
    includeMixed: false,
    onChange: (change) => changes.push(change)
  });

  connector.onChange({ name: 'qualityProfileId', value: 'none' });

  assert.deepEqual(toPlainValue(changes), [{ name: 'qualityProfileId', value: 'none' }]);
});

test('author metadata profile can reset to root defaults without selecting a different profile', () => {
  const MetadataProfileSelectInputConnector = loadConnector('MetadataProfileSelectInputConnector.js');
  const changes = [];
  const connector = new MetadataProfileSelectInputConnector({
    name: 'ebookMetadataProfileId',
    value: null,
    includeNone: true,
    includeRootFolderDefault: true,
    rootFolderDefaultLabel: 'Use root folder defaults',
    includeNoChange: false,
    includeMixed: false,
    onChange: (change) => changes.push(change)
  });

  connector.state = {
    profiles: [
      { id: 17, name: 'Fiction' },
      { id: 18, name: 'None' }
    ],
    isFetching: false
  };

  assert.equal(connector.render().props.value, 'rootFolderDefault');
  assert.deepEqual(toPlainValue(connector.getSelectValues().map((item) => item.key)), ['rootFolderDefault', '17', '18']);

  connector.checkAndSetDefaultValue();
  connector.onChange({ name: 'ebookMetadataProfileId', value: 'rootFolderDefault' });
  connector.onChange({ name: 'ebookMetadataProfileId', value: '17' });

  assert.deepEqual(toPlainValue(changes), [
    { name: 'ebookMetadataProfileId', value: null },
    { name: 'ebookMetadataProfileId', value: 17 }
  ]);
});
