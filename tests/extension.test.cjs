const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const test = require('node:test');
const { spawn } = require('node:child_process');
const path = require('node:path');

function event() {
  const listeners = [];
  return { addListener: fn => listeners.push(fn), emit: (...args) => listeners.map(fn => fn(...args)) };
}
function setup({ noHost = false, attachError = false, videos = null } = {}) {
  const messages = [], scripts = [], badges = [], store = {};
  let openedHelp = 0, disconnected = false;
  const port = {
    onMessage: event(), onDisconnect: event(),
    disconnect() { disconnected = true; this.onDisconnect.emit(); },
    postMessage(message) {
      messages.push(message);
      if (message.id) queueMicrotask(() => port.onMessage.emit({ id: message.id, ...(attachError && message.type === 'attach' ? { error: 'cannot identify PiP' } : { ok: true }) }));
    }
  };
  const chrome = {
    runtime: {
      connectNative() {
        if (noHost) queueMicrotask(() => { chrome.runtime.lastError = { message: 'native host not found' }; port.onDisconnect.emit(); chrome.runtime.lastError = undefined; });
        return port;
      },
      onMessage: event(), openOptionsPage: async () => { openedHelp++; }
    },
    action: { onClicked: event(), setBadgeText: async x => badges.push(x.text), setBadgeBackgroundColor: async () => {}, setTitle: async () => {} },
    storage: { local: { set: async x => Object.assign(store, x) } },
    tabs: { onRemoved: event(), update: async () => {}, get: async () => ({ windowId: 1 }) },
    windows: { update: async () => {} },
    scripting: { executeScript: async input => {
      scripts.push(input);
      if (input.func.name === 'findVideos') return videos || [
        { frameId: 0, result: [{ index: 0, width: 1920, height: 1080, score: 2000000 }] },
        { frameId: 2, result: [{ index: 1, width: 1080, height: 1920, score: 1e12 }] }
      ];
      if (input.func.name === 'openVideo') return [{ result: { width: 1080, height: 1920 } }];
      return [{ result: true }];
    } }
  };
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../extension/background.js'), 'utf8'), { chrome, setTimeout, clearTimeout });
  return { chrome, port, messages, scripts, badges, store, help: () => openedHelp, disconnected: () => disconnected,
    activate: () => chrome.action.onClicked.emit({ id: 42 })[0] };
}
const settle = () => new Promise(resolve => setImmediate(resolve));

test('one click prepares before attaching and chooses playing video across accessible frames', async () => {
  const s = setup(); await s.activate();
  assert.deepEqual(s.messages.filter(x => x.id).map(x => x.type), ['prepare', 'attach']);
  const open = s.scripts.find(x => x.func.name === 'openVideo');
  assert.equal(open.target.frameIds[0], 2);
  assert.equal(open.args[0], 1);
  assert.equal(s.messages.at(-1).width, 1080);
  assert.equal(s.badges.at(-1), 'ON');
  s.port.disconnect(); await settle();
});

test('missing host opens useful error page and never creates a PiP window', async () => {
  const s = setup({ noHost: true }); await s.activate();
  assert.equal(s.scripts.some(x => x.func.name === 'openVideo'), false);
  assert.match(s.store.lastError, /native host not found/);
  assert.equal(s.help(), 1);
});

test('no eligible video cancels native work', async () => {
  const s = setup({ videos: [{ frameId: 0, result: [] }] }); await s.activate();
  assert.equal(s.messages.some(x => x.type === 'attach'), false);
  assert.equal(s.messages.some(x => x.type === 'close'), true);
  assert.match(s.store.lastError, /没有找到/);
});

test('attach failure exits source PiP and disconnects', async () => {
  const s = setup({ attachError: true }); await s.activate();
  assert.match(s.store.lastError, /cannot identify/);
  assert.equal(s.scripts.filter(x => x.func.name !== 'openVideo' && x.func.toString().includes('exitPictureInPicture')).length, 1);
  assert.equal(s.disconnected(), true);
});

test('unrelated frame cannot close session; owned source and tab closure clean up', async () => {
  const s = setup(); await s.activate();
  s.chrome.runtime.onMessage.emit({ type: 'sourceClosed' }, { tab: { id: 99 }, frameId: 2 });
  assert.equal(s.messages.at(-1).type, 'attach');
  s.chrome.runtime.onMessage.emit({ type: 'ratio', width: 640, height: 480 }, { tab: { id: 42 }, frameId: 2 });
  assert.equal(s.messages.at(-1).type, 'ratio');
  s.chrome.tabs.onRemoved.emit(42); await settle();
  assert.equal(s.messages.at(-1).type, 'close');
  assert.equal(s.badges.at(-1), '');
  s.port.disconnect();
});

test('native process EOF and invalid commands terminate cleanly without showing a player', async () => {
  const exe = path.join(__dirname, '../app/MiniFloat.exe');
  const child = spawn(exe, ['chrome-extension://dkahhcoogblcjjlnlggodiocgmkegffb/'], { windowsHide: true });
  let output = Buffer.alloc(0);
  child.stdout.on('data', data => { output = Buffer.concat([output, data]); });
  const body = Buffer.from(JSON.stringify({ id: 'test', type: 'invalid-command' }));
  const header = Buffer.alloc(4); header.writeUInt32LE(body.length);
  const exited = new Promise((resolve, reject) => { child.on('error', reject); child.on('exit', resolve); });
  child.stdin.write(Buffer.concat([header, body]));
  // Wait for an actual framed reply before closing stdin; no sleep-based handshake.
  await new Promise((resolve, reject) => {
    const timer = setTimeout(() => { child.kill(); reject(new Error('host timeout')); }, 5000);
    child.stdout.on('data', () => { if (output.length >= 4 && output.length >= output.readUInt32LE(0) + 4) { clearTimeout(timer); resolve(); } });
  });
  const reply = JSON.parse(output.subarray(4, 4 + output.readUInt32LE(0)).toString());
  assert.equal(reply.id, 'test'); assert.match(reply.error, /不支持/);
  child.stdin.end(); assert.equal(await exited, 0);
});
