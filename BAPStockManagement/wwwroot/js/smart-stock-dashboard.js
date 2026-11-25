(() => {
    const dashboard = document.querySelector('[data-smart-stock-dashboard]');
    if (!dashboard) return;

    const number = new Intl.NumberFormat('th-TH', { maximumFractionDigits: 0 });
    const date = new Intl.DateTimeFormat('th-TH', { day: 'numeric', month: 'short', timeZone: 'UTC' });
    const series = JSON.parse(dashboard.querySelector('[data-smart-stock-series]').textContent);
    const analyses = new Map(JSON.parse(dashboard.querySelector('[data-smart-stock-analyses]').textContent)
        .map(item => [String(item.risk.productId), item]));
    const chart = dashboard.querySelector('[data-smart-stock-chart]');
    const svg = chart.querySelector('svg');
    const filters = Array.from(dashboard.querySelectorAll('[data-smart-stock-filter]'));

    function svgElement(tag, attributes, value) {
        const element = document.createElementNS('http://www.w3.org/2000/svg', tag);
        Object.entries(attributes).forEach(([name, attributeValue]) => element.setAttribute(name, attributeValue));
        if (value != null) element.textContent = value;
        return element;
    }

    function chartPoints(segment) {
        const weeks = new Map();
        for (const row of series) {
            if (segment !== 'all' && row.segment !== segment) continue;
            const week = weeks.get(row.weekStart) || { actual: null, forecast: null };
            if (row.actual != null) week.actual = (week.actual ?? 0) + row.actual;
            if (row.forecast != null) week.forecast = (week.forecast ?? 0) + row.forecast;
            weeks.set(row.weekStart, week);
        }
        return Array.from(weeks).sort(([first], [second]) => first.localeCompare(second));
    }

    function renderChart(segment) {
        const points = chartPoints(segment);
        for (const type of ['actual', 'forecast']) {
            const values = points.map(([, point]) => point[type]).filter(value => value != null);
            dashboard.querySelector('[data-chart-' + type + ']').textContent = values.length
                ? number.format(values.reduce((sum, value) => sum + value, 0))
                : '—';
        }
        svg.replaceChildren();
        svg.toggleAttribute('hidden', points.length === 0);
        chart.querySelector('[data-chart-empty]').hidden = points.length > 0;
        if (!points.length) return;

        const step = Math.max(1, Math.ceil(Math.max(...points.flatMap(([, point]) =>
            [point.actual ?? 0, point.forecast ?? 0])) / 5));
        const ceiling = step * 5;
        const x = index => points.length === 1 ? 365 : 54 + index * 620 / (points.length - 1);
        const y = quantity => 203 - quantity * 178 / ceiling;
        const actualLine = [];
        const marks = [];
        for (let index = 0; index <= 5; index++) {
            const tick = index * step;
            svg.append(svgElement('line', { x1: 54, x2: 674, y1: y(tick), y2: y(tick), class: 'smart-stock-live__grid' }),
                svgElement('text', { x: 46, y: y(tick) + 4, 'text-anchor': 'end' }, number.format(tick)));
        }

        points.forEach(([week, point], index) => {
            const label = date.format(new Date(week + 'T00:00:00Z'));
            svg.append(svgElement('text', { x: x(index), y: 223, 'text-anchor': 'middle' }, label));
            if (point.actual != null) actualLine.push(x(index) + ',' + y(point.actual));

            for (const [type, caption] of [['actual', 'ยอดจริง'], ['forecast', 'ยอดคาดการณ์']]) {
                const value = point[type];
                if (value == null) continue;
                const circle = svgElement('circle', { cx: x(index), cy: y(value), r: type === 'forecast' ? 6 : 3.5,
                    class: 'smart-stock-live__point--' + type });
                circle.append(svgElement('title', {}, label + ' · ' + caption + ' ' + number.format(value)));
                marks.push(circle);
            }
        });
        svg.append(svgElement('polyline', { points: actualLine.join(' '), class: 'smart-stock-actual-line', fill: 'none' }), ...marks);
    }

    for (const filter of filters) {
        filter.addEventListener('click', () => {
            for (const item of filters) {
                const selected = item === filter;
                item.classList.toggle('btn-primary', selected);
                item.classList.toggle('btn-outline-secondary', !selected);
                item.setAttribute('aria-pressed', String(selected));
            }
            renderChart(filter.dataset.smartStockFilter);
        });
    }
    renderChart('all');

    const listOpeners = new WeakMap();
    for (const list of dashboard.querySelectorAll('[data-smart-stock-list]')) {
        list.addEventListener('show.bs.modal', event => {
            if (event.relatedTarget) listOpeners.set(list, event.relatedTarget);
        });

        const search = list.querySelector('[data-list-search]');
        if (!search) continue;

        const filter = list.querySelector('[data-list-risk]');
        const count = list.querySelector('[data-list-count]');
        const noMatches = list.querySelector('[data-list-no-matches]');
        const rows = Array.from(list.querySelectorAll('[data-list-row]'));
        function filterRows() {
            const term = search.value.trim().toLocaleLowerCase();
            let shown = 0;
            for (const row of rows) {
                const matches = row.dataset.query.toLocaleLowerCase().includes(term) &&
                    (!filter.value || filter.value === row.dataset.risk);
                row.hidden = !matches;
                if (matches) shown++;
            }
            count.textContent = number.format(shown) + ' รายการ';
            noMatches.hidden = shown > 0;
        }
        search.addEventListener('input', filterRows);
        filter.addEventListener('change', filterRows);
    }

    const detail = dashboard.querySelector('[data-smart-stock-detail-modal]');
    const back = detail.querySelector('[data-smart-stock-back]');
    const fields = new Map(Array.from(detail.querySelectorAll('[data-detail]'))
        .map(field => [field.dataset.detail, field]));
    const levels = { High: 'สูง', Medium: 'ปานกลาง', Low: 'ต่ำ' };
    let origin = null;
    let restoreList = false;

    function populate(item) {
        const { risk, purchase } = item;
        const quantity = value => number.format(value) + ' ' + item.unit;
        const values = {
            identity: item.sku + ' · ' + item.segmentLabel,
            name: risk.productName,
            risk: 'ความเสี่ยง' + levels[risk.riskLevel],
            action: purchase.suggestedQuantity > 0 ? 'สั่งเพิ่ม ' + quantity(purchase.suggestedQuantity) : 'ยังไม่ต้องสั่งเพิ่ม',
            reason: purchase.recommendationReason,
            stock: quantity(risk.currentStock),
            demand: quantity(risk.forecastDemand),
            safety: quantity(risk.safetyStock),
            remaining: quantity(risk.estimatedRemainingStock),
            model: item.model,
            share: new Intl.NumberFormat('th-TH', { style: 'percent', maximumFractionDigits: 1 }).format(item.salesShare)
        };
        Object.entries(values).forEach(([key, value]) => { fields.get(key).textContent = value; });
        fields.get('risk').className = 'smart-stock-risk smart-stock-risk-' + risk.riskLevel.toLowerCase();
    }

    dashboard.addEventListener('click', event => {
        const trigger = event.target.closest('[data-smart-stock-detail]');
        if (!trigger || !window.bootstrap || detail.classList.contains('show')) return;
        const item = analyses.get(trigger.dataset.smartStockDetail);
        if (!item) return;
        const list = trigger.closest('[data-smart-stock-list]');
        origin = { trigger, list };
        restoreList = false;
        back.hidden = !list;
        populate(item);
        const showDetail = () => bootstrap.Modal.getOrCreateInstance(detail).show(trigger);
        if (list) {
            list.addEventListener('hidden.bs.modal', showDetail, { once: true });
            bootstrap.Modal.getOrCreateInstance(list).hide();
        } else {
            showDetail();
        }
    });

    back.addEventListener('click', () => {
        if (!origin?.list) return;
        restoreList = true;
        bootstrap.Modal.getOrCreateInstance(detail).hide();
    });

    detail.addEventListener('hidden.bs.modal', () => {
        if (!origin) return;
        const { list, trigger } = origin;
        if (restoreList) {
            list.addEventListener('shown.bs.modal', () => trigger.focus(), { once: true });
            bootstrap.Modal.getOrCreateInstance(list).show(listOpeners.get(list));
        } else {
            (list ? listOpeners.get(list) : trigger)?.focus();
        }
        origin = null;
    });
})();
