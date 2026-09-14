chrome.storage.local.get('lastError').then(({ lastError }) => {
  if (!lastError) return;
  const element = document.getElementById('error');
  element.hidden = false;
  element.textContent = `上次启动未成功：${lastError}\n若提示 native host 未找到，请先运行 Install.cmd，再重新点击扩展。`;
});
