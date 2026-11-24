import { initializeProductSearch } from "./product-search.js";
import { initializePurchaseRequests } from "./purchase-requests.js";
import { initializeForecast } from "./forecast.js";
import { initializeStockStatus } from "./donut.js";
import { initializeWhatIf } from "./whatif.js";
import { initializeSparklines } from "./sparklines.js";
import { initializeSidebar } from "./sidebar.js";

const dataElement = document.querySelector("#dashboard-data");

if (dataElement) {
    const dashboard = JSON.parse(dataElement.textContent);

    initializeForecast(dashboard);
    initializeStockStatus(dashboard);
    initializeWhatIf();
    initializeSparklines(dashboard);
    initializePurchaseRequests(dashboard);
}

initializeProductSearch();
initializeSidebar();
