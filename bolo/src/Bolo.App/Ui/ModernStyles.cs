using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace Bolo.App.Ui;

/// <summary>
/// Runtime-built control templates that replace WPF's dated default chrome with the
/// v1.2 look: near-square 3 to 4 px corners, hairline borders, hover washes instead of
/// bevels, an accent focus ring, a switch style for on/off settings, tick sliders and
/// slim scrollbars. Parsed from XAML with theme colours injected. Also the DWM calls
/// that make the title bar part of the design (dark mode, caption and border colours,
/// rounded corners) instead of a white strip from 2003.
/// </summary>
public static class ModernStyles
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    /// <summary>
    /// Title bar in the theme's chrome colour with the theme's ink, a hairline border and
    /// rounded corners (Windows 11; each call is ignored where unsupported, so Windows 10
    /// keeps a plain dark or light caption).
    /// </summary>
    public static void ApplyTitleBar(Window window, Theme t)
    {
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            var dark = t.IsDark ? 1 : 0;
            DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref dark, 4);
            var caption = ColorRef(t.ColorOf(t.SidebarBg));
            DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref caption, 4);
            var text = ColorRef(t.ColorOf(t.Text));
            DwmSetWindowAttribute(handle, DwmwaTextColor, ref text, 4);
            var border = ColorRef(t.ColorOf(t.CardBorder));
            DwmSetWindowAttribute(handle, DwmwaBorderColor, ref border, 4);
            var round = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref round, 4);
        };
    }

    /// <summary>COLORREF is 0x00BBGGRR.</summary>
    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    /// <summary>Merges the control templates into a window. Degrades gracefully.</summary>
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
        string Hex(Brush b) => b is SolidColorBrush s
            ? $"#{s.Color.A:X2}{s.Color.R:X2}{s.Color.G:X2}{s.Color.B:X2}"
            : "#FF2A2A30";

        var xaml = Template
            .Replace("@ACCENT@", Hex(t.Accent))
            .Replace("@ACCENTINK@", Hex(t.AccentInk))
            .Replace("@ACCENTSOFT@", Hex(t.AccentSoft))
            .Replace("@ONACCENT@", Hex(t.OnAccent))
            .Replace("@TEXT@", Hex(t.Text))
            .Replace("@MUTED@", Hex(t.TextMuted))
            .Replace("@FAINT@", Hex(t.TextFaint))
            .Replace("@INPUT@", Hex(t.InputBg))
            .Replace("@RAISE@", Hex(t.RaiseBg))
            .Replace("@BORDER@", Hex(t.CardBorder))
            .Replace("@LINE2@", Hex(t.Line2))
            .Replace("@POPUP@", Hex(t.CardBg))
            .Replace("@WASH@", t.IsDark ? "White" : "Black");

        return (ResourceDictionary)XamlReader.Parse(xaml);
    }

    private const string Template = """
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Focus ring shared by inputs and buttons: a 1 px accent line, no dotted rectangle. -->
  <Style x:Key="FocusRing">
    <Setter Property="Control.Template">
      <Setter.Value>
        <ControlTemplate>
          <Border BorderBrush="@ACCENT@" BorderThickness="1.5" CornerRadius="5" Margin="-2"/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Buttons: a flat tile with a hairline, a light wash on hover, a stronger one on press. -->
  <Style TargetType="Button">
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="BorderBrush" Value="@LINE2@"/>
    <Setter Property="FocusVisualStyle" Value="{StaticResource FocusRing}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Grid>
            <Border x:Name="bg" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="4"/>
            <Border x:Name="hover" Background="@WASH@" Opacity="0" CornerRadius="4"/>
            <ContentPresenter Margin="{TemplateBinding Padding}"
                              HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                              VerticalAlignment="Center" RecognizesAccessKey="True"/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="hover" Property="Opacity" Value="0.06"/>
            </Trigger>
            <Trigger Property="IsPressed" Value="True">
              <Setter TargetName="hover" Property="Opacity" Value="0.12"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.4"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Pill buttons (the record button): same behaviour, fully rounded. -->
  <Style x:Key="Pill" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Button">
          <Grid>
            <Border x:Name="bg" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="999"/>
            <Border x:Name="hover" Background="@WASH@" Opacity="0" CornerRadius="999"/>
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="Center" VerticalAlignment="Center"/>
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

  <!-- TextBox: hairline, accent border on focus. -->
  <Style TargetType="TextBox">
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="SelectionBrush" Value="@ACCENTSOFT@"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TextBox">
          <Border x:Name="bd" Background="{TemplateBinding Background}"
                  BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="3">
            <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}"
                          VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="bd" Property="BorderBrush" Value="@MUTED@"/>
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="bd" Property="BorderBrush" Value="@ACCENT@"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ComboBox: a hairline tile with a thin chevron, a panel-coloured popup. -->
  <Style TargetType="ComboBoxItem">
    <Setter Property="Foreground" Value="@TEXT@"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ComboBoxItem">
          <Border x:Name="bd" Background="Transparent" CornerRadius="3" Padding="10,7">
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsHighlighted" Value="True">
              <Setter TargetName="bd" Property="Background" Value="@RAISE@"/>
            </Trigger>
            <Trigger Property="IsSelected" Value="True">
              <Setter TargetName="bd" Property="Background" Value="@ACCENTSOFT@"/>
              <Setter Property="Foreground" Value="@ACCENTINK@"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="ComboBox">
    <Setter Property="Foreground" Value="@TEXT@"/>
    <Setter Property="Background" Value="@INPUT@"/>
    <Setter Property="FocusVisualStyle" Value="{StaticResource FocusRing}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ComboBox">
          <Grid>
            <ToggleButton x:Name="toggle" Focusable="False" ClickMode="Press"
                          IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}">
              <ToggleButton.Template>
                <ControlTemplate TargetType="ToggleButton">
                  <Grid>
                    <Border x:Name="bg" Background="@INPUT@" BorderBrush="@LINE2@"
                            BorderThickness="1" CornerRadius="3"/>
                    <Path HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,12,0"
                          Data="M 0 0 L 4.5 4.5 L 9 0" Stroke="@ACCENT@" StrokeThickness="1.5"/>
                  </Grid>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                      <Setter TargetName="bg" Property="BorderBrush" Value="@MUTED@"/>
                    </Trigger>
                    <Trigger Property="IsChecked" Value="True">
                      <Setter TargetName="bg" Property="BorderBrush" Value="@ACCENT@"/>
                    </Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <ContentPresenter Content="{TemplateBinding SelectionBoxItem}"
                              ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                              Margin="12,0,30,0" VerticalAlignment="Center" IsHitTestVisible="False"/>
            <Popup x:Name="PART_Popup" IsOpen="{TemplateBinding IsDropDownOpen}"
                   Placement="Bottom" AllowsTransparency="True" PopupAnimation="Fade">
              <Border Background="@POPUP@" BorderBrush="@LINE2@" BorderThickness="1"
                      CornerRadius="4" Padding="4" Margin="0,4,14,14"
                      MinWidth="{TemplateBinding ActualWidth}"
                      MaxHeight="{TemplateBinding MaxDropDownHeight}">
                <Border.Effect>
                  <DropShadowEffect BlurRadius="24" ShadowDepth="6" Opacity="0.45" Color="#000000"/>
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

  <!-- CheckBox: a small square with a drawn tick. -->
  <Style TargetType="CheckBox">
    <Setter Property="Foreground" Value="@TEXT@"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="CheckBox">
          <StackPanel Orientation="Horizontal" Background="Transparent">
            <Border x:Name="box" Width="18" Height="18" CornerRadius="3"
                    Background="@INPUT@" BorderBrush="@LINE2@" BorderThickness="1"
                    VerticalAlignment="Center">
              <Path x:Name="check" Data="M 4 9 L 7.5 12.5 L 14 5.5" Stroke="@ONACCENT@" StrokeThickness="2"
                    StrokeStartLineCap="Round" StrokeEndLineCap="Round" Opacity="0"/>
            </Border>
            <ContentPresenter Margin="9,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True"/>
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

  <!-- Switch: a CheckBox drawn as a toggle (Style page, on/off settings). -->
  <Style x:Key="Switch" TargetType="CheckBox">
    <Setter Property="Foreground" Value="@TEXT@"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="CheckBox">
          <StackPanel Orientation="Horizontal" Background="Transparent">
            <Border x:Name="track" Width="38" Height="22" CornerRadius="11" Background="@LINE2@" VerticalAlignment="Center">
              <Ellipse x:Name="knob" Width="16" Height="16" Fill="White" HorizontalAlignment="Left" Margin="3,0,0,0">
                <Ellipse.RenderTransform>
                  <TranslateTransform x:Name="slide" X="0"/>
                </Ellipse.RenderTransform>
                <Ellipse.Effect>
                  <DropShadowEffect BlurRadius="4" ShadowDepth="1" Opacity="0.35" Color="#000000"/>
                </Ellipse.Effect>
              </Ellipse>
            </Border>
            <ContentPresenter Margin="10,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True"/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="track" Property="Background" Value="@ACCENT@"/>
              <Trigger.EnterActions>
                <BeginStoryboard>
                  <Storyboard>
                    <DoubleAnimation Storyboard.TargetName="slide" Storyboard.TargetProperty="X" To="16" Duration="0:0:0.167">
                      <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                  </Storyboard>
                </BeginStoryboard>
              </Trigger.EnterActions>
              <Trigger.ExitActions>
                <BeginStoryboard>
                  <Storyboard>
                    <DoubleAnimation Storyboard.TargetName="slide" Storyboard.TargetProperty="X" To="0" Duration="0:0:0.167">
                      <DoubleAnimation.EasingFunction><CubicEase EasingMode="EaseOut"/></DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                  </Storyboard>
                </BeginStoryboard>
              </Trigger.ExitActions>
            </Trigger>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="track" Property="Opacity" Value="0.85"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Slider: thin track, accent fill, round thumb, ticks under the track. -->
  <Style TargetType="Slider">
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Slider">
          <Grid Height="34">
            <Grid.RowDefinitions>
              <RowDefinition Height="24"/>
              <RowDefinition Height="10"/>
            </Grid.RowDefinitions>
            <Border Grid.Row="0" Height="4" CornerRadius="2" Background="@LINE2@" VerticalAlignment="Center"/>
            <Track Grid.Row="0" x:Name="PART_Track">
              <Track.DecreaseRepeatButton>
                <RepeatButton Command="{x:Static Slider.DecreaseLarge}" Focusable="False">
                  <RepeatButton.Template>
                    <ControlTemplate TargetType="RepeatButton">
                      <Border Height="4" CornerRadius="2" Background="@ACCENT@" VerticalAlignment="Center"/>
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
                <Thumb Width="16" Height="16" Focusable="False">
                  <Thumb.Template>
                    <ControlTemplate TargetType="Thumb">
                      <Ellipse Fill="@ACCENT@" Stroke="@POPUP@" StrokeThickness="2"/>
                    </ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
            </Track>
            <TickBar Grid.Row="1" Placement="Bottom" Fill="@FAINT@" Height="5" Margin="8,0"
                     Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}"
                     TickFrequency="{TemplateBinding TickFrequency}" Ticks="{TemplateBinding Ticks}"/>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Slim scrollbars. -->
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
                      <Border x:Name="th" Background="@LINE2@" CornerRadius="3" Margin="1"/>
                      <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                          <Setter TargetName="th" Property="Background" Value="@MUTED@"/>
                        </Trigger>
                      </ControlTemplate.Triggers>
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

  <!-- Tooltips on the panel colour. -->
  <Style TargetType="ToolTip">
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ToolTip">
          <Border Background="@POPUP@" BorderBrush="@LINE2@" BorderThickness="1"
                  CornerRadius="4" Padding="10,6">
            <Border.Effect>
              <DropShadowEffect BlurRadius="16" ShadowDepth="4" Opacity="0.4" Color="#000000"/>
            </Border.Effect>
            <ContentPresenter TextBlock.Foreground="@TEXT@" TextBlock.FontSize="12"/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

</ResourceDictionary>
""";
}
