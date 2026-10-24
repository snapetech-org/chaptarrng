import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');

function read(relativePath) {
  return normalizeLineEndings(fs.readFileSync(path.join(repoRoot, relativePath), 'utf8'));
}

function normalizeLineEndings(contents) {
  return contents.replace(/\r\n/g, '\n');
}

function readWorkflowJob(workflow, name) {
  workflow = normalizeLineEndings(workflow);
  const marker = `\n  ${name}:\n`;
  const markerIndex = workflow.indexOf(marker);
  assert.notEqual(markerIndex, -1, `workflow job ${name} exists`);
  const start = markerIndex + 1;
  const nextJob = /\n  [a-z0-9-]+:\n/g;
  nextJob.lastIndex = start + marker.length - 1;
  const match = nextJob.exec(workflow);
  return workflow.slice(start, match ? match.index : workflow.length);
}

test('release workflow job extraction accepts Windows CRLF checkouts', () => {
  const workflow = read('.github/workflows/release.yml').replace(/\n/g, '\r\n');
  assert.match(readWorkflowJob(workflow, 'legacy-desktop-candidates'), /needs: \[release, standalone-assets\]/);
});

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

test('release builds and verifies desktop migration candidates only behind all readiness gates', () => {
  const workflow = read('.github/workflows/release.yml');
  const builder = read('packaging/scripts/build-bookshelf-desktop-candidates.sh');
  const desktopJob = readWorkflowJob(workflow, 'legacy-desktop-candidates');
  const publishJob = readWorkflowJob(workflow, 'publish-release');

  assert.match(desktopJob, /needs: \[release, standalone-assets\]/);
  assert.match(desktopJob, /EXPECTED_RELEASE_SHA: \$\{\{ needs\.release\.outputs\.sha \}\}/);
  assert.match(desktopJob, /name: chaptarrng-standalone-linux-x64/);
  assert.match(desktopJob, /sha256sum --check \"\$archive\.sha256\"/);
  assert.match(desktopJob, /build-bookshelf-desktop-candidates\.sh/);
  assert.match(desktopJob, /appimagetool\/releases\/download\/1\.9\.1\/appimagetool-x86_64\.AppImage/);
  assert.match(desktopJob, /APPIMAGETOOL_SHA256: ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0/);
  assert.match(desktopJob, /APPIMAGE_RUNTIME_SHA256: 2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d/);
  assert.match(desktopJob, /type2-runtime\/releases\/download\/20251108\/runtime-x86_64/);
  assert.match(desktopJob, /APPIMAGETOOL_RUNTIME_FILE=\$runtime/);
  assert.match(builder, /appimagetool_runtime_args\+=\(--runtime-file "\$APPIMAGETOOL_RUNTIME_FILE"\)/);
  const guardedSteps = desktopJob.match(/^      if: .*vars\.CHAPTARRNG_MIGRATION_BRIDGE_ENABLED == 'true'.*vars\.CHAPTARRNG_RELEASE_READY == 'true'.*vars\.BOOKSHELFNG_RELEASE_READY == 'true'.*$/gm) ?? [];
  assert.equal(guardedSteps.length, 5, 'download, build, and upload steps all require every release gate');
  assert.match(desktopJob, /name: chaptarrng-bookshelfng-desktop-candidates/);
  assert.match(publishJob, /needs: \[release, docker, standalone-assets, rpm-source, windows-migration-installer, legacy-desktop-candidates\]/);
  assert.match(publishJob, /name: chaptarrng-bookshelfng-desktop-candidates/);
  assert.match(publishJob, /ChaptarrNG-\$\{VERSION\}-bookshelfng-x86_64\.AppImage/);
  assert.match(publishJob, /ChaptarrNG-\$\{VERSION\}-bookshelfng\.flatpak/);
});

test('release pins Cloudron and Compose candidates to the built compatibility image digest', () => {
  const workflow = read('.github/workflows/release.yml');
  const builder = read('packaging/scripts/build-bookshelf-container-platform-candidates.sh');
  const publishJob = readWorkflowJob(workflow, 'publish-release');

  assert.ok(builder.includes('^sha256:[a-f0-9]{64}$'));
  assert.ok(builder.includes('tech.snapetech.bookshelfng'));
  assert.match(builder, /CHAPTARRNG_COMPAT_IMAGE=/);
  assert.ok(builder.includes('bookshelfng-container-platform-candidates.tar.gz'));
  assert.ok(publishJob.includes('BOOKSHELF_HARDCOVER_DIGEST: ${{ needs.docker.outputs.bookshelf_hardcover_digest }}'));
  assert.ok(publishJob.includes('build-bookshelf-container-platform-candidates.sh'));
  assert.ok(publishJob.includes('bookshelfng-container-platform-candidates.tar.gz'));
  assert.ok(publishJob.includes('tar -tzf "$RUNNER_TEMP/release-assets/$container_candidates"'));
});

test('StartOS candidate documents the package identity and external device-acceptance gate', () => {
  const readme = read('packaging/legacy/startos/README.md');
  const manifest = read('packaging/legacy/startos/candidate/startos/manifest/index.ts');

  assert.match(readme, /`candidate\/` contains the StartOS wrapper source/);
  assert.match(readme, /package ID `bookshelfng`/);
  assert.match(readme, /volume IDs `main`,\s*`books`, and `downloads`/);
  assert.match(readme, /`\/config`, `\/books`, and `\/downloads`/);
  assert.match(readme, /exact ChaptarrNG\s+multi-architecture Hardcover digest/);
  assert.match(readme, /device install, backup, update, and rollback acceptance still\s+belong to that package's handoff/i);
  assert.match(manifest, /packageRepo: 'https:\/\/github\.com\/snapetech\/bookshelfng-startos'/);
  assert.match(manifest, /upstreamRepo: 'https:\/\/github\.com\/snapetech-org\/chaptarrng'/);
  assert.match(manifest, /source: \{ dockerTag: 'ghcr\.io\/snapetech-org\/chaptarrng@sha256:__BOOKSHELF_HARDCOVER_DIGEST__' \}/);
});

test('StartOS candidate uses the patched SDK 3 dependency contract and documents its OS floor', () => {
  const packageJson = JSON.parse(read('packaging/legacy/startos/candidate/package.json'));
  const packageLock = JSON.parse(read('packaging/legacy/startos/candidate/package-lock.json'));
  const readme = read('packaging/legacy/startos/candidate/README.md');
  const dependencies = read('packaging/legacy/startos/candidate/startos/dependencies.ts');
  const index = read('packaging/legacy/startos/candidate/startos/index.ts');
  const init = read('packaging/legacy/startos/candidate/startos/init/index.ts');
  const manifest = read('packaging/legacy/startos/candidate/startos/manifest/index.ts');

  assert.equal(packageJson.dependencies['@start9labs/start-sdk'], '3.1.0');
  assert.equal(packageLock.packages[''].dependencies['@start9labs/start-sdk'], '3.1.0');
  assert.equal(packageLock.packages['node_modules/@start9labs/start-sdk'].version, '3.1.0');
  assert.match(dependencies, /sdk\.Dependencies\.of\(\)/);
  assert.match(index, /buildManifest\(versionGraph,\s*sdkManifest,\s*dependencies\)/);
  assert.match(init, /import \{ dependencies \} from '\.\.\/dependencies'/);
  assert.match(init, /versionGraph,\s*setInterfaces,\s*dependencies,\s*actions/);
  assert.doesNotMatch(manifest, /dependencies:\s*\{\s*\}/);
  assert.match(readme, /StartOS 0\.4\.0\.2 or newer/);
});
