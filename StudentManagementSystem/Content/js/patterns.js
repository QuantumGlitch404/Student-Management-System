/* ========================================================================
   Layout Patterns — JavaScript Behaviors
   Works with ASP.NET Web Forms (UpdatePanel, postbacks) and fetch partials.
   Uses event delegation, survives partial postbacks.
======================================================================== */

(function () {
  'use strict';

  // ------------------------------------------------------------------------
  // Utilities
  // ------------------------------------------------------------------------
  const $ = (sel, ctx = document) => ctx.querySelector(sel);
  const $$ = (sel, ctx = document) => Array.from(ctx.querySelectorAll(sel));
  const on = (type, sel, handler, ctx = document) => {
    ctx.addEventListener(type, e => {
      const t = e.target.closest(sel);
      if (t) handler.call(t, e);
    });
  };
  const emit = (el, name, detail) => el.dispatchEvent(new CustomEvent(name, { detail, bubbles: true }));
  const qs = (key) => new URLSearchParams(location.search).get(key);
  const setQs = (params) => {
    const u = new URL(location.href);
    Object.entries(params).forEach(([k, v]) => v == null ? u.searchParams.delete(k) : u.searchParams.set(k, v));
    history.replaceState(null, '', u);
  };
  const scrollLock = (lock) => document.body.style.overflow = lock ? 'hidden' : '';

  // ------------------------------------------------------------------------
  // P2 — DRAWER
  // ------------------------------------------------------------------------
  let drawerFocusTrap = null;

  function openDrawer(drawerId) {
    const overlay = $('#drawer-overlay');
    const drawer = $(`#${drawerId}`);
    if (!drawer) return;
    overlay.classList.add('is-open');
    drawer.classList.add('is-open');
    scrollLock(true);
    // focus trap
    const focusable = drawer.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
    drawerFocusTrap = focusable[0];
    drawerFocusTrap?.focus();
    emit(document, 'drawer:open', { drawerId });
  }

  function closeDrawer(drawerId) {
    const overlay = $('#drawer-overlay');
    const drawer = drawerId ? $(`#${drawerId}`) : $('.drawer.is-open');
    if (!drawer) return;
    overlay.classList.remove('is-open');
    drawer.classList.remove('is-open');
    scrollLock(false);
    emit(document, 'drawer:close', { drawerId: drawer.id });
  }

  on('click', '[data-drawer-open]', e => {
    const id = e.currentTarget.getAttribute('data-drawer-open');
    openDrawer(id);
  });
  on('click', '[data-drawer-close], #drawer-overlay', e => {
    if (e.target === e.currentTarget || e.target.closest('[data-drawer-close]')) closeDrawer();
  });
  on('keydown', e => {
    if (e.key === 'Escape') closeDrawer();
  });

  // focus trap inside drawer
  document.addEventListener('keydown', e => {
    if (e.key !== 'Tab') return;
    const drawer = $('.drawer.is-open');
    if (!drawer) return;
    const focusable = drawer.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
    if (!focusable.length) return;
    const first = focusable[0], last = focusable[focusable.length - 1];
    if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
    else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
  });

  // ------------------------------------------------------------------------
  // P1 — SPLIT VIEW (list -> detail via fetch / UpdatePanel)
  // ------------------------------------------------------------------------
  on('click', '[data-split-select]', async e => {
    const btn = e.currentTarget;
    const id = btn.getAttribute('data-split-select');
    const target = btn.getAttribute('data-split-target') || '#split-detail';
    const url = btn.getAttribute('data-split-url') || `${location.pathname}?id=${encodeURIComponent(id)}`;
    const container = $(target);
    if (!container) return;

    // update URL without reload
    setQs({ id });

    // update active state
    $$('[data-split-select]').forEach(b => b.classList.remove('is-active'));
    btn.classList.add('is-active');

    // load detail via fetch (returns HTML fragment)
    container.classList.add('is-loading');
    try {
      const res = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
      const html = await res.text();
      container.innerHTML = html;
      emit(container, 'split:load', { id });
      // re-init any patterns inside
      initPatterns(container);
    } catch (err) {
      container.innerHTML = `<div class="tile__empty"><div class="tile__empty-icon">⚠</div><p>Failed to load detail.</p></div>`;
      console.error(err);
    } finally {
      container.classList.remove('is-loading');
    }
  });

  // ------------------------------------------------------------------------
  // P4 — TAB BAR (URL-synced)
  // ------------------------------------------------------------------------
  function initTabBars(root = document) {
    $$('.tab-bar', root).forEach(bar => {
      const param = bar.getAttribute('data-tab-param') || 'tab';
      const current = qs(param) || bar.querySelector('.tab-bar__item')?.dataset.tab;
      if (current) activateTab(bar, current);

      bar.addEventListener('click', e => {
        const item = e.target.closest('.tab-bar__item');
        if (!item) return;
        const tab = item.dataset.tab;
        if (!tab) return;
        setQs({ [param]: tab });
        activateTab(bar, tab);
        emit(bar, 'tab:change', { tab });
      });
    });
  }

  function activateTab(bar, tab) {
    $$('.tab-bar__item', bar).forEach(i => i.classList.toggle('is-active', i.dataset.tab === tab));
    $$('[data-tab-panel]', bar.parentElement).forEach(p => p.hidden = p.dataset.tabPanel !== tab);
  }

  // ------------------------------------------------------------------------
  // P5 — WIZARD
  // ------------------------------------------------------------------------
  function initWizards(root = document) {
    $$('.wizard', root).forEach(w => {
      const steps = $$('.wizard__step', w);
      const panels = $$('[data-wizard-step]', w);
      const param = w.getAttribute('data-wizard-param') || 'step';
      let current = parseInt(qs(param)) || 0;
      goStep(current);

      w.addEventListener('click', e => {
        if (e.target.closest('[data-wizard-next]')) goStep(current + 1);
        if (e.target.closest('[data-wizard-back]')) goStep(current - 1);
        if (e.target.closest('[data-wizard-step-link]')) goStep(parseInt(e.target.closest('[data-wizard-step-link]').dataset.step));
      });

      function goStep(idx) {
        if (idx < 0 || idx >= steps.length) return;
        current = idx;
        setQs({ [param]: idx });
        steps.forEach((s, i) => {
          s.classList.toggle('is-active', i === idx);
          s.classList.toggle('is-complete', i < idx);
        });
        panels.forEach(p => p.hidden = p.dataset.wizardStep != String(idx));
        emit(w, 'wizard:step', { step: idx });
      }
    });
  }

  // ------------------------------------------------------------------------
  // P6 — TABLE WORKSPACE (row click -> drawer, bulk actions)
  // ------------------------------------------------------------------------
  function initTableWorkspaces(root = document) {
    $$('.table-workspace', root).forEach(ws => {
      const table = $('table', ws);
      if (!table) return;

      // row click -> drawer
      on('click', 'tbody tr[data-row-id]', e => {
        const tr = e.currentTarget;
        const id = tr.dataset.rowId;
        const drawerId = ws.getAttribute('data-row-drawer') || 'row-drawer';
        const url = `${location.pathname}?detail=${encodeURIComponent(id)}`;
        // load drawer content
        const drawer = $(`#${drawerId}`);
        if (drawer) {
          const body = $('.drawer__body', drawer);
          body.classList.add('is-loading');
          fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(r => r.text())
            .then(html => { body.innerHTML = html; initPatterns(body); })
            .catch(() => { body.innerHTML = '<div class="tile__empty"><p>Failed to load.</p></div>'; })
            .finally(() => body.classList.remove('is-loading'));
        }
        openDrawer(drawerId);
        setQs({ detail: id });
        tr.classList.add('is-selected');
        $$('tbody tr', table).forEach(r => r !== tr && r.classList.remove('is-selected'));
      }, table);

      // bulk selection
      const selectAll = $('thead input[type="checkbox"]', table);
      if (selectAll) {
        selectAll.addEventListener('change', () => {
          $$('tbody input[type="checkbox"]', table).forEach(cb => cb.checked = selectAll.checked);
          updateSelectionBar(ws);
        });
      }
      on('change', 'tbody input[type="checkbox"]', () => updateSelectionBar(ws), table);

      function updateSelectionBar(ws) {
        const checked = $$('tbody input[type="checkbox"]:checked', ws).length;
        const bar = $('.table-workspace__selection-bar', ws);
        if (bar) {
          bar.hidden = checked === 0;
          if (!bar.hidden) {
            const countEl = $('.selection-count', bar) || bar;
            countEl.textContent = `${checked} item${checked !== 1 ? 's' : ''} selected`;
          }
        }
      }
    });
  }

  // ------------------------------------------------------------------------
  // P9 — FOCUS MODE
  // ------------------------------------------------------------------------
  function initFocusMode() {
    const toggle = $('.focus-mode-toggle');
    if (!toggle) return;
    const saved = localStorage.getItem('focusMode') === 'true';
    if (saved) document.body.classList.add('is-focus-mode');
    toggle.classList.toggle('is-active', saved);
    toggle.addEventListener('click', () => {
      const on = document.body.classList.toggle('is-focus-mode');
      toggle.classList.toggle('is-active', on);
      localStorage.setItem('focusMode', on);
    });
  }

  // ------------------------------------------------------------------------
  // Global init / re-init after partial postbacks
  // ------------------------------------------------------------------------
  function initPatterns(root = document) {
    initTabBars(root);
    initWizards(root);
    initTableWorkspaces(root);
    // drawer overlay ensure
    if (!$('#drawer-overlay')) {
      const ov = document.createElement('div');
      ov.id = 'drawer-overlay';
      ov.className = 'drawer-overlay';
      document.body.appendChild(ov);
    }
  }

  // initial load
  document.addEventListener('DOMContentLoaded', () => {
    initPatterns();
    initFocusMode();
    // restore tab state from URL
    initTabBars();
    initWizards();
    initTableWorkspaces();
  });

  // ASP.NET Web Forms partial postback support
  if (typeof Sys !== 'undefined' && Sys.WebForms && Sys.WebForms.PageRequestManager) {
    Sys.WebForms.PageRequestManager.getInstance().add_endRequest(() => {
      initPatterns();
      initTabBars();
      initWizards();
      initTableWorkspaces();
    });
  }

  // expose for manual use
  window.SMSPatterns = {
    openDrawer, closeDrawer,
    initPatterns, initTabBars, initWizards, initTableWorkspaces,
    setQs, qs, emit
  };

})();