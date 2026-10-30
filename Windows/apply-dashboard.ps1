param(
    [Parameter(Mandatory = $true)]
    [string]$SourceRoot
)

$ErrorActionPreference = "Stop"
$xamlPath = Join-Path $SourceRoot "v2rayN\v2rayN\Views\MainWindow.xaml"
if (-not (Test-Path $xamlPath)) { throw "Upstream MainWindow.xaml not found: $xamlPath" }

[xml]$document = [System.IO.File]::ReadAllText($xamlPath)
$dialogHost = $document.SelectSingleNode("//*[local-name()='DialogHost']")
if ($null -eq $dialogHost) { throw "Could not locate MaterialDesign DialogHost in upstream MainWindow.xaml." }

$rootGrid = $dialogHost.SelectSingleNode("./*[local-name()='Grid']")
if ($null -eq $rootGrid) { throw "Could not locate the main Grid inside DialogHost." }

$legacyContent = $rootGrid.SelectSingleNode("./*[local-name()='DockPanel']")
if ($null -eq $legacyContent) { throw "Could not locate the original v2rayN content to preserve." }

if ($rootGrid.SelectSingleNode("./*[local-name()='Grid' and @*[local-name()='Name']='vwDashboard']")) {
    Write-Host "V2rayWatch dashboard already exists; skipping."
    exit 0
}

$legacyContent.SetAttribute("Visibility", "Collapsed")

