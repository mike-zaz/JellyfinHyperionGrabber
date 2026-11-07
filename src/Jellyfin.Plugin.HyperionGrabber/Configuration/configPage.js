// Configuration page controller. Loaded by Jellyfin via data-controller="__plugin/HyperionGrabberJs".
// Uses only the globals every supported Jellyfin web client provides: ApiClient and Dashboard.

const HyperionGrabberConfig = {
    pluginUniqueId: '501879a2-6653-4450-b66c-73ba37aa6e3f',
    minPriority: 100,
    maxPriority: 199,
    defaultFramesPerSecond: 25,
    maxFramesPerSecond: 60,
    maxLatencyOffsetMilliseconds: 2000,
    defaultPauseReleaseSeconds: 15,
    maxPauseReleaseSeconds: 3600,
    defaultKodiPort: 9090,
    defaultPort: 19400,
    defaultPriority: 150,
    maxTargets: 8,
    statusRefreshMilliseconds: 2000,
    previewRefreshMilliseconds: 250
};

const noDevicesText = 'No devices yet. Start playing something on your TV, then press Refresh.';

// Shown under a selected device. Static markup so Jellyfin upgrades the emby-input.
const deviceOffsetMarkup = `
<input is="emby-input" type="number" class="deviceOffset" label="Light timing offset for this device (ms)" min="-2000" max="2000" step="10" placeholder="Empty: the general offset" />
<div class="fieldDescription">Only if this TV needs another value than the general light timing offset.</div>`;

// One more Hyperion server. Static markup only (names and hosts are set as values afterwards), so Jellyfin upgrades
// the emby elements; emby-input creates each label from its label attribute.
const targetCardMarkup = `
<h4 class="targetTitle"></h4>
<div class="inputContainer">
    <input is="emby-input" type="text" class="targetName" label="Name (optional)" maxlength="100" autocomplete="off" placeholder="Bedroom" />
    <div class="fieldDescription">Shown in the status, for example the room.</div>
</div>
<div class="inputContainer">
    <input is="emby-input" type="text" class="targetHost" label="Host" autocomplete="off" placeholder="192.168.1.21" />
    <div class="fieldDescription">The Hyperion.ng or HyperHDR server of this TV.</div>
</div>
<button is="emby-button" type="button" class="raised button-alt block targetDiscover"><span>Find Hyperion servers</span></button>
<div class="fieldDescription targetDiscoverResult" role="status" aria-live="polite" hidden></div>
<div class="inputContainer">
    <input is="emby-input" type="number" class="targetPort" label="FlatBuffers port" min="1" max="65535" />
</div>
<div class="inputContainer">
    <input is="emby-input" type="number" class="targetPriority" label="Priority" min="100" max="199" />
</div>
<button is="emby-button" type="button" class="raised button-alt block targetTestConnection"><span>Test connection</span></button>
<button is="emby-button" type="button" class="raised button-alt block targetTestPattern"><span>Send test pattern (8 s)</span></button>
<div class="fieldDescription targetResult" role="status" aria-live="polite" hidden></div>
<h4>Devices</h4>
<div class="checkboxList targetDevices"></div>
<div class="fieldDescription">Playback on these devices drives this server.</div>
<button is="emby-button" type="button" class="raised button-alt block targetRemove"><span>Remove this Hyperion server</span></button>`;

function readInteger(view, id) {
    return Number.parseInt(view.querySelector(id).value, 10);
}

// 0 is a valid saved value for these settings, so only fall back to the default when the value is missing.
function savedOrDefault(value, fallback) {
    return Number.isInteger(value) ? value : fallback;
}

function validateTiming(offset, pauseRelease) {
    const maxOffset = HyperionGrabberConfig.maxLatencyOffsetMilliseconds;
    if (!Number.isInteger(offset) || offset < -maxOffset || offset > maxOffset) {
        return `Light timing offset must be between -${maxOffset} and ${maxOffset} ms.`;
    }

    if (!Number.isInteger(pauseRelease) || pauseRelease < 0 || pauseRelease > HyperionGrabberConfig.maxPauseReleaseSeconds) {
        return `Release after pause must be between 0 and ${HyperionGrabberConfig.maxPauseReleaseSeconds} seconds.`;
    }

    return null;
}

