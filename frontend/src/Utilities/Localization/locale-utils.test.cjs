const assert = require('node:assert/strict');
const test = require('node:test');
const {
  applyDocumentLocale,
  getCurrentLocale,
  getDirectionForLocale,
  normalizeLocaleTag
} = require('./locale-utils.cjs');

test('normalizes locale tags and derives direction from language or script', () => {
  assert.equal(normalizeLocaleTag('pt_BR'), 'pt-BR');
  assert.equal(getDirectionForLocale('ar-EG'), 'rtl');
  assert.equal(getDirectionForLocale('fa'), 'rtl');
  assert.equal(getDirectionForLocale('he-IL'), 'rtl');
  assert.equal(getDirectionForLocale('az-Arab'), 'rtl');
  assert.equal(getDirectionForLocale('az-Latn'), 'ltr');
  assert.equal(getDirectionForLocale('zh-Hant'), 'ltr');
  assert.equal(getDirectionForLocale('en-US'), 'ltr');
});

test('applies lang and direction to the document root', () => {
  const root = { lang: '', dir: '' };
  assert.deepEqual(applyDocumentLocale('fa_IR', root), { tag: 'fa-IR', direction: 'rtl' });
  assert.equal(root.lang, 'fa-IR');
  assert.equal(root.dir, 'rtl');
  assert.equal(getCurrentLocale(root), 'fa-IR');
});
