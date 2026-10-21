import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

function read(relativePath) {
  return fs.readFileSync(path.join(repoRoot, relativePath), 'utf8');
}

test('AppImage keeps its desktop identity, BookshelfNG data directory, and Chaptarr launcher', () => {
  const appRun = read('packaging/legacy/appimage/AppRun');
  const desktop = read('packaging/legacy/appimage/com.snapetech.bookshelfng.desktop');

  assert.match(desktop, /^Name=ChaptarrNG$/m);
  assert.match(desktop, /^Exec=AppRun$/m);
  assert.match(appRun, /XDG_DATA_HOME:-\$HOME\/\.local\/share\}\/bookshelfng/);
  assert.match(appRun, /exec "\$HERE\/usr\/bin\/Chaptarr" -nobrowser "-data=\$DATA_DIR"/);
  assert.match(appRun, /HARDCOVER="\$\{HARDCOVER:-false\}"/);
  assert.match(appRun, /CHAPTARR_PACKAGE_UPDATE_MECHANISM:-External/);
  assert.match(appRun, /Replace this AppImage with a newer compatible build/);
});

test('Flatpak keeps the installed app ID and BookshelfNG sandbox data directory', () => {
  const manifest = read('packaging/legacy/flatpak/com.snapetech.BookshelfNG.yml');
  const desktop = read('packaging/legacy/flatpak/com.snapetech.BookshelfNG.desktop');
  const metainfo = read('packaging/legacy/flatpak/com.snapetech.BookshelfNG.metainfo.xml');

  assert.match(manifest, /^app-id: com\.snapetech\.BookshelfNG$/m);
  assert.match(manifest, /--filesystem=xdg-config\/bookshelfng:create/);
  assert.match(manifest, /install -D -m 0644 com\.snapetech\.BookshelfNG\.desktop \/app\/share\/applications/);
  assert.match(manifest, /install -D -m 0644 com\.snapetech\.BookshelfNG\.metainfo\.xml \/app\/share\/metainfo/);
  assert.match(manifest, /install -D -m 0644 com\.snapetech\.BookshelfNG\.png \/app\/share\/icons/);
  assert.match(manifest, /XDG_CONFIG_HOME:-\$HOME\/\.config\}\/bookshelfng/);
  assert.match(manifest, /\/app\/lib\/bookshelfng\/Chaptarr -nobrowser/);
  assert.match(manifest, /HARDCOVER="\$\{HARDCOVER:-false\}"/);
  assert.match(manifest, /CHAPTARR_PACKAGE_UPDATE_MECHANISM:-External/);
  assert.match(desktop, /^Exec=bookshelfng-wrapper$/m);
  assert.match(desktop, /^Icon=com\.snapetech\.BookshelfNG$/m);
  assert.match(metainfo, /<launchable type="desktop-id">com\.snapetech\.BookshelfNG\.desktop<\/launchable>/);
});

test('Cloudron candidate preserves application ID, health endpoint, port, and mounts', () => {
  const manifest = JSON.parse(read('packaging/legacy/cloudron/CloudronManifest.json'));
  const dockerfile = read('packaging/legacy/cloudron/Dockerfile');

  assert.equal(manifest.id, 'tech.snapetech.bookshelfng');
  assert.equal(manifest.httpPort, 8787);
  assert.equal(manifest.healthCheckPath, '/ping');
  assert.deepEqual(manifest.persistentDirs, ['/config', '/books', '/downloads']);
  assert.match(dockerfile, /^FROM ghcr\.io\/snapetech-org\/chaptarrng:bookshelf-hardcover$/m);
  assert.doesNotMatch(dockerfile, /docker\.io\/snapetech\/bookshelfng/);
});

test('catalog Compose handoff keeps the BookshelfNG container and named volumes', () => {
  const compose = read('packaging/legacy/catalogs/compose.yaml');

  assert.match(compose, /container_name: bookshelfng/);
  assert.match(compose, /CHAPTARRNG_COMPAT_IMAGE:-ghcr\.io\/snapetech-org\/chaptarrng:bookshelf-hardcover/);
  assert.match(compose, /"127\.0\.0\.1:8787:8787\/tcp"/);
  assert.match(compose, /bookshelfng-config:\/config/);
  assert.match(compose, /bookshelfng-books:\/books/);
  assert.match(compose, /bookshelfng-downloads:\/downloads/);
});

test('Co-op Cloud handoff keeps stack volumes and uses the compatibility image', () => {
  const compose = read('packaging/legacy/coop-cloud/bookshelfng/compose.yml');
  const sample = read('packaging/legacy/coop-cloud/bookshelfng/.env.sample');

  assert.match(compose, /CHAPTARRNG_COMPAT_IMAGE:-ghcr\.io\/snapetech-org\/chaptarrng:bookshelf-hardcover/);
  assert.match(compose, /bookshelfng_config:\/config/);
  assert.match(compose, /bookshelfng_books:\/books/);
  assert.match(compose, /bookshelfng_downloads:\/downloads/);
  assert.match(compose, /loadbalancer\.server\.port=8787/);
  assert.match(sample, /^CHAPTARRNG_COMPAT_IMAGE=ghcr\.io\/snapetech-org\/chaptarrng:bookshelf-hardcover$/m);
});

test('StartOS candidate documents the package identity and external device-acceptance gate', () => {
  const readme = read('packaging/legacy/startos/README.md');

  assert.match(readme, /bookshelfng-startos/);
  assert.match(readme, /`bookshelfng` package ID/);
  assert.match(readme, /preserves the `\/config`,\s*`\/books`, and `\/downloads` mount contract/);
  assert.match(readme, /no StartOS package update,\s*device install,\s*backup\/restore,\s*or\s+rollback has been accepted/i);
});