function validateKodiPort(port) {
    if (!Number.isInteger(port) || port < 1 || port > 65535) {
        return 'Kodi remote control port must be between 1 and 65535.';
    }

    return null;
}

function readFramesPerSecond(view) {
    return Number.parseInt(view.querySelector('#FramesPerSecond').value, 10);
}

function validateFramesPerSecond(fps) {
    if (!Number.isInteger(fps) || fps < 1 || fps > HyperionGrabberConfig.maxFramesPerSecond) {
        return `Frame rate must be between 1 and ${HyperionGrabberConfig.maxFramesPerSecond}.`;
    }

    return null;
}

function readTarget(view) {
    return {
        Name: view.querySelector('#HyperionName').value.trim(),
        Host: view.querySelector('#HyperionHost').value.trim(),
        Port: Number.parseInt(view.querySelector('#HyperionPort').value, 10),
        Priority: Number.parseInt(view.querySelector('#HyperionPriority').value, 10)
    };
}

function readCardTarget(card) {
    return {
        Name: card.querySelector('.targetName').value.trim(),
        Host: card.querySelector('.targetHost').value.trim(),
        Port: Number.parseInt(card.querySelector('.targetPort').value, 10),
        Priority: Number.parseInt(card.querySelector('.targetPriority').value, 10),
        Devices: readChoices(card.querySelector('.targetDevices'))
    };
}

function targetCards(view) {
    return Array.from(view.querySelector('#AdditionalTargetList').querySelectorAll('.hyperionTarget'));
}

// Two targets with the same server and priority would fight over it.
function findDuplicateTarget(targets) {
    const seen = new Set();
    for (const target of targets) {
        const key = `${target.Host.toLowerCase()}|${target.Port}|${target.Priority}`;
        if (seen.has(key)) {
            return `Two Hyperion servers use ${target.Host}:${target.Port} with priority ${target.Priority}. Give one of them another priority, or remove it.`;
        }

        seen.add(key);
    }

    return null;
}

function validateTarget(target) {
    if (!target.Host) {
        return 'Enter the host name or IP address of your Hyperion server.';
    }

    if (!Number.isInteger(target.Port) || target.Port < 1 || target.Port > 65535) {
        return 'Port must be between 1 and 65535.';
    }

    if (!Number.isInteger(target.Priority) || target.Priority < HyperionGrabberConfig.minPriority || target.Priority > HyperionGrabberConfig.maxPriority) {
        return `Priority must be between ${HyperionGrabberConfig.minPriority} and ${HyperionGrabberConfig.maxPriority}.`;
    }

    return null;
}

function showResult(result, success, message) {
    result.textContent = (success ? '✔ ' : '✖ ') + message;
    result.hidden = false;
}

function showAdditionalTargetResult(view, message) {
    const result = view.querySelector('#AdditionalTargetResult');
    result.textContent = message || '';
    result.hidden = !message;
}

function showPlaybackResult(view, message) {
    const result = view.querySelector('#PlaybackResult');
    result.textContent = message || '';
    result.hidden = !message;
}

function setBusy(buttons, busy) {
    for (const button of buttons) {
        button.disabled = busy;
    }
}

async function postAction(path, body) {
    try {
        return await ApiClient.ajax({
            type: 'POST',
            url: ApiClient.getUrl(path),
            data: JSON.stringify(body),
            contentType: 'application/json',
            dataType: 'json'
        });
    } catch (error) {
        // ApiClient rejects with the fetch Response for HTTP errors; our 400 responses carry a message.
        if (error && typeof error.json === 'function') {
            try {
                const payload = await error.json();
                return { Success: false, Message: payload.Message || payload.title || `Request failed (${error.status}).` };
            } catch {
                return { Success: false, Message: `Request failed (${error.status}).` };
            }
        }

        return { Success: false, Message: 'Request failed. Check that the Jellyfin server is reachable.' };
    }
}

async function runAction(target, result, buttons, path, extra, pendingMessage) {
    const problem = validateTarget(target);
    if (problem) {
        showResult(result, false, problem);
        return;
    }

    setBusy(buttons, true);
    showResult(result, true, pendingMessage);
    try {
        const response = await postAction(path, Object.assign({ Host: target.Host, Port: target.Port, Priority: target.Priority }, extra));
        showResult(result, response.Success, response.Message);
    } finally {
        setBusy(buttons, false);
    }
}

