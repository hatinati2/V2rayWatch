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
      Background="#F1F5F9" FlowDirection="RightToLeft">
  <Grid.Resources>
    <SolidColorBrush x:Key="VW.Panel" Color="#FFFFFF"/>
    <SolidColorBrush x:Key="VW.Panel2" Color="#E8EEF5"/>
    <SolidColorBrush x:Key="VW.Line" Color="#C8D3DF"/>
    <SolidColorBrush x:Key="VW.Muted" Color="#526174"/>
    <SolidColorBrush x:Key="VW.Cyan" Color="#007C91"/>
    <SolidColorBrush x:Key="VW.Green" Color="#15803D"/>
    <Style x:Key="VW.Card" TargetType="Border">
      <Setter Property="Background" Value="{StaticResource VW.Panel}"/>
      <Setter Property="BorderBrush" Value="{StaticResource VW.Line}"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="CornerRadius" Value="15"/>
      <Setter Property="Padding" Value="16"/>
    </Style>
    <Style x:Key="VW.Button" TargetType="Button">
      <Setter Property="Foreground" Value="#172B4D"/>
      <Setter Property="Background" Value="#E2EAF3"/>
      <Setter Property="BorderBrush" Value="#B8C6D6"/>
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
    <SolidColorBrush x:Key="VW.TextBrush" Color="#172B4D"/>
    <Style x:Key="VW.Label" TargetType="TextBlock">
      <Setter Property="Foreground" Value="{StaticResource VW.Muted}"/>
      <Setter Property="FontSize" Value="13"/>
    </Style>
    <Style x:Key="VW.ComboBoxItem" TargetType="{x:Type ComboBoxItem}">
      <Setter Property="Foreground" Value="{DynamicResource VW.TextBrush}"/>
      <Setter Property="Background" Value="#FFFFFF"/>
      <Setter Property="Padding" Value="9,7"/>
      <Setter Property="FontSize" Value="13"/>
      <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
      <Style.Triggers>
        <Trigger Property="IsHighlighted" Value="True">
          <Setter Property="Background" Value="#DCEAF5"/>
        </Trigger>
        <Trigger Property="IsSelected" Value="True">
          <Setter Property="Background" Value="#CFE3F0"/>
        </Trigger>
        <Trigger Property="IsEnabled" Value="False">
          <Setter Property="Foreground" Value="#8492A4"/>
        </Trigger>
      </Style.Triggers>
    </Style>
    <Style x:Key="VW.DataGridHeader" TargetType="{x:Type DataGridColumnHeader}">
      <Setter Property="Background" Value="#E2EAF2"/>
      <Setter Property="Foreground" Value="#172B4D"/>
      <Setter Property="FontWeight" Value="SemiBold"/>
      <Setter Property="FontSize" Value="13"/>
      <Setter Property="Padding" Value="8,7"/>
      <Setter Property="BorderBrush" Value="#C8D3DF"/>
      <Setter Property="BorderThickness" Value="0,0,0,1"/>
    </Style>
    <Style x:Key="VW.DataGridCell" TargetType="{x:Type DataGridCell}">
      <Setter Property="Foreground" Value="{DynamicResource VW.TextBrush}"/>
      <Setter Property="FontSize" Value="13"/>
      <Setter Property="Padding" Value="7,5"/>
      <Setter Property="BorderBrush" Value="#E2E8F0"/>
      <Setter Property="BorderThickness" Value="0,0,0,1"/>
      <Style.Triggers>
        <Trigger Property="IsSelected" Value="True">
          <Setter Property="Background" Value="#D8EAF5"/>
          <Setter Property="Foreground" Value="#172B4D"/>
        </Trigger>
      </Style.Triggers>
    </Style>
    <Style x:Key="VW.DataGridRow" TargetType="{x:Type DataGridRow}">
      <Setter Property="Foreground" Value="{DynamicResource VW.TextBrush}"/>
      <Setter Property="MinHeight" Value="34"/>
      <Style.Triggers>
        <Trigger Property="IsSelected" Value="True">
          <Setter Property="Background" Value="#D8EAF5"/>
          <Setter Property="Foreground" Value="#172B4D"/>
        </Trigger>
      </Style.Triggers>
    </Style>
  </Grid.Resources>

  <Grid.RowDefinitions>
    <RowDefinition Height="78"/>
    <RowDefinition Height="*"/>
    <RowDefinition Height="34"/>
  </Grid.RowDefinitions>

  <Border Grid.Row="0" Background="#E7EEF5" BorderBrush="#C8D3DF" BorderThickness="0,0,0,1" Padding="22,0">
    <DockPanel LastChildFill="True">
      <StackPanel Orientation="Horizontal" DockPanel.Dock="Right" VerticalAlignment="Center">
        <TextBlock Text="◈" Foreground="{StaticResource VW.Cyan}" FontSize="30" FontWeight="Bold" Margin="0,0,10,0"/>
        <TextBlock Text="V2rayWatch" Foreground="#172B4D" FontSize="21" FontWeight="SemiBold" VerticalAlignment="Center"/>
        <TextBlock Text="  WINDOWS CLIENT" Foreground="{StaticResource VW.Muted}" FontSize="10" VerticalAlignment="Center" Margin="12,5,0,0"/>
      </StackPanel>
      <StackPanel Orientation="Horizontal" DockPanel.Dock="Left" VerticalAlignment="Center">
        <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuReload}" Command="{Binding ReloadCmd}"/>
        <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuAddServerViaClipboard}" Command="{Binding AddServerViaClipboardCmd}"/>
        <TextBlock x:Name="vwMonitorIntervalLabel" Text="Interval (sec)" Foreground="{StaticResource VW.Muted}" FontSize="12" VerticalAlignment="Center" Margin="7,0,3,0"/>
        <TextBox x:Name="vwMonitorIntervalInput" Text="20" Width="88" MaxLength="5" Margin="3,0,5,0" Padding="7,5"
                 ToolTip="Enter any interval from 1 to 99999 seconds / فاصله از ۱ تا ۹۹۹۹۹ ثانیه"
                 Background="#FFFFFF" Foreground="#172B4D" BorderBrush="{StaticResource VW.Line}"
                 VerticalContentAlignment="Center" HorizontalContentAlignment="Center" TextAlignment="Center"
                 FlowDirection="LeftToRight" FontSize="13"
                 PreviewTextInput="VwIntervalInput_PreviewTextInput" DataObject.Pasting="VwIntervalInput_Pasting"/>
        <ComboBox x:Name="vwMonitorFailureCombo" Width="132" Margin="3,0" Padding="7" SelectedIndex="1"
                  ToolTip="Failures before alert / تعداد خطا تا هشدار" Background="#FFFFFF" Foreground="#172B4D"
                  BorderBrush="{StaticResource VW.Line}" ItemContainerStyle="{StaticResource VW.ComboBoxItem}" FontSize="13">
          <ComboBoxItem Content="1 failure" Tag="1"/>
          <ComboBoxItem Content="2 failures" Tag="2"/>
          <ComboBoxItem Content="3 failures" Tag="3"/>
        </ComboBox>
        <Button x:Name="vwMonitorToggleButton" Style="{StaticResource VW.Button}" Content="Start monitor" Click="VwMonitorToggle_Click"/>
        <TextBlock x:Name="vwMonitorStatus" Text="Monitoring stopped" Foreground="{StaticResource VW.Muted}" FontSize="12" VerticalAlignment="Center" Margin="8,0,0,0" TextWrapping="Wrap" MaxWidth="170"/>
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
              <TextBlock Text="{x:Static resx:ResUI.TbConnections}" Foreground="#172B4D" FontWeight="SemiBold" FontSize="15"/>
              <TextBlock x:Name="vwConnectionModeLabel" Text="WINDOWS CONNECTION" Foreground="{StaticResource VW.Muted}" FontSize="9" DockPanel.Dock="Left" VerticalAlignment="Center"/>
            </DockPanel>
            <Grid Grid.Row="1" Margin="0,15">
              <Grid.ColumnDefinitions><ColumnDefinition Width="90"/><ColumnDefinition/></Grid.ColumnDefinitions>
              <Border Width="78" Height="78" CornerRadius="39" Background="#DFF1F3" BorderBrush="{StaticResource VW.Cyan}" BorderThickness="2" VerticalAlignment="Center">
                <TextBlock Text="✓" Foreground="{StaticResource VW.Green}" FontSize="43" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center"/>
              </Border>
              <StackPanel Grid.Column="1" VerticalAlignment="Center" Margin="14,0,0,0">
                <TextBlock Text="{Binding StatusBarViewModel.RunningServerDisplay, TargetNullValue=—}" Foreground="#172B4D" FontSize="20" FontWeight="SemiBold" TextTrimming="CharacterEllipsis"/>
                <TextBlock Text="{Binding StatusBarViewModel.RunningInfoDisplay, TargetNullValue=—}" Foreground="{StaticResource VW.Cyan}" FontSize="13" Margin="0,7,0,0"/>
                <TextBlock Text="{Binding StatusBarViewModel.InboundDisplay, TargetNullValue=—}" Foreground="{StaticResource VW.Muted}" FontSize="11" Margin="0,5,0,0"/>
              </StackPanel>
            </Grid>
            <StackPanel Grid.Row="2" Orientation="Horizontal">
              <Button Style="{StaticResource VW.Button}" Background="#B9DDEE" BorderBrush="{StaticResource VW.Cyan}" Content="{x:Static resx:ResUI.menuSetDefaultServer}" Command="{Binding ProfilesViewModel.SetDefaultServerCmd}"/>
              <Button x:Name="vwManualProbeButton" Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuRealPingServer}" Command="{Binding ProfilesViewModel.FastRealPingCmd}"/>
            </StackPanel>
          </Grid>
        </Border>

        <Grid Grid.Column="1">
          <Grid.RowDefinitions><RowDefinition/><RowDefinition/></Grid.RowDefinitions>
          <Border Grid.Row="0" Style="{StaticResource VW.Card}" Margin="7,0,0,6">
            <StackPanel>
              <TextBlock Text="{x:Static resx:ResUI.TbSettingsDisplayRealTimeSpeed}" Foreground="{StaticResource VW.Muted}" FontSize="12"/>
              <TextBlock Text="{Binding StatusBarViewModel.SpeedProxyDisplay, TargetNullValue=—}" Foreground="#172B4D" FontSize="17" FontWeight="SemiBold" Margin="0,10,0,0" TextWrapping="Wrap"/>
              
            </StackPanel>
          </Border>
          <Border Grid.Row="1" Style="{StaticResource VW.Card}" Margin="7,6,0,0">
            <StackPanel>
              <TextBlock Text="{x:Static resx:ResUI.TbSettingsDisplayRealTimeSpeed}" Foreground="{StaticResource VW.Muted}" FontSize="12"/>
              <TextBlock Text="{Binding StatusBarViewModel.SpeedDirectDisplay, TargetNullValue=—}" Foreground="#172B4D" FontSize="17" FontWeight="SemiBold" Margin="0,10,0,0" TextWrapping="Wrap"/>
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
              <TextBlock Text="{x:Static resx:ResUI.menuServers}" Foreground="#172B4D" FontSize="16" FontWeight="SemiBold"/>
              <TextBlock Text="PROFILES" Foreground="{StaticResource VW.Muted}" FontSize="9" DockPanel.Dock="Left" VerticalAlignment="Center"/>
            </DockPanel>
            <DataGrid x:Name="vwProfilesGrid" Grid.Row="1"
                      ItemsSource="{Binding ProfilesViewModel.ProfileItems}"
                      SelectedItem="{Binding ProfilesViewModel.SelectedProfile, Mode=TwoWay}"
                      AutoGenerateColumns="False" CanUserAddRows="False" IsReadOnly="True"
                      SelectionMode="Single" SelectionUnit="FullRow"
                      Background="#FFFFFF" Foreground="#172B4D"
                      RowBackground="#FFFFFF" AlternatingRowBackground="#F1F5F9"
                      BorderBrush="{StaticResource VW.Line}" GridLinesVisibility="Horizontal"
                      HeadersVisibility="Column" FontSize="13" ColumnHeaderStyle="{StaticResource VW.DataGridHeader}"
                      CellStyle="{StaticResource VW.DataGridCell}" RowStyle="{StaticResource VW.DataGridRow}"
                      RowDetailsVisibilityMode="Visible">
              <DataGrid.Columns>
                <DataGridTextColumn Header="{x:Static resx:ResUI.menuServers}" Binding="{Binding Remarks}" Width="2*"/>
                <DataGridTextColumn Header="{x:Static resx:ResUI.LvServiceType}" Binding="{Binding ConfigType}" Width="*"/>
                <DataGridTextColumn Header="{x:Static resx:ResUI.TbSortingHost}" Binding="{Binding Address}" Width="2*"/>
                <DataGridTextColumn Header="{x:Static resx:ResUI.LvPort}" Binding="{Binding Port}" Width="65"/>
                <DataGridTextColumn Header="{x:Static resx:ResUI.LvTestDelay}" Binding="{Binding DelayVal}" Width="75"/>
                <DataGridTextColumn Header="{x:Static resx:ResUI.LvTestSpeed}" Binding="{Binding SpeedVal}" Width="75"/>
                <DataGridTextColumn Header="Link health" Binding="{Binding VwHealth}" Width="115"/>
                <DataGridTextColumn Header="Checked" Binding="{Binding VwLastChecked}" Width="75"/>
              </DataGrid.Columns>
              <DataGrid.RowDetailsTemplate>
                <DataTemplate>
                  <Grid Margin="9,1,9,5">
                    <Grid.ColumnDefinitions>
                      <ColumnDefinition Width="88"/>
                      <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="۲۰ نتیجهٔ اخیر / Last 20" Foreground="#64748B" FontSize="10" VerticalAlignment="Center"
                               ToolTip="قدیمی‌تر در چپ، جدیدتر در راست / Oldest left, newest right"/>
                    <ItemsControl Grid.Column="1" ItemsSource="{Binding VwHistory}" ToolTip="20 most recent monitoring results">
                      <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate>
                          <UniformGrid Rows="1" Columns="20"/>
                        </ItemsPanelTemplate>
                      </ItemsControl.ItemsPanel>
                      <ItemsControl.ItemTemplate>
                        <DataTemplate>
                          <Border Height="9" Margin="1,2" CornerRadius="2" Background="#CBD5E1">
                            <Border.Style>
                              <Style TargetType="{x:Type Border}">
                                <Setter Property="Background" Value="#CBD5E1"/>
                                <Setter Property="ToolTip" Value="No result recorded"/>
                                <Style.Triggers>
                                  <DataTrigger Binding="{Binding}" Value="G">
                                    <Setter Property="Background" Value="#16A34A"/>
                                    <Setter Property="ToolTip" Value="Connected"/>
                                  </DataTrigger>
                                  <DataTrigger Binding="{Binding}" Value="R">
                                    <Setter Property="Background" Value="#DC2626"/>
                                    <Setter Property="ToolTip" Value="Disconnected"/>
                                  </DataTrigger>
                                  <DataTrigger Binding="{Binding}" Value="N">
                                    <Setter Property="Background" Value="#CBD5E1"/>
                                    <Setter Property="ToolTip" Value="Not tested / not testable"/>
                                  </DataTrigger>
                                </Style.Triggers>
                              </Style>
                            </Border.Style>
                          </Border>
                        </DataTemplate>
                      </ItemsControl.ItemTemplate>
                    </ItemsControl>
                  </Grid>
                </DataTemplate>
              </DataGrid.RowDetailsTemplate>
            </DataGrid>
            <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Left" Margin="0,10,0,0">
              <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuSetDefaultServer}" Command="{Binding ProfilesViewModel.SetDefaultServerCmd}"/>
              <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuMixedTestServer}" Command="{Binding ProfilesViewModel.MixedTestServerCmd}"/>
              <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuAddServerViaClipboard}" Command="{Binding AddServerViaClipboardCmd}"/>
              <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuRemoveServer}" Command="{Binding ProfilesViewModel.RemoveServerCmd}" ToolTip="Delete the selected profile from the local list"/>
            </StackPanel>
          </Grid>
        </Border>

        <StackPanel Grid.Column="1">
          <Border Style="{StaticResource VW.Card}" Margin="7,0,0,9">
            <StackPanel>
              <TextBlock Text="{x:Static resx:ResUI.menuSystemproxy}" Foreground="#172B4D" FontSize="15" FontWeight="SemiBold"/>
              <TextBlock x:Name="vwSystemProxyDescription" Text="Windows apps use the active profile through the local proxy." Foreground="{StaticResource VW.Muted}" FontSize="10" TextWrapping="Wrap" Margin="0,3,0,8"/>
              <StackPanel Orientation="Horizontal">
                <Button Style="{StaticResource VW.Button}" Background="#B9DDEE" Content="{x:Static resx:ResUI.menuSystemProxySet}" Command="{Binding StatusBarViewModel.SystemProxySetCmd}"/>
                <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuSystemProxyClear}" Command="{Binding StatusBarViewModel.SystemProxyClearCmd}"/>
              </StackPanel>
            </StackPanel>
          </Border>

          <Border Style="{StaticResource VW.Card}" Margin="7,0,0,9">
            <StackPanel>
              <TextBlock Text="{x:Static resx:ResUI.TbEnableTunAs}" Foreground="#172B4D" FontSize="15" FontWeight="SemiBold"/>
              <TextBlock Text="{x:Static resx:ResUI.NotRunAsAdmin}" Foreground="{StaticResource VW.Muted}" FontSize="10" TextWrapping="Wrap" Margin="0,5,0,4"/>
              <TextBlock x:Name="vwTunDescription" Text="TUN routes Windows traffic through the active profile; it is not required for link tests." Foreground="{StaticResource VW.Muted}" FontSize="10" TextWrapping="Wrap" Margin="0,0,0,8"/>
              <CheckBox Content="{x:Static resx:ResUI.TbEnableTunAs}" IsChecked="{Binding StatusBarViewModel.EnableTun, Mode=TwoWay}" Foreground="{StaticResource VW.Cyan}" FontSize="13"/>
            </StackPanel>
          </Border>

          <Border Style="{StaticResource VW.Card}" Margin="7,0,0,9">
            <StackPanel>
              <TextBlock Text="{x:Static resx:ResUI.menuRouting}" Foreground="#172B4D" FontSize="15" FontWeight="SemiBold"/>
              <ComboBox ItemsSource="{Binding StatusBarViewModel.RoutingItems}"
                        SelectedItem="{Binding StatusBarViewModel.SelectedRouting, Mode=TwoWay}"
                        DisplayMemberPath="Remarks" Margin="0,9,0,0" Padding="7"
                        Background="#FFFFFF" Foreground="#172B4D" BorderBrush="{StaticResource VW.Line}"
                        ItemContainerStyle="{StaticResource VW.ComboBoxItem}" FontSize="13"/>
              <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuRoutingSetting}" Command="{Binding RoutingSettingCmd}" HorizontalAlignment="Stretch" Margin="0,8,0,0"/>
            </StackPanel>
          </Border>

          <Border Style="{StaticResource VW.Card}" Margin="7,0,0,0">
            <StackPanel>
              <TextBlock Text="{x:Static resx:ResUI.menuSubscription}" Foreground="#172B4D" FontSize="15" FontWeight="SemiBold"/>
              <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuSubSetting}" Command="{Binding SubSettingCmd}" HorizontalAlignment="Stretch" Margin="0,7,0,0"/>
              <Button Style="{StaticResource VW.Button}" Content="{x:Static resx:ResUI.menuSubUpdate}" Command="{Binding SubUpdateCmd}" HorizontalAlignment="Stretch"/>
            </StackPanel>
          </Border>
        </StackPanel>
      </Grid>
    </Grid>

    <Border Grid.Column="1" Background="#FFFFFF" BorderBrush="#C8D3DF" BorderThickness="1" CornerRadius="15" Padding="12">
      <DockPanel LastChildFill="True">
        <StackPanel DockPanel.Dock="Top" Margin="7,8,7,23">
          <TextBlock Text="V2rayWatch" Foreground="#172B4D" FontSize="19" FontWeight="Bold"/>
          
        </StackPanel>
        <ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">
          <StackPanel>
            <StackPanel>
              <Border Background="#DDECF5" CornerRadius="10" Padding="12" Margin="0,0,0,5">
                <TextBlock Text="⌂   داشبورد / Dashboard" Foreground="#172B4D" FontSize="14" FontWeight="SemiBold"/>
              </Border>
              <Button Style="{StaticResource VW.NavButton}" Content="{x:Static resx:ResUI.menuServers}" Command="{Binding AddServerViaClipboardCmd}"/>
              <Button Style="{StaticResource VW.NavButton}" Content="{x:Static resx:ResUI.menuSubscription}" Command="{Binding SubSettingCmd}"/>
              <Button Style="{StaticResource VW.NavButton}" Content="{x:Static resx:ResUI.menuRouting}" Command="{Binding RoutingSettingCmd}"/>
              <Button Style="{StaticResource VW.NavButton}" Content="{x:Static resx:ResUI.menuSetting}" Command="{Binding OptionSettingCmd}"/>
              <Button Style="{StaticResource VW.NavButton}" Content="{x:Static resx:ResUI.menuReload}" Command="{Binding ReloadCmd}"/>
            </StackPanel>
            <Border Background="#FFFFFF" BorderBrush="{StaticResource VW.Line}" BorderThickness="1" CornerRadius="10" Padding="10" Margin="0,13,0,5">
              <StackPanel>
                <TextBlock x:Name="vwAppearanceTitle" Text="ظاهر و خوانایی / Appearance" Foreground="#172B4D" FontSize="14" FontWeight="SemiBold" Margin="0,0,0,8"/>
                <TextBlock x:Name="vwFontFamilyLabel" Text="فونت / Font" Foreground="{StaticResource VW.Muted}" FontSize="12" Margin="0,0,0,4"/>
                <ComboBox x:Name="vwFontFamilyCombo" Padding="6" SelectedIndex="0"
                          Background="#FFFFFF" Foreground="#172B4D" BorderBrush="{StaticResource VW.Line}"
                          ItemContainerStyle="{StaticResource VW.ComboBoxItem}" FontSize="13">
                  <ComboBoxItem Content="Tahoma" Tag="Tahoma"/>
                  <ComboBoxItem Content="Segoe UI" Tag="Segoe UI"/>
                  <ComboBoxItem Content="Arial" Tag="Arial"/>
                  <ComboBoxItem Content="Verdana" Tag="Verdana"/>
                </ComboBox>
                <TextBlock x:Name="vwFontColorLabel" Text="رنگ نوشته / Text color" Foreground="{StaticResource VW.Muted}" FontSize="12" Margin="0,9,0,4"/>
                <ComboBox x:Name="vwFontColorCombo" Padding="6" SelectedIndex="0"
                          Background="#FFFFFF" Foreground="#172B4D" BorderBrush="{StaticResource VW.Line}"
                          ItemContainerStyle="{StaticResource VW.ComboBoxItem}" FontSize="13">
                  <ComboBoxItem Content="سرمه‌ای تیره / Navy" Tag="#172B4D"/>
                  <ComboBoxItem Content="مشکی / Black" Tag="#111827"/>
                  <ComboBoxItem Content="آبی / Blue" Tag="#164E8B"/>
                  <ComboBoxItem Content="سبزآبی / Teal" Tag="#006D77"/>
                  <ComboBoxItem Content="زرشکی / Burgundy" Tag="#7A284B"/>
                </ComboBox>
                <TextBlock x:Name="vwFontSizeLabel" Text="اندازه نوشته / Text size" Foreground="{StaticResource VW.Muted}" FontSize="12" Margin="0,9,0,4"/>
                <ComboBox x:Name="vwFontSizeCombo" Padding="6" SelectedIndex="2"
                          Background="#FFFFFF" Foreground="#172B4D" BorderBrush="{StaticResource VW.Line}"
                          ItemContainerStyle="{StaticResource VW.ComboBoxItem}" FontSize="13">
                  <ComboBoxItem Content="12" Tag="12"/>
                  <ComboBoxItem Content="13" Tag="13"/>
                  <ComboBoxItem Content="14" Tag="14"/>
                  <ComboBoxItem Content="15" Tag="15"/>
                  <ComboBoxItem Content="16" Tag="16"/>
                </ComboBox>
                <Button x:Name="vwApplyAppearanceButton" Style="{StaticResource VW.Button}" Content="اعمال ظاهر / Apply" Click="VwApplyAppearance_Click" HorizontalAlignment="Stretch" Margin="0,10,0,4"/>
                <Button x:Name="vwLanguageToggleButton" Style="{StaticResource VW.Button}" Content="تغییر زبان / Switch language" Click="VwLanguageToggle_Click" HorizontalAlignment="Stretch" Margin="0,2,0,0"/>
                <TextBlock x:Name="vwAppearanceStatus" Text="تنظیمات ظاهر فقط روی این برنامه اعمال می‌شود." Foreground="{StaticResource VW.Muted}" FontSize="11" TextWrapping="Wrap" Margin="0,8,0,0"/>
              </StackPanel>
            </Border>
          </StackPanel>
        </ScrollViewer>
        <Border DockPanel.Dock="Bottom" Background="#E7EEF5" CornerRadius="10" Padding="11" Margin="0,10,0,0">
          <StackPanel>
            <TextBlock Text="هسته‌های پروکسی" Foreground="{StaticResource VW.Cyan}" FontSize="12" FontWeight="SemiBold"/>
            <TextBlock Text="Xray / sing-box" Foreground="#334155" FontSize="11" Margin="0,6,0,0"/>
            <TextBlock Text="رابط اختصاصی V2rayWatch" Foreground="{StaticResource VW.Muted}" FontSize="10" Margin="0,4,0,0"/>
          </StackPanel>
        </Border>
      </DockPanel>
    </Border>
  </Grid>

  <Border Grid.Row="2" Background="#E7EEF5" BorderBrush="#C8D3DF" BorderThickness="0,1,0,0" Padding="16,0">
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
if (-not $code.Contains("vwManualProbeButton.ToolTip")) {
    $initializeMarker = "        InitializeComponent();"
    if (-not $code.Contains($initializeMarker)) { throw "Could not locate InitializeComponent for V2rayWatch mode labels." }
    $uiModeLabels = @'
        var vwIsPersian = global::System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("fa", global::System.StringComparison.OrdinalIgnoreCase);
        vwConnectionModeLabel.Text = vwIsPersian ? "اتصال ویندوز" : "WINDOWS CONNECTION";
        vwManualProbeButton.Content = vwIsPersian ? "تست واقعی همه لینک‌ها" : "Test all links";
        vwManualProbeButton.ToolTip = vwIsPersian
            ? "هر پروفایل مستقل و از مسیر Core موقت خودش با درخواست HTTPS بررسی می‌شود؛ نیازی به فعال‌کردن System Proxy یا TUN نیست."
            : "Each profile is checked independently using its own temporary core and an HTTPS request. System Proxy and TUN do not need to be enabled.";
        vwSystemProxyDescription.Text = vwIsPersian
            ? "ترافیک برنامه‌های ویندوز از پروفایل فعال عبور می‌کند؛ این گزینه برای تست سلامت لینک لازم نیست."
            : "Routes Windows app traffic through the active profile. It is not required for link health checks.";
        vwTunDescription.Text = vwIsPersian
            ? "تونل ترافیک ویندوز از مسیر پروفایل فعال؛ برای تست مستقل لینک‌ها لازم نیست."
            : "Routes Windows traffic through the active profile. It is not required for independent link tests.";
        vwMonitorIntervalLabel.Text = vwIsPersian ? "فاصله (ثانیه)" : "Interval (sec)";
        vwMonitorIntervalInput.ToolTip = vwIsPersian ? "فاصله را از ۱ تا ۹۹۹۹۹ ثانیه وارد کن" : "Enter an interval from 1 to 99999 seconds.";
        vwMonitorToggleButton.Content = vwIsPersian ? "شروع پایش" : "Start monitoring";
        vwMonitorStatus.Text = vwIsPersian ? "پایش متوقف است" : "Monitoring stopped";
        vwMonitorToggleButton.ToolTip = vwIsPersian ? "آزمون دوره‌ای همهٔ لینک‌ها؛ مستقل از System Proxy و TUN" : "Periodically test all links independently of System Proxy and TUN.";
'@
    $code = $code.Replace($initializeMarker, $initializeMarker + [Environment]::NewLine + $uiModeLabels.TrimEnd())
    [System.IO.File]::WriteAllText($codePath, $code, [System.Text.UTF8Encoding]::new($false))
}

