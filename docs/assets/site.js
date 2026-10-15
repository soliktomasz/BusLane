(() => {
  const controls = Array.from(document.querySelectorAll('[data-view]'));
  const views = Array.from(document.querySelectorAll('.product-view'));

  document.querySelector('.view-controls').hidden = false;

  controls.forEach((button, index) => {
    button.addEventListener('click', () => {
      controls.forEach((control) => {
        control.setAttribute('aria-pressed', String(control === button));
      });
      views.forEach((view) => {
        const active = view.id === button.getAttribute('aria-controls');
        view.hidden = !active;
        view.classList.toggle('is-switching', active);
      });
    });

    button.addEventListener('keydown', (event) => {
      let nextIndex;
      if (event.key === 'ArrowRight' || event.key === 'ArrowDown') nextIndex = (index + 1) % controls.length;
      if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') nextIndex = (index - 1 + controls.length) % controls.length;
      if (event.key === 'Home') nextIndex = 0;
      if (event.key === 'End') nextIndex = controls.length - 1;
      if (nextIndex === undefined) return;
      event.preventDefault();
      controls[nextIndex].focus();
      controls[nextIndex].click();
    });
  });

  const copyButton = document.getElementById('copy-commands');
  const copyStatus = document.getElementById('copy-status');
  if (!navigator.clipboard?.writeText) return;
  copyButton.hidden = false;
  copyButton.addEventListener('click', async () => {
    copyButton.disabled = true;
    try {
      await navigator.clipboard.writeText(document.getElementById('build-commands').textContent.trim());
      copyStatus.textContent = 'Commands copied.';
    } catch {
      copyStatus.textContent = 'Copy unavailable. Select the commands and copy them manually.';
    } finally {
      copyButton.disabled = false;
    }
  });
})();
