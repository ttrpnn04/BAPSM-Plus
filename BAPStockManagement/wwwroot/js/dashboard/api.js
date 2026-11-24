function antiforgeryToken() {
    const token = document.querySelector('#antiforgery-token input[name="__RequestVerificationToken"]')?.value;
    if (!token) {
        throw new Error("ไม่พบโทเคนยืนยันคำขอ กรุณาโหลดหน้าใหม่");
    }

    return token;
}

export function createIdempotencyKey() {
    if (typeof globalThis.crypto?.randomUUID !== "function") {
        throw new Error("เบราว์เซอร์นี้ไม่รองรับการสร้างรหัสคำขอที่ปลอดภัย");
    }

    return globalThis.crypto.randomUUID();
}

async function readEnvelope(response) {
    const contentType = response.headers.get("content-type") ?? "";
    const body = contentType.includes("json") ? await response.json() : null;

    if (!response.ok) {
        const detail = typeof body?.detail === "string" && body.detail.trim()
            ? body.detail
            : typeof body?.title === "string" && body.title.trim()
                ? body.title
                : `คำขอไม่สำเร็จ (HTTP ${response.status})`;
        throw new Error(detail);
    }

    if (!body || typeof body !== "object" || !Object.hasOwn(body, "data")) {
        throw new Error("รูปแบบคำตอบจากเซิร์ฟเวอร์ไม่ถูกต้อง");
    }

    return body.data;
}

export async function getApiData(path, { signal } = {}) {
    const response = await fetch(path, {
        method: "GET",
        credentials: "same-origin",
        headers: { Accept: "application/json" },
        signal
    });

    return readEnvelope(response);
}

export async function postApiData(path, data, { idempotencyKey } = {}) {
    if (!idempotencyKey) {
        throw new Error("ไม่พบรหัสคำขอ กรุณาลองอีกครั้ง");
    }

    const response = await fetch(path, {
        method: "POST",
        credentials: "same-origin",
        headers: {
            Accept: "application/json",
            "Content-Type": "application/json",
            "RequestVerificationToken": antiforgeryToken(),
            "Idempotency-Key": idempotencyKey
        },
        body: JSON.stringify(data)
    });

    return readEnvelope(response);
}
