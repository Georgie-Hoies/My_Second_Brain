using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Collections.Generic;
using System.Linq;

// 【关键修改】使用别名解决冲突 (修复所有 31 处命名冲突)
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MenuItem = System.Windows.Controls.MenuItem;

namespace My_Second_Brain
{
    public partial class MainWindow : Window
    {
        // 状态变量：存储当前选中的颜色和笔触大小，以便在切换工具时恢复设置
        private Color _currentColor = Colors.Black;
        private double _currentSize = 4.0;
        private double _eraserSize = 10.0; // 橡皮擦大小

        // --- 【新增】撤销/重做功能所需的字段 ---
        private readonly Stack<StrokeCollection> _undoStack = new Stack<StrokeCollection>(); // 撤销历史堆栈
        private readonly Stack<StrokeCollection> _redoStack = new Stack<StrokeCollection>(); // 重做历史堆栈
        private bool _isChangingStrokes = false; // 保护标志：防止在执行撤销/重做时，再次触发保存状态的逻辑

        // 记录当前激活的工具按钮 (用于实现 OneNote 风格的二次点击弹出菜单)
        private System.Windows.Controls.RadioButton? _activeToolButton;

        // 【新增】记录当前的橡皮擦模式 (Stroke 或 Point)
        // 修复 CS0103: The name '_eraserMode' does not exist
        private System.Windows.Controls.InkCanvasEditingMode _eraserMode = System.Windows.Controls.InkCanvasEditingMode.EraseByStroke;

        public MainWindow()
        {
            InitializeComponent();

            // 【新增】纯代码绑定键盘按下事件，使 Ctrl+Z/Y 等快捷键生效，并保持 XAML 界面文件整洁。
            this.KeyDown += Window_KeyDown;
        }

        // 画布加载完成时的初始化逻辑
        private void OnInkCanvasLoaded(object sender, RoutedEventArgs e)
        {
            // 默认初始化为钢笔模式
            UpdateDrawingAttributes("Pen");

            // 【新增】监听墨迹创建完成事件 (笔和荧光笔)
            MainInkCanvas.StrokeCollected += (s, args) => PushToUndoStack();
            // 【新增】监听墨迹擦除完成事件 (橡皮)
            MainInkCanvas.StrokeErased += (s, args) => PushToUndoStack();

            // 【新增】在初始化时保存一个空的初始状态
            PushToUndoStack();
        }

        // ---------------------------------------------------------
        // 【核心】沉浸式分级菜单状态机
        // ---------------------------------------------------------

        // 1. 工具切换逻辑 (替换原有的 OnToolChanged)
        private void OnToolChanged(object sender, RoutedEventArgs e)
        {
            // 安全检查：确保发送者是 RadioButton 且 Tag 属性已设置
            if (sender is System.Windows.Controls.RadioButton rb && rb.Tag != null)
            {
                string toolType = rb.Tag.ToString()!;

                // --- OneNote 核心交互逻辑：二次点击橡皮擦呼出菜单 ---
                if (toolType != "Select" && toolType != "Text" && _activeToolButton == rb)
                {
                    if (rb.ContextMenu != null)
                    {
                        rb.ContextMenu.PlacementTarget = rb;
                        rb.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                        rb.ContextMenu.IsOpen = true;
                    }
                    return; // 既然只是打开菜单，就不需要重复执行下面的切换逻辑了
                }

                _activeToolButton = rb; // 记录当前工具，供后续逻辑使用

                // 切换画布模式
                if (toolType == "Select")
                {
                    MainInkCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Select;
                    MainInkCanvas.Select((System.Windows.Ink.StrokeCollection)null!);
                }
                else if (toolType == "Text")
                {
                    MainInkCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.None;
                }
                else if (toolType == "Eraser")
                {
                    ApplyEraserMode(); // 应用当前的橡皮设置
                }
                else // Pen, Highlighter
                {
                    MainInkCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
                    UpdateDrawingAttributes(toolType);
                }
            }
        }

