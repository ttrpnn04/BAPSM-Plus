const statusLabels = {
    NORMAL: "ปกติ",
    LOW_STOCK: "ใกล้หมด",
    OUT_OF_STOCK: "หมดสต็อก",
    OVERSTOCK: "เกินสต็อก"
};

const statusColors = {
    NORMAL: "#25b881",
    LOW_STOCK: "#f1ad43",
    OUT_OF_STOCK: "#ee6674",
    OVERSTOCK: "#8b5cf6"
};

const centerTextPlugin = {
    id: "dashboardCenterText",
    afterDraw(chart, args, options) {
        const { ctx, chartArea } = chart;

        if (!chartArea) {
            return;
        }

        const x = (chartArea.left + chartArea.right) / 2;
        const y = (chartArea.top + chartArea.bottom) / 2;
        ctx.save();
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";
        ctx.fillStyle = "#252941";
        ctx.font = "700 22px 'Noto Sans Thai', sans-serif";
        ctx.fillText(new Intl.NumberFormat("th-TH").format(options.total), x, y - 7);
        ctx.fillStyle = "#85899a";
        ctx.font = "400 12px 'Noto Sans Thai', sans-serif";
        ctx.fillText("รายการ", x, y + 17);
        ctx.restore();
    }
};

export function initializeStockStatus(dashboard) {
    const canvas = document.querySelector("#stock-status-chart");

    if (!canvas || !window.Chart || !dashboard.stockStatus?.length) {
        return;
    }

    new Chart(canvas, {
        type: "doughnut",
        data: {
            labels: dashboard.stockStatus.map((status) => statusLabels[status.status] ?? status.status),
            datasets: [{
                data: dashboard.stockStatus.map((status) => status.count),
                backgroundColor: dashboard.stockStatus.map((status) => statusColors[status.status] ?? "#a6acc0"),
                borderWidth: 0,
                hoverOffset: 4
            }]
        },
        options: {
            cutout: "76%",
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: (context) => `${context.label}: ${new Intl.NumberFormat("th-TH").format(context.parsed)} รายการ`
                    }
                },
                dashboardCenterText: { total: dashboard.kpis.totalProducts }
            }
        },
        plugins: [centerTextPlugin]
    });
}
