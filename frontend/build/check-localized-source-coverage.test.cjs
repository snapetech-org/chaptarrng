const assert = require('node:assert/strict');
const { test } = require('node:test');
const { fieldDefinitionText } = require('./check-localized-source-coverage.cjs');

test('field coverage includes multiline labels and punctuation inside quoted help', () => {
  assert.deepEqual(fieldDefinitionText('[FieldDefinition(0,\n Label = "provider.field.label", HelpText = "provider.field.help",\n HelpTextWarning = "warning.with.parentheses")]'), [
    { kind: 'Label', id: 'provider.field.label' },
    { kind: 'HelpText', id: 'provider.field.help' },
    { kind: 'HelpTextWarning', id: 'warning.with.parentheses' }
  ]);
  assert.deepEqual(fieldDefinitionText('[FieldDefinition(0, HelpText = "Literal (text) \\\"example\\\"")]'), [
    { kind: 'HelpText', id: 'Literal (text) "example"' }
  ]);
});
test('new literal help text is visible to the catalog-ID audit', () => {
  assert.deepEqual(fieldDefinitionText('[FieldDefinition(0, Label = "AlreadyKnown", HelpText = "New untranslated text")]')[1], {
    kind: 'HelpText', id: 'New untranslated text'
  });
});
test('unclosed attributes fail instead of silently reducing the coverage count', () => {
  assert.throws(() => fieldDefinitionText('[FieldDefinition(0, Label = "unterminated"'), /Unterminated/);
});
