// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
(function () {
    function onNextFrame(callback) {
        if (typeof window.requestAnimationFrame !== 'function') {
            window.setTimeout(callback, 0);
            return;
        }

        window.requestAnimationFrame(function () {
            window.requestAnimationFrame(callback);
        });
    }

    var pendingTasks = new Set();
    var taskMessages = new Map();
    var loaderInitialized = false;
    var dataTableTrackingInitialized = false;
    var bootOverlayReleased = false;
    var bootTrackingEnabled = true;
    var dataTableSequence = 0;
    var defaultLoaderMessage = '';

    function getBody() {
        return document.body instanceof HTMLElement ? document.body : null;
    }

    function getLoaderMessageElement() {
        return document.getElementById('global-loading-spinner-text');
    }

    function getScreenReaderMessageElement() {
        return document.getElementById('loadingText');
    }

    function captureDefaultLoaderMessage() {
        var messageElement = getLoaderMessageElement();
        if (!messageElement) {
            return;
        }

        defaultLoaderMessage = messageElement.dataset.defaultText
            || messageElement.textContent?.trim()
            || defaultLoaderMessage
            || 'Loading...';
    }

    function getLatestTaskMessage() {
        var latestMessage = '';

        taskMessages.forEach(function (message) {
            if (message) {
                latestMessage = message;
            }
        });

        return latestMessage;
    }

    function updateLoaderMessage() {
        var messageElement = getLoaderMessageElement();
        var screenReaderElement = getScreenReaderMessageElement();
        var nextMessage = getLatestTaskMessage() || defaultLoaderMessage || 'Loading...';

        if (messageElement) {
            messageElement.textContent = nextMessage;
        }

        if (screenReaderElement) {
            screenReaderElement.textContent = nextMessage;
        }
    }

    function syncBodyState() {
        var body = getBody();
        if (!body) {
            return;
        }

        var hasPendingTasks = pendingTasks.size > 0;
        body.classList.toggle('inventar-page-loading', hasPendingTasks);
        body.classList.toggle('inventar-page-ready', !hasPendingTasks);

        if (!hasPendingTasks && !bootOverlayReleased) {
            bootOverlayReleased = true;
            bootTrackingEnabled = false;
            body.classList.remove('inventar-navigation-loading');
        }
    }

    function beginTask(taskName, options) {
        if (!taskName) {
            return null;
        }

        if (options && options.bootOnly && (!bootTrackingEnabled || bootOverlayReleased)) {
            return null;
        }

        if (options && options.message) {
            taskMessages.set(taskName, options.message);
        }

        pendingTasks.add(taskName);
        syncBodyState();
        updateLoaderMessage();
        return taskName;
    }

    function finishTask(taskName) {
        if (!taskName || !pendingTasks.has(taskName)) {
            return;
        }

        onNextFrame(function () {
            pendingTasks.delete(taskName);
            taskMessages.delete(taskName);
            syncBodyState();
            updateLoaderMessage();
        });
    }

    function normalizeExportType(exportType) {
        if (typeof exportType !== 'string') {
            return 'file';
        }

        if (/excel/i.test(exportType)) {
            return 'excel';
        }

        if (/pdf/i.test(exportType)) {
            return 'pdf';
        }

        return 'file';
    }

    function resolveExportMessage(exportType) {
        var normalizedType = normalizeExportType(exportType);
        var configuredMessages = window.InventarExportMessages || {};

        if (normalizedType === 'excel' && configuredMessages.excel) {
            return configuredMessages.excel;
        }

        if (normalizedType === 'pdf' && configuredMessages.pdf) {
            return configuredMessages.pdf;
        }

        return configuredMessages.file || defaultLoaderMessage || 'Loading...';
    }

    function beginExport(exportType) {
        var normalizedType = normalizeExportType(exportType);
        var taskName = 'export:' + normalizedType + ':' + Date.now() + ':' + Math.random().toString(36).slice(2);
        return beginTask(taskName, { message: resolveExportMessage(normalizedType) });
    }

    function initializeLoader() {
        if (loaderInitialized) {
            return;
        }

        loaderInitialized = true;
        captureDefaultLoaderMessage();
        updateLoaderMessage();
        beginTask('window-load');

        if (document.readyState === 'complete') {
            finishTask('window-load');
        } else {
            window.addEventListener('load', function () {
                finishTask('window-load');
            }, { once: true });
        }

        if (document.readyState !== 'loading') {
            syncBodyState();
        } else {
            document.addEventListener('DOMContentLoaded', function () {
                syncBodyState();
            }, { once: true });
        }
    }

    function initializeDataTableTracking() {
        if (dataTableTrackingInitialized || !(window.jQuery && $.fn && $.fn.dataTable)) {
            return;
        }

        dataTableTrackingInitialized = true;

        $(document).on('preInit.dt.inventarPageLoader', function (event, settings) {
            if (!settings || settings._inventarPageLoaderTaskName) {
                return;
            }

            var tableId = settings.sTableId || settings.nTable?.id || ('datatable-' + (++dataTableSequence));
            var taskName = beginTask('datatable:' + tableId + ':' + (++dataTableSequence), { bootOnly: true });
            if (!taskName) {
                return;
            }

            settings._inventarPageLoaderTaskName = taskName;
            settings._inventarPageLoaderFailSafe = window.setTimeout(function () {
                finishTask(taskName);
            }, 15000);
        });

        $(document).on('init.dt.inventarPageLoader', function (event, settings) {
            if (!settings || !settings._inventarPageLoaderTaskName) {
                return;
            }

            if (settings._inventarPageLoaderFailSafe) {
                window.clearTimeout(settings._inventarPageLoaderFailSafe);
            }

            finishTask(settings._inventarPageLoaderTaskName);
            delete settings._inventarPageLoaderTaskName;
            delete settings._inventarPageLoaderFailSafe;
        });
    }

    function showForNavigation() {
        var body = getBody();
        if (!body) {
            return;
        }

        bootTrackingEnabled = true;
        bootOverlayReleased = false;
        body.classList.add('inventar-navigation-loading');
        beginTask('navigation');
    }

    window.InventarPageLoader = {
        initialize: initializeLoader,
        initializeDataTableTracking: initializeDataTableTracking,
        beginTask: beginTask,
        finishTask: finishTask,
        showForNavigation: showForNavigation,
        beginExport: beginExport,
        finishExport: finishTask
    };
})();

