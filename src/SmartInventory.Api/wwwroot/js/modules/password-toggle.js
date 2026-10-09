import { qsa } from './dom.js';

/** <button data-password-toggle aria-controls="Password"> shows/hides the field. */
export function initPasswordToggle() {
  qsa('[data-password-toggle]').forEach((btn) => {
    const input = document.getElementById(btn.getAttribute('aria-controls'));
    if (!input) return;

    btn.addEventListener('click', () => {
      const show = input.type === 'password';
      input.type = show ? 'text' : 'password';
      btn.setAttribute('aria-pressed', String(show));
      btn.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
      btn.querySelector('use')?.setAttribute('href', show ? '#i-eye-off' : '#i-eye');
    });
  });
}
