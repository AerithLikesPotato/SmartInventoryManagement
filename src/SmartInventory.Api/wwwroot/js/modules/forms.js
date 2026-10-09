import { qsa } from './dom.js';

/**
 * Form enhancements (all progressive — forms work without JS):
 *  - form[data-submit-lock]: blocks double submits and shows a loading state.
 *      Button text while busy comes from [data-loading-label].
 *  - Mirrors server validation errors to aria-invalid for assistive tech.
 */
export function initForms() {
  qsa('.input-validation-error').forEach((el) => el.setAttribute('aria-invalid', 'true'));

  // Move focus to the first error so keyboard/screen-reader users land on it.
  const firstError = document.querySelector('.input-validation-error');
  if (firstError && !document.activeElement?.matches('.input')) firstError.focus();

  qsa('form[data-submit-lock]').forEach((form) => {
    let submitting = false;

    form.addEventListener('submit', (e) => {
      if (submitting) {
        e.preventDefault();
        return;
      }
      submitting = true;

      const btn = form.querySelector('[type="submit"]');
      if (!btn) return;
      const label = btn.dataset.loadingLabel;
      btn.dataset.originalLabel = btn.textContent;
      if (label) btn.textContent = label;
      btn.classList.add('is-loading');
      btn.setAttribute('aria-busy', 'true');
    });

    // Back/forward cache restores the page in its "busy" state — undo it.
    window.addEventListener('pageshow', (e) => {
      if (!e.persisted) return;
      submitting = false;
      const btn = form.querySelector('[type="submit"]');
      if (!btn) return;
      btn.classList.remove('is-loading');
      btn.removeAttribute('aria-busy');
      if (btn.dataset.originalLabel) btn.textContent = btn.dataset.originalLabel;
    });
  });
}
