/* LanCloud — رفتار صفحات (بدون هیچ کتابخانه‌ی خارجی) */
(() => {
  'use strict';

  const $ = (sel, root = document) => root.querySelector(sel);
  const $$ = (sel, root = document) => Array.from(root.querySelectorAll(sel));

  const LC = window.LC || { urls: {} };
  const csrf = document.querySelector('meta[name="csrf-token"]')?.content || '';
  const html = document.documentElement;

  const store = {
    get(k) { try { return localStorage.getItem(k); } catch { return null; } },
    set(k, v) { try { localStorage.setItem(k, v); } catch { /* ignore */ } }
  };

  /* ───────────── تم تاریک/روشن (با انیمیشن) ───────────── */
  const reduceMotion = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;

  function setTheme(next, origin) {
    const apply = () => {
      html.setAttribute('data-theme', next);
      store.set('lc-theme', next);
    };

    if (!reduceMotion && document.startViewTransition && origin) {
      const x = origin.x, y = origin.y;
      const radius = Math.hypot(Math.max(x, innerWidth - x), Math.max(y, innerHeight - y));
      const t = document.startViewTransition(apply);
      t.ready.then(() => {
        html.animate(
          { clipPath: [`circle(0px at ${x}px ${y}px)`, `circle(${radius}px at ${x}px ${y}px)`] },
          { duration: 600, easing: 'cubic-bezier(.4,0,.2,1)', pseudoElement: '::view-transition-new(root)' }
        );
      }).catch(() => { });
      return;
    }

    html.classList.add('theme-anim');
    apply();
    setTimeout(() => html.classList.remove('theme-anim'), 450);
  }

  $$('[data-theme-toggle]').forEach((btn) => {
    btn.addEventListener('click', () => {
      const next = html.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
      const r = btn.getBoundingClientRect();
      setTheme(next, { x: r.left + r.width / 2, y: r.top + r.height / 2 });
    });
  });

  /* ───────────── منوی کناری در موبایل ───────────── */
  const sidebar = $('#sidebar');
  const backdrop = $('#backdrop');
  const setSidebar = (open) => {
    sidebar?.classList.toggle('open', open);
    backdrop?.classList.toggle('show', open);
    document.body.classList.toggle('no-scroll', open);
    $$('[data-menu-toggle]').forEach((b) => {
      b.classList.toggle('active', open);
      b.setAttribute('aria-expanded', open ? 'true' : 'false');
    });
  };
  $$('[data-menu-toggle]').forEach((b) => b.addEventListener('click', () => setSidebar(!sidebar.classList.contains('open'))));
  backdrop?.addEventListener('click', () => setSidebar(false));
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape') setSidebar(false); });
  window.addEventListener('resize', () => { if (innerWidth > 980) setSidebar(false); });

  /* ───────────── پیام‌ها (toast) ───────────── */
  const toastBox = $('#toasts');
  function toast(message, type = 'ok') {
    if (!toastBox) return;
    const el = document.createElement('div');
    el.className = 'toast ' + type;
    el.setAttribute('role', 'status');
    el.innerHTML = `<svg class="icon" aria-hidden="true"><use href="#i-${type === 'ok' ? 'check' : 'alert'}"></use></svg>`;
    const span = document.createElement('span');
    span.textContent = message;
    el.appendChild(span);
    toastBox.appendChild(el);
    setTimeout(() => { el.classList.add('out'); setTimeout(() => el.remove(), 350); }, 5000);
  }
  $$('#toasts .toast').forEach((el) => {
    setTimeout(() => { el.classList.add('out'); setTimeout(() => el.remove(), 350); }, 5000);
  });

  /* ───────────── دیالوگ‌ها ───────────── */
  $$('dialog').forEach((dlg) => {
    dlg.addEventListener('click', (e) => { if (e.target === dlg) dlg.close(); });
  });
  document.addEventListener('click', (e) => {
    const closer = e.target.closest('[data-close]');
    if (closer) closer.closest('dialog')?.close();

    const opener = e.target.closest('[data-open]');
    if (opener) {
      const dlg = document.getElementById(opener.dataset.open);
      if (dlg) {
        dlg.showModal();
        const first = dlg.querySelector('input[type="text"], input:not([type="hidden"]):not([type="checkbox"])');
        first?.focus();
      }
    }
  });

  // تأیید قبل از ارسال فرم‌هایی که data-confirm دارند
  const confirmDlg = $('#confirmDlg');
  if (confirmDlg) {
    let pendingForm = null;
    document.addEventListener('submit', (e) => {
      const form = e.target;
      if (!(form instanceof HTMLFormElement) || !form.dataset.confirm || form.dataset.confirmed) return;
      e.preventDefault();
      pendingForm = form;
      $('#confirmText', confirmDlg).textContent = form.dataset.confirm;
      const ok = $('#confirmOk', confirmDlg);
      ok.className = 'btn ' + (form.dataset.confirmStyle === 'primary' ? 'btn-primary' : 'btn-danger');
      confirmDlg.showModal();
    });
    $('#confirmOk', confirmDlg)?.addEventListener('click', () => {
      confirmDlg.close();
      if (pendingForm) {
        pendingForm.dataset.confirmed = '1';
        pendingForm.requestSubmit();
      }
    });
    confirmDlg.addEventListener('close', () => { if (pendingForm && !pendingForm.dataset.confirmed) pendingForm = null; });
  }

  // ارسال خودکار فرم با تغییر select (مرتب‌سازی)
  document.addEventListener('change', (e) => {
    const el = e.target;
    if (el instanceof HTMLElement && el.hasAttribute('data-autosubmit')) el.closest('form')?.submit();
  });

  /* ───────────── حالت نمایش شبکه‌ای / لیستی ───────────── */
  const itemsBox = $('#items');
  if (itemsBox) {
    $$('.item', itemsBox).forEach((el, i) => el.style.setProperty('--i', Math.min(i, 24)));
    const applyView = (v, animate) => {
      itemsBox.classList.toggle('view-grid', v === 'grid');
      itemsBox.classList.toggle('view-list', v === 'list');
      $$('[data-view]').forEach((b) => b.classList.toggle('active', b.dataset.view === v));
      if (animate) {
        // انیمیشن ورود دوباره پخش شود
        itemsBox.classList.remove('replay');
        void itemsBox.offsetWidth;
        itemsBox.classList.add('replay');
      }
    };
    applyView(store.get('lc-view') === 'list' ? 'list' : 'grid', false);
    $$('[data-view]').forEach((b) => b.addEventListener('click', () => {
      if (b.classList.contains('active')) return;
      store.set('lc-view', b.dataset.view);
      applyView(b.dataset.view, true);
    }));
  }

  /* ───────────── منوی سه‌نقطه‌ی هر آیتم ───────────── */
  function closeMenus() {
    $$('.menu-wrap.open').forEach((w) => {
      w.classList.remove('open');
      w.closest('.item')?.classList.remove('menu-open');
      w.querySelector('.menu-btn')?.setAttribute('aria-expanded', 'false');
    });
  }

  document.addEventListener('click', (e) => {
    const btn = e.target.closest('.menu-btn');
    if (btn) {
      e.preventDefault();
      const wrap = btn.closest('.menu-wrap');
      const wasOpen = wrap.classList.contains('open');
      closeMenus();
      if (!wasOpen) {
        wrap.classList.add('open');
        wrap.closest('.item')?.classList.add('menu-open');
        btn.setAttribute('aria-expanded', 'true');
      }
      return;
    }
    if (!e.target.closest('.menu')) closeMenus();
  });
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape') closeMenus(); });

  /* ───────────── نمایشگر: تصویر / ویدیو / صوت / PDF / متن + قبلی و بعدی ───────────── */
  const previewDlg = $('#previewDlg');
  const stage = $('#previewStage');
  const pvPrev = $('#pvPrev');
  const pvNext = $('#pvNext');
  const pvCount = $('#previewCount');
  const GALLERY = '#items .item[data-preview="image"], #items .item[data-preview="video"]';
  const TEXT_CHUNK = 256 * 1024; // فقط ابتدای فایل‌های متنی خوانده می‌شود

  let pvId = null;
  let pvKind = null;
  let pvSeq = 0;
  let pvAbort = null;

  // پخش و دانلود رسانه را کامل متوقف می‌کند (برای صرفه‌جویی در ترافیک)
  function stopMedia() {
    pvAbort?.abort();
    pvAbort = null;
    stage.querySelectorAll('video, audio').forEach((m) => {
      try { m.pause(); } catch { /* ignore */ }
      m.removeAttribute('src');
      m.load();
    });
    stage.replaceChildren();
  }

  const galleryItems = () => (pvKind === 'image' || pvKind === 'video') ? $$(GALLERY) : [];

  function updateNav() {
    const list = galleryItems();
    const idx = list.findIndex((el) => el.dataset.id === String(pvId));
    const show = list.length > 1 && idx >= 0;
    pvPrev.hidden = pvNext.hidden = pvCount.hidden = !show;
    if (!show) return;

    pvCount.textContent = `${idx + 1} / ${list.length}`;
    pvPrev.disabled = idx === 0;
    pvNext.disabled = idx === list.length - 1;

    // تصویرهای همسایه از قبل بارگذاری می‌شوند؛ ویدیوها نه (حجیم‌اند)
    [list[idx - 1], list[idx + 1]].forEach((el) => {
      if (el && el.dataset.preview === 'image') new Image().src = `${LC.urls.preview}/${el.dataset.id}`;
    });
  }

  function message(text) {
    const d = document.createElement('div');
    d.className = 'pv-msg';
    d.textContent = text;
    return d;
  }

  async function renderText(src, seq) {
    const box = document.createElement('pre');
    box.className = 'pv-text';
    box.dir = 'auto';
    box.textContent = 'در حال بارگذاری…';
    stage.appendChild(box);

    pvAbort = new AbortController();
    try {
      // فقط بخش اول فایل با درخواست Range گرفته می‌شود
      const res = await fetch(src, {
        credentials: 'same-origin',
        headers: { Range: `bytes=0-${TEXT_CHUNK - 1}` },
        signal: pvAbort.signal
      });
      if (!res.ok) throw new Error('bad status');
      const buf = await res.arrayBuffer();
      if (seq !== pvSeq) return;

      box.textContent = new TextDecoder('utf-8').decode(buf);
      if (!buf.byteLength) box.textContent = '(فایل خالی است)';

      const range = res.headers.get('Content-Range');
      const total = range ? Number(range.split('/')[1]) : buf.byteLength;
      if (total > buf.byteLength) {
        const note = document.createElement('div');
        note.className = 'pv-note';
        note.textContent = 'فقط بخش ابتدایی فایل نمایش داده شد؛ برای دیدن همه‌ی آن دانلودش کن.';
        stage.appendChild(note);
      }
    } catch (err) {
      if (err.name === 'AbortError' || seq !== pvSeq) return;
      box.textContent = 'نمایش این فایل ممکن نشد.';
    }
  }

  function showPreview(id, name, kind) {
    stopMedia();
    const seq = ++pvSeq;
    const alive = () => seq === pvSeq;

    pvId = id;
    pvKind = kind;
    $('#previewTitle', previewDlg).textContent = name;
    $('#previewDownload', previewDlg).href = `${LC.urls.download}/${id}`;
    stage.dataset.kind = kind;

    const src = `${LC.urls.preview}/${id}`;
    const loader = document.createElement('div');
    loader.className = 'pv-loader';
    const loaded = (el) => { if (!alive()) return; loader.remove(); el?.classList.add('ready'); };
    const failed = () => { if (!alive()) return; stage.replaceChildren(message('نمایش این فایل ممکن نشد. می‌توانی آن را دانلود کنی.')); };

    let el = null;
    if (kind === 'image') {
      el = new Image();
      el.alt = name;
      el.className = 'pv-media';
      el.decoding = 'async';
      el.onload = () => loaded(el);
      el.onerror = failed;
      el.src = src;
    } else if (kind === 'video' || kind === 'audio') {
      // preload=metadata: فقط اطلاعات اولیه گرفته می‌شود؛ بقیه‌ی ویدیو هنگام پخش/جلو بردن با Range تکه‌تکه می‌آید
      el = document.createElement(kind);
      el.className = 'pv-media';
      el.controls = true;
      el.autoplay = true;
      el.preload = 'metadata';
      el.playsInline = true;
      if (kind === 'video') el.setAttribute('controlslist', 'nodownload');
      el.onloadedmetadata = () => loaded(el);
      el.onerror = failed;
      el.src = src;
    } else if (kind === 'text') {
      renderText(src, seq);
    } else {
      el = document.createElement('iframe');
      el.title = name;
      el.className = 'pv-media';
      el.onload = () => loaded(el);
      el.src = src;
    }

    if (el) {
      stage.appendChild(loader);
      stage.appendChild(el);
    }
    updateNav();
  }

  function openPreview(id, name, kind) {
    if (!previewDlg) return;
    showPreview(id, name, kind);
    if (!previewDlg.open) previewDlg.showModal();
  }

  function step(delta) {
    const list = galleryItems();
    const idx = list.findIndex((el) => el.dataset.id === String(pvId));
    const target = list[idx + delta];
    if (idx < 0 || !target) return;
    showPreview(target.dataset.id, target.dataset.name, target.dataset.preview);
  }

  if (previewDlg) {
    pvPrev.addEventListener('click', () => step(-1));
    pvNext.addEventListener('click', () => step(1));

    previewDlg.addEventListener('close', () => {
      pvSeq++;
      stopMedia();
      pvId = pvKind = null;
    });

    // کیبورد (صفحه راست‌چین است: راست = قبلی، چپ = بعدی). داخل پخش‌کننده پیکان‌ها برای جلو/عقب بردن ویدیو می‌مانند.
    document.addEventListener('keydown', (e) => {
      if (!previewDlg.open) return;
      const inMedia = e.target instanceof HTMLMediaElement;
      if (e.key === 'PageUp' || (!inMedia && e.key === 'ArrowRight')) { e.preventDefault(); step(-1); }
      else if (e.key === 'PageDown' || (!inMedia && e.key === 'ArrowLeft')) { e.preventDefault(); step(1); }
    });

    // کشیدن انگشت روی تصویر در موبایل
    let tx = 0, ty = 0;
    stage.addEventListener('touchstart', (e) => { tx = e.changedTouches[0].clientX; ty = e.changedTouches[0].clientY; }, { passive: true });
    stage.addEventListener('touchend', (e) => {
      if (pvKind !== 'image') return;
      const dx = e.changedTouches[0].clientX - tx;
      const dy = e.changedTouches[0].clientY - ty;
      if (Math.abs(dx) > 60 && Math.abs(dx) > Math.abs(dy) * 1.5) step(dx < 0 ? 1 : -1);
    }, { passive: true });
  }

  // کلیک روی فایل: اگر پیش‌نمایش داشت باز شود، وگرنه لینک دانلود دنبال شود
  document.addEventListener('click', (e) => {
    const link = e.target.closest('.item-main');
    if (!link) return;
    const item = link.closest('.item');
    if (!item || item.dataset.folder === '1' || !item.dataset.preview) return;
    if (e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey) return;
    e.preventDefault();
    openPreview(item.dataset.id, item.dataset.name, item.dataset.preview);
  });

  /* ───────────── اکشن‌های هر آیتم ───────────── */
  async function openShare(id, name) {
    const dlg = $('#shareDlg');
    if (!dlg) return;
    $('#shareTitle', dlg).textContent = name;
    const body = $('#shareBody', dlg);
    body.innerHTML = '<p class="muted" style="padding:24px 0;text-align:center">در حال بارگذاری…</p>';
    dlg.showModal();
    try {
      const res = await fetch(`${LC.urls.shareManage}/${id}`, {
        credentials: 'same-origin',
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
      });
      if (!res.ok) throw new Error('bad status');
      body.innerHTML = await res.text();
    } catch {
      body.innerHTML = '<p class="muted" style="padding:24px 0;text-align:center">بارگذاری انجام نشد. دوباره تلاش کن.</p>';
    }
  }

  document.addEventListener('click', (e) => {
    const act = e.target.closest('[data-action]');
    if (!act) return;
    const item = act.closest('.item');
    if (!item) return;

    const id = item.dataset.id;
    const name = item.dataset.name;
    closeMenus();

    switch (act.dataset.action) {
      case 'preview':
        openPreview(id, name, item.dataset.preview);
        break;
      case 'rename': {
        const dlg = $('#renameDlg');
        $('[name="id"]', dlg).value = id;
        const input = $('[name="name"]', dlg);
        input.value = name;
        dlg.showModal();
        input.focus();
        const dot = item.dataset.folder === '1' ? -1 : name.lastIndexOf('.');
        input.setSelectionRange(0, dot > 0 ? dot : name.length);
        break;
      }
      case 'delete': {
        const dlg = $('#deleteDlg');
        $('[name="id"]', dlg).value = id;
        $('#deleteName', dlg).textContent = name;
        $('#deleteNote', dlg).hidden = item.dataset.folder !== '1';
        dlg.showModal();
        break;
      }
      case 'share':
        openShare(id, name);
        break;
      default:
        break;
    }
  });

  // داخل دیالوگ اشتراک: فیلتر کاربران و کپی لینک
  document.addEventListener('input', (e) => {
    if (e.target.id !== 'shareFilter') return;
    const term = e.target.value.trim().toLowerCase();
    $$('#shareBody .user-row').forEach((row) => {
      row.hidden = term !== '' && !row.dataset.search.includes(term);
    });
  });

  async function copyText(text) {
    try {
      if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(text);
        return true;
      }
    } catch { /* fall through */ }
    const ta = document.createElement('textarea');
    ta.value = text;
    ta.style.cssText = 'position:fixed;opacity:0;top:0;left:0';
    document.body.appendChild(ta);
    ta.select();
    let ok = false;
    try { ok = document.execCommand('copy'); } catch { /* ignore */ }
    ta.remove();
    return ok;
  }

  document.addEventListener('click', async (e) => {
    const btn = e.target.closest('[data-copy]');
    if (!btn) return;
    const url = new URL(btn.dataset.copy, location.origin).href;
    const ok = await copyText(url);
    toast(ok ? 'لینک کپی شد.' : 'کپی انجام نشد؛ لینک را دستی کپی کن.', ok ? 'ok' : 'err');
  });

  // بازنشانی رمز (مدیریت)
  document.addEventListener('click', (e) => {
    const btn = e.target.closest('[data-reset]');
    if (!btn) return;
    const dlg = $('#resetDlg');
    if (!dlg) return;
    $('[name="id"]', dlg).value = btn.dataset.id;
    $('#resetName', dlg).textContent = btn.dataset.name;
    dlg.showModal();
    $('[name="newPassword"]', dlg).focus();
  });

  /* ───────────── انتقال با کشیدن و رها کردن ───────────── */
  const ITEM_TYPE = 'text/x-lc-item';
  const hasType = (e, type) => Array.from(e.dataTransfer?.types || []).includes(type);
  const dropTargetOf = (el) => el.closest?.('.item.is-folder, [data-drop-folder]');
  const clearTargets = () => $$('.drop-target').forEach((x) => x.classList.remove('drop-target'));

  document.addEventListener('dragstart', (e) => {
    const item = e.target.closest?.('.item[draggable="true"]');
    if (!item) return;
    e.dataTransfer.setData(ITEM_TYPE, item.dataset.id);
    e.dataTransfer.effectAllowed = 'move';
    item.classList.add('dragging');
  });
  document.addEventListener('dragend', () => {
    $$('.dragging').forEach((x) => x.classList.remove('dragging'));
    clearTargets();
  });
  document.addEventListener('dragover', (e) => {
    if (!hasType(e, ITEM_TYPE)) return;
    const target = dropTargetOf(e.target);
    clearTargets();
    if (!target || target.classList.contains('dragging')) return;
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    target.classList.add('drop-target');
  });
  document.addEventListener('drop', async (e) => {
    if (!hasType(e, ITEM_TYPE)) return;
    const target = dropTargetOf(e.target);
    clearTargets();
    if (!target) return;
    e.preventDefault();

    const id = e.dataTransfer.getData(ITEM_TYPE);
    const targetId = target.dataset.dropFolder !== undefined ? target.dataset.dropFolder : target.dataset.id;
    if (!id || id === targetId) return;

    try {
      const res = await fetch(LC.urls.move, {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'RequestVerificationToken': csrf, 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({ id, targetId: targetId || '' })
      });
      const data = await res.json().catch(() => ({}));
      if (res.ok && data.ok) {
        location.reload();
      } else {
        toast(data.error || 'انتقال انجام نشد.', 'err');
      }
    } catch {
      toast('خطای شبکه؛ انتقال انجام نشد.', 'err');
    }
  });

  /* ───────────── آپلود ───────────── */
  const uploadBtn = $('#uploadBtn');
  const fileInput = $('#fileInput');
  const panel = $('#uploadPanel');
  const list = $('#uploadList');
  const dropzone = $('#dropzone');

  if (uploadBtn && fileInput && panel && list) {
    const folderId = uploadBtn.dataset.folder || '';
    let free = Number(uploadBtn.dataset.free); // بایت باقی‌مانده؛ منفی یعنی نامحدود
    if (Number.isNaN(free)) free = -1;

    const fmtSize = (n) => {
      if (n < 1024) return n + ' B';
      const u = ['KB', 'MB', 'GB', 'TB'];
      let v = n / 1024, i = 0;
      while (v >= 1024 && i < u.length - 1) { v /= 1024; i++; }
      return (Math.round(v * 10) / 10) + ' ' + u[i];
    };

    function addRow(name) {
      const row = document.createElement('div');
      row.className = 'up-row';
      row.innerHTML = '<div class="top"><span class="nm" dir="auto"></span><span class="st">در صف</span></div><div class="meter"><span style="width:0%"></span></div>';
      $('.nm', row).textContent = name;
      list.appendChild(row);
      list.scrollTop = list.scrollHeight;
      return {
        progress(p) { $('.meter span', row).style.width = p + '%'; $('.st', row).textContent = p + '٪'; },
        done() { row.classList.add('done'); $('.meter span', row).style.width = '100%'; $('.st', row).textContent = 'تمام شد'; },
        fail(msg) { row.classList.add('fail'); $('.meter span', row).style.width = '100%'; $('.st', row).textContent = msg; }
      };
    }

    function sendOne(file, row) {
      return new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        const fd = new FormData();
        if (folderId) fd.append('folderId', folderId);
        fd.append('file', file, file.name);

        xhr.open('POST', LC.urls.upload);
        xhr.setRequestHeader('RequestVerificationToken', csrf);
        xhr.upload.onprogress = (ev) => {
          if (ev.lengthComputable) row.progress(Math.min(99, Math.round((ev.loaded / ev.total) * 100)));
        };
        xhr.onload = () => {
          let data = null;
          try { data = JSON.parse(xhr.responseText); } catch { /* ignore */ }
          if (xhr.status >= 200 && xhr.status < 300 && data?.ok) resolve(data);
          else reject(new Error(data?.error || (xhr.status === 401 ? 'نشست منقضی شده؛ دوباره وارد شو.' : 'خطا در آپلود')));
        };
        xhr.onerror = () => reject(new Error('خطای شبکه'));
        xhr.send(fd);
      });
    }

    let busy = false;
    async function uploadFiles(files) {
      if (busy) { toast('آپلود قبلی هنوز در جریان است.', 'err'); return; }
      files = Array.from(files).filter(Boolean);
      if (!files.length) return;

      busy = true;
      panel.hidden = false;
      list.replaceChildren();
      const title = $('#uploadTitle');
      const closeBtn = $('#uploadClose');
      closeBtn.hidden = true;
      title.textContent = `در حال آپلود ${files.length} فایل…`;

      let ok = 0, failed = 0;
      for (const f of files) {
        const row = addRow(f.name);
        if (free >= 0 && f.size > free) {
          row.fail('فضا کافی نیست');
          failed++;
          continue;
        }
        try {
          await sendOne(f, row);
          row.done();
          ok++;
          if (free >= 0) free -= f.size;
        } catch (err) {
          row.fail(err.message || 'خطا');
          failed++;
        }
      }

      busy = false;
      title.textContent = failed ? `${ok} فایل آپلود شد، ${failed} مورد ناموفق` : `${ok} فایل آپلود شد`;
      if (!failed) {
        setTimeout(() => location.reload(), 700);
      } else {
        closeBtn.hidden = false;
        closeBtn.onclick = () => location.reload();
      }
    }

    uploadBtn.addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', () => {
      uploadFiles(fileInput.files);
      fileInput.value = '';
    });

    window.addEventListener('beforeunload', (e) => { if (busy) { e.preventDefault(); e.returnValue = ''; } });

    // کشیدن فایل از کامپیوتر روی صفحه
    let depth = 0;
    const isFileDrag = (e) => hasType(e, 'Files');

    window.addEventListener('dragenter', (e) => {
      if (!isFileDrag(e)) return;
      depth++;
      dropzone?.classList.add('show');
    });
    window.addEventListener('dragleave', (e) => {
      if (!isFileDrag(e)) return;
      depth = Math.max(0, depth - 1);
      if (!depth) dropzone?.classList.remove('show');
    });
    window.addEventListener('dragover', (e) => { if (isFileDrag(e)) e.preventDefault(); });
    window.addEventListener('drop', (e) => {
      if (!isFileDrag(e)) return;
      e.preventDefault();
      depth = 0;
      dropzone?.classList.remove('show');

      const dt = e.dataTransfer;
      const files = [];
      let skippedFolders = 0;
      if (dt.items && dt.items.length && dt.items[0].webkitGetAsEntry) {
        for (const it of dt.items) {
          if (it.kind !== 'file') continue;
          const entry = it.webkitGetAsEntry();
          if (entry && entry.isDirectory) { skippedFolders++; continue; }
          const f = it.getAsFile();
          if (f) files.push(f);
        }
      } else {
        files.push(...dt.files);
      }
      if (skippedFolders) toast('آپلود پوشه پشتیبانی نمی‌شود؛ پوشه را بساز و فایل‌ها را داخلش بریز.', 'err');
      uploadFiles(files);
    });
  } else {
    // صفحاتی که آپلود ندارند: رها کردن فایل نباید صفحه را عوض کند
    window.addEventListener('dragover', (e) => { if (hasType(e, 'Files')) e.preventDefault(); });
    window.addEventListener('drop', (e) => { if (hasType(e, 'Files')) e.preventDefault(); });
  }
})();
