(() => {
  const el = id => document.getElementById(id);
  const chart = el('activity-canvas');
  const chartDetail = el('chart-detail');
  const number = new Intl.NumberFormat('it-IT');
  const decimal = new Intl.NumberFormat('it-IT', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
  const date = new Intl.DateTimeFormat('it-IT', { dateStyle: 'medium', timeStyle: 'short' });
  const day = new Intl.DateTimeFormat('it-IT', { day: 'numeric', month: 'short' });
  const colors = ['#a87af6', '#e5ceff', '#d4fb85'];
  let state, selected, byDay = false, points = [], mean;
  const total = point => point.opened + point.broken + point.extra;
  const dayKey = at => {
    const value = new Date(at);
    return new Date(value.getFullYear(), value.getMonth(), value.getDate()).getTime();
  };
  const put = (name, value, format = number) => { el('activity-' + name).textContent = value == null ? '—' : format.format(value); };
  const press = (name, enabled) => el(name).setAttribute('aria-pressed', String(enabled));

  function render() {
    const personal = state?.personal?.activity;
    const team = state?.team?.activity;
    el('activity-panel').hidden = !state?.team && !personal;
    el('activity-team').disabled = !state?.team;
    el('activity-personal').disabled = !personal;
    const showTeam = selected === 'team' ? !!state?.team : selected === 'personal' ? !personal : !!team?.matches || !personal;
    const activity = showTeam ? team : personal;
    press('activity-team', showTeam);
    press('activity-personal', !showTeam);
    press('chart-match', !byDay);
    press('chart-day', byDay);
    const recent = activity?.recent ?? [];
    el('activity-split').textContent = activity?.matches ? number.format(activity.opened) + ' · ' + number.format(activity.broken) : '—';
    put('average', activity?.average, decimal);
    put('best', activity?.best);
    el('activity-best').title = activity?.bestAt ? date.format(activity.bestAt) : '';
    put('rate', activity?.perMinute, decimal);
    put('last', activity?.last);
    put('today', activity ? recent.filter(point => dayKey(point.at) === dayKey(Date.now())).reduce((sum, point) => sum + total(point), 0) : null);
    put('treasures', activity?.treasures);
    put('matches', activity?.matches);
    points = recent;
    if (byDay) {
      const grouped = new Map();
      for (const point of recent) {
        const at = dayKey(point.at);
        const group = grouped.get(at) ?? { at, opened: 0, broken: 0, extra: 0 };
        group.opened += point.opened;
        group.broken += point.broken;
        group.extra += point.extra;
        grouped.set(at, group);
      }
      points = [...grouped.values()];
    }
    mean = byDay ? null : activity?.average;
    el('chart-title').textContent = byDay ? 'Chest per giorno' : 'Chest per partita';
    el('chart-content').hidden = points.length === 0;
    el('chart-empty').hidden = points.length > 0;
    chartDetail.textContent = 'Dati delle ultime ' + number.format(recent.length) + (recent.length === 1 ? ' partita.' : ' partite.');
    draw();
  }

  function draw() {
    if (points.length === 0) return;
    const { width, height } = chart.getBoundingClientRect();
    if (!width || !height) return;
    const ratio = window.devicePixelRatio || 1;
    chart.width = Math.round(width * ratio);
    chart.height = Math.round(height * ratio);
    const ctx = chart.getContext('2d');
    ctx.scale(ratio, ratio);
    const floor = height - 27;
    const peak = Math.max(1, mean ?? 0, ...points.map(total));
    const slot = (width - 52) / points.length;
    const plot = floor - 14;
    const centerX = index => 42 + index * slot + slot / 2;
    ctx.font = '12px system-ui';
    ctx.fillStyle = '#baafcd';
    for (const fraction of [0, .5, 1]) {
      const y = floor - plot * fraction;
      ctx.fillStyle = '#3a304b';
      ctx.fillRect(40, y, width - 48, 1);
      ctx.fillStyle = '#baafcd';
      ctx.fillText(number.format(Math.round(peak * fraction)), 4, y + 4);
    }
    points.forEach((point, index) => {
      let y = floor;
      const size = Math.min(44, Math.max(1, slot - 2));
      [point.opened, point.broken, point.extra].forEach((value, segment) => {
        const extent = value / peak * plot;
        y -= extent;
        ctx.fillStyle = colors[segment];
        if (extent) ctx.fillRect(centerX(index) - size / 2, y, size, extent);
      });
    });
    if (mean != null) {
      const y = floor - (mean / peak) * plot;
      ctx.strokeStyle = '#e5ceff';
      ctx.setLineDash([4, 5]);
      ctx.beginPath(); ctx.moveTo(40, y); ctx.lineTo(width - 8, y); ctx.stroke();
    }
    ctx.fillStyle = '#baafcd';
    ctx.fillText(day.format(points[0].at), 42, height - 8);
    if (points.length > 1) {
      const label = day.format(points[points.length - 1].at);
      ctx.fillText(label, width - 10 - ctx.measureText(label).width, height - 8);
    }
  }

  chart.addEventListener('pointermove', event => {
    if (!points.length) return;
    const idx = Math.floor((event.offsetX - 42) * points.length / (chart.clientWidth - 52));
    const point = points[Math.max(0, Math.min(idx, points.length - 1))];
    chartDetail.textContent = (byDay ? day : date).format(point.at) + ' · ' +
      number.format(total(point)) + ' totali · ' + number.format(point.opened) + ' aperte · ' +
      number.format(point.broken) + ' distrutte · ' + number.format(point.extra) + ' extra o storiche';
  });
  chart.addEventListener('pointerleave', () => {
    const recent = (el('activity-team').getAttribute('aria-pressed') === 'true' ? state?.team?.activity : state?.personal?.activity)?.recent ?? [];
    chartDetail.textContent = 'Dati delle ultime ' + number.format(recent.length) + (recent.length === 1 ? ' partita.' : ' partite.');
  });
  el('activity-team').onclick = () => { selected = 'team'; render(); };
  el('activity-personal').onclick = () => { selected = 'personal'; render(); };
  el('chart-match').onclick = () => { byDay = false; render(); };
  el('chart-day').onclick = () => { byDay = true; render(); };
  new ResizeObserver(draw).observe(chart);
  globalThis.renderTeamersActivity = value => { state = value; render(); };
})();
