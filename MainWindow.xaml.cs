using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop; // 【新增】用于处理 Windows 系统底层消息 (如水平滚轮)

namespace My_Second_Brain
{
    public partial class MainWindow : Window
    {
        // 状态变量：存储当前选中的颜色和笔触大小，以便在切换工具时恢复设置
        private Color _currentColor = Colors.Black;
        private double _currentSize = 4.0;

        // --- 【新增】撤销/重做功能所需的字段 ---
        private readonly Stack<StrokeCollection> _undoStack = new Stack<StrokeCollection>(); // 撤销历史堆栈
        private readonly Stack<StrokeCollection> _redoStack = new Stack<StrokeCollection>(); // 重做历史堆栈
        private bool _isChangingStrokes = false; // 保护标志：防止在执行撤销/重做时，再次触发保存状态的逻辑

        // 记录当前激活的工具按钮 (用于实现 OneNote 风格的二次点击弹出菜单)
        private RadioButton? _activeToolButton;

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

        // 处理工具切换（笔、荧光笔、橡皮、文本）
        private void OnToolChanged(object sender, RoutedEventArgs e)
        {
            // 安全检查：确保发送者是 RadioButton 且 Tag 属性已设置
            if (sender is RadioButton rb && rb.Tag != null)
            {
                string toolType = rb.Tag.ToString()!;

                // --- OneNote 核心交互逻辑：二次点击橡皮擦呼出菜单 ---
                if (toolType == "Eraser" && _activeToolButton == rb)
                {
                    if (rb.ContextMenu != null)
                    {
                        // 【修复】显式指定菜单的“锚点”为当前按钮
                        rb.ContextMenu.PlacementTarget = rb;
                        rb.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;

                        // 强制打开
                        rb.ContextMenu.IsOpen = true;
                    }
                    return;
                }
                // ------------------------------------------------

                // 更新当前激活的按钮记录
                _activeToolButton = rb;

                // --- 1. 选择模式 ---
                if (toolType == "Select")
                {
                    MainInkCanvas.EditingMode = InkCanvasEditingMode.Select;
                    MainInkCanvas.Select((StrokeCollection)null!);
                }
                // --- 2. 文本模式 (v1.1.0 预留) ---
                else if (toolType == "Text")
                {
                    // 暂时先设为 None，后续我们会在这里加点击生成文本框的逻辑
                    MainInkCanvas.EditingMode = InkCanvasEditingMode.None;
                }
                // --- 3. 橡皮擦模式 (本次修改重点) ---
                else if (toolType == "Eraser")
                {
                    // 调用辅助方法来设置具体的擦除模式
                    ApplyEraserMode();
                }
                // --- 4. 笔刷模式 ---
                else
                {
                    MainInkCanvas.EditingMode = InkCanvasEditingMode.Ink;
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
                var menu = (ContextMenu)clickedItem.Parent;
                foreach (MenuItem item in menu.Items.OfType<MenuItem>())
                {
                    item.IsChecked = (item == clickedItem);
                }

                // 2. 如果当前正在使用橡皮擦，立即应用新模式
                if (ToolEraser.IsChecked == true)
                {
                    ApplyEraserMode();
                }
            }
        }

        // 辅助方法：应用当前的橡皮擦设置 (从菜单状态读取)
        private void ApplyEraserMode()
        {
            if (MainInkCanvas == null || ToolEraser.ContextMenu == null) return;

            // 遍历菜单，找到那个被打钩 (IsChecked) 的项
            foreach (MenuItem item in ToolEraser.ContextMenu.Items.OfType<MenuItem>())
            {
                if (item.IsChecked && item.Tag != null)
                {
                    string mode = item.Tag.ToString()!;

                    if (mode == "Stroke")
                    {
                        // 模式 A: 笔划擦除 (碰到即删)
                        MainInkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
                    }
                    else if (mode == "Point")
                    {
                        // 模式 B: 点擦除 (标准橡皮)
                        MainInkCanvas.EditingMode = InkCanvasEditingMode.EraseByPoint;

                        // 设置橡皮擦的大小 (你可以根据需要调整这个数值，这里设为 10x10)
                        MainInkCanvas.EraserShape = new RectangleStylusShape(10, 10);
                    }
                    break; // 找到一个就够了
                }
            }
        }

        // 当用户勾选或取消“书法模式”时触发
        private void OnAttributeChanged(object sender, RoutedEventArgs e)
        {
            // 如果当前选中的是笔或荧光笔，立即刷新一下属性
            if (ToolPen.IsChecked == true) UpdateDrawingAttributes("Pen");
            else if (ToolHighlighter.IsChecked == true) UpdateDrawingAttributes("Highlighter");
        }

        // 核心方法：更新画笔属性（颜色、大小、样式）
        private void UpdateDrawingAttributes(string toolType)
        {
            // 防止在组件未完全加载前调用导致空引用
            if (MainInkCanvas == null) return;

            // 创建新的绘图属性对象
            DrawingAttributes attributes = new DrawingAttributes();

            // 通用设置
            attributes.Color = _currentColor;
            attributes.Width = _currentSize;
            attributes.Height = _currentSize;
            attributes.FitToCurve = true; // 开启平滑曲线，让笔迹更圆润

            // --- 书法逻辑开始 ---
            // 只有当工具是 "Pen" 且 "书法模式" 被勾选时才执行
            if (toolType == "Pen" && CheckCalligraphy.IsChecked == true)
            {
                // 【知识点】Matrix (矩阵变换)
                // 想象笔尖原本是一个圆球。
                // 1. Scale(1.0, 0.3): 我们把它压扁，X轴不变，Y轴变成原来的 30%。它变成了扁平的椭圆。
                // 2. Rotate(45): 我们把这个扁平的笔尖旋转 45 度。
                // 这样写出来的线条，横细竖粗，就像钢笔一样。
                Matrix matrix = new Matrix();
                matrix.Rotate(45);
                matrix.Scale(1.0, 0.3);

                attributes.StylusTipTransform = matrix; // 应用变形
                attributes.StylusTip = StylusTip.Ellipse; // 必须是椭圆模式才生效
            }
            // --- 书法逻辑结束 ---

            // 工具特定设置
            if (toolType == "Highlighter")
            {
                // 荧光笔设置
                attributes.IsHighlighter = true; // 关键：开启半透明混合模式
                attributes.StylusTip = StylusTip.Rectangle; // 笔尖设为方形
                attributes.Width = 20; // 荧光笔默认较宽
                attributes.Height = 20;

                // 如果当前选的是黑色，荧光笔会看不见，强制改为黄色
                if (_currentColor == Colors.Black)
                    attributes.Color = Colors.Yellow;
            }
            else if (CheckCalligraphy.IsChecked == false) // 如果没开书法模式
            {
                // 普通钢笔设置
                attributes.IsHighlighter = false; // 不透明
                attributes.StylusTip = StylusTip.Ellipse; // 恢复成普通圆点笔尖
            }

            // 将新属性应用到画布
            MainInkCanvas.DefaultDrawingAttributes = attributes;
        }

        // 处理颜色点击事件
        private void OnColorPicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Background is SolidColorBrush brush)
            {
                _currentColor = brush.Color;

                // 选中颜色后，根据当前是否选中荧光笔来刷新属性
                if (ToolHighlighter.IsChecked == true) UpdateDrawingAttributes("Highlighter");
                else UpdateDrawingAttributes("Pen");
            }
        }

        // 处理滑块拖动事件，实时调整笔触大小
        private void OnSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _currentSize = e.NewValue;

            // 同样需要根据当前工具刷新属性
            if (ToolHighlighter.IsChecked == true) UpdateDrawingAttributes("Highlighter");
            else UpdateDrawingAttributes("Pen");
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
            SaveFileDialog saveDialog = new SaveFileDialog();
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
            OpenFileDialog openDialog = new OpenFileDialog();
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
        private void Window_KeyDown(object sender, KeyEventArgs e)
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
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
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