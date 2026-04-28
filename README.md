# Serial MITM Proxy GUI (V3)

Windows 平台的串口中间人代理工具（MITM），用于在两端串口设备之间做实时转发、监控、拦截、修改、注入、捕获和回放。

本项目基于以下技术栈实现：
- C# / .NET 8（开发框架）
- WPF + MVVM
- async/await + Channels
- xUnit

## 1. 核心能力

- 双向串口 MITM 转发（A->B / B->A）
- 实时监控（HEX + ASCII）
- 拦截队列与人工决策（Forward / Drop / EditAndForward / Repeat）
- 一键添加方向拦截规则（A->B / B->A）
- 规则引擎（匹配器 + 动作 + 变换器）
- 报文捕获与回放（原始时序、倍速、单步）
- 插件扩展（运行时加载 DLL）
- UI 中英切换 + 内置配置字段帮助面板
- UI 内置配置编辑：保存后自动重载配置并重启会话

## 2. 界面模块

应用主界面包含以下模块：
- `Session Manager`：启动/停止会话，添加默认拦截规则
- `Live Monitor`：高频帧监控（启用虚拟化）
- `Intercept Queue`：处理待决拦截帧
- `Rule Editor`：展示当前规则列表
- `Replay Controller`：加载 capture 并按时序回放或单步
- `Config Editor`：在 UI 内直接编辑配置并保存，保存后自动重载并重启会话
- `Config Help`：内置 `serialmitmproxy.json` 字段说明（中英文跟随 UI）

## 3. 目录结构

```text
SerialMitmProxy.sln
src/
  SerialMitmProxy.App              # WPF UI
  SerialMitmProxy.Application      # ViewModel / 应用层逻辑
  SerialMitmProxy.Core             # 代理核心、解码器、规则、拦截、捕获回放
  SerialMitmProxy.Infrastructure   # 串口实现、插件加载、内存端点
  SerialMitmProxy.Plugins          # 示例插件项目
tests/
  SerialMitmProxy.Core.Tests       # 单元+集成测试
config/
  serialmitmproxy.template.json    # 配置模板
release/
deploy/
  publish.ps1                      # 发布脚本
```

## 4. 快速开始

### 4.1 直接运行发布包

1. 进入 `release/win-x64-self-contained-config-ui`。
2. 将 `serialmitmproxy.template.json` 复制为 `serialmitmproxy.json`。
3. 按实际串口修改配置（`COM`、波特率、分帧模式等）。
4. 运行 `SerialMitmProxy.App.exe`。

说明：
- 默认发布脚本输出 `self-contained` 包，最终用户运行不依赖系统已安装的 .NET Runtime。
- 如果你改用 `-FrameworkDependent` 发布到 `release/win-x64`，则目标机器需安装匹配的 .NET 运行时。

### 4.2 从源码构建

```powershell
dotnet build SerialMitmProxy.sln -c Release
```

发布：

```powershell
./deploy/publish.ps1
```

或：

```powershell
dotnet publish src/SerialMitmProxy.App/SerialMitmProxy.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=false /p:PublishTrimmed=false -o release/win-x64-self-contained-config-ui
```

## 5. 高级功能重点：模板配置

模板文件：`config/serialmitmproxy.template.json`

程序启动时按以下优先级加载：
1. `serialmitmproxy.json`
2. `serialmitmproxy.template.json`
3. 内置默认值

### 5.1 核心配置字段

| 字段 | 说明 |
|---|---|
| `session.useInMemory` | `true` 使用内存端点（联调/测试），`false` 使用真实串口 |
| `session.endpointA/endpointB` | 串口参数：`portName`/`baudRate`/`dataBits`/`parity`/`stopBits`/`handshake` |
| `session.decoders.mode` | 分帧模式：`TimeSlice` / `Delimiter` / `FixedLength` |
| `session.decoders.timeSliceMs` | `TimeSlice` 模式空闲切帧阈值（毫秒） |
| `session.decoders.delimiterHex` | `Delimiter` 模式分隔符（如 `0D 0A`） |
| `session.decoders.fixedLength` | `FixedLength` 模式固定长度 |
| `capture.enabled` | 是否启用捕获 |
| `capture.folder` | 捕获输出目录 |
| `monitor.maxFrames` | UI 最大帧缓存 |
| `monitor.uiThrottleMs` | UI 刷新节流参数（毫秒，支持运行时配置） |
| `plugins.folder` | 插件 DLL 扫描目录 |

### 5.2 推荐模板用法

- 每类设备建立一份独立 JSON（例如 `serialmitmproxy.deviceA.json`）。
- 运行前复制目标模板为 `serialmitmproxy.json`。
- 团队协作时只提交模板，不提交环境私有配置。

### 5.3 Config Editor 中 Endpoint A / B 如何填写

核心原则：
- `Endpoint A` 填上游软件要连接的那一侧。
- `Endpoint B` 填真实设备所在的那一侧。
- 不要把同一对 `com0com` 虚拟串口的两端同时都填进本软件，否则会形成回环。

示例：
- `com0com` 创建了一对虚拟串口：`COM15 <-> COM16`
- 上位机软件连接 `COM15`
- 真实设备实际在 `COM3`

这时应这样设置：
- 本软件 `Endpoint A.portName = COM16`
- 本软件 `Endpoint B.portName = COM3`
- `Endpoint A` 和 `Endpoint B` 的 `baudRate`、`dataBits`、`parity`、`stopBits`、`handshake`，通常都按真实设备 `COM3` 的协议填写

