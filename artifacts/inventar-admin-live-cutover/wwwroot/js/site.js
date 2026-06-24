// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
(function () {
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
