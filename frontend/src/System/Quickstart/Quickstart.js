import PropTypes from 'prop-types';
import { HTML5toTouch } from 'rdndmb-html5-to-touch';
import React from 'react';
import { DndProvider } from 'react-dnd-multi-backend';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import { align, icons } from 'Helpers/Props';
import {
  getRootFolderMediaTypes,
  getRootFolderMediaTypeScope } from 'Helpers/Props/folderTypes';
import translate from 'Utilities/String/translate';
import QuickstartAudioBookShelfSection from './QuickstartAudioBookShelfSection';
import QuickstartCustomFormatsSection from './QuickstartCustomFormatsSection';
import QuickstartDownloadClientsSection from './QuickstartDownloadClientsSection';
import QuickstartGrimmorySection from './QuickstartGrimmorySection';
import QuickstartHardcoverSection from './QuickstartHardcoverSection';
import QuickstartMAMSection from './QuickstartMAMSection';
import QuickstartMatchingSection from './QuickstartMatchingSection';
import QuickstartMetadataProfilesSection from './QuickstartMetadataProfilesSection';
import QuickstartProxySection from './QuickstartProxySection';
import QuickstartQualityProfilesSection from './QuickstartQualityProfilesSection';
import quickstartReadiness from './quickstartReadiness.cjs';
import QuickstartRootFoldersSection from './QuickstartRootFoldersSection';
import QuickstartSection, { QuickstartSectionProvider, useQuickstartSections } from './QuickstartSection';
import QuickstartSettingsBackupSection from './QuickstartSettingsBackupSection';
import styles from './Quickstart.css';
import summaryStyles from './QuickstartSetupSummary.css';

const {
  getQuickstartConnectionVisibility,
  getQuickstartReadiness
} = quickstartReadiness;

const QUICKSTART_SECTION_KEYS = [
  'rootFolders',
  'indexers',
  'downloadClients',
  'customFormats',
  'qualityProfiles',
  'metadataProfiles',
  'matching',
  'optionalConnections',
  'settingsBackup'
];

const GUIDED_CORE_SECTION_KEYS = [
  'rootFolders',
  'indexers',
  'downloadClients'
];
const GUIDED_REVIEW_SECTION_KEYS = ['indexers', 'downloadClients'];

function getRootFolderScopeTranslationKey(scope) {
  if (scope === 'ebook') {
    return 'QuickstartRootFolderScopeEbook';
  }

  if (scope === 'audiobook') {
    return 'QuickstartRootFolderScopeAudio';
  }

  if (scope === 'both') {
    return 'QuickstartRootFolderScopeBoth';
  }

  return 'QuickstartSetupFormatUnknown';
}

function getSetupItemName(item) {
  return item.name || item.implementationName || item.implementation;
}

function getSetupListSummary(isLoaded, items, emptyTranslationKey) {
  if (!isLoaded) {
    return translate('QuickstartSetupSummaryLoading');
  }

  if (items.length) {
    return items.join(', ');
  }

  return translate(emptyTranslationKey);
}

function QuickstartSectionToolbar(props) {
  const {
    showGuidance
  } = props;

  const {
    allExpanded,
    allCollapsed,
    setAllExpanded
  } = useQuickstartSections();

  let expandIcon = icons.EXPAND_INDETERMINATE;

  if (allExpanded) {
    expandIcon = icons.COLLAPSE;
  } else if (allCollapsed) {
    expandIcon = icons.EXPAND;
  }

  return (
    <PageToolbar>
      {
        showGuidance &&
          <PageToolbarSection
            alignContent={align.LEFT}
            collapseButtons={false}
          >
            <div className={styles.quickstartToolbarMessage}>
              {translate('QuickstartWorkTopToBottomHint')}
            </div>
          </PageToolbarSection>
      }

      <PageToolbarSection alignContent={align.RIGHT}>
        <PageToolbarButton
          label={allExpanded ? translate('AllExpandedCollapseAll') : translate('AllExpandedExpandAll')}
          iconName={expandIcon}
          onPress={() => setAllExpanded(!allExpanded)}
        />
      </PageToolbarSection>
    </PageToolbar>
  );
}

