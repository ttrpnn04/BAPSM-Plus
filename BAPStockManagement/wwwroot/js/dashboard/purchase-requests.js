import { createIdempotencyKey, postApiData } from "./api.js";

export function initializePurchaseRequests(dashboard) {
    const button = document.querySelector("[data-purchase-request]");
    const label = button?.querySelector("[data-purchase-request-label]");
    const status = document.querySelector("[data-purchase-request-status]");

    if (!button || !label || !status) {
        return;
    }

    let idempotencyKey;
    const defaultLabel = label.textContent;

    button.addEventListener("click", async () => {
        if (!idempotencyKey) {
            try {
                idempotencyKey = createIdempotencyKey();
            } catch (error) {
                status.textContent = `สร้างคำขอไม่สำเร็จ: ${error.message}`;
                return;
            }
        }

        const skus = [...new Set((dashboard.urgentItems ?? []).map((item) => item.sku))];
        button.disabled = true;
        label.textContent = "กำลังสร้าง...";
        status.textContent = "กำลังส่งคำขอ...";

        try {
            const job = await postApiData(
                "/api/v1/purchase-requests/auto-generate",
                { strategy: "AI_RECOMMENDED", skus },
                { idempotencyKey });
            const completion = job.status === "COMPLETED"
                ? "งานตัวอย่างเสร็จแล้ว"
                : `สถานะงาน: ${job.status}`;
            const detail = job.message || `Job ${job.jobId}`;
            status.textContent = `${completion} · ${detail}`;
            idempotencyKey = undefined;
        } catch (error) {
            status.textContent = `สร้างคำขอไม่สำเร็จ: ${error.message}`;
        } finally {
            button.disabled = false;
            label.textContent = defaultLabel;
        }
    });
}