function wireTestButtons(readTargetNow, result, testConnection, testPattern) {
    const buttons = [testConnection, testPattern];
    testConnection.addEventListener('click', function () {
        runAction(readTargetNow(), result, buttons, 'HyperionGrabber/TestConnection', {}, 'Connecting…');
    });
    testPattern.addEventListener('click', function () {
        runAction(readTargetNow(), result, buttons, 'HyperionGrabber/TestPattern', { DurationSeconds: 8 }, 'Sending the test pattern for 8 seconds… look at the TV.');
    });
}

// Searches the network and lists each server with a Use button that hands it to apply(server). Names come from devices
// on the network, so they are only ever set as text.
function wireDiscoverButton(button, result, apply) {
    button.addEventListener('click', async function () {
        button.disabled = true;
        result.hidden = false;
        result.textContent = 'Searching the network for Hyperion.ng and HyperHDR…';
        try {
            const response = await postAction('HyperionGrabber/Discover', {});
            const servers = (response && response.Servers) || [];
            if (servers.length === 0) {
                result.textContent = response && response.Success === false
                    ? '✖ ' + response.Message
                    : '✖ No server answered. Check that Hyperion runs on this network; with Jellyfin in Docker the search needs host networking. You can always enter the address yourself.';
                return;
            }

            result.replaceChildren(...servers.map(server => {
                const row = document.createElement('div');
                row.innerHTML = '<span></span> <button is="emby-button" type="button" class="raised button-alt"><span>Use</span></button>';
                row.querySelector('span').textContent = `${server.Kind} “${server.Name}” at ${server.Host}:${server.Port}`;
                row.querySelector('button').addEventListener('click', function () {
                    apply(server);
                    result.textContent = `✔ Using ${server.Host}:${server.Port}. Press Save to keep it.`;
                });
                return row;
            }));
        } finally {
            button.disabled = false;
        }
    });
}

// Numbers the cards after the first server and allows adding only up to the limit.
function updateTargetCards(view) {
    const cards = targetCards(view);
    cards.forEach((card, index) => {
        card.querySelector('.targetTitle').textContent = `Hyperion server ${index + 2}`;
    });
    view.querySelector('#AddTarget').disabled = cards.length + 1 >= HyperionGrabberConfig.maxTargets;
}

function addTargetCard(view, target) {
    const card = document.createElement('div');
    card.className = 'verticalSection hyperionTarget';
    card.innerHTML = targetCardMarkup;
    view.querySelector('#AdditionalTargetList').append(card);
    card.querySelector('.targetName').value = target.Name || '';
    card.querySelector('.targetHost').value = target.Host || '';
    card.querySelector('.targetPort').value = target.Port || HyperionGrabberConfig.defaultPort;
    card.querySelector('.targetPriority').value = target.Priority || HyperionGrabberConfig.defaultPriority;
    renderChoices(card.querySelector('.targetDevices'), view.hyperionOfferedDevices || [], target.Devices || [], noDevicesText, true);
    wireTestButtons(() => readCardTarget(card), card.querySelector('.targetResult'), card.querySelector('.targetTestConnection'), card.querySelector('.targetTestPattern'));
    wireDiscoverButton(card.querySelector('.targetDiscover'), card.querySelector('.targetDiscoverResult'), function (server) {
        card.querySelector('.targetHost').value = server.Host;
        card.querySelector('.targetPort').value = server.Port;
        if (!card.querySelector('.targetName').value.trim()) {
            card.querySelector('.targetName').value = server.Name;
        }
    });
    card.querySelector('.targetRemove').addEventListener('click', function () {
        card.remove();
        updateTargetCards(view);
    });
    updateTargetCards(view);
    return card;
}

// Jellyfin may format GUIDs with or without dashes; compare them normalized.
function normalizeId(id) {
    return String(id || '').replace(/-/g, '').toLowerCase();
}