QuickstartSectionToolbar.propTypes = {
  showGuidance: PropTypes.bool.isRequired
};

function Quickstart(props) {
  const {
    hasActiveAudioBookShelf,
    audioBookShelfNotification,
    hasActiveGrimmory,
    grimmoryNotification,
    mamIndexer,
    indexersState,
    notificationsState,
    downloadClientsState,
    rootFoldersState,
    quickstartState,
    fetchIndexerSchema,
    selectIndexerSchema,
    fetchNotificationSchema,
    selectNotificationSchema,
    fetchDownloadClientSchema,
    selectDownloadClientSchema,
    markSectionInteracted,
    fetchNotifications,
    isHardcoverConfigured,
    hardcoverUsername,
    hardcoverAvatarUrl,
    proxies
  } = props;

  const interactions = quickstartState?.interactions || {};
  const [rootFolderScope, setRootFolderScope] = React.useState(null);
  const [connectionTestFailures, setConnectionTestFailures] = React.useState({});
  const configuredRootFormats =
    (rootFoldersState?.items || []).flatMap(getRootFolderMediaTypes);
  const {
    showAudioBookShelf,
    showGrimmory,
    showHardcover
  } = getQuickstartConnectionVisibility({
    configuredRootFormats,
    selectedRootScope: rootFolderScope,
    audioBookShelfConfigured: !!audioBookShelfNotification,
    grimmoryConfigured: !!grimmoryNotification,
    hardcoverConfigured: isHardcoverConfigured === true
  });
  const connectionTestFailed =
    (showAudioBookShelf && connectionTestFailures.audioBookShelf) ||
    (showGrimmory && connectionTestFailures.grimmory) ||
    (showHardcover && connectionTestFailures.hardcover);
  const optionalConnectionReviewItems = [];

  if (showAudioBookShelf) {
    optionalConnectionReviewItems.push(audioBookShelfNotification);
  }
  if (showGrimmory) {
    optionalConnectionReviewItems.push(grimmoryNotification);
  }
  if (showHardcover) {
    optionalConnectionReviewItems.push(isHardcoverConfigured === true);
  }
  const optionalConnectionsComplete = !!interactions.optionalConnections ||
    (optionalConnectionReviewItems.length > 0 &&
      optionalConnectionReviewItems.every(Boolean) &&
      !connectionTestFailed);

  const recordConnectionTestResult = (name, hasFailed) => {
    setConnectionTestFailures((current) => ({
      ...current,
      [name]: hasFailed
    }));
  };

  const getConnectionStatus = (name, configured, wasTested) => {
    if (connectionTestFailures[name]) {
      return translate('QuickstartConnectionFailed');
    }
    if (wasTested) {
      return translate('QuickstartConnectionTested');
    }
    if (configured) {
      return translate('QuickstartConnectionConfigured');
    }
    if (interactions.optionalConnections) {
      return translate('QuickstartConnectionSkipped');
    }
    return translate('QuickstartConnectionNotConfigured');
  };
  const enabledIndexers = (indexersState.items || []).filter((indexer) => indexer.enable);
  const enabledDownloadClients = (downloadClientsState.items || []).filter((client) => client.enable);
  const setupRoots = rootFoldersState.items || [];
  const configuredFormats = new Set(setupRoots.flatMap(getRootFolderMediaTypes));
  const formatLabels = [];

  if (configuredFormats.has('ebook')) {
    formatLabels.push(translate('QuickstartRootFolderScopeEbook'));
  }
  if (configuredFormats.has('audiobook')) {
    formatLabels.push(translate('QuickstartRootFolderScopeAudio'));
  }
  const formatSummary = getSetupListSummary(
    rootFoldersState.isPopulated,
    formatLabels,
    'QuickstartSetupNoFormats'
  );

  const optionalConnectionStatuses = [
    showAudioBookShelf && {
      name: 'AudioBookShelf',
      status: getConnectionStatus('audioBookShelf', !!audioBookShelfNotification, !!interactions.audioBookShelf)
    },
    showGrimmory && {
      name: 'Grimmory',
      status: getConnectionStatus('grimmory', !!grimmoryNotification, !!interactions.grimmory)
    },
    showHardcover && {
      name: 'Hardcover',
      status: getConnectionStatus('hardcover', isHardcoverConfigured === true, !!interactions.hardcover)
    }
  ].filter(Boolean);
  const {
    showGuidance,
    guidedInitialSectionKey,
    guidedCompletedSectionKeys
  } = getQuickstartReadiness({
    indexersState,
    downloadClientsState,
    rootFoldersState,
    interactions
  });

  return (
    <PageContent title={translate('Quickstart')}>
      <QuickstartSectionProvider
        sectionKeys={QUICKSTART_SECTION_KEYS}
        guidedSectionKeys={GUIDED_CORE_SECTION_KEYS}
        guidedInitialSectionKey={guidedInitialSectionKey}
        guidedReviewSectionKeys={GUIDED_REVIEW_SECTION_KEYS}
        guidedCompletedSectionKeys={guidedCompletedSectionKeys}
        isGuidedMode={showGuidance}
        onGuidedSectionReviewed={(sectionKey) => markSectionInteracted({ section: sectionKey })}
      >
        <QuickstartSectionToolbar showGuidance={showGuidance} />

        <PageContentBody>
          <DndProvider options={HTML5toTouch}>
            <QuickstartRootFoldersSection
              rootFolderScope={rootFolderScope}
              onRootFolderScopeChange={setRootFolderScope}
              markSectionInteracted={markSectionInteracted}
              quickstartState={quickstartState}
            />

            <QuickstartMAMSection
              mamIndexer={mamIndexer}
              indexersState={indexersState}
              fetchIndexerSchema={fetchIndexerSchema}
              selectIndexerSchema={selectIndexerSchema}
              markSectionInteracted={markSectionInteracted}
              proxies={proxies}
            />

            <QuickstartDownloadClientsSection
              downloadClientsState={downloadClientsState}
              fetchDownloadClientSchema={fetchDownloadClientSchema}
              selectDownloadClientSchema={selectDownloadClientSchema}
              markSectionInteracted={markSectionInteracted}
            />

            <QuickstartCustomFormatsSection
              markSectionInteracted={markSectionInteracted}
              quickstartState={quickstartState}
            />

            <QuickstartSection
              sectionKey="qualityProfiles"
              title={translate('QuickstartQualityProfilesTitle')}
              isComplete={!!interactions.qualityProfiles}
            >
              <div className={styles.sectionDescription}>
                {translate('QuickstartQualityProfilesDescription')}
              </div>

              <QuickstartQualityProfilesSection markSectionInteracted={markSectionInteracted} />
            </QuickstartSection>

            <QuickstartSection
              sectionKey="metadataProfiles"
              title={translate('QuickstartMetadataProfilesTitle')}
              isComplete={!!interactions.metadataProfiles}
            >
              <div className={styles.sectionDescription}>
                {translate('QuickstartMetadataProfilesDescription')}
              </div>

              <QuickstartMetadataProfilesSection markSectionInteracted={markSectionInteracted} />
            </QuickstartSection>

            <QuickstartMatchingSection
              markSectionInteracted={markSectionInteracted}
              quickstartState={quickstartState}
            />

            <QuickstartSection
              sectionKey="optionalConnections"
              title={translate('QuickstartOptionalConnectionsTitle')}
              isComplete={optionalConnectionsComplete}
            >
              <div className={styles.sectionDescription}>
                {translate('QuickstartOptionalConnectionsDescription')}
              </div>
              <div className={styles.optionalConnectionsRow}>
                {showAudioBookShelf && (
                  <div className={styles.optionalConnectionItem}>
                    <QuickstartAudioBookShelfSection
                      hasActiveAudioBookShelf={hasActiveAudioBookShelf}
                      audioBookShelfNotification={audioBookShelfNotification}
                      notificationsState={notificationsState}
                      fetchNotificationSchema={fetchNotificationSchema}
                      selectNotificationSchema={selectNotificationSchema}
                      markSectionInteracted={markSectionInteracted}
                      fetchNotifications={fetchNotifications}
                      wasSkipped={!!interactions.optionalConnections}
                      onConnectionTestSuccess={() => recordConnectionTestResult('audioBookShelf', false)}
                      onConnectionTestFailure={() => recordConnectionTestResult('audioBookShelf', true)}
                      compact={true}
                    />
                  </div>
                )}

                {showGrimmory && (
                  <div className={styles.optionalConnectionItem}>
                    <QuickstartGrimmorySection
                      hasActiveGrimmory={hasActiveGrimmory}
                      grimmoryNotification={grimmoryNotification}
                      notificationsState={notificationsState}
                      fetchNotificationSchema={fetchNotificationSchema}
                      selectNotificationSchema={selectNotificationSchema}
                      markSectionInteracted={markSectionInteracted}
                      fetchNotifications={fetchNotifications}
                      wasSkipped={!!interactions.optionalConnections}
                      onConnectionTestSuccess={() => recordConnectionTestResult('grimmory', false)}
                      onConnectionTestFailure={() => recordConnectionTestResult('grimmory', true)}
                      compact={true}
                    />
                  </div>
                )}

                {showHardcover && (
                  <div className={styles.optionalConnectionItem}>
                    <QuickstartHardcoverSection
                      compact={true}
                      isHardcoverConfigured={isHardcoverConfigured}
                      hardcoverUsername={hardcoverUsername}
                      hardcoverAvatarUrl={hardcoverAvatarUrl}
                      markSectionInteracted={markSectionInteracted}
                      wasSkipped={!!interactions.optionalConnections}
                      onConnectionTestSuccess={() => recordConnectionTestResult('hardcover', false)}
                      onConnectionTestFailure={() => recordConnectionTestResult('hardcover', true)}
                    />
                  </div>
                )}

                <div className={styles.optionalConnectionItem}>
                  <QuickstartProxySection proxies={proxies} />
                </div>
              </div>

              <button
                type="button"
                className={styles.quickstartSkipOptionalButton}
                onClick={() => markSectionInteracted({ section: 'optionalConnections' })}
              >
                {translate('QuickstartSkipOptionalConnections')}
              </button>
            </QuickstartSection>

            <QuickstartSettingsBackupSection />

            <section
              className={summaryStyles.quickstartSetupSummary}
              aria-labelledby="quickstart-setup-summary-title"
            >
              <h2
                id="quickstart-setup-summary-title"
                className={summaryStyles.quickstartSetupSummaryTitle}
              >
                {translate('QuickstartSetupSummaryTitle')}
              </h2>

              <div className={summaryStyles.quickstartSetupSummaryGrid}>
                <div className={summaryStyles.quickstartSetupSummaryGroup}>
                  <h3>{translate('QuickstartSetupFormats')}</h3>
                  <p>{formatSummary}</p>
                  {rootFolderScope && (
                    <p className={summaryStyles.quickstartSetupSummaryHelp}>
                      {translate('QuickstartSetupDraftScope', {
                        scope: translate(getRootFolderScopeTranslationKey(rootFolderScope))
                      })}
                    </p>
                  )}
                </div>

                <div className={summaryStyles.quickstartSetupSummaryGroup}>
                  <h3>{translate('QuickstartSetupRoots')}</h3>
                  {!rootFoldersState.isPopulated && (
                    <p>{translate('QuickstartSetupSummaryLoading')}</p>
                  )}
                  {rootFoldersState.isPopulated && !setupRoots.length && (
                    <p>{translate('QuickstartSetupNoRoots')}</p>
                  )}
                  {rootFoldersState.isPopulated && setupRoots.length > 0 && (
                    <ul className={summaryStyles.quickstartSetupSummaryList}>
                      {setupRoots.map((root) => {
                        const scope = getRootFolderMediaTypeScope(root);
                        const scopeLabel = translate(getRootFolderScopeTranslationKey(scope));

                        return (
                          <li key={root.id}>
                            <span>{root.name || root.path}</span>
                            {root.name && root.path && <span>{' — '}{root.path}</span>}
                            <span className={summaryStyles.quickstartSetupSummaryHelp}> ({scopeLabel})</span>
                          </li>
                        );
                      })}
                    </ul>
                  )}
                </div>

                <div className={summaryStyles.quickstartSetupSummaryGroup}>
                  <h3>{translate('QuickstartSetupIndexers')}</h3>
                  {!indexersState.isPopulated && (
                    <p>{translate('QuickstartSetupSummaryLoading')}</p>
                  )}
                  {indexersState.isPopulated && !enabledIndexers.length && (
                    <p>{translate('QuickstartSetupNoEnabledIndexers')}</p>
                  )}
                  {indexersState.isPopulated && enabledIndexers.length > 0 && (
                    <ul className={summaryStyles.quickstartSetupSummaryList}>
                      {enabledIndexers.map((indexer) => (
                        <li key={indexer.id}>
                          {getSetupItemName(indexer)}
                        </li>
                      ))}
                    </ul>
                  )}
                </div>

                <div className={summaryStyles.quickstartSetupSummaryGroup}>
                  <h3>{translate('QuickstartSetupDownloadClients')}</h3>
                  {!downloadClientsState.isPopulated && (
                    <p>{translate('QuickstartSetupSummaryLoading')}</p>
                  )}
                  {downloadClientsState.isPopulated && !enabledDownloadClients.length && (
                    <p>{translate('QuickstartSetupNoEnabledDownloadClients')}</p>
                  )}
                  {downloadClientsState.isPopulated && enabledDownloadClients.length > 0 && (
                    <ul className={summaryStyles.quickstartSetupSummaryList}>
                      {enabledDownloadClients.map((client) => (
                        <li key={client.id}>
                          {getSetupItemName(client)}
                        </li>
                      ))}
                    </ul>
                  )}
                </div>

                <div className={summaryStyles.quickstartSetupSummaryGroup}>
                  <h3>{translate('QuickstartSetupOptionalConnections')}</h3>
                  {optionalConnectionStatuses.length ? (
                    <ul className={summaryStyles.quickstartSetupSummaryList}>
                      {optionalConnectionStatuses.map((connection) => (
                        <li key={connection.name}>
                          <span>{connection.name}</span>
                          <span className={summaryStyles.quickstartSetupSummaryStatus}>{' — '}{connection.status}</span>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <p>{translate('QuickstartSetupNoOptionalConnections')}</p>
                  )}
                </div>
              </div>
            </section>
          </DndProvider>
        </PageContentBody>
      </QuickstartSectionProvider>
    </PageContent>
  );
}

Quickstart.propTypes = {
  hasActiveAudioBookShelf: PropTypes.bool,
  audioBookShelfNotification: PropTypes.object,
  hasActiveGrimmory: PropTypes.bool,
  grimmoryNotification: PropTypes.object,
  mamIndexer: PropTypes.object,
  indexersState: PropTypes.object.isRequired,
  notificationsState: PropTypes.object.isRequired,
  downloadClientsState: PropTypes.object.isRequired,
  rootFoldersState: PropTypes.object.isRequired,
  quickstartState: PropTypes.object,
  proxies: PropTypes.arrayOf(PropTypes.object),
  isHardcoverConfigured: PropTypes.bool,
  hardcoverUsername: PropTypes.string,
  hardcoverAvatarUrl: PropTypes.string,
  fetchIndexerSchema: PropTypes.func.isRequired,
  selectIndexerSchema: PropTypes.func.isRequired,
  fetchNotificationSchema: PropTypes.func.isRequired,
  selectNotificationSchema: PropTypes.func.isRequired,
  fetchDownloadClientSchema: PropTypes.func.isRequired,
  selectDownloadClientSchema: PropTypes.func.isRequired,
  markSectionInteracted: PropTypes.func.isRequired,
  fetchNotifications: PropTypes.func.isRequired
};

export default Quickstart;
