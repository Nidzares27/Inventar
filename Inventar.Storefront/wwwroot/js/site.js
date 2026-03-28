(() => {
  const root = document.documentElement;
  const body = document.body;
  const siteChrome = document.querySelector("[data-site-chrome]");
  const navToggle = document.querySelector("[data-nav-toggle]");
  const navDrawer = document.querySelector("[data-nav-drawer]");
  const navBackdrop = document.querySelector("[data-nav-backdrop]");
  const navCloseButtons = document.querySelectorAll("[data-nav-close]");
  const notificationCloseButtons = document.querySelectorAll("[data-cart-notification-close]");

  let lastScrollY = window.scrollY;
  let navOpen = false;
  let ticking = false;

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
    if (window.scrollY > 32 && !navOpen) {
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

  updateChromeHeight();
  applyScrollState();

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

    if (navOpen) {
      setNavOpen(false);
    }

    closeCartNotification();
  });
})();