        // 【新增】处理橡皮擦菜单点击事件 (修复 CS1061 错误)
        private void OnEraserMenuClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem clickedItem)
            {
                // 1. 实现菜单项单选逻辑 (互斥)：把其他选项的勾去掉
                var menu = (System.Windows.Controls.ContextMenu)clickedItem.Parent;
                foreach (MenuItem item in menu.Items.OfType<MenuItem>())
                {
                    item.IsChecked = (item == clickedItem);
                }

                // 2. 如果当前正在使用橡皮擦，立即应用新模式
                if (_activeToolButton != null && _activeToolButton.Tag?.ToString() == "Eraser" && _activeToolButton.IsChecked == true)
                {
                    ApplyEraserMode();
                }
            }
        }

        // 辅助方法：应用当前的橡皮擦设置 (从菜单状态读取)
        private void ApplyEraserMode()
        {
            System.Windows.Controls.ContextMenu? menu = null;
            if (_activeToolButton != null && _activeToolButton.Tag?.ToString() == "Eraser")
            {
                menu = _activeToolButton.ContextMenu;
            }

            if (MainInkCanvas == null || menu == null) return;

            // 遍历菜单，找到那个被打钩 (IsChecked) 的项
            foreach (MenuItem item in menu.Items.OfType<MenuItem>())
            {
                if (item.IsChecked && item.Tag != null)
                {
                    string mode = item.Tag.ToString()!;

                    if (mode == "Stroke")
                    {
                        MainInkCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.EraseByStroke;
                    }
                    else if (mode == "Point")
                    {
                        MainInkCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.EraseByPoint;

                        // 设置橡皮擦的大小 (你可以根据需要调整这个数值，这里设为 10x10)
                        MainInkCanvas.EraserShape = new System.Windows.Ink.RectangleStylusShape(_eraserSize, _eraserSize);
                    }
                    break; // 找到一个就够了
                }
            }
        }

        // 当用户勾选或取消“书法模式”时触发
        private void OnAttributeChanged(object sender, RoutedEventArgs e)
        {
            if (_activeToolButton?.Tag?.ToString() == "Pen") UpdateDrawingAttributes("Pen");
            else if (_activeToolButton?.Tag?.ToString() == "Highlighter") UpdateDrawingAttributes("Highlighter");
        }

        // 核心方法：更新画笔属性
        private void UpdateDrawingAttributes(string toolType)
        {
            if (MainInkCanvas == null) return;

            DrawingAttributes attributes = new DrawingAttributes();
            attributes.Color = _currentColor;
            attributes.Width = _currentSize;
            attributes.Height = _currentSize;
            attributes.FitToCurve = true;

            // 查找 CheckCalligraphy 状态 (需确保 XAML 中有 x:Name="CheckCalligraphy")
            bool isCalligraphy = false;
            object chk = this.FindName("CheckCalligraphy");
            if (chk is System.Windows.Controls.CheckBox c && c.IsChecked == true)
            {
                isCalligraphy = true;
            }

            if (toolType == "Pen" && isCalligraphy)
            {
                System.Windows.Media.Matrix matrix = new System.Windows.Media.Matrix();
                matrix.Rotate(45);
                matrix.Scale(1.0, 0.3);
                attributes.StylusTipTransform = matrix;
                attributes.StylusTip = System.Windows.Ink.StylusTip.Ellipse;
            }

            if (toolType == "Highlighter")
            {
                attributes.IsHighlighter = true;
                attributes.StylusTip = System.Windows.Ink.StylusTip.Rectangle;
                attributes.Width = 20; attributes.Height = 20;
                if (_currentColor == Colors.Black) attributes.Color = Colors.Yellow;
            }
            else if (!isCalligraphy)
            {
                attributes.IsHighlighter = false;
                attributes.StylusTip = System.Windows.Ink.StylusTip.Ellipse;
            }
            MainInkCanvas.DefaultDrawingAttributes = attributes;
        }

        // 处理颜色点击事件
        private void OnColorPicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Background is System.Windows.Media.SolidColorBrush brush)
            {
                _currentColor = brush.Color;
                if (_activeToolButton?.Tag?.ToString() == "Highlighter") UpdateDrawingAttributes("Highlighter");
                else UpdateDrawingAttributes("Pen");
            }
        }

        // 处理滑块拖动事件，实时调整笔触大小
        private void OnSizeChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            _currentSize = e.NewValue;

            if (_activeToolButton?.Tag?.ToString() == "Highlighter") UpdateDrawingAttributes("Highlighter");
            else UpdateDrawingAttributes("Pen");
        }

        // 【新增】处理固定粗细选择 (0.25mm - 3.5mm)
        private void OnThicknessChanged(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && rb.Tag != null)
            {
                // Tag 存储的是 UI 上圆点的直径 (4, 6, 8...)
                // 我们需要把它映射回实际的笔触粗细 (像素)

                double dotSize = double.Parse(rb.Tag.ToString()!);

                // 简单的映射逻辑：
                // UI圆点 4   -> 笔触 1px   (0.25mm)
                // UI圆点 6   -> 笔触 1.5px (0.35mm)
                // UI圆点 8   -> 笔触 2px   (0.5mm)
                // UI圆点 12  -> 笔触 4px   (1mm)
                // UI圆点 16  -> 笔触 8px   (2mm)
                // UI圆点 20  -> 笔触 14px  (3.5mm)

                // 简单公式模拟：
                if (dotSize <= 4) _currentSize = 1.0;
                else if (dotSize <= 6) _currentSize = 1.5;
                else if (dotSize <= 8) _currentSize = 2.0;
                else if (dotSize <= 12) _currentSize = 4.0;
                else if (dotSize <= 16) _currentSize = 8.0;
                else _currentSize = 14.0;

                // 立即刷新属性
                if (_activeToolButton?.Tag?.ToString() == "Highlighter")
                    UpdateDrawingAttributes("Highlighter");
                else
                    UpdateDrawingAttributes("Pen");
            }
        }

        // 处理橡皮擦模式单选框变化
        private void OnEraserModeRadioChanged(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && rb.Tag != null)
            {
                // 更新状态变量
                if (rb.Tag.ToString() == "Stroke")
                    _eraserMode = System.Windows.Controls.InkCanvasEditingMode.EraseByStroke;
                else
                    _eraserMode = System.Windows.Controls.InkCanvasEditingMode.EraseByPoint;

                // 如果当前工具是橡皮，立即应用
                if (_activeToolButton?.Tag?.ToString() == "Eraser")
                    UpdateEraserSettings();
            }
        }

        // 视图切换：返回 L3 工具列表 (新增方法，绑定到 Back 按钮)
        // 此方法在当前 Rich Flyout 模式中已无用，但为防止 XAML 报错，需保留定义
        private void OnBackToToolsClick(object sender, RoutedEventArgs e)
        {
            // 保持方法定义，避免 XAML 找不到 Click 事件目标
        }

        // 橡皮擦大小滑块变化
        private void OnEraserSizeChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            _eraserSize = e.NewValue;
            UpdateEraserSettings();
        }

        // 辅助方法：实时应用橡皮擦设置 (模式 + 大小)
        private void UpdateEraserSettings()
        {
            if (MainInkCanvas == null) return;

            // 直接使用变量状态，而不是去查 UI 控件，更稳定
            if (_eraserMode == System.Windows.Controls.InkCanvasEditingMode.EraseByStroke)
            {
                MainInkCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.EraseByStroke;
            }
            else
            {
                MainInkCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.EraseByPoint;
                MainInkCanvas.EraserShape = new System.Windows.Ink.RectangleStylusShape(_eraserSize, _eraserSize);
            }
        }

        // ---------------------------------------------------------
        // 【新增】高级功能支持 (HEX取色器 / 橡皮实时调整)
        // ---------------------------------------------------------

        // 调用 Windows 系统调色板 (需引用 System.Windows.Forms)
        private void OnMoreColorsClick(object sender, RoutedEventArgs e)
        {
            // 使用别名 WinForms 来实例化
            WinForms.ColorDialog colorDialog = new WinForms.ColorDialog();
            colorDialog.AllowFullOpen = true; // 允许展开自定义颜色 (RGB/HEX)
            colorDialog.FullOpen = true;      // 默认展开

            // 使用别名 WinForms 来判断结果
            if (colorDialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                // 这里的 color 是 WinForms 的 (System.Drawing.Color)
                Drawing.Color formColor = colorDialog.Color;

                // 手动转换为 WPF 的颜色 (System.Windows.Media.Color)
                _currentColor = System.Windows.Media.Color.FromArgb(formColor.A, formColor.R, formColor.G, formColor.B);

                // 立即应用新颜色
                if (_activeToolButton?.Tag?.ToString() == "Highlighter") UpdateDrawingAttributes("Highlighter");
                else UpdateDrawingAttributes("Pen");
            }
        }

        // 【新增】辅助方法：保存当前墨迹状态到撤销堆栈
        private void PushToUndoStack()
        {
            // 如果当前操作是由 Undo/Redo 引起的，则忽略（防止无限递归）
            if (_isChangingStrokes) return;

            // 克隆当前的墨迹集合，并推入撤销堆栈
            // Clone() 是关键，确保我们保存的是一个独立的副本，而不是引用
            _undoStack.Push(MainInkCanvas.Strokes.Clone());

            // 任何新操作（画笔、橡皮、移动等）发生时，都必须清空重做堆栈
            _redoStack.Clear();
        }

        // 【新增】处理撤销 (Undo) 按钮点击
        private void OnUndoClick(object sender, RoutedEventArgs e)
        {
            // 如果堆栈中只剩下初始状态（或更少），则无法撤销
            if (_undoStack.Count > 1)
            {
                // 1. 将当前状态（即即将被撤销的状态）保存到重做堆栈
                _redoStack.Push(MainInkCanvas.Strokes.Clone());

                // 2. 弹出当前状态
                _undoStack.Pop();

                // 3. 获取上一个状态（现在栈顶就是上一个有效状态）
                StrokeCollection previousState = _undoStack.Peek();

                // 4. 应用上一个状态
                _isChangingStrokes = true; // 开启保护标志
                // 必须再次克隆应用，防止两个堆栈共享同一个 StrokeCollection 实例
                MainInkCanvas.Strokes = previousState.Clone();
                _isChangingStrokes = false; // 关闭保护标志
            }
        }

        // 【新增】处理重做 (Redo) 按钮点击
        private void OnRedoClick(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count > 0)
            {
                // 1. 获取重做状态
                StrokeCollection nextState = _redoStack.Pop();

                // 2. 将当前状态保存到撤销堆栈（因为Redo本身也是一个新操作）
                _undoStack.Push(MainInkCanvas.Strokes.Clone());

                // 3. 应用重做状态
                _isChangingStrokes = true; // 开启保护标志
                MainInkCanvas.Strokes = nextState; // 不需要克隆，因为它刚从 Redo 堆栈弹出，是独立的
                _isChangingStrokes = false; // 关闭保护标志
            }
        }

        // 清空画布所有内容
        private void OnClearCanvas(object sender, RoutedEventArgs e)
        {
            MainInkCanvas.Strokes.Clear();
            // 【重要修复】清空也是一种操作，必须记录到历史堆栈中，否则无法撤销“清空”！
            PushToUndoStack();
        }

        // 保存功能：将墨迹保存为 .isf (Ink Serialized Format) 文件
        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            // 【修复】使用 WPF 原生 SaveFileDialog (需要加上 Microsoft.Win32 前缀以防二义性)
            Microsoft.Win32.SaveFileDialog saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.Filter = "Ink File (*.isf)|*.isf"; // 文件过滤器
            saveDialog.FileName = "My_Notes";

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    using (FileStream fs = new FileStream(saveDialog.FileName, FileMode.Create))
                    {
                        // 核心保存 API：直接序列化 Strokes 集合
                        MainInkCanvas.Strokes.Save(fs);
                    }
                    MessageBox.Show("Save Successful!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Save Failed: {ex.Message}");
                }
            }
        }

        // 加载功能：读取 .isf 文件并还原到画布
        private void OnLoadClick(object sender, RoutedEventArgs e)
        {
            // 【修复】使用 WPF 原生 OpenFileDialog
            Microsoft.Win32.OpenFileDialog openDialog = new Microsoft.Win32.OpenFileDialog();
            openDialog.Filter = "Ink File (*.isf)|*.isf";

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    using (FileStream fs = new FileStream(openDialog.FileName, FileMode.Open))
                    {
                        // 核心加载 API：从文件流反序列化为 StrokeCollection
                        StrokeCollection strokes = new StrokeCollection(fs);
                        MainInkCanvas.Strokes = strokes;

                        // 【优化】加载新文件后，将这个新状态也推入撤销栈，允许用户撤销“加载”操作
                        PushToUndoStack();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Load Failed: {ex.Message}");
                }
            }
        }

        // 【新增】处理键盘快捷键 (Ctrl+Z 撤销, Ctrl+Y 重做)
        // 【修复】显式指定 System.Windows.Input.KeyEventArgs 以避免二义性
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 检查 Ctrl 键是否被按住
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.Z)
                {
                    // 如果按下了 Z，调用撤销方法
                    OnUndoClick(null!, null!);
                }
                else if (e.Key == Key.Y)
                {
                    // 如果按下了 Y，调用重做方法
                    OnRedoClick(null!, null!);
                }
            }
        }

        // ---------------------------------------------------------
        // 【新增】处理纸张背景切换 (空白 / 横线 / 网格)
        // ---------------------------------------------------------
        private void OnPaperStyleChanged(object sender, SelectionChangedEventArgs e)
        {
            // 安全检查
            if (MainInkCanvas == null) return;
            // 【修复】使用 WPF 的 ComboBox
            if (sender is System.Windows.Controls.ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                string style = item.Tag.ToString()!;

                if (style == "None")
                {
                    // 1. 空白模式：直接设为纯白
                    MainInkCanvas.Background = Brushes.White;
                }
                else if (style == "Rule")
                {
                    // 2. 横线模式：创建横线画刷
                    MainInkCanvas.Background = CreateRuleBrush();
                }
                else if (style == "Grid")
                {
                    // 3. 网格模式：创建网格画刷
                    MainInkCanvas.Background = CreateGridBrush();
                }
            }
        }

        // 辅助方法：创建横线背景
        private DrawingBrush CreateRuleBrush()
        {
            // 使用 DrawingGroup 以便叠加图层 (背景层 + 线条层)
            var drawingGroup = new DrawingGroup();

            // 1. 先画“纸”：铺一层纯白色的背景
            // 防止背景透明导致颜色叠加异常
            drawingGroup.Children.Add(new GeometryDrawing(
                Brushes.White,
                null,
                new RectangleGeometry(new Rect(0, 0, 100, 40))));

            // 2. 再画“线”：在底部画一条浅蓝色的线
            // 定义线条颜色和粗细 (浅蓝色，0.5像素)
            drawingGroup.Children.Add(new GeometryDrawing(
                null,
                new Pen(Brushes.LightBlue, 0.5),
                new LineGeometry(new Point(0, 40), new Point(100, 40))));

            // 创建画刷
            DrawingBrush brush = new DrawingBrush(drawingGroup);

            // 【关键】设置平铺模式
            brush.TileMode = TileMode.Tile; // 像瓷砖一样重复

            // Viewport 定义了“一块瓷砖”的大小：宽 100 (相对值不重要)，高 40 (行高)
            // BrushMappingMode.Absolute 表示我们用像素单位
            brush.Viewport = new Rect(0, 0, 100, 40);
            brush.ViewportUnits = BrushMappingMode.Absolute;
            brush.Freeze(); // 冻结对象以提升性能

            return brush;
        }

        // 辅助方法：创建网格背景
        private DrawingBrush CreateGridBrush()
        {
            // 使用 DrawingGroup 以便叠加图层
            var drawingGroup = new DrawingGroup();

            // 1. 先画“纸”：铺一层纯白色的背景
            drawingGroup.Children.Add(new GeometryDrawing(
                Brushes.White,
                null,
                new RectangleGeometry(new Rect(0, 0, 20, 20))));

            // 2. 再画“格”：创建两个几何图形（一条横线，一条竖线）
            var lineGroup = new GeometryGroup();
            lineGroup.Children.Add(new LineGeometry(new Point(0, 0), new Point(20, 0))); // 横线
            lineGroup.Children.Add(new LineGeometry(new Point(0, 0), new Point(0, 20))); // 竖线

            // 定义颜色 (浅灰色，0.5像素)
            drawingGroup.Children.Add(new GeometryDrawing(
                null,
                new Pen(Brushes.LightGray, 0.5),
                lineGroup));

            // 创建画刷
            DrawingBrush brush = new DrawingBrush(drawingGroup);

            // 【关键】设置平铺模式
            brush.TileMode = TileMode.Tile;

            // 设置格子大小为 20x20 像素
            brush.Viewport = new Rect(0, 0, 20, 20);
            brush.ViewportUnits = BrushMappingMode.Absolute;
            brush.Freeze();

            return brush;
        }

        // ---------------------------------------------------------
        // 【新增】水平滚轮支持 (Logitech 等鼠标)
        // ---------------------------------------------------------

        // 重写系统初始化方法，挂载消息钩子
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // 获取当前窗口句柄，并添加消息处理钩子
            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            source?.AddHook(WndProc);
        }

        // 核心消息处理函数：拦截 Windows 系统消息
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Windows 消息常量：水平滚轮消息 ID
            const int WM_MOUSEHWHEEL = 0x020E;

            if (msg == WM_MOUSEHWHEEL)
            {
                // 解析滚轮偏移量 (高 16 位)
                int tilt = (short)((wParam.ToInt64() >> 16) & 0xFFFF);

                // 【新增】智能判断：鼠标在哪，就滚谁
                // 如果鼠标悬停在工具栏上，就滚动工具栏
                if (ToolbarScrollViewer.IsMouseOver)
                {
                    if (tilt > 0)
                    {
                        ToolbarScrollViewer.LineRight();
                        ToolbarScrollViewer.LineRight();
                    }
                    else if (tilt < 0)
                    {
                        ToolbarScrollViewer.LineLeft();
                        ToolbarScrollViewer.LineLeft();
                    }
                    handled = true;
                }
                // 否则，默认滚动画布 (前提是画布不为空)
                else if (MainScrollViewer != null)
                {
                    if (tilt > 0)
                    {
                        MainScrollViewer.LineRight();
                        MainScrollViewer.LineRight();
                        MainScrollViewer.LineRight(); // 画布很大，滚快一点
                    }
                    else if (tilt < 0)
                    {
                        MainScrollViewer.LineLeft();
                        MainScrollViewer.LineLeft();
                        MainScrollViewer.LineLeft();
                    }
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }
    }
}