(function () {
    var fetchTrackingInitialized = false;
    var jqueryTrackingInitialized = false;

    function getRequestUrl(input) {
        if (typeof input === 'string' || input instanceof URL) {
            return input.toString();
        }

        if (input instanceof Request) {
            return input.url;
        }

        return '';
    }

    function isExportRequest(urlValue) {
        if (typeof urlValue !== 'string' || !urlValue.trim()) {
            return false;
        }

        return /(generate|export)[^?#]*(pdf|excel)/i.test(urlValue);
    }

    function getExportType(urlValue) {
        if (!isExportRequest(urlValue)) {
            return null;
        }

        if (/excel/i.test(urlValue)) {
            return 'excel';
        }

        if (/pdf/i.test(urlValue)) {
            return 'pdf';
        }

        return 'file';
    }

    function initializeFetchTracking() {
        if (fetchTrackingInitialized || typeof window.fetch !== 'function') {
            return;
        }

        fetchTrackingInitialized = true;
        var originalFetch = window.fetch.bind(window);

        window.fetch = async function (input, init) {
            var urlValue = getRequestUrl(input);
            var exportType = getExportType(urlValue);
            var taskName = exportType
                ? window.InventarPageLoader?.beginExport(exportType)
                : null;

            try {
                var response = await originalFetch(input, init);

                if (taskName && response && typeof response.clone === 'function') {
                    try {
                        await response.clone().arrayBuffer();
                    } catch {
                        // Ignore secondary read failures and still return the original response.
                    }
                }

                return response;
            } finally {
                if (taskName) {
                    window.InventarPageLoader?.finishExport(taskName);
                }
            }
        };
    }

    function initializeJQueryTracking() {
        if (jqueryTrackingInitialized || !(window.jQuery && $.ajax)) {
            return false;
        }

        jqueryTrackingInitialized = true;

        $(document).ajaxSend(function (event, jqXHR, settings) {
            var exportType = getExportType(settings?.url || '');
            if (!exportType) {
                return;
            }

            jqXHR.__inventarExportLoaderTask = window.InventarPageLoader?.beginExport(exportType) || null;
        });

        $(document).ajaxComplete(function (event, jqXHR) {
            var taskName = jqXHR?.__inventarExportLoaderTask;
            if (!taskName) {
                return;
            }

            window.InventarPageLoader?.finishExport(taskName);
            delete jqXHR.__inventarExportLoaderTask;
        });

        return true;
    }

    function initializeJQueryTrackingWhenReady() {
        if (initializeJQueryTracking()) {
            return;
        }

        var attempts = 0;
        var retryInterval = window.setInterval(function () {
            attempts += 1;

            if (initializeJQueryTracking() || attempts >= 40) {
                window.clearInterval(retryInterval);
            }
        }, 250);
    }

    initializeFetchTracking();

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeJQueryTrackingWhenReady, { once: true });
    } else {
        initializeJQueryTrackingWhenReady();
    }

    window.InventarExportFeedback = {
        initializeJQueryTracking: initializeJQueryTracking,
        isExportRequest: isExportRequest,
        getExportType: getExportType
    };
})();

