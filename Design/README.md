# 哇小兴 · AR 界面第一版

独立 worktree：`/Users/kateiso_cao/Developer/AR-Glasses-App-waxiaoxing-ui`，分支 `waxiaoxing-ui`。
原仓库无首次提交，因此使用 orphan worktree 并复制当前源码，不创建提交。未合并或安装到设备。

## 设计

- 保留透明光学 HUD，以浅绿、白色和少量紫色建立哇小兴的视觉身份。
- 天气数据占主位；角色放右上方，体积小、无额外场景背景。
- 对话入口“和小兴聊聊”；角色旁显示状态与短字幕，开始/结束、静音仍是原有真实交互。
- 聆听/回答显示绿色动态条，思考/连接显示紫色动态条；不是测得的音量波形。回答时仅轻微浮动，空闲静止。
- 设置及校准采用统一的深绿圆角底板与按钮。
- 更新时间、缺测、模拟数据和来源边界保留，不把角色表现当成视觉识别能力。

角色由用户低清图片生成清晰透明底提案，并非官方精确还原。原图 `waxiaoxing-reference.png`；实际素材 `Assets/Resources/Companion/Waxiaoxing.png`，Unity 导入上限 512，无 mipmap、不可读纹理。

## 验证

Unity 6000.1.5f1、Android 目标下运行 `AirDemoValidation.Run` 成功。检查了 26 度保守视野、文字溢出、圆角网格生成、隐藏界面射线交互、校准复位和原有数据展示检查。

`waxiaoxing-weather-voice.png` / `waxiaoxing-weather.png` / `waxiaoxing-setup.png` 是实际 Unity 编辑器渲染，数值为测试夹具，不代表当前天气或眼镜实拍。已视觉复核天气、对话、校准和详情视图。未构建新 APK，尚未验证设备性能、光学对比度或佩戴体验。

## 改动入口

- AirCompanion.cs：角色、状态动效
- AirSoftPanel.cs：圆角 UI 几何
- AirWeatherHud.cs：天气卡
- AirGeminiVoice.cs：语音入口、字幕布局、角色状态绑定
- AirEnvironmentDemo.cs：设置/校准样式
- AirVoiceProtocol.cs：哇小兴名称与语气，保留环保回答约束
- AirCompanionImporter.cs：纹理导入预算
- AirDemoValidation.cs：新增圆角表面渲染检查

原主目录的天气/问答功能保留；打断前的第二轮全语音设备复测未在此次 UI 任务中继续，不能把本界面检查当成那项验收完成。
