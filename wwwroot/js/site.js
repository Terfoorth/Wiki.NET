window.wikiBlaze = window.wikiBlaze || {};
window.wikiBlaze.themeMap = {
    cerulean: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/cerulean/bootstrap.min.css",
    cyborg: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/cyborg/bootstrap.min.css",
    cosmo: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/cosmo/bootstrap.min.css",
    darkly: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/darkly/bootstrap.min.css",
    flatly: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/flatly/bootstrap.min.css",
    journal: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/journal/bootstrap.min.css",
    litera: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/litera/bootstrap.min.css",
    lumen: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/lumen/bootstrap.min.css",
    lux: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/lux/bootstrap.min.css",
    materia: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/materia/bootstrap.min.css",
    minty: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/minty/bootstrap.min.css",
    morph: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/morph/bootstrap.min.css",
    pulse: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/pulse/bootstrap.min.css",
    quartz: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/quartz/bootstrap.min.css",
    sandstone: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/sandstone/bootstrap.min.css",
    simplex: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/simplex/bootstrap.min.css",
    sketchy: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/sketchy/bootstrap.min.css",
    slate: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/slate/bootstrap.min.css",
    solar: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/solar/bootstrap.min.css",
    spacelab: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/spacelab/bootstrap.min.css",
    superhero: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/superhero/bootstrap.min.css",
    united: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/united/bootstrap.min.css",
    vapor: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/vapor/bootstrap.min.css",
    yeti: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/yeti/bootstrap.min.css",
    zephyr: "https://cdn.jsdelivr.net/npm/bootswatch@5.3.3/dist/zephyr/bootstrap.min.css"
};
window.wikiBlaze.preferenceStorage = {
    themeKey: "wikiBlaze.preference.theme",
    densityKey: "wikiBlaze.preference.density",
    themeHrefKey: "wikiBlaze.preference.themeHref"
};

window.wikiBlaze.cacheUiPreferences = function (theme, density, themeHref) {
    try {
        if (theme !== undefined && theme !== null) {
            window.localStorage.setItem(window.wikiBlaze.preferenceStorage.themeKey, theme);
        }

        if (density !== undefined && density !== null) {
            window.localStorage.setItem(window.wikiBlaze.preferenceStorage.densityKey, density);
        }

        if (themeHref !== undefined && themeHref !== null) {
            window.localStorage.setItem(window.wikiBlaze.preferenceStorage.themeHrefKey, themeHref);
        }
    } catch (error) {
        // Ignore local storage access errors in locked-down browser contexts.
    }
};

window.wikiBlaze.setTheme = function (theme) {
    var root = document.documentElement;
    var bootstrapLink = document.getElementById("bootstrap-theme");
    var defaultHref = bootstrapLink ? bootstrapLink.getAttribute("data-default-href") : null;
    var resolvedTheme = theme || "system";
    var resolvedThemeHref = bootstrapLink ? bootstrapLink.getAttribute("href") : defaultHref;
    var applyTheme = function (value) {
        root.setAttribute('data-bs-theme', value);
    };
    var applyBootstrapHref = function (href) {
        if (bootstrapLink && href) {
            bootstrapLink.setAttribute("href", href);
            resolvedThemeHref = href;
        }
    };

    if (resolvedTheme && resolvedTheme.startsWith("bootswatch:")) {
        var name = resolvedTheme.split(":")[1];
        var cssHref = window.wikiBlaze.themeMap[name];
        if (cssHref) {
            applyBootstrapHref(cssHref);
            applyTheme("light");
        }
        if (window.wikiBlaze._themeMediaQuery && window.wikiBlaze._themeListener) {
            window.wikiBlaze._themeMediaQuery.removeEventListener('change', window.wikiBlaze._themeListener);
            window.wikiBlaze._themeListener = null;
            window.wikiBlaze._themeMediaQuery = null;
        }
        window.wikiBlaze.cacheUiPreferences(resolvedTheme, null, resolvedThemeHref);
        return;
    }

    if (defaultHref) {
        applyBootstrapHref(defaultHref);
    }

    if (resolvedTheme === 'system') {
        var mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
        applyTheme(mediaQuery.matches ? 'dark' : 'light');

        if (window.wikiBlaze._themeMediaQuery && window.wikiBlaze._themeListener) {
            window.wikiBlaze._themeMediaQuery.removeEventListener('change', window.wikiBlaze._themeListener);
        }

        window.wikiBlaze._themeListener = function (event) {
            applyTheme(event.matches ? 'dark' : 'light');
        };

        mediaQuery.addEventListener('change', window.wikiBlaze._themeListener);
        window.wikiBlaze._themeMediaQuery = mediaQuery;
    } else {
        applyTheme(resolvedTheme);
        if (window.wikiBlaze._themeMediaQuery && window.wikiBlaze._themeListener) {
            window.wikiBlaze._themeMediaQuery.removeEventListener('change', window.wikiBlaze._themeListener);
            window.wikiBlaze._themeListener = null;
            window.wikiBlaze._themeMediaQuery = null;
        }
    }

    window.wikiBlaze.cacheUiPreferences(resolvedTheme, null, resolvedThemeHref);
};