$dashboardMarkup = @'
<Grid x:Name="vwDashboard"
      DataContext="{Binding BindingRoot, RelativeSource={RelativeSource AncestorType={x:Type Window}}}"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:resx="clr-namespace:ServiceLib.Resx;assembly=ServiceLib"
      Background="#081426" FlowDirection="RightToLeft">
  <Grid.Resources>
    <SolidColorBrush x:Key="VW.Panel" Color="#101F38"/>
    <SolidColorBrush x:Key="VW.Panel2" Color="#142746"/>
    <SolidColorBrush x:Key="VW.Line" Color="#263F60"/>
    <SolidColorBrush x:Key="VW.Muted" Color="#91A9CB"/>
    <SolidColorBrush x:Key="VW.Cyan" Color="#08C7D8"/>
    <SolidColorBrush x:Key="VW.Green" Color="#16D99A"/>
    <Style x:Key="VW.Card" TargetType="Border">
      <Setter Property="Background" Value="{StaticResource VW.Panel}"/>
      <Setter Property="BorderBrush" Value="{StaticResource VW.Line}"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="CornerRadius" Value="15"/>
      <Setter Property="Padding" Value="16"/>
    </Style>
    <Style x:Key="VW.Button" TargetType="Button">
      <Setter Property="Foreground" Value="#F3F7FF"/>
      <Setter Property="Background" Value="#1A3457"/>
      <Setter Property="BorderBrush" Value="#315277"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="Padding" Value="13,9"/>
      <Setter Property="Margin" Value="3"/>
      <Setter Property="FontSize" Value="13"/>
      <Setter Property="Cursor" Value="Hand"/>
    </Style>
    <Style x:Key="VW.NavButton" TargetType="Button" BasedOn="{StaticResource VW.Button}">
      <Setter Property="HorizontalContentAlignment" Value="Right"/>
      <Setter Property="Background" Value="Transparent"/>
      <Setter Property="BorderBrush" Value="Transparent"/>
      <Setter Property="Padding" Value="15,13"/>
      <Setter Property="Margin" Value="0,3"/>
    </Style>
    <Style x:Key="VW.Label" TargetType="TextBlock">
      <Setter Property="Foreground" Value="{StaticResource VW.Muted}"/>
      <Setter Property="FontSize" Value="12"/>
    </Style>
  </Grid.Resources>

  <Grid.RowDefinitions>
    <RowDefinition Height="66"/>
    <RowDefinition Height="*"/>
    <RowDefinition Height="34"/>
  </Grid.RowDefinitions>

  <Border Grid.Row="0" Background="#0B192D" BorderBrush="#1C3453" BorderThickness="0,0,0,1" Padding="22,0">
    <DockPanel LastChildFill="True">
      <StackPanel Orientation="Horizontal" DockPanel.Dock="Right" VerticalAlignment="Center">
        <TextBlock Text="◈" Foreground="{StaticResource VW.Cyan}" FontSize="30" FontWeight="Bold" Margin="0,0,10,0"/>
        <TextBlock Text="V2rayWatch" Foreground="#F4F8FF" FontSize="21" FontWeight="SemiBold" VerticalAlignment="Center"/>
        <TextBlock Text="  WINDOWS CLIENT" Foreground="{StaticResource VW.Muted}" FontSize="10" VerticalAlignment="Center" Margin="12,5,0,0"/>
      </StackPanel>
      <StackPanel Orientation="Horizontal" DockPanel.Dock="Left" VerticalAlignment="Center">
        <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuReload}" Command="{Binding ReloadCmd}"/>
        <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuAddServerViaClipboard}" Command="{Binding AddServerViaClipboardCmd}"/>
      </StackPanel>
      <TextBlock Text="کلاینت دسکتاپ فارسی / English" Foreground="{StaticResource VW.Muted}" FontSize="11" VerticalAlignment="Center" HorizontalAlignment="Center"/>
    </DockPanel>
  </Border>

  <Grid Grid.Row="1" Margin="14,13">
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="*"/>
      <ColumnDefinition Width="226"/>
    </Grid.ColumnDefinitions>

    <Grid Grid.Column="0" Margin="0,0,12,0">
      <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
      </Grid.RowDefinitions>

      <Grid Grid.Row="0" Margin="0,0,0,12">
        <Grid.ColumnDefinitions>
          <ColumnDefinition Width="1.3*"/>
          <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <Border Grid.Column="0" Style="{StaticResource VW.Card}" Margin="0,0,7,0" MinHeight="196">
          <Grid>
            <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
            <DockPanel>
              <TextBlock Text="{x:Static resx:ResUI.TbConnections}" Foreground="#F4F8FF" FontWeight="SemiBold" FontSize="15"/>
              <TextBlock Text="ENGINE STATUS" Foreground="{StaticResource VW.Muted}" FontSize="9" DockPanel.Dock="Left" VerticalAlignment="Center"/>
            </DockPanel>
            <Grid Grid.Row="1" Margin="0,15">
              <Grid.ColumnDefinitions><ColumnDefinition Width="90"/><ColumnDefinition/></Grid.ColumnDefinitions>
              <Border Width="78" Height="78" CornerRadius="39" Background="#102D42" BorderBrush="{StaticResource VW.Cyan}" BorderThickness="2" VerticalAlignment="Center">
                <TextBlock Text="✓" Foreground="{StaticResource VW.Green}" FontSize="43" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center"/>
              </Border>
              <StackPanel Grid.Column="1" VerticalAlignment="Center" Margin="14,0,0,0">
                <TextBlock Text="{Binding StatusBarViewModel.RunningServerDisplay, TargetNullValue=—}" Foreground="#F4F8FF" FontSize="20" FontWeight="SemiBold" TextTrimming="CharacterEllipsis"/>
                <TextBlock Text="{Binding StatusBarViewModel.RunningInfoDisplay, TargetNullValue=—}" Foreground="{StaticResource VW.Cyan}" FontSize="13" Margin="0,7,0,0"/>
                <TextBlock Text="{Binding StatusBarViewModel.InboundDisplay, TargetNullValue=—}" Foreground="{StaticResource VW.Muted}" FontSize="11" Margin="0,5,0,0"/>
              </StackPanel>
            </Grid>
            <StackPanel Grid.Row="2" Orientation="Horizontal">
              <Button Style="{StaticResource VW.Button}" Background="#087EBA" BorderBrush="{StaticResource VW.Cyan}" Content="{x:Static resx:ResUI.menuSetDefaultServer}" Command="{Binding ProfilesViewModel.SetDefaultServerCmd}"/>
              <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuRealPingServer}" Command="{Binding ProfilesViewModel.FastRealPingCmd}"/>
            </StackPanel>
          </Grid>
        </Border>

        <Grid Grid.Column="1">
          <Grid.RowDefinitions><RowDefinition/><RowDefinition/></Grid.RowDefinitions>
          <Border Grid.Row="0" Style="{StaticResource VW.Card}" Margin="7,0,0,6">
            <StackPanel>
              <TextBlock Text="{x:Static resx:ResUI.TbSettingsDisplayRealTimeSpeed}" Foreground="{StaticResource VW.Muted}" FontSize="12"/>
              <TextBlock Text="{Binding StatusBarViewModel.SpeedProxyDisplay, TargetNullValue=—}" Foreground="#F4F8FF" FontSize="17" FontWeight="SemiBold" Margin="0,10,0,0" TextWrapping="Wrap"/>
              
            </StackPanel>
          </Border>
          <Border Grid.Row="1" Style="{StaticResource VW.Card}" Margin="7,6,0,0">
            <StackPanel>
              <TextBlock Text="{x:Static resx:ResUI.TbSettingsDisplayRealTimeSpeed}" Foreground="{StaticResource VW.Muted}" FontSize="12"/>
              <TextBlock Text="{Binding StatusBarViewModel.SpeedDirectDisplay, TargetNullValue=—}" Foreground="#F4F8FF" FontSize="17" FontWeight="SemiBold" Margin="0,10,0,0" TextWrapping="Wrap"/>
              <Canvas x:Name="vwTrafficCanvas" Height="36" Margin="0,8,0,0" ClipToBounds="True">
                <Polyline x:Name="vwDownloadLine" Stroke="{StaticResource VW.Green}" StrokeThickness="2" Points=""/>
                <Polyline x:Name="vwUploadLine" Stroke="{StaticResource VW.Cyan}" StrokeThickness="2" Points=""/>
              </Canvas>
            </StackPanel>
          </Border>
        </Grid>
      </Grid>

      <Grid Grid.Row="1">
        <Grid.ColumnDefinitions><ColumnDefinition Width="1.55*"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
        <Border Grid.Column="0" Style="{StaticResource VW.Card}" Margin="0,0,7,0">
          <Grid>
            <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
            <DockPanel Margin="0,0,0,12">
              <TextBlock Text="{x:Static resx:ResUI.menuServers}" Foreground="#F4F8FF" FontSize="16" FontWeight="SemiBold"/>
              <TextBlock Text="PROFILES" Foreground="{StaticResource VW.Muted}" FontSize="9" DockPanel.Dock="Left" VerticalAlignment="Center"/>
            </DockPanel>
            <DataGrid x:Name="vwProfilesGrid" Grid.Row="1"
                      ItemsSource="{Binding ProfilesViewModel.ProfileItems}"
                      SelectedItem="{Binding ProfilesViewModel.SelectedProfile, Mode=TwoWay}"
                      AutoGenerateColumns="False" CanUserAddRows="False" IsReadOnly="True"
                      SelectionMode="Single" SelectionUnit="FullRow"
                      Background="#0A1729" Foreground="#EAF2FF"
                      RowBackground="#0D1C31" AlternatingRowBackground="#122541"
                      BorderBrush="{StaticResource VW.Line}" GridLinesVisibility="Horizontal"
                      HeadersVisibility="Column" FontSize="12">
              <DataGrid.Columns>
                <DataGridTextColumn Header="{x:Static resx:ResUI.menuServers}" Binding="{Binding Remarks}" Width="2*"/>
                <DataGridTextColumn Header="{x:Static resx:ResUI.LvServiceType}" Binding="{Binding ConfigType}" Width="*"/>
                <DataGridTextColumn Header="{x:Static resx:ResUI.TbSortingHost}" Binding="{Binding Address}" Width="2*"/>
                <DataGridTextColumn Header="{x:Static resx:ResUI.LvPort}" Binding="{Binding Port}" Width="65"/>
                <DataGridTextColumn Header="{x:Static resx:ResUI.LvTestDelay}" Binding="{Binding DelayVal}" Width="75"/>
                <DataGridTextColumn Header="{x:Static resx:ResUI.LvTestSpeed}" Binding="{Binding SpeedVal}" Width="75"/>
              </DataGrid.Columns>
            </DataGrid>
            <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Left" Margin="0,10,0,0">
              <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuSetDefaultServer}" Command="{Binding ProfilesViewModel.SetDefaultServerCmd}"/>
              <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuMixedTestServer}" Command="{Binding ProfilesViewModel.MixedTestServerCmd}"/>
              <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuAddServerViaClipboard}" Command="{Binding AddServerViaClipboardCmd}"/>
            </StackPanel>
          </Grid>
        </Border>

        <StackPanel Grid.Column="1">
          <Border Style="{StaticResource VW.Card}" Margin="7,0,0,9">
            <StackPanel>
              <TextBlock Text="{x:Static resx:ResUI.menuSystemproxy}" Foreground="#F4F8FF" FontSize="15" FontWeight="SemiBold"/>
              <TextBlock Text="System Proxy" Foreground="{StaticResource VW.Muted}" FontSize="10" Margin="0,3,0,8"/>
              <StackPanel Orientation="Horizontal">
                <Button Style="{StaticResource VW.Button}" Background="#087EBA" Content="{x:Static resx:ResUI.menuSystemProxySet}" Command="{Binding StatusBarViewModel.SystemProxySetCmd}"/>
                <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuSystemProxyClear}" Command="{Binding StatusBarViewModel.SystemProxyClearCmd}"/>
              </StackPanel>
            </StackPanel>
          </Border>

          <Border Style="{StaticResource VW.Card}" Margin="7,0,0,9">
            <StackPanel>
              <TextBlock Text="{x:Static resx:ResUI.TbEnableTunAs}" Foreground="#F4F8FF" FontSize="15" FontWeight="SemiBold"/>
              <TextBlock Text="{x:Static resx:ResUI.NotRunAsAdmin}" Foreground="{StaticResource VW.Muted}" FontSize="10" TextWrapping="Wrap" Margin="0,5,0,8"/>
              <CheckBox Content="{x:Static resx:ResUI.TbEnableTunAs}" IsChecked="{Binding StatusBarViewModel.EnableTun, Mode=TwoWay}" Foreground="{StaticResource VW.Cyan}" FontSize="13"/>
            </StackPanel>
          </Border>

          <Border Style="{StaticResource VW.Card}" Margin="7,0,0,9">
            <StackPanel>
              <TextBlock Text="{x:Static resx:ResUI.menuRouting}" Foreground="#F4F8FF" FontSize="15" FontWeight="SemiBold"/>
              <ComboBox ItemsSource="{Binding StatusBarViewModel.RoutingItems}"
                        SelectedItem="{Binding StatusBarViewModel.SelectedRouting, Mode=TwoWay}"
                        DisplayMemberPath="Remarks" Margin="0,9,0,0" Padding="7"
                        Background="#142746" Foreground="#EAF2FF" BorderBrush="{StaticResource VW.Line}"/>
              <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuRoutingSetting}" Command="{Binding RoutingSettingCmd}" HorizontalAlignment="Stretch" Margin="0,8,0,0"/>
            </StackPanel>
          </Border>

          <Border Style="{StaticResource VW.Card}" Margin="7,0,0,0">
            <StackPanel>
              <TextBlock Text="{x:Static resx:ResUI.menuSubscription}" Foreground="#F4F8FF" FontSize="15" FontWeight="SemiBold"/>
              <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuSubSetting}" Command="{Binding SubSettingCmd}" HorizontalAlignment="Stretch" Margin="0,7,0,0"/>
              <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuSubUpdate}" Command="{Binding SubUpdateCmd}" HorizontalAlignment="Stretch"/>
            </StackPanel>
          </Border>
        </StackPanel>
      </Grid>
    </Grid>

    <Border Grid.Column="1" Background="#0D1B30" BorderBrush="#1D3555" BorderThickness="1" CornerRadius="15" Padding="12">
      <DockPanel LastChildFill="True">
        <StackPanel DockPanel.Dock="Top" Margin="7,8,7,23">
          <TextBlock Text="V2rayWatch" Foreground="#F4F8FF" FontSize="19" FontWeight="Bold"/>
          
        </StackPanel>
        <StackPanel>
          <Border Background="#075E91" CornerRadius="10" Padding="12" Margin="0,0,0,5">
            <TextBlock Text="⌂   داشبورد" Foreground="White" FontSize="14" FontWeight="SemiBold"/>
          </Border>
          <Button Style="{StaticResource VW.NavButton}" Content="{x:Static resx:ResUI.menuServers}" Command="{Binding AddServerViaClipboardCmd}"/>
          <Button Style="{StaticResource VW.NavButton}" Content="{x:Static resx:ResUI.menuSubscription}" Command="{Binding SubSettingCmd}"/>
          <Button Style="{StaticResource VW.NavButton}" Content="{x:Static resx:ResUI.menuRouting}" Command="{Binding RoutingSettingCmd}"/>
          <Button Style="{StaticResource VW.NavButton}" Content="{x:Static resx:ResUI.menuSetting}" Command="{Binding OptionSettingCmd}"/>
          <Button Style="{StaticResource VW.NavButton}" Content="{x:Static resx:ResUI.menuReload}" Command="{Binding ReloadCmd}"/>
        </StackPanel>
        <Border DockPanel.Dock="Bottom" Background="#122844" CornerRadius="10" Padding="11" Margin="0,20,0,0">
          <StackPanel>
            <TextBlock Text="هسته‌های پروکسی" Foreground="{StaticResource VW.Cyan}" FontSize="12" FontWeight="SemiBold"/>
            <TextBlock Text="Xray / sing-box" Foreground="#DDE9FA" FontSize="11" Margin="0,6,0,0"/>
            <TextBlock Text="رابط اختصاصی V2rayWatch" Foreground="{StaticResource VW.Muted}" FontSize="10" Margin="0,4,0,0"/>
          </StackPanel>
        </Border>
      </DockPanel>
    </Border>
  </Grid>

  <Border Grid.Row="2" Background="#0B192D" BorderBrush="#1C3453" BorderThickness="0,1,0,0" Padding="16,0">
    <DockPanel>
      <TextBlock Text="{Binding StatusBarViewModel.RunningInfoDisplay, TargetNullValue=—}" Foreground="{StaticResource VW.Green}" FontSize="10" VerticalAlignment="Center"/>
      <TextBlock Text="{Binding StatusBarViewModel.RunningServerToolTipText, TargetNullValue=V2rayWatch}" Foreground="{StaticResource VW.Muted}" FontSize="10" VerticalAlignment="Center" DockPanel.Dock="Left"/>
    </DockPanel>
  </Border>
