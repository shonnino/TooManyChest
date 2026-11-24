const el = id => document.getElementById(id);
const number = new Intl.NumberFormat('it-IT');
const date = new Intl.DateTimeFormat('it-IT', { dateStyle: 'short', timeStyle: 'short' });
const show = (id, visible) => { el(id).hidden = !visible; };
const put = (id, value) => { el(id).textContent = value; };
let active = 0, requestVersion = 0, drawnVersion = 0, handoff, checking = false, joining = false, joinAttempted = false;
const countFields = ['count-ammo', 'count-food', 'count-cannons', 'count-manual'];
let countingDirty = false;
const pathCode = location.pathname.match(/^\/teamers\/join\/([a-f0-9]{32})\/?$/)?.[1];
let pendingInvite = pathCode ?? new URLSearchParams(location.search).get('invite');

async function request(action, data) {
  const response = await fetch('/api/web-teamers/' + action, data === undefined ? {} : {
    method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(data),
  });
  const result = await response.json();
  if (!response.ok) throw new Error(result.error ?? 'Teamers non è disponibile.');
  return result;
}

function notice(message) {
  put('notice', message || '');
  show('notice', !!message);
}

function node(tag, text, className) {
  const element = document.createElement(tag);
  if (text) element.textContent = text;
  if (className) element.className = className;
  return element;
}

function row(left, right) {
  const item = node('li');
  item.append(node('strong', left), typeof right === 'string' ? node('small', right) : right);
  return item;
}

function render(view, version) {
  if (version < drawnVersion) return;
  drawnVersion = version;
  const { state, linked, synchronizedAt } = view;
  show('sign-out', !!state);
  show('welcome', !state);
  show('main-view', !!state);
  if (!state) {
    if (pendingInvite) {
      if (pathCode && location.pathname !== '/teamers') history.replaceState(null, '', '/teamers?invite=' + pendingInvite);
      put('welcome-title', 'Collega ChestCounter per entrare');
      put('welcome-text', 'Dopo aver collegato l’app, Teamers userà questo invito per farti entrare nella squadra.');
    }
    return;
  }
  const { team, invitations, personal, counting } = state;
  put('device-title', state.username);
  put('device-state', synchronizedAt ? 'Ultima sincronizzazione: ' + date.format(synchronizedAt)
    : linked ? 'App collegata. Apri ChestCounter con un profilo che ha almeno un replay completato.' : 'ChestCounter da collegare.');
  show('personal-view', !!personal);
  show('counting', !!counting?.ready);
  show('counting-pending', !!personal && !counting?.ready);
  const own = team?.members.find(member => member.playerKey === team.playerKey);
  show('my-contribution', !!own);
  if (own) put('my-contribution', number.format(own.initialChests + own.sharedChests) + ' nella tua quota della squadra');
  if (personal) {
    put('personal-total', number.format(personal.total));
  }
  if (!countingDirty && counting?.ready) {
    setCounting(counting.options);
    el('count-starting').value = counting.starting;
  }
  globalThis.renderTeamersActivity?.(state);
  show('create', !team);
  show('score', !!team);
  show('member-panel', !!team);
  show('no-invites', invitations.length === 0);
  el('create-button').disabled = !synchronizedAt;
  if (!team) put('team-name', 'Crea una squadra');
  if (team) {
    put('team-name', team.name);
    put('total', number.format(team.initialChests + team.sharedChests));
    put('initial', number.format(team.initialChests));
    put('shared', number.format(team.sharedChests));
    put('matches', number.format(team.sharedMatches));
    show('invite-box', team.canInvite && team.members.length < 4);
    el('members').replaceChildren(...team.members.map(member => row(member.username,
      number.format(member.initialChests) + ' iniziali · +' + number.format(member.sharedChests) + ' insieme')));
  }
  el('invitations').replaceChildren(...invitations.map(invite => {
    const actions = node('div', null, 'invite-actions');
    const accept = node('button', 'Accetta');
    const decline = node('button', 'Rifiuta', 'secondary');
    accept.disabled = !!team || !synchronizedAt;
    accept.title = team ? 'Fai già parte di una squadra.' : synchronizedAt ? '' : 'Collega prima ChestCounter.';
    accept.onclick = () => act('respond', { invitationId: invite.id, accept: true }, 'Invito accettato.');
    decline.onclick = () => act('respond', { invitationId: invite.id, accept: false }, 'Invito rifiutato.');
    actions.append(accept, decline);
    return row(invite.teamName + ' · ' + invite.fromUsername, actions);
  }));
  if (pendingInvite && !joinAttempted) void joinInvitation();
}

