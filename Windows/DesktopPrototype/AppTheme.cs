using System.Windows;
using System.Windows.Media;
namespace V2rayWatch.DesktopPrototype;
public static class AppTheme
{
 private static readonly IReadOnlyDictionary<string,string> Dark=new Dictionary<string,string>{
 ["ThemeWindowBgBrush"]="#071426",["ThemeHeaderBgBrush"]="#0C1A2E",["ThemePanelBrush"]="#0D2038",["ThemePanelAltBrush"]="#102844",
 ["ThemeLineBrush"]="#1D3A5C",["ThemeMutedBrush"]="#93A9C5",["ThemeAccentBrush"]="#12B8C5",["ThemeGoodBrush"]="#45D483",
 ["ThemeBadBrush"]="#F16B75",["ThemeButtonBrush"]="#193B5D",["ThemeButtonBorderBrush"]="#315273",["ThemeButtonHoverBrush"]="#245178",
 ["ThemePrimaryButtonBrush"]="#087EBA",["ThemeGridHeaderBrush"]="#173451",["ThemeGridHeaderTextBrush"]="#C6D7EA",["ThemeGridSelectedBrush"]="#155176",
 ["ThemeGridHoverBrush"]="#12304B",["ThemeLogoBgBrush"]="#123D59",["ThemeAccentTextBrush"]="#69E6DF",["ThemeTextBrush"]="#EAF2FF",
 ["ThemeTextStrongBrush"]="#F4F8FF",["ThemeGridBgBrush"]="#0A1729",["ThemeHistoryNeutralBrush"]="#718096",["ThemeDialogPanelBrush"]="#102440",
 ["ThemeInputBorderBrush"]="#29466E"};
 private static readonly IReadOnlyDictionary<string,string> Light=new Dictionary<string,string>{
 ["ThemeWindowBgBrush"]="#F1F5F9",["ThemeHeaderBgBrush"]="#FFFFFF",["ThemePanelBrush"]="#FFFFFF",["ThemePanelAltBrush"]="#F4F7FB",
 ["ThemeLineBrush"]="#D6E0EA",["ThemeMutedBrush"]="#5B6878",["ThemeAccentBrush"]="#078C98",["ThemeGoodBrush"]="#167A4A",
 ["ThemeBadBrush"]="#C23B4A",["ThemeButtonBrush"]="#E7EEF5",["ThemeButtonBorderBrush"]="#AAB8C8",["ThemeButtonHoverBrush"]="#DCEAF2",
 ["ThemePrimaryButtonBrush"]="#087EBA",["ThemeGridHeaderBrush"]="#E8EEF5",["ThemeGridHeaderTextBrush"]="#28384B",["ThemeGridSelectedBrush"]="#C5E7EB",
 ["ThemeGridHoverBrush"]="#E7F2F5",["ThemeLogoBgBrush"]="#D9F1F3",["ThemeAccentTextBrush"]="#087E87",["ThemeTextBrush"]="#243244",
 ["ThemeTextStrongBrush"]="#142033",["ThemeGridBgBrush"]="#FFFFFF",["ThemeHistoryNeutralBrush"]="#CBD5E1",["ThemeDialogPanelBrush"]="#F1F5F9",
 ["ThemeInputBorderBrush"]="#AAB8C8"};
 public static void Apply(bool light){foreach(var item in light?Light:Dark){var c=(Color)ColorConverter.ConvertFromString(item.Value);Application.Current.Resources[item.Key]=new SolidColorBrush(c);}}
}