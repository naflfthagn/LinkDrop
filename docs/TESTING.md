# 验证记录 · LinkDrop 1.0.0

测试日期：2026-10-20（Asia/Shanghai）

## 环境

| 项目 | 配置 |
| --- | --- |
| Windows | Windows 11 25H2，x64，本机为 100%–175% DPI 环境 |
| Windows 运行时 | .NET Framework 4.8 |
| Windows 编译器 | .NET Framework 4.8 csc（无需 Visual Studio） |
| Android 测试设备 | Android 11（API 30）x86_64 模拟器，1080 × 2280 |
| Android 构建 | JDK 17，AGP 8.10.0，Gradle 8.13，minSdk 29，release 签名 |
| 联调方式 | adb reverse 将设备 48622 转发到 Windows 端，用于模拟同网段连接；发现服务另通过 IPv4 UDP 信标注入验证 |

> 由于这台开发机没有连接实体 Android 手机，真实 Wi-Fi 下的组播路径未做实体设备验证。发现接收、配对、TLS、双向传输和失败恢复路径已在模拟器与 Windows 主机之间完成端到端验证。真实手机使用手动 IP 作为可靠兜底。

## Windows 协议集成测试

测试入口：<code>windows/tests/IntegrationTest.cs</code>

覆盖内容：

- 身份证书与 6 位安全码生成
- TLS 1.2 服务端启动
- 配对请求 → 等待批准 → 令牌下发
- 手机使用令牌完成认证
- 手机 → PC 文件接收、SHA-256 校验、落盘
- PC → 手机文件发送、SHA-256 校验
- 错误令牌被拒绝

结果：

<pre>
== 20 passed, 0 failed ==
</pre>

## Android release 验证

安装包：<code>LinkDrop-Android-1.0.0.apk</code>

- <code>apksigner verify</code>：通过。
- 签名证书 SHA-256：
  <code>70D9826CE81A553AADF80381C9BEA8B9863BBE9BA73F91B27B1E6CBF6DE771FC</code>
- release 构建启用 R8 代码压缩和资源压缩，最终 APK 约 2.4 MB。
- 在模拟器上安装 release APK 后可正常启动、配对、收发文件，说明 R8 没有破坏网络、TLS、MediaStore 或 Compose 代码路径。

## 端到端流程验证

1. 电脑开启配对模式，显示安全码 <code>942176</code>。
2. Android release 手动连接电脑地址；手机显示 <code>942 176</code>，与电脑一致。
3. 电脑点击“允许配对”；手机显示配对成功，并在约 2 秒后自动建立连接。
4. 电脑向手机发送 <code>电脑桌布.jpg</code> 和 <code>发布测试.txt</code>。
5. 手机向电脑回传同名文件。
6. 两端文件 SHA-256 完全一致。

### 文件图标外部打开

- 电脑向手机发送测试图片 <code>OpenTest.jpg</code>（18.9 KB）。
- Android 下载完成后，文件卡片图标显示“↗”角标，底部提示“点击文件图标用其他应用打开”。
- 点击图标后，系统实际启动：
  <code>com.android.gallery3d/.app.GalleryActivity</code>
  <code>act=android.intent.action.VIEW dat=content://media/external/downloads/79 typ=image/jpeg</code>
- Gallery 成功显示测试图片，说明 MediaStore URI 授权和外部应用跳转均正常。

### 哈希对照片段

<pre>
电脑桌布.jpg
  PC 源文件          66295913F61DBE0D59DB7B26BEADFDC41C8724AAEBD7740E9718FB0E2B79C372
  Android 收到       66295913f61dbe0d59db7b26beadfdc41c8724aaebd7740e9718fb0e2b79c372
  PC 收到的手机回传   66295913F61DBE0D59DB7B26BEADFDC41C8724AAEBD7740E9718FB0E2B79C372

发布测试.txt
  PC 源文件 / Android / PC 回传均一致
  39DB6ACC2980FE31A35A3A8D07C6CC6B2E34BAC9CABFC688A6F2936F6A72C653
</pre>

### 自动重连

- **Windows 进程重启**：Windows 端被杀掉后重启，Android 在约 3 秒内重新连接。
- **Android 应用重启**：Android 应用被强制停止后重新打开，约 2 秒内重新连接。
- 重启过程不需要重新输入安全码或点击任何配对按钮。

### 发现服务

由于模拟器处于 NAT 网络，无法直接收到 Windows 主机的组播包。为验证解析和 UI：

1. 在 Android 设备本机向 <code>127.0.0.1:48721</code> 发送合法的 Windows 信标：
   <code>{"m":"QCLINK1","v":1,"kind":"beacon","id":"deadbeefcafebabe","name":"DISCOVERY-TEST","platform":"windows","port":48622,"pairing":true}</code>
2. Android 界面的“附近的电脑”正确出现 <code>DISCOVERY-TEST</code>，并显示“可配对”。
3. 修复了 Android 上 MulticastSocket 默认绑定 IPv6 导致收不到 IPv4 信标的问题，现在显式绑定 IPv4。

## 已知边界

- 真实手机 + 家用路由器的组播/广播是否可达，仍取决于 Wi-Fi 网络配置。手动 IP 是兜底方案。
- Android 模拟器不能代表所有厂商的后台限制；真实设备上请保持应用前台并关闭省电限制。
- Windows 防火墙的“修复防火墙”按钮需要用户 UAC 同意。
- 未做公网/NAT 穿透测试。
