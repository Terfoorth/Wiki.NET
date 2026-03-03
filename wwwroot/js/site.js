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

window.wikiBlaze.setTheme = function (theme) {
    var root = document.documentElement;
    var bootstrapLink = document.getElementById("bootstrap-theme");
    var defaultHref = bootstrapLink ? bootstrapLink.getAttribute("data-default-href") : null;
    var applyTheme = function (value) {
        root.setAttribute('data-bs-theme', value);
    };
    var applyBootstrapHref = function (href) {
        if (bootstrapLink && href) {
            bootstrapLink.setAttribute("href", href);
        }
    };

    if (theme && theme.startsWith("bootswatch:")) {
        var name = theme.split(":")[1];
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
        return;
    }

    if (defaultHref) {
        applyBootstrapHref(defaultHref);
    }

    if (theme === 'system') {
        var mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
        applyTheme(mediaQuery.matches ? 'dark' : 'light');

        if (window.wikiBlaze._themeListener) {
            mediaQuery.removeEventListener('change', window.wikiBlaze._themeListener);
        }

        window.wikiBlaze._themeListener = function (event) {
            applyTheme(event.matches ? 'dark' : 'light');
        };

        mediaQuery.addEventListener('change', window.wikiBlaze._themeListener);
        window.wikiBlaze._themeMediaQuery = mediaQuery;
    } else {
        applyTheme(theme);
        if (window.wikiBlaze._themeMediaQuery && window.wikiBlaze._themeListener) {
            window.wikiBlaze._themeMediaQuery.removeEventListener('change', window.wikiBlaze._themeListener);
            window.wikiBlaze._themeListener = null;
            window.wikiBlaze._themeMediaQuery = null;
        }
    }
};

window.wikiBlaze.setDensity = function (density) {
    document.documentElement.setAttribute('data-density', density);
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
