import serverMessageMatching from 'Utilities/Localization/server-message-match.cjs';
import serverMessages from 'Utilities/Localization/server-messages.json';
import { translateWithFallback } from 'Utilities/String/translate';

const { compileServerMessageMatchers, matchServerMessage } = serverMessageMatching;
const matchers = compileServerMessageMatchers(serverMessages);
const retainedFileMarker = ' Converted file retained at: ';
const toolOutputMarker = ' Tool output: ';
const readableM4bSuffix = ' A readable converted M4B was produced but will not be imported because m4b-tool reported a failure.';
const alternateDownloadClientMessageId = 'server.message.m7842e5a82f2c';
const alternateDownloadClientPhraseId = 'server.message.mae71772271ad';
const toolOutputMessageId = 'server.message.m3347b5962c6a';
const retainedFileMessageId = 'server.message.m45d5b4f145d0';
const readableM4bMessageId = 'server.message.m2dfd8b029cda';
const snippetBaseMessageIds = new Set([
  'server.message.m02884586a62c',
  'server.message.m4b666af14ddc',
  'server.message.m4d3e6de749b4',
  'server.message.m8af0d2379b97',
  'server.message.ma69b7ec09fb3',
  'server.message.mccfb6e3b050a',
]);

// Only call this at a server-message presentation boundary. Unknown messages
// and captured user, library, provider, path, URL, and protocol data stay intact.
function translateMatch(match) {
  // Isolate rendered values from the sentence direction. The matched API/history
  // string and each captured value stay intact; only this display result gains
  // first-strong isolate / pop directional isolate boundaries.
  const tokens = Object.fromEntries(Object.entries(match.tokens).map(([key, value]) => {
    const isIsolated = value.startsWith('\u2068') && value.endsWith('\u2069');

    return [key, !value || isIsolated ? value : `\u2068${value}\u2069`];
  }));

  return translateWithFallback(match.id, match.source, tokens);
}

function translateAlternateDownloadClients(match) {
  const remaining = match.tokens.value1;

  if (typeof remaining !== 'string' || !remaining) {
    return null;
  }

  const phrases = [];
  let suffix = remaining;

  while (suffix) {
    const phraseMatch = /^(Found \d+ enabled (?:direct|torrent|usenet) client\(s\)\.)/.exec(suffix);

    if (!phraseMatch) {
      return null;
    }

    const localizedPhrase = matchServerMessage(phraseMatch[1], matchers);

    if (!localizedPhrase || localizedPhrase.id !== alternateDownloadClientPhraseId) {
      return null;
    }

    phrases.push(translateMatch(localizedPhrase));
    suffix = suffix.slice(phraseMatch[1].length);

    if (suffix) {
      if (!suffix.startsWith(' ')) {
        return null;
      }

      suffix = suffix.slice(1);
    }
  }

  return translateMatch({
    ...match,
    tokens: { ...match.tokens, value1: phrases.join(' ') },
  });
}

function translateToolOutputMessage(message) {
  const markerIndex = message.indexOf(toolOutputMarker);

  if (markerIndex < 0) {
    return null;
  }

  const baseMatch = matchServerMessage(message.slice(0, markerIndex), matchers);

  if (!baseMatch || !snippetBaseMessageIds.has(baseMatch.id)) {
    return null;
  }

  // Everything after the first marker belongs to the external tool's firstLine.
  // Do not search this token for any later M4B suffix markers.
  const toolOutput = matchServerMessage(`Tool output: ${message.slice(markerIndex + toolOutputMarker.length)}`, matchers);

  if (!toolOutput || toolOutput.id !== toolOutputMessageId) {
    return null;
  }

  return `${translateMatch(baseMatch)} ${translateMatch(toolOutput)}`;
}

function matchFixedM4bBase(message) {
  const match = matchServerMessage(message, matchers);
  return match && Object.keys(match.tokens).length === 0 ? match : null;
}

function markerPositions(value, marker) {
  const positions = [];
  let cursor = 0;

  while (true) {
    const index = value.indexOf(marker, cursor);

    if (index < 0) {
      return positions;
    }

    positions.push(index);
    cursor = index + 1;
  }
}

function translateComposedM4bMessage(message) {
  if (typeof message !== 'string' || !message || message.includes(toolOutputMarker)) {
    return null;
  }

  // Retained-path messages have the construction order: fixed base, optional
  // readable-M4B suffix, then retained-path suffix. Try each marker position,
  // but accept it only when the remaining prefix is an exact fixed source.
  for (const retainedFileIndex of markerPositions(message, retainedFileMarker)) {
    const retainedPath = matchServerMessage(message.slice(retainedFileIndex + 1), matchers);

    if (!retainedPath || retainedPath.id !== retainedFileMessageId) {
      continue;
    }

    let prefix = message.slice(0, retainedFileIndex);
    const suffixes = [retainedPath];

    if (prefix.endsWith(readableM4bSuffix)) {
      const readableSuffix = matchServerMessage(readableM4bSuffix.slice(1), matchers);

      if (!readableSuffix || readableSuffix.id !== readableM4bMessageId) {
        continue;
      }

      prefix = prefix.slice(0, -readableM4bSuffix.length);
      suffixes.unshift(readableSuffix);
    }

    const base = matchFixedM4bBase(prefix);

    if (base) {
      return [base, ...suffixes].map(translateMatch).join(' ');
    }
  }

  if (message.endsWith(readableM4bSuffix)) {
    const base = matchFixedM4bBase(message.slice(0, -readableM4bSuffix.length));
    const suffix = matchServerMessage(readableM4bSuffix.slice(1), matchers);

    if (base && suffix?.id === readableM4bMessageId) {
      return [base, suffix].map(translateMatch).join(' ');
    }
  }

  return null;
}

export default function translateServerMessage(message) {
  const toolOutputTranslation = typeof message === 'string'
    ? translateToolOutputMessage(message)
    : null;

  if (toolOutputTranslation) {
    return toolOutputTranslation;
  }

  const match = matchServerMessage(message, matchers);

  if (match) {
    if (match.id === alternateDownloadClientMessageId) {
      return translateAlternateDownloadClients(match) || message;
    }

    return translateMatch(match);
  }

  return translateComposedM4bMessage(message) || message;
}
