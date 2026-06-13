/**
 * Клиентская логика страницы «Список товаров».
 * Фильтрация по поисковой строке и диапазону скидки, сортировка по цене/количеству/скидке.
 * Подключается только для ролей с расширенными инструментами (CanUseAdvancedTools).
 */
(function () {
    // Элементы панели инструментов и тело таблицы товаров
    const searchInput = document.getElementById('searchInput');
    const discountFilter = document.getElementById('discountFilter');
    const sortField = document.getElementById('sortField');
    const sortDirection = document.getElementById('sortDirection');
    const tbody = document.querySelector('#productsTable tbody');

    // Если таблица отсутствует на странице — скрипт не выполняется
    if (!tbody) return;

    /** Возвращает все строки товаров из tbody */
    function getRows() {
        return Array.from(tbody.querySelectorAll('.product-row'));
    }

    /**
     * Проверяет, попадает ли скидка строки в выбранный диапазон.
     * Диапазоны различаются для вариантов экзамена (Profile / Standard).
     * @param {HTMLElement} row — строка таблицы с data-discount
     * @param {string} filterValue — значение option из discountFilter
     */
    function matchDiscount(row, filterValue) {
        const discount = parseFloat(row.dataset.discount || '0');
        switch (filterValue) {
            // Вариант Profile (09.02.07-2)
            case '0-12.99': return discount >= 0 && discount <= 12.99;
            case '13-16.99': return discount >= 13 && discount <= 16.99;
            case '17+': return discount >= 17;
            // Вариант Standard (09.02.07-1)
            case '0-11.99': return discount >= 0 && discount <= 11.99;
            case '12-18.99': return discount >= 12 && discount <= 18.99;
            case '19+': return discount >= 19;
            default: return true; // «Все диапазоны»
        }
    }

    /** Применяет фильтры, сортирует видимые строки и обновляет порядок в DOM */
    function applyFiltersAndSort() {
        const term = (searchInput?.value || '').trim().toLowerCase();
        const filterValue = discountFilter?.value || 'all';
        const field = sortField?.value || 'price';
        const direction = sortDirection?.value || 'asc';

        // Фильтрация: текстовый поиск по data-search и диапазон скидки
        let rows = getRows().filter(row => {
            const searchText = row.dataset.search || '';
            const matchesSearch = !term || searchText.includes(term);
            const matchesDiscount = matchDiscount(row, filterValue);
            return matchesSearch && matchesDiscount;
        });

        // Сортировка по числовому полю из data-* (price, quantity, discount)
        rows.sort((a, b) => {
            const aValue = parseFloat(a.dataset[field] || '0');
            const bValue = parseFloat(b.dataset[field] || '0');
            return direction === 'asc' ? aValue - bValue : bValue - aValue;
        });

        // Перестановка строк в DOM в отсортированном порядке
        rows.forEach(row => tbody.appendChild(row));

        // Скрытие строк, не прошедших фильтрацию
        getRows().forEach(row => {
            row.style.display = rows.includes(row) ? '' : 'none';
        });
    }

    // Подписка на изменения всех элементов панели инструментов
    [searchInput, discountFilter, sortField, sortDirection].forEach(el => {
        if (el) el.addEventListener('input', applyFiltersAndSort);
        if (el && el.tagName === 'SELECT') el.addEventListener('change', applyFiltersAndSort);
    });

    // Первичное применение фильтров при загрузке страницы
    applyFiltersAndSort();
})();
