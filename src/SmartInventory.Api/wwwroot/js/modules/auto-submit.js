import { qs, qsa } from './dom.js';

/**
 * form[data-autosubmit] (a normal GET form) submits itself as the user types
 * or picks a filter. Without JS the visible submit button still works.
 *
 * The page reloads (server-side filtering keeps pagination correct), so the
 * focused field is remembered and restored after navigation.
 */
const FOCUS_KEY = 'si-refocus';
const DEBOUNCE_MS = 400;

export function initAutoSubmit() {
  restoreFocus();

  qsa('form[data-autosubmit]').forEach((form) => {
    let timer;

    const submit = (field) => {
      clearTimeout(timer);
      try {
        if (field.id) sessionStorage.setItem(FOCUS_KEY, field.id);
      } catch {
        /* ignore */
      }
      form.requestSubmit();
    };

    form.addEventListener('input', (e) => {
      if (e.target.matches('input[type="search"], input[type="text"]')) {
        clearTimeout(timer);
        timer = setTimeout(() => submit(e.target), DEBOUNCE_MS);
      }
    });

    form.addEventListener('change', (e) => {
      if (e.target.matches('input[type="radio"], select')) submit(e.target);
    });
  });
}

function restoreFocus() {
  let id = null;
  try {
    id = sessionStorage.getItem(FOCUS_KEY);
    sessionStorage.removeItem(FOCUS_KEY);
  } catch {
    /* ignore */
  }
  const el = id && qs(`#${CSS.escape(id)}`);
  if (!el) return;
  el.focus();
  if (typeof el.setSelectionRange === 'function' && el.type !== 'radio') {
    const end = el.value.length;
    el.setSelectionRange(end, end);
  }
}
