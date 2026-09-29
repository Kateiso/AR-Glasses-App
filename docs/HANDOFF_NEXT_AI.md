# Beam Pro AR 项目交接说明

更新时间：2026-09-30（Asia/Taipei）

## 当前交付状态

- 工程目录：`/Users/kateiso_cao/Developer/AR-Glasses-App`
- Unity：6000.1.5f1；XREAL SDK 3.1.0；XRI 2.6.5；Android ARM64 / IL2CPP / OpenGLES3。
- 包名：`com.smartcity.ar.environmentcheck`
- 已合并版本：`0.4.3`，版本号 `7`。
- APK：`Builds/Android/AR-Air-Environment_0.4.3.apk`
- APK SHA-256：`e84f4310c55b638ccca2b0a4b36db20af115890fc874f9ac6c9f853ba13009c1`
- 设备：XREAL Air 2 Ultra + Beam Pro（X4000，Android 14）。
- 最近安装目标：ADB `192.168.8.223:44293`；安装结果 `Success`。
- 最近一次短期 GCP OAuth 注入成功，凭证仅写入应用私有目录，未写入源码/APK/日志；本次有效期为 2026-09-29 22:51（北京时间），后续使用前必须重新执行认证准备脚本。
- 本次安装未启动应用、未播放声音、未开启自动语音探针。

## 已实现

- 窗息天气 HUD：天气、温度、PM2.5、地点、数据时间和底部小字来源提示。
- “哇小兴”视觉组件、圆角面板、Beam Pro 竖屏布局。
- 手势/射线输入配置，保留原有控制器绑定。
- 中央气象台大兴区域天气源；空气数据为 Open-Meteo/CAMS 区域模型。
- Gemini Live WSS 直连：连续对话、静音、退出、打断时清空播放队列、暂停/追踪丢失/断线/认证失效/十分钟上限停止音频。
- 只读 `get_environment` 环境查询接口，以及大兴空气污染/碳核算知识包。
- 正常有效数据回答不反复口播“区域模型”免责声明；来源和限制保留在 UI 底部小字，缺测/过期/来源问题仍需说明。

## 未完成或不能宣称已完成

- 真实窗框自动识别：当前 XREAL SDK 公开环境能力没有可用 Window 语义，RGB/相机标定和同步空间映射未验证；当前仍是手动放置/方向演示。
- 真人佩戴状态下的麦克风收音、回声消除、口头打断、舒适度和真实捏合点击，尚未完成验收。
- 断网、真实令牌过期和恢复流程尚未做完整真人验收。
- 当前 `main` 已有首个提交 `a121394`（handoff 文档）；原始 worktree 和旧 APK 保留。

## 运行与认证

```bash
cd /Users/kateiso_cao/Developer/AR-Glasses-App
./scripts/build-android.sh
python3 scripts/prepare-voice-session.py --device <当前ADB地址>
```

认证脚本使用 Mac ADC 刷新短期 OAuth access token，通过 stdin 注入设备私有目录；不要把令牌放入命令参数、日志、源码或 APK。不要使用 `--probe-audio` 做普通真机验收，除非明确需要合成音频云端诊断；用户要求静音时不得启动该探针。

ADB 地址会变化，先执行：

```bash
adb mdns services
adb connect <发现的地址>
adb -s <地址> shell getprop ro.product.model
```

眼镜显示应用应从 Beam Pro 的“我的眼镜”空间应用启动。普通 `am start` 可能触发 XREAL `NotFindRuntime`，不能替代眼镜内启动。

## 下一步建议

1. 先重新刷新短期认证并在 Beam Pro 内打开应用。
2. 做静音 UI/天气/手势检查；获得用户明确同意后再做真人语音验收。
3. 记录天气加载、连续五轮问答、打断、缺测、断网和令牌失效的实际结果。
4. 如果继续研究窗框识别，先向 XREAL 核实环境帧访问、相机内外参、时间同步和与 6DoF 并发能力；不要用方向触发冒充视觉识别。
5. 需要恢复旧版本时使用 `Builds/Android/AR-Air-Environment.apk` 或 `Logs/rollback_0.4.0/` 中的备份；不要删除现有 worktree。

## Git 交接

当前仓库已完成首个本地提交 `a121394`，但没有配置 `origin`，因此尚未推送。下一位 AI 需要先确认远端 URL 和目标分支，再执行 `git remote add origin ...` 与 `git push -u origin main`。推送前必须确认不会把 APK、Library、Temp、Logs 或任何认证文件加入版本库；`.gitignore` 已排除这些路径。
