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