if (-not $code.Contains("vwDashboard.FlowDirection")) {
    $initializeMarker = "        InitializeComponent();"
    if (-not $code.Contains($initializeMarker)) { throw "Could not locate InitializeComponent in upstream MainWindow.xaml.cs." }
    $appIdCall = '        try { VwSetCurrentProcessExplicitAppUserModelID("V2rayWatch.Windows.Desktop"); } catch { }'
    $code = $code.Replace($initializeMarker, $appIdCall + [Environment]::NewLine + $initializeMarker + [Environment]::NewLine + $flowStatement)
    [System.IO.File]::WriteAllText($codePath, $code, [System.Text.UTF8Encoding]::new($false))
}


if (-not $code.Contains("_vwTrafficSubscription")) {
    $fieldMarker = "    private static Config _config;"
    if (-not $code.Contains($fieldMarker)) { throw "Could not locate MainWindow fields for live traffic chart." }
    $fields = @'
    private readonly Queue<(double Up, double Down)> _vwTrafficSamples = new();
    private IDisposable? _vwTrafficSubscription;
    private global::System.Threading.CancellationTokenSource? _vwMonitorCts;
    private readonly global::System.Collections.Generic.Dictionary<string, int> _vwMonitorFailures = new();
    private readonly global::System.Collections.Generic.HashSet<string> _vwMonitorDown = new();
    private readonly global::System.Collections.Generic.HashSet<string> _vwMonitorRoundProfileIds = new();
    private readonly global::System.Collections.Generic.Dictionary<string, int> _vwMonitorResults = new();
    private readonly global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.List<string>> _vwMonitorHistory = new();
    private const int VwMonitorHistoryLimit = 20;
    private bool _vwMonitorHistoryLoaded;
    private bool _vwMonitorRoundRunning;
    private int _vwMonitorIntervalSeconds = 20;
    private int _vwMonitorFailureThreshold = 2;
'@
    $code = $code.Replace($fieldMarker, $fieldMarker + [Environment]::NewLine + $fields.TrimEnd())

    $initializeMarker = "        InitializeComponent();"
    if (-not $code.Contains($initializeMarker)) { throw "Could not locate InitializeComponent for live traffic chart." }
    $chartInit = @'
        _vwTrafficSubscription = AppEvents.DispatcherStatisticsRequested.AsObservable()
            .Subscribe(update => Dispatcher.BeginInvoke(new Action(() => UpdateVwTrafficChart(update))));
        Closed += (_, _) => _vwTrafficSubscription?.Dispose();
        Loaded += (_, _) => VwInitializeMonitor();
        Closed += (_, _) => VwStopMonitoring();
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
    $monitorMethods = @'

    private bool VwIsPersian() => global::System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("fa", global::System.StringComparison.OrdinalIgnoreCase);

    private void VwInitializeMonitor()
    {
        if (ViewModel?.ProfilesViewModel is not { } profilesViewModel) return;
        profilesViewModel.VwSpeedTestResultReceived -= VwOnSpeedTestResultReceived;
        profilesViewModel.VwSpeedTestResultReceived += VwOnSpeedTestResultReceived;
        try
        {
            var path = global::System.IO.Path.Combine(global::System.Environment.GetFolderPath(global::System.Environment.SpecialFolder.LocalApplicationData), "V2rayWatch", "monitor-settings.json");
            if (global::System.IO.File.Exists(path))
            {
                var saved = global::System.Text.Json.JsonSerializer.Deserialize<global::System.Collections.Generic.Dictionary<string, int>>(global::System.IO.File.ReadAllText(path));
                if (saved != null)
                {
                    if (saved.TryGetValue("intervalSeconds", out var interval)) _vwMonitorIntervalSeconds = interval;
                    if (saved.TryGetValue("failureThreshold", out var threshold)) _vwMonitorFailureThreshold = threshold;
                }
            }
        }
        catch { }
        _vwMonitorIntervalSeconds = global::System.Math.Clamp(_vwMonitorIntervalSeconds, 1, 99999);
        _vwMonitorFailureThreshold = global::System.Math.Clamp(_vwMonitorFailureThreshold, 1, 3);
        vwMonitorIntervalInput.Text = _vwMonitorIntervalSeconds.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
        VwSelectComboTag(vwMonitorFailureCombo, _vwMonitorFailureThreshold);
        VwLoadMonitorHistory(profilesViewModel.ProfileItems);
    }

    private static void VwSelectComboTag(global::System.Windows.Controls.ComboBox combo, int value)
    {
        foreach (var item in combo.Items.OfType<global::System.Windows.Controls.ComboBoxItem>())
        {
            if (int.TryParse(item.Tag?.ToString(), out var tag) && tag == value)
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private static int VwReadComboTag(global::System.Windows.Controls.ComboBox combo, int fallback)
    {
        return combo.SelectedItem is global::System.Windows.Controls.ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var value) ? value : fallback;
    }

    private static string VwNormalizeDigits(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] >= '\u06F0' && chars[i] <= '\u06F9')
                chars[i] = (char)('0' + chars[i] - '\u06F0');
            else if (chars[i] >= '\u0660' && chars[i] <= '\u0669')
                chars[i] = (char)('0' + chars[i] - '\u0660');
        }
        return new string(chars);
    }

    private void VwIntervalInput_PreviewTextInput(object sender, global::System.Windows.Input.TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsDigit);
    }

    private void VwIntervalInput_Pasting(object sender, global::System.Windows.DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(global::System.Windows.DataFormats.UnicodeText)
            || e.DataObject.GetData(global::System.Windows.DataFormats.UnicodeText) is not string pasted
            || !pasted.All(char.IsDigit))
        {
            e.CancelCommand();
        }
    }

    private bool VwTryReadMonitorInterval(out int seconds)
    {
        var normalized = VwNormalizeDigits(vwMonitorIntervalInput.Text.Trim());
        return int.TryParse(normalized, global::System.Globalization.NumberStyles.None,
            global::System.Globalization.CultureInfo.InvariantCulture, out seconds)
            && seconds >= 1 && seconds <= 99999;
    }

    private void VwSaveMonitorSettings()
    {
        try
        {
            var folder = global::System.IO.Path.Combine(global::System.Environment.GetFolderPath(global::System.Environment.SpecialFolder.LocalApplicationData), "V2rayWatch");
            global::System.IO.Directory.CreateDirectory(folder);
            var path = global::System.IO.Path.Combine(folder, "monitor-settings.json");
            var json = global::System.Text.Json.JsonSerializer.Serialize(new { intervalSeconds = _vwMonitorIntervalSeconds, failureThreshold = _vwMonitorFailureThreshold });
            global::System.IO.File.WriteAllText(path, json);
        }
        catch { }
    }

    private void VwMonitorToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_vwMonitorCts != null)
        {
            VwStopMonitoring();
            return;
        }
        if (ViewModel?.ProfilesViewModel == null) return;
        if (!VwTryReadMonitorInterval(out var selectedInterval))
        {
            vwMonitorIntervalInput.BorderBrush = global::System.Windows.Media.Brushes.IndianRed;
            vwMonitorStatus.Text = VwIsPersian()
                ? "فاصله باید عددی بین ۱ تا ۹۹۹۹۹ ثانیه باشد."
                : "Interval must be a whole number from 1 to 99999 seconds.";
            vwMonitorIntervalInput.Focus();
            vwMonitorIntervalInput.SelectAll();
            return;
        }
        vwMonitorIntervalInput.BorderBrush = (global::System.Windows.Media.Brush)vwDashboard.Resources["VW.Line"];
        _vwMonitorIntervalSeconds = selectedInterval;
        _vwMonitorFailureThreshold = VwReadComboTag(vwMonitorFailureCombo, 2);
        VwSaveMonitorSettings();
        _vwMonitorFailures.Clear();
        _vwMonitorDown.Clear();
        var monitorCts = new global::System.Threading.CancellationTokenSource();
        _vwMonitorCts = monitorCts;
        vwMonitorIntervalInput.IsEnabled = false;
        vwMonitorFailureCombo.IsEnabled = false;
        vwMonitorToggleButton.Content = VwIsPersian() ? "توقف پایش" : "Stop monitoring";
        vwMonitorStatus.Text = VwIsPersian() ? "در حال شروع..." : "Starting...";
        _ = VwMonitorLoopAsync(monitorCts);
    }

    private void VwStopMonitoring()
    {
        var monitorCts = _vwMonitorCts;
        if (monitorCts == null) return;
        _vwMonitorCts = null;
        monitorCts.Cancel();
        VwSetMonitorStopped();
    }

    private void VwSetMonitorStopped()
    {
        vwMonitorIntervalInput.IsEnabled = true;
        vwMonitorFailureCombo.IsEnabled = true;
        vwMonitorToggleButton.Content = VwIsPersian() ? "شروع پایش" : "Start monitoring";
        vwMonitorStatus.Text = VwIsPersian() ? "پایش متوقف است" : "Monitoring stopped";
    }

    private async Task VwMonitorLoopAsync(global::System.Threading.CancellationTokenSource monitorCts)
    {
        var cancellationToken = monitorCts.Token;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var roundTimer = global::System.Diagnostics.Stopwatch.StartNew();
                var profilesViewModel = ViewModel?.ProfilesViewModel;
                if (profilesViewModel == null) break;
                var profileCount = profilesViewModel.ProfileItems.Count;
                if (profileCount == 0)
                {
                    await Dispatcher.InvokeAsync(() => vwMonitorStatus.Text = VwIsPersian() ? "ابتدا لینک وارد کن" : "Import links first");
                    await Task.Delay(global::System.TimeSpan.FromSeconds(3), cancellationToken);
                    continue;
                }
                _vwMonitorResults.Clear();
                _vwMonitorRoundProfileIds.Clear();
                foreach (var item in profilesViewModel.ProfileItems) _vwMonitorRoundProfileIds.Add(item.IndexId);
                _vwMonitorRoundRunning = true;
                await Dispatcher.InvokeAsync(() => vwMonitorStatus.Text = VwIsPersian() ? $"در حال بررسی {profileCount} لینک..." : $"Checking {profileCount} links...");
                try
                {
                    await profilesViewModel.ServerSpeedtest(ESpeedActionType.FastRealping, cancellationToken, true);
                    await Dispatcher.InvokeAsync(VwCompleteMonitorRound, DispatcherPriority.Background);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() => vwMonitorStatus.Text = VwIsPersian() ? "خطا در اجرای دور پایش: " + ex.Message : "Monitor cycle failed: " + ex.Message);
                }
                finally
                {
                    _vwMonitorRoundRunning = false;
                }
                var remaining = global::System.TimeSpan.FromSeconds(_vwMonitorIntervalSeconds) - roundTimer.Elapsed;
                if (remaining > global::System.TimeSpan.Zero)
                    await Task.Delay(remaining, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() => vwMonitorStatus.Text = VwIsPersian() ? "خطای پایش: " + ex.Message : "Monitoring error: " + ex.Message);
        }
        finally
        {
            if (global::System.Object.ReferenceEquals(_vwMonitorCts, monitorCts))
            {
                _vwMonitorCts = null;
                await Dispatcher.InvokeAsync(VwSetMonitorStopped);
            }
            monitorCts.Dispose();
        }
    }

    private void VwOnSpeedTestResultReceived(SpeedTestResult result)
    {
        if (!_vwMonitorRoundRunning || string.IsNullOrWhiteSpace(result.IndexId)) return;
        if (int.TryParse(result.Delay, out var delay))
        {
            _vwMonitorResults[result.IndexId] = delay;
        }
        else if (!string.IsNullOrWhiteSpace(result.Speed))
        {
            // FastRealPing may fall back to a mixed test when a core cannot start;
            // that path reports the failure text in Speed instead of Delay.
            _vwMonitorResults[result.IndexId] = -1;
        }
    }

    private static string VwMonitorHistoryPath()
    {
        var folder = global::System.IO.Path.Combine(global::System.Environment.GetFolderPath(global::System.Environment.SpecialFolder.LocalApplicationData), "V2rayWatch");
        global::System.IO.Directory.CreateDirectory(folder);
        return global::System.IO.Path.Combine(folder, "monitor-history.json");
    }

    private void VwLoadMonitorHistory(global::System.Collections.Generic.IEnumerable<global::ServiceLib.Models.Dto.ProfileItemModel> profiles)
    {
        var profileList = profiles.ToList();
        // The first window Loaded event can arrive before the server list is populated.
        // Do not mark history loaded or prune saved IDs until at least one real profile exists.
        if (profileList.Count == 0) return;

        if (!_vwMonitorHistoryLoaded)
        {
            try
            {
                var path = VwMonitorHistoryPath();
                if (global::System.IO.File.Exists(path))
                {
                    var saved = global::System.Text.Json.JsonSerializer.Deserialize<global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.List<string>>>(global::System.IO.File.ReadAllText(path));
                    if (saved != null)
                    {
                        foreach (var pair in saved)
                        {
                            _vwMonitorHistory[pair.Key] = pair.Value ?? new global::System.Collections.Generic.List<string>();
                        }
                    }
                }
            }
            catch
            {
                _vwMonitorHistory.Clear();
            }
            _vwMonitorHistoryLoaded = true;
        }

        var liveIds = new global::System.Collections.Generic.HashSet<string>();
        foreach (var profile in profileList)
        {
            liveIds.Add(profile.IndexId);
            VwEnsureProfileHistory(profile);
        }

        foreach (var staleId in _vwMonitorHistory.Keys.Where(id => !liveIds.Contains(id)).ToArray())
        {
            _vwMonitorHistory.Remove(staleId);
        }
    }

    private void VwEnsureProfileHistory(global::ServiceLib.Models.Dto.ProfileItemModel profile)
    {
        if (!_vwMonitorHistory.TryGetValue(profile.IndexId, out var history))
        {
            history = new global::System.Collections.Generic.List<string>();
            _vwMonitorHistory[profile.IndexId] = history;
        }

        history.RemoveAll(value => value != "G" && value != "R" && value != "N");
        if (history.Count > VwMonitorHistoryLimit)
            history.RemoveRange(0, history.Count - VwMonitorHistoryLimit);
        while (history.Count < VwMonitorHistoryLimit)
            history.Insert(0, "N");

        profile.VwHistory.Clear();
        foreach (var state in history)
            profile.VwHistory.Add(state);
    }

    private void VwRecordMonitorHistory(global::ServiceLib.Models.Dto.ProfileItemModel profile, string state)
    {
        VwEnsureProfileHistory(profile);
        var history = _vwMonitorHistory[profile.IndexId];
        history.Add(state);
        if (history.Count > VwMonitorHistoryLimit)
            history.RemoveRange(0, history.Count - VwMonitorHistoryLimit);

        profile.VwHistory.Clear();
        foreach (var value in history)
            profile.VwHistory.Add(value);
    }

    private void VwSaveMonitorHistory()
    {
        try
        {
            global::System.IO.File.WriteAllText(
                VwMonitorHistoryPath(),
                global::System.Text.Json.JsonSerializer.Serialize(_vwMonitorHistory));
        }
        catch { }
    }

    private void VwCompleteMonitorRound()
    {
        var profiles = ViewModel?.ProfilesViewModel?.ProfileItems;
        if (profiles == null) return;
        VwLoadMonitorHistory(profiles);
        var tested = 0;
        var down = 0;
        foreach (var profile in profiles)
        {
            if (!_vwMonitorRoundProfileIds.Contains(profile.IndexId)) continue;
            if (!_vwMonitorResults.TryGetValue(profile.IndexId, out var delay))
            {
                if (profile.ConfigType == EConfigType.Custom || (!profile.ConfigType.IsComplexType() && profile.Port <= 0))
                {
                    profile.VwHealth = VwIsPersian() ? "پشتیبانی نمی‌شود" : "Not testable";
                    VwRecordMonitorHistory(profile, "N");
                    continue;
                }
                // A valid profile with no final result indicates a failed core startup/probe.
                delay = -1;
            }
            tested++;
            profile.VwLastChecked = global::System.DateTime.Now.ToString("HH:mm:ss");
            if (delay > 0)
            {
                VwRecordMonitorHistory(profile, "G");
                profile.VwHealth = VwIsPersian() ? $"متصل · {delay} ms" : $"Connected · {delay} ms";
                _vwMonitorFailures[profile.IndexId] = 0;
                if (_vwMonitorDown.Remove(profile.IndexId)) VwShowMonitorNotification(profile.Remarks, false, delay);
            }
            else
            {
                VwRecordMonitorHistory(profile, "R");
                var failures = _vwMonitorFailures.GetValueOrDefault(profile.IndexId) + 1;
                _vwMonitorFailures[profile.IndexId] = failures;
                var isDown = failures >= _vwMonitorFailureThreshold;
                profile.VwHealth = isDown
                    ? (VwIsPersian() ? "قطع" : "Disconnected")
                    : (VwIsPersian() ? $"ناپایدار · {failures}/{_vwMonitorFailureThreshold}" : $"Unstable · {failures}/{_vwMonitorFailureThreshold}");
                if (isDown)
                {
                    down++;
                    if (_vwMonitorDown.Add(profile.IndexId)) VwShowMonitorNotification(profile.Remarks, true, -1);
                }
            }
        }
        VwSaveMonitorHistory();
        vwMonitorStatus.Text = VwIsPersian()
            ? $"آخرین دور: {tested} لینک تست شد، {down} لینک قطع"
            : $"Last round: {tested} tested, {down} down";
    }

    private void VwShowMonitorNotification(string profileName, bool disconnected, int delay)
    {
        var title = disconnected
            ? (VwIsPersian() ? "لینک قطع شد" : "Proxy link disconnected")
            : (VwIsPersian() ? "اتصال بازیابی شد" : "Proxy link recovered");
        var message = disconnected
            ? (VwIsPersian() ? $"{profileName}: پس از {_vwMonitorFailureThreshold} خطای متوالی اتصال برقرار نشد." : $"{profileName}: unavailable after {_vwMonitorFailureThreshold} consecutive failures.")
            : (VwIsPersian() ? $"{profileName}: اتصال برقرار شد؛ تأخیر {delay} ms." : $"{profileName}: connection restored; delay {delay} ms.");
        try
        {
            var tray = VwFindVisualChild<global::H.NotifyIcon.TaskbarIcon>(this);
            tray?.ShowNotification(title, message, disconnected ? global::H.NotifyIcon.Core.NotificationIcon.Error : global::H.NotifyIcon.Core.NotificationIcon.Info, sound: true);
        }
        catch { }
    }

    private static T? VwFindVisualChild<T>(global::System.Windows.DependencyObject parent) where T : global::System.Windows.DependencyObject
    {
        for (var i = 0; i < global::System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = global::System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var nested = VwFindVisualChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }


'@
    $chartMethods += $monitorMethods

    if (-not $code.Contains($methodMarker)) { throw "Could not find insertion point for live traffic chart method." }
    $code = $code.Replace($methodMarker, $chartMethods + $methodMarker)
    [System.IO.File]::WriteAllText($codePath, $code, [System.Text.UTF8Encoding]::new($false))
}