window.wikiBlaze.setDensity = function (density) {
    var resolvedDensity = density || "comfortable";
    document.documentElement.setAttribute('data-density', resolvedDensity);
    window.wikiBlaze.cacheUiPreferences(null, resolvedDensity, null);
};

window.wikiBlaze.loadDashboardSettings = function (key) {
    if (!key) {
        return null;
    }

    return window.localStorage.getItem(key);
};

window.wikiBlaze.saveDashboardSettings = function (key, payload) {
    if (!key) {
        return;
    }

    if (payload === null || payload === undefined) {
        window.localStorage.removeItem(key);
        return;
    }

    window.localStorage.setItem(key, payload);
};

window.wikiBlaze.downloadFileFromBytes = function (fileName, base64Data, contentType) {
    const link = document.createElement('a');
    link.href = `data:${contentType};base64,${base64Data}`;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.wikiBlaze.onboardingGrid = (function () {
    const hostHandlers = new Map();
    const rowAttributeName = "data-onboarding-visible-index";

    function getHostElement(hostId) {
        if (!hostId || typeof hostId !== "string") {
            return null;
        }

        return document.getElementById(hostId);
    }

    function tryGetVisibleIndex(target) {
        if (!(target instanceof Element)) {
            return null;
        }

        const rowElement = target.closest("[" + rowAttributeName + "]");
        if (!rowElement) {
            return null;
        }

        const visibleIndexRaw = rowElement.getAttribute(rowAttributeName);
        const visibleIndex = Number.parseInt(visibleIndexRaw ?? "", 10);
        return Number.isInteger(visibleIndex) ? visibleIndex : null;
    }

    function isNonDataAreaClick(target) {
        if (!(target instanceof Element)) {
            return true;
        }

        // Ignore clicks on headers and pager controls.
        return !!target.closest(".dxbl-grid-header, .dxbl-grid-pager");
    }

    function attach(hostId, dotNetRef) {
        detach(hostId);

        const host = getHostElement(hostId);
        if (!host || !dotNetRef) {
            return false;
        }

        const clickHandler = function (event) {
            if (event.button !== 0) {
                return;
            }

            if (tryGetVisibleIndex(event.target) !== null) {
                return;
            }

            if (isNonDataAreaClick(event.target)) {
                return;
            }

            dotNetRef.invokeMethodAsync("HandleGridBlankClick").catch(function () {
            });
        };

        const contextMenuHandler = function (event) {
            const visibleIndex = tryGetVisibleIndex(event.target);
            if (visibleIndex === null) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();

            dotNetRef.invokeMethodAsync(
                "HandleGridRowContextMenu",
                visibleIndex,
                event.clientX,
                event.clientY)
                .catch(function () {
                });
        };

        host.addEventListener("click", clickHandler);
        host.addEventListener("contextmenu", contextMenuHandler);
        hostHandlers.set(hostId, { host: host, clickHandler: clickHandler, contextMenuHandler: contextMenuHandler });

        return true;
    }

    function detach(hostId) {
        const handlers = hostHandlers.get(hostId);
        if (!handlers) {
            return;
        }

        handlers.host.removeEventListener("click", handlers.clickHandler);
        handlers.host.removeEventListener("contextmenu", handlers.contextMenuHandler);
        hostHandlers.delete(hostId);
    }

    return {
        attach: attach,
        detach: detach
    };
})();