// Renders one checkbox per entry: everything Jellyfin offers plus saved selections it no longer lists. Device lists
// (withOffset) also get a timing offset box under each selected device.
// Names come from Jellyfin clients, so they are only ever set as text.
function renderChoices(list, offered, selected, emptyText, withOffset = false) {
    const selectedById = new Map(selected.map(s => [normalizeId(s.Id), s]));
    const entries = offered.map(o => ({ Id: o.Id, Name: o.Name, Detail: o.Detail }));
    const offeredIds = new Set(entries.map(e => normalizeId(e.Id)));
    for (const saved of selected) {
        if (!offeredIds.has(normalizeId(saved.Id))) {
            entries.push({ Id: saved.Id, Name: saved.Name || saved.Id, Detail: 'not seen recently' });
        }
    }

    list.replaceChildren();
    if (entries.length === 0) {
        const empty = document.createElement('div');
        empty.className = 'fieldDescription';
        empty.textContent = emptyText;
        list.append(empty);
        return;
    }

    for (const entry of entries) {
        // Static markup so Jellyfin upgrades the emby-checkbox (its polyfill breaks createElement(name, { is })).
        const wrapper = document.createElement('div');
        wrapper.innerHTML = '<label><input is="emby-checkbox" type="checkbox" /><span></span></label>';
        const label = wrapper.firstElementChild;
        const input = label.querySelector('input');
        input.dataset.id = entry.Id;
        input.dataset.name = entry.Name;
        const saved = selectedById.get(normalizeId(entry.Id));
        input.checked = saved !== undefined;
        label.querySelector('span').textContent = entry.Detail ? `${entry.Name} (${entry.Detail})` : entry.Name;
        list.append(label);
        if (withOffset) {
            const offset = document.createElement('div');
            offset.className = 'inputContainer deviceOffsetContainer';
            offset.innerHTML = deviceOffsetMarkup;
            const savedOffset = saved ? saved.LatencyOffsetMilliseconds : null;
            const box = offset.querySelector('.deviceOffset');
            box.value = Number.isInteger(savedOffset) ? savedOffset : '';
            // Hidden and disabled together: a disabled box is left out of the form's validation, so a value left in
            // an unticked device's box cannot block Save without a visible message.
            const showOffset = () => {
                offset.hidden = !input.checked;
                box.disabled = !input.checked;
            };
            showOffset();
            input.addEventListener('change', showOffset);
            list.append(offset);
        }
    }
}

// The offset box of a device follows its checkbox's label (see renderChoices).
function offsetBoxOf(input) {
    const next = input.closest('label').nextElementSibling;
    return next && next.classList.contains('deviceOffsetContainer') ? next.querySelector('.deviceOffset') : null;
}

function readChoices(list) {
    return Array.from(list.querySelectorAll('input[type="checkbox"]'))
        .filter(input => input.checked)
        .map(input => {
            const choice = { Id: input.dataset.id, Name: input.dataset.name };
            const box = offsetBoxOf(input);
            if (box) {
                const text = box.value.trim();
                choice.LatencyOffsetMilliseconds = text === '' ? null : Number(text);
            }

            return choice;
        });
}

// Returns a message for the first device whose own timing offset is not a whole number in range.
function validateDeviceOffsets(devices) {
    const maxOffset = HyperionGrabberConfig.maxLatencyOffsetMilliseconds;
    for (const device of devices) {
        const offset = device.LatencyOffsetMilliseconds;
        if (offset !== null && offset !== undefined && (!Number.isInteger(offset) || offset < -maxOffset || offset > maxOffset)) {
            return `The light timing offset of ${device.Name || 'a device'} must be empty or between -${maxOffset} and ${maxOffset} ms.`;
        }
    }

    return null;
}

function describeDevice(device) {
    return device.UserName ? `${device.Client}, ${device.UserName}` : device.Client;
}

async function loadClients(view, selectedDevices, selectedUsers) {
    let clients = { Devices: [], Users: [] };
    try {
        clients = await ApiClient.ajax({ type: 'GET', url: ApiClient.getUrl('HyperionGrabber/Clients'), dataType: 'json' });
        showPlaybackResult(view, null);
    } catch {
        showPlaybackResult(view, '✖ Could not load the devices and users from Jellyfin. Saved selections are still shown.');
    }

    const offeredDevices = (clients.Devices || []).map(d => ({ Id: d.Id, Name: d.Name, Detail: describeDevice(d) }));
    view.hyperionOfferedDevices = offeredDevices;
    renderChoices(view.querySelector('#PlaybackDeviceList'), offeredDevices, selectedDevices, noDevicesText, true);
    for (const card of targetCards(view)) {
        const list = card.querySelector('.targetDevices');
        renderChoices(list, offeredDevices, readChoices(list), noDevicesText, true);
    }

    renderChoices(
        view.querySelector('#PlaybackUserList'),
        (clients.Users || []).map(u => ({ Id: u.Id, Name: u.Name })),
        selectedUsers,
        'No users yet.');
}

