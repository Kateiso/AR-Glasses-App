## 0.4.3 已合入主目录（2026-09-29）

代码评审后整合：哇小兴界面、手势指向/捏合与手机竖屏、中央气象台天气、大兴环保问答。空气模型说明固定为底部小字，正常有效数据的语音回答不反复口播来源说明。

合并方式：当前各分支无 Git 提交历史，逐文件校验后整合到 main 工作目录；未创建提交或推送。原 worktree、新旧 APK 均保留，被替换文件备份在 Logs/before-merge-043-*。

验证：集成版 Unity 布局/功能检查及 Android 构建成功；此前已安装 Beam Pro 且安装包哈希一致，竖屏、手势能力/模式启动、天气取值有设备记录。2026-09-29 合并前再次校验源码清单、旧控制器绑定保留、左右手 UI Press 绑定和 APK 哈希。没有运行有声测试。真实捏合点击、真人收音/回声、佩戴舒适度仍待使用者验收，静音端到端测试尚未完成。

构建：scripts/build-android.sh，输出 Builds/Android/AR-Air-Environment_0.4.3.apk。审阅记录：docs/integration-review-0.4.3.md。

## 0.4.1 天气与大兴环保问答

天气改为中央气象台公开网页数据（`/rest/real/iEilj`），不是有 SLA 的商用 API。当前只对黄村演示中心 39.7244, 116.336 周围纬度 ±0.08°、经度 ±0.10° 范围提供大兴区域天气参考；范围外明确未接入，不用大兴数据冒充其他地点。不是 GPS 或眼镜现场测温。空气仍为 Open-Meteo/CAMS 区域模型。

天气请求最多三次，间隔 2/4 秒；失败保留本次运行的旧值并标刷新失败，超过三小时另标过期。不写磁盘天气缓存。中央气象台时间按北京时间解析，9999/缺测与零值分离，响应站点校验。五分钟刷新周期不变。

语音会话随 setup 加载 `Assets/Resources/DaxingEnvironmentKnowledge.txt`：大兴2025年历史统计、VOCs监测工作背景、污染研判边界、碳核算单位及因子口径、领导简报与现场核查回答方式。资料注明官方来源和核验日期；它是人工核验的有限资料包，不是联网搜索或完整法规库。实时数值仍通过 get_environment 获取。

验收题：实时温度/PM2.5来源；2025年大兴年均29.5与当前值能否比较；PM2.5能否推算企业碳；1000kWh×假设0.5kgCO2/kWh的单位换算；追问因子是否官方；异味能否判违法/给罚款；无数据时拒绝编造全区碳排放。调试探针仅记录合成测试题对应回答，正常用户对话不记录文本或原始音频。

旧0.4.0 APK保留用于回退，改动前源码备份在 `Logs/rollback_0.4.0/`。构建/设备结果见 `Logs/weather-daxing-build.log` 与 `Builds/Android/weather-daxing-verification.json`。最终设备完成八轮合成中文语音往返，重复回顾前文未再出现；碳核算与缺测边界正确。年度统计的指代句和演示因子的说话人归属仍有两处措辞问题，详见记录。真人收音/回声未以本测试验收。

# AR 眼镜城市信息演示

## 0.4.0 — Beam Pro 直连语音（2026-09-28）

- Unity 内加入 Gemini Live WSS 客户端、Android 原生 AudioRecord / AudioTrack 和小型对话栏；无需电脑或科技大厦中转音频。
- 点击「开始对话」请求麦克风权限；授权返回后再点击一次开始。支持连续追问、静音、结束，以及服务端检测到用户打断后清空播放队列。暂停、追踪丢失、断线、认证失效或十分钟上限都会停止音频。
- 当前地点通过只读 `get_environment` 工具供模型查询，包含时间、单位、缺测、过期、模拟和模型来源。没有视觉输入，不声称识别窗框或看见窗外。
- 主卡片不再显示「预设」「测试锚点」，手动放置与来源说明仍在详情；模拟模式保留「演示数据」。