</Grid>
'@

[xml]$dashboardDocument = $dashboardMarkup
$dashboardNode = $document.ImportNode($dashboardDocument.DocumentElement, $true)
[void]$rootGrid.AppendChild($dashboardNode)

$settings = New-Object System.Xml.XmlWriterSettings
$settings.Encoding = New-Object System.Text.UTF8Encoding($false)
$settings.Indent = $true
$settings.OmitXmlDeclaration = $true
$writer = [System.Xml.XmlWriter]::Create($xamlPath, $settings)
try { $document.Save($writer) } finally { $writer.Dispose() }


$codePath = Join-Path $SourceRoot "v2rayN\v2rayN\Views\MainWindow.xaml.cs"
if (-not (Test-Path $codePath)) { throw "Upstream MainWindow.xaml.cs not found: $codePath" }
$code = [System.IO.File]::ReadAllText($codePath)
$flowStatement = '        vwDashboard.FlowDirection = global::System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("fa", global::System.StringComparison.OrdinalIgnoreCase) ? global::System.Windows.FlowDirection.RightToLeft : global::System.Windows.FlowDirection.LeftToRight;'
if (-not $code.Contains("vwDashboard.FlowDirection")) {
    $initializeMarker = "        InitializeComponent();"
    if (-not $code.Contains($initializeMarker)) { throw "Could not locate InitializeComponent in upstream MainWindow.xaml.cs." }
    $code = $code.Replace($initializeMarker, $initializeMarker + [Environment]::NewLine + $flowStatement)
    [System.IO.File]::WriteAllText($codePath, $code, [System.Text.UTF8Encoding]::new($false))
}


