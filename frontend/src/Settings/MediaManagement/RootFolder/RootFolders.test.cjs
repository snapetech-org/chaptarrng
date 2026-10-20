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

    setState(update) {
      this.state = { ...this.state, ...update };
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

function loadRootFolders(ajaxRequest) {
  const filePath = path.join(__dirname, 'RootFolders.js');
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

  function Stub() {
    return null;
  }

  const modules = {
    'prop-types': PropTypes,
    react: React,
    'Components/FieldSet': Stub,
    'Components/Icon': Stub,
    'Components/Link/Button': Stub,
    'Components/Link/SpinnerButton': Stub,
    'Components/Modal/ConfirmModal': Stub,
    'Components/Page/PageSectionContent': Stub,
    'Components/Table/Table': Stub,
    'Components/Table/TableBody': Stub,
    'Helpers/Props': { icons: { ADD: 'add' }, kinds: { PRIMARY: 'primary', DEFAULT: 'default' } },
    'Helpers/Props/folderTypes': {
      getRootFolderMediaTypeLabel: () => 'Both',
      getRootFolderMediaTypes: () => []
    },
    'Utilities/Array/sortByName': (items) => items,
    'Utilities/createAjaxRequest': ajaxRequest,
    'Utilities/String/translate': (key, args) => args ? `${key}(${args.join(',')})` : key,
    './EditRootFolderModalConnector': Stub,
    './RootFolder': Stub,
    './RootFolders.css': new Proxy({}, { get: () => '' })
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

  return module.exports.default || module.exports;
}

function findElement(tree, type) {
  if (!tree || typeof tree !== 'object') {
    return null;
  }

  if (tree.type === type) {
    return tree;
  }

  const children = tree.props?.children;
  const childList = Array.isArray(children) ? children : [children];

  for (const child of childList) {
    const match = findElement(child, type);

    if (match) {
      return match;
    }
  }

  return null;
}

function collectText(tree, values = []) {
  if (typeof tree === 'string' || typeof tree === 'number') {
    values.push(String(tree));
    return values;
  }

  if (!tree || typeof tree !== 'object') {
    return values;
  }

  const children = tree.props?.children;
  (Array.isArray(children) ? children : [children]).forEach((child) => collectText(child, values));
  return values;
}

function createAjaxHarness() {
  let resolveRequest;
  let rejectRequest;
  let requestOptions;

  return {
    requestOptions: () => requestOptions,
    resolve: (value) => resolveRequest(value),
    reject: (error) => rejectRequest(error),
    create(options) {
      requestOptions = options;
      const request = new Promise((resolve, reject) => {
        resolveRequest = resolve;
        rejectRequest = reject;
      });

      return {
        request,
        abortRequest() {
          return false;
        }
      };
    }
  };
}

function createComponent(RootFolders, onBackfillAuthorRootFolders) {
  return new RootFolders({
    items: [],
    isFetching: false,
    isPopulated: true,
    isDeleting: false,
    isBackfillingAuthorRootFolders: false,
    qualityProfiles: [],
    metadataProfiles: [],
    onConfirmDeleteRootFolder: () => {},
    onBackfillAuthorRootFolders,
    setRootFolderValue: () => {}
  });
}

test('author root backfill reviews the dry-run counts and only runs after confirmation', async () => {
  const ajax = createAjaxHarness();
  const RootFolders = loadRootFolders(ajax.create);
  let backfillCount = 0;
  const component = createComponent(RootFolders, () => backfillCount++);

  component.onReviewAuthorRootBackfillPress();

  assert.deepEqual(JSON.parse(JSON.stringify(ajax.requestOptions())), {
    url: '/author/editor/backfill-roots?dryRun=true',
    method: 'POST',
    dataType: 'json'
  });
  assert.equal(component.state.isPreviewingAuthorRoots, true);

  ajax.resolve({
    candidates: 5,
    updated: 2,
    skippedManuallyOverridden: 1,
    skippedNoDefault: 1,
    skippedNoProfile: 1
  });
  await new Promise((resolve) => setTimeout(resolve, 0));

  assert.equal(component.state.isBackfillPreviewModalOpen, true);
  assert.equal(component.state.backfillPreview.updated, 2);
  assert.equal(backfillCount, 0);

  const previewText = collectText(component.renderAuthorRootBackfillPreview()).join(' ');
  assert.match(previewText, /BackfillAuthorRootFoldersSummary\(2,5\)/);
  assert.match(previewText, /BackfillAuthorRootFoldersHelpText/);

  component.onConfirmAuthorRootBackfill();

  assert.equal(backfillCount, 1);
  assert.equal(component.state.isBackfillPreviewModalOpen, false);
});

test('author root backfill cannot run when the preview has no eligible authors', () => {
  const ajax = createAjaxHarness();
  const RootFolders = loadRootFolders(ajax.create);
  let backfillCount = 0;
  const component = createComponent(RootFolders, () => backfillCount++);

  component.state.backfillPreview = { candidates: 2, updated: 0 };
  component.state.isBackfillPreviewModalOpen = true;
  component.onConfirmAuthorRootBackfill();

  assert.equal(backfillCount, 0);
  assert.equal(component.state.isBackfillPreviewModalOpen, false);
});

test('author root backfill shows a retryable preview error', async () => {
  const ajax = createAjaxHarness();
  const RootFolders = loadRootFolders(ajax.create);
  const component = createComponent(RootFolders, () => {});

  component.onReviewAuthorRootBackfillPress();
  ajax.reject(new Error('offline'));
  await new Promise((resolve) => setTimeout(resolve, 0));

  assert.equal(component.state.isPreviewingAuthorRoots, false);
  assert.equal(component.state.backfillPreviewError, 'UnableToLoadAuthorRootFolderBackfillPreview');
});
