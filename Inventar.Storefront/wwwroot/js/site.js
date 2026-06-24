(() => {
  const root = document.documentElement;
  const body = document.body;
  const siteChrome = document.querySelector("[data-site-chrome]");
  const navToggle = document.querySelector("[data-nav-toggle]");
  const navDrawer = document.querySelector("[data-nav-drawer]");
  const navBackdrop = document.querySelector("[data-nav-backdrop]");
  const navCloseButtons = document.querySelectorAll("[data-nav-close]");
  const notificationCloseButtons = document.querySelectorAll("[data-cart-notification-close]");
  const dropdownRoots = Array.from(document.querySelectorAll("[data-dropdown-root]"));
  const headerSearchRoot = document.querySelector("[data-header-search-root]");
  const headerSearchToggle = headerSearchRoot?.querySelector("[data-header-search-toggle]");
  const headerSearchPanel = headerSearchRoot?.querySelector("[data-header-search-panel]");
  const headerSearchForm = headerSearchRoot?.querySelector("[data-header-search-form]");
  const headerSearchInput = headerSearchRoot?.querySelector("[data-header-search-input]");
  const headerSearchClose = headerSearchRoot?.querySelector("[data-header-search-close]");
  const headerSearchResults = headerSearchRoot?.querySelector("[data-header-search-results]");
  const catalogFiltersForm = document.querySelector("[data-catalog-filters]");
  const categoryPicker = catalogFiltersForm?.querySelector("[data-filter-category-picker]");
  const catalogToolbar = document.querySelector("[data-catalog-toolbar]");
  const catalogPanelToggle = catalogToolbar?.querySelector("[data-catalog-panel-toggle]");
  const catalogPanelCloseButtons = catalogToolbar?.querySelectorAll("[data-catalog-panel-close]") ?? [];
  const catalogPanelBackdrop = catalogToolbar?.querySelector("[data-catalog-panel-backdrop]");

  let lastScrollY = window.scrollY;
  let navOpen = false;
  let searchOpen = false;
  let catalogPanelOpen = false;
  let ticking = false;
  let predictiveSearchDebounceId = 0;
  let predictiveSearchSequence = 0;
  let activePredictiveSearchController = null;
  const supportsHoverMenus = window.matchMedia("(hover: hover) and (pointer: fine)").matches;

  function updateChromeHeight() {
    if (!siteChrome) {
      return;
    }

    root.style.setProperty("--site-chrome-height", `${siteChrome.offsetHeight}px`);
  }

  function showChrome() {
    body.classList.remove("site-chrome-hidden");
  }

  function hideChrome() {
    if (window.scrollY > 32 && !navOpen && !searchOpen) {
      body.classList.add("site-chrome-hidden");
    }
  }

  function applyScrollState() {
    const currentScrollY = window.scrollY;
    const delta = currentScrollY - lastScrollY;

    if (navOpen) {
      showChrome();
      lastScrollY = currentScrollY;
      ticking = false;
      return;
    }

    if (currentScrollY <= 16) {
      showChrome();
    } else if (delta > 8) {
      hideChrome();
    } else if (delta < -4) {
      showChrome();
    }

    lastScrollY = currentScrollY;
    ticking = false;
  }

  function requestScrollStateUpdate() {
    if (ticking) {
      return;
    }

    window.requestAnimationFrame(applyScrollState);
    ticking = true;
  }

  function setNavOpen(nextState) {
    navOpen = nextState;
    body.classList.toggle("nav-open", nextState);
    showChrome();

    if (nextState) {
      setSearchOpen(false);
    }

    if (navToggle) {
      navToggle.setAttribute("aria-expanded", String(nextState));
    }

    if (navDrawer) {
      navDrawer.setAttribute("aria-hidden", String(!nextState));
    }
  }

  function closeCartNotification() {
    body.classList.remove("cart-notification-open");
  }

  function setCatalogPanelOpen(nextState) {
    if (!catalogToolbar || !catalogPanelBackdrop) {
      return;
    }

    catalogPanelOpen = nextState;
    catalogToolbar.classList.toggle("is-panel-open", nextState);
    body.classList.toggle("catalog-toolbar-open", nextState);
    catalogPanelBackdrop.hidden = !nextState;

    if (catalogPanelToggle instanceof HTMLButtonElement) {
      catalogPanelToggle.setAttribute("aria-expanded", String(nextState));
    }
  }

  function createElement(tagName, className, text) {
    const element = document.createElement(tagName);
    if (className) {
      element.className = className;
    }
    if (typeof text === "string") {
      element.textContent = text;
    }

    return element;
  }

  function renderPredictiveSearchResults(result) {
    if (!(headerSearchResults instanceof HTMLElement)) {
      return;
    }

    headerSearchResults.innerHTML = "";

    const wrapper = createElement("div", "predictive-search");
    const suggestions = Array.isArray(result?.suggestions) ? result.suggestions : [];
    const products = Array.isArray(result?.products) ? result.products : [];
    const query = typeof result?.query === "string" ? result.query.trim() : "";

    if (suggestions.length > 0) {
      const suggestionsSection = createElement("section", "predictive-search__section");
      suggestionsSection.appendChild(createElement("h3", "predictive-search__title", "Prijedlozi"));

      const suggestionsList = createElement("div", "predictive-search__suggestions");
      suggestions.forEach((suggestion) => {
        const suggestionLink = createElement("a", "predictive-search__suggestion");
        suggestionLink.href = suggestion.url;
        suggestionLink.textContent = suggestion.label;
        suggestionsList.appendChild(suggestionLink);
      });

      suggestionsSection.appendChild(suggestionsList);
      wrapper.appendChild(suggestionsSection);
    }

    const productsSection = createElement("section", "predictive-search__section");
    productsSection.appendChild(createElement("h3", "predictive-search__title", "Proizvodi"));

    if (products.length > 0) {
      const productsList = createElement("div", "predictive-search__products");

      products.forEach((product) => {
        const productLink = createElement("a", "predictive-search__product");
        productLink.href = product.url;

        if (product.imageUrl) {
          const image = document.createElement("img");
          image.className = "predictive-search__product-image";
          image.src = product.imageUrl;
          image.alt = product.shortDescription || "Proizvod";
          image.loading = "lazy";
          productLink.appendChild(image);
        } else {
          productLink.appendChild(createElement("div", "predictive-search__product-image predictive-search__product-image--placeholder", "Bez fotografije"));
        }

        const content = createElement("div", "predictive-search__product-content");
        content.appendChild(createElement("strong", "predictive-search__product-description", product.shortDescription));
        if (product.price) {
          content.appendChild(createElement("span", "predictive-search__product-price", product.price));
        }

        productLink.appendChild(content);
        productsList.appendChild(productLink);
      });

      productsSection.appendChild(productsList);
    } else {
      productsSection.appendChild(createElement(
        "p",
        "predictive-search__empty",
        query ? "Nema proizvoda za trenutnu pretragu." : "Pocnite da kucate kako biste vidjeli proizvode."));
    }

    wrapper.appendChild(productsSection);

    if (result?.resultsUrl) {
      const footer = createElement("div", "predictive-search__footer");
      const footerLink = createElement(
        "a",
        "predictive-search__footer-link",
        query ? `Prikazi sve rezultate za "${query}"` : "Pogledaj sve proizvode");
      footerLink.href = result.resultsUrl;
      footer.appendChild(footerLink);
      wrapper.appendChild(footer);
    }

    headerSearchResults.appendChild(wrapper);
    headerSearchResults.hidden = false;
    updateChromeHeight();
  }

  function renderPredictiveSearchLoading() {
    if (!(headerSearchResults instanceof HTMLElement)) {
      return;
    }

    headerSearchResults.innerHTML = "";
    headerSearchResults.appendChild(createElement("div", "predictive-search__loading", "Pretrazujem..."));
    headerSearchResults.hidden = false;
    updateChromeHeight();
  }

  function renderPredictiveSearchError() {
    if (!(headerSearchResults instanceof HTMLElement)) {
      return;
    }

    headerSearchResults.innerHTML = "";
    headerSearchResults.appendChild(createElement("div", "predictive-search__empty", "Trenutno nije moguce prikazati rezultate pretrage."));
    headerSearchResults.hidden = false;
    updateChromeHeight();
  }

  async function fetchPredictiveSearchResults() {
    if (!(headerSearchRoot instanceof HTMLElement) || !(headerSearchInput instanceof HTMLInputElement)) {
      return;
    }

    const predictiveSearchUrl = headerSearchRoot.dataset.predictiveSearchUrl;
    if (!predictiveSearchUrl) {
      return;
    }

    predictiveSearchSequence += 1;
    const requestSequence = predictiveSearchSequence;

    if (activePredictiveSearchController) {
      activePredictiveSearchController.abort();
    }

    activePredictiveSearchController = new AbortController();
    const url = new URL(predictiveSearchUrl, window.location.origin);
    const query = headerSearchInput.value.trim();
    if (query) {
      url.searchParams.set("q", query);
    }

    renderPredictiveSearchLoading();

    try {
      const response = await fetch(url.toString(), {
        headers: {
          "X-Requested-With": "XMLHttpRequest"
        },
        signal: activePredictiveSearchController.signal
      });

      if (!response.ok) {
        throw new Error("Predictive search failed.");
      }

      const result = await response.json();
      if (requestSequence !== predictiveSearchSequence) {
        return;
      }

      renderPredictiveSearchResults(result);
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") {
        return;
      }

      if (requestSequence !== predictiveSearchSequence) {
        return;
      }

      renderPredictiveSearchError();
    } finally {
      if (requestSequence === predictiveSearchSequence) {
        activePredictiveSearchController = null;
      }
    }
  }

  function queuePredictiveSearch() {
    window.clearTimeout(predictiveSearchDebounceId);
    predictiveSearchDebounceId = window.setTimeout(() => {
      fetchPredictiveSearchResults();
    }, 160);
  }

  function setSearchOpen(nextState) {
    if (!(headerSearchPanel instanceof HTMLElement)) {
      return;
    }

    searchOpen = nextState;
    body.classList.toggle("header-search-open", nextState);
    headerSearchPanel.hidden = !nextState;
    showChrome();

    if (nextState) {
      setNavOpen(false);
      closeAllDropdowns();
      if (headerSearchInput instanceof HTMLInputElement) {
        window.requestAnimationFrame(() => {
          headerSearchInput.focus();
          headerSearchInput.select();
        });
      }

      queuePredictiveSearch();
    } else {
      window.clearTimeout(predictiveSearchDebounceId);
      if (activePredictiveSearchController) {
        activePredictiveSearchController.abort();
        activePredictiveSearchController = null;
      }

      if (headerSearchResults instanceof HTMLElement) {
        headerSearchResults.hidden = true;
      }
    }

    updateChromeHeight();
  }

  function setSubmenuState(group, isOpen) {
    if (!group) {
      return;
    }

    const toggle = group.querySelector("[data-submenu-toggle]");
    const submenu = group.querySelector("[data-submenu]");

    group.classList.toggle("is-open", isOpen);

    if (toggle) {
      toggle.setAttribute("aria-expanded", String(isOpen));
    }

    if (submenu) {
      submenu.hidden = !isOpen;
    }
  }

  function closeDropdown(rootElement) {
    if (!rootElement) {
      return;
    }

    const toggle = rootElement.querySelector("[data-dropdown-toggle]");
    const panel = rootElement.querySelector("[data-dropdown-panel]");

    rootElement.classList.remove("is-open");

    if (toggle) {
      toggle.setAttribute("aria-expanded", "false");
    }

    if (panel) {
      panel.hidden = true;
    }

    rootElement.querySelectorAll("[data-submenu-group]").forEach((group) => {
      setSubmenuState(group, false);
    });
  }

  function closeAllDropdowns(exceptRoot = null) {
    dropdownRoots.forEach((rootElement) => {
      if (rootElement !== exceptRoot) {
        closeDropdown(rootElement);
      }
    });
  }

  function openDropdown(rootElement) {
    if (!rootElement) {
      return;
    }

    closeAllDropdowns(rootElement);

    const toggle = rootElement.querySelector("[data-dropdown-toggle]");
    const panel = rootElement.querySelector("[data-dropdown-panel]");

    rootElement.classList.add("is-open");

    if (toggle) {
      toggle.setAttribute("aria-expanded", "true");
    }

    if (panel) {
      panel.hidden = false;
    }
  }

  function toggleDropdown(rootElement) {
    if (!rootElement) {
      return;
    }

    if (rootElement.classList.contains("is-open")) {
      closeDropdown(rootElement);
      return;
    }

    openDropdown(rootElement);
  }

  function submitCatalogFilters() {
    if (!catalogFiltersForm) {
      return;
    }

    if (catalogPanelOpen) {
      setCatalogPanelOpen(false);
    }

    catalogFiltersForm.requestSubmit();
  }

  function normalizeTextValue(value) {
    return typeof value === "string" ? value.trim().toLowerCase() : "";
  }

  function matchesTextValue(left, right) {
    return normalizeTextValue(left) === normalizeTextValue(right);
  }

  function matchesExactValue(left, right) {
    return typeof left === "string" &&
      typeof right === "string" &&
      left.trim() === right.trim();
  }

  function formatCurrency(value) {
    const numericValue = Number(value);
    return Number.isFinite(numericValue) ? `${numericValue.toFixed(2)} \u20AC` : "";
  }

  function initializeDropdowns() {
    dropdownRoots.forEach((rootElement) => {
      const toggle = rootElement.querySelector("[data-dropdown-toggle]");

      toggle?.addEventListener("click", (event) => {
        event.preventDefault();
        event.stopPropagation();
        toggleDropdown(rootElement);
      });

      rootElement.querySelectorAll("[data-submenu-toggle]").forEach((button) => {
        button.addEventListener("click", (event) => {
          event.preventDefault();
          event.stopPropagation();

          const group = button.closest("[data-submenu-group]");
          if (!group) {
            return;
          }

          const shouldOpen = !group.classList.contains("is-open");

          rootElement.querySelectorAll("[data-submenu-group]").forEach((otherGroup) => {
            if (otherGroup !== group) {
              setSubmenuState(otherGroup, false);
            }
          });

          setSubmenuState(group, shouldOpen);
        });
      });

      if (supportsHoverMenus) {
        rootElement.querySelectorAll("[data-submenu-group]").forEach((group) => {
          group.addEventListener("mouseenter", () => {
            rootElement.querySelectorAll("[data-submenu-group]").forEach((otherGroup) => {
              setSubmenuState(otherGroup, otherGroup === group);
            });
          });
        });

        rootElement.addEventListener("mouseleave", () => {
          rootElement.querySelectorAll("[data-submenu-group]").forEach((group) => {
            if (!group.querySelector(".filter-category-picker__option.is-selected") &&
                !group.querySelector(".category-menu__narrower.is-selected")) {
              setSubmenuState(group, false);
            }
          });
        });
      }
    });

    document.addEventListener("click", (event) => {
      const target = event.target;

      if (!(target instanceof Element)) {
        closeAllDropdowns();
        return;
      }

      const clickedInsideDropdown = dropdownRoots.some((rootElement) => rootElement.contains(target));
      if (!clickedInsideDropdown) {
        closeAllDropdowns();
      }
    });
  }

  function initializeCategoryPicker() {
    if (!categoryPicker || !catalogFiltersForm) {
      return;
    }

    const broaderInput = catalogFiltersForm.querySelector("[data-category-broader]");
    const narrowerInput = catalogFiltersForm.querySelector("[data-category-narrower]");
    const label = categoryPicker.querySelector("[data-category-label]");

    categoryPicker.querySelectorAll("[data-category-option]").forEach((option) => {
      option.addEventListener("click", () => {
        if (!(option instanceof HTMLElement)) {
          return;
        }

        if (broaderInput instanceof HTMLInputElement) {
          broaderInput.value = option.dataset.broaderCategory ?? "";
        }

        if (narrowerInput instanceof HTMLInputElement) {
          narrowerInput.value = option.dataset.narrowerCategory ?? "";
        }

        if (label) {
          label.textContent = option.dataset.categoryLabel ?? option.textContent?.trim() ?? "Sve kategorije";
        }

        categoryPicker.querySelectorAll("[data-category-option]").forEach((otherOption) => {
          otherOption.classList.toggle("is-selected", otherOption === option);
        });

        closeDropdown(categoryPicker);
        submitCatalogFilters();
      });
    });

    categoryPicker.querySelectorAll("[data-submenu-group]").forEach((group) => {
      if (group.querySelector(".filter-category-picker__option.is-selected")) {
        setSubmenuState(group, true);
      }
    });
  }

  function initializeCatalogFilters() {
    if (!catalogFiltersForm) {
      return;
    }

    catalogFiltersForm.querySelectorAll("[data-auto-submit]").forEach((control) => {
      control.addEventListener("change", submitCatalogFilters);
    });
  }

  function initializeHeaderSearch() {
    if (!(headerSearchRoot instanceof HTMLElement)) {
      return;
    }

    headerSearchToggle?.addEventListener("click", () => {
      setSearchOpen(true);
    });

    headerSearchClose?.addEventListener("click", () => {
      setSearchOpen(false);
    });

    headerSearchInput?.addEventListener("input", () => {
      queuePredictiveSearch();
    });

    headerSearchForm?.addEventListener("submit", () => {
      setSearchOpen(false);
    });
  }

  function initializeSearchClearButtons() {
    document.querySelectorAll("[data-search-clear]").forEach((button) => {
      if (!(button instanceof HTMLButtonElement)) {
        return;
      }

      const container = button.parentElement;
      const input = container?.querySelector('input[type="search"]');
      if (!(input instanceof HTMLInputElement)) {
        return;
      }

      function syncVisibility() {
        button.hidden = input.value.trim().length === 0;
      }

      syncVisibility();

      input.addEventListener("input", syncVisibility);
      input.addEventListener("change", syncVisibility);

      button.addEventListener("click", () => {
        input.value = "";
        syncVisibility();
        input.focus();

        if (input.hasAttribute("data-header-search-input")) {
          queuePredictiveSearch();
          return;
        }

        const form = button.closest("form");
        if (form instanceof HTMLFormElement) {
          const actionUrl = new URL(form.action || window.location.href, window.location.origin);
          const formData = new FormData(form);

          actionUrl.search = "";

          formData.forEach((value, key) => {
            const normalizedValue = typeof value === "string" ? value.trim() : "";
            if (!normalizedValue) {
              return;
            }

            actionUrl.searchParams.append(key, normalizedValue);
          });

          window.location.assign(actionUrl.toString());
        }
      });
    });
  }

  function initializeCatalogToolbar() {
    if (!catalogToolbar) {
      return;
    }

    catalogPanelToggle?.addEventListener("click", () => {
      setCatalogPanelOpen(!catalogPanelOpen);
    });

    catalogPanelBackdrop?.addEventListener("click", () => {
      setCatalogPanelOpen(false);
    });

    catalogPanelCloseButtons.forEach((button) => {
      button.addEventListener("click", () => {
        setCatalogPanelOpen(false);
      });
    });
  }

  function initializeProductGalleries() {
    const galleries = document.querySelectorAll("[data-product-gallery]");

    galleries.forEach((gallery) => {
      const thumbs = Array.from(gallery.querySelectorAll("[data-gallery-thumb]"));
      const mainMedia = gallery.querySelector("[data-gallery-main-media]");
      const countLabel = gallery.querySelector("[data-gallery-count]");
      const openButtons = gallery.querySelectorAll("[data-gallery-open]");
      const modal = gallery.querySelector("[data-gallery-modal]");
      const modalMedia = gallery.querySelector("[data-gallery-modal-media]");
      const modalCount = gallery.querySelector("[data-gallery-modal-count]");
      const closeButtons = gallery.querySelectorAll("[data-gallery-close]");
      const prevButtons = gallery.querySelectorAll("[data-gallery-prev]");
      const nextButtons = gallery.querySelectorAll("[data-gallery-next]");

      if (!(mainMedia instanceof HTMLElement) || thumbs.length === 0) {
        return;
      }

      let activeIndex = thumbs.findIndex((thumb) => thumb.classList.contains("is-active"));
      if (activeIndex < 0) {
        activeIndex = 0;
      }

      function buildMediaElement(mediaType, mediaUrl, mediaAlt, isModal) {
        if (matchesTextValue(mediaType, "video")) {
          const video = document.createElement("video");
          video.className = isModal ? "product-gallery-modal__video" : "product-details__main-video";
          video.controls = true;
          video.playsInline = true;
          video.preload = "metadata";
          video.src = mediaUrl;
          video.setAttribute("aria-label", mediaAlt || "Video proizvoda");
          return video;
        }

        const image = document.createElement("img");
        image.className = isModal ? "product-gallery-modal__image" : "product-details__main-image";
        image.src = mediaUrl;
        image.alt = mediaAlt;

        if (!isModal) {
          image.dataset.galleryMain = "";
        }

        return image;
      }

      function renderMedia(container, mediaType, mediaUrl, mediaAlt, isModal) {
        if (!(container instanceof HTMLElement)) {
          return;
        }

        container.innerHTML = "";
        container.appendChild(buildMediaElement(mediaType, mediaUrl, mediaAlt, isModal));
      }

      function renderGallery(index) {
        activeIndex = (index + thumbs.length) % thumbs.length;

        thumbs.forEach((thumb, thumbIndex) => {
          const isActive = thumbIndex === activeIndex;
          thumb.classList.toggle("is-active", isActive);
          thumb.setAttribute("aria-current", isActive ? "true" : "false");
        });

        const activeThumb = thumbs[activeIndex];
        const mediaUrl = activeThumb.dataset.mediaUrl ?? "";
        const mediaType = activeThumb.dataset.mediaType ?? "image";
        const imageAlt = activeThumb.dataset.mediaAlt ?? "";
        const countText = `${activeIndex + 1} / ${thumbs.length}`;

        renderMedia(mainMedia, mediaType, mediaUrl, imageAlt, false);

        if (countLabel) {
          countLabel.textContent = countText;
        }

        renderMedia(modalMedia, mediaType, mediaUrl, imageAlt, true);

        if (modalCount) {
          modalCount.textContent = countText;
        }
      }

      function setActiveImageByUrl(imageUrl) {
        if (!imageUrl) {
          return;
        }

        const nextIndex = thumbs.findIndex((thumb) => matchesExactValue(thumb.dataset.mediaUrl, imageUrl));
        if (nextIndex >= 0) {
          renderGallery(nextIndex);
        }
      }

      function openModal() {
        if (!modal) {
          return;
        }

        modal.hidden = false;
        body.classList.add("gallery-modal-open");
        renderGallery(activeIndex);
      }

      function closeModal() {
        if (!modal) {
          return;
        }

        modal.querySelectorAll("video").forEach((video) => {
          if (video instanceof HTMLVideoElement) {
            video.pause();
          }
        });

        modal.hidden = true;
        body.classList.remove("gallery-modal-open");
      }

      thumbs.forEach((thumb, index) => {
        thumb.addEventListener("click", () => {
          renderGallery(index);
        });
      });

      openButtons.forEach((button) => {
        button.addEventListener("click", () => {
          openModal();
        });
      });

      closeButtons.forEach((button) => {
        button.addEventListener("click", () => {
          closeModal();
        });
      });

      prevButtons.forEach((button) => {
        button.addEventListener("click", () => {
          renderGallery(activeIndex - 1);
        });
      });

      nextButtons.forEach((button) => {
        button.addEventListener("click", () => {
          renderGallery(activeIndex + 1);
        });
      });

      modal?.addEventListener("click", (event) => {
        const target = event.target;

        if (target instanceof Element && target.matches("[data-gallery-close]")) {
          closeModal();
        }
      });

      mainMedia.addEventListener("click", (event) => {
        if (event.target instanceof Element && event.target.closest("video")) {
          event.stopPropagation();
        }
      });

      openButtons.forEach((button) => {
        button.addEventListener("keydown", (event) => {
          if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            openModal();
          }
        });
      });

      document.addEventListener("keydown", (event) => {
        if (modal?.hidden !== false) {
          return;
        }

        if (event.key === "ArrowLeft") {
          renderGallery(activeIndex - 1);
        } else if (event.key === "ArrowRight") {
          renderGallery(activeIndex + 1);
        } else if (event.key === "Escape") {
          closeModal();
        }
      });

      gallery.setActiveImageByUrl = setActiveImageByUrl;
      renderGallery(activeIndex);
    });
  }

  function initializeVariantPickers() {
    const pickers = document.querySelectorAll("[data-variant-picker]");

    pickers.forEach((picker) => {
      const dataElement = picker.querySelector("[data-variant-data]");
      if (!(dataElement instanceof HTMLScriptElement)) {
        return;
      }

      let variants;

      try {
        variants = JSON.parse(dataElement.textContent ?? "[]");
      } catch {
        return;
      }

      if (!Array.isArray(variants) || variants.length === 0) {
        return;
      }

      const colorSelect = picker.querySelector('[data-variant-select="color"]');
      const sizeSelect = picker.querySelector('[data-variant-select="size"]');
      const quantityInput = picker.querySelector("[data-variant-quantity]");
      const decrementButton = picker.querySelector("[data-details-decrement]");
      const incrementButton = picker.querySelector("[data-details-increment]");
      const customLengthInput = picker.querySelector("[data-po-mjeri-length]");
      const previewUrl = picker.dataset.previewUrl ?? "";
      const isPoMjeri = picker.dataset.isPoMjeri === "true";
      const productIdInput = picker.querySelector("[data-selected-product-id]");
      const selectedQuantityInput = picker.querySelector("[data-selected-quantity]");
      const selectedColorInput = picker.querySelector("[data-selected-color-input]");
      const selectedCustomWidthInput = picker.querySelector("[data-selected-custom-width]");
      const selectedCustomLengthInput = picker.querySelector("[data-selected-custom-length]");
      const priceElement = picker.querySelector("[data-variant-price]");
      const comparePriceElement = picker.querySelector("[data-variant-compare-price]");
      const priceNoteElement = picker.querySelector("[data-variant-price-note]");
      const availabilityElement = picker.querySelector("[data-variant-availability]");
      const submitButton = picker.querySelector("[data-variant-submit]");
      const selectedColorElement = picker.querySelector("[data-selected-color]");
      const selectedSizeElement = picker.querySelector("[data-selected-size]");
      const quantityFeedbackElement = picker.querySelector("[data-quantity-feedback]");
      const galleryElement = picker.closest(".product-details")?.querySelector("[data-product-gallery]");
      const soldOutOverlay = picker.closest(".product-details")?.querySelector("[data-product-sold-out-overlay]");
      let previewDebounceId = 0;
      let fieldDebounceId = 0;
      let previewRequestSequence = 0;
      let activePreviewController = null;
      let lastRenderedPrice = priceElement instanceof HTMLElement ? priceElement.textContent ?? "" : "";
      let lastRenderedComparePrice = comparePriceElement instanceof HTMLElement ? comparePriceElement.textContent ?? "" : "";
      let lastRenderedCompareHidden = comparePriceElement instanceof HTMLElement ? comparePriceElement.hidden : true;
      let lastRenderedPriceNote = priceNoteElement instanceof HTMLElement ? priceNoteElement.textContent ?? "" : "";
      let lastRenderedPriceNoteHidden = priceNoteElement instanceof HTMLElement ? priceNoteElement.hidden : true;
      let lastAppliedVariantKey = "";
      let lastQuantityFeedbackMessage =
        quantityFeedbackElement instanceof HTMLElement ? quantityFeedbackElement.textContent ?? "" : "";
      let lastAvailabilityMessage =
        availabilityElement instanceof HTMLElement ? availabilityElement.textContent?.trim() ?? "" : "";
      let lastAvailabilityHidden =
        availabilityElement instanceof HTMLElement ? availabilityElement.hidden : true;
      let lastAvailabilityUnavailable =
        availabilityElement instanceof HTMLElement ? availabilityElement.classList.contains("is-unavailable") : false;

      function buildQuantityLimitMessage(maxQuantity) {
        if (!Number.isFinite(maxQuantity) || maxQuantity <= 0) {
          return "Odabrani proizvod trenutno nije dostupan.";
        }

        return `Mogu\u0107e je naru\u010Diti najvi\u0161e ${maxQuantity} komada za dati proizvod!`;
      }

      function showQuantityFeedback(message) {
        if (!(quantityFeedbackElement instanceof HTMLElement)) {
          return;
        }

        const normalizedMessage = typeof message === "string" ? message.trim() : "";

        if (!normalizedMessage) {
          if (quantityFeedbackElement.hidden && lastQuantityFeedbackMessage.length === 0) {
            return;
          }

          quantityFeedbackElement.hidden = true;
          quantityFeedbackElement.textContent = "";
          lastQuantityFeedbackMessage = "";
          return;
        }

        if (!quantityFeedbackElement.hidden && lastQuantityFeedbackMessage === normalizedMessage) {
          return;
        }

        quantityFeedbackElement.hidden = false;
        quantityFeedbackElement.textContent = normalizedMessage;
        lastQuantityFeedbackMessage = normalizedMessage;
      }

      function setAvailabilityState(message, isUnavailable) {
        if (!(availabilityElement instanceof HTMLElement)) {
          return;
        }

        const normalizedMessage = typeof message === "string" ? message.trim() : "";
        const hidden = normalizedMessage.length === 0;
        const unavailable = Boolean(isUnavailable);

        if (
          lastAvailabilityMessage === normalizedMessage &&
          lastAvailabilityHidden === hidden &&
          lastAvailabilityUnavailable === unavailable
        ) {
          return;
        }

        availabilityElement.textContent = normalizedMessage;
        availabilityElement.hidden = hidden;
        availabilityElement.classList.toggle("is-unavailable", unavailable);
        lastAvailabilityMessage = normalizedMessage;
        lastAvailabilityHidden = hidden;
        lastAvailabilityUnavailable = unavailable;
      }

      function queueVariantSelectionSync(delay = 220) {
        window.clearTimeout(fieldDebounceId);
        fieldDebounceId = window.setTimeout(() => {
          syncVariantSelection();
        }, delay);
      }

      function sortVariantMatches(matches) {
        return matches
          .slice()
          .sort((left, right) =>
            Number(right.availableQuantity > 0) - Number(left.availableQuantity > 0) ||
            left.productId - right.productId);
      }

      function pickVariant(preferredColor, preferredSize) {
        const normalizedColor = normalizeTextValue(preferredColor);
        const normalizedSize = normalizeTextValue(preferredSize);

        if (normalizedColor && normalizedSize) {
          const exactMatch = sortVariantMatches(variants.filter((variant) =>
            matchesTextValue(variant.color, normalizedColor) &&
            matchesTextValue(variant.sizeLabel, normalizedSize)))[0];

          if (exactMatch) {
            return exactMatch;
          }
        }

        if (normalizedColor) {
          const colorMatch = sortVariantMatches(variants.filter((variant) =>
            matchesTextValue(variant.color, normalizedColor)))[0];

          if (colorMatch) {
            return colorMatch;
          }
        }

        if (normalizedSize) {
          const sizeMatch = sortVariantMatches(variants.filter((variant) =>
            matchesTextValue(variant.sizeLabel, normalizedSize)))[0];

          if (sizeMatch) {
            return sizeMatch;
          }
        }

        return sortVariantMatches(variants)[0] ?? null;
      }

      function updateSelectOptions(activeVariant) {
        const activeColor = activeVariant?.color ?? "";
        const activeSize = activeVariant?.sizeLabel ?? "";

        if (colorSelect instanceof HTMLSelectElement) {
          Array.from(colorSelect.options).forEach((option) => {
            const isAvailable = variants.some((variant) =>
              matchesTextValue(variant.color, option.value));

            option.disabled = !isAvailable;
            option.dataset.unavailable = String(!isAvailable);
          });

          if (activeColor) {
            colorSelect.value = activeColor;
          }
        }

        if (sizeSelect instanceof HTMLSelectElement) {
          Array.from(sizeSelect.options).forEach((option) => {
            const isAvailable = variants.some((variant) =>
              matchesTextValue(variant.sizeLabel, option.value) &&
              (!activeColor || matchesTextValue(variant.color, activeColor)));

            option.disabled = !isAvailable;
            option.dataset.unavailable = String(!isAvailable);
          });

          if (activeSize) {
            sizeSelect.value = activeSize;
          }
        }
      }

      function updateSelectedMeta(targetElement, value) {
        if (!(targetElement instanceof HTMLElement)) {
          return;
        }

        const hasValue = Boolean(value);
        targetElement.hidden = !hasValue;

        if (hasValue) {
          targetElement.textContent = value;
        }
      }

      function setPriceNote(pricePerSquareMeter) {
        if (!(priceNoteElement instanceof HTMLElement)) {
          return;
        }

        const numericValue = Number(pricePerSquareMeter);
        const hasValue = Number.isFinite(numericValue) && numericValue > 0;
        const nextHidden = !hasValue;
        const nextText = hasValue
          ? isPoMjeri
            ? `${formatCurrency(numericValue)} (cijena po metru dužine)`
            : `${formatCurrency(numericValue)} (cijena po kvadratnom metru)`
          : "";

        if (lastRenderedPriceNote === nextText && lastRenderedPriceNoteHidden === nextHidden) {
          return;
        }

        priceNoteElement.hidden = nextHidden;
        priceNoteElement.textContent = nextText;
        lastRenderedPriceNote = nextText;
        lastRenderedPriceNoteHidden = nextHidden;
      }

      function setDisplayedPricing(currentPrice, compareAtPrice, pricePerSquareMeter) {
        if (priceElement instanceof HTMLElement) {
          const nextPrice = formatCurrency(currentPrice);
          if (lastRenderedPrice !== nextPrice) {
            priceElement.textContent = nextPrice;
            lastRenderedPrice = nextPrice;
          }
        }

        if (comparePriceElement instanceof HTMLElement) {
          const hasComparePrice =
            compareAtPrice !== null &&
            compareAtPrice !== undefined &&
            Number(compareAtPrice) > Number(currentPrice);
          const nextCompareText = hasComparePrice
            ? formatCurrency(compareAtPrice)
            : "";
          const nextCompareHidden = !hasComparePrice;

          if (lastRenderedComparePrice !== nextCompareText || lastRenderedCompareHidden !== nextCompareHidden) {
            comparePriceElement.hidden = nextCompareHidden;
            comparePriceElement.textContent = nextCompareText;
            lastRenderedComparePrice = nextCompareText;
            lastRenderedCompareHidden = nextCompareHidden;
          }
        }

        setPriceNote(pricePerSquareMeter);
      }

      function applyVariant(activeVariant) {
        if (!activeVariant) {
          return;
        }

        const variantKey = [
          activeVariant.productId,
          activeVariant.color ?? "",
          activeVariant.sizeLabel ?? "",
          activeVariant.originalWidth ?? ""
        ].join("|");
        const variantChanged = variantKey !== lastAppliedVariantKey;

        updateSelectOptions(activeVariant);

        if (productIdInput instanceof HTMLInputElement) {
          productIdInput.value = String(activeVariant.productId);
        }

        if (selectedColorInput instanceof HTMLInputElement) {
          selectedColorInput.value = activeVariant.color ?? "";
        }

        if (selectedCustomWidthInput instanceof HTMLInputElement) {
          selectedCustomWidthInput.value = Number.isFinite(Number(activeVariant.originalWidth)) && Number(activeVariant.originalWidth) > 0
            ? String(activeVariant.originalWidth)
            : "";
        }

        if (!isPoMjeri || variantChanged) {
          setDisplayedPricing(
            activeVariant.currentPrice,
            activeVariant.compareAtPrice,
            activeVariant.pricePerSquareMeter);
        }

        if (!isPoMjeri) {
          setAvailabilityState(activeVariant.availabilityStatusMessage ?? "", Boolean(activeVariant.isSoldOut));

          if (submitButton instanceof HTMLButtonElement) {
            const isSoldOut = Boolean(activeVariant.isSoldOut);
            submitButton.disabled = isSoldOut;
            submitButton.textContent = isSoldOut ? "Rasprodato" : "Dodaj u korpu";
          }
        }

        updateSelectedMeta(selectedColorElement, activeVariant.color);
        updateSelectedMeta(selectedSizeElement, activeVariant.sizeLabel);

        if (!isPoMjeri && soldOutOverlay instanceof HTMLElement) {
          soldOutOverlay.hidden = !Boolean(activeVariant.isSoldOut);
        }

        if (activeVariant.primaryImageUrl && galleryElement?.setActiveImageByUrl) {
          galleryElement.setActiveImageByUrl(activeVariant.primaryImageUrl);
        }

        lastAppliedVariantKey = variantKey;
      }

      function normalizeRequestedQuantity() {
        if (!(quantityInput instanceof HTMLInputElement)) {
          return {
            quantity: 1,
            wasLimited: false,
            max: 1
          };
        }

        let nextQuantity = Number.parseInt(quantityInput.value || "1", 10);
        let wasLimited = false;
        if (!Number.isFinite(nextQuantity) || nextQuantity < 1) {
          nextQuantity = 1;
        }

        const max = Number.parseInt(quantityInput.max || "0", 10);
        if (Number.isFinite(max) && max > 0) {
          if (nextQuantity > max) {
            nextQuantity = max;
            wasLimited = true;
          }
        }

        quantityInput.value = String(nextQuantity);
        if (selectedQuantityInput instanceof HTMLInputElement) {
          selectedQuantityInput.value = String(nextQuantity);
        }

        if (wasLimited) {
          showQuantityFeedback(buildQuantityLimitMessage(max));
        } else {
          showQuantityFeedback("");
        }

        return {
          quantity: nextQuantity,
          wasLimited,
          max
        };
      }

      async function previewPoMjeri(activeVariant) {
        if (!isPoMjeri || !activeVariant || !previewUrl) {
          return;
        }

        previewRequestSequence += 1;
        const requestSequence = previewRequestSequence;
        if (activePreviewController) {
          activePreviewController.abort();
        }

        activePreviewController = new AbortController();
        const requestedWidth = Number.parseInt(String(activeVariant.originalWidth ?? 0), 10);
        const requestedLength = Number.parseInt(customLengthInput?.value || "0", 10);
        const quantityState = normalizeRequestedQuantity();
        const requestedQuantity = quantityState.quantity;

        if (selectedCustomWidthInput instanceof HTMLInputElement) {
          selectedCustomWidthInput.value = Number.isFinite(requestedWidth) && requestedWidth > 0
            ? String(requestedWidth)
            : "";
        }

        if (selectedCustomLengthInput instanceof HTMLInputElement) {
          selectedCustomLengthInput.value = Number.isFinite(requestedLength) && requestedLength > 0
            ? String(requestedLength)
            : "";
        }

        if (!Number.isFinite(requestedWidth) || requestedWidth <= 0 ||
            !Number.isFinite(requestedLength) || requestedLength <= 0) {
          if (availabilityElement instanceof HTMLElement) {
            availabilityElement.hidden = false;
            availabilityElement.textContent = "Unesite željenu širinu i dužinu.";
            availabilityElement.classList.add("is-unavailable");
          }

          if (submitButton instanceof HTMLButtonElement) {
            submitButton.disabled = true;
          }

          return;
        }

        if (false && requestedLength < requestedWidth) {
          if (availabilityElement instanceof HTMLElement) {
            availabilityElement.hidden = false;
            availabilityElement.textContent = "Dužina ne može biti manja od širine.";
            availabilityElement.classList.add("is-unavailable");
          }

          if (submitButton instanceof HTMLButtonElement) {
            submitButton.disabled = true;
          }

          return;
        }

        const previewParams = new URLSearchParams();
        previewParams.set("productId", String(activeVariant.productId));
        previewParams.set("color", activeVariant.color ?? "");
        previewParams.set("customWidth", String(requestedWidth));
        previewParams.set("customLength", String(requestedLength));
        previewParams.set("quantity", String(requestedQuantity));

        try {
          const response = await fetch(`${previewUrl}?${previewParams.toString()}`, {
            headers: {
              "X-Requested-With": "XMLHttpRequest"
            },
            signal: activePreviewController.signal
          });

          if (!response.ok) {
            throw new Error("Preview failed.");
          }

          const result = await response.json();
          if (requestSequence !== previewRequestSequence) {
            return;
          }

          const maxAvailableQuantity = Number.parseInt(String(result.maxAvailableQuantity ?? 0), 10) || 0;

          if (quantityInput instanceof HTMLInputElement) {
            quantityInput.max = String(Math.max(maxAvailableQuantity, 1));
            if (maxAvailableQuantity > 0 && requestedQuantity > maxAvailableQuantity) {
              quantityInput.value = String(maxAvailableQuantity);
              showQuantityFeedback(buildQuantityLimitMessage(maxAvailableQuantity));
            } else if (result.success && !quantityState.wasLimited) {
              showQuantityFeedback("");
            }
          }

          if (productIdInput instanceof HTMLInputElement && result.selectedProductId) {
            productIdInput.value = String(result.selectedProductId);
          }

          setDisplayedPricing(
            result.currentPrice,
            result.compareAtPrice,
            result.pricePerSquareMeter);

          setAvailabilityState(result.message ?? "", !result.success);

          if (submitButton instanceof HTMLButtonElement) {
            submitButton.disabled = !result.success;
            submitButton.textContent = result.success ? "Dodaj u korpu" : "Rasprodato";
          }

          if (soldOutOverlay instanceof HTMLElement) {
            soldOutOverlay.hidden = Boolean(result.success);
          }
        } catch (error) {
          if (error instanceof DOMException && error.name === "AbortError") {
            return;
          }

          if (requestSequence !== previewRequestSequence) {
            return;
          }

          setAvailabilityState("Trenutno nismo uspjeli da provjerimo dostupnost.", true);

          if (submitButton instanceof HTMLButtonElement) {
            submitButton.disabled = true;
          }
        } finally {
          if (requestSequence === previewRequestSequence) {
            activePreviewController = null;
          }
        }
      }

      function syncVariantSelection() {
        const preferredColor = colorSelect instanceof HTMLSelectElement ? colorSelect.value : "";
        const preferredSize = sizeSelect instanceof HTMLSelectElement ? sizeSelect.value : "";
        const activeVariant = pickVariant(preferredColor, preferredSize);

        applyVariant(activeVariant);

        if (quantityInput instanceof HTMLInputElement && !isPoMjeri) {
          const maxQuantity = Math.max(Number(activeVariant?.availableQuantity ?? 0), 1);
          quantityInput.max = String(maxQuantity);
          normalizeRequestedQuantity();
        }

        if (isPoMjeri) {
          window.clearTimeout(previewDebounceId);
          previewDebounceId = window.setTimeout(() => {
            previewPoMjeri(activeVariant);
          }, 180);
        }
      }

      colorSelect?.addEventListener("change", syncVariantSelection);
      sizeSelect?.addEventListener("change", syncVariantSelection);

      decrementButton?.addEventListener("click", () => {
        if (!(quantityInput instanceof HTMLInputElement)) {
          return;
        }

        const currentValue = Number.parseInt(quantityInput.value || "1", 10) || 1;
        quantityInput.value = String(Math.max(currentValue - 1, 1));
        syncVariantSelection();
      });

      incrementButton?.addEventListener("click", () => {
        if (!(quantityInput instanceof HTMLInputElement)) {
          return;
        }

        const currentValue = Number.parseInt(quantityInput.value || "1", 10) || 1;
        const maxValue = Number.parseInt(quantityInput.max || "0", 10) || 1;
        if (currentValue >= maxValue) {
          quantityInput.value = String(maxValue);
          showQuantityFeedback(buildQuantityLimitMessage(maxValue));
          return;
        }

        quantityInput.value = String(Math.min(currentValue + 1, maxValue));
        showQuantityFeedback("");
        syncVariantSelection();
      });

      quantityInput?.addEventListener("input", () => {
        queueVariantSelectionSync();
      });

      quantityInput?.addEventListener("change", () => {
        syncVariantSelection();
      });

      customLengthInput?.addEventListener("input", () => {
        queueVariantSelectionSync();
      });

      customLengthInput?.addEventListener("change", () => {
        syncVariantSelection();
      });

      syncVariantSelection();
    });
  }

  function initializeCartQuantityForms() {
    const cartPage = document.querySelector("[data-cart-page]");
    if (!cartPage) {
      return;
    }

    const feedbackElement = cartPage.querySelector("[data-cart-feedback]");
    const subtotalElement = cartPage.querySelector("[data-cart-subtotal]");
    const totalItemsElement = cartPage.querySelector("[data-cart-total-items]");
    const cartCountElement = document.querySelector("[data-cart-pill-count]");

    function showFeedback(message) {
      if (!(feedbackElement instanceof HTMLElement)) {
        return;
      }

      if (!message) {
        feedbackElement.hidden = true;
        feedbackElement.textContent = "";
        return;
      }

      feedbackElement.textContent = message;
      feedbackElement.hidden = false;
    }

    function updateSummary(response) {
      if (subtotalElement) {
        subtotalElement.textContent = response.subtotalFormatted;
      }

      if (totalItemsElement) {
        totalItemsElement.textContent = String(response.totalItems);
      }

      if (cartCountElement) {
        cartCountElement.textContent = String(response.totalItems);
      }
    }

    async function submitQuantity(form, requestedQuantity) {
      if (!(form instanceof HTMLFormElement)) {
        return;
      }

      const quantityInput = form.querySelector("[data-cart-quantity-input]");
      const lineIdInput = form.querySelector('input[name="lineId"]');
      const antiForgeryInput = form.querySelector('input[name="__RequestVerificationToken"]');
      const line = form.closest("[data-cart-line]");
      const lineTotalElement = line?.querySelector("[data-cart-line-total]");

      if (!(quantityInput instanceof HTMLInputElement) ||
          !(lineIdInput instanceof HTMLInputElement) ||
          !(antiForgeryInput instanceof HTMLInputElement)) {
        return;
      }

      let normalizedQuantity = Number.parseInt(String(requestedQuantity), 10);
      if (!Number.isFinite(normalizedQuantity) || normalizedQuantity < 1) {
        normalizedQuantity = 1;
      }

      const formData = new URLSearchParams();
      formData.set("__RequestVerificationToken", antiForgeryInput.value);
      formData.set("lineId", lineIdInput.value);
      formData.set("quantity", String(normalizedQuantity));

      quantityInput.disabled = true;

      try {
        const response = await fetch(form.action, {
          method: "POST",
          headers: {
            "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
            "X-Requested-With": "XMLHttpRequest"
          },
          body: formData.toString()
        });

        if (!response.ok) {
          throw new Error("Cart update failed.");
        }

        const result = await response.json();
        if (result.cartEmpty || result.removed) {
          window.location.reload();
          return;
        }

        quantityInput.value = String(result.quantity);
        quantityInput.max = String(result.maxOrderQuantity);

        if (lineTotalElement) {
          lineTotalElement.textContent = result.lineTotalFormatted;
        }

        updateSummary(result);
        showFeedback(result.message);
      } catch {
        showFeedback("Doslo je do greske prilikom azuriranja korpe.");
      } finally {
        quantityInput.disabled = false;
      }
    }

    cartPage.querySelectorAll("[data-cart-line-form]").forEach((form) => {
      if (!(form instanceof HTMLFormElement)) {
        return;
      }

      const quantityInput = form.querySelector("[data-cart-quantity-input]");
      const incrementButton = form.querySelector("[data-cart-increment]");
      const decrementButton = form.querySelector("[data-cart-decrement]");

      if (!(quantityInput instanceof HTMLInputElement)) {
        return;
      }

      let debounceId = 0;

      incrementButton?.addEventListener("click", () => {
        const currentValue = Number.parseInt(quantityInput.value || "1", 10) || 1;
        window.clearTimeout(debounceId);
        submitQuantity(form, currentValue + 1);
      });

      decrementButton?.addEventListener("click", () => {
        const currentValue = Number.parseInt(quantityInput.value || "1", 10) || 1;
        window.clearTimeout(debounceId);
        submitQuantity(form, Math.max(currentValue - 1, 1));
      });

      quantityInput.addEventListener("input", () => {
        window.clearTimeout(debounceId);
        debounceId = window.setTimeout(() => {
          submitQuantity(form, quantityInput.value);
        }, 320);
      });

      quantityInput.addEventListener("blur", () => {
        window.clearTimeout(debounceId);
        submitQuantity(form, quantityInput.value);
      });
    });
  }

  updateChromeHeight();
  applyScrollState();
  initializeDropdowns();
  initializeCategoryPicker();
  initializeCatalogFilters();
  initializeCatalogToolbar();
  initializeHeaderSearch();
  initializeSearchClearButtons();
  initializeProductGalleries();
  initializeVariantPickers();
  initializeCartQuantityForms();

  window.addEventListener("resize", updateChromeHeight);
  window.addEventListener("scroll", requestScrollStateUpdate, { passive: true });

  navToggle?.addEventListener("click", () => {
    setNavOpen(!navOpen);
  });

  navBackdrop?.addEventListener("click", () => {
    setNavOpen(false);
  });

  navCloseButtons.forEach((button) => {
    button.addEventListener("click", () => {
      setNavOpen(false);
    });
  });

  navDrawer?.querySelectorAll("a").forEach((link) => {
    link.addEventListener("click", () => {
      setNavOpen(false);
    });
  });

  notificationCloseButtons.forEach((button) => {
    button.addEventListener("click", closeCartNotification);
  });

  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape") {
      return;
    }

    closeAllDropdowns();

    if (navOpen) {
      setNavOpen(false);
    }

    if (searchOpen) {
      setSearchOpen(false);
    }

    if (catalogPanelOpen) {
      setCatalogPanelOpen(false);
    }

    closeCartNotification();
  });

  document.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    if (searchOpen && headerSearchRoot instanceof HTMLElement && !headerSearchRoot.contains(target)) {
      setSearchOpen(false);
    }
  });
})();