(function () {
    function stripHtml(value) {
        if (typeof value !== 'string' || value.indexOf('<') === -1) {
            return value ?? '';
        }

        const container = document.createElement('div');
        container.innerHTML = value;
        return container.textContent || container.innerText || '';
    }

    function parseUiNumber(value) {
        if (typeof value === 'number') {
            return Number.isFinite(value) ? value : 0;
        }

        if (typeof value === 'bigint') {
            return Number(value);
        }

        if (value === null || typeof value === 'undefined') {
            return 0;
        }

        let normalized = stripHtml(String(value))
            .replace(/\u00A0/g, ' ')
            .trim();

        if (!normalized) {
            return 0;
        }

        normalized = normalized
            .replace(/[^\d,.\- ]+/g, '')
            .replace(/\s+/g, '');

        if (!normalized) {
            return 0;
        }

        const lastComma = normalized.lastIndexOf(',');
        const lastDot = normalized.lastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0) {
            if (lastComma > lastDot) {
                normalized = normalized.replace(/\./g, '').replace(',', '.');
            } else {
                normalized = normalized.replace(/,/g, '');
            }
        } else if (lastComma >= 0) {
            normalized = normalized.replace(',', '.');
        } else {
            normalized = normalized.replace(/,/g, '');
        }

        const parsed = Number(normalized);
        return Number.isFinite(parsed) ? parsed : 0;
    }

    window.InventarNumber = window.InventarNumber || {};
    window.InventarNumber.parseUiNumber = parseUiNumber;

    function defaultRowMatch(row) {
        return row.dataset.filterMatch !== 'false';
    }

    window.InventarTableLoadMore = {
        init: function (options) {
            const rows = Array.from(document.querySelectorAll(options.rowSelector));
            const button = document.getElementById(options.loadMoreButtonId);
            const pageSize = Number(options.pageSize) > 0 ? Number(options.pageSize) : rows.length;
            const isRowMatched = typeof options.isRowMatched === 'function'
                ? options.isRowMatched
                : defaultRowMatch;

            let visibleLimit = pageSize;

            function refresh(resetLimit) {
                if (resetLimit) {
                    visibleLimit = pageSize;
                }

                const matchedRows = rows.filter(isRowMatched);
                const matchedSet = new Set(matchedRows);

                matchedRows.forEach((row, index) => {
                    row.style.display = index < visibleLimit ? '' : 'none';
                });

                rows.forEach(row => {
                    if (!matchedSet.has(row)) {
                        row.style.display = 'none';
                    }
                });

                if (button) {
                    const hasMore = matchedRows.length > visibleLimit;
                    button.hidden = !hasMore;
                    button.disabled = !hasMore;
                }

                if (typeof options.onAfterRender === 'function') {
                    options.onAfterRender({
                        matchedRows: matchedRows,
                        shownCount: Math.min(visibleLimit, matchedRows.length),
                        totalCount: matchedRows.length
                    });
                }
            }

            if (button) {
                button.addEventListener('click', function () {
                    visibleLimit += pageSize;
                    refresh(false);
                });
            }

            refresh(true);

            return {
                refresh: refresh,
                reset: function () {
                    refresh(true);
                }
            };
        }
    };

    window.InventarSectionLoadMore = {
        init: function (options) {
            const sections = Array.from(document.querySelectorAll(options.sectionSelector));
            const button = document.getElementById(options.loadMoreButtonId);
            const pageSize = Number(options.pageSize) > 0 ? Number(options.pageSize) : sections.length;
            const getSectionRowCount = typeof options.getSectionRowCount === 'function'
                ? options.getSectionRowCount
                : function (section) {
                    return Number(section.dataset.sectionRowCount || 1);
                };
            const isSectionMatched = typeof options.isSectionMatched === 'function'
                ? options.isSectionMatched
                : function (section) {
                    return section.dataset.filterMatch !== 'false';
                };

            let visibleLimit = pageSize;

            function refresh(resetLimit) {
                if (resetLimit) {
                    visibleLimit = pageSize;
                }

                let consumedRows = 0;
                let matchingSectionCount = 0;
                let shownSectionCount = 0;

                sections.forEach(section => {
                    const matches = isSectionMatched(section);
                    if (!matches) {
                        section.style.display = 'none';
                        return;
                    }

                    matchingSectionCount += 1;

                    const sectionRowCount = Math.max(1, Number(getSectionRowCount(section) || 1));
                    const canShowSection = consumedRows < visibleLimit || shownSectionCount === 0;

                    if (canShowSection) {
                        section.style.display = '';
                        consumedRows += sectionRowCount;
                        shownSectionCount += 1;
                    } else {
                        section.style.display = 'none';
                    }
                });

                if (button) {
                    const hasMore = matchingSectionCount > shownSectionCount;
                    button.hidden = !hasMore;
                    button.disabled = !hasMore;
                }

                if (typeof options.onAfterRender === 'function') {
                    options.onAfterRender({
                        shownSectionCount: shownSectionCount,
                        matchingSectionCount: matchingSectionCount
                    });
                }
            }

            if (button) {
                button.addEventListener('click', function () {
                    visibleLimit += pageSize;
                    refresh(false);
                });
            }

            refresh(true);

            return {
                refresh: refresh,
                reset: function () {
                    refresh(true);
                }
            };
        }
    };
})();

