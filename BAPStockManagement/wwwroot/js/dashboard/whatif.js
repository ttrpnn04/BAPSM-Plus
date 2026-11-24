import { createIdempotencyKey, postApiData } from "./api.js";

export function initializeWhatIf() {
    const slider = document.querySelector("#order-quantity");
    const output = document.querySelector("#order-quantity-output");
    const section = document.querySelector(".dashboard-simulation");
    const button = section?.querySelector("[data-simulation-submit]");
    const label = button?.querySelector("[data-simulation-label]");
    const status = section?.querySelector("[data-simulation-status]");

    if (!slider || !output || !section || !button || !label || !status) {
        return;
    }

    const sku = section.dataset.sku;
    const defaultLabel = label.textContent;
    let idempotencyKey;

    const updateQuantity = () => {
        const value = Number(slider.value);
        const quantity = new Intl.NumberFormat("th-TH").format(value);
        output.value = `${quantity} ชิ้น`;
        slider.setAttribute("aria-valuetext", `${quantity} ชิ้น`);
        const progress = ((value - Number(slider.min)) / (Number(slider.max) - Number(slider.min))) * 100;
        slider.style.setProperty("--simulation-progress", `${progress}%`);
    };

    slider.addEventListener("input", () => {
        updateQuantity();
        idempotencyKey = undefined;
        status.textContent = "ปรับจำนวนแล้ว กด “ลองจำลอง” เพื่อคำนวณผลลัพธ์";
    });
    updateQuantity();

    button.addEventListener("click", async () => {
        if (!idempotencyKey) {
            try {
                idempotencyKey = createIdempotencyKey();
            } catch (error) {
                status.textContent = `จำลองไม่สำเร็จ: ${error.message}`;
                return;
            }
        }

        slider.disabled = true;
        button.disabled = true;
        label.textContent = "กำลังคำนวณ...";
        status.textContent = "กำลังคำนวณผลการจำลอง...";

        try {
            const result = await postApiData(
                "/api/v1/simulations/what-if",
                { sku, orderQty: Number(slider.value) },
                { idempotencyKey });
            section.querySelector("[data-simulation-risk]").textContent =
                `${Number(result.stockoutRiskPercent).toLocaleString("th-TH", { maximumFractionDigits: 1 })}%`;
            section.querySelector("[data-simulation-order-cost]").textContent =
                `฿${Number(result.orderCost.amount).toLocaleString("th-TH", { maximumFractionDigits: 0 })}`;
            section.querySelector("[data-simulation-holding-cost]").textContent =
                `฿${Number(result.holdingCost.amount).toLocaleString("th-TH", { maximumFractionDigits: 0 })}`;
            section.querySelector("[data-simulation-coverage]").textContent =
                `${Number(result.coverageDays).toLocaleString("th-TH")} วัน`;
            status.textContent = "คำนวณผลการจำลองเรียบร้อย";
            idempotencyKey = undefined;
        } catch (error) {
            status.textContent = `จำลองไม่สำเร็จ: ${error.message}`;
        } finally {
            slider.disabled = false;
            button.disabled = false;
            label.textContent = defaultLabel;
        }
    });
}
