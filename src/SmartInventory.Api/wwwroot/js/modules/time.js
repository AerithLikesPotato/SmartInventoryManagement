import { qsa } from './dom.js';

/**
 * <time datetime="2026-10-10T03:12:00Z" data-localize>…UTC fallback…</time>
 * is rewritten in the viewer's own time zone ("Oct 10, 10:12 AM").
 * The server only knows UTC, so this is the correct place to localise.
 */
const format = new Intl.DateTimeFormat(undefined, {
  month: 'short',
  day: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
});

export function initLocalTime() {
  qsa('time[data-localize]').forEach((el) => {
    const date = new Date(el.getAttribute('datetime'));
    if (!Number.isNaN(date.getTime())) el.textContent = format.format(date);
  });
}