# Add persisted appearance controls and a working language preference toggle.
$code = [System.IO.File]::ReadAllText($codePath)
if (-not $code.Contains("VwInitializeAppearance")) {
    $fieldMarker = "    private static Config _config;"
    if (-not $code.Contains($fieldMarker)) { throw "Could not locate MainWindow fields for appearance settings." }
    $appearanceFields = @'
    private string _vwFontFamily = "Tahoma";
    private string _vwFontColor = "#172B4D";
    private int _vwFontSize = 14;
'@
    $code = $code.Replace($fieldMarker, $fieldMarker + [Environment]::NewLine + $appearanceFields.TrimEnd())

    $initializeMonitorLine = "        Loaded += (_, _) => VwInitializeMonitor();"
    if (-not $code.Contains($initializeMonitorLine)) { throw "Could not locate monitor Loaded hook for appearance initialization." }
    $code = $code.Replace($initializeMonitorLine, $initializeMonitorLine + [Environment]::NewLine + "        Loaded += (_, _) => VwInitializeAppearance();")

    $appearanceMethods = @'

    [global::System.Runtime.InteropServices.DllImport("shell32.dll", EntryPoint = "SetCurrentProcessExplicitAppUserModelID", CharSet = global::System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int VwSetCurrentProcessExplicitAppUserModelID(string appID);

    private static string VwAppearanceSettingsPath()
    {
        var folder = global::System.IO.Path.Combine(global::System.Environment.GetFolderPath(global::System.Environment.SpecialFolder.LocalApplicationData), "V2rayWatch");
        global::System.IO.Directory.CreateDirectory(folder);
        return global::System.IO.Path.Combine(folder, "appearance-settings.json");
    }

    private void VwInitializeAppearance()
    {
        try
        {
            var path = VwAppearanceSettingsPath();
            if (global::System.IO.File.Exists(path))
            {
                var saved = global::System.Text.Json.JsonSerializer.Deserialize<global::System.Collections.Generic.Dictionary<string, string>>(global::System.IO.File.ReadAllText(path));
                if (saved != null)
                {
                    if (saved.TryGetValue("fontFamily", out var family) && !string.IsNullOrWhiteSpace(family)) _vwFontFamily = family;
                    if (saved.TryGetValue("fontColor", out var color) && !string.IsNullOrWhiteSpace(color)) _vwFontColor = color;
                    if (saved.TryGetValue("fontSize", out var sizeText) && int.TryParse(sizeText, out var size)) _vwFontSize = global::System.Math.Clamp(size, 12, 16);
                }
            }
        }
        catch { }

        VwSelectComboStringTag(vwFontFamilyCombo, _vwFontFamily);
        VwSelectComboStringTag(vwFontColorCombo, _vwFontColor);
        VwSelectComboTag(vwFontSizeCombo, _vwFontSize);
        VwUpdateDashboardLanguage();
        VwApplyAppearance();
        vwAppearanceStatus.Text = VwIsPersian() ? "تنظیمات ظاهر ذخیره می‌شود و فقط روی همین برنامه اثر دارد." : "Appearance settings are saved for this app only.";
    }

    private static void VwSelectComboStringTag(global::System.Windows.Controls.ComboBox combo, string value)
    {
        foreach (var item in combo.Items.OfType<global::System.Windows.Controls.ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), value, global::System.StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private static string VwReadComboStringTag(global::System.Windows.Controls.ComboBox combo, string fallback)
    {
        return combo.SelectedItem is global::System.Windows.Controls.ComboBoxItem item && !string.IsNullOrWhiteSpace(item.Tag?.ToString())
            ? item.Tag.ToString()!
            : fallback;
    }

    private void VwSaveAppearance()
    {
        try
        {
            var values = new global::System.Collections.Generic.Dictionary<string, string>
            {
                ["fontFamily"] = _vwFontFamily,
                ["fontColor"] = _vwFontColor,
                ["fontSize"] = _vwFontSize.ToString(global::System.Globalization.CultureInfo.InvariantCulture)
            };
            global::System.IO.File.WriteAllText(VwAppearanceSettingsPath(), global::System.Text.Json.JsonSerializer.Serialize(values));
        }
        catch { }
    }

    private void VwApplyAppearance_Click(object sender, RoutedEventArgs e)
    {
        _vwFontFamily = VwReadComboStringTag(vwFontFamilyCombo, "Tahoma");
        _vwFontColor = VwReadComboStringTag(vwFontColorCombo, "#172B4D");
        _vwFontSize = global::System.Math.Clamp(VwReadComboTag(vwFontSizeCombo, 14), 12, 16);
        VwSaveAppearance();
        VwApplyAppearance();
        vwAppearanceStatus.Text = VwIsPersian() ? "فونت و رنگ نوشته اعمال و ذخیره شد." : "Font and text color applied and saved.";
    }

    private void VwApplyAppearance()
    {
        global::System.Windows.Media.FontFamily family;
        try { family = new global::System.Windows.Media.FontFamily(_vwFontFamily); }
        catch { family = new global::System.Windows.Media.FontFamily("Tahoma"); }

        var converted = global::System.Windows.Media.ColorConverter.ConvertFromString(_vwFontColor);
        var color = converted is global::System.Windows.Media.Color parsed ? parsed : global::System.Windows.Media.Color.FromRgb(23, 43, 77);
        var brush = new global::System.Windows.Media.SolidColorBrush(color);
        brush.Freeze();
        vwDashboard.Resources["VW.TextBrush"] = brush;
        VwApplyAppearanceToTree(vwDashboard, family, brush, _vwFontSize);
        vwProfilesGrid.FontFamily = family;
        vwProfilesGrid.FontSize = _vwFontSize;
        vwProfilesGrid.Foreground = brush;
    }

    private static void VwApplyAppearanceToTree(global::System.Windows.DependencyObject root, global::System.Windows.Media.FontFamily family, global::System.Windows.Media.Brush brush, int fontSize)
    {
        if (root is global::System.Windows.Controls.TextBlock textBlock)
        {
            textBlock.FontFamily = family;
            textBlock.Foreground = brush;
            if (textBlock.FontSize < fontSize) textBlock.FontSize = fontSize;
        }
        else if (root is global::System.Windows.Controls.Control control)
        {
            control.FontFamily = family;
            control.Foreground = brush;
            if (control.FontSize < fontSize) control.FontSize = fontSize;
        }

        var childCount = global::System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            VwApplyAppearanceToTree(global::System.Windows.Media.VisualTreeHelper.GetChild(root, index), family, brush, fontSize);
        }
    }

    private void VwUpdateDashboardLanguage()
    {
        var persian = VwIsPersian();
        vwDashboard.FlowDirection = persian ? global::System.Windows.FlowDirection.RightToLeft : global::System.Windows.FlowDirection.LeftToRight;
        vwConnectionModeLabel.Text = persian ? "اتصال ویندوز" : "WINDOWS CONNECTION";
        vwManualProbeButton.Content = persian ? "تست واقعی همه لینک‌ها" : "Test all links";
        vwSystemProxyDescription.Text = persian
            ? "ترافیک برنامه‌های ویندوز از پروفایل فعال عبور می‌کند؛ این گزینه برای تست سلامت لینک لازم نیست."
            : "Routes Windows app traffic through the active profile. It is not required for link health checks.";
        vwTunDescription.Text = persian
            ? "تونل ترافیک ویندوز از مسیر پروفایل فعال؛ برای تست مستقل لینک‌ها لازم نیست."
            : "Routes Windows traffic through the active profile. It is not required for independent link tests.";
        vwAppearanceTitle.Text = persian ? "ظاهر و خوانایی" : "Appearance & readability";
        vwFontFamilyLabel.Text = persian ? "نوع فونت" : "Font family";
        vwFontColorLabel.Text = persian ? "رنگ نوشته" : "Text color";
        vwFontSizeLabel.Text = persian ? "اندازه نوشته" : "Text size";
        vwApplyAppearanceButton.Content = persian ? "اعمال و ذخیره" : "Apply and save";
        vwLanguageToggleButton.Content = persian ? "Switch to English" : "تغییر به فارسی";
        vwLanguageToggleButton.ToolTip = persian ? "Restart the app in English" : "برنامه را با زبان فارسی دوباره اجرا می‌کند";
        vwMonitorIntervalLabel.Text = persian ? "فاصله (ثانیه)" : "Interval (sec)";
        vwMonitorIntervalInput.ToolTip = persian ? "فاصله را از ۱ تا ۹۹۹۹۹ ثانیه وارد کن" : "Enter an interval from 1 to 99999 seconds.";
        vwMonitorFailureCombo.ToolTip = persian ? "تعداد خطاهای پیاپی تا ارسال هشدار" : "Consecutive failures before an alert";
        foreach (var item in vwMonitorFailureCombo.Items.OfType<global::System.Windows.Controls.ComboBoxItem>())
        {
            if (int.TryParse(item.Tag?.ToString(), out var failures)) item.Content = persian ? $"{failures} خطای متوالی" : $"{failures} consecutive failures";
        }
        if (_vwMonitorCts == null)
        {
            vwMonitorToggleButton.Content = persian ? "شروع پایش" : "Start monitoring";
            vwMonitorStatus.Text = persian ? "پایش متوقف است" : "Monitoring stopped";
        }
        vwMonitorToggleButton.ToolTip = persian ? "بررسی دوره‌ای لینک‌ها؛ مستقل از System Proxy و TUN" : "Periodically check links, independently of System Proxy and TUN.";
    }

    private async void VwLanguageToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = AppManager.Instance.Config;
            var current = config.UiItem.CurrentLanguage;
            var next = string.Equals(current, "fa", global::System.StringComparison.OrdinalIgnoreCase) ? "en" : "fa";
            config.UiItem.CurrentLanguage = next;
            await ConfigHandler.SaveConfig(config);

            var executablePath = global::System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new global::System.InvalidOperationException("Could not locate the running executable.");

            var restart = global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = "rebootas",
                WorkingDirectory = global::System.AppContext.BaseDirectory,
                UseShellExecute = false
            });
            if (restart == null)
                throw new global::System.InvalidOperationException("Could not start the application again.");

            vwAppearanceStatus.Text = next == "fa"
                ? "زبان فارسی ذخیره شد؛ برنامه در حال راه‌اندازی دوباره است."
                : "English saved. Restarting the application...";
            await AppManager.Instance.AppExitAsync(true);
        }
        catch (Exception ex)
        {
            vwAppearanceStatus.Text = (VwIsPersian() ? "تغییر زبان ناموفق بود: " : "Language change failed: ") + ex.Message;
        }
    }