async function loadLibraries(view, excludedLibraries) {
    let libraries = { Libraries: [] };
    try {
        libraries = await ApiClient.ajax({ type: 'GET', url: ApiClient.getUrl('HyperionGrabber/Libraries'), dataType: 'json' });
    } catch {
        showPlaybackResult(view, '✖ Could not load the libraries from Jellyfin. Saved selections are still shown.');
    }

    renderChoices(
        view.querySelector('#ExcludedLibraryList'),
        (libraries.Libraries || []).map(l => ({ Id: l.Id, Name: l.Name })),
        excludedLibraries,
        'No libraries yet.');
}

function formatPosition(seconds) {
    const total = Math.max(0, Math.floor(seconds));
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const pad = value => String(value).padStart(2, '0');
    return hours > 0 ? `${hours}:${pad(minutes)}:${pad(total % 60)}` : `${minutes}:${pad(total % 60)}`;
}

function describeStatus(status) {
    const lines = [];
    if (status.DeviceName) {
        const client = status.Client ? ` (${status.Client})` : '';
        lines.push(`Playing: ${status.ItemName || 'an item Jellyfin no longer lists'} on ${status.DeviceName}${client}`);
    }

    if (typeof status.PositionSeconds === 'number') {
        const speed = typeof status.PlaybackSpeed === 'number' ? `, at ${status.PlaybackSpeed}× speed` : '';
        lines.push(`Position: ${formatPosition(status.PositionSeconds)} (±${status.PositionUncertaintySeconds.toFixed(1)} s)${speed}`);
    }

    if (status.Timing) {
        lines.push(`Timing: ${status.Timing}`);
    }

    if (status.LightOffset) {
        lines.push(`Light timing offset: ${status.LightOffset}`);
    }

    if (typeof status.FramesPerSecond === 'number') {
        lines.push(`Pictures: ${status.FramesPerSecond.toFixed(1)} of ${status.TargetFramesPerSecond} per second; `
            + `${status.FramesSent} sent, ${status.FramesDropped} dropped, ${status.DecoderRestarts} decoder restarts`);
    }

    if (status.Decoder) {
        lines.push(`Decoder: ${status.Decoder}${status.HardwareFallback ? ' (hardware decoding failed, see the log)' : ''}`);
    }

    if (status.Picture) {
        lines.push(`Picture: ${status.Picture}`);
    }

    if (status.LastProblem) {
        const time = status.LastProblemTime ? ` (${new Date(status.LastProblemTime).toLocaleString()})` : '';
        lines.push(`Last problem${time}: ${status.LastProblem}`);
    }

    return lines;
}

function listItem(text) {
    const item = document.createElement('li');
    item.textContent = text;
    return item;
}

// Device and item names come from Jellyfin clients, so status values are only ever set as text.
// With one Hyperion server the panel looks as it always did; with several, each gets its own entry.
function renderStatus(view, status) {
    const targets = status.Targets || [];
    view.hyperionTargetNames = targets.map(target => target.Name);
    const single = targets.length === 1 ? targets[0] : null;
    view.querySelector('#HyperionStatusSummary').textContent = status.Summary;
    const problem = view.querySelector('#HyperionStatusProblem');
    problem.textContent = single && single.Problem ? '✖ ' + single.Problem : '';
    problem.hidden = !problem.textContent;
    const details = view.querySelector('#HyperionStatusDetails');
    if (targets.length <= 1) {
        details.replaceChildren(...(single ? describeStatus(single) : []).map(listItem));
        return;
    }

    details.replaceChildren(...targets.map(target => {
        const item = document.createElement('li');
        const name = document.createElement('b');
        name.textContent = `${target.Name}: `;
        item.append(name, document.createTextNode(target.Summary));
        const lines = describeStatus(target);
        if (target.Problem) {
            lines.unshift('✖ ' + target.Problem);
        }

        if (lines.length > 0) {
            const sublist = document.createElement('ul');
            sublist.replaceChildren(...lines.map(listItem));
            item.append(sublist);
        }

        return item;
    }));
}

