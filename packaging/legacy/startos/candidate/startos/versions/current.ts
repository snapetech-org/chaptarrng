import { IMPOSSIBLE, VersionInfo } from '@start9labs/start-sdk'

export const current = VersionInfo.of({
  version: '0.4.21:2',
  releaseNotes: {
    en_US: 'Switch the BookshelfNG package runtime to ChaptarrNG.',
    es_ES: 'Cambiar el runtime del paquete BookshelfNG a ChaptarrNG.',
    de_DE: 'Laufzeit des BookshelfNG-Pakets auf ChaptarrNG umstellen.',
    pl_PL: 'Zmiana środowiska pakietu BookshelfNG na ChaptarrNG.',
    fr_FR: 'Passage du runtime du paquet BookshelfNG à ChaptarrNG.',
  },
  migrations: {
    up: async ({ effects }) => {},
    down: IMPOSSIBLE,
  },
})
