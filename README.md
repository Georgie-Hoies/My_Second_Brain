# My Second Brain (WPF)

一款基于 Windows Presentation Foundation (WPF) 开发的桌面端“第二大脑”手写笔记应用。旨在模仿并超越 OneNote 的核心体验，提供流畅的墨迹书写、无限画布与混合排版能力。

## 📦 当前版本状态 (Base Version)

**Core Features (核心功能):**

* **Ink Engine (墨迹引擎)**:
    * 基于 `InkCanvas` 的高性能书写。
    * 支持 **Pen (钢笔)**、**Highlighter (荧光笔)**、**Eraser (橡皮擦)**。
    * **Calligraphy Mode (书法模式)**: 独特的笔锋算法 (Matrix Transform)，模拟真实钢笔书写质感。
* **Editing Tools (编辑工具)**:
    * **Lasso Select (套索选择)**: 支持自由圈选、移动、缩放墨迹。
* **History System (历史记录)**:
    * 完整的 **Undo/Redo (撤销/重做)** 支持 (Ctrl+Z / Ctrl+Y)。
    * 基于 `Memento Pattern` (备忘录模式) 的双堆栈架构，覆盖所有绘图与文件操作。
* **User Interface (界面)**:
    * **Ribbon UI**: 现代化的选项卡布局 (Home, Draw, View)。
    * **Paper Styles**: 支持切换 **Rule (横线)** 和 **Grid (网格)** 背景，基于高性能 `DrawingBrush` 渲染。
* **Input Support (输入支持)**:
    * 支持 **Horizontal Scrolling (水平滚动)** (适配 Logitech 等鼠标滚轮)。
    * 支持键盘快捷键监听。

## 📅 更新日志 (Changelog)

### [v1.1.0-pre] - 2025-11-25
**Major UI Refactor & Interaction Upgrade (界面重构与交互升级)**
* **✨ Ribbon UI 重构**: 全面升级为 **TabControl (选项卡)** 布局，划分为 **Home (常用)**、**Draw (绘图)**、**View (视图)** 三大分区。
* **🧹 橡皮擦交互进化**:
    * 实现 **Stroke (笔划擦除)** 与 **Point (点擦除)** 模式切换。
    * **智能交互**: 完美复刻 OneNote 逻辑——点击选中，再次点击（或右键）呼出配置菜单。
    * **视觉反馈**: 选中时动态显示下拉箭头 `⌵`，修复了菜单弹出位置偏移的问题。
* **🖱️ 输入增强**: 新增 **水平滚轮 (Horizontal Scrolling)** 支持，智能识别鼠标位置（工具栏/画布）进行滚动。
* **🎨 渲染优化**: 修复了 **Rule/Grid** 背景在透明底色下的渲染叠加错误（增加白色底图层）。
* **🔤 功能预埋**: UI 新增 **Text (文本)** 工具按钮，为下一阶段的混合输入做准备。

### [v0.4.0] - 2025-11-25
**Paper Styles (纸张背景支持)**
* **📄 背景系统**: 新增 **Rule (横线)** 和 **Grid (网格)** 两种经典笔记背景。
* **⚡ 高性能渲染**: 使用 `DrawingBrush` 和 `TileMode` 技术，确保在大尺寸画布下的流畅度。

### [v0.3.0] - 2025-11-25
**History System (时光机)**
* **↺ 撤销/重做**: 实现了基于 **备忘录模式 (Memento Pattern)** 的双堆栈历史记录系统。
* **⌨️ 快捷键**: 支持全局 **Ctrl+Z (Undo)** 和 **Ctrl+Y (Redo)** 快捷键监听。
* **🛡️ 全覆盖**: 涵盖了绘图、擦除、清空画布、文件加载等所有关键操作。

### [v0.2.0] - 2025-11-25
**Ink Engine Upgrade (墨迹引擎升级)**
* **✒️ 书法模式 (Calligraphy)**: 引入矩阵变换 (Matrix Transform)，模拟 45° 倾斜钢笔笔锋，提升书写质感。
* **🖐️ 选择模式**: 新增 **Lasso Select (套索选择)** 工具，支持墨迹的自由圈选、移动和缩放。

### [v0.1.0] - 2025-11-25
**MVP (最小可行性产品)**
* **🎨 基础绘图**: 基于 `InkCanvas` 实现毫无延迟的书写体验。
* **🛠️ 基础工具**: 实现了 **Pen (钢笔)**、**Highlighter (荧光笔)**、**Eraser (橡皮擦)** 的基础切换逻辑。
* **💾 文件系统**: 支持 `.isf` (Ink Serialized Format) 格式的保存与读取。

## 🛠 Tech Stack (技术栈)

* **Framework**: .NET 8.0 / .NET 9.0 (WPF)
* **IDE**: Visual Studio 2026
* **Language**: C# 12.0

## 🚀 快速开始

1. 克隆仓库到本地。
2. 使用 Visual Studio 打开 `.sln` 解决方案文件。
3. 按 `F5` 运行。

---
*Created by 朱彦燊 (云彦) - 2026*