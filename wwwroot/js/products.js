/**
 * Мгновенная фильтрация/поиск каталога через fetch + клиентский lock формы редактирования.
 */
(function () {
    const form = document.getElementById('productsFilterForm');
    const results = document.getElementById('productsResults')
        || (document.querySelector('.product-table-wrapper') ? document.querySelector('.product-table-wrapper').parentElement : null);
    const modalElements = ensureLockModal();
    const lockModal = modalElements.modal;
    const lockClose = modalElements.close;
    const lockKey = 'kodshop.product.edit.lock';
    const lockTtlMs = 30 * 60 * 1000;

    let debounceTimer;
    let abortController;

    function hasFreshEditLock() {
        const raw = localStorage.getItem(lockKey);
        if (!raw) return false;
        const ts = Number(raw);
        if (!Number.isFinite(ts)) {
            localStorage.removeItem(lockKey);
            return false;
        }
        if (Date.now() - ts > lockTtlMs) {
            localStorage.removeItem(lockKey);
            return false;
        }
        return true;
    }

    function showLockModal() {
        if (!lockModal) return;
        lockModal.classList.remove('hidden');
    }

    function hideLockModal() {
        if (!lockModal) return;
        lockModal.classList.add('hidden');
    }

    function bindEditLinks() {
        const links = document.querySelectorAll('[data-product-edit-link="true"], a[href*="/Products/Edit"], a[href*="/Products/Create"], a[href$="/Products/Create"]');
        links.forEach(link => {
            link.addEventListener('click', function (e) {
                if (hasFreshEditLock()) {
                    e.preventDefault();
                    showLockModal();
                    return;
                }
                localStorage.setItem(lockKey, String(Date.now()));
            });
        });
    }

    function ensureLockModal() {
        let modal = document.getElementById('editLockModal');
        let close = document.getElementById('editLockModalClose');
        if (modal && close) return { modal, close };

        modal = document.createElement('div');
        modal.id = 'editLockModal';
        modal.className = 'modal-overlay hidden';
        modal.setAttribute('role', 'dialog');
        modal.setAttribute('aria-modal', 'true');
        modal.innerHTML = [
            '<div class="modal-card">',
            '  <h3>Форма редактирования уже открыта</h3>',
            '  <p>Сначала закройте текущую форму редактирования товара, затем откройте новую.</p>',
            '  <div class="modal-actions">',
            '    <button type="button" id="editLockModalClose" class="btn btn-accent">Понятно</button>',
            '  </div>',
            '</div>'
        ].join('');
        document.body.appendChild(modal);
        close = document.getElementById('editLockModalClose');
        return { modal, close };
    }

    if (lockClose) {
        lockClose.addEventListener('click', hideLockModal);
    }
    if (lockModal) {
        lockModal.addEventListener('click', function (e) {
            if (e.target === lockModal) hideLockModal();
        });
    }

    bindEditLinks();

    if (!form || !results) return;

    const searchInput = document.getElementById('searchInput');
    const discountFilter = document.getElementById('discountFilter');
    const sortField = document.getElementById('sortField');
    const sortDirection = document.getElementById('sortDirection');

    async function fetchFilteredResults() {
        const params = new URLSearchParams(new FormData(form));
        const url = form.action + '?' + params.toString();

        if (abortController) abortController.abort();
        abortController = new AbortController();

        try {
            const response = await fetch(url, {
                method: 'GET',
                headers: { 'X-Requested-With': 'fetch' },
                signal: abortController.signal
            });
            if (!response.ok) return;

            const html = await response.text();
            const doc = new DOMParser().parseFromString(html, 'text/html');
            const next = doc.getElementById('productsResults')
                || (doc.querySelector('.product-table-wrapper') ? doc.querySelector('.product-table-wrapper').parentElement : null);
            if (!next) return;

            results.innerHTML = next.innerHTML;
            history.replaceState(null, '', url);
            bindEditLinks();
        } catch (err) {
            if (err && err.name === 'AbortError') return;
        }
    }

    function debouncedFetch() {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(fetchFilteredResults, 300);
    }

    if (searchInput) {
        searchInput.addEventListener('input', debouncedFetch);
    }

    [discountFilter, sortField, sortDirection].forEach(el => {
        if (el) el.addEventListener('change', fetchFilteredResults);
    });
})();