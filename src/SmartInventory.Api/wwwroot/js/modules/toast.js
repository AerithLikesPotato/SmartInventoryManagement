import { qs, qsa } from './dom.js';

/**
 * Toasts live in <div class="toasts" data-toasts aria-live="polite">.
 * The server can pre-render [data-toast] elements (see _Toasts.cshtml via
 * TempData); other modules can call showToast(message, kind).
 */
const AUTO_HIDE_MS = 5000;

const region = () => qs('[data-toasts]');

function dismiss(toast) {
  toast.classList.add('is-leaving');
  setTimeout(() => toast.remove(), 260);
}

function arm(toast) {
  let timer = setTimeout(() => dismiss(toast), AUTO_HIDE_MS);
  // Don't yank a toast away while someone is reading or focused on it.
  const pause = () => clearTimeout(timer);
  const resume = () => {
    timer = setTimeout(() => dismiss(toast), AUTO_HIDE_MS);
  };
  toast.addEventListener('mouseenter', pause);
  toast.addEventListener('mouseleave', resume);
  toast.addEventListener('focusin', pause);
  toast.addEventListener('focusout', resume);
  qs('[data-toast-close]', toast)?.addEventListener('click', () => dismiss(toast));
}

export function showToast(message, kind = 'info') {
  const host = region();
  if (!host) return;

  const toast = document.createElement('div');
  toast.className = `toast toast--${kind}`;
  toast.dataset.toast = '';
  toast.innerHTML = `
    <svg class="icon toast__icon" aria-hidden="true" focusable="false"><use href="#i-${
      kind === 'error' ? 'alert' : 'check-circle'
    }"></use></svg>
    <span data-toast-text></span>
    <button type="button" class="btn btn--icon" data-toast-close aria-label="Dismiss notification">
      <svg class="icon" aria-hidden="true" focusable="false"><use href="#i-x"></use></svg>
    </button>`;
  qs('[data-toast-text]', toast).textContent = message; // textContent: never inject HTML
  host.append(toast);
  arm(toast);
}

export function initToasts() {
  qsa('[data-toast]').forEach(arm);
}
