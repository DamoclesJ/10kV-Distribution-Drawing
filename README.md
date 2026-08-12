# 10kV Distribution Drawing

10kV 配电工作票附图绘制软件。

## 项目目标

本项目面向 10kV 配电工作票附图，提供设备放置、端子连接、属性与状态编辑、工程保存、JPG 导出和 Windows 打印能力，形成可保存、可重新打开并继续编辑的结构化绘图工程。

项目不承担潮流计算、电气仿真、自动停电分析或自动生成工作地线等职责。

## 软件定位

软件定位为 Windows 离线桌面端的结构化配电单线图编辑器，而不是通用 CAD。

用户编辑的是设备、端子、连接、操作状态、电气状态、工作范围、工作地线和布局等领域对象。画布图元、颜色、线路和文字是这些语义数据的可视化结果，不作为工程数据的事实源。

## 当前技术路线

- 目标平台：Windows 桌面。
- 运行时与语言：.NET 10 LTS、C#。
- UI 框架：WPF，采用 MVVM。
- 软件结构：多项目的模块化单体。
- 绘图方式：WPF 原生矢量场景，画布、JPG 和打印共享绘制语义。
- 工程文件：本地版本化结构化文件，领域对象与持久化 DTO 分离。
- 数据存储：本地文件系统，不使用数据库或云服务。

## MVP 范围

### 设备与连接

- 普通负荷开关型环网柜：3、4、5、6 个普通间隔。
- 一二次融合环网柜：4、6 个普通间隔，以及柜内 PT 间隔和关联 DTU。
- 电缆。
- 水泥杆。
- 架空线路。
- 柱上断路器、柱上负荷开关、柱上隔离开关和跌落式熔断器。
- 杆塔附属关系及电缆终端。

PT 不是独立柜体，而是一二次融合环网柜内部的特殊间隔。DTU 只作为与 PT 固定关联的独立布局柜体，不参与一次电气拓扑，也不作为设备库中的独立设备类别。

### 编辑与安全表达

- 从设备库拖放、选择和移动设备。
- 编辑设备、线路和标注所需属性。
- 独立修改适用开关的拉开、合入状态。
- 通过端子创建电缆和架空线路连接。
- 人工定义由两个端子边界构成的工作范围。
- 人工添加、编号并关联工作地线。
- 表达架空线路延续及人工确认的延续状态。

### 保存与输出

- 保存本地工程并重新打开继续编辑。
- 导出完整页面 JPG。
- 通过 Windows 打印体系进行预览和打印。

### MVP 明确不包含

- CAD DWG 导入、导出或转换。
- 潮流计算和电气仿真。
- 自动停电分析。
- 自动生成工作地线或其他安全措施。
- 数据库、云同步和多用户协作。

## 核心设计理念

- 语义模型是唯一事实源，图形对象不进入领域模型。
- 电气拓扑与画布布局分离，移动设备不改变连接语义。
- 操作状态与电气状态分离，不根据开关状态自动推导带电范围。
- 所有电气连接必须落到端子，图形相交不代表电气导通。
- 环网柜使用容器、间隔和内部设备的组合模型，内部开关状态独立保存。
- 柱上设备和电缆终端通过杆塔附属关系安装到杆塔，不保存为悬空设备。
- 工作范围和工作地线由用户人工定义，不自动生成安全措施。
- 画布、JPG 和打印共用同一绘制语义。

## 当前开发阶段

当前主线：**Drawing Core Production Readiness**。

已完成：

- 工程会话基础能力。
- Pole/RingCabinet 基础放置与移动。
- 基于 Terminal 的 OverheadLine 连线。
- Zoom/Pan/Fit 画布视图能力。
- Persistence Core。
- Professional Core。

当前下一目标：**P0-6-B Minimal Configurable RingCabinet**。

详细当前状态请阅读 [`docs/project-current-state.md`](docs/project-current-state.md)。

## 文档目录

| 文档 | 说明 |
| --- | --- |
| [`docs/requirements.md`](docs/requirements.md) | MVP 正式范围、操作要求、输出要求和验收边界。 |
| [`docs/architecture.md`](docs/architecture.md) | 产品定位、总体架构、领域分层和开发里程碑。 |
| [`docs/equipment-model.md`](docs/equipment-model.md) | Device、Terminal、Connection、Pole、WorkScope、GroundingPoint 等核心领域模型。 |
| [`docs/ring-cabinet-design.md`](docs/ring-cabinet-design.md) | 两类环网柜、普通间隔、PT 间隔及 DTU 联动设计。 |
| [`docs/drawing-rule.md`](docs/drawing-rule.md) | 配电工作票和现场勘察附图的专业绘图规则及软件模型映射。 |
| [`docs/implementation-plan.md`](docs/implementation-plan.md) | .NET/WPF 技术路线、项目边界、工程文件、绘制输出和实施顺序。 |
| `reference/` | 项目使用的原始规范资料，仅作为专业规则和图元核对依据。 |

如文档之间出现 MVP 范围差异，以 `docs/requirements.md` 为第一阶段范围判断依据；专业图面表达以确认后的规范整理结果为准。