```json
{
  "session": {
    "useInMemory": false,
    "endpointA": {
      "portName": "COM16",
      "baudRate": 9600,
      "dataBits": 8,
      "parity": "None",
      "stopBits": "One",
      "handshake": "None"
    },
    "endpointB": {
      "portName": "COM3",
      "baudRate": 9600,
      "dataBits": 8,
      "parity": "None",
      "stopBits": "One",
      "handshake": "None"
    }
  }
}
```

如果只是本地联调，不接真实设备，优先使用 `session.useInMemory = true`。

## 6. 高级功能重点：插件系统

插件用于在规则执行后对帧做额外变换。加载机制：
- 启动会话时扫描 `plugins.folder` 下所有 `*.dll`
- 反射实例化所有实现 `IFramePlugin` 的非抽象类型
- 在转发前按加载顺序依次调用 `Transform`

插件接口：

```csharp
public interface IFramePlugin
{
    string Name { get; }
    byte[] Transform(Direction direction, byte[] payload);
}
```

### 6.1 插件开发步骤

1. 新建 Class Library（建议 `net8.0`）。
2. 引用 `SerialMitmProxy.Core`。
3. 实现 `IFramePlugin`。
4. 构建得到 DLL，复制到运行目录下 `plugins` 文件夹。
5. 重启应用。

### 6.2 插件示例

```csharp
public sealed class UpperAsciiPlugin : IFramePlugin
{
    public string Name => "UpperAsciiPlugin";

    public byte[] Transform(Direction direction, byte[] payload)
    {
        var bytes = payload.ToArray();
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] >= (byte)'a' && bytes[i] <= (byte)'z')
            {
                bytes[i] = (byte)(bytes[i] - 32);
            }
        }
        return bytes;
    }
}
```

注意：
- 插件应避免阻塞与长耗时逻辑。
- 插件异常会影响会话稳定性，建议插件内部自处理错误。

## 7. 规则引擎能力

匹配器：
- `DirectionMatcher`
- `LengthMatcher`
- `HexPatternMatcher`（支持 `??` 通配）
- `RegexMatcher`（ASCII）

动作：
- `Pass`
- `Drop`
- `Modify`
- `Intercept`
- `Delay`
- `Inject`
- `Duplicate`

变换器：
- `ReplaceBytesTransformer`
- `PatchOffsetTransformer`
- `ChecksumFixTransformer`

## 8. 捕获与回放

- 捕获文件：`capture.bin` + `capture.idx`
- `capture.idx` 记录：时间戳、方向、偏移、长度
- 回放支持原始时序
- 回放支持倍速（`speedFactor`）
- 回放支持单步
- `Replay Control` 支持点击选择目录
- `Replay Control` 支持将当前内存中的捕获帧保存为可直接回放的 `capture.bin/capture.idx` 包

## 9. Live Monitor 与 Intercept Queue 说明

### 9.1 Live Monitor

- 左侧表显示 `原始入站`：上游软件发出的流量和设备回复的流量，刚进入代理时就会出现在这里。
- 右侧表显示 `实际转发后`：经过规则、插件、拦截决策后，最终真正写到对端的流量。
- 如果在拦截队列中执行了 `编辑后转发`，右侧表会对这条已修改报文做高亮。
- `HEX / ASCII` 现在是切换显示，不再同时占两列。这样长报文能拿到更宽的显示空间，避免被截断。
- 在 `Rule Editor` 中右键可新增 `HEX 过滤规则` 或 `ASCII 匹配规则`。只要存在已启用的监控规则，`Live Monitor` 就只显示命中这些规则的流量。

### 9.2 `Repeat x2` 的含义

- 拦截队列表格支持直接双击编辑报文。点击 `Forward` 时，会按你当前编辑后的内容转发；如果内容没有变化，则按原报文直接放行。
- `Intercept Queue` 内置 `Checksum / CRC` 助手，当前支持 `Checksum-8(SUM)`、`Checksum-8(XOR)`、`CRC-8`、`CRC-16/MODBUS`、`CRC-16/IBM`、`CRC-16/CCITT-FALSE`，可在编辑报文后直接覆盖尾部校验位或追加校验位。
- `Repeat x2` 表示把当前被拦截的这条报文总共发送两次。
- 它不是“额外再发两次”，而是“最终发送总次数 = 2”。
- 这个功能常用于测试设备对重复帧、重发帧或幂等处理的反应。

## 10. 测试状态

已实现自动化测试覆盖：
- 解码器正确性
- 规则引擎行为
- 双向转发
- 拦截流程
- 捕获回放一致性

本地验证命令：

```powershell
dotnet build SerialMitmProxy.sln -v minimal
dotnet test tests/SerialMitmProxy.Core.Tests/SerialMitmProxy.Core.Tests.csproj --framework net10.0 -v minimal
```

说明：
- 应用项目目标框架为 `net8.0-windows`。
- 测试项目支持 `net8.0` 与 `net10.0` 多目标，便于在不同本机 SDK 环境下执行测试。
- 上述目标框架是开发/编译信息；用户运行 `release/win-x64-self-contained-config-ui` 发布包时不要求预装 .NET Runtime。

## 11. 已知限制与建议

- `Rule Editor` 当前主要用于展示；复杂规则建议通过应用层 API（`RuleProfileStore`）接入。
- 尚未内置插件沙箱机制；生产环境请仅加载可信插件。
- 1 小时高吞吐压测脚本未内置，建议在目标设备链路上做场景化压力验证。

## 12. 许可证与使用声明

当前仓库未附加独立许可证文件。若用于商业/外部发布，请先补充许可证与合规说明。