async function loadStatus(view) {
    try {
        renderStatus(view, await ApiClient.ajax({ type: 'GET', url: ApiClient.getUrl('HyperionGrabber/Status'), dataType: 'json' }));
    } catch {
        renderStatus(view, { Summary: 'Could not read the status from the Jellyfin server.' });
    }
}

// Polls only while the page is shown and the browser tab is visible; one request at a time.
function startStatusRefresh(view) {
    stopStatusRefresh(view);
    const refresh = { active: true, loading: false, timer: 0 };
    const tick = async () => {
        refresh.timer = 0;
        if (!refresh.active || refresh.loading || document.hidden) {
            return;
        }

        refresh.loading = true;
        try {
            await loadStatus(view);
        } finally {
            refresh.loading = false;
        }

        if (refresh.active && !document.hidden) {
            refresh.timer = setTimeout(tick, HyperionGrabberConfig.statusRefreshMilliseconds);
        }
    };
    refresh.onVisibilityChange = () => {
        if (!document.hidden && !refresh.timer) {
            tick();
        }
    };
    document.addEventListener('visibilitychange', refresh.onVisibilityChange);
    view.hyperionStatusRefresh = refresh;
    tick();
}

function stopStatusRefresh(view) {
    const refresh = view.hyperionStatusRefresh;
    if (!refresh) {
        return;
    }

    refresh.active = false;
    clearTimeout(refresh.timer);
    document.removeEventListener('visibilitychange', refresh.onVisibilityChange);
    view.hyperionStatusRefresh = null;
}

// Draws a picture from the preview endpoint (RGB24 as base64) onto a canvas.
function drawPreview(canvas, preview) {
    const bytes = atob(preview.Pixels);
    canvas.width = preview.Width;
    canvas.height = preview.Height;
    const context = canvas.getContext('2d');
    const image = context.createImageData(preview.Width, preview.Height);
    for (let source = 0, target = 0; source + 2 < bytes.length; source += 3, target += 4) {
        image.data[target] = bytes.charCodeAt(source);
        image.data[target + 1] = bytes.charCodeAt(source + 1);
        image.data[target + 2] = bytes.charCodeAt(source + 2);
        image.data[target + 3] = 255;
    }

    context.putImageData(image, 0, 0);
}

// One entry per Hyperion server: a caption and a canvas.
function previewEntry(list, index) {
    let entry = list.children[index];
    if (!entry) {
        entry = document.createElement('div');
        entry.innerHTML = '<div class="fieldDescription"></div><canvas></canvas>';
        const canvas = entry.querySelector('canvas');
        canvas.style.width = '100%';
        canvas.style.maxWidth = '480px';
        list.append(entry);
    }

    return entry;
}

// Server names come from the configuration, so they are only ever set as text.
async function loadPreviews(view) {
    const names = view.hyperionTargetNames || [];
    const count = Math.max(1, names.length);
    const list = view.querySelector('#PreviewList');
    while (list.children.length > count) {
        list.lastElementChild.remove();
    }

    for (let index = 0; index < count; index++) {
        const entry = previewEntry(list, index);
        const caption = entry.querySelector('div');
        const canvas = entry.querySelector('canvas');
        const url = ApiClient.getUrl('HyperionGrabber/Preview');
        let preview = null;
        try {
            preview = await ApiClient.ajax({ type: 'GET', url: `${url}${url.includes('?') ? '&' : '?'}target=${index}`, dataType: 'json' });
        } catch {
            preview = null;
        }

        const several = names.length > 1;
        if (preview && preview.Available) {
            caption.textContent = several ? names[index] : '';
            caption.hidden = !several;
            canvas.hidden = false;
            drawPreview(canvas, preview);
        } else {
            caption.textContent = several ? `${names[index]}: nothing to show while no video plays.` : 'Nothing to show while no video plays.';
            caption.hidden = false;
            canvas.hidden = true;
        }
    }
}

