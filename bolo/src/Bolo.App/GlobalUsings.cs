// UseWPF + UseWindowsForms (needed for NotifyIcon) makes several type names ambiguous.
// Pin the WPF/Win32 variants project-wide.
global using Application = System.Windows.Application;
global using Brush = System.Windows.Media.Brush;
global using Brushes = System.Windows.Media.Brushes;
global using Color = System.Windows.Media.Color;
global using Orientation = System.Windows.Controls.Orientation;
global using TextBox = System.Windows.Controls.TextBox;
global using Button = System.Windows.Controls.Button;
global using UserControl = System.Windows.Controls.UserControl;
global using ComboBox = System.Windows.Controls.ComboBox;
global using CheckBox = System.Windows.Controls.CheckBox;
global using UniformGrid = System.Windows.Controls.Primitives.UniformGrid;
global using Rectangle = System.Windows.Shapes.Rectangle;
global using Point = System.Windows.Point;
global using Cursors = System.Windows.Input.Cursors;
global using Clipboard = System.Windows.Clipboard;
global using MessageBox = System.Windows.MessageBox;
global using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