短期认证只写入 debug APK 的 Android 内部私有目录，通过 stdin 传输，不进入源码、APK、命令参数或日志。准备命令：

```sh
python3 scripts/prepare-voice-session.py --device <当前ADB地址>
```

使用 Mac 的 ADC 刷新 OAuth access token。到期需重新执行；未实现无人值守认证续期，不向设备复制 refresh token。默认 `kateiso-core` / `us-central1` / `gemini-live-2.5-flash-native-audio`。真实会话产生 GCP 用量；应用不保存原始录音或对话文本。

自动诊断可用 `--probe-audio <合成PCM文件>` 注入最长20秒 PCM16/16kHz/单声道测试输入；下一次启动自动发送合成音频（补足2秒静音供VAD判停）与四轮文字问题，接收真实云端语音，不开启麦克风。诊断一次性标记和 PCM 用后删除。自动诊断不能替代佩戴收音、回声、打断与主观可读性验收。

真机验收（2026-09-28）：0.4.0 构建与签名通过，设备 APK 哈希与本地一致。Beam Pro 直连 GCP 完成 5 个有音频的回答（第一轮合成语音上传，其余文字追问），收到 1,729,810 字节音频；环境工具调用成功，ADB 断开期间继续完成问答。原配置地点名迁移成功，坐标与 `_preset` 来源信息保留；原 ADB 文件不可覆盖的问题已用临时文件原子替换修复。捕获日志未包含短期令牌。

实测能力：头部位置追踪可用，`rgbSupported=False`；SDK 网格语义无 Window 类别，公开 RGB 窗框识别路线受阻，原始灰度访问未验证。

尚未验收：用户佩戴时的真实麦克风收音、回声、口头打断、可读性与射线操作；设备网络中断及真实令牌到期场景。代码包含对应停止处理，令牌过期已通过 Editor 检查。自动测试的打断事件验证了清空播放队列，但不等同于真实说话打断。日志 `latest_input_to_audio_ms` 是最近转写/文字请求至收到音频的时间，不是严格的语音结束到出声延迟。

详细状态见 `Builds/Android/voice-verification.json`。保留旧版 APK，`Logs/rollback_0.3.0` 保存此次修改前的关键文件和设备配置。


开发目录：`/Users/kateiso_cao/Developer/AR-Glasses-App`。桌面 SmartCity 下的同名入口指向这里。

目标设备：XREAL Air 2 Ultra + Beam Pro（X4000，Android 14）。

## 窗息 HUD（2026-09-28，0.3.0）

Unity 原生移植 Claude Design「窗息」第二版：500×330 世界空间卡片，在约2米处宽约14°，细角标、矢量天气图标、温度、PM2.5、地点与各自数据时间；200ms 显隐，详情按需展开，日常仅保留小菜单入口。默认纯透视，菜单可切淡色背板（光学眼镜不能用黑色遮住真实背景）。

