const HOST = 'com.minifloat.host';
let port = null;
let pending = new Map();
let serial = 0;
let session = null;
let busy = false;

function connect() {
  if (port) return port;
  const p = chrome.runtime.connectNative(HOST);
  port = p;
  p.onMessage.addListener(message => {
    if (message.id && pending.has(message.id)) {
      const request = pending.get(message.id);
      pending.delete(message.id);
      clearTimeout(request.timer);
      if (message.error) request.reject(new Error(message.error));
      else request.resolve(message);
    } else if (message.type === 'control') {
      control(message.action);
    } else if (message.type === 'closed') {
      endSession();
    }
  });
  p.onDisconnect.addListener(() => {
    const reason = chrome.runtime.lastError?.message;
    if (port !== p) return;
    port = null;
    for (const request of pending.values()) {
      clearTimeout(request.timer);
      request.reject(new Error(reason || '桌面助手已退出，请重试。'));
    }
    pending.clear();
    endSession();
  });
  return p;
}

function rpc(type, values = {}) {
  return new Promise((resolve, reject) => {
    const id = String(++serial);
    const timer = setTimeout(() => {
      pending.delete(id);
      reject(new Error('桌面助手连接超时，请关闭小窗后重试。'));
    }, 15000);
    pending.set(id, { resolve, reject, timer });
    try { connect().postMessage({ type, id, ...values }); }
    catch (error) { clearTimeout(timer); pending.delete(id); reject(error); }
  });
}

async function endSession() {
  const previous = session;
  session = null;
  await chrome.action.setBadgeText({ text: '' });
  if (!previous) return;
  try {
    await chrome.scripting.executeScript({
      target: { tabId: previous.tabId, frameIds: [previous.frameId] },
      func: async () => {
        const state = globalThis.__miniFloat;
        if (state?.video === document.pictureInPictureElement) await document.exitPictureInPicture();
        state?.cleanup();
      }
    });
  } catch { /* The source tab may have closed. */ }
}

async function control(action) {
  if (!session) return;
  const current = session;
  if (action === 'return') {
    try {
      await chrome.tabs.update(current.tabId, { active: true });
      const tab = await chrome.tabs.get(current.tabId);
      await chrome.windows.update(tab.windowId, { focused: true });
    } catch { /* Tab already gone. */ }
    port?.postMessage({ type: 'close' });
    return;
  }
  try {
    await chrome.scripting.executeScript({
      target: { tabId: current.tabId, frameIds: [current.frameId] },
      args: [action],
      func: async action => {
        const video = globalThis.__miniFloat?.video;
        if (!video?.isConnected) return;
        if (action === 'toggle') {
          if (video.paused) await video.play(); else video.pause();
        } else if (action === 'mute') video.muted = !video.muted;
        else if (action === 'back') video.currentTime = Math.max(0, video.currentTime - 5);
        else if (action === 'forward' && Number.isFinite(video.duration))
          video.currentTime = Math.min(video.duration, video.currentTime + 5);
      }
    });
  } catch (error) {
    await chrome.action.setTitle({ title: `微窗：${error.message}` });
  }
}

// Executed in the isolated world: no page-supplied URLs or commands reach the native host.
function findVideos() {
  const videos = [];
  function visit(root) {
    videos.push(...root.querySelectorAll('video'));
    for (const element of root.querySelectorAll('*')) if (element.shadowRoot) visit(element.shadowRoot);
  }
  visit(document);
  return videos.map((video, index) => {
    const rect = video.getBoundingClientRect();
    return {
      index, width: video.videoWidth, height: video.videoHeight,
      score: (!video.paused && !video.ended ? 1e12 : 0) + Math.min(rect.width * rect.height, 1e9),
      eligible: video.readyState >= 2 && video.videoWidth > 0 && rect.width > 0 && rect.height > 0
    };
  }).filter(video => video.eligible);
}

