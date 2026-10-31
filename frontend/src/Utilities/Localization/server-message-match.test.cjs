const assert = require('node:assert/strict');
const test = require('node:test');
const records = require('./server-messages.json');
const { compileServerMessageMatchers, matchServerMessage } = require('./server-message-match.cjs');

function record(id, source, parameters = []) {
  return {
    id,
    source,
    parameters: parameters.map((name) => ({ name })),
    patterns: [source]
  };
}

test('every registry pattern preserves every dynamic value exactly', () => {
  const matchers = compileServerMessageMatchers(records);

  for (const entry of records) {
    const tokens = Object.fromEntries(entry.parameters.map(({ name }) => [name, `DYNAMIC_${name}_العربية` ]));

    for (const pattern of entry.patterns) {
      const message = pattern.replace(/\{value\d+\}/g, (token) => tokens[token.slice(1, -1)]);
      const actual = matchServerMessage(message, matchers);

      assert.equal(actual?.id, entry.id, `${entry.id}: ${pattern}`);
      assert.deepEqual(actual.tokens, tokens, entry.id);
    }
  }
});

test('fixed source matches are anchored and unknown messages remain unknown', () => {
  const matchers = compileServerMessageMatchers([record('fixed', 'A category is recommended')]);

  assert.equal(matchServerMessage('A category is recommended', matchers).id, 'fixed');
  assert.equal(matchServerMessage('prefix A category is recommended', matchers), null);
  assert.equal(matchServerMessage('A category is recommended suffix', matchers), null);
  assert.equal(matchServerMessage('A user wrote this message', matchers), null);
});

test('captured external data is not recursively interpreted or normalized', () => {
  const matchers = compileServerMessageMatchers([
    record('file', "File '{value0}' does not exist", ['value0']),
    record('other', 'A category is recommended')
  ]);
  const value = 'A category is recommended / الكتب / file{0}.epub';
  const actual = matchServerMessage(`File '${value}' does not exist`, matchers);

  assert.equal(actual.id, 'file');
  assert.equal(actual.tokens.value0, value);
});

test('literal protocol examples with braces remain literal', () => {
  const source = 'Only Hardcover series IDs are currently supported (expected hc:{id})';
  const matchers = compileServerMessageMatchers([record('literal', source)]);

  assert.equal(matchServerMessage(source, matchers).id, 'literal');
  assert.equal(matchServerMessage(source.replace('{id}', '123'), matchers), null);
});

test('the most specific fixed wording wins over a broader template', () => {
  const matchers = compileServerMessageMatchers([
    record('broad', 'Unable to connect: {value0}', ['value0']),
    record('specific', 'Unable to connect: import list {value0}', ['value0'])
  ]);

  assert.equal(matchServerMessage('Unable to connect: import list external text', matchers).id, 'specific');
});

test('equally specific ambiguous messages are left unchanged', () => {
  const matchers = compileServerMessageMatchers([
    record('left', 'A{value0}B', ['value0']),
    record('right', 'A{value0}B', ['value0'])
  ]);

  assert.equal(matchServerMessage('AexternalB', matchers), null);
});

test('ambiguous interior delimiters are rejected without changing any data', () => {
  const matchers = compileServerMessageMatchers([
    record('two', "From '{value0}' to '{value1}'", ['value0', 'value1'])
  ]);

  assert.equal(matchServerMessage("From 'a' to 'b' to 'c'", matchers), null);
  assert.deepEqual(matchServerMessage("From 'a' to 'b'", matchers).tokens, { value0: 'a', value1: 'b' });
});

test('empty optional details and multiline external data are preserved', () => {
  const matchers = compileServerMessageMatchers([
    record('details', 'Test failed: {value0}', ['value0'])
  ]);

  assert.equal(matchServerMessage('Test failed: ', matchers).tokens.value0, '');
  assert.equal(matchServerMessage('Test failed: first\nsecond', matchers).tokens.value0, 'first\nsecond');
});

test('non-string and oversized inputs never enter matching', () => {
  const matchers = compileServerMessageMatchers([record('details', 'Test failed: {value0}', ['value0'])]);

  for (const value of [undefined, null, 1, {}, [], '']) {
    assert.equal(matchServerMessage(value, matchers), null);
  }

  assert.equal(matchServerMessage(`Test failed: ${'a'.repeat(8192)}`, matchers), null);
});

test('wholly dynamic patterns and adjacent captures are refused', () => {
  assert.throws(() => compileServerMessageMatchers([record('dynamic', '{value0}: {value1}', ['value0', 'value1'])]), /fixed application wording/);
  assert.throws(() => compileServerMessageMatchers([record('adjacent', 'Message {value0}{value1}', ['value0', 'value1'])]), /Adjacent/);
});

test('missing, duplicate, or unsafe parameter declarations are refused', () => {
  assert.throws(() => compileServerMessageMatchers([record('missing', 'Message {value0}', ['value0', 'value1'])]), /every parameter once/);
  assert.throws(() => compileServerMessageMatchers([record('repeated', 'Message {value0} {value0}', ['value0'])]), /every parameter once/);
  assert.throws(() => compileServerMessageMatchers([record('unsafe', 'Message {value0}', ['value0|.*'])]), /Invalid server-message parameters/);
});

test('duplicate IDs and conflicting exact patterns are refused', () => {
  assert.throws(() => compileServerMessageMatchers([record('same', 'First'), record('same', 'Second')]), /duplicate/);
  assert.throws(() => compileServerMessageMatchers([record('first', 'Same'), record('second', 'Same')]), /Conflicting/);
});

test('undeclared valueN examples stay literal and never become captures', () => {
  const matchers = compileServerMessageMatchers([{
    id: 'example.literalValue', source: 'Use {value0} literally', parameters: [], patterns: ['Use {value0} literally']
  }]);
  assert.deepEqual(matchServerMessage('Use {value0} literally', matchers).tokens, {});
  assert.equal(matchServerMessage('Use private-provider-data literally', matchers), null);
});
