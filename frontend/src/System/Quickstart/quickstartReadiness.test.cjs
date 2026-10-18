const test = require('node:test');
const assert = require('node:assert/strict');
const {
  getQuickstartConnectionVisibility,
  getQuickstartReadiness
} = require('./quickstartReadiness.cjs');

const populated = (items) => ({ isPopulated: true, items });

test('waits for all existing settings requests before deciding guided readiness', () => {
  const readiness = getQuickstartReadiness({
    indexersState: populated([]),
    downloadClientsState: { isPopulated: false, items: [] },
    rootFoldersState: populated([])
  });

  assert.equal(readiness.isLoaded, false);
  assert.equal(readiness.showGuidance, false);
});

test('starts on the first missing core section and skips configured sections', () => {
  const readiness = getQuickstartReadiness({
    indexersState: populated([{ enable: true }]),
    downloadClientsState: populated([]),
    rootFoldersState: populated([{ id: 1 }])
  });

  assert.equal(readiness.showGuidance, true);
  assert.equal(readiness.guidedInitialSectionKey, 'downloadClients');
  assert.deepEqual(readiness.guidedCompletedSectionKeys, ['rootFolders', 'indexers']);
});

test('starts with the root format choice when no root exists', () => {
  const readiness = getQuickstartReadiness({
    indexersState: populated([{ enable: true }]),
    downloadClientsState: populated([{ enable: true }]),
    rootFoldersState: populated([])
  });

  assert.equal(readiness.showGuidance, true);
  assert.equal(readiness.guidedInitialSectionKey, 'rootFolders');
  assert.deepEqual(readiness.guidedCompletedSectionKeys, ['indexers', 'downloadClients']);
});

test('moves to root folders after indexers and clients are reviewed as skipped', () => {
  const readiness = getQuickstartReadiness({
    indexersState: populated([]),
    downloadClientsState: populated([]),
    rootFoldersState: populated([]),
    interactions: { indexers: true, downloadClients: true }
  });

  assert.equal(readiness.showGuidance, true);
  assert.equal(readiness.guidedInitialSectionKey, 'rootFolders');
  assert.deepEqual(readiness.guidedCompletedSectionKeys, ['indexers', 'downloadClients']);
});

test('ends guided setup when an existing root is present and missing backends were skipped', () => {
  const readiness = getQuickstartReadiness({
    indexersState: populated([]),
    downloadClientsState: populated([]),
    rootFoldersState: populated([{ id: 1 }]),
    interactions: { indexers: true, downloadClients: true }
  });

  assert.equal(readiness.showGuidance, false);
  assert.deepEqual(readiness.guidedCompletedSectionKeys, ['rootFolders', 'indexers', 'downloadClients']);
});

test('shows AudioBookShelf only for audiobook workflows and keeps shared book connections', () => {
  const visibility = getQuickstartConnectionVisibility({
    configuredRootFormats: ['ebook']
  });

  assert.equal(visibility.showAudioBookShelf, false);
  assert.equal(visibility.showGrimmory, true);
  assert.equal(visibility.showHardcover, true);
});

test('shows audiobook and shared connections for audiobook and mixed workflows', () => {
  const audioOnly = getQuickstartConnectionVisibility({
    selectedRootScope: 'audiobook'
  });
  const both = getQuickstartConnectionVisibility({
    selectedRootScope: 'both'
  });

  assert.equal(audioOnly.showAudioBookShelf, true);
  assert.equal(audioOnly.showGrimmory, true);
  assert.equal(both.hasEbookWorkflow, true);
  assert.equal(both.showHardcover, true);
});

test('keeps an existing connection visible even when no current root uses its format', () => {
  const visibility = getQuickstartConnectionVisibility({
    audioBookShelfConfigured: true
  });

  assert.equal(visibility.showAudioBookShelf, true);
});
