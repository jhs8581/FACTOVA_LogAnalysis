using System;
using System.IO;

/// <summary>
/// 애플리케이션 설정을 관리하는 클래스
/// </summary>
public class AppSettings
{
    public string LogFolderPath { get; set; } = "";
    
    // Text Tab GridSplitter 위치
    public double LeftColumnWidth { get; set; } = 1.0;
    public double RightColumnWidth { get; set; } = 1.0;
    public double TopRowHeight { get; set; } = 1.0;
    public double BottomRowHeight { get; set; } = 1.0;
    
    // DataGrid Tab GridSplitter 위치
    public double DataLeftColumnWidth { get; set; } = 1.0;
    public double DataRightColumnWidth { get; set; } = 1.0;
    public double DataTopRowHeight { get; set; } = 1.0;
    public double DataBottomRowHeight { get; set; } = 1.0;

    // Font sizes
    public double TextFontSize { get; set; } = 14.0;
    public double DataGridFontSize { get; set; } = 11.0;
    public double UnifiedLogFontSize { get; set; } = 11.0; // 통합로그 폰트 사이즈

    // Persist view mode: true = 4-panel, false = tab view
    public bool IsFourPanelMode { get; set; } = true;
    // Selected tab index for DataGrid tab control (0..3)
    public int DataGridTabIndex { get; set; } = 0;

    // Text view mode and selected text tab index
    public bool IsTextFourPanelMode { get; set; } = true;
    public int TextTabIndex { get; set; } = 0;

    // Load Options 체크박스 상태
    public bool LoadTextChecked { get; set; } = true;
    public bool LoadDataGridChecked { get; set; } = true;
    public bool LoadExecTimeChecked { get; set; } = false;

    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FACTOVA_LogAnalysis",
        "settings.json");

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"설정 저장 실패: {ex.Message}");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"설정 로드 실패: {ex.Message}");
        }

        return new AppSettings();
    }
}