function setCounting(options) {
  const masks = [1, 2, 4, 16];
  countFields.forEach((field, index) => { el(field).checked = !!(options & masks[index]); });
}

async function act(action, data, success, onResult) {
  if (active) return false;
  active++;
  const version = ++requestVersion;
  notice(null);
  try {
    const result = await request(action, data);
    if (onResult) onResult(result);
    else render(result, version);
    notice(success);
    return true;
  } catch (failure) {
    notice(failure.message);
    return false;
  } finally {
    active--;
  }
}

async function joinInvitation() {
  if (joining || joinAttempted) return;
  joining = joinAttempted = true;
  try {
    const version = ++requestVersion;
    const result = await request('join', { code: pendingInvite });
    pendingInvite = null;
    history.replaceState(null, '', '/teamers');
    render(result, version);
    notice('Ora fai parte della squadra.');
  } catch (failure) {
    notice(failure.message);
  } finally {
    joining = false;
  }
}

async function checkConnection() {
  if (!handoff || active || checking || document.hidden) return;
  checking = true;
  try {
    if (Date.now() >= handoff.expiresAt) throw new Error('Il codice è scaduto. Premi «Collega all’app» per generarne un altro.');
    const version = ++requestVersion;
    const result = await request('connect/finish', {});
    if (result.waiting) return;
    handoff = null;
    el('connect-code').value = '';
    show('connect-box', false);
    notice('ChestCounter collegato.');
    render(result, version);
  } catch (failure) {
    handoff = null;
    show('connect-box', false);
    notice(failure.message);
  } finally {
    checking = false;
  }
}

function form(id, action, success) {
  el(id).addEventListener('submit', async event => {
    event.preventDefault();
    if (await act(action, Object.fromEntries(new FormData(event.currentTarget)), success)) el(id).reset();
  });
}

async function refresh() {
  if (document.hidden || active || handoff || joining) return;
  const version = ++requestVersion;
  try { render(await request('session'), version); }
  catch { if (!drawnVersion) notice('Teamers non è disponibile. Ricarica la pagina tra poco.'); }
}

form('team-form', 'teams', 'Squadra creata.');
el('counting-form').addEventListener('input', () => { countingDirty = true; });
el('counting-form').addEventListener('submit', event => {
  event.preventDefault();
  const masks = [1, 2, 4, 16];
  const options = countFields.reduce((mask, field, index) => mask | (el(field).checked ? masks[index] : 0), 0);
  const starting = Number(el('count-starting').value);
  void act('counting', { options, starting }, 'Opzioni Teamers salvate.', view => {
    countingDirty = false;
    render(view, ++requestVersion);
  });
});
el('connect').onclick = () => act('connect/start', {}, 'Codice pronto. Apri ChestCounter o copia il codice.', result => {
  handoff = result;
  el('connect-code').value = result.code;
  el('open-app').href = 'chestcounter-teamers://connect/' + result.code;
  show('connect-box', true);
});
el('open-app').onclick = () => {
  notice('Conferma in ChestCounter. Se la finestra non si apre, copia il codice e incollalo nell’app.');
};
el('sign-out').onclick = () => act('disconnect', {}, 'Browser scollegato.', result => {
  joinAttempted = false;
  countingDirty = false;
  show('invite-link-box', false);
  el('share-link').value = '';
  render(result, ++requestVersion);
});
el('invite-link').onclick = () => act('invite-link', {}, 'Link pronto da condividere.', result => {
  el('share-link').value = result.link;
  show('invite-link-box', true);
});
function copyFrom(field, message) {
  return async () => {
    try { await navigator.clipboard.writeText(el(field).value); notice(message); }
    catch { el(field).select(); notice('Premi Ctrl+C per copiare.'); }
  };
}
el('copy-code').onclick = copyFrom('connect-code', 'Codice copiato. Incollalo in ChestCounter → Teamers → Collega al sito.');
el('copy-invite').onclick = copyFrom('share-link', 'Link copiato.');
document.addEventListener('visibilitychange', () => {
  if (handoff) void checkConnection();
  else void refresh();
});
setInterval(checkConnection, 2500);
setInterval(refresh, 20000);
refresh();