// Polls like the status: only while the preview is on and the browser tab is visible; one request at a time.
function startPreview(view) {
    stopPreview(view);
    const preview = { active: true, loading: false, timer: 0 };
    const tick = async () => {
        preview.timer = 0;
        if (!preview.active || preview.loading || document.hidden) {
            return;
        }

        preview.loading = true;
        try {
            await loadPreviews(view);
        } finally {
            preview.loading = false;
        }

        if (preview.active && !document.hidden) {
            preview.timer = setTimeout(tick, HyperionGrabberConfig.previewRefreshMilliseconds);
        }
    };
    preview.onVisibilityChange = () => {
        if (!document.hidden && !preview.timer) {
            tick();
        }
    };
    document.addEventListener('visibilitychange', preview.onVisibilityChange);
    view.hyperionPreview = preview;
    view.querySelector('#PreviewList').hidden = false;
    view.querySelector('#PreviewToggle span').textContent = 'Hide live preview';
    tick();
}

function stopPreview(view) {
    const preview = view.hyperionPreview;
    if (!preview) {
        return;
    }

    preview.active = false;
    clearTimeout(preview.timer);
    document.removeEventListener('visibilitychange', preview.onVisibilityChange);
    view.hyperionPreview = null;
    view.querySelector('#PreviewList').hidden = true;
    view.querySelector('#PreviewToggle span').textContent = 'Show live preview';
}

