export function initializeForecast(dashboard) {
    const canvas = document.querySelector("#forecast-chart");
    const horizonSelect = document.querySelector("#forecast-horizon");

    if (!canvas || !window.Chart || !dashboard.forecast?.length) {
        return;
    }

    const points = dashboard.forecast;
    const chart = new Chart(canvas, {
        type: "line",
        data: {
            labels: points.map((point) => point.month),
            datasets: [
                {
                    label: "ยอดใช้จริง",
                    data: points.map((point) => point.actual),
                    borderColor: "#4f5df5",
                    backgroundColor: "#4f5df5",
                    borderWidth: 3,
                    pointRadius: 3,
                    pointHoverRadius: 5,
                    tension: 0.35,
                    spanGaps: false
                },
                {
                    label: "ขอบล่าง",
                    data: points.map((point) => point.confidenceLow),
                    borderColor: "transparent",
                    pointRadius: 0,
                    fill: false,
                    tension: 0.35
                },
                {
                    label: "ช่วงความเชื่อมั่น",
                    data: points.map((point) => point.confidenceHigh),
                    borderColor: "transparent",
                    backgroundColor: "rgba(139, 92, 246, 0.13)",
                    pointRadius: 0,
                    fill: { target: "-1" },
                    tension: 0.35
                },
                {
                    label: "คาดการณ์ (AI)",
                    data: points.map((point) => point.forecast),
                    borderColor: "#8b5cf6",
                    backgroundColor: "#8b5cf6",
                    borderWidth: 3,
                    borderDash: [7, 6],
                    pointRadius: 3,
                    pointHoverRadius: 5,
                    tension: 0.35,
                    spanGaps: false
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { intersect: false, mode: "index" },
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: (context) => {
                            if (context.datasetIndex === 1 || context.datasetIndex === 2) {
                                return null;
                            }

                            return `${context.dataset.label}: ${new Intl.NumberFormat("th-TH").format(context.parsed.y)} รายการ`;
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: { display: false },
                    border: { display: false },
                    ticks: { color: "#85899a", font: { family: "Noto Sans Thai" } }
                },
                y: {
                    min: 0,
                    max: 2000,
                    ticks: {
                        stepSize: 500,
                        color: "#85899a",
                        font: { family: "Noto Sans Thai" },
                        callback: (value) => new Intl.NumberFormat("th-TH").format(value)
                    },
                    grid: { color: "#eef0f6", drawTicks: false },
                    border: { display: false }
                }
            }
        }
    });

    horizonSelect?.addEventListener("change", () => {
        const horizon = Number(horizonSelect.value);
        const forecastStart = points.findIndex((point) => point.forecast !== null);
        const start = Math.max(0, forecastStart - 1);
        const end = Math.min(points.length, forecastStart + horizon + 1);
        const visiblePoints = points.slice(start, end);

        chart.data.labels = visiblePoints.map((point) => point.month);
        chart.data.datasets.forEach((dataset, index) => {
            const keys = ["actual", "confidenceLow", "confidenceHigh", "forecast"];
            dataset.data = visiblePoints.map((point) => point[keys[index]]);
        });
        chart.update();
    });
}
