function getQuickstartReadiness({
  indexersState,
  downloadClientsState,
  rootFoldersState,
  interactions = {}
}) {
  const isLoaded = [indexersState, downloadClientsState, rootFoldersState].every(
    (state) => state?.isPopulated === true
  );

  if (!isLoaded) {
    return {
      isLoaded: false,
      showGuidance: false,
      guidedInitialSectionKey: 'rootFolders',
      guidedCompletedSectionKeys: []
    };
  }

  const hasEnabledIndexers = (indexersState.items || []).some((indexer) => indexer.enable);
  const hasEnabledDownloadClients = (downloadClientsState.items || []).some((client) => client.enable);
  const hasRootFolders = (rootFoldersState.items || []).length > 0;
  const needsIndexerReview = !hasEnabledIndexers && !interactions.indexers;
  const needsDownloadClientReview = !hasEnabledDownloadClients && !interactions.downloadClients;
  const guidedCompletedSectionKeys = [
    hasRootFolders && 'rootFolders',
    (hasEnabledIndexers || interactions.indexers) && 'indexers',
    (hasEnabledDownloadClients || interactions.downloadClients) && 'downloadClients'
  ].filter(Boolean);

  return {
    isLoaded: true,
    showGuidance: !hasRootFolders || needsIndexerReview || needsDownloadClientReview,
    guidedInitialSectionKey: !hasRootFolders ?
      'rootFolders' : needsIndexerReview ?
        'indexers' : needsDownloadClientReview ?
          'downloadClients' : 'rootFolders',
    guidedCompletedSectionKeys
  };
}

function getQuickstartConnectionVisibility({
  configuredRootFormats = [],
  selectedRootScope,
  audioBookShelfConfigured = false,
  grimmoryConfigured = false,
  hardcoverConfigured = false
}) {
  const hasAudiobookWorkflow = configuredRootFormats.includes('audiobook') ||
    selectedRootScope === 'audiobook' || selectedRootScope === 'both';
  const hasEbookWorkflow = configuredRootFormats.includes('ebook') ||
    selectedRootScope === 'ebook' || selectedRootScope === 'both';

  return {
    hasAudiobookWorkflow,
    hasEbookWorkflow,
    showAudioBookShelf: hasAudiobookWorkflow || audioBookShelfConfigured,
    showGrimmory: hasAudiobookWorkflow || hasEbookWorkflow || grimmoryConfigured,
    showHardcover: hasAudiobookWorkflow || hasEbookWorkflow || hardcoverConfigured
  };
}

module.exports = { getQuickstartConnectionVisibility, getQuickstartReadiness };
