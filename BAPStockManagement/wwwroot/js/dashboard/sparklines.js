export function initializeSparklines(dashboard) {
    if (!window.Chart || !dashboard.topProducts?.length) {
        return;
    }

    document.querySelectorAll(".sparkline").forEach((canvas) => {
        const index = Number(canvas.dataset.sparklineIndex);
        const product = dashboard.topProducts[index];

        if (!product) {
            return;
        }

        new Chart(canvas, {
            type: "line",
            data: {
                labels: product.trend.map((_, pointIndex) => pointIndex + 1),
                datasets: [{
                    data: product.trend,
                    borderColor: "#4f5df5",
                    borderWidth: 2,
                    pointRadius: 0,
                    tension: 0.4,
                    fill: false
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: false,
                plugins: { legend: { display: false }, tooltip: { enabled: false } },
                scales: {
                    x: { display: false },
                    y: { display: false, suggestedMin: 0 }
                },
                elements: { line: { capBezierPoints: true } }
            }
        });
    });
}
