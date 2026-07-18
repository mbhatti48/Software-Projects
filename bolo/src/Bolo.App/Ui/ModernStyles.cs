using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace Bolo.App.Ui;

/// <summary>
/// Runtime-built control templates that replace WPF's dated default chrome (white
/// dropdowns, stock checkboxes, chunky scrollbars) with the Bolo look: dark, rounded,
/// soft shadows, hover feedback. Parsed from XAML with theme colors injected.
/// </summary>
public static class ModernStyles
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Dark (or light) title bar via DWM — the white caption bar screams 2003.</summary>
    public static void ApplyTitleBar(Window window, bool dark)
    {
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            var value = dark ? 1 : 0;
            DwmSetWindowAttribute(handle, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref value, 4);
        };
    }

    /// <summary>Merges the modern control templates into a window. Degrades gracefully.</summary>
    public static void Apply(Window window, Theme t)
    {
        try
        {
            window.Resources.MergedDictionaries.Add(Build(t));
        }
        catch (Exception ex)
        {
            Bolo.Core.Storage.BoloLog.Write($"modern styles failed to parse: {ex.Message}");
        }
    }

    private static ResourceDictionary Build(Theme t)
    {
        static string Hex(Brush b) => b is SolidColorBrush s
            ? $"#{s.Color.R:X2}{s.Color.G:X2}{s.Color.B:X2}"
            : "#2A2A30";

        var accent = Hex(t.Accent);
        var text = Hex(t.Text);
        var muted = Hex(t.TextMuted);
        var input = Hex(t.InputBg);
        var border = Hex(t.CardBorder);
        var popup = Hex(t.SidebarBg);

        var xaml = Template
            .Replace("@ACCENT@", accent)
            .Replace("@TEXT@", text)
            .Replace("@MUTED@", muted)
            .Replace("@INPUT@", input)
            .Replace("@BORDER@", border)
            .Replace("@POPUP@", popup);

        return (ResourceDictionary)XamlReader.Parse(xaml);
    }

    private const string Template = """
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Buttons: rounded, hover lighten, press dim. Colors come from code. -->
  <Style TargetType="Button">
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Grid>
            <Border x:Name="bg" Background="{TemplateBinding Background}" CornerRadius="8"/>
            <Border x:Name="hover" Background="White" Opacity="0" CornerRadius="8"/>
            <ContentPresenter Margin="{TemplateBinding Padding}"
                              HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                              VerticalAlignment="Center"/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="hover" Property="Opacity" Value="0.08"/>
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Setter TargetName="hover" Property="Opacity" Value="0.16"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- TextBox: rounded, accent border on focus. -->
  <Style TargetType="TextBox">
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TextBox">
          <Border x:Name="bd" Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8">
            <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}"
                          VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="bd" Property="BorderBrush" Value="@ACCENT@"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ComboBox: dark rounded chrome + dark popup (kills the biggest 2000s tell). -->
  <Style TargetType="ComboBoxItem">
    <Setter Property="Foreground" Value="@TEXT@"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ComboBoxItem">
          <Border x:Name="bd" Background="Transparent" CornerRadius="6" Padding="10,7">
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsHighlighted" Value="True">
              <Setter TargetName="bd" Property="Background" Value="@INPUT@"/>
            </Trigger>
            <Trigger Property="IsSelected" Value="True">
              <Setter TargetName="bd" Property="Background" Value="@INPUT@"/>
              <Setter Property="Foreground" Value="@ACCENT@"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="ComboBox">
    <Setter Property="Foreground" Value="@TEXT@"/>
    <Setter Property="Background" Value="@INPUT@"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ComboBox">
          <Grid>
            <ToggleButton x:Name="toggle" Focusable="False" ClickMode="Press"
                          IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}">
              <ToggleButton.Template>
                <ControlTemplate TargetType="ToggleButton">
                  <Grid>
                    <Border x:Name="bg" Background="@INPUT@" BorderBrush="@BORDER@"
                            BorderThickness="1" CornerRadius="8"/>
                    <Path HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,12,0"
                          Data="M 0 0 L 4 4 L 8 0" Stroke="@MUTED@" StrokeThickness="1.6"/>
                  </Grid>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                      <Setter TargetName="bg" Property="BorderBrush" Value="@MUTED@"/>
                    </Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <ContentPresenter Content="{TemplateBinding SelectionBoxItem}"
                              ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                              Margin="12,0,28,0" VerticalAlignment="Center" IsHitTestVisible="False"/>
            <Popup x:Name="PART_Popup" IsOpen="{TemplateBinding IsDropDownOpen}"
                   Placement="Bottom" AllowsTransparency="True" PopupAnimation="Fade">
              <Border Background="@POPUP@" BorderBrush="@BORDER@" BorderThickness="1"
                      CornerRadius="10" Padding="4" Margin="0,4,12,12"
                      MinWidth="{TemplateBinding ActualWidth}"
                      MaxHeight="{TemplateBinding MaxDropDownHeight}">
                <Border.Effect>
                  <DropShadowEffect BlurRadius="18" ShadowDepth="4" Opacity="0.45" Color="#000000"/>
                </Border.Effect>
                <ScrollViewer VerticalScrollBarVisibility="Auto">
                  <ItemsPresenter/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- CheckBox: rounded accent box with a drawn check. -->
  <Style TargetType="CheckBox">
    <Setter Property="Foreground" Value="@TEXT@"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="CheckBox">
          <StackPanel Orientation="Horizontal" Background="Transparent">
            <Border x:Name="box" Width="19" Height="19" CornerRadius="5"
                    Background="Transparent" BorderBrush="@MUTED@" BorderThickness="1.5"
                    VerticalAlignment="Center">
              <Path x:Name="check" Data="M 3.5 9 L 8 13 L 15 5" Stroke="White" StrokeThickness="2.2"
                    StrokeStartLineCap="Round" StrokeEndLineCap="Round" Opacity="0"/>
            </Border>
            <ContentPresenter Margin="8,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True"/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="box" Property="Background" Value="@ACCENT@"/>
              <Setter TargetName="box" Property="BorderBrush" Value="@ACCENT@"/>
              <Setter TargetName="check" Property="Opacity" Value="1"/>
            </Trigger>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="box" Property="BorderBrush" Value="@ACCENT@"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Slider: thin track, round accent thumb. -->
  <Style TargetType="Slider">
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Slider">
          <Grid Height="26">
            <Border Height="5" CornerRadius="2.5" Background="@INPUT@" VerticalAlignment="Center"/>
            <Track x:Name="PART_Track">
              <Track.DecreaseRepeatButton>
                <RepeatButton Command="{x:Static Slider.DecreaseLarge}" Focusable="False">
                  <RepeatButton.Template>
                    <ControlTemplate TargetType="RepeatButton">
                      <Border Height="5" CornerRadius="2.5" Background="@ACCENT@" VerticalAlignment="Center"/>
                    </ControlTemplate>
                  </RepeatButton.Template>
                </RepeatButton>
              </Track.DecreaseRepeatButton>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command="{x:Static Slider.IncreaseLarge}" Focusable="False">
                  <RepeatButton.Template>
                    <ControlTemplate TargetType="RepeatButton">
                      <Border Background="Transparent"/>
                    </ControlTemplate>
                  </RepeatButton.Template>
                </RepeatButton>
              </Track.IncreaseRepeatButton>
              <Track.Thumb>
                <Thumb Width="18" Height="18">
                  <Thumb.Template>
                    <ControlTemplate TargetType="Thumb">
                      <Ellipse Fill="@ACCENT@" Stroke="White" StrokeThickness="1.5">
                        <Ellipse.Effect>
                          <DropShadowEffect BlurRadius="8" ShadowDepth="1" Opacity="0.4" Color="#000000"/>
                        </Ellipse.Effect>
                      </Ellipse>
                    </ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Slim modern scrollbars. -->
  <Style TargetType="ScrollBar">
    <Setter Property="Width" Value="8"/>
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ScrollBar">
          <Grid Background="Transparent">
            <Track x:Name="PART_Track" IsDirectionReversed="True">
              <Track.DecreaseRepeatButton>
                <RepeatButton Command="{x:Static ScrollBar.PageUpCommand}" Focusable="False">
                  <RepeatButton.Template>
                    <ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate>
                  </RepeatButton.Template>
                </RepeatButton>
              </Track.DecreaseRepeatButton>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command="{x:Static ScrollBar.PageDownCommand}" Focusable="False">
                  <RepeatButton.Template>
                    <ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate>
                  </RepeatButton.Template>
                </RepeatButton>
              </Track.IncreaseRepeatButton>
              <Track.Thumb>
                <Thumb>
                  <Thumb.Template>
                    <ControlTemplate TargetType="Thumb">
                      <Border Background="@MUTED@" CornerRadius="4" Opacity="0.45" Margin="1"/>
                    </ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
    <Style.Triggers>
      <Trigger Property="Orientation" Value="Horizontal">
        <Setter Property="Width" Value="Auto"/>
        <Setter Property="Height" Value="8"/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <!-- Dark rounded tooltips. -->
  <Style TargetType="ToolTip">
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ToolTip">
          <Border Background="@POPUP@" BorderBrush="@BORDER@" BorderThickness="1"
                  CornerRadius="8" Padding="10,6">
            <ContentPresenter TextBlock.Foreground="@TEXT@" TextBlock.FontSize="12"/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

</ResourceDictionary>
""";
}