'@
    $methodMarker = "    private void AddHelpMenuItem()"
    if (-not $code.Contains($methodMarker)) { throw "Could not locate MainWindow method insertion point for appearance controls." }
    $code = $code.Replace($methodMarker, $appearanceMethods + $methodMarker)
    [System.IO.File]::WriteAllText($codePath, $code, [System.Text.UTF8Encoding]::new($false))
}

# Add V2rayWatch-specific per-profile health fields to the upstream profile row model.
$profileModelPath = Join-Path $SourceRoot "v2rayN\ServiceLib\Models\Dto\ProfileItemModel.cs"
if (-not (Test-Path $profileModelPath)) { throw "ProfileItemModel.cs not found; cannot add link health columns." }
$profileModel = [System.IO.File]::ReadAllText($profileModelPath)
if (-not $profileModel.Contains("VwHealth")) {
    $delayValMarker = "    public partial string DelayVal { get; set; }"
    if (-not $profileModel.Contains($delayValMarker)) { throw "Could not locate DelayVal in ProfileItemModel." }
    $healthFields = @'
    [Reactive]
    public partial string VwHealth { get; set; } = "Unknown";

    [Reactive]
    public partial string VwLastChecked { get; set; } = "—";

    public global::System.Collections.ObjectModel.ObservableCollection<string> VwHistory { get; } = new();
'@
    $profileModel = $profileModel.Replace($delayValMarker, $delayValMarker + [Environment]::NewLine + $healthFields.TrimEnd())
    [System.IO.File]::WriteAllText($profileModelPath, $profileModel, [System.Text.UTF8Encoding]::new($false))
}

