import { getApiData } from "./api.js";

export function initializeProductSearch() {
    const root = document.querySelector("[data-product-search]");
    const input = root?.querySelector("[data-product-search-input]");
    const list = root?.querySelector("[data-product-search-results]");
    const status = root?.querySelector("[data-product-search-status]");

    if (!root || !input || !list || !status) {
        return;
    }

    let timerId;
    let controller;
    let activeIndex = -1;
    let results = [];
    let searchGeneration = 0;

    const closeResults = () => {
        results = [];
        list.replaceChildren();
        list.hidden = true;
        input.setAttribute("aria-expanded", "false");
        input.removeAttribute("aria-activedescendant");
        activeIndex = -1;
    };

    const setActiveOption = (index) => {
        const options = [...list.querySelectorAll('[role="option"]')];
        if (options.length === 0) {
            activeIndex = -1;
            input.removeAttribute("aria-activedescendant");
            return;
        }

        activeIndex = (index + options.length) % options.length;
        options.forEach((option, optionIndex) => {
            const selected = optionIndex === activeIndex;
            option.setAttribute("aria-selected", String(selected));
            if (selected) {
                input.setAttribute("aria-activedescendant", option.id);
                option.scrollIntoView({ block: "nearest" });
            }
        });
    };

    const chooseProduct = (product) => {
        controller?.abort();
        searchGeneration++;
        input.value = product.sku;
        status.textContent = `${product.productName} — คงเหลือ ${product.onHand.toLocaleString("th-TH")} ${product.unit}`;
        closeResults();
        input.focus();
    };

    const renderResults = (products) => {
        results = products;
        activeIndex = -1;
        input.removeAttribute("aria-activedescendant");
        list.replaceChildren();

        products.forEach((product, index) => {
            const option = document.createElement("div");
            option.id = `product-search-option-${index}`;
            option.setAttribute("role", "option");
            option.setAttribute("aria-selected", "false");
            option.className = "dashboard-search-option";

            const name = document.createElement("strong");
            name.textContent = product.productName;
            const details = document.createElement("span");
            details.textContent = `${product.sku} · คงเหลือ ${product.onHand.toLocaleString("th-TH")} ${product.unit}`;
            option.append(name, details);

            option.addEventListener("mouseenter", () => setActiveOption(index));
            option.addEventListener("click", () => chooseProduct(product));
            list.append(option);
        });

        list.hidden = products.length === 0;
        input.setAttribute("aria-expanded", String(products.length > 0));
        if (products.length === 0) {
            status.textContent = "ไม่พบสินค้าที่ตรงกับคำค้น";
        } else {
            status.textContent = `พบสินค้า ${products.length} รายการ`;
        }
    };

    const search = async () => {
        const query = input.value.trim();
        controller?.abort();
        const generation = ++searchGeneration;

        if (!query) {
            results = [];
            list.replaceChildren();
            status.textContent = "";
            closeResults();
            return;
        }

        controller = new AbortController();
        status.textContent = "กำลังค้นหา...";
        try {
            const products = await getApiData(
                `/api/v1/products/search?q=${encodeURIComponent(query)}&limit=10`,
                { signal: controller.signal });
            if (generation === searchGeneration) {
                renderResults(products);
            }
        } catch (error) {
            if (error.name === "AbortError" || generation !== searchGeneration) {
                return;
            }

            closeResults();
            status.textContent = `ค้นหาไม่สำเร็จ: ${error.message}`;
        }
    };

    input.addEventListener("input", () => {
        controller?.abort();
        searchGeneration++;
        window.clearTimeout(timerId);
        results = [];
        list.replaceChildren();
        closeResults();
        status.textContent = input.value.trim() ? "กำลังค้นหา..." : "";
        timerId = window.setTimeout(search, 250);
    });

    input.addEventListener("keydown", (event) => {
        if (event.key === "ArrowDown" && results.length > 0) {
            event.preventDefault();
            setActiveOption(activeIndex + 1);
        } else if (event.key === "ArrowUp" && results.length > 0) {
            event.preventDefault();
            setActiveOption(activeIndex < 0 ? results.length - 1 : activeIndex - 1);
        } else if (event.key === "Enter") {
            if (activeIndex >= 0 && results[activeIndex]) {
                event.preventDefault();
                chooseProduct(results[activeIndex]);
            } else if (input.value.trim()) {
                event.preventDefault();
                window.clearTimeout(timerId);
                void search();
            }
        } else if (event.key === "Escape") {
            controller?.abort();
            searchGeneration++;
            closeResults();
        }
    });

    document.addEventListener("click", (event) => {
        if (!root.contains(event.target)) {
            controller?.abort();
            searchGeneration++;
            closeResults();
        }
    });

    document.addEventListener("keydown", (event) => {
        if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
            event.preventDefault();
            input.focus();
            input.select();
        }
    });
}