- 设计来源：[私有 Claude Design 项目](https://claude.ai/design/p/341080d1-ba32-44ce-a2bb-e9b32cb3166b)。新增 `AirWeatherHud.cs`、`AirWeatherGlyph.cs`。
- 构建命令仍是 `./scripts/build-android.sh`，新 APK 单独输出 `Builds/Android/AR-Air-Environment_0.3.0.apk`；旧版 `AR-Air-Environment.apk` 保留。沿用包名、配置格式和应用私有目录，升级不清空已有坐标。
- 先看向窗框旁希望放卡片的位置并确认，第二步可选「只测试窗户」跳过目标方向；这里是**测试锚点**，不是窗户自动识别。仍支持双方向、重校准、地点设置。追踪无效或暂停时立即隐藏卡片并重新校准，隐藏卡片不拦截射线。
- 在线天气新增 `weather_code`、`is_day`；主卡独立显示结构化读数。缺失数值显示「—」，失败清空对应读数和图标，天气和空气分别显示时间，超过3小时标为过期；不自动切成模拟数据。
- Editor 验证：`AirDemoValidation.ValidateAndBuild`（需图形设备，不带 `-nographics`），覆盖缺失/零值、天气码、坐标、校准重置、隐藏状态、文本高度、图标实际网格和截图；`Logs/air-preview/hud_*_fixture.png` 是布局样例，不是实时环境读数。
- 真机新增 `AR_WINDOW_CAPABILITY` 日志：在头部追踪有效后只读查询 SDK RGB 与位置追踪能力，不开启相机、不录制图像。该日志为能力标志，不能替代画面/相机参数/坐标映射的完整验证。
- 本轮使用本机已配置的 Unity 与本地 Beam Pro 验证，依赖本地设备及图形渲染。没有启动远程服务。

本轮验证结果：Editor 检查和 APK 构建通过（0错误），APK v2签名通过；已安装 Beam Pro 0.3.0/code3，设备APK SHA256与本地一致，升级前后配置一致。设备日志确认新版初始化、两个地点的天气/空气请求均成功；本次普通ADB启动出现 XREAL `NotFindRuntime`（`libnr_api.so` 未加载）并退出，未检测到眼镜显示器，需接好眼镜后从“我的眼镜”空间应用启动。眼镜内显示/交互和能力日志未验收。构建后清理可重建Bee缓存，实测释放约3.34 GiB，剩余约7.88 GiB。记录见 `Builds/Android/hud-verification.json`。

### 真实窗户识别的当前边界

2026-09-28 复核 [XREAL SDK 3.1 RGB 文档](https://docs.xreal.com/Camera/Access%20RGB%20Camera)：公开 RGB 功能仅支持 One 系列 + Eye。当前 SDK 平面/网格语义没有 Window 类别，原始灰度环境画面也未获得可用访问路径。Beam Pro 自身相机不等于与眼镜同步标定的第一视角。

因此本版仅交付新 UI 与能力日志，**没有实现真实窗户检测**。下一阶段必须先验证环境帧获取、时间戳、内外参、空间映射及与6DoF并发；不能从平面或网格空洞直接推断窗户。需厂商接口时先准备技术问题，不自动对外联系或更换硬件。

## 空气环境首版（2026-09-21，0.2.0）

已实现双方向会话内校准、转头停留切换信息卡、隐藏和重新校准、中文地点设置、在线天气与区域空气模型数据、明确标记的模拟模式。当前没有接入 GPS/RTK，位置需手动输入 WGS84 经纬度；未配置时不请求数据，不假装当前位置已获得。

- 场景：`Assets/Scenes/AirEnvironment.unity`。主要代码：`Assets/Scripts/AirEnvironmentDemo.cs`、`AirEnvironmentData.cs`。
- 构建：`./scripts/build-android.sh`；输出 `Builds/Android/AR-Air-Environment.apk`。应用名 `AR Air Environment`，沿用原包名，因此安装会升级环境检查应用。
- 旧立方体验证场景和 APK 保留；需重建旧版时运行 `./scripts/build-android.sh --environment`。
- 首次打开依次朝向窗户、目标方向，用 Beam Pro 射线点击确认。当前两方向至少相隔约 35°，头向进入约 20° 范围、停留 0.3 秒后显示；退出阈值 28°。这是本次会话内的方向演示，不是自动地理配准。
- 在「地点 / 数据设置」填两个位置和目标名称，或切到「模拟」后保存。位置保存到应用私有目录 `air-settings.json`，方向不跨启动保存；追踪丢失或暂停恢复后重校准。
- 在线来源：[Open-Meteo 天气](https://open-meteo.com/en/docs)、[CAMS / Open-Meteo 空气](https://open-meteo.com/en/docs/air-quality-api)，每 5 分钟刷新。空气选用全球模型（约 45 km），仅适合区域背景展示，不能表示街道差异或监测站实测；天气也注明模型数据，不冒充眼镜测量。
- 不计算中国 AQI、不推断碳排；缺测显示破折号，网络失败明确提示，不自动用模拟读数替代在线读数。三小时以上的模型时间差标注时效待核实。
- 中文字体 Noto Sans CJK SC，许可保存在 `Assets/Fonts/OFL.txt`。
- 2026-09-21 用户确认“大兴区生态环境局”预设：设备配置已设为 WGS84 纬度 39.7244、经度 116.3360，在线模式，两卡暂共用。该点来自兴政南巷道路参考位置，非 8 号楼精确坐标；地址与坐标来源保存在 `configs/air-settings_daxing.json`。配置已推送至应用外部私有 files 目录，未重新打包 APK。
- 编辑器验证入口 `AirDemoValidation.Run`，截图输出 `Logs/air-preview/`。读数截图是排版用固定样例，不是实时查询结果。

验收状态：最终 Android 构建成功，0 个错误，APK 约 217 MiB，v2 签名验证通过。Mac 上天气与空气 API 请求成功。编辑器检查通过缺测/零值区分、无效坐标、双方向校准/重置、文本布局与 26° 垂直视野边界；三张截图已人工查看。设置标题裁切及更换地点时旧读数短暂残留均已修复。最终 APK 已经无线 ADB 安装成功，设备端 SHA-256 与本地文件一致；当前眼镜显示器未连接，新版眼镜内交互、中文键盘尚未验收；9 月 21 日预设大兴位置后，设备日志已确认天气请求成功，空气请求另待确认。构建结束后已清理可重建的 Library/Bee，实测释放约 3.86 GiB，剩余约 9.65 GiB。

## 基础环境验收记录（2026-09-20）

- Android APK 构建成功，0 个构建错误；约 165 MiB，APK v2 签名验证通过，ARM64，版本 0.1.0；已安装到 Beam Pro。
- 无线 ADB 已连接验证。曾出现普通 TCP 可达而旧 ADB 后台报 `No route to host`，重启 Mac 的 ADB 服务后恢复。无线连接端口可能变化，重连时读取设备当前端口或 `adb mdns services`。
- Air 2 Ultra 已识别，眼镜显示分辨率为 3840×1080。
- 已为本验证应用开启 `SYSTEM_ALERT_WINDOW`（显示在其他应用上层）权限；XREAL 启动组件需要此权限，未开启时会跳往系统设置而无法进入 Unity 场景。
- 实机日志确认 `loader=XREALXRLoader displayRunning=True inputRunning=True`，用户确认看到青色立方体。
- 用户完成转头与侧移检查，确认立方体留在原来的空间位置：基础显示、头部追踪及当前会话内的空间稳定性初步验收通过。
- 本次未验证重启后恢复的持久空间锚点、地理坐标定位或真实管网配准。9 月 16 日的 `NotFindRuntime` 状态已被本次成功实测更新。
- 验证记录：`Builds/Android/environment-verification.json`；实机日志摘录与用户确认：`Logs/device-validation-2026-09-20.log`。
- 业务方向已调整为窗外与指定方向的空气环境信息展示；已形成调研方案，0.2.0 已实现首版，见上方。当前先做两个效果：看向窗户显示窗外大气环境，看向黄村（名称待确认）方向显示区域空气质量。
- 为节省存储，之前已清理 `Library/Bee` 编译中间文件，下次构建会重新生成；已保留 APK 和依赖下载缓存。

## 早期业务方案（2026-09-20）

- 用户确认当前没有执法人员接触，先兼顾领导演示效果与未来真实环境数据需求；空气监测优先，碳后续扩展。
- 暂无真实业务数据，首版采用明确标记的模拟案例。建议以“校准窗户方向—窗外空气卡—转向黄村—地点空气卡”为主线。
- 完整内容见 [空气执法 AR 调研与产品方案](docs/空气执法AR_调研与产品方案_2026-09-20.md)。该方案为设计背景；0.2.0 的实际功能以上方记录为准，尚未经业务人员访谈验证。
- 原地下管网可作为第三个效果候选；黄村公园卡片的地点名称待确认。方向触发使用头部朝向，不等同于眼动追踪。
- 真实监测数据、地理配准、碳核算及正式执法系统均尚未接入。

## 已安装的开发环境

- Unity 6000.1.5f1 Apple Silicon，复用现有 Editor。
- Android Build Support、OpenJDK 17.0.9、NDK r27c、SDK Platform 34、Build Tools 34.0.0。
- Android SDK 许可证已在用户授权后确认。
- XREAL SDK 3.1.0、XR Interaction Toolkit 2.6.5、XR Plug-in Management 4.5.1。
- 已导入 XRI Starter Assets，供 XREAL 交互预制体引用相机与输入配置。
- Android 使用 ARM64、IL2CPP、OpenGLES3，最低 API 29，目标 API 34。

Android 工具链位于 `/Applications/Unity/Hub/Editor/6000.1.5f1/PlaybackEngines/AndroidPlayer/`。
`Packages` 下的两个 `.tgz` 是本地依赖来源，请保留。`Library`、`Temp`、`Logs` 是可重新生成的缓存或日志。

## 环境验证场景

`Assets/Scenes/EnvironmentCheck.unity` 使用 XREAL 的 XR 交互预制体。运行两秒后在前方生成青色立方体，供戴眼镜后检查显示及头部运动时的空间稳定性。它保留为独立环境验证场景；空气业务使用 AirEnvironment 场景。地下管网尚未实现。

构建入口：Unity 菜单 `AR Project > Build Environment Check APK`，或在 Unity 关闭时运行旧版构建：

```bash
./scripts/build-android.sh --environment
```

输出：`Builds/Android/AR-Environment-Check.apk`。
脚本日志：`Logs/android-build.log`。
包名：`com.smartcity.ar.environmentcheck`。
首次构建需要联网获取 Gradle 依赖。`AR_UNITY_EDITOR` 可覆盖 Unity 可执行文件路径。

## 设备验收

1. Beam Pro 解锁后有线连接 Mac，并允许 USB 调试；运行 `adb devices -l` 核对当前设备。
2. `adb -s <当前设备序列号> install -r Builds/Android/AR-Environment-Check.apk`。
3. 将眼镜接到 Beam Pro，解锁设备，通过“我的眼镜”进入空间应用启动环境检查程序。首次启动如跳往系统设置，为 AR Environment Check 开启“显示在其他应用上层”权限。
4. 检查双眼显示、转头响应和立方体是否留在空间中；APK 构建成功不代表这些设备行为已验收。
5. 可使用 `adb -s <当前设备序列号> logcat -d -s Unity` 查看 `AR_ENV_CHECK` 的 Loader、显示和输入子系统状态。

## 目录迁移说明

原桌面项目被 iCloud 转为占位文件后无法正常读回。由于原工程尚无业务代码和场景，本机工程使用相同 Unity 与 SDK 版本重建。原目录保留在 `/Users/kateiso_cao/Desktop/SmartCity/AR-Glasses-App-iCloud-backup`，未删除；后续开发以本 Developer 目录为准。

## 官方资料

- [XREAL SDK 入门](https://docs.xreal.com/Getting%20Started%20with%20XREAL%20SDK)
- [设备兼容性](https://docs.xreal.com/XREALDevices/Compatibility)
- [Android SDK 条款](https://developer.android.com/studio/terms)

开发调试可按[官方 FAQ 第 8 项](https://docs.xreal.com/Frequently%20Asked%20Questions)在未连接眼镜时连续点击“我的眼镜”左上角眼镜图标 10 次，再在开发调试页选择其他 MR 应用。本次尚未设置该选项。