async function openVideo(index) {
  try {
    const videos = [];
    function visit(root) {
      videos.push(...root.querySelectorAll('video'));
      for (const element of root.querySelectorAll('*')) if (element.shadowRoot) visit(element.shadowRoot);
    }
    visit(document);
    const video = videos[index];
    if (!video || video.readyState < 2) throw new Error('视频尚未准备好，请先播放几秒。');
    if (!document.pictureInPictureEnabled) throw new Error('这个页面或内嵌播放器不允许画中画。');
    if (document.pictureInPictureElement) await document.exitPictureInPicture();
    globalThis.__miniFloat?.cleanup();
    const wasDisabled = video.disablePictureInPicture;
    video.disablePictureInPicture = false;
    try { await video.requestPictureInPicture(); }
    finally { video.disablePictureInPicture = wasDisabled; }
    const send = message => {
      try { chrome.runtime.sendMessage(message).catch(() => {}); } catch { /* Extension reloaded. */ }
    };
    const leave = () => { send({ type: 'sourceClosed' }); cleanup(); };
    const resize = () => send({ type: 'ratio', width: video.videoWidth, height: video.videoHeight });
    const cleanup = () => {
      video.removeEventListener('leavepictureinpicture', leave);
      video.removeEventListener('resize', resize);
      if (globalThis.__miniFloat?.video === video) delete globalThis.__miniFloat;
    };
    video.addEventListener('leavepictureinpicture', leave);
    video.addEventListener('resize', resize);
    globalThis.__miniFloat = { video, cleanup };
    return { width: video.videoWidth, height: video.videoHeight };
  } catch (error) { return { error: error.message }; }
}

async function activate(tab) {
  if (busy || !tab?.id) return;
  busy = true;
  try {
    // Inject directly inside the action gesture, before any await/native startup.
    // This activates the eligible frames so the subsequent PiP request can use
    // their short-lived browser user activation. Capture rejection immediately.
    const probe = chrome.scripting.executeScript({ target: { tabId: tab.id, allFrames: true }, func: findVideos })
      .then(frames => ({ frames }), error => ({ error }));
    if (session) {
      await rpc('close');
      await endSession();
    }
    // Take the native window snapshot before Chrome creates its PiP window.
    await rpc('prepare');
    const discovery = await probe;
    if (discovery.error) throw discovery.error;
    const frames = discovery.frames;
    const candidates = frames.flatMap(frame => (frame.result || []).map(video => ({ ...video, frameId: frame.frameId })));
    candidates.sort((a, b) => b.score - a.score);
    if (!candidates.length) throw new Error('没有找到可用视频。请先播放视频；内嵌视频可尝试在播放器原网站打开。');
    const selected = candidates[0];
    const result = await chrome.scripting.executeScript({
      target: { tabId: tab.id, frameIds: [selected.frameId] }, func: openVideo, args: [selected.index]
    });
    const video = result[0]?.result;
    if (!video || video.error) throw new Error(video?.error || '无法进入画中画。');
    session = { tabId: tab.id, frameId: selected.frameId };
    await rpc('attach', { width: video.width, height: video.height });
    await chrome.action.setBadgeText({ text: 'ON' });
    await chrome.action.setBadgeBackgroundColor({ color: '#297B68' });
    await chrome.action.setTitle({ title: '微窗运行中 · 点击可重新接管当前视频' });
  } catch (error) {
    try { port?.postMessage({ type: 'close' }); } catch { /* Host disconnected. */ }
    await endSession();
    await chrome.storage.local.set({ lastError: error.message });
    await chrome.action.setBadgeText({ text: '!' });
    await chrome.action.setBadgeBackgroundColor({ color: '#B34435' });
    await chrome.runtime.openOptionsPage();
    port?.disconnect();
    port = null;
  } finally { busy = false; }
}

chrome.action.onClicked.addListener(activate);
chrome.runtime.onMessage.addListener((message, sender) => {
  if (!session || sender.tab?.id !== session.tabId || sender.frameId !== session.frameId) return;
  if (message.type === 'sourceClosed') {
    port?.postMessage({ type: 'close' });
    endSession();
  } else if (message.type === 'ratio' && Number.isFinite(message.width) && Number.isFinite(message.height)) {
    port?.postMessage({ type: 'ratio', width: message.width, height: message.height });
  }
});
chrome.tabs.onRemoved.addListener(tabId => {
  if (session?.tabId === tabId) { port?.postMessage({ type: 'close' }); endSession(); }
});
