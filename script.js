// Menú móvil
const menuBtn = document.querySelector(".menu-btn");
const links = document.querySelector(".links");

menuBtn.addEventListener("click", () => {
    const open = links.classList.toggle("open");
    menuBtn.setAttribute("aria-expanded", String(open));
});

links.querySelectorAll("a").forEach((a) =>
    a.addEventListener("click", () => {
        links.classList.remove("open");
        menuBtn.setAttribute("aria-expanded", "false");
    })
);

// Botones "Copiar"
document.querySelectorAll(".copy").forEach((btn) => {
    btn.addEventListener("click", async () => {
        const text = document.getElementById(btn.dataset.target).textContent;
        try {
            await navigator.clipboard.writeText(text);
            btn.textContent = "¡Copiado!";
        } catch {
            btn.textContent = "Error";
        }
        setTimeout(() => (btn.textContent = "Copiar"), 1500);
    });
});

// Aparición al hacer scroll
const observer = new IntersectionObserver(
    (entries) => {
        entries.forEach((entry) => {
            if (entry.isIntersecting) {
                entry.target.classList.add("visible");
                observer.unobserve(entry.target);
            }
        });
    },
    { threshold: 0.12 }
);
document.querySelectorAll(".reveal").forEach((el) => observer.observe(el));

// Enlaces de descarga directos a la última versión publicada en GitHub.
// Si falla (sin conexión o límite de la API), los botones siguen llevando a la página de Releases.
fetch("https://api.github.com/repos/sergio120602/Proyectito_Launcherito/releases/latest")
    .then((r) => (r.ok ? r.json() : Promise.reject(r.status)))
    .then((release) => {
        const version = release.tag_name.replace(/^v/, "");
        document.querySelectorAll(".js-version").forEach((el) => (el.textContent = version));

        const find = (ext) => release.assets.find((a) => a.name.toLowerCase().endsWith(ext));
        const zip = find(".zip");
        const apk = find(".apk");
        if (zip) document.querySelector(".js-dl-win").href = zip.browser_download_url;
        if (apk) document.querySelector(".js-dl-apk").href = apk.browser_download_url;
    })
    .catch(() => {});

// Año en el pie de página
document.getElementById("year").textContent = new Date().getFullYear();
