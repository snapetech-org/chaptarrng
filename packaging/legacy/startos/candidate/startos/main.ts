import { i18n } from './i18n'
import { sdk } from './sdk'
import { uiPort } from './utils'

export const main = sdk.setupMain(async ({ effects }) => {
  /**
   * ======================== Setup (optional) ========================
   *
   * In this section, we fetch any resources or run any desired preliminary commands.
   */
  console.info(i18n('Starting BookshelfNG!'))

  /**
   * ======================== Daemons ========================
   *
   * In this section, we create one or more daemons that define the service runtime.
   *
   * Each daemon defines its own health check, which can optionally be exposed to the user.
   */
  return sdk.Daemons.of(effects).addDaemon('primary', {
    subcontainer: sdk.SubContainer.of(
      effects,
      { imageId: 'bookshelfng' },
      sdk.Mounts.of().mountVolume({
        volumeId: 'main',
        subpath: null,
        mountpoint: '/config',
        readonly: false,
      })
        .mountVolume({ volumeId: 'books', subpath: null, mountpoint: '/books', readonly: false })
        .mountVolume({ volumeId: 'downloads', subpath: null, mountpoint: '/downloads', readonly: false }),
      'bookshelfng-sub',
    ),
    exec: { command: ['/init'] },
    ready: {
      display: i18n('Web Interface'),
      fn: () =>
        sdk.healthCheck.checkPortListening(effects, uiPort, {
          successMessage: i18n('The web interface is ready'),
          errorMessage: i18n('The web interface is not ready'),
        }),
    },
    requires: [],
  })
})