export default function (view) {
    view.addEventListener('viewhide', function () {
        stopStatusRefresh(view);
        stopPreview(view);
    });

    view.querySelector('#PreviewToggle').addEventListener('click', function () {
        if (view.hyperionPreview) {
            stopPreview(view);
        } else {
            startPreview(view);
        }
    });

    view.addEventListener('viewshow', function () {
        startStatusRefresh(view);
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(HyperionGrabberConfig.pluginUniqueId).then(function (config) {
            view.querySelector('#HyperionName').value = config.HyperionName || '';
            view.querySelector('#HyperionHost').value = config.HyperionHost || '';
            view.querySelector('#HyperionPort').value = config.HyperionPort || 19400;
            view.querySelector('#HyperionPriority').value = config.HyperionPriority || 150;
            view.querySelector('#PlaybackEnabled').checked = config.PlaybackEnabled === true;
            view.querySelector('#FramesPerSecond').value = config.FramesPerSecond || HyperionGrabberConfig.defaultFramesPerSecond;
            view.querySelector('#LatencyOffsetMilliseconds').value = savedOrDefault(config.LatencyOffsetMilliseconds, 0);
            view.querySelector('#PauseReleaseSeconds').value = savedOrDefault(config.PauseReleaseSeconds, HyperionGrabberConfig.defaultPauseReleaseSeconds);
            view.querySelector('#KodiClockEnabled').checked = config.KodiClockEnabled !== false;
            view.querySelector('#KodiJsonRpcPort').value = savedOrDefault(config.KodiJsonRpcPort, HyperionGrabberConfig.defaultKodiPort);
            view.querySelector('#FollowExtras').checked = config.FollowExtras !== false;
            view.querySelector('#ExcludedTag').value = typeof config.ExcludedTag === 'string' ? config.ExcludedTag : 'nolights';
            view.querySelector('#AdditionalTargetList').replaceChildren();
            showAdditionalTargetResult(view, null);
            for (const target of config.AdditionalTargets || []) {
                addTargetCard(view, target);
            }

            updateTargetCards(view);
            // One after the other: both report problems in #PlaybackResult, and loadClients clears it when it works.
            return loadClients(view, config.PlaybackDevices || [], config.PlaybackUsers || [])
                .then(() => loadLibraries(view, config.ExcludedLibraries || []));
        }).then(function () {
            Dashboard.hideLoadingMsg();
        }).catch(function () {
            Dashboard.hideLoadingMsg();
            Dashboard.processErrorResponse({ statusText: 'Failed to load the Hyperion Grabber configuration.' });
        });
    });

    wireTestButtons(
        () => readTarget(view),
        view.querySelector('#HyperionResult'),
        view.querySelector('#HyperionTestConnection'),
        view.querySelector('#HyperionTestPattern'));

    wireDiscoverButton(view.querySelector('#HyperionDiscover'), view.querySelector('#HyperionDiscoverResult'), function (server) {
        view.querySelector('#HyperionHost').value = server.Host;
        view.querySelector('#HyperionPort').value = server.Port;
        if (!view.querySelector('#HyperionName').value.trim()) {
            view.querySelector('#HyperionName').value = server.Name;
        }
    });

    view.querySelector('#AddTarget').addEventListener('click', function () {
        showAdditionalTargetResult(view, null);
        addTargetCard(view, {}).querySelector('.targetHost').focus();
    });

    view.querySelector('#PlaybackRefresh').addEventListener('click', function () {
        loadClients(view, readChoices(view.querySelector('#PlaybackDeviceList')), readChoices(view.querySelector('#PlaybackUserList')));
    });

    view.querySelector('#HyperionGrabberConfigForm').addEventListener('submit', function (e) {
        e.preventDefault();
        const target = readTarget(view);
        const problem = validateTarget(target);
        if (problem) {
            showResult(view.querySelector('#HyperionResult'), false, problem);
            return false;
        }

        const additionalTargets = [];
        for (const [index, card] of targetCards(view).entries()) {
            const additional = readCardTarget(card);
            const additionalProblem = validateTarget(additional);
            if (additionalProblem) {
                showResult(card.querySelector('.targetResult'), false, additionalProblem);
                showAdditionalTargetResult(view, `✖ Hyperion server ${index + 2}: ${additionalProblem}`);
                return false;
            }

            additionalTargets.push(additional);
        }

        const duplicate = findDuplicateTarget([target, ...additionalTargets]);
        if (duplicate) {
            showAdditionalTargetResult(view, '✖ ' + duplicate);
            return false;
        }

        showAdditionalTargetResult(view, null);

        const playbackEnabled = view.querySelector('#PlaybackEnabled').checked;
        const framesPerSecond = readFramesPerSecond(view);
        const devices = readChoices(view.querySelector('#PlaybackDeviceList'));
        const users = readChoices(view.querySelector('#PlaybackUserList'));
        const latencyOffset = readInteger(view, '#LatencyOffsetMilliseconds');
        const pauseRelease = readInteger(view, '#PauseReleaseSeconds');
        const kodiClock = view.querySelector('#KodiClockEnabled').checked;
        const kodiPort = readInteger(view, '#KodiJsonRpcPort');
        const fpsProblem = validateFramesPerSecond(framesPerSecond) || validateTiming(latencyOffset, pauseRelease) || validateKodiPort(kodiPort)
            || validateDeviceOffsets([...devices, ...additionalTargets.flatMap(t => t.Devices)]);
        if (fpsProblem) {
            showPlaybackResult(view, '✖ ' + fpsProblem);
            return false;
        }

        if (playbackEnabled && devices.length === 0 && additionalTargets.every(t => t.Devices.length === 0)) {
            showPlaybackResult(view, '✖ Select at least one device, or turn off Follow playback.');
            return false;
        }

        const followExtras = view.querySelector('#FollowExtras').checked;
        const excludedTag = view.querySelector('#ExcludedTag').value.trim();
        const excludedLibraries = readChoices(view.querySelector('#ExcludedLibraryList'));
        showPlaybackResult(view, null);
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(HyperionGrabberConfig.pluginUniqueId).then(function (config) {
            config.HyperionName = target.Name;
            config.HyperionHost = target.Host;
            config.HyperionPort = target.Port;
            config.HyperionPriority = target.Priority;
            config.AdditionalTargets = additionalTargets;
            config.PlaybackEnabled = playbackEnabled;
            config.FramesPerSecond = framesPerSecond;
            config.LatencyOffsetMilliseconds = latencyOffset;
            config.PauseReleaseSeconds = pauseRelease;
            config.KodiClockEnabled = kodiClock;
            config.KodiJsonRpcPort = kodiPort;
            config.PlaybackDevices = devices;
            config.PlaybackUsers = users;
            config.FollowExtras = followExtras;
            config.ExcludedTag = excludedTag;
            config.ExcludedLibraries = excludedLibraries;
            return ApiClient.updatePluginConfiguration(HyperionGrabberConfig.pluginUniqueId, config);
        }).then(function (result) {
            Dashboard.processPluginConfigurationUpdateResult(result);
        }).catch(function () {
            Dashboard.hideLoadingMsg();
            Dashboard.processErrorResponse({ statusText: 'Failed to save the Hyperion Grabber configuration.' });
        });

        return false;
    });
}