if (-not $code.Contains("_vwTrafficSubscription")) {
    $fieldMarker = "    private static Config _config;"
    if (-not $code.Contains($fieldMarker)) { throw "Could not locate MainWindow fields for live traffic chart." }
    $fields = @'
    private readonly Queue<(double Up, double Down)> _vwTrafficSamples = new();
    private IDisposable? _vwTrafficSubscription;
'@
    $code = $code.Replace($fieldMarker, $fieldMarker + [Environment]::NewLine + $fields.TrimEnd())

    $initializeMarker = "        InitializeComponent();"
    if (-not $code.Contains($initializeMarker)) { throw "Could not locate InitializeComponent for live traffic chart." }
    $chartInit = @'
        _vwTrafficSubscription = AppEvents.DispatcherStatisticsRequested.AsObservable()
            .Subscribe(update => Dispatcher.BeginInvoke(new Action(() => UpdateVwTrafficChart(update))));
        Closed += (_, _) => _vwTrafficSubscription?.Dispose();
'@
    $code = $code.Replace($initializeMarker, $initializeMarker + [Environment]::NewLine + $chartInit.TrimEnd())

    $methodMarker = "    private void AddHelpMenuItem()"
    $chartMethods = @'
    private void UpdateVwTrafficChart(ServerSpeedItem update)
    {
        if (vwTrafficCanvas.ActualWidth <= 1 || vwTrafficCanvas.ActualHeight <= 1)
        {
            return;
        }

        _vwTrafficSamples.Enqueue((Math.Max(0, update.ProxyUp), Math.Max(0, update.ProxyDown)));
        while (_vwTrafficSamples.Count > 48)
        {
            _vwTrafficSamples.Dequeue();
        }

        var samples = _vwTrafficSamples.ToArray();
        var maxValue = Math.Max(1d, samples.Max(sample => Math.Max(sample.Up, sample.Down)));
        var width = vwTrafficCanvas.ActualWidth;
        var height = vwTrafficCanvas.ActualHeight;
        var downloadPoints = new global::System.Windows.Media.PointCollection();
        var uploadPoints = new global::System.Windows.Media.PointCollection();

        for (var index = 0; index < samples.Length; index++)
        {
            var x = samples.Length == 1 ? width : index * (width - 2) / (samples.Length - 1);
            var downloadY = height - 2 - samples[index].Down / maxValue * (height - 4);
            var uploadY = height - 2 - samples[index].Up / maxValue * (height - 4);
            downloadPoints.Add(new global::System.Windows.Point(x, downloadY));
            uploadPoints.Add(new global::System.Windows.Point(x, uploadY));
        }

        vwDownloadLine.Points = downloadPoints;
        vwUploadLine.Points = uploadPoints;
    }

'@
    if (-not $code.Contains($methodMarker)) { throw "Could not find insertion point for live traffic chart method." }
    $code = $code.Replace($methodMarker, $chartMethods + $methodMarker)
    [System.IO.File]::WriteAllText($codePath, $code, [System.Text.UTF8Encoding]::new($false))
}

Write-Host "V2rayWatch dashboard inserted into the upstream WPF main window."