# Make upstream real-ping runs awaitable and publish each real result to the dashboard monitor.
$profilesVmPath = Join-Path $SourceRoot "v2rayN\ServiceLib\ViewModels\ProfilesViewModel.cs"
if (-not (Test-Path $profilesVmPath)) { throw "ProfilesViewModel.cs not found; cannot wire link monitor." }
$profilesVm = [System.IO.File]::ReadAllText($profilesVmPath)
if (-not $profilesVm.Contains("VwSpeedTestResultReceived")) {
    $fieldMarker = "    private string _serverFilter = string.Empty;"
    if (-not $profilesVm.Contains($fieldMarker)) { throw "Could not locate ProfilesViewModel fields." }
    $monitorFields = @'
    private readonly global::System.Threading.SemaphoreSlim _vwSpeedtestSemaphore = new(1, 1);
    public event Action<SpeedTestResult>? VwSpeedTestResultReceived;
'@
    $profilesVm = $profilesVm.Replace($fieldMarker, $fieldMarker + [Environment]::NewLine + $monitorFields.TrimEnd())

    $oldCallback = @'
        _speedtestService ??= new SpeedtestService(_config, async (SpeedTestResult result) =>
        {
            RxSchedulers.MainThreadScheduler.Schedule(() =>
            {
                _ = SetSpeedTestResult(result);
            });
            await Task.CompletedTask;
        });
        _speedtestService?.RunLoop(actionType, lstSelected);
'@
    $newCallback = @'
        await _vwSpeedtestSemaphore.WaitAsync(cancellationToken);
        try
        {
            _speedtestService ??= new SpeedtestService(_config, async (SpeedTestResult result) =>
            {
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                RxSchedulers.MainThreadScheduler.Schedule(() =>
                {
                    try
                    {
                        SetSpeedTestResult(result).GetAwaiter().GetResult();
                        if (captureVwResults) VwSpeedTestResultReceived?.Invoke(result);
                        completion.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        completion.TrySetException(ex);
                    }
                });
                await completion.Task.ConfigureAwait(false);
            });
            await _speedtestService.RunLoop(actionType, lstSelected, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _vwSpeedtestSemaphore.Release();
        }
'@
    $crlf = ([string][char]13) + [string][char]10
    $lf = [string][char]10
    $profilesVm = $profilesVm.Replace($crlf, $lf).Replace([string][char]13, $lf)
    $oldCallback = $oldCallback.Replace($crlf, $lf).Replace([string][char]13, $lf)
    $newCallback = $newCallback.Replace($crlf, $lf).Replace([string][char]13, $lf)
    if (-not $profilesVm.Contains($oldCallback)) { throw "Could not locate existing SpeedtestService callback block." }
    $profilesVm = $profilesVm.Replace($oldCallback, $newCallback)

    $signatureOld = "public async Task ServerSpeedtest(ESpeedActionType actionType)"
    $signatureNew = "public async Task ServerSpeedtest(ESpeedActionType actionType, CancellationToken cancellationToken = default, bool captureVwResults = false)"
    if (-not $profilesVm.Contains($signatureOld)) { throw "Could not locate ServerSpeedtest method signature." }
    $profilesVm = $profilesVm.Replace($signatureOld, $signatureNew)
    [System.IO.File]::WriteAllText($profilesVmPath, $profilesVm, [System.Text.UTF8Encoding]::new($false))
}
Write-Host "V2rayWatch dashboard inserted into the upstream WPF main window."
