import serverMessageMatching from 'Utilities/Localization/server-message-match.cjs';
import serverMessages from 'Utilities/Localization/server-messages.json';
import { translateWithFallback } from 'Utilities/String/translate';

const { compileServerMessageMatchers, matchServerMessage } = serverMessageMatching;
const matchers = compileServerMessageMatchers(serverMessages);

// Only call this at a server-message presentation boundary. Unknown messages
// and captured user, library, provider, path, URL, and protocol data stay intact.
export default function translateServerMessage(message) {
  const match = matchServerMessage(message, matchers);

  return match ? translateWithFallback(match.id, match.source, match.tokens) : message;
}
