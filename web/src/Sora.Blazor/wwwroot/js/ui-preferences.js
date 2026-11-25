let soraFollowSystemTheme = false;
const soraSystemThemeQuery = window.matchMedia("(prefers-color-scheme: dark)");
const setSoraEffectiveTheme = () => {
    if (soraFollowSystemTheme) document.documentElement.dataset.theme = soraSystemThemeQuery.matches ? "dark" : "light";
};
soraSystemThemeQuery.addEventListener("change", setSoraEffectiveTheme);

window.soraUiPreferences = {
    get: key => localStorage.getItem(key),
    set: (key, value) => localStorage.setItem(key, value),
    apply: json => {
        const prefs = JSON.parse(json || "{}");
        const root = document.documentElement;
        soraFollowSystemTheme = prefs.theme === "system";
        root.dataset.theme = ["light", "dark", "system"].includes(prefs.theme) ? prefs.theme : "light";
        setSoraEffectiveTheme();
        root.dataset.contrast = prefs.highContrast ? "high" : "normal";
        const scale = [100, 112, 125, 150, 200].includes(prefs.fontScale) ? prefs.fontScale : 100;
        root.style.setProperty("--font-scale", String(scale / 100));
    }
};
