# 微窗 · MiniFloat

Windows 10/11 x64 + Chrome / Microsoft Edge 的轻量桌面视频小窗。点击浏览器中的扩展即可接管当前播放的视频，无需复制粘贴链接。

## 安装和使用

可从本仓库的 `dist/MiniFloat-0.1.1.zip` 下载安装包（包含 EXE、扩展和源码）。

1. 解压安装包，将整个目录保存在固定位置，双击 **Install.cmd**。它只为当前 Windows 用户同时注册 Chrome 和 Edge 本地助手，不需要管理员权限。
2. Chrome 地址栏输入 **chrome://extensions**；Edge 输入 **edge://extensions**，打开右上角的“开发者模式”，点击“加载已解压的扩展程序”，选择解压目录中的 **extension** 文件夹。
3. 在 Chrome 或 Edge 工具栏的扩展菜单中固定“微窗”。打开视频网站并播放视频，点击微窗图标，或按 **Alt + Shift + V**。

已安装旧版的用户：关闭微窗并重新运行 Install.cmd，然后在 Chrome 扩展管理页点击“重新加载”；Edge 可直接加载同一个 extension 文件夹。两种浏览器均兼容，目前仍共用一个小窗，跨浏览器切换时请先关闭原小窗并等待助手退出（约 60 秒）。

如果移动了整个目录，重新运行 Install.cmd。不要只移动 EXE 或 extension 文件夹。

也可先双击 **Demo.cmd** 体验窗口缩放和不透明度。演示会交替显示红蓝测试画面，不会打开网络视频。

## 小窗操作

| 操作 | 功能 |
| --- | --- |
| 拖动画面 | 移动窗口 |
| 拖动边缘 / 普通滚轮 | 保持视频比例调整大小 |
| Ctrl + 滚轮 | 调节不透明度，每档 5% |
| 右键 → 不透明度 | 滑块或 10%、25%、50%、75%、100% 预设 |
| 右键 → 尺寸 | 64、80、96、120、180、240、360、480、720 像素长边 |
| 双击 / 空格 | 播放或暂停 |
| 左 / 右方向键 | 快退 / 快进 5 秒（需要小窗获得焦点） |
| 右键 → 返回视频网页 | 回到 浏览器原视频标签页 |
| Esc / 右键 → 关闭 | 关闭小窗，视频回到网页中继续原有播放状态 |
| Ctrl + Alt + O | 恢复到 100% 不透明度并关闭鼠标穿透 |
| 托盘图标双击 | 恢复可见；全局快捷键被其他软件占用时仍可使用 |

默认长边 240 px、不透明度 85%；记住位置、大小、不透明度。横屏 16:9 最小为 **64 × 36 px**，竖屏 9:16 为 **36 × 64 px**。极宽/极高视频还需满足短边至少 24 px。一次显示一个视频。

## 如何保持轻量

- Windows 原生 WinForms 小窗，使用系统自带的 .NET Framework 4.x，无 npm 运行依赖、不打包 Electron 或额外浏览器内核。
- 视频由原浏览器 页面继续播放，保留原来的登录状态、进度、音轨。助手使用 Windows DWM 实时合成视频窗口，不下载、转码、录制或另行解码视频。
- 扩展使用 Native Messaging 启动助手，无本地 HTTP 服务、无网络监听端口、无开机启动。
- 小窗关闭后，闲置助手约 60 秒自动退出；退出或连接断开时恢复浏览器原窗口状态。

这里的“一键转发”转发的是当前视频播放会话，而不是提取网页中的某个 URL。视频网站的 `blob:`/分段流地址通常不能直接交给外部播放器，这种接入方式避免了重新登录或复制链接。

## 已知边界

- **需要保持原视频标签页打开**，不支持脱离浏览器 的独立播放。
- 使用 浏览器原生视频画中画作为画面源，因此禁止画中画的网站、跨域内嵌播放器和受保护/DRM 视频可能无法使用。没有申请“读取所有网站”权限；无法访问的跨域内嵌视频需在播放器原网站打开。
- 只复制 `<video>` 画面；网页额外绘制的弹幕、HTML 字幕和按钮不保证带入。视频本身包含的字幕可显示。
- 为防止源窗口完全移出屏幕后停止重绘，原浏览器 画中画留在原位置，设为 **1/255 不透明度并允许鼠标穿透**。其内容几乎不可见，特定纯色背景上可能有极轻微痕迹。新小窗的不透明度独立控制。
- 不要手动最小化原浏览器 画中画。如果黑屏或停帧，在右键菜单取消“收起浏览器原小窗”，检查源画面；关闭小窗后重新调用也可恢复。
- 极小窗口适合观看；精确操作请用右键菜单、快捷键或托盘。全局 Ctrl+Alt+O 被占用时改用托盘恢复。
- 非正常强制结束助手时，原画中画可能保持几乎不可见。可用 浏览器媒体控制退出画中画，再重新打开；不必更改浏览器安全设置。

## 验证范围

本版本为 **0.1.1 本机可试用版**。

- 已编译 Windows x64 EXE，并验证真实原生窗口的实时画面更新、40% 不透明度、96 × 54 和 64 × 36 尺寸，以及关闭/恢复时源窗口的样式和位置。
- 已检查比例约束、拖动锚点、消息长度限制、UTF-8 分帧、断连退出及扩展连接失败等路径。
- Chrome / Edge 共用扩展链路有模拟 API 测试；**尚未在用户实际 Chrome / Edge 配置和目标视频网站完成端到端验证**。安装后需用你常看的视频网站确认播放兼容性。
- 测试结果与截图在 `artifacts` 目录，源码在 `native` 和 `extension`。

## 卸载

先关闭小窗，在安装过扩展的 Chrome / Edge 中删除扩展，再双击 **Uninstall.cmd**。卸载脚本只移除属于本安装目录的 Chrome 的 `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.minifloat.host` 和 Edge 的 `HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.minifloat.host` 注册项；保留程序、源代码和设置文件。无需删除其他浏览器数据。

## 开发

构建：在此目录运行 `powershell -NoProfile -File .\Build.ps1`。

扩展及进程通信测试：`node --test tests/extension.test.cjs`。

原生无界面测试：`app\MiniFloat.exe --self-test artifacts\test-results.txt`。

真实窗口视觉测试：`powershell -NoProfile -File .\Test-Visual.ps1`，会短暂显示测试窗口并自动关闭。

扩展公钥固定，ID 为 `dkahhcoogblcjjlnlggodiocgmkegffb`。安装脚本根据公钥计算 ID，并只允许此扩展调用本地助手；不包含私钥。消息只包含有限动作和视频尺寸，不向本地助手传入视频 URL、Cookie 或任意程序命令。

参考：[Edge Native Messaging](https://learn.microsoft.com/en-us/microsoft-edge/extensions/developer-guide/native-messaging)、[Chrome Native Messaging](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging)、[Windows DWM Thumbnail](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail)。