(function () {
    function applyMobileInitialScale() {
        const viewport = document.querySelector('meta[name="viewport"]');
        if (!viewport) {
            return;
        }

        const width = window.innerWidth || document.documentElement.clientWidth || window.screen.width || 0;
        let viewportContent = 'width=device-width, initial-scale=1.0';

        if (width <= 480) {
            viewportContent = 'width=device-width, initial-scale=0.76, maximum-scale=5, user-scalable=yes';
        } else if (width <= 768) {
            viewportContent = 'width=device-width, initial-scale=0.82, maximum-scale=5, user-scalable=yes';
        } else if (width <= 1024) {
            viewportContent = 'width=device-width, initial-scale=0.9, maximum-scale=5, user-scalable=yes';
        }

        viewport.setAttribute('content', viewportContent);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', applyMobileInitialScale, { once: true });
    } else {
        applyMobileInitialScale();
    }

    window.addEventListener('orientationchange', function () {
        window.setTimeout(applyMobileInitialScale, 100);
    });
})();

(function () {
    const restorableActionPattern = /\/(details|edit|delete)(\/|$|\?)/i;

    function getSessionStorage() {
        try {
            return window.sessionStorage;
        } catch {
            return null;
        }
    }

    function getDataTableScope() {
        const scope = document.body?.dataset?.datatableStateScope;
        if (!scope || !scope.trim()) {
            return 'anonymous';
        }

        return encodeURIComponent(scope.trim().toLowerCase());
    }

    function getCurrentLocationKey() {
        const path = (window.location.pathname || '').toLowerCase();
        const search = window.location.search || '';
        return `${path}${search}`;
    }

    function getTableStorageKey(settings) {
        const tableId = settings?.nTable?.id || settings?.sTableId || 'datatable';
        return [
            'inventar',
            'datatable-state',
            getDataTableScope(),
            getCurrentLocationKey(),
            tableId
        ].join('::');
    }

    function getTrackedControls(tableElement) {
        if (!tableElement) {
            return [];
        }

        const tracked = [];
        const seenElements = new Set();

        function pushControl(control) {
            if (!(control instanceof HTMLElement) || seenElements.has(control)) {
                return;
            }

            seenElements.add(control);
            tracked.push(control);
        }

        tableElement
            .querySelectorAll('thead tr.filters input, thead tr.filters select, thead tr.filters textarea')
            .forEach(pushControl);

        if (tableElement.id) {
            document
                .querySelectorAll(`[data-datatable-target="${tableElement.id}"]`)
                .forEach(pushControl);
        }

        return tracked.map(function (control, index) {
            const explicitKey = control.dataset?.datatableStateKey;
            const key = explicitKey
                || (control.id ? `id:${control.id}` : null)
                || (control.name ? `name:${control.name}` : null)
                || `index:${index}`;

            return { control, key };
        });
    }

    function readControlValue(control) {
        if (!control || control.disabled) {
            return undefined;
        }

        if (control instanceof HTMLInputElement && (control.type === 'checkbox' || control.type === 'radio')) {
            return control.checked;
        }

        return control.value ?? '';
    }

    function writeControlValue(control, value) {
        if (!control || typeof value === 'undefined') {
            return;
        }

        if (control instanceof HTMLInputElement && (control.type === 'checkbox' || control.type === 'radio')) {
            control.checked = value === true || value === 'true' || value === 1 || value === '1';
            return;
        }

        control.value = value ?? '';
    }

    function captureExtraControlState(tableElement) {
        const values = {};

        getTrackedControls(tableElement).forEach(function (entry) {
            const value = readControlValue(entry.control);
            if (typeof value !== 'undefined') {
                values[entry.key] = value;
            }
        });

        return values;
    }

    function triggerControlRefresh(control) {
        if (!(control instanceof HTMLElement)) {
            return;
        }

        control.dispatchEvent(new Event('input', { bubbles: true }));
        control.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function restoreExtraControlState(tableElement, savedValues) {
        if (!tableElement || !savedValues || typeof savedValues !== 'object') {
            return;
        }

        const restoredControls = [];

        getTrackedControls(tableElement).forEach(function (entry) {
            if (!Object.prototype.hasOwnProperty.call(savedValues, entry.key)) {
                return;
            }

            writeControlValue(entry.control, savedValues[entry.key]);
            restoredControls.push(entry.control);
        });

        if (restoredControls.length === 0) {
            return;
        }

        window.setTimeout(function () {
            restoredControls.forEach(triggerControlRefresh);
        }, 0);
    }

    function getLoadedPageIndex(api, loadedState) {
        if (!loadedState) {
            return 0;
        }

        const start = Number(loadedState.start || 0);
        const length = Number(loadedState.length || api.page.len() || 25);

        if (!Number.isFinite(start) || start <= 0 || !Number.isFinite(length) || length <= 0) {
            return 0;
        }

        return Math.floor(start / length);
    }

    function restorePagingState(api, loadedState) {
        if (!api || !loadedState) {
            return;
        }

        const pageInfo = api.page.info();
        if (!pageInfo) {
            return;
        }

        const desiredPage = getLoadedPageIndex(api, loadedState);
        const lastPage = Math.max(0, (pageInfo.pages || 1) - 1);
        const pageToRestore = Math.min(desiredPage, lastPage);

        if (pageInfo.page !== pageToRestore) {
            api.page(pageToRestore).draw('page');
        }
    }

    function schedulePagingRestore(api, loadedState) {
        [0, 40, 140].forEach(function (delay) {
            window.setTimeout(function () {
                restorePagingState(api, loadedState);
            }, delay);
        });
    }

    function getPendingStatePath(storageKey) {
        return `${storageKey}::pending`;
    }

    function cloneState(value) {
        if (!value || typeof value !== 'object') {
            return value ?? null;
        }

        try {
            return JSON.parse(JSON.stringify(value));
        } catch {
            return null;
        }
    }

    function getNormalizedPathname(urlValue) {
        if (!urlValue) {
            return '';
        }

        try {
            const parsedUrl = new URL(urlValue, window.location.origin);
            return (parsedUrl.pathname || '').toLowerCase();
        } catch {
            return '';
        }
    }

    function getReferrerPathname() {
        try {
            if (!document.referrer) {
                return '';
            }

            const parsedUrl = new URL(document.referrer);
            if (parsedUrl.origin !== window.location.origin) {
                return '';
            }

            return (parsedUrl.pathname || '').toLowerCase();
        } catch {
            return '';
        }
    }

    function isRestorableActionUrl(urlValue) {
        const pathname = getNormalizedPathname(urlValue);
        return pathname ? restorableActionPattern.test(pathname) : false;
    }

    function getOwningDataTableElement(element) {
        const tableElement = element?.closest('table');
        if (!tableElement || !(window.jQuery && $.fn && $.fn.dataTable)) {
            return null;
        }

        if (!$.fn.dataTable.isDataTable(tableElement)) {
            return null;
        }

        return tableElement;
    }

    function getCurrentStateFromSettings(settings) {
        if (!settings) {
            return null;
        }

        return cloneState(settings._inventarCurrentState) || null;
    }

    function persistPendingState(settings, state, expectedReferrerPath) {
        const storage = getSessionStorage();
        if (!storage) {
            return;
        }

        try {
            const payload = {
                state: state,
                expectedReferrerPath: expectedReferrerPath || '',
                savedAtUtc: new Date().toISOString()
            };

            storage.setItem(getPendingStatePath(getTableStorageKey(settings)), JSON.stringify(payload));
        } catch {
            // Ignore private mode/quota failures and fall back gracefully.
        }
    }

    function loadPendingState(settings) {
        const storage = getSessionStorage();
        if (!storage) {
            return null;
        }

        try {
            const pendingPath = getPendingStatePath(getTableStorageKey(settings));
            const rawState = storage.getItem(pendingPath);
            if (!rawState) {
                return null;
            }

            const payload = JSON.parse(rawState);
            const expectedReferrerPath = (payload?.expectedReferrerPath || '').toLowerCase();
            const actualReferrerPath = getReferrerPathname();

            storage.removeItem(pendingPath);

            if (!payload?.state || !expectedReferrerPath || actualReferrerPath !== expectedReferrerPath) {
                return null;
            }

            return payload.state;
        } catch {
            return null;
        }
    }

    function restoreColumnFilterInputs(tableElement, loadedState) {
        if (!tableElement || !loadedState?.columns) {
            return;
        }

        const controls = tableElement.querySelectorAll('thead tr.filters input, thead tr.filters select, thead tr.filters textarea');
        controls.forEach(function (control) {
            if (!(control instanceof HTMLElement) || control instanceof HTMLInputElement && (control.type === 'checkbox' || control.type === 'radio')) {
                return;
            }

            const explicitIndex = control.dataset?.columnIndex;
            const columnIndex = explicitIndex
                ? Number(explicitIndex)
                : Array.from(control.closest('tr')?.children || []).indexOf(control.closest('th'));

            if (!Number.isInteger(columnIndex) || columnIndex < 0) {
                return;
            }

            const columnState = loadedState.columns[columnIndex];
            const searchValue = columnState?.search?.search ?? '';
            if (typeof searchValue !== 'string') {
                return;
            }

            control.value = searchValue;
        });
    }

    window.InventarDataTableState = {
        initialize: function () {
            if (window.__inventarDataTableStateInitialized) {
                return;
            }

            if (!(window.jQuery && $.fn && $.fn.dataTable)) {
                return;
            }

            window.__inventarDataTableStateInitialized = true;

            $.extend(true, $.fn.dataTable.defaults, {
                pageLength: 25,
                stateSave: true,
                stateDuration: -1,
                stateSaveCallback: function (settings, data) {
                    settings._inventarCurrentState = cloneState(data);
                },
                stateLoadCallback: function (settings) {
                    const loadedState = loadPendingState(settings);
                    if (loadedState) {
                        settings._inventarCurrentState = cloneState(loadedState);
                    }

                    return loadedState;
                }
            });

            $(document).on('stateSaveParams.dt.inventarState', function (event, settings, data) {
                if (!settings?.nTable || !data) {
                    return;
                }

                data._inventarExtraControls = captureExtraControlState(settings.nTable);
                settings._inventarCurrentState = cloneState(data);
            });

            $(document).on('init.dt.inventarState', function (event, settings) {
                if (!settings?.nTable) {
                    return;
                }

                const api = new $.fn.dataTable.Api(settings);
                const loadedState = api.state.loaded();

                if (!loadedState) {
                    return;
                }

                if (loadedState._inventarExtraControls) {
                    restoreExtraControlState(settings.nTable, loadedState._inventarExtraControls);
                }
                restoreColumnFilterInputs(settings.nTable, loadedState);

                schedulePagingRestore(api, loadedState);
            });

            $(document).on('click.inventarState', 'table a[href]', function () {
                const href = this.getAttribute('href');
                if (!isRestorableActionUrl(href)) {
                    return;
                }

                const tableElement = getOwningDataTableElement(this);
                if (!tableElement) {
                    return;
                }

                const api = $(tableElement).DataTable();
                if (!api) {
                    return;
                }

                if (api.state && typeof api.state.save === 'function') {
                    api.state.save();
                }

                const state = getCurrentStateFromSettings(api.settings()[0]);
                if (!state) {
                    return;
                }

                state._inventarExtraControls = captureExtraControlState(tableElement);
                persistPendingState(api.settings()[0], state, getNormalizedPathname(href));
            });

            $(document).on('submit.inventarState', 'table form[action]', function () {
                const action = this.getAttribute('action');
                if (!isRestorableActionUrl(action)) {
                    return;
                }

                const tableElement = getOwningDataTableElement(this);
                if (!tableElement) {
                    return;
                }

                const api = $(tableElement).DataTable();
                if (!api) {
                    return;
                }

                if (api.state && typeof api.state.save === 'function') {
                    api.state.save();
                }

                const state = getCurrentStateFromSettings(api.settings()[0]);
                if (!state) {
                    return;
                }

                state._inventarExtraControls = captureExtraControlState(tableElement);
                persistPendingState(api.settings()[0], state, getNormalizedPathname(action));
            });
        }
    };
})